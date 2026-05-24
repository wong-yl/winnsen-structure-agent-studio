# 16029 14 door native SolidWorks enriched reference

Output level: engineering reference, not released production drawing.

## Open first

- Native SolidWorks assembly: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM`
- Safe launcher: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\open_14door_native_enriched_solidworks.cmd`
- Neutral STEP from the same native assembly: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.step`

## Validation

- Validation report: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\solidworks_16029_enriched_14door_matrix_validation.md`
- Validation data: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\solidworks_16029_enriched_14door_matrix_validation.csv`
- STEP bbox CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_14door_enriched_v2_step_bbox.csv`
- Builder result JSON: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_14door_enriched_v2_result.json`
- Placement TSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_14door_enriched_v2_placements.tsv`
- Dependency manifest: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\native_dependency_manifest.csv`

## Native validation summary

- Result: `PASS`
- Checks: `43/43`
- Door modules: `14` total, `7` left, `7` right
- Door hardware: weld `14`, panel `14`, hinge `14`, lock hook `14`
- Shelf / crossbar: shelf `12`, crossbar `12`

## Boundary

- This folder collects the current native enhanced reference and its evidence in one place.
- This is not a true independent Pack-and-Go release yet. The dependency manifest lists top-level source references that must stay available on this workstation.
- The launcher preflights the dependency manifest before opening SolidWorks and writes a missing-dependency report if references are not available.
- Use this before the older STEP-only handoff when reviewing in SolidWorks 2025.
