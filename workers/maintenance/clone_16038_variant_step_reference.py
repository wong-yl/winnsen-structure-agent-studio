from __future__ import annotations

import argparse
import json
import os
import shutil
from datetime import datetime, timezone
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[2]
TEMPLATE_ROOT = Path(
    os.environ.get(
        "STUDIO_PARAMETRIC_TEMPLATE_ROOT",
        r"C:\Users\Administrator\Desktop\参数化模板素材",
    )
)
OUTPUT_DIR = Path(os.environ.get("LOCKER_OUT_DIR", ROOT_DIR / "workers" / "generated_models" / "manual_16038_step"))


VARIANTS = {
    4: {
        "label": "16038 4-door full assembly STEP",
        "step": TEMPLATE_ROOT
        / "16038 寄存柜XY(标准组合式 1917×1000×550)"
        / "16038 寄存柜XY(标准组合式 1917×1000×550)"
        / "4门"
        / "5.STP-4门"
        / "标准洗衣寄存柜(总装配)-4门.STEP",
        "role_evidence": ROOT_DIR
        / "workers"
        / "variant_step_evidence"
        / "16038"
        / "4door"
        / "4door_step_component_role_bindings.json",
    },
    7: {
        "label": "16038 7-door full assembly STEP",
        "step": TEMPLATE_ROOT
        / "16038 寄存柜XY(标准组合式 1917×1000×550)"
        / "4.step 简化图"
        / "标准洗衣寄存柜(总装配).STEP",
        "role_evidence": ROOT_DIR
        / "workers"
        / "variant_step_evidence"
        / "16038"
        / "7door_simplified"
        / "7door_simplified_step_component_role_bindings.json",
    },
    8: {
        "label": "16038 8-door full assembly STEP",
        "step": TEMPLATE_ROOT
        / "16038 寄存柜XY(标准组合式 1917×1000×550)"
        / "16038 寄存柜XY(标准组合式 1917×1000×550)"
        / "8门"
        / "4.step -8门"
        / "标准洗衣寄存柜(总装配)-8门.STEP",
        "role_evidence": ROOT_DIR
        / "workers"
        / "variant_step_evidence"
        / "16038"
        / "8door"
        / "8door_step_component_role_bindings.json",
    },
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_role_summary(path: Path) -> dict[str, object]:
    if not path.exists():
        return {"available": False, "path": str(path)}
    data = json.loads(path.read_text(encoding="utf-8"))
    bindings = data.get("bindings", [])
    role_counts: dict[str, int] = {}
    for row in bindings:
        role = str(row.get("role") or "unknown")
        role_counts[role] = role_counts.get(role, 0) + 1
    return {
        "available": True,
        "path": str(path),
        "bindingCount": len(bindings),
        "roleCounts": role_counts,
    }


def write_report(output_dir: Path, name: str, variant: dict[str, object], copied_step: Path, role_summary: dict[str, object]) -> None:
    report = output_dir / f"{name}_reference_report.md"
    report.write_text(
        "\n".join(
            [
                "# 16038 STEP Reference Clone",
                "",
                f"- Generated at: `{utc_now()}`",
                f"- Variant: `{variant['label']}`",
                f"- Source STEP: `{variant['step']}`",
                f"- Output STEP: `{copied_step}`",
                f"- Role evidence: `{role_summary.get('path')}`",
                f"- Role bindings: `{role_summary.get('bindingCount', 0)}`",
                "",
                "## Notes",
                "",
                "- This is a neutral STEP / FreeCAD migration reference for the 16038 same-size family.",
                "- SolidWorks remains the engineering-mainline generator for native `.SLDASM` output.",
                "- 12/12 is currently available as a SolidWorks native door module, not as a full-cabinet STEP clone.",
                "",
            ]
        ),
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--door-count", type=int, required=True)
    args = parser.parse_args()

    if args.door_count not in VARIANTS:
        supported = ", ".join(str(value) for value in sorted(VARIANTS))
        raise SystemExit(f"16038 FreeCAD/STEP reference supports full assemblies for door counts: {supported}.")

    variant = VARIANTS[args.door_count]
    source_step = Path(variant["step"])
    if not source_step.exists():
        raise FileNotFoundError(source_step)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    name = f"locker_16038_{args.door_count}door_step_reference"
    copied_step = OUTPUT_DIR / f"{name}.step"
    shutil.copy2(source_step, copied_step)

    role_summary = load_role_summary(Path(variant["role_evidence"]))
    summary_path = OUTPUT_DIR / f"{name}_summary.json"
    summary_path.write_text(
        json.dumps(
            {
                "generatedAt": utc_now(),
                "doorCount": args.door_count,
                "variant": variant["label"],
                "sourceStep": str(source_step),
                "outputStep": str(copied_step),
                "roleEvidence": role_summary,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    write_report(OUTPUT_DIR, name, variant, copied_step, role_summary)
    print(f"step={copied_step}")
    print(f"summary={summary_path}")
    print(f"role_bindings={role_summary.get('bindingCount', 0)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
