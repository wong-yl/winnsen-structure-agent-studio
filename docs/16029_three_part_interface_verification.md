# 16029 三散件最小接口验证包

生成时间：2026-06-18T14:32:41.859Z

## 结论

- 状态：`READY_FOR_SW_NATIVE_INTERFACE_RERUN_NOT_RELEASED`
- 当前正式三件：`door_panel_2_12_right_v43`、`front_frame_vertical_left_v43`、`shelf_cross_panel_left_v43`
- 结论口径：本包证明三散件证据链、placement 和接口检查口径已经闭合；它不签核正式几何 clearance。
- 边界：不生成完整柜体，不进入供应商 DXF release，不生成 released BOM。

## 正式接口包

- placement：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_placements.tsv`
- build：`completed`，saved=`true`，reference_count=`3`
- inspect：opened=`false`，rebuilt=`false`，component_count=`0`
- formal native clearance：`NOT_CLOSED_CURRENT_SLDASM_INSPECT_DID_NOT_OPEN_OR_ENUMERATE`

## 三散件证据

| Part | Validation | Placement matches BOM SLDPRT | Added to SLDASM | STEP bbox sanity |
|---|---|---:|---:|---|
| door_panel_2_12_right_v43 | cad_closed_pending_engineering_signoff_for_supplier | true | true | PASS |
| front_frame_vertical_left_v43 | second_part_sheetmetal_verified | true | true | PASS |
| shelf_cross_panel_left_v43 | third_part_sheetmetal_verified | true | true | FAIL |

## 接口检查口径

| Interface | Contract check | Historical envelope gap mm | Current package conclusion |
|---|---|---:|---|
| door_to_front_frame_datum | door gap and hinge-side datum alignment | 8.284 | Current formal parts and placement are packaged; native clearance must be rerun because current SLDASM inspect did not open and STEP AABB is not sufficient for hinge datum/gap signoff. |
| shelf_to_front_frame_locating | front-edge and locating-hole interface | 252.141 | Shelf part-level SheetMetal/FlatPattern is verified, but production interface clearance remains open until SW native rerun with the current SLDPRT succeeds. |
| shelf_to_door_swing_clearance | door swing and shelf front-edge clearance | 261.5 | Door and shelf are ready for narrow clearance rerun; this package does not close swing clearance. |

## STEP BBox 诊断

- 当前正式三件 STEP probe：`FALLBACK_AABB_DEGRADED`
- usable component：`2/3`
- 层板 STEP sanity：`FAIL`
- 说明：STEP CARTESIAN_POINT envelope is far larger than the shelf flat evidence, so it is not trusted for clearance.

## 历史 native-envelope 参考

- clearance：`SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED`
- separated pairs：`3`
- overlapping pairs：`0`
- 用途：reference_only_not_current_formal_geometry

## 下一步 SW2020 复核

1. Open the current formal SLDASM from door_frame_lock_shelf_minimum_interface_v43_placements.tsv in SW2020.
2. Rebuild and enumerate exactly three unsuppressed components using the current production SLDPRTs.
3. Run native component bbox or exact interference for door-front, shelf-front, and shelf-door pairs.
4. Run a door swing swept clearance check or record an engineering gap for shelf-to-door swing.

## Release 边界

- BOM：`pilot_only_not_released`
- supplier release：`supplier_release_excluded`
- supplier precheck：`BLOCKED`
