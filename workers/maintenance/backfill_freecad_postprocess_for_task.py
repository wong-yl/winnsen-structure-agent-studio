from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from services.api.app.main import (  # noqa: E402
    connect,
    fetch_task_or_404,
    freecad_postprocess_quality,
    now_iso,
    row_to_task,
    run_16029_freecad_postprocess,
)


def freecad_tasks_needing_backfill(limit: int) -> list[str]:
    with connect() as connection:
        rows = connection.execute(
            """
            SELECT id, execution_json
            FROM generation_tasks
            WHERE cad_runner = 'freecad'
              AND capability_id = 'locker_16029_regression'
              AND status = 'completed_reference'
              AND execution_json IS NOT NULL
            ORDER BY created_at DESC
            LIMIT ?
            """,
            (limit,),
        ).fetchall()

    task_ids: list[str] = []
    for row in rows:
        try:
            execution = json.loads(row["execution_json"])
        except json.JSONDecodeError:
            task_ids.append(row["id"])
            continue
        if not execution.get("freecad_quality_status"):
            task_ids.append(row["id"])
    return task_ids


def append_existing_output(outputs: list[str], path: str | None) -> None:
    if not path:
        return
    normalized = str(Path(path))
    if Path(normalized).exists() and normalized not in outputs:
        outputs.append(normalized)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Backfill FreeCAD postprocess quality fields for one completed generation task."
    )
    parser.add_argument("task_id", nargs="?", help="Generation task id to backfill.")
    parser.add_argument("--all-missing", action="store_true", help="Backfill recent completed FreeCAD tasks that lack quality fields.")
    parser.add_argument("--limit", type=int, default=20, help="Maximum recent tasks to scan when using --all-missing.")
    parser.add_argument("--force", action="store_true", help="Run postprocess even when the task already has quality fields.")
    args = parser.parse_args()

    task_ids = [args.task_id] if args.task_id else []
    if args.all_missing:
        task_ids = freecad_tasks_needing_backfill(args.limit)
        if not task_ids:
            print(json.dumps([], ensure_ascii=False, indent=2))
            return 0
    if not task_ids or any(task_id is None for task_id in task_ids):
        parser.error("Provide a task_id or use --all-missing.")

    results: list[dict[str, object]] = []
    exit_code = 0
    for task_id in task_ids:
        try:
            results.append(backfill_one_task(str(task_id), force=args.force))
        except RuntimeError as error:
            exit_code = 2
            results.append({"task_id": task_id, "status": "skipped", "reason": str(error)})

    print(json.dumps(results[0] if len(results) == 1 else results, ensure_ascii=False, indent=2))
    return exit_code


def backfill_one_task(task_id: str, force: bool = False) -> dict[str, object]:
    task = row_to_task(fetch_task_or_404(task_id))
    if task.cad_runner != "freecad":
        raise RuntimeError("task is not a FreeCAD task")
    if task.capability_id != "locker_16029_regression":
        raise RuntimeError("task is not a 16029 full-cabinet FreeCAD task")
    if task.execution_result is None:
        raise RuntimeError("task has no execution_result")
    if task.execution_result.freecad_quality_status and not force:
        return {
            "task_id": task.id,
            "status": task.execution_result.freecad_quality_status,
            "summary": task.execution_result.freecad_quality_summary,
            "report": task.execution_result.freecad_quality_report,
            "skipped": "already_has_quality_status",
        }

    output_dir = Path(task.execution_result.output_dir)
    postprocess = run_16029_freecad_postprocess(task, output_dir)
    quality_status, quality_summary, quality_report = freecad_postprocess_quality(postprocess)
    if not quality_status:
        raise RuntimeError("16029 FreeCAD postprocess did not produce a quality status")

    execution = task.execution_result
    execution.freecad_quality_status = quality_status
    execution.freecad_quality_summary = quality_summary
    execution.freecad_quality_report = quality_report
    if quality_summary:
        execution.message = quality_summary

    outputs = list(execution.outputs)
    append_existing_output(outputs, quality_report)
    if isinstance(postprocess, dict):
        step_geometry_check = postprocess.get("step_geometry_check", {})
        fcstd_integrity_check = postprocess.get("fcstd_integrity_check", {})
        if isinstance(step_geometry_check, dict):
            append_existing_output(outputs, step_geometry_check.get("report"))
        if isinstance(fcstd_integrity_check, dict):
            append_existing_output(outputs, fcstd_integrity_check.get("report"))
    execution.outputs = outputs

    if execution.log_path:
        Path(execution.log_path).write_text(execution.model_dump_json(indent=2), encoding="utf-8")

    updated_at = now_iso()
    with connect() as connection:
        connection.execute(
            """
            UPDATE generation_tasks
            SET execution_json = ?, worker_log_path = ?, updated_at = ?
            WHERE id = ?
            """,
            (execution.model_dump_json(), execution.log_path, updated_at, task.id),
        )

    return {
        "task_id": task.id,
        "status": quality_status,
        "summary": quality_summary,
        "report": quality_report,
        "output_count": len(outputs),
    }


if __name__ == "__main__":
    raise SystemExit(main())
