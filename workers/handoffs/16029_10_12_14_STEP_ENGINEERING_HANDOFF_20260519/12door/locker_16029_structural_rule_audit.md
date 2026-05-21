# 16029 10/12/14 门结构规则审计

- Generated at: `2026-05-20T17:12:10+08:00`
- Scope: `1000W x 1917H x 550D 10/12/14 structural rule audit from verify.csv`
- Source quality matrix: `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_variant_quality_matrix.json`

| 门数 | 状态 | 每列门数 | 门宽 | 门高 | 门距 | 锁孔X | 铰链X | 门 Y 范围 | STEP bbox X/Y/Z | 失败项 | 门板来源 |
| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- | ---: | --- |
| 10 | PASS | 5 | 437.0 | 359.0 | 366.0 | ±55.0 | ±467.0 | 32.0-1855.0 | 1000.0/1983.0/552.0 | 0 | derived_one_piece_folded_sheetmetal_with_bend_radius |
| 12 | PASS | 6 | 437.0 | 298.0 | 305.0 | ±55.0 | ±467.0 | 32.0-1855.0 | 1000.0/1983.0/552.0 | 0 | validated_solidworks_panel_step |
| 14 | PASS | 7 | 437.0 | 254.429 | 261.429 | ±55.0 | ±467.0 | 32.0-1855.0 | 1000.0/1983.0/552.0 | 0 | freecad_gold_seam_production_signature_candidate |

## Check Details

### 10 门

| check | status | actual | expected |
| --- | --- | --- | --- |
| door_module_count | PASS | `10` | `10` |
| left_column_row_count | PASS | `5` | `5` |
| right_column_row_count | PASS | `5` | `5` |
| door_height_formula | PASS | `{"min": 359.0, "max": 359.0, "spread": 0.0}` | `359.0` |
| door_gap_formula | PASS | `{"min": 7.0, "max": 7.0, "spread": 0.0}` | `7.0` |
| door_pitch_formula | PASS | `{"door_height": {"min": 359.0, "max": 359.0, "spread": 0.0}, "gap": {"min": 7.0, "max": 7.0, "spread": 0.0}}` | `366.0` |
| door_stack_bottom_top | PASS | `{"first_y_min": 32.0, "last_y_max": 1855.0}` | `{"first_y_min": 32.0, "last_y_max": 1855.0}` |
| left_right_door_row_alignment | PASS | `0.0` | `<= 0.05` |
| door_width_reference | PASS | `{"min": 437.0, "max": 437.0, "spread": 0.0}` | `437.0` |
| door_lock_hinge_side_relation | PASS | `{"L": {"lock_hole_center_x": -55.0, "hinge_axis_x": -467.0}, "R": {"lock_hole_center_x": 55.0, "hinge_axis_x": 467.0}}` | `left negative, right positive` |
| lock_hole_count | PASS | `10` | `10` |
| lock_hook_count | PASS | `10` | `10` |
| lock_position_relation | PASS | `all lock centers match door centers and side sign` | `per-door lock references` |
| shelf_count | PASS | `8` | `8` |
| door_frame_horizontal_count | PASS | `8` | `8` |
| shelf_and_frame_driven_from_door_boundary | PASS | `all shelf/frame rows follow door boundary offsets` | `{"shelf_y_min": "+2mm from lower door y_max", "frame_y_min": "-10mm from lower door y_max"}` |
| overall_bbox_x | PASS | `1000.0` | `1000.0` |
| step_bbox_xyz_envelope | PASS | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` |
| step_invalid_shape_count | PASS | `0.0` | `0` |

### 12 门

| check | status | actual | expected |
| --- | --- | --- | --- |
| door_module_count | PASS | `12` | `12` |
| left_column_row_count | PASS | `6` | `6` |
| right_column_row_count | PASS | `6` | `6` |
| door_height_formula | PASS | `{"min": 298.0, "max": 298.0, "spread": 0.0}` | `298.0` |
| door_gap_formula | PASS | `{"min": 7.0, "max": 7.0, "spread": 0.0}` | `7.0` |
| door_pitch_formula | PASS | `{"door_height": {"min": 298.0, "max": 298.0, "spread": 0.0}, "gap": {"min": 7.0, "max": 7.0, "spread": 0.0}}` | `305.0` |
| door_stack_bottom_top | PASS | `{"first_y_min": 32.0, "last_y_max": 1855.0}` | `{"first_y_min": 32.0, "last_y_max": 1855.0}` |
| left_right_door_row_alignment | PASS | `0.0` | `<= 0.05` |
| door_width_reference | PASS | `{"min": 437.0, "max": 437.0, "spread": 0.0}` | `437.0` |
| door_lock_hinge_side_relation | PASS | `{"L": {"lock_hole_center_x": -55.0, "hinge_axis_x": -467.0}, "R": {"lock_hole_center_x": 55.0, "hinge_axis_x": 467.0}}` | `left negative, right positive` |
| lock_hole_count | PASS | `12` | `12` |
| lock_hook_count | PASS | `12` | `12` |
| lock_position_relation | PASS | `all lock centers match door centers and side sign` | `per-door lock references` |
| shelf_count | PASS | `10` | `10` |
| door_frame_horizontal_count | PASS | `10` | `10` |
| shelf_and_frame_driven_from_door_boundary | PASS | `all shelf/frame rows follow door boundary offsets` | `{"shelf_y_min": "+2mm from lower door y_max", "frame_y_min": "-10mm from lower door y_max"}` |
| overall_bbox_x | PASS | `1000.0` | `1000.0` |
| step_bbox_xyz_envelope | PASS | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` |
| step_invalid_shape_count | PASS | `0.0` | `0` |

