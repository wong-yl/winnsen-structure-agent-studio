# 16029 Enriched 12-Door Validation

- Generated at: `2026-05-23T19:46:28+08:00`
- Mode: `identity`
- Result: `PASS`
- Assembly: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521\native_16029_12door_cabinet_enriched_v1.SLDASM`
- STEP: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521\native_16029_12door_cabinet_enriched_v1.step`
- Recommended fixed modules placed: `3`
- Combined bbox source: `root_app_part`
- Combined bbox: `{'x_min': -500.0, 'x_max': 500.0, 'x_len': 1000.0, 'y_min': -21.2, 'y_max': 1917.0, 'y_len': 1938.2, 'z_min': -550.0, 'z_max': 0.0, 'z_len': 550.0}`

## Checks

| check | result | actual | expected |
|---|---|---|---|
| assembly_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521\native_16029_12door_cabinet_enriched_v1.SLDASM | native SolidWorks assembly |
| step_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521\native_16029_12door_cabinet_enriched_v1.step | STEP export |
| bbox_csv_exists | PASS | D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-ENRICHED-12DOOR-20260521\native_16029_12door_cabinet_enriched_v1_step_bbox.csv | FreeCAD bbox CSV |
| builder_saved | PASS | True | True |
| placement_count | PASS | 4 | 4 |
| all_components_added | PASS | [] | no failed component additions |
| cabinet_width_preserved | PASS | 1000.0 | 1000.0 |
| cabinet_height_top_preserved | PASS | 1917.0 | 1917.0 |
| cabinet_depth_preserved | PASS | 550.0 | 550.0 |
| recommended_candidate_bboxes_match | PASS | 3/3 matched | 3 candidate bboxes match within 0.75 mm |
| embedded_ordinary_door_count | PASS | 12 | 12 |
| embedded_left_column_door_count | PASS | 6 | 6 |
| embedded_right_column_door_count | PASS | 6 | 6 |
| embedded_door_array_all_transforms_applied | PASS | {'total': 12, 'ok': 12, 'failed_roles': []} | 12 placements with added=true and transformApplied=true |
| embedded_left_column_x_position | PASS | 0.0 | -258.5mm +/- 0.1mm |
| embedded_right_column_x_position | PASS | 0.0 | 258.5mm +/- 0.1mm |
| embedded_right_column_standard_rotation | PASS | [[-1, 0, 0, 0, -1, 0, 0, 0, 1], [-1, 0, 0, 0, -1, 0, 0, 0, 1]] | right door placements use 180deg Z rotation |
| embedded_left_right_door_y_alignment | PASS | 0.0 | <= 0.1mm |
| embedded_left_column_pitch | PASS | {'values': [305.0, 305.0, 305.0, 305.0, 305.0], 'max_error': 0.0} | 305.0 |
| embedded_right_column_pitch | PASS | {'values': [305.0, 305.0, 305.0, 305.0, 305.0], 'max_error': 0.0} | 305.0 |
| embedded_door_weld_subassembly_count | PASS | 12 | 12 |
| embedded_door_panel_feature_count | PASS | 12 | 12 |
| embedded_hinge_pin_count | PASS | 12 | 12 |
| embedded_lock_hook_pad_count | PASS | 12 | 12 |
| embedded_electric_lock_hook_count | PASS | 12 | 12 |
| embedded_shelf_module_count | PASS | 10 | 10 |
| embedded_shelf_level_pairs | PASS | [{'center': 333.5, 'count': 2}, {'center': 638.5, 'count': 2}, {'center': 943.5, 'count': 2}, {'center': 1248.5, 'count': 2}, {'center': 1553.5, 'count': 2}] | 5 levels x 2 |
| embedded_shelf_pitch | PASS | {'values': [305.0, 305.0, 305.0, 305.0], 'max_error': 0.0} | 305.0 |
| embedded_shelf_driven_from_lower_door_boundary | PASS | {'ok': True, 'expected_delta': 2.0, 'tolerance': 0.15, 'max_error': 0.0, 'rows': [{'column': 'L', 'boundary_after_row': 1, 'door_y_max': 331.5, 'driven_center_y': 333.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板L焊接004'}, {'column': 'L', 'boundary_after_row': 2, 'door_y_max': 636.5, 'driven_center_y': 638.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板L焊接003'}, {'column': 'L', 'boundary_after_row': 3, 'door_y_max': 941.5, 'driven_center_y': 943.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板L焊接002'}, {'column': 'L', 'boundary_after_row': 4, 'door_y_max': 1246.5, 'driven_center_y': 1248.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板L焊接001'}, {'column': 'L', 'boundary_after_row': 5, 'door_y_max': 1551.5, 'driven_center_y': 1553.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板L焊接'}, {'column': 'R', 'boundary_after_row': 1, 'door_y_max': 331.5, 'driven_center_y': 333.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板R焊接004'}, {'column': 'R', 'boundary_after_row': 2, 'door_y_max': 636.5, 'driven_center_y': 638.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板R焊接003'}, {'column': 'R', 'boundary_after_row': 3, 'door_y_max': 941.5, 'driven_center_y': 943.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板R焊接002'}, {'column': 'R', 'boundary_after_row': 4, 'door_y_max': 1246.5, 'driven_center_y': 1248.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板R焊接001'}, {'column': 'R', 'boundary_after_row': 5, 'door_y_max': 1551.5, 'driven_center_y': 1553.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '箱体横层板R焊接'}], 'errors': []} | center_y = lower door y_max + 2.0mm |
| embedded_front_frame_crossbar_count | PASS | 10 | 10 |
| embedded_front_frame_crossbar_level_pairs | PASS | [{'center': 333.5, 'count': 2}, {'center': 638.5, 'count': 2}, {'center': 943.5, 'count': 2}, {'center': 1248.5, 'count': 2}, {'center': 1553.5, 'count': 2}] | 5 levels x 2 |
| embedded_front_frame_crossbar_pitch | PASS | {'values': [305.0, 305.0, 305.0, 305.0], 'max_error': 0.0} | 305.0 |
| embedded_front_frame_crossbar_driven_from_lower_door_boundary | PASS | {'ok': True, 'expected_delta': 2.0, 'tolerance': 0.15, 'max_error': 0.0, 'rows': [{'column': 'L', 'boundary_after_row': 1, 'door_y_max': 331.5, 'driven_center_y': 333.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板004'}, {'column': 'L', 'boundary_after_row': 2, 'door_y_max': 636.5, 'driven_center_y': 638.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板003'}, {'column': 'L', 'boundary_after_row': 3, 'door_y_max': 941.5, 'driven_center_y': 943.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板002'}, {'column': 'L', 'boundary_after_row': 4, 'door_y_max': 1246.5, 'driven_center_y': 1248.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板001'}, {'column': 'L', 'boundary_after_row': 5, 'door_y_max': 1551.5, 'driven_center_y': 1553.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板'}, {'column': 'R', 'boundary_after_row': 1, 'door_y_max': 331.5, 'driven_center_y': 333.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板R004'}, {'column': 'R', 'boundary_after_row': 2, 'door_y_max': 636.5, 'driven_center_y': 638.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板R003'}, {'column': 'R', 'boundary_after_row': 3, 'door_y_max': 941.5, 'driven_center_y': 943.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板R002'}, {'column': 'R', 'boundary_after_row': 4, 'door_y_max': 1246.5, 'driven_center_y': 1248.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板R001'}, {'column': 'R', 'boundary_after_row': 5, 'door_y_max': 1551.5, 'driven_center_y': 1553.5, 'delta': 2.0, 'error': 0.0, 'driven_label': '门框 横隔板R'}], 'errors': []} | center_y = lower door y_max + 2.0mm |
