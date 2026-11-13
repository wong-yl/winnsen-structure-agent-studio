# 16029 门板上铰链孔规则决策包

日期：2026-06-17

## 决策问题

门板上铰链 `10.2 mm` 孔当前不能进入供应商 DXF 放行验证。原因不是 SolidWorks 切除参数，而是孔位 datum 映射到当前展开轮廓后落在无材料区域。

需要结构工程确认：是孔位 datum 错、左上角 corner relief / 法兰展开规则错，还是该孔应定义为后加工/工艺孔。

## 当前已确认事实

正式门板核心几何已闭合：

- SW2020 特征树：`SheetMetal + FlatPattern + EdgeFlange`
- 折弯安装态：`307 x 298 x 18.2 mm`
- 展开 DXF：`343.4 x 334.4 mm`，正式 flat check PASS

孔切状态：

- 锁侧 2 个 `2.0 mm` 孔：可切，且不破坏展开/折弯尺寸。
- 下铰链 `10.2 mm` 孔：flat-mapped Z 向选面可切，安全 variant 为 `1, 2, 11, 12`。
- 上铰链 `10.2 mm` 孔：16 个 flat-mapped variant 都不能选中 Z 向材料面。

## 展开坐标证据

当前门板 FlatPattern DXF 外轮廓是 12 条线段。用于孔位映射的偏移为：

- `mapped_x = source_x + 139.7`
- `mapped_y = source_y + 107.95`

| 孔 | source datum mm | mapped center mm | 材料状态 |
|---|---:|---:|---|
| 锁侧下孔 | `(138.5, -25.0)` | `(278.2, 82.95)` | 在材料内 |
| 锁侧上孔 | `(138.5, 25.0)` | `(278.2, 132.95)` | 在材料内 |
| 下铰链孔 | `(-143.5, -154.6)` | `(-3.8, -46.65)` | 在材料内 |
| 上铰链孔 | `(-143.5, 154.6)` | `(-3.8, 262.55)` | 在当前材料边界外 |

上铰链孔当前半径为 `5.1 mm`。在保持 Y=`154.6` 不变的情况下：

- 只让孔心进入顶边料带：source X 至少为 `-134.5`，即右移 `9.0 mm`。
- 让完整圆孔进入顶边料带：source X 至少为 `-129.4`，即右移 `14.1 mm`。

## 可选决策

| 选项 | 规则动作 | 优点 | 风险 |
|---|---|---|---|
| A. 移动上铰链孔 | 上铰链 source X 从 `-143.5` 调到至少 `-129.4` | 最容易让孔进入当前展开材料并生成 4/4 cut features | 上下铰链不再共线，可能破坏门轴/铰链安装基准，不建议默认采用 |
| B. 调整左上角 corner relief / 法兰展开 | 保持铰链孔 datum 不变，把左上角顶边料带向左补足至少 `14.1 mm` | 保持上下铰链共线，更符合门轴逻辑 | 需确认折弯避让、角部成形、涂装和外观间隙；会改变展开轮廓 |
| C. 定义为后加工孔 | 当前 SW2020 FlatPattern 只保留 datum，供应商在折弯后钻/铰该孔 | 不强改当前展开轮廓，适合试制 | 不能称为供应商 DXF 4/4 cut features；BOM/DXF 放行需工艺签字 |
| D. 修正门板手性/镜像展开解释 | 复核上/下铰链孔是否被错误映射到不对称 corner relief 一侧 | 可能是规则源头问题，修正后最干净 | 需要回查金标 DXF、门板左右手性、法兰展开方向 |

## 推荐判断

优先选 **B**，不要直接选 A。D 已完成窄范围复核，当前证据不支持“简单手性/镜像错配”是主因。

理由：gold 2/12 展开图中两个 `10.2 mm` 铰链孔上下对称、同一铰链基准；上孔中心 `(-208.5, 154.6)` 距 hinge-side 顶部料带边 `9.0 mm`，完整圆孔仍有约 `3.9 mm` 余量。当前 SW formal flat 中，上孔映射中心为 `(-3.8, 262.55)`，但左上顶部料带边在 `X=5.2 mm`；按 gold 规则应保留到 `X=-12.8 mm`，完整容纳圆孔的最低要求是 `X<=-8.9 mm`。也就是说当前左上 corner relief 比 gold 规则多切掉约 `18.0 mm` 料带，至少缺 `14.1 mm` 才能完整容纳上孔。

因此下一步只验证 **B：保持铰链 datum 不动，修正左上角 corner relief / 顶部法兰料带规则**。A 仍不推荐作为默认方案，因为只移动上孔会破坏上下铰链孔共线；C 只能作为工艺签字的后加工方案，不能关闭 4/4 supplier DXF cut-feature 目标。

补充审计报告：

- Machine report: `data/locker_16029_door_upper_hinge_gold_contour_audit.json`
- Human report: `docs/16029_door_upper_hinge_gold_contour_audit.md`

## B 候选验证结果

已按正式件参数复测：

- `edge-base-width-mm = 269`
- `edge-base-height-mm = 260`
- `edge-offset-mm = 18.2`
- `edge-position-offset-mm = 18.2`
- `edge-position-type = 6`
- `edge-bend-allowance-mm = 2`

结论：**简单 EdgeFlange 顺序/trim 参数不能闭合 4/4**。

- `native` / `horizontal-first`：可保持 `flat 343.4 x 334.4`、`folded 307 x 298 x 18.2`，但仍只有 `3/4` 孔， 上铰链孔仍选不到材料面。
- 其他顺序：多退化为 `269 x 260 x 0.8` 基板或无效法兰结果，不可用。
- `UsePositionTrimSideBends=1`：等同当前正式可用几何，仍 `3/4`。
- `UsePositionTrimSideBends=0`：退化为基板，不可用。

因此下一步不要继续扩大 SW 参数扫表。若选择 B，需要实现**自定义左上角 corner relief / 法兰草图规则**，使顶部 hinge-side 料带保留到 `X<=-12.8 mm`（最低 `X<=-8.9 mm`）。如果结构不批准该轮廓，则只能走 C：上铰链孔后加工/工艺签字，supplier DXF precheck 继续保持 blocked。

补充 B 候选报告：

- Machine report: `data/locker_16029_door_upper_hinge_candidate_B_probe.json`
- Human report: `docs/16029_door_upper_hinge_candidate_B_probe.md`

## 需要结构工程确认的问题

1. 上下两个 `10.2 mm` 铰链孔是否必须共线？
2. 当前上铰链孔 `(-143.5, 154.6)` 是否来自金标门板真实孔位，还是从下孔/另一手性门板镜像得到？
3. 左上角 corner relief 是否允许向左补料至少 `14.1 mm`，且不影响折弯避让？
4. 如果不能补料，该孔是否定义为折弯后加工孔？
5. 供应商 DXF 是否必须包含 4/4 物理圆孔，还是允许上铰链孔 datum-only 加工说明？

## 验证通过条件

结构规则确认后，重新生成门板并要求：

- `holeCutCount = 4`
- FlatPattern DXF bbox 仍为 `343.4 x 334.4 mm`
- folded bbox 仍为 `307 x 298 x 18.2 mm`
- SW2020 特征树仍包含 `SheetMetal + FlatPattern + EdgeFlange`
- default/strict gate 均 PASS
- supplier DXF precheck 从 `BLOCKED` 改为 `READY_FOR_REVIEW`
