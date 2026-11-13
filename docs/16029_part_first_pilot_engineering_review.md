# 16029 散件先行试点评审

更新日期：`2026-06-13`

## 结论

`散件先行`路线可行，而且比一次性生成整柜更利于模型质量控制。原因不是它已经通过全部工程 gate，而是本次窄试点已经把问题拆小并提前暴露出来：

- 门板能生成 SW2020 钣金件、STEP 和展开 DXF，但展开边界与金标规则冲突。
- 层板/横层板能打开、导出 STEP、导出展开 DXF，并完成 DXF 实体检查。
- 门框单件已换成 `v43-int-v20-centerstrip` 的 source-sheetmetal `front_frame_weldment_body_005`，已具备 SW2020 native SLDPRT 和 STEP；金标展开 DXF 也已接入为 source reference，但它不是当前 SLDPRT 的生产 FlatPattern 导出。

所以当前判断是：路线值得继续，但本轮不能进入生产放行，也不能扩展到完整柜体。

## 试点范围

本轮只验证 3 个散件，不沿用当前 v20/v43 整装配修补路线：

| 零件 | 当前状态 | 工程意义 |
|---|---:|---|
| `door_panel_2_12_right_v43` | strict gate 通过 | 当前 SW2020 edge-flange 特征树已统一目标展开和折弯包络；孔位仍为 datum evidence |
| `shelf_cross_panel_left_v43` | 通过当前试点检查 | 证明散件复制/打开/STEP/展开 DXF 链路可跑通 |
| `front_frame_vertical_left_v43` | 试点可用，生产展开未通过 | 已有 SW2020 native/STEP 和 source flat reference，FlatPattern 仍为 gap |

试点不包含电控板、电控锁体、电控锁钩，不输出生产 BOM，不输出供应商可用 DXF。

## 关键证据

门板：

- SW2020 零件：已生成 `review_single_door_panel_W307_H298_sheetmetal.SLDPRT`
- STEP：已生成
- 展开 DXF：已生成，实体数 `102`
- DXF 实测边界：`307 x 298 mm`
- v43 规则期望：`343.4 x 334.4 mm`
- 规则来源：gold/source `2/12` 门板基准安装尺寸 `437 x 298 mm`，展开 `473.4 x 334.4 mm`，因此制造展开余量是稳定的 `+36.4/+36.4 mm`；v43 门宽 `307 mm` 对应目标展开宽为 `307 + 36.4 = 343.4 mm`。
- 根因判断：当前模板把折弯/安装草图直接设为 `307 x 298 mm`，FlatPattern 导出也正好是 `307 x 298 mm`，没有把金标法兰/折弯展开余量带入；12-door seed 的 flat reference 也表现为安装宽高，不足以直接缩放闭环。
- 已生成门板 FlatPattern 目标规格：`review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json/md`。该规格给出 `343.4 x 334.4 mm` 目标 blank 和 4 个边距锚定孔 datum：锁侧小孔 `x=138.5, y=+/-25`，铰链孔 `x=-143.5, y=+/-154.6`。
- 已修正规则层孔位映射：模板规划、单门生成摘要和 gate 现在都按目标规格校验 4 个边距锚定孔 datum；旧摘要中右开门孔位被错误镜像到 blank 外的问题已作为元数据修复，但门板 FlatPattern 几何仍是 release blocker。
- 2026-06-13 追加 smoke v4：已在隔离目录 `door_panel_2_12_right_v43_smoke_rebuild_v4` 生成目标 blank `SLDPRT/STEP/DXF/JSON`。该件已有真实 `SheetMetal` 和 `FlatPattern` 特征，4 个目标孔切全部生成，展开 DXF 解析为 `343.4 x 334.4 mm`，实体为 `4 LINE + 4 CIRCLE`。
- 2026-06-13 追加 smoke v5：已尝试在 `InsertSheetMetal3dBend` 前补选固定面和折弯草图的 3 种选择顺序，结果仍为 `sketchedBendCreated=false`；因此当前阻塞点不是简单的“漏选固定面”，下一步应改走 EdgeFlange/真实法兰建模或人工 UI 录制参数确认。
- 2026-06-13 追加 smoke v6：已尝试 `EdgeFlange` 路径，先生成 `307 x 298 mm` 安装基板并识别到 4 条候选边；但 `edgeFlangeCreatedCount=0`，展开仍为 `307 x 298 mm`。这说明当前 API 参数/选择仍未形成法兰，不能作为折弯态证据。
- 2026-06-13 追加 smoke v7：已生成专用折弯包络几何 `SLDPRT/STEP/JSON`，实测包络为 `307 x 298 x 18.2 mm`。该证据可用于最小接口装配的门板安装态占位/间隙验证，但它不是 SheetMetal/FlatPattern 生产展开，不能关闭制造 DXF release blocker。
- 2026-06-13 追加 v7 接口替换验证：新增 `door_frame_lock_shelf_minimum_interface_v43_placements_v7_folded_door.tsv`，只把门板路径替换为 v7 折弯包络件。fallback AABB 探针显示门板包络通过 sanity check：`307 x 298 x 18.2 mm`；门板与左前框 AABB 分离，X 向间隙 `8.284 mm`。但横撑/层板 STEP 包络仍异常放大，所以整体状态仍为 `FALLBACK_AABB_DEGRADED`，不能关闭最小接口装配 release blocker。
- 2026-06-17 追加 edge-flange release candidate：正式试点件 `review_single_door_panel_W307_H298_sheetmetal.SLDPRT` 已替换为 SW2020 `SheetMetal + FlatPattern + 4 EdgeFlange` 特征树；folded 包络实测 `307 x 298 x 18.2 mm`，FlatPattern DXF 解析 `343.4 x 334.4 mm` PASS。
- 2026-06-17 追加供应商 DXF 前置试验 v34：`--edge-unfold-hole-cuts` 能在不破坏 `343.4 x 334.4 mm` 展开和 `307 x 298 x 18.2 mm` 折弯包络的情况下切出锁侧 2 个小孔；铰链侧 2 个 `10.2 mm` 孔仍未形成 cut features，因此门板供应商 DXF 孔切仍未闭合。
- 仍需工程确认项：4 个目标孔目前记录为 flat datum evidence，不是该 edge-flange 特征树中的物理 cut features；若供应商 DXF 必须带孔，需要继续补切孔特征。

