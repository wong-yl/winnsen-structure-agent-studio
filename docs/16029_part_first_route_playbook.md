# 16029 散件先行最小工程流程

生成时间：2026-06-18T14:20:18.402Z

## 当前状态

- 流程状态：`READY_FOR_THREE_PART_INTERFACE_ITERATION`
- 边界：不进入供应商 DXF release，不发布生产图，不生成 released BOM。
- 当前 supplier precheck 状态仅作为边界提示：`BLOCKED`

## 门板成功规则

- 零件：`door_panel_2_12_right_v43`
- 已证实路线：official SketchUseEdge stepped 14.1 mm edge-flange profile + folded top-return SimpleHole2/SketchHole
- 上铰链方案：`B_ADJUST_TOP_LEFT_CORNER_RELIEF`
- 上铰链 source center：`-143.5, 154.6 mm`
- 上铰链 mapped center：`-3.8, 262.55 mm`
- 左上轮廓 step：`14.1 mm`
- 真实孔数：`4`
- flat DXF 实体：`{"CIRCLE":5,"LINE":13}`
- folded：`307 x 298 x 18.2 mm`
- flat：`343.4 x 334.4 mm`

## 第二散件验证

- 选择：`front_frame_vertical_left_v43`
- 原因：Chosen before shelf panel because it already has clean SW2020 SheetMetal/FlatPattern evidence, 12 source hole cuts, and refreshed flat DXF validation.
- SW2020 SheetMetal：`true`
- SW2020 FlatPattern：`true`
- flat DXF：`PASS`
- flat bbox：`49.3 x 1834 mm`
- 孔特征数：`12`

## 第三散件验证

- 选择：`shelf_cross_panel_left_v43`
- 原因：Promoted after the front frame because it completes the current minimum interface trio: door panel, front frame, and shelf/cross panel.
- 证据读取口径：STEP/open evidence is read from shelf_cross_panel_left_v43_result.json; SheetMetal/FlatPattern/flat-DXF evidence is read from shelf_cross_panel_left_v43_flat_export.json and shelf_cross_panel_left_v43_flat_check.json. All JSON reads strip a leading UTF-8 BOM.
- STEP saved：`true`
- SW2020 SheetMetal：`true`
- SW2020 FlatPattern：`true`
- flat DXF：`PASS`
- flat bbox：`574.4 x 480.4 mm`
- flat 实体：`{"LINE":70,"CIRCLE":11,"ARC":11}`

## 试点 BOM

| Part | Role | Status |
|---|---|---|
| door_panel_2_12_right_v43 | ordinary door panel | cad_closed_pending_engineering_signoff_for_supplier |
| front_frame_vertical_left_v43 | front frame vertical / side frame | second_part_sheetmetal_verified |
| shelf_cross_panel_left_v43 | shelf / cross panel | third_part_sheetmetal_verified |

## 装配接口

- door_panel_2_12_right_v43 -> front_frame_vertical_left_v43: door gap and hinge-side datum alignment
- door_panel_2_12_right_v43 -> lock_mounting_hole_datum_R_row01: lock-side hole and mechanical tongue datum alignment
- shelf_cross_panel_left_v43 -> front_frame_vertical_left_v43: front-edge and locating-hole interface
- shelf_cross_panel_left_v43 -> door_panel_2_12_right_v43: door swing and shelf front-edge clearance

规则：Assemble only after individual parts pass their own SheetMetal/FlatPattern/flat-DXF checks; do not use assembly constraints to hide part-level geometry defects.

## 最小 Gate 流程

1. Generate or promote one SW2020 SLDPRT per sheet-metal part.
2. Export matching STEP and flat DXF for each part.
3. Parse flat DXF with tools/inspect_16029_flat_dxf.mjs using part-specific expected bbox.
4. Run tools/verify_16029_part_first_pilot.mjs --out data/locker_16029_part_first_pilot_gate_report.json.
5. Run tools/verify_16029_part_first_pilot.mjs --strict-artifacts --out data/locker_16029_part_first_pilot_gate_report.strict.json.
6. Run tools/verify_16029_part_first_route_playbook.mjs for the reusable workflow package.

当前 gate：
- default：`PASS`
- strict：`PASS`

## 签核边界

CAD evidence is closed, but supplier DXF release still requires data/locker_16029_door_upper_hinge_engineering_signoff.json.

## 结构工程问题

- B rule: Is the stepped top-left/top-flange contour acceptable for bend relief, coating, appearance gap, and hinge installation?
- Door module: Should the hinge-side datum be made a named reference that every row/handedness variant inherits?
- Front frame: Are the 12 holes and 49.3 x 1834 mm flat envelope sufficient for the next interface assembly review, or must the source DXF be re-probed before broader scaling?
- Shelf/cross panel: Are the 11 circular holes, 11 arcs, and 574.4 x 480.4 mm flat envelope acceptable for the front-frame locating interface and door swing clearance review?
- Assembly: Which interfaces are allowed in the minimum interface assembly, and which must stay out until each scattered part has passed part-level gates?
- BOM: What naming, revision, and quantity rules should be used when moving from pilot-only rows to released BOM rows?
