# 16029 钣金规则证据候选

- Generated at: `2026-05-19T19:15:33+08:00`
- Source batch: `BATCH-16029-SHEETMETAL-20260519`
- Source root: `C:\Users\Administrator\Desktop\参数化模板素材\16029 寄存柜(标准组合式 1917×1000×550)`
- Files: `49` / accepted seeds: `34` / candidates: `7`

## 已提炼公式

### 16029 储物柜门板 1/12-6/12 展开高度序列

- Formula: `flat_height_mm = 181.9 + (door_index - 1) * 152.5`
- Width avg: `473.4 mm`
- Step residual: `0.0 mm`
- Confidence: `high`
- Samples: `6`
- Note: 这是 DXF 展开图的门板尺寸序列证据，可用于生成器的单件规则校准；不是直接等同于整柜装配门缝或外观尺寸。

## 规则候选件

| ID | 角色 | 尺寸证据 | 样本数 | 可信度 | 用途 |
| --- | --- | --- | ---: | --- | --- |
| 16029-door-panel-12-flat | door_panel | 1878.6 x 492.0 mm | 1 | high | door_panel_full_height_flat_pattern_bbox |
| 16029-door-panel-reinforcement-flat | door_reinforcement | 1800.5 x 66.8 mm | 1 | medium | door_reinforcement_bbox_and_hole_pattern |
| 16029-cabinet-shelf-flat | shelf | 574.4 x 480.4 mm | 2 | medium | shelf_flat_pattern_normalized_bbox |
| 16029-shelf-stiffener-flat | shelf_stiffener | 429.0 x 71.4 mm | 1 | medium | shelf_stiffener_bbox |
| 16029-cabinet-vertical-divider-flat | cabinet_divider | 1856.6 x 583.7 mm | 12 | high | vertical_divider_flat_pattern_bbox_and_hole_count |
| 16029-door-frame-horizontal-divider-flat | door_frame_divider | 445.0 x 50.0 mm | 2 | high | door_frame_horizontal_divider_bbox |
| 16029-door-frame-vertical-divider-flat | door_frame_divider | 1834.0 x 49.3 mm | 2 | high | door_frame_vertical_divider_bbox |

## 不进入自动生成的证据

- Blocked files: `15`
- Large bbox noise files: `4`
- Status counts: `{'needs_layout_filter': 7, 'needs_closed_loop_rebuild': 8}`

## 生成器使用建议

- 先把 16029 同外形 10/12/14 门的门板、层板、门框横隔板、竖隔板规则跑通，不做全量变种库存。
- 门板 1/12-6/12 展开高度序列可作为尺寸变化证据，但整柜门缝、铰链、锁孔位置仍需独立装配规则。
- needs_layout_filter 和 needs_closed_loop_rebuild 样本不能直接驱动模型生成，只能进入待修复证据池。
- SolidWorks 交付前，应先用 FreeCAD/STEP 或 SolidWorks 低并发检查 bbox、实体数和装配位置。