层板/横层板：

- SW2020 零件：已存在 `shelf_cross_panel_left_v43.SLDPRT`
- STEP：已生成
- 展开 DXF：已生成，实体数 `92`
- DXF 实测边界：`574.4 x 480.4 mm`
- 结论：作为散件先行的正向样本成立。

门框：

- 试点零件文件存在：`front_frame_vertical_left_v43.SLDPRT`
- STEP 存在：`front_frame_vertical_left_v43.step`
- 来源：`v43-int-v20-centerstrip` 的 `front_frame_weldment_body_005`
- SW2020 证据：历史 import JSON 记录 `openedSource=true`、`savedNative=true`、`reopenedNative=true`、`savedStep=true`
- STEP 包络：`12.432 x 1834 x 18.5 mm`
- 金标竖隔板参考包络：`16.8 x 1834 x 18.5 mm`
- 展开 DXF：已接入 `门框 竖隔板L展开图.DXF` 的金标/source reference；解析实体 `112`，bbox `49.3 x 1834 mm`
- 当前状态：`SOURCE_GOLD_FLAT_REFERENCE_FOR_PILOT_ONLY`
- 结论：门框已足够进入试点接口装配和 source flat reference 校验，但不能作为当前 SLDPRT 的生产钣金展开/DXF 放行。

最小接口装配：

