# 16029 10/12/14 door engineering handoff

Output level: engineering reference, not released production drawing.

- Generated at: `2026-05-22T12:45:34+08:00`
- Handoff directory: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519`
- Source quality matrix: `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_variant_quality_matrix.json`

## Files to open

| Door count | Status | Recommended SolidWorks file | Open script | Key quality |
| ---: | --- | --- | --- | --- |
| 10 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\16029_1000W_1917H_550D_10door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\open_10door_native_enriched_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |
| 12 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\solidworks_native\16029_1000W_1917H_550D_12door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\solidworks_native\open_12door_native_enriched_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |
| 14 | ready_for_engineering_review | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\open_14door_native_enriched_solidworks.cmd` | bbox X=1000.0mm; STEP=geometry_check_pass; invalid=0; FCStd=PASS; structural=PASS |

## Engineer quick open

- Chinese index: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\ENGINEER_OPEN_INDEX.md`
- Handoff ready summary: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\HANDOFF_READY_SUMMARY.md`
- One-click ready check: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\CHECK_HANDOFF_READY.cmd`
- Quality summary CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_quality_summary.csv`
- Dependency summary CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_dependency_summary.csv`
- Pack-and-Go report: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_PACK_AND_GO_20260522.md`
- Pack-and-Go summary CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_summary.csv`
- Root launchers are available as `open_10door_in_solidworks.cmd`, `open_12door_in_solidworks.cmd`, and `open_14door_in_solidworks.cmd`.
- The launchers start the SolidWorks main window first, try API open on the native enriched `.SLDASM`, and fall back to selecting the native assembly in Explorer.

## Software verification

- Native SolidWorks enriched assemblies, STEP exports, Pack-and-Go folders, and FCStd/STEP quality gates are available for this bundle.
- Native validation covers door left/right placement, right-door 180 degree rotation, door module counts, hinge/lock counts, shelf levels, front-frame crossbars, fixed-module bbox matching, and source dependency presence.
- Native folders are workstation references; `solidworks_pack_and_go` folders are the safer transfer packages.
- SolidWorks open verification: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_OPEN_VERIFICATION_20260520.md`
- Native SolidWorks open verification: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_NATIVE_OPEN_VERIFICATION_20260522.md`

## Use rules

- Prefer the native enriched `.SLDASM` files for SolidWorks engineering review on this workstation.
- Prefer each door folder's `solidworks_pack_and_go` package when moving models to another SolidWorks workstation.
- Keep the listed source dependency paths available when using the non-packaged native SLDASM launchers.
- Use `.stp` files when a neutral exchange file is required.
- Use `.FCStd` as FreeCAD reference after confirming the FCStd integrity status is PASS.
- Do not treat these files as production drawings or released BOM/DXF.
