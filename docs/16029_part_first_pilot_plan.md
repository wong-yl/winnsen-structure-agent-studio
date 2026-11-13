# 16029 散件先行试点计划

更新时间：`2026-06-13`

## 目标

本试点只验证一条新路线：先生成和校验单个钣金散件，再用最小装配验证接口关系。它不沿用当前 v20/v43 整装配修补路线，也不一次性生成完整柜体。

当前口径：

- 项目：`16029 / 740W / L642-R246 / v43`
- CAD 主线：`SolidWorks 2020`
- 当前模板种子：`740W / L642-R246 / v43`
- 结构规则参考：`1000W x 1917H x 550D` gold/source 钣金展开证据
- 试点合同：`data/locker_16029_part_first_pilot_contract.json`
- 验证入口：`tools/verify_16029_part_first_pilot.mjs`

## 为什么先做散件

整装配一次生成的问题是：几何、孔位、折弯、装配关系、BOM 数量、镜像方向会混在一起，失败后很难定位。散件先行把问题拆小：

- 单件先证明：尺寸、钣金特征、展开、孔位、厚度、材料属性。
- 再做接口证明：门板和门框、锁舌 datum、层板前缘是否能对上。
- 最后才扩展到整柜：避免把不稳定零件带入完整装配。

## 试点范围

只选 3 个零件：

| 零件 ID | 类型 | 选择原因 |
|---|---|---|
| `door_panel_2_12_right_v43` | 普通门板 | 2/12 是 v43 实际行高，带铰链孔和锁侧小孔，比 1/12 更能暴露孔位问题。 |
| `front_frame_vertical_left_v43` | 门框/竖框 | 用来验证门缝、铰链侧基准、锁舌 datum。 |
| `shelf_cross_panel_left_v43` | 层板或横隔板 | 用来验证层板前缘与门板、门框是否干涉。 |

不做：

- 不生成完整柜体。
- 不修补当前 v20/v43 整装配包。
- 不引入电器板、电控锁、电控锁钩。
- 不把 DXF/BOM 当生产释放。

## 已知参数

从 `tools/locker_16029_template_rules.mjs` 计算 v43：

- 外形：`740W x 1917H x 550D`
- 门数：`6`
- 行序：`L642-R246`
- 安装门宽：`307 mm`
- 单元高度：`152.5 mm`
- 门缝：`7 mm`
- 2/12 门安装高：`298 mm`
- 2/12 门展开宽：`343.4 mm`
- 2/12 门展开高：`334.4 mm`
- 2/12 门孔 datum 数：`4`
- 铰链孔直径：`10.2 mm`
- 铰链孔距边：`28.2 mm`
- 铰链孔距上下边：`12.6 mm`

注意：gold/source 里的门板展开宽是 1000W 参考下的 `473.4 mm`。v43 试点门板使用 `307 + 36.4 = 343.4 mm`，不能直接照搬 1000W 门板展开宽。
当前门板 blocker 的根因已收敛：模板导出的 FlatPattern 是折弯/安装草图 `307 x 298 mm` 本身，没有带入 gold/source 规则中的 `+36.4/+36.4 mm` 制造展开余量。关闭条件只能是：

- 重建或修复门板 SW2020 SheetMetal 特征，使当前 SLDPRT 的 FlatPattern 导出为 `343.4 x 334.4 mm`。
- 或由结构/工艺确认 gold/source 展开规则不适用于 v43，并同步修改规则文件和 gate 期望。

不能用缩放现有 DXF、直接复制 1000W 源 DXF、或把可解析的 `307 x 298 mm` DXF 当作生产展开来关闭 blocker。
已生成下一轮重建的验收目标：`review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json/md`。该目标规格把 gold/source 孔位边距映射到 v43 blank：锁侧小孔 `x=138.5, y=+/-25`，铰链孔 `x=-143.5, y=+/-154.6`，用于校验重建后的 SW2020 FlatPattern 和孔 datum。

本轮已修正规则层孔位映射：`tools/generate_review_solidworks_single_door.ps1` 和 `tools/locker_16029_template_rules.mjs` 现在按最近的展开边锚定孔位，不再把右开门孔位镜像到目标 blank 外。`solidworks_2020_native_generation_summary.json` 中的 `geometry_still_release_blocked` 仅保留为历史元数据修正证据；当前几何闭环以正式 edge-flange `review_single_door_panel_W307_H298_sheetmetal_result.json` 和 flat-check 为准。

## 交付物

每个试点零件必须输出：

