# 16029 Enriched 10-Door Validation

- Generated at: `2026-05-22T09:39:12+08:00`
- Mode: `matrix`
- Result: `PASS`
- Assembly: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-10DOOR-20260521\native_16029_10door_cabinet_enriched_v2.SLDASM`
- STEP: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-10DOOR-20260521\native_16029_10door_cabinet_enriched_v2.step`
- Recommended fixed modules placed: `9`
- Combined bbox source: `root_app_part`
- Combined bbox: `{'x_min': -500.0, 'x_max': 500.0, 'x_len': 1000.0, 'y_min': -21.2, 'y_max': 1917.0, 'y_len': 1938.2, 'z_min': -550.0, 'z_max': 0.0, 'z_len': 550.0}`

## Checks

| check | result | actual | expected |
|---|---|---|---|
| assembly_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-10DOOR-20260521\native_16029_10door_cabinet_enriched_v2.SLDASM | native SolidWorks assembly |
| step_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-10DOOR-20260521\native_16029_10door_cabinet_enriched_v2.step | STEP export |
| bbox_csv_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-10DOOR-20260521\native_16029_10door_cabinet_enriched_v2_step_bbox.csv | FreeCAD bbox CSV |
| builder_saved | PASS | True | True |
| placement_count | PASS | 10 | 10 |
| all_components_added | PASS | [] | no failed component additions |
| cabinet_width_preserved | PASS | 1000.0 | 1000.0 |
| cabinet_height_top_preserved | PASS | 1917.0 | 1917.0 |
| cabinet_depth_preserved | PASS | 550.0 | 550.0 |
| recommended_candidate_bboxes_match | PASS | 9/9 matched | 9 candidate bboxes match within 0.75 mm |
| embedded_ordinary_door_count | PASS | 10 | 10 |
| embedded_left_column_door_count | PASS | 5 | 5 |
| embedded_right_column_door_count | PASS | 5 | 5 |
| embedded_door_array_all_transforms_applied | PASS | {'total': 10, 'ok': 10, 'failed_roles': []} | 10 placements with added=true and transformApplied=true |
| embedded_left_column_x_position | PASS | 0.0 | -258.5mm +/- 0.1mm |
| embedded_right_column_x_position | PASS | 0.0 | 258.5mm +/- 0.1mm |
| embedded_right_column_standard_rotation | PASS | [[-1, 0, 0, 0, -1, 0, 0, 0, 1], [-1, 0, 0, 0, -1, 0, 0, 0, 1]] | right door placements use 180deg Z rotation |
| embedded_left_right_door_y_alignment | PASS | 0.0 | <= 0.1mm |
| embedded_left_column_pitch | PASS | {'values': [366.0, 366.0, 366.0, 366.0], 'max_error': 0.0} | 366.0 |
| embedded_right_column_pitch | PASS | {'values': [366.0, 366.0, 366.0, 366.0], 'max_error': 0.0} | 366.0 |
| embedded_door_weld_subassembly_count | PASS | 10 | 10 |
| embedded_door_panel_feature_count | PASS | 10 | 10 |
| embedded_hinge_pin_count | PASS | 10 | 10 |
| embedded_lock_hook_pad_count | PASS | 10 | 10 |
| embedded_electric_lock_hook_count | PASS | 10 | 10 |
| embedded_shelf_module_count | PASS | 8 | 8 |
| embedded_shelf_level_pairs | PASS | [{'center': 576.5, 'count': 2}, {'center': 942.5, 'count': 2}, {'center': 1308.5, 'count': 2}, {'center': 1674.5, 'count': 2}] | 4 levels x 2 |
| embedded_shelf_pitch | PASS | {'values': [366.0, 366.0, 366.0], 'max_error': 0.0} | 366.0 |
| embedded_front_frame_crossbar_count | PASS | 8 | 8 |
| embedded_front_frame_crossbar_level_pairs | PASS | [{'center': 541.0, 'count': 2}, {'center': 907.0, 'count': 2}, {'center': 1273.0, 'count': 2}, {'center': 1639.0, 'count': 2}] | 4 levels x 2 |
| embedded_front_frame_crossbar_pitch | PASS | {'values': [366.0, 366.0, 366.0], 'max_error': 0.0} | 366.0 |
