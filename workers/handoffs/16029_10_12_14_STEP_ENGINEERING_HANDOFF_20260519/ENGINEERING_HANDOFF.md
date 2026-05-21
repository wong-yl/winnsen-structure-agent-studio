# 16029 10/12/14 door engineering handoff

Output level: engineering reference, not released production drawing.

- Generated at: `2026-05-20T17:12:10+08:00`
- Handoff directory: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519`
- Source quality matrix: `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_variant_quality_matrix.json`

## Files to open

| Door count | Status | Recommended SolidWorks file | Open script | Key quality |
| ---: | --- | --- | --- | --- |
| 10 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\16029_1000W_1917H_550D_10door_solidworks_import.stp` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\open_10door_step_in_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |
| 12 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\16029_1000W_1917H_550D_12door_solidworks_import.stp` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\open_12door_step_in_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |
| 14 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_solidworks_import.stp` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\open_14door_step_in_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |

## Engineer quick open

- Chinese index: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\ENGINEER_OPEN_INDEX.md`
- Root launchers are available as `open_10door_in_solidworks.cmd`, `open_12door_in_solidworks.cmd`, and `open_14door_in_solidworks.cmd`.
- The launchers start the SolidWorks main window first, try API open, and fall back to selecting the STEP file in Explorer.

## Software verification

- STEP and FCStd quality gates are available for this bundle.
- SolidWorks API/visual-open automation is not production-ready until a visible document open is confirmed.
- SolidWorks open verification: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_OPEN_VERIFICATION_20260520.md`

## Use rules

- Prefer the `.stp` files for SolidWorks engineering review.
- Use `.FCStd` as FreeCAD reference after confirming the FCStd integrity status is PASS.
- Do not treat these files as production drawings or released BOM/DXF.
