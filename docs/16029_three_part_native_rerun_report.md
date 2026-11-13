# 16029 三散件 SW2020 Native Rerun 报告

生成时间：2026-06-18T15:00:10.054Z

## 结论

- 状态：`SW2020_NATIVE_RERUN_COMPLETE_WITH_DOOR_FRONT_AABB_BLOCKER`
- inspect 修复：`RESOLVED_CURRENT_FORMAL_SLDASM_OPENS_REBUILDS_AND_ENUMERATES_THREE_COMPONENTS`
- clearance 结论：`TWO_PAIRS_AABB_SEPARATED_DOOR_FRONT_REQUIRES_EXACT_INTERFERENCE_OR_DATUM_GAP_REVIEW`
- 下一步：Run exact SolidWorks interference or engineering datum/gap review for door_panel_2_12_right_v43 vs front_frame_vertical_left_v43. Do not treat bbox overlap as pass.

## 构建与枚举

- build：`completed`，saved=`true`，step_saved=`true`
- assembly：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.SLDASM`
- components：opened=`true`，rebuilt=`true`，count=`3`
- enumeration：`root_children_recursive`

| Component | Transform tx,ty,tz mm | Native bbox mm |
|---|---:|---:|
| review_single_door_panel_W307_H298_sheetmetal-1 | 0, 0, 0 | 307.0000000000002 x 298.0000000000003 x 18.200000000000006 |
| front_frame_vertical_left_v43-1 | -168, 0, 0 | 49.30000000000001 x 1834 x 0.8 |
| shelf_cross_panel_left_v43-1 | 0, -160, -280 | 433.20000000000016 x 25.000000000000227 x 527.2 |

## 三对接口 BBox

| Pair | AABB overlaps | Min separating gap mm | Conclusion |
|---|---:|---:|---|
| door_to_front_frame | true | 0 | AABB_OVERLAPS_REQUIRES_EXACT_SOLID_INTERFERENCE_CHECK |
| shelf_to_door | false | 300.5 | SOLIDWORKS_NATIVE_AABB_SEPARATED_NO_SOLID_INTERSECTION_POSSIBLE |
| shelf_to_front_frame | false | 300.5 | SOLIDWORKS_NATIVE_AABB_SEPARATED_NO_SOLID_INTERSECTION_POSSIBLE |

## 边界

- BOM：`pilot_only_not_released`
- supplier release：`supplier_release_excluded`
- supplier precheck：`BLOCKED`
- 不修改散件几何，不进入完整柜体，不进入供应商 DXF release。