- placement TSV 已生成：`door_frame_lock_shelf_minimum_interface_v43_placements.tsv`
- `BuildPlacedComponentsModule.exe` 已成功用 placement TSV 构建最小接口 `.SLDASM`。
- 构建结果：`build_status=completed`，`saved=true`，3 个组件加入并应用 transform，`reference_count=3`。
- 真实 `.SLDASM` 已生成：`door_frame_lock_shelf_minimum_interface_v43.SLDASM`
- 最小装配 STEP 已补一个 `FALLBACK_STEP_MERGED_NOT_SW_NATIVE` 交换格式文件；它由 3 个 component STEP 和 placement TSV 合并而来，不是 SW 原生 `.SLDASM` 导出。
- fallback STEP 基础解析通过：`14846` 个 STEP 实体、`2967` 个 `CARTESIAN_POINT`。
- 打开已保存 `.SLDASM` 导出原生 STEP 失败于 `OpenDoc6_silent`，临时 legacy 重新装配导出 STEP 也在 SolidWorks 会话启动后、`NewAssembly` 完成前超时。
- 追加读检尝试：`InspectAssemblyComponents.exe` 能让 SolidWorks 显示已保存 `.SLDASM` 窗口，但 COM 调用在 124 秒内没有返回到 rebuild/组件枚举阶段，已清理本轮挂起的 `SLDWORKS` 和 inspector 进程。
- 干涉检查未运行。
- 已更新 gap report：`door_frame_lock_shelf_minimum_interface_v43_interference.json`
- STEP AABB fallback 已生成：`door_frame_lock_shelf_minimum_interface_v43_step_bbox_probe.json`
- fallback 结果：3 个 STEP 都可解析，但状态为 `FALLBACK_AABB_DEGRADED`；只有门框包络通过尺寸可信度检查，门板和层板点云包络明显大于名义/展开证据。
- v7 门板替换版已生成：`door_frame_lock_shelf_minimum_interface_v43_v7_folded_door.step` 和 `door_frame_lock_shelf_minimum_interface_v43_v7_folded_door_step_bbox_probe.json`。该版本把门板包络恢复到 `307 x 298 x 18.2 mm`，门板-左前框 AABB 分离，X 向间隙 `8.284 mm`；但在该早期版本中层板/横撑包络仍异常，所以它只证明门板占位方向有效，不能单独证明完整最小接口已通过。
- 横撑/层板 AABB-only envelope 版已生成：`shelf_cross_panel_left_v43_aabb_envelope.step` 和 `door_frame_lock_shelf_minimum_interface_v43_v7_door_shelf_envelope_step_bbox_probe.json`。该版本使用已通过展开 DXF 的 `574.4 x 480.4 mm` 外包络和 `18.5 mm` 厚度，仅用于 fallback AABB；探针状态为 `FALLBACK_AABB_PROBED`，3 个组件 sanity check 均通过，门板-前框、门板-层板、前框-层板均为 `AABB_SEPARATED`。
- 横撑/层板 SW native clean envelope 版已生成：`shelf_cross_panel_left_v43_sw_native_envelope/shelf_cross_panel_left_v43_clean_envelope.SLDPRT`、`.step` 和 `shelf_cross_panel_left_v43_clean_envelope_result.json`。该件由 SW2020 新建零件生成，实测 `574.4 x 480.4 x 18.5 mm`，`bodyCount=1`，STEP bbox 解析通过。
- 使用 v7 门板 + 真实前框 + SW native 横撑 envelope 的最小接口探针已生成：`door_frame_lock_shelf_minimum_interface_v43_v7_door_shelf_native_envelope_step_bbox_probe.json`。结果为 `FALLBACK_AABB_PROBED`，3 个组件 sanity check 均通过；门板-前框 X 向间隙 `8.284 mm`，门板-层板 Z 向间隙 `261.5 mm`，前框-层板 Z 向间隙 `252.25 mm`，三组 AABB 均分离。
- v7/native-envelope 最小接口原生装配已生成：`door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope.SLDASM`、`.step` 和 `door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_build.json`。构建结果 `build_status=completed`，`placement_count=3`，3 个组件均 `added=true` 且 `transform_applied=true`，`saved=true`，`step_saved=true`；原生 STEP bbox 解析通过，整体包络 `574.4 x 1834 x 298.5 mm`。
- 新装配读取检查已通过：`door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_components.json` 记录 `opened=true`，`rebuilt=true`，`component_count=3`；前框、横撑 envelope、v7 门板 3 个组件均未抑制，transform 和组件 bbox 均可枚举。
- SolidWorks native bbox clearance 报告已生成：`door_frame_lock_shelf_minimum_interface_v43_interference.json` 和 `door_frame_lock_shelf_minimum_interface_v43_v7_native_envelope_clearance.json`。状态为 `SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED`，`checks_not_performed=[]`，`blocking_reasons=[]`；前框-层板 Z 向最小分离 `252.141 mm`，前框-门板 X 向最小分离 `8.284 mm`，层板-门板 Z 向最小分离 `261.5 mm`。
- 结论：现在可以声称“最小接口 placement 在 SW native 干净包络下无 bbox 粗碰撞，且 gate 接受为最小接口 clearance 已完成”。但它仍是 v7 门板 envelope + 横撑 clean envelope 的窄试点证据，不能替代生产门板折弯态、前框生产 FlatPattern、精确 hinge/lock datum 和公差叠加签核。

