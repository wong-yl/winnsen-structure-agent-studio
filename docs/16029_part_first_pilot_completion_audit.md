# 16029 散件先行试点完成审计

审计日期：2026-06-17

## 结论

散件先行路线可以继续，且已经证明它比一次性生成整装配更可控。当前可以收口的是“窄范围试点路线评估和工程结论”，不能收口的是“供应商直接生产放行”。

当前状态：

| 项目 | 状态 |
|---|---|
| 路线可行性 | 可行，建议继续窄试点 |
| SolidWorks 2020 单件/接口证据 | 试点层面已形成 |
| 最小接口装配 | v7/native-envelope 已通过 SW native bbox clearance |
| 生产 DXF | strict gate 已通过；供应商 DXF 前置检查仍未通过：前框 12 孔已补，门板仅验证到 2/4 cut features |
| 生产 BOM | 不可放行 |
| 完整柜体扩展 | 暂停 |

## 已经证明的事

1. 散件先行能把问题拆开：门板展开规则、前框 FlatPattern、层板展开、最小接口关系可以分别定位。
2. 门板问题已在散件层关闭核心几何 blocker：当前 edge-flange 候选件把 `343.4 x 334.4 mm` 展开和 `307 x 298 x 18.2 mm` 折弯安装态统一到同一棵 SW2020 特征树。
3. 层板/横撑链路已跑通：`SLDPRT + STEP + flat DXF + flat_check` 已满足当前试点。
4. 最小接口已经有可用证据：v7 门板包络、前框候选件、横撑 native envelope 组成的 `.SLDASM` 已构建并通过 SW native bbox clearance。
5. BOM 控制是正确的：所有试点 BOM 行仍应保持 `pilot_only_not_released`，避免误当生产 BOM。
6. 前框 12 个源孔已补进当前 SW2020 重建件：正式 `front_frame_vertical_left_v43.SLDPRT` 现在有 12 个 cut features，FlatPattern DXF 为 `12 ARC + 16 LINE`，bbox 仍为 `49.3 x 1834 mm` PASS。

## 当前生产放行状态

| blocker | 当前状态 | 关闭条件 |
|---|---|---|
| 门板展开/折弯规则 | 已关闭核心几何 blocker：当前 `review_single_door_panel_W307_H298_sheetmetal.SLDPRT` 为 SW2020 SheetMetal/FlatPattern/EdgeFlange 特征树，folded 包络 `307 x 298 x 18.2 mm`，FlatPattern DXF `343.4 x 334.4 mm` PASS；v34 Unfold/Cut/Fold 试验只切出锁侧 2 个小孔，铰链侧 2 个 10.2 孔仍未进入 cut features | 供应商 DXF 前必须继续解决门板 4/4 物理孔切，或由结构/工艺签字接受 datum-only/后加工 |
| 前框生产 FlatPattern | 已从 source reference 推进为当前 `front_frame_vertical_left_v43.SLDPRT` 的 SW2020 SheetMetal/FlatPattern 导出，且 12 个源孔已补为 cut features；DXF bbox `49.3 x 1834 mm`，实体 `12 ARC + 16 LINE` PASS | 前框孔切细节已闭合，可进入供应商 DXF 评审前复核 |

## 对目标的判断

如果目标是“判断散件先行能不能提高模型质量和工程可控性，并给出窄范围试点结论”，现在已经有足够证据回答：能提高，值得继续，但必须保留最小接口 gate。

如果目标是“关闭散件先行 strict gate blocker 并进入工程放行评审”，现在已完成。若目标是“供应商可直接生产的完整 DXF/BOM 包”，前框孔切已补，但仍需补门板铰链侧 2 个孔的物理 cut features，并完成正式 BOM 放行。

## 下一步目标

只做一件主线事：

1. 门板：继续基于 edge-flange + Unfold/Cut/Fold 方向处理铰链侧 2 个 10.2 孔；当前锁侧 2 个小孔已能切出且不破坏展开/折弯。
2. 前框：保留当前真实 SheetMetal/FlatPattern + 12 孔 cut features 证据；后续只做工程复核，不再作为孔切 blocker。

完成后重新运行：

```powershell
node tools\verify_16029_part_first_pilot.mjs --out data\locker_16029_part_first_pilot_gate_report.json
node tools\verify_16029_part_first_pilot.mjs --strict-artifacts --out data\locker_16029_part_first_pilot_gate_report.strict.json
```
