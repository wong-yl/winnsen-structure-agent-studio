# 16029 FCStd Geometry Integrity Audit

- model: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260520042611-E8E6F4\locker_16029_12door_rule_driven.FCStd`
- status: `PASS`
- shape_objects: `435`
- invalid_shape_objects: `0`
- non_group_no_shape_objects: `0`
- center_vertical_signature_checks: `4`
- center_vertical_signature_failures: `0`
- total_bbox_x_status: `PASS`
- total_bbox: `-500.000..500.000, -60.000..1923.000, -550.000..2.000`
- total_bbox_x_len: `1000.000`

## Audit Notes

- closed: `not_checked`; this avoids a known FreeCADCmd stability risk on large imported assemblies.
- This audit is a geometry integrity gate for engineering-reference models, not a production drawing release.

## Category Counts

| category | shape_count |
|---|---:|
| center_vertical_cut | 4 |
| door_frame_horizontal | 10 |
| door_hardware | 108 |
| door_panel | 12 |
| door_rib | 12 |
| gold_cabinet_shell | 93 |
| semantic_reference | 168 |
| shelf_weld | 10 |
| solidworks_api_step_backed | 18 |

## Failures

- none
