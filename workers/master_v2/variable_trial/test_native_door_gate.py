"""Exercise the compiled job entry point with cancellation before CAD activation."""
import argparse
import copy
import hashlib
import json
import subprocess
import uuid
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / "data/master_v2/variable_trial_20261112"
SOURCE = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--toolchain", type=Path, default=DATA / "toolchain-current.json")
    args = parser.parse_args()
    tool = json.loads(args.toolchain.read_text(encoding="utf-8-sig"))
    exe = Path(tool["exe"])
    assert sha(exe) == tool["sha256"]
    old = DATA / "runs/2026-11-13T01-00-00-839Z-T2-outputs-dxf-e377825c/job.json"
    baseline = json.loads(old.read_text(encoding="utf-8-sig"))
    target = DATA / ("analysis-native-door-gate-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
    target.mkdir(exist_ok=False)
    cancellation = target / "cancel.json"
    cancellation.write_text('{"test":"pre-CAD cancellation"}', encoding="utf-8")
    baseline["cancellationPath"] = str(cancellation)
    cases = []

    def case(name, mutate, expected):
        job = copy.deepcopy(baseline)
        job["resultPath"] = str(target / (name + "-native.json"))
        mutate(job)
        path = target / (name + "-job.json")
        path.write_text(json.dumps(job, ensure_ascii=False), encoding="utf-8")
        result = subprocess.run([str(exe), str(path)], capture_output=True, encoding="utf-8", errors="replace", timeout=30)
        (target / (name + "-stdout.txt")).write_text(result.stdout, encoding="utf-8")
        (target / (name + "-stderr.txt")).write_text(result.stderr, encoding="utf-8")
        native = json.loads(Path(job["resultPath"]).read_text(encoding="utf-8-sig"))
        errors = [row.get("error", "") for row in native["events"] if row.get("inspection") == "failure"]
        passed = (result.returncode == 1 and native["cadPid"] == 0 and not any(row.get("inspection") == "cad_owned" for row in native["events"])
                  and len(errors) == 1 and expected in errors[0] and native["events"][-1]["cadExited"] is True)
        cases.append({"name": name, "pass": passed, "exitCode": result.returncode, "cadPid": native["cadPid"], "expectedError": expected,
                      "jobSha256": sha(path), "nativeSha256": sha(Path(job["resultPath"]))})

    case("valid-input-cancelled", lambda job: None, "TRIAL_CANCELLATION_REQUESTED")
    case("changed-process", lambda job: job["process"].update(bendDeductionMm=1.5), "DOOR_FIXED_PROCESS_INPUT_REQUIRED")
    case("changed-variable", lambda job: job["doors"][0]["variables"].update(BEND_DED=1.5), "DOOR_VARIABLE_PROCESS_CONFLICT")
    case("illegal-size", lambda job: job["doors"][0]["variables"].update(DOOR_H=-1), "DOOR_INPUT_OUTSIDE_PILOT")
    case("string-size", lambda job: job["doors"][0]["variables"].update(DOOR_W="437"), "DOOR_NUMERIC_INPUT_REQUIRED")
    case("hand-conflict", lambda job: job["doors"][0]["properties"].update(hand="R"), "DOOR_COMPILED_PROPERTIES_CONFLICT")
    case("duplicate-identity", lambda job: job["doors"].__setitem__(1, copy.deepcopy(job["doors"][0])), "DOOR_SPECIFICATION_CONFLICT")
    case("input-hash", lambda job: job["inputVersions"][0].update(sha256="0" * 64), "DOOR_INPUT_FILE_HASH_CHANGED")
    case("release-forbidden", lambda job: job.update(production_release=True), "DOOR_PRODUCTION_RELEASE_FORBIDDEN")
    summary = {"schema": "16029-door-native-gate-test/v1", "toolchain": str(args.toolchain), "toolchainSha256": sha(args.toolchain),
               "exeSha256": sha(exe), "sourceSha256": sha(SOURCE / "VariableTrial.cs"), "cases": cases,
               "pass": all(row["pass"] for row in cases), "CADStarted": False, "production_release": False}
    (target / "test-result.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"directory": str(target), "pass": summary["pass"], "cases": cases}, ensure_ascii=False))
    raise SystemExit(0 if summary["pass"] else 1)


if __name__ == "__main__":
    main()
