from __future__ import annotations

import asyncio
import base64
import json
import sqlite3
import subprocess
import sys
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest.mock import patch
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from app import main


async def asgi_request(method: str, path: str, payload: dict | None = None) -> tuple[int, object]:
    body = json.dumps(payload or {}).encode("utf-8")
    messages = []

    async def receive():
        return {"type": "http.request", "body": body, "more_body": False}

    async def send(message):
        messages.append(message)

    request_path, _, query = path.partition("?")
    scope = {
        "type": "http", "asgi": {"version": "3.0", "spec_version": "2.4"},
        "http_version": "1.1", "method": method, "scheme": "http",
        "path": request_path, "raw_path": request_path.encode(),
        "query_string": query.encode(), "root_path": "",
        "headers": [(b"content-type", b"application/json")],
        "client": ("127.0.0.1", 1), "server": ("127.0.0.1", 1),
    }
    await main.app(scope, receive, send)
    status = next(message["status"] for message in messages if message["type"] == "http.response.start")
    data = b"".join(message.get("body", b"") for message in messages if message["type"] == "http.response.body")
    try:
        return status, json.loads(data)
    except (UnicodeDecodeError, json.JSONDecodeError):
        return status, data


class ApiTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="studio-api-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        # Every configured path is redirected before any endpoint is invoked.
        for name, value in vars(main).copy().items():
            if name.isupper() and isinstance(value, Path):
                path = self.root / name.lower()
                patcher = patch.object(main, name, path)
                patcher.start()
                self.addCleanup(patcher.stop)
        for name, path in {
            "ROOT_DIR": self.root,
            "DB_PATH": self.root / "studio.sqlite",
            "WORKER_LOG_DIR": self.root / "logs",
            "GENERATED_MODEL_DIR": self.root / "models",
            "MANUAL_RUN_DIR": self.root / "manual",
            "SOLIDWORKS_RUN_LOCK_PATH": self.root / "logs" / "solidworks.lock",
            "FREECAD_RUN_LOCK_PATH": self.root / "logs" / "freecad.lock",
        }.items():
            patcher = patch.object(main, name, path)
            patcher.start()
            self.addCleanup(patcher.stop)
        main.init_db()
        main.WORKER_LOG_DIR.mkdir()
        self.payload = {
            "capability_id": "audit_reference", "capability_title": "Audit reference",
            "product_type": "fixture", "module": "fixture", "maturity": "engineering_reference",
            "command": 'FreeCADCmd.exe "scripts/audit.py"', "evidence": ["fixture"],
        }

    def request(self, method, path, payload=None):
        return asyncio.run(asgi_request(method, path, payload))

    def create_task(self, runner="freecad"):
        status, task = self.request("POST", "/api/generation-tasks", {**self.payload, "cad_runner": runner})
        self.assertEqual(status, 201)
        return task

    def ready_task(self, runner="freecad"):
        task = self.create_task(runner)
        result = main.DryRunResult(
            task_id=task["id"], status="ready_to_run", checks=[],
            log_path=str(main.WORKER_LOG_DIR / "dry-run.json"), checked_at=main.now_iso(),
        )
        with patch.object(main, "build_dry_run", return_value=result):
            status, task = self.request("POST", f'/api/generation-tasks/{task["id"]}/dry-run')
        self.assertEqual(status, 200)
        return task

    def completed_result(self, task):
        return main.WorkerExecutionResult(
            task_id=task.id, status="completed_reference", cad_runner=task.cad_runner,
            started_at=main.now_iso(), finished_at=main.now_iso(), command=[], cwd=str(self.root),
            output_dir=str(main.GENERATED_MODEL_DIR / task.id),
            log_path=str(main.WORKER_LOG_DIR / f"{task.id}.json"), exit_code=0, message="fixture complete",
        )

    def test_health_list_validation_and_missing_task(self):
        status, data = self.request("GET", "/health")
        self.assertEqual((status, data["status"]), (200, "ok"))
        self.assertEqual(data["database"], str(main.DB_PATH))
        self.assertEqual(self.request("GET", "/api/generation-tasks"), (200, []))
        self.assertEqual(self.request("GET", "/api/generation-tasks?limit=0")[0], 422)
        self.assertEqual(self.request("GET", "/api/generation-tasks/missing")[0], 404)

    def test_database_connections_commit_close_and_rollback(self):
        with main.connect() as connection:
            connection.execute("CREATE TABLE fixture (value TEXT)")
            connection.execute("INSERT INTO fixture VALUES ('committed')")
        with self.assertRaises(sqlite3.ProgrammingError):
            connection.execute("SELECT 1")
        with self.assertRaises(RuntimeError):
            with main.connect() as connection:
                connection.execute("INSERT INTO fixture VALUES ('rolled_back')")
                raise RuntimeError("fixture rollback")
        with main.connect() as connection:
            self.assertEqual([row[0] for row in connection.execute("SELECT value FROM fixture")], ["committed"])

    def test_creation_cannot_supply_execution_state(self):
        for status in ("ready_to_run", "running", "completed_reference", "requires_manual_run", "failed_worker"):
            with self.subTest(status=status):
                response_status, _ = self.request("POST", "/api/generation-tasks", {**self.payload, "status": status})
                self.assertIn(response_status, (400, 422))
        self.assertEqual(self.request("GET", "/api/generation-tasks"), (200, []))

    def test_disabled_generator_cannot_use_alternative_status(self):
        status, _ = self.request("POST", "/api/generation-tasks", {
            **self.payload, "command": "not_enabled", "status": "blocked_pending_evidence",
        })
        self.assertEqual(status, 400)

    def test_all_16029_creation_routes_are_retired(self):
        for capability in (
            "locker_16029_native_assistance", "locker_16029_regression", "locker_16029_door_panel",
            "locker_16029_v43_internal_sheetmetal_current", "locker_16029_gold_variable_current",
        ):
            with self.subTest(capability=capability):
                status, _ = self.request("POST", "/api/generation-tasks", {**self.payload, "capability_id": capability})
                self.assertEqual(status, 410)

    def test_persisted_16029_tasks_cannot_run_retired_workers(self):
        task = self.ready_task("solidworks")
        with main.connect() as connection:
            connection.execute("UPDATE generation_tasks SET capability_id = ? WHERE id = ?", (
                "locker_16029_v43_internal_sheetmetal_current", task["id"],
            ))
        with patch.object(main, "build_dry_run") as dry_run, patch.object(main, "run_solidworks_manual_package") as worker:
            for action in ("dry-run", "execute", "run-solidworks-package"):
                with self.subTest(action=action):
                    self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/{action}')[0], 410)
            dry_run.assert_not_called()
            worker.assert_not_called()
        self.assertEqual(self.request("GET", f'/api/generation-tasks/{task["id"]}')[0], 200)

    def test_draft_cannot_execute_without_dry_run(self):
        task = self.create_task()
        with patch.object(main, "run_freecad_worker") as worker:
            self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/execute')[0], 409)
            worker.assert_not_called()

    def test_running_task_cannot_be_reset_by_dry_run(self):
        task = self.ready_task()
        with main.connect() as connection:
            connection.execute("UPDATE generation_tasks SET status = 'running' WHERE id = ?", (task["id"],))
        result = main.DryRunResult(
            task_id=task["id"], status="ready_to_run", checks=[],
            log_path=str(main.WORKER_LOG_DIR / "dry-run.json"), checked_at=main.now_iso(),
        )
        with patch.object(main, "build_dry_run", return_value=result) as dry_run:
            self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/dry-run')[0], 409)
            dry_run.assert_not_called()
        self.assertEqual(self.request("GET", f'/api/generation-tasks/{task["id"]}')[1]["status"], "running")

    def test_two_simultaneous_executes_claim_task_once(self):
        task = self.ready_task()
        original_fetch = main.fetch_task_or_404
        barrier = threading.Barrier(2)

        def read_same_ready_task(task_id):
            row = original_fetch(task_id)
            barrier.wait(timeout=5)
            return row

        with patch.object(main, "fetch_task_or_404", side_effect=read_same_ready_task), \
                patch.object(main, "run_freecad_worker", side_effect=self.completed_result) as worker:
            with ThreadPoolExecutor(max_workers=2) as executor:
                futures = [executor.submit(self.request, "POST", f'/api/generation-tasks/{task["id"]}/execute') for _ in range(2)]
                responses = [future.result(timeout=10)[0] for future in futures]
            self.assertEqual(sorted(responses), [200, 409])
            self.assertEqual(worker.call_count, 1)
        self.assertEqual(self.request("GET", f'/api/generation-tasks/{task["id"]}')[1]["status"], "completed_reference")

    def test_worker_setup_failure_does_not_leave_task_running(self):
        task = self.ready_task()
        with patch.object(main, "run_freecad_worker", side_effect=main.HTTPException(status_code=409, detail="fixture missing script")):
            self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/execute')[0], 409)
        self.assertEqual(self.request("GET", f'/api/generation-tasks/{task["id"]}')[1]["status"], "failed_worker")

    def test_dry_run_cannot_overwrite_concurrent_execution(self):
        task = self.ready_task()

        def start_worker_during_dry_run(current):
            main.claim_generation_task(current)
            return main.DryRunResult(
                task_id=current.id, status="ready_to_run", checks=[],
                log_path=str(main.WORKER_LOG_DIR / "dry-run.json"), checked_at=main.now_iso(),
            )

        with patch.object(main, "build_dry_run", side_effect=start_worker_during_dry_run):
            self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/dry-run')[0], 409)
        self.assertEqual(self.request("GET", f'/api/generation-tasks/{task["id"]}')[1]["status"], "running")

    def test_freecad_timeout_with_bytes_is_recorded(self):
        task = self.ready_task()
        script = self.root / "audit.py"
        script.touch()
        timeout = subprocess.TimeoutExpired(["fixture"], 1, output=b"partial output\xff", stderr=b"timeout detail")
        with patch.object(main, "resolve_freecad_cmd", return_value=(True, "FreeCADCmd.exe")), \
                patch.object(main, "resolve_script", return_value=(True, str(script))), \
                patch.object(main.subprocess, "run", side_effect=timeout):
            status, result = self.request("POST", f'/api/generation-tasks/{task["id"]}/execute')
        self.assertEqual((status, result["status"]), (200, "failed_worker"))
        self.assertIn("partial output", Path(result["execution_result"]["stdout_path"]).read_text(encoding="utf-8"))
        self.assertFalse(main.FREECAD_RUN_LOCK_PATH.exists())

    def test_solidworks_timeout_with_bytes_is_recorded(self):
        task = self.ready_task("solidworks")
        output_dir = main.MANUAL_RUN_DIR / task["id"]
        output_dir.mkdir(parents=True)
        script = output_dir / "run-solidworks-worker.ps1"
        script.touch()
        timeout = subprocess.TimeoutExpired(["fixture"], 1, output=b"partial", stderr=b"timeout")
        with patch.object(main, "ensure_solidworks_run_script", return_value=(output_dir, script)), \
                patch.object(main, "solidworks_manual_command", return_value=["cscript.exe", "//Nologo", "audit.js"]), \
                patch.object(main, "solidworks_run_guard_status", return_value=(True, "fixture")), \
                patch.object(main.subprocess, "run", side_effect=timeout):
            status, result = self.request("POST", f'/api/generation-tasks/{task["id"]}/run-solidworks-package')
        self.assertEqual((status, result["status"]), (200, "failed_worker"))
        self.assertFalse(main.SOLIDWORKS_RUN_LOCK_PATH.exists())

    def extraction_run(self):
        output_dir = main.RULE_EXTRACTION_DIR / "RULE-FIXTURE"
        output_dir.mkdir(parents=True)
        script = output_dir / "run.ps1"
        script.touch()
        result = main.RuleExtractionResult(
            id="RULE-FIXTURE", template_id="fixture", template_title="fixture", assembly_path="fixture.SLDASM",
            status="requires_solidworks_run", created_at=main.now_iso(), updated_at=main.now_iso(), output_dir=str(output_dir),
            run_script_path=str(script), message="fixture",
        )
        main.write_rule_extraction_result(result)
        return result

    def test_rule_extraction_launch_failure_is_recorded(self):
        run = self.extraction_run()
        with patch.object(main, "solidworks_run_guard_status", return_value=(True, "fixture")), \
                patch.object(main.subprocess, "run", side_effect=FileNotFoundError("fixture powershell missing")):
            status, result = self.request("POST", f"/api/template-rule-extractions/{run.id}/run")
        self.assertEqual((status, result["status"]), (200, "failed"))
        self.assertFalse(main.SOLIDWORKS_RUN_LOCK_PATH.exists())

    def test_rule_extraction_honors_shared_solidworks_lock(self):
        run = self.extraction_run()
        main.SOLIDWORKS_RUN_LOCK_PATH.write_text('{"task_id":"another-task"}', encoding="utf-8")
        with patch.object(main, "active_solidworks_automation_processes", return_value=[]), \
                patch.object(main.subprocess, "run") as worker:
            self.assertEqual(self.request("POST", f"/api/template-rule-extractions/{run.id}/run")[0], 409)
            worker.assert_not_called()
        self.assertEqual(main.read_rule_extraction_result(Path(run.output_dir)).status, "requires_solidworks_run")

    def test_rule_extraction_timeout_releases_lock(self):
        run = self.extraction_run()
        timeout = subprocess.TimeoutExpired(["fixture"], 1, output=b"partial", stderr=b"timeout")
        with patch.object(main, "solidworks_run_guard_status", return_value=(True, "fixture")), \
                patch.object(main.subprocess, "run", side_effect=timeout):
            status, result = self.request("POST", f"/api/template-rule-extractions/{run.id}/run")
        self.assertEqual((status, result["status"]), (200, "failed"))
        self.assertFalse(main.SOLIDWORKS_RUN_LOCK_PATH.exists())

    def test_rule_extraction_rejects_traversal_before_reading_outputs(self):
        run = self.extraction_run()
        outside = main.RULE_EXTRACTION_DIR.parent / "escaped"
        outside.mkdir()
        main.rule_extraction_summary_path(outside).write_text(run.model_dump_json(), encoding="utf-8")
        with patch.object(main, "solidworks_run_guard_status", return_value=(True, "fixture")), \
                patch.object(main.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, "", "fixture")):
            status, _ = self.request("POST", "/api/template-rule-extractions/..\\escaped/run")
        self.assertIn(status, (400, 403, 404))

    def test_template_assembly_must_be_a_file(self):
        directory = main.TEMPLATE_ASSET_ROOT / "fixture.SLDASM"
        directory.mkdir(parents=True)
        with patch.object(main, "shutil") as programs:
            status, _ = self.request("POST", "/api/template-rule-extractions", {
                "template_id": "fixture", "template_title": "fixture", "assembly_path": str(directory),
            })
            programs.which.assert_not_called()
        self.assertEqual(status, 400)

    def test_download_contains_nested_outputs_but_not_worker_wrappers(self):
        task = self.ready_task()
        output_dir = main.GENERATED_MODEL_DIR / task["id"]
        (output_dir / "nested").mkdir(parents=True)
        (output_dir / "nested" / "model.step").write_text("fixture model")
        (output_dir / "freecad_worker_entry.py").write_text("fixture wrapper")
        with patch.object(main, "run_freecad_worker", side_effect=self.completed_result):
            self.assertEqual(self.request("POST", f'/api/generation-tasks/{task["id"]}/execute')[0], 200)
        status, data = self.request("GET", f'/api/generation-tasks/{task["id"]}/download')
        self.assertEqual(status, 200)
        with tempfile.TemporaryFile() as file:
            file.write(data)
            file.seek(0)
            with ZipFile(file) as archive:
                self.assertEqual(archive.namelist(), ["nested/model.step"])
                self.assertEqual(archive.read("nested/model.step"), b"fixture model")

    def test_upload_validation_duplicates_and_path_allowlist(self):
        self.assertEqual(self.request("POST", "/api/local-actions/open-path", {"path": "relative"})[0], 400)
        outside = self.root / "outside.txt"
        outside.touch()
        self.assertEqual(self.request("POST", "/api/local-actions/open-path", {"path": str(outside)})[0], 403)
        file = {"name": "fixture.dxf", "content_base64": base64.b64encode(b"fixture").decode(), "size_bytes": 7}
        status, record = self.request("POST", "/api/drawing-sheetmetal-intake", {"files": [file, file]})
        self.assertEqual(status, 201)
        self.assertEqual([item["file_name"] for item in record["files"]], ["fixture.dxf", "fixture_2.dxf"])
        self.assertEqual(self.request("POST", "/api/drawing-sheetmetal-intake", {"files": [{**file, "size_bytes": 2}]})[0], 400)
        self.assertEqual(self.request("POST", "/api/drawing-sheetmetal-intake", {"files": [{**file, "name": "fixture.exe"}]})[0], 400)
        self.assertEqual(self.request("POST", "/api/drawing-sheetmetal-intake", {"files": [{**file, "content_base64": "invalid"}]})[0], 400)


if __name__ == "__main__":
    unittest.main()
