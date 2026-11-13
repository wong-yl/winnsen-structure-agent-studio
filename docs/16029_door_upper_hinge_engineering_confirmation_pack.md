# 16029 门板上铰链 10.2 mm 孔工程确认包

生成时间：2026-06-18T13:33:51.122Z

## 当前结论

CAD 侧已闭合：正式门板已有 4/4 真实孔特征，SheetMetal/FlatPattern 保留，展开与折弯包络均保持目标值。当前不能发供应商 DXF 的唯一原因是缺少 B 规则结构工程签核。

- 正式门板状态：`SMOKE_EDGE_FLANGE_SHEETMETAL_FLAT_AND_FOLDED_ENVELOPE_EXPORTED`
- SW 特征：SheetMetal=true，FlatPattern=true
- 折弯包络：`307 x 298 x 18.2 mm`
- 展开包络：`343.4 x 334.4 mm`
- 目标孔 datum：`4`
- 真实 cut features：`4`
- flat DXF 圆孔实体：`5`
- supplier DXF precheck：`BLOCKED`

## 剩余阻塞

- `door_panel_2_12_right_v43:b_engineering_signoff_not_accepted_for_true_cut_precheck`

模板本身不算签核。正式签核文件必须保存为：
`data/locker_16029_door_upper_hinge_engineering_signoff.json`

## B 规则确认点

- 选择项：`B_ADJUST_TOP_LEFT_CORNER_RELIEF`
- 上铰链孔 source center：`-143.5, 154.6 mm`
- 上铰链孔 mapped center：`-3.8, 262.55 mm`
- 当前材料状态：`INSIDE_CURRENT_FLAT_PATTERN`
- 上下两个 10.2 mm 铰链孔保持同一 source X datum。
- 通过左上角 / 顶部法兰展开轮廓补料，使上铰链孔落在当前展开材料内。

## CAD 证据

- 路线：官方 SketchUseEdge stepped 14.1 mm edge-flange profile + folded top-return SimpleHole2/SketchHole
- 门板 result：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_result.json`
- flat DXF 检查：`workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json`
- default gate：`data/locker_16029_part_first_pilot_gate_report.json`
- strict gate：`data/locker_16029_part_first_pilot_gate_report.strict.json`
- supplier precheck：`data/locker_16029_supplier_dxf_precheck.json`

## 签核后验收

- SW2020 feature tree contains SheetMetal and FlatPattern
- formal door result holeCutCount is 4
- flat DXF has at least 4 CIRCLE entities
- flat bbox remains 343.4 x 334.4 mm within existing tolerance
- folded bbox remains 307 x 298 x 18.2 mm
- default part-first gate PASS
- strict part-first gate PASS
- supplier DXF precheck READY

## 下一步

让结构工程签署 B_ADJUST_TOP_LEFT_CORNER_RELIEF，并保存为 data/locker_16029_door_upper_hinge_engineering_signoff.json，然后重跑 supplier DXF precheck。