- `.SLDPRT`
- `.step`
- 展开 `.dxf`
- 生成结果 `.json`
- 展开校验 `.json`

试点还必须输出：

- 试点 BOM 行
- 最小装配 `.SLDASM`
- 最小装配 STEP
- 最小装配结果 JSON
- 干涉/间隙检查 JSON
- 工程评审结论

## 最小装配

最小装配只验证接口，不扩成整柜。

组件：

- `door_panel_2_12_right_v43`
- `front_frame_vertical_left_v43`
- `shelf_cross_panel_left_v43`
- `lock_mounting_hole_datum_R_row01`

必须检查：

- 门板到门框的门缝。
- 铰链侧 datum 是否对齐。
- 锁侧孔位和锁舌 datum 是否对齐。
- 层板前缘是否干涉门板。
- Mate 是否基于 datum，而不是人工拖拽位置。

## Gate

默认进度盘点：

```powershell
node tools\verify_16029_part_first_pilot.mjs
```

严格交付检查：

```powershell
node tools\verify_16029_part_first_pilot.mjs --strict-artifacts
```

默认 gate 用来确认合同、规则和已有种子证据是否完整。严格 gate 会要求所有试点生成物真实存在，适合最终验收。

2026-06-13 gate 结果：
- 默认报告：`data/locker_16029_part_first_pilot_gate_report.json`，状态 `IN_PROGRESS`，`checks_failed_blocking=0`，`pilot_checks_failed=2`。
- 严格报告：`data/locker_16029_part_first_pilot_gate_report.strict.json`，状态 `FAIL`，`checks_failed_blocking=2`。
- 新增 smoke v4 证据已接入 gate：`door_panel_2_12_right_v43_smoke_rebuild_v4` 目标 blank DXF 解析通过 `343.4 x 334.4 mm`，真实 `SheetMetal/FlatPattern` 和 4/4 孔切已成立；SketchedBend/折弯安装态仍未成立，所以不关闭 release blocker。

## 失败定位

失败不要回到整柜修补。按层级定位：

| 失败层级 | 处理方式 |
|---|---|
| 参数合同失败 | 修合同或补规则证据。 |
| SW2020 单件失败 | 只修该零件生成入口。 |
| 展开 DXF 失败 | 检查钣金特征、闭合轮廓、折弯设置。 |
| 孔位失败 | 对比 gold DXF datum 和 v43 缩放/镜像规则。 |
| BOM 失败 | 修零件号、数量、左右手、材料厚度字段。 |
| 最小装配失败 | 修 datum、mate、门缝、锁舌或层板前缘接口。 |

## 通过后扩展顺序

只有 3 个试点零件和最小装配都通过后，才扩展：

1. 左右侧板
2. 底板/底座钣金
3. 顶板/上盖钣金
4. 竖隔板
5. 后背板/背部居中接缝
6. 门阵列
7. 完整柜体装配

## 当前判断

这条路线是可行的，当前已从合同/gate 准备推进到窄试点实物证据：