## 结构工程判断

散件先行能提高工程可控性，主要体现在：

1. 单件尺寸、展开、孔位、折弯能先独立验收，避免整柜失败后无法定位。
2. 门板展开冲突被提前发现，没有被完整装配视觉效果掩盖。
3. 门框问题被限制在前框单件链路，不会污染侧板、顶底板、背板和整柜 mate。
4. BOM 可以先做试点行，保留 `pilot_only_not_released` 状态，避免误放行。

但它不能自动解决接口问题。每个散件正确，不代表门缝、锁舌、层板前缘和铰链基准正确。因此最小接口装配仍是强制 gate。

## 风险

| 风险 | 当前控制 |
|---|---|
| 门板孔位仍为 datum evidence | 工程评审确认是否接受；若供应商 DXF 要求带孔，继续补 cut features |
| 门板用缩放/复制 DXF 假闭环 | 禁止作为 release 修复；必须有匹配折弯态 SLDPRT 的 SW2020 FlatPattern 导出，或有工程批准改规则 |
| 门框 FlatPattern 不可导出 | 已接入 source flat reference；生产 DXF 前必须重建真实钣金特征并从当前 SLDPRT 导出 |
| SW COM / UI 模态框不稳定 | 最小接口 SLDASM 已保留；后续 STEP/干涉检查需避开 SolidWorks 打开/新建模板对话框 |
| COM 自动化会话不稳定 | 记录为自动化风险；不要用后续失败覆盖已保存的成功证据 |
| 已保存 SLDASM 读检超时 | 不能把“窗口可打开”当作“组件枚举/干涉检查已通过”；下一步需人工受控会话或先硬化 OpenDoc/modal 抑制 |
| STEP fallback 包络不可信 | 仅作为 STEP 可读性/可视化证据；不作为 clearance 通过依据 |
| BOM 被误用为生产 BOM | 所有行保持 `pilot_only_not_released` |
| 最小装配原生 STEP 和干涉检查缺失 | fallback STEP 不作为放行依据；不扩展到完整柜体 |

## 最小验证路径

下一轮只做 4 件事：

1. 门板：基于当前 edge-flange 正式试点件继续，只补孔切生产细节；不要再回到缩放 DXF 或整装配修补。
   - 当前最近证据：正式 SLDPRT 已有真实 SheetMetal/FlatPattern/EdgeFlange、`343.4 x 334.4 mm` 展开 DXF、`307 x 298 x 18.2 mm` folded 包络；未闭细节是 4 孔 cut features。
2. 门框：保留当前 `front_frame_weldment_body_005` 候选件用于接口装配；另行重建真实 SheetMetal/FlatPattern。
3. 层板：保留当前通过件。
4. 最小装配：v7/native-envelope `.SLDASM`、原生 STEP、组件读取和 SolidWorks native bbox clearance 已完成；后续只有在生产几何替换 envelope 后需要复核，不再作为当前严格 gate blocker。

通过条件：

- 3 个散件都有 SW2020 可打开证据。
- 3 个散件都有 STEP。
- 钣金件都有展开 DXF 和可解析实体。
- 门板展开/折弯核心冲突关闭。
- 门框单件 `SLDPRT + STEP + SW2020 SheetMetal/FlatPattern DXF` 已关闭原 source-reference blocker；当前重建前框已有 12 个 cut features，flat DXF 为 `12 ARC + 16 LINE`，bbox 仍为 `49.3 x 1834 mm` PASS。
- 最小接口装配通过或形成明确 datum gap 报告。

## 是否继续

建议继续，但不要扩大范围。新增完成审计见 `docs/16029_part_first_pilot_completion_audit.md` 和 `data/locker_16029_part_first_pilot_completion_audit.json`：路线评估、最小接口试点证据、门板展开/折弯核心 blocker、前框当前 SLDPRT FlatPattern 与 12 孔 cut features 均已可收口；下一阶段只处理门板铰链侧孔切和 BOM/DXF 放行评审，不应直接扩到整柜。