### 14 门

| check | status | actual | expected |
| --- | --- | --- | --- |
| door_module_count | PASS | `14` | `14` |
| left_column_row_count | PASS | `7` | `7` |
| right_column_row_count | PASS | `7` | `7` |
| door_height_formula | PASS | `{"min": 254.428, "max": 254.429, "spread": 0.001}` | `254.429` |
| door_gap_formula | PASS | `{"min": 7.0, "max": 7.0, "spread": 0.0}` | `7.0` |
| door_pitch_formula | PASS | `{"door_height": {"min": 254.428, "max": 254.429, "spread": 0.001}, "gap": {"min": 7.0, "max": 7.0, "spread": 0.0}}` | `261.429` |
| door_stack_bottom_top | PASS | `{"first_y_min": 32.0, "last_y_max": 1855.0}` | `{"first_y_min": 32.0, "last_y_max": 1855.0}` |
| left_right_door_row_alignment | PASS | `0.0` | `<= 0.05` |
| door_width_reference | PASS | `{"min": 437.0, "max": 437.0, "spread": 0.0}` | `437.0` |
| door_lock_hinge_side_relation | PASS | `{"L": {"lock_hole_center_x": -55.0, "hinge_axis_x": -467.0}, "R": {"lock_hole_center_x": 55.0, "hinge_axis_x": 467.0}}` | `left negative, right positive` |
| lock_hole_count | PASS | `14` | `14` |
| lock_hook_count | PASS | `14` | `14` |
| lock_position_relation | PASS | `all lock centers match door centers and side sign` | `per-door lock references` |
| shelf_count | PASS | `12` | `12` |
| door_frame_horizontal_count | PASS | `12` | `12` |
| shelf_and_frame_driven_from_door_boundary | PASS | `all shelf/frame rows follow door boundary offsets` | `{"shelf_y_min": "+2mm from lower door y_max", "frame_y_min": "-10mm from lower door y_max"}` |
| overall_bbox_x | PASS | `1000.0` | `1000.0` |
| step_bbox_xyz_envelope | PASS | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` | `{"x": 1000.0, "y": 1983.0, "z": 552.0}` |
| step_invalid_shape_count | PASS | `0.0` | `0` |

- This audit verifies layout rules from generated evidence rows; it does not release production drawings.
- Checks cover door grid, door width, lock/hinge side relation, shelf/frame boundary offsets, L/R symmetry, STEP bbox X/Y/Z envelope, invalid STEP shape count, and overall bbox X.
