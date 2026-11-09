"""Compare solids with cached measurements and exact BREP identity shortcuts."""
import hashlib
import json
import sys
from pathlib import Path

import FreeCAD
import Part


def main():
    before, after, output = [Path(p).resolve() for p in sys.argv[1:4]]
    assert not output.exists(), "Preserve previous comparison evidence"
    progress = output.with_suffix(".progress.json")

    def update(phase, **values):
        progress.write_text(json.dumps({"status": "RUNNING", "phase": phase, **values}), encoding="utf-8")

    records = []
    for path in (before, after):
        update("read", path=str(path))
        shape = Part.read(str(path))
        rows = []
        for index, solid in enumerate(shape.Solids):
            update("measure", path=str(path), index=index)
            rows.append({"shape": solid, "center": solid.CenterOfMass, "volume": solid.Volume,
                         "bounds": solid.BoundBox,
                         "brep": hashlib.sha256(solid.exportBrepToString().encode("utf-8")).hexdigest()})
        records.append(rows)
    remaining = list(records[1])
    checks = []
    for index, expected in enumerate(records[0]):
        if not remaining:
            break
        closest = min(range(len(remaining)), key=lambda i: remaining[i]["center"].distanceToPoint(expected["center"])
                      + abs(remaining[i]["volume"] - expected["volume"]) / 1000)
        actual = remaining.pop(closest)
        delta = max(abs(getattr(expected["bounds"], key) - getattr(actual["bounds"], key))
                    for key in ("XMin", "XMax", "YMin", "YMax", "ZMin", "ZMax"))
        identity = expected["brep"] == actual["brep"]
        update("exact_identity" if identity else "boolean_difference", index=index, total=len(records[0]),
               before_volume_mm3=expected["volume"], after_volume_mm3=actual["volume"], bounds_max_delta_mm=delta)
        missing = 0.0 if identity else expected["shape"].cut(actual["shape"]).Volume
        extra = 0.0 if identity else actual["shape"].cut(expected["shape"]).Volume
        checks.append({"index": index, "missing_mm3": missing, "extra_mm3": extra,
                       "bounds_max_delta_mm": delta, "before_volume_mm3": expected["volume"],
                       "after_volume_mm3": actual["volume"], "exact_brep_identity": identity,
                       "pass": expected["shape"].isValid() and actual["shape"].isValid()
                       and missing < 0.01 and extra < 0.01 and delta < 0.001})
    passed = len(records[0]) > 0 and len(records[0]) == len(records[1]) == len(checks)
    passed = passed and not remaining and all(c["pass"] for c in checks)
    result = {"status": "PASS" if passed else "MISMATCH", "method": "cached_measurements_and_exact_bidirectional_differences",
              "before": str(before), "after": str(after), "before_solids": len(records[0]), "after_solids": len(records[1]),
              "before_sha256": hashlib.sha256(before.read_bytes()).hexdigest(),
              "after_sha256": hashlib.sha256(after.read_bytes()).hexdigest(),
              "tool_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), "checks": checks}
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    progress.write_text(json.dumps({"status": result["status"], "checked": len(checks)}), encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "checks"}, ensure_ascii=False))
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