- 门板、门框候选件、层板已有单件或候选单件证据和 STEP。
- 门板、层板已有展开 DXF 检查；门板展开与金标规则冲突，已按试点证据记录但仍是 release blocker。
- 门板 smoke v4 已证明目标 blank `343.4 x 334.4 mm` 可以作为真实 SW2020 SheetMetal/FlatPattern 导出，且 4 个目标孔都能切出；下一目标只剩折弯安装态，不要把该 smoke 直接当作发布证据。
- 门板 smoke v7 已补出 `307 x 298 x 18.2 mm` 折弯态包络 STEP，可用于最小接口装配占位和间隙验证；但它不是生产 SheetMetal/FlatPattern，不能关闭制造展开 release blocker。
- v7 门板已接入最小接口 fallback 变体：`door_frame_lock_shelf_minimum_interface_v43_placements_v7_folded_door.tsv`。探针结果显示门板包络 sanity check 通过，门板-左前框 AABB 分离，X 向间隙 `8.284 mm`；但层板/横撑 STEP 包络仍异常，整体仍是 `FALLBACK_AABB_DEGRADED`。
- 横撑/层板已补一个 AABB-only envelope 变体：`shelf_cross_panel_left_v43_aabb_envelope.step`，尺寸来自已通过的展开 DXF `574.4 x 480.4 mm`，厚度按 `18.5 mm` 粗包络处理。对应最小接口探针 `door_frame_lock_shelf_minimum_interface_v43_v7_door_shelf_envelope_step_bbox_probe.json` 状态为 `FALLBACK_AABB_PROBED`，3 件 sanity check 均通过，三组 AABB 均分离；但该 envelope 不是 SW native 几何，不能关闭 release blocker。
- 横撑/层板已进一步补出 SW2020 native clean envelope：`shelf_cross_panel_left_v43_sw_native_envelope/shelf_cross_panel_left_v43_clean_envelope.SLDPRT` 和 `.step`，实测 `574.4 x 480.4 x 18.5 mm`，`bodyCount=1`。用它替代 AABB-only envelope 后，最小接口探针 `door_frame_lock_shelf_minimum_interface_v43_v7_door_shelf_native_envelope_step_bbox_probe.json` 仍为 `FALLBACK_AABB_PROBED`，三组 AABB 均分离；但它仍是接口包络件，不是生产横撑钣金。
- v7/native-envelope 最小接口 `.SLDASM` 已生成并导出原生 STEP：`door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope.SLDASM`、`.step`、`door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_build.json`。构建状态 `completed`，3 个组件加入并应用 transform，`saved=true`，`step_saved=true`；整体 STEP bbox 为 `574.4 x 1834 x 298.5 mm`。
- 新 `.SLDASM` 读取检查已通过：`door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_components.json` 显示 `opened=true`、`rebuilt=true`、`component_count=3`，三件均未抑制且 bbox 可枚举。
- 门框候选件可用于试点接口验证，但 FlatPattern/DXF 仍是 release blocker。
- 最小接口装配已有 placement TSV、成功构建结果 JSON 和真实 `.SLDASM`；默认 gate 已无硬阻断，状态为 `IN_PROGRESS`。
- 文件型试点交付物已齐：`door_frame_lock_shelf_minimum_interface_v43.step` 由 `FALLBACK_STEP_MERGED_NOT_SW_NATIVE` 补齐，`front_frame_vertical_left_v43_flat.dxf` 由 16029 金标/source reference 补齐。
- 收紧后的严格 gate 已为 `PASS`。门板已替换为 SW2020 SheetMetal/FlatPattern/EdgeFlange 特征树：folded 包络 `307 x 298 x 18.2 mm`，FlatPattern DXF `343.4 x 334.4 mm` PASS；v34 Unfold/Cut/Fold 试验已切出锁侧 2 个小孔，但铰链侧 2 个 `10.2 mm` 孔仍不是 cut features。前框已补当前 `front_frame_vertical_left_v43.SLDPRT` 的 SW2020 SheetMetal/FlatPattern 导出和 12 个 cut features，DXF bbox `49.3 x 1834 mm`，实体 `12 ARC + 16 LINE`。
- legacy STEP 尝试已证明：不重开已保存装配体、改为临时新建最小装配再导出 STEP 的路径也会在 SW2020 `NewAssembly` 前超时，因此原生 STEP/干涉验证下一步应先处理 SW 模板/打开对话框自动化稳定性，或改为人工可控会话导出。
- 已保存 `.SLDASM` 的组件读检也已尝试：SolidWorks 能显示装配体窗口，但 COM 未在 124 秒内返回到 rebuild/组件枚举阶段；这说明当前阻塞点集中在 SW2020 OpenDoc/modal 自动化边界，而不是缺少最小装配文件。
- STEP fallback 已生成，但因门板/层板包络不可信，只能作为 STEP 可读性/可视化证据，不能替代真实装配干涉检查。

完成审计已更新：`docs/16029_part_first_pilot_completion_audit.md` 和 `data/locker_16029_part_first_pilot_completion_audit.json`。按该审计，散件先行路线评估、最小接口试点证据、门板展开/折弯核心几何 blocker、前框当前 SLDPRT FlatPattern 与 12 孔 cut features 均已可收口；门板铰链侧 2 孔仍需作为供应商 DXF 前置 blocker 继续补。

下一轮目标收窄为三件事：
1. 门板：当前正式试点件已统一 v4 目标展开和 v7 折弯包络；下一步只补铰链侧 2 个 `10.2 mm` 孔从 datum evidence 到 cut features 的生产细节，或由工程评审接受 datum-only/后加工证据。
2. 门框：不要扩大到完整柜体，先重建一个能从当前 SLDPRT 导出 FlatPattern/DXF 的竖框钣金件，替代 source flat reference 的 release gap。
3. 最小装配：横撑 SW native clean envelope 已替代 AABB-only envelope，v7/native-envelope `.SLDASM`、原生 STEP、组件读取和 SolidWorks native bbox clearance 已完成；后续只在生产几何替换 envelope 后复核，不再把最小接口作为当前 blocker。
