# 16029 14 door engineering handoff

Output level: engineering reference, not released production drawing.

## Recommended file

- Native SolidWorks enriched reference: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM`
- Native SolidWorks launcher: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\open_14door_native_enriched_solidworks.cmd`
- SolidWorks review: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_solidworks_import.stp`
- FreeCAD reference: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_freecad_reference.FCStd`
- Status: `PASS_READY_FOR_ENGINEERING_REVIEW`
- Interpretation: Use the native SolidWorks enriched assembly first; STEP and FCStd remain neutral/open-source review backups.

## Quality snapshot

- bbox X: `1000.0` mm
- verify rows: `418`
- STEP geometry: `geometry_check_pass`
- STEP invalid shapes: `0`
- FCStd integrity: `PASS`
- Structural rule audit: `PASS`
- Door height / pitch: `254.429` / `261.429` mm
- STEP size: `35.439` MB
- FCStd size: `3.962` MB

## Open scripts

- `open_14door_step_in_solidworks.cmd`
- `open_14door_reference_in_freecad.cmd`

## Evidence

- report: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\locker_16029_14door_rule_driven_report.md`
- verify csv: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\locker_16029_14door_rule_driven_verify.csv`
- STEP geometry json: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\freecad_geometry_check.json`
- FCStd integrity report: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\locker_16029_14door_rule_driven_geometry_integrity.md`
- structural rule audit: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\locker_16029_structural_rule_audit.md`

## Native SolidWorks package

- package dir: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native`
- dependency manifest: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\native_dependency_manifest.csv`
- dependency rows: `10/10`
- boundary: `native engineering reference package; not independent Pack-and-Go`
