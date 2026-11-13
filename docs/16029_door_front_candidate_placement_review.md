# 16029 门板-前框候选 Placement 复核包

生成时间：2026-06-19T06:31:20.719Z

## 结论

- 状态：`CANDIDATE_SW2020_NATIVE_AABB_SEPARATED_ENGINEERING_GAP_REVIEW_READY`
- 候选：固定门板，前框 tx 从 `-168 mm` 调到 `-185.15 mm`
- 前框相对偏移：`-17.15 mm`
- 目标 X 门缝：`7 mm`
- 预期 AABB：`CANDIDATE_AABB_SEPARATED_NO_SOLID_INTERSECTION_POSSIBLE_AFTER_SW2020_RERUN`
- 预期 X 间隙：`7 mm`

## 文件

- 候选 placement：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_placements.tsv`
- 候选 SLDASM：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate.SLDASM`
- 候选 build：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_build.json`
- 候选 components：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_components.json`
- 候选 clearance：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_clearance.json`

## 预期 BBox

| Item | xmin | xmax |
|---|---:|---:|
| Door | -153.5000000000001 | 153.5000000000001 |
| Shifted front frame | -209.8 | -160.5 |

## SW2020 候选 rerun

- build exists：`true`
- components exists：`true`
- clearance exists：`true`
- build status：`completed`
- opened/rebuilt/count：`true/true/3`
- clearance status：`SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED`
- rerun complete：`true`

## 下一步命令

1. `workers\solidworks_tools\bin\BuildPlacedComponentsModule.exe workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_placements.tsv workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate.SLDASM workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_build.json`
2. `workers\solidworks_tools\bin\InspectAssemblyComponents.exe workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate.SLDASM workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_components.json --require-rebuild`
3. `node tools\build_16029_min_interface_sw_bbox_clearance_report.mjs --components workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_components.json --out workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_clearance.json --assembly-id door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate`

## 边界

- 不修改正式 CAD：`false`
- 不覆盖正式 placement：`false`
- supplier release：`supplier_release_excluded`
- supplier precheck：`BLOCKED`
- 不进入完整柜体，不进入供应商 DXF release。
