# 16029 散件先行窄范围试点最终状态 - 2026-06-17

## 结论

窄范围试点可以收口：散件先行路线已经证明可行，并且比一次性生成整柜更利于模型质量和工程可控性。

当前结论不是供应商生产放行。供应商 DXF/BOM 仍保持不放行，原因集中在门板上铰链孔 datum 与当前展开轮廓冲突。

## 已完成范围

| 项目 | 当前证据 | 结论 |
|---|---|---|
| 零件规则 | `data/locker_16029_part_first_pilot_contract.json`、`data/locker_16029_gold_sheetmetal_rules.json`、gate source/contract checks PASS | 规则梳理完成 |
| 门板 | SW2020 `SheetMetal + FlatPattern + EdgeFlange`，folded `307 x 298 x 18.2 mm`，flat DXF `343.4 x 334.4 mm` PASS | 核心折弯/展开闭合；供应商孔切未闭合 |
| 前框 | 当前 `front_frame_vertical_left_v43.SLDPRT` 有 SheetMetal/FlatPattern，12 个源孔为 cut features，flat DXF `49.3 x 1834 mm`，`12 ARC + 16 LINE` PASS | 前框源孔 blocker 已闭合 |
| 层板/横撑 | SW2020 单件、STEP、flat DXF、flat check 均通过当前 gate | 试点链路通过 |
| 最小装配 | strict gate 记录 `SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED`，blocking reasons 为空 | 最小接口验证通过 |
| BOM | strict gate 校验 3 行 trial BOM，状态保持 `pilot_only_not_released` | BOM 已校验，但不作为生产 BOM |
| gate | default/strict 均 PASS，blocking 0 | 窄试点工程评审 ready |

## 门板孔切最新判断

- 锁侧 2 个 `2.0 mm` 孔：v34 `Unfold/Cut/Fold` 可切出，且不破坏 folded/flat 尺寸。
- 下铰链 `10.2 mm` 孔：flat-mapped Z 向选面可切出，安全 variant 为 `1, 2, 11, 12`，folded/flat 尺寸保持。
- 上铰链 `10.2 mm` 孔：datum 映射到 `(-3.8, 262.55) mm`，在当前 12 线 FlatPattern 材料边界外；16 个 variant 均不能选中 Z 向材料面。

这说明剩余问题不是继续扫 SolidWorks API 参数，而是结构规则冲突：上铰链孔 datum、左上角避让/缺口、或法兰展开解释需要结构工程确认。

## 当前放行边界

可以放行：

- 散件先行路线评估结论。
- 窄范围工程评审结论。
- 继续沿单件规则推进的试点方向。

不能放行：

- 供应商直接生产 DXF。
- 正式生产 BOM。
- 完整整柜扩展。

## 下一步

只处理门板上铰链孔规则，不扩大到整柜：

1. 让结构工程确认上铰链 `10.2 mm` 孔是否应移动、是否应调整左上角 corner relief，或是否定义为后加工。
2. 规则修正后重新跑门板 4/4 cut features。
3. 通过条件仍然是：`holeCutCount=4`、flat `343.4 x 334.4 mm`、folded `307 x 298 x 18.2 mm`，且 default/strict gate 均 PASS。
