# 16029 10/12/14 door engineering handoff

Output level: engineering reference, not released production drawing.

- Generated at: `2026-05-21T18:53:22+08:00`
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
- Root launchers are available as `open_10door_in_solidworks.cmd`, `open_12door_in_solidworks.cmd`, and `open_14door_in_solidworks.cmd`.
- The launchers start the SolidWorks main window first, try API open on the native enriched `.SLDASM`, and fall back to selecting the native assembly in Explorer.

## Software verification

- Native SolidWorks enriched assemblies, STEP exports, and FCStd/STEP quality gates are available for this bundle.
- This is a native engineering-reference package, not a true independent Pack-and-Go release yet.
- SolidWorks open verification: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_OPEN_VERIFICATION_20260520.md`

## Use rules

- Prefer the native enriched `.SLDASM` files for SolidWorks engineering review on this workstation.
- Keep the listed source dependency paths available until true Pack-and-Go is implemented.
- Use `.stp` files when a neutral exchange file is required.
- Use `.FCStd` as FreeCAD reference after confirming the FCStd integrity status is PASS.
- Do not treat these files as production drawings or released BOM/DXF.
