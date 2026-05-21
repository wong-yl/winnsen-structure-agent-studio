# 16029 10门变体配方

- Generated at: `2026-05-18T23:36:28.646868+00:00`
- Status: `usable_as_template_clone_native_save_smoke_passed_packgo_pending`
- Practice SolidWorks source: `D:\机械结构工程师智能体\work\16029_练习副本_20260429\1.工程图\标准寄存柜1917×1000×550(总装配).SLDASM`
- Source exists: `True`
- 10-door native save smoke: `D:\Winnsen_Structure_Agent_Studio\workers\manual_runs\QA-16029-10DOOR-FAST-CLONE-20260519073300\QA-16029-10DOOR-FAST-CLONE-20260519073300.SLDASM`

## 关键参数

| Item | Value |
| --- | --- |
| layout | 2列 x 每列5门 = 10门 |
| doorType | 2/10 |
| baseDoorZoneHeightMm | 1830.00mm |
| unitHeightMm | 183.00mm |
| doorPitchMm | 366.00mm |
| doorPanelHeightMm | 359.00mm |
| doorPanelWidthMm | 437.00mm |

## SolidWorks 修改动作

| Operation | Detail |
| --- | --- |
| derive_2_10_door_panel | Copy 2/12 door-panel source to 2/10, set panel height to 359.00mm and keep width near 437.00mm. |
| derive_2_10_door_weld_and_assembly | Copy 2/12 door weldment and assembly to 2/10 names, replace the inner panel with the 2/10 panel, keep ZJA-S500 lock hardware. |
| update_door_pattern | Set cabinet door pattern count from 6 to 5 and pitch from 305.00mm to 366.00mm. |
| update_frame_and_shelf_patterns | Set door-frame divider and shelf patterns from 11 x 152.50mm to 9 x 183.00mm; recompute skipped instances. |

## 数量复核

| Check | Original | Current | Delta | Status | Note |
| --- | ---: | ---: | ---: | --- | --- |
| 总装配实例数 | 423 | 385 | -38 | 通过 | 从原423降到385，布局与门板替换均已影响装配树 |
| 2/10门板数量 | 0 | 10 | 10 | 通过 | 目标为10个2/10门板 |
| 2/12旧门板数量 | 12 | 0 | -12 | 通过 | 旧2/12门板不应继续出现在10门方案中 |
| 门装配数量 | 12 | 10 | -2 | 通过 | 装配名称仍是2/12，但内部门板已替换为2/10；正式版建议派生重命名为2/10装配 |
| 电控锁ZJA-S500 | 12 | 10 | -2 | 通过 | 目标为10把锁 |
| 门框横隔板L | 6 | 5 | -1 | 通过 | 每侧5层开口 |
| 门框横隔板R | 6 | 5 | -1 | 通过 | 每侧5层开口 |
| 箱体横层板L | 6 | 5 | -1 | 通过 | 左侧层板零件变为5 |
| 箱体横层板R | 6 | 5 | -1 | 通过 | 右侧层板零件变为5 |

## 当前边界

- 10-door is available as a SolidWorks practice-template clone, not yet as a fully algorithmic arbitrary door-count mutator.
- Do not overwrite the original 12-door standard template.
- Native SolidWorks save smoke has passed for the 10-door template clone; Pack-and-Go is still required before independent handoff.
