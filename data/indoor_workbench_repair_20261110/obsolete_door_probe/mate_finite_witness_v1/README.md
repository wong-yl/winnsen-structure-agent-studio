本目录是下一删门候选的有限面见证准备模块，未接入 v6 或任何生产流程。真实四面尚未采集；`Equal`、原始 strict 失败、warning32 和历史探针均未改动。

`MateFiniteWitness.cs` 提供 `CaptureTarget(...)`、`Compare(...)`、`CompareFour(...)`。采集函数只读取调用方已持有的 SOLIDWORKS COM 接口（运行中 CAD 提供的对象），不会启动、打开、激活、重建、保存文档。它需要四项目标逐一调用，返回的结构可由现有 JSON 序列化器写入候选证据，不引入新依赖。

受限范围：`箱体竖隔板L焊接.SLDASM` 内 `同心1` 至 `同心4`，`MateConcentric`/`mate.Type == 1`，entity index 0，`ReferenceType == 4`，`箱体竖隔板L-1` / `箱体竖隔板L.sldprt`。调用方传入新候选的绝对文档路径、配置、已锁定的 feature/owner-face 持久 ID；本模块不会从 v6 常量推导新候选身份。

记录内容：

- 配合所属装配坐标系的八项原始 `EntityParams`，feature/component/owner face 身份及配合状态。
- 当前活动根装配路径/配置、组件 `Transform2`、`GetTotalTransform(false)` 的 16 项原始数据及其坐标系标签。严格保留未使用的最后三项；它们变化也会保守拒绝。
- 通过底层零件的 `local.Extension.GetCorrespondingEntity2(ownerFace)` 取得实际 local `Face2`，再用 `component.GetCorrespondingEntity(localFace)` 映射回 owner，通过 owner-face 持久 ID 核对往返；local face 必须没有 assembly component context。官方 SDK 的对应示例明确由目标底层零件的 `Extension` 调用，见 `sdk_docs/Get_Corresponding_Entities_Between_Parts_and_Views_Example_CSharp.htm`。
- local 面持久 ID、surface identity、圆柱轴/半径、面积、面朝向、loop/edge 计数。全部 loops、edge IDs、vertex IDs/坐标；每环通过 `GetFirstCoEdge/GetNext` 遍历并验证闭环与 `GetCoEdges` 清单完全一致。
- 每条真实 edge 的 curve identity/圆参数，`GetCurveParams3` 的端点、UMin/UMax、sense、type、tag、有限区间长度和首尾 vertex。`IEdge.GetCurve` 必须先于 `GetCurveParams3`，已按官方 SDK 执行。
- 每条 coedge 的 edge 关联、方向和 `GetCurveParams` 有效的前八项。官方标记未使用的 slots 8/9 不参加计算，避免把它们解释为有效拓扑信息；独立 coedge 曲线若存在，仍记录可得参数并拒绝支持。

首版只接受完整有限圆柱带：恰好两个 loop、两条 edge，每环一个 coedge，每条 edge 是完整圆。校验实际 trim 周期为 `2π`、长度为 `2πr`、闭合端点、圆心/轴/半径与圆柱一致、顶点关联、两端轴向间距与面积一致。完整圆可以有一个首尾共用 vertex，或没有 topological vertex。真实四面是否符合此范围尚未验证。直线仅记录诊断参数；圆弧、样条、额外 seam/loop、奇异环、独立 coedge 曲线等均拒绝，保留在失败前已经读到的诊断。若读接口异常，`CaptureComplete=false`；比较重新检查完整性，不信任 `ProfileSupported` 标志。

`Compare` 只允许两个文档化的轴上代表点变化：`MateParameters[0..2]`、`Face.CylinderParameters[0..2]` 的差必须沿各自未变单位轴。方向、半径、所有有限边界数据、持久 ID、配置和组件变换仍逐项比较。沿轴搬动完整有限面会改变圆心、trim 端点、vertex/coedge 坐标，即使轴线、面积、半径和组件变换不变，也会被拒绝。保留现有数值容差 `1e-12 + max(abs(a),abs(b))*1e-10`；此模块不是完整 BRep（实体边界表示）等价证明，也不代表总模型验收。

旧输入缺证与下一次采集：

`input-evidence.json` 绑定两份旧日志 SHA256。旧 v6 `MateState` 只有 entity 参数与持久 ID，`ComponentSnapshot` 是变换、体统计等聚合信息；没有此处真实 Face2 有限边界。不得从旧面积、包围盒或轴参数合成所谓采集数据。下一新删门候选必须在 `before`、`edit`、`independent-cold` 分别对四面重新采集并保存；每份证据还要由现有候选运行框架绑定 CAD 文件、输入清单、探针/模块 hash、进程归属和阶段。此目录没有新建 CAD runner。本轮 v2 研究不接入本模块。

使用顺序（未来由已授权候选 runner 负责）：

1. 新候选 baseline 锁定四项目标的 owner/feature/component/face 身份和配置，并传入 `TargetBinding`；确保 `app.ActiveDoc` 对应传入的活动根装配。传入接口始终由调用方持有；本模块不主动 `ReleaseComObject`，防止使调用方共享包装失效。
2. 对四个 feature 逐一 `CaptureTarget(app, owner, feature, binding, stage)`，立即保留成功或失败结构。不要用相同路径但不同配置的解析零件替代；local 映射/配置缺证即拒绝。
3. 只有 `CompareFour(before, edit)` 和 `CompareFour(edit, independentCold)` 都成功，才得到本受限见证的等价结果；仍须保留原 strict/其他身份、依赖、几何、打开健康验收。这里不提供跳过原 `Equal` 的实现。

本地验证命令：

```powershell
& .\build.ps1
```

使用本机 .NET Framework C# 编译器与 SOLIDWORKS 2020 官方 Interop DLL；`/warn:4 /warnaserror+`。构建路径绑定内容版本，不覆盖已有结果。`PureCases.exe` 实际调用编译后 helper，通过合成解析圆柱带行为测试；测试不会创建 COM 对象或启动 CAD。`bin/<version>/build-result.json` 记录命令、输出、源/SDK/binary hash，`pure-results.json` 记录逐例结果。较早构建保留为审读历史；最终构建版本在 `freeze-manifest.json` 指定。

未验证：缺真实四面在新候选三个阶段的 COM 采集、实际拓扑种类及独立冷重开数据。当前通过的是模块编译和纯比较行为，不是 CAD 运行、旧模型 strict 验收、生产迁移或工程放行。
