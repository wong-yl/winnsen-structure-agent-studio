# 888×14 原生柜门模块执行器原型

结论：本目录提供一个已通过无 CAD 进程静态编译与门禁的 SolidWorks 2020 C# 原型。它只用于在隔离副本中生成辅助结构工程师继续深化的柜门基础模；本阶段没有启动 SolidWorks，也没有生成或修改任何正式 CAD 文件。

## 固定契约

- 柜体宽度：`888 mm`
- 布局：`2 列 × 每列 7 门 = 14 门`
- 单门板：`W381 mm × H(1781/7) mm`，即约 `254.42857142857142 mm`
- 门板加强筋长度：`H - 10.5 mm`
- 输出：左/右门板、左/右 5 件焊接组件、左/右 7 件普通门组件
- 普通门五金：上下塑料轴套、门轴销、两个开口挡圈、一个机械锁舌
- 每扇普通门只放置一个机械锁舌；不接受电控锁或锁控板零件

机械锁舌的几何/接口拓扑证明是后续根装配拓扑阶段的外部前置证据。本门模块只核对其隔离副本为 `.SLDPRT`、SHA-256 与普通门中恰好一次的放置，不据文件名猜测或宣称锁舌拓扑已验证。

## 原生左右件路线

唯一允许的路线是 `right_panel_strategy = solidworks_mirror_part`：

1. 在隔离根目录内复制已经只读审计的原生左门 `1/12` 钣金源件。
2. 只修改 `D1@草图1` 门高和 `D2@草图1` 门宽，重建并保存左门副本。
3. 选择左门的 `右视 / Right Plane`，调用 SolidWorks 2020 `IPartDoc.MirrorPart2`。
4. `BreakLink=true`，并导入 `Solids + SMInfo + IndProps + CutListProperties`。
5. 右门必须同时通过：一个实体、`MirrorPart/MirrorStock`、`SheetMetal`、`FlatPattern`、无 `BaseBody/ImportedBody`、外部引用数为 0、实体极值点包围盒、左右 SHA-256 不同。
6. 关闭当前 SolidWorks 进程后，以新进程依次做只读重开、搬移重开、提交目录重开；每次都验证特征、包围盒、引用闭包和原始保存哈希。

不接受外部提供的右门种子，也不接受 V37 的 `BaseBody` 右件。右门必须由当前隔离任务中的原生左门通过 SolidWorks `MirrorPart2` 生成、断开链接并完成三次新会话重开验证。

## 事务边界

- 请求、全部源件、证明文件和结果文件必须位于同一个授权的 `isolated_root`。
- `isolated_root` 必须严格位于 worker 指定的 `<attempts-root>/<task-id>/attempt-NNNN`，且任务路径不得包含 reparse point。
- `WINNSEN_NATIVE_16029_ATTEMPTS_ROOT` 与 `WINNSEN_NATIVE_16029_PLAN_SHA256` 必须由 worker 提供；工具会读取并绑定不可变 `native_build_plan.json`、任务、配方和 SHA-256。
- 规划态计划只允许执行 `preflight`；`execute` 不读取计划中的执行开关，而是只接受由 worker 写入、路径和 SHA-256 均精确绑定的独立执行授权。
- 根目录必须放置 `.winnsen-native-isolated-root.json`；其内容参考 `isolation-sentinel.example.json`。
- `execute` 还要求 `--confirm-task` 与 `request.task_id` 完全相同。
- 执行前要求 `SLDWORKS` 与 `sldProcMon` 进程基线为空。
- 每个源文件先按 SHA-256 备份，构建只发生在 transaction candidate 中；输出目录已存在时拒绝覆盖。
- 每个 SolidWorks 阶段只创建并持有一个 `SldWorks.Application.28`，核验 revision 28.x、EXE major 28、PID、进程起始时间与路径，正常退出并确认进程基线恢复为空。
- 加强筋既校验原生钣金与驱动尺寸，也校验实体极值包围盒的最长边；门装配在重建后和每次新会话重开时逐组件回读路径、名称、旋转和平移。
- 源文件在执行结束时重新计算 SHA-256；任何变化立即判定 `NO_GO`。

## 构建与静态验证

在仓库根目录运行：

```powershell
& 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/Build-NativeDoorModule888x14.ps1'

& 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/Verify-NativeDoorModule888x14.ps1'
```

静态验证会重新编译 x64 `.NET Framework 4` 可执行文件，反射检查本机 SolidWorks 2020 interop 的 `MirrorPart2`、外链计数及钣金导入选项，扫描禁用路径，并运行不会启动 SolidWorks 的 `--help` 冒烟测试。

它还会运行无参数失败负控、`--receipt-self-test`、`--toolchain-self-test` 与
`--toolchain-live-check`。receipt 自测真实创建、复读、篡改并清理临时 evidence/receipt 对，
验证共享 ECMAScript canonical commitment、28 键 runtime receipt、前序 receipt/evidence
篡改拒绝、授权过期拒绝、不可变路径冲突拒绝，以及只清理由当前进程实际拥有的未配对提交物。
toolchain 自测验证 5 项 generic manifest 与唯一 lock 复合 manifest；live check 则动态读取当前
6 项 manifest，不钉死旧哈希。所有这些测试都要求 `SLDWORKS` 与 `sldProcMon` 前后为零。

## 执行授权

`native_build_plan.json` 永远是规划文件：`qualityBoundary.planningOnly=true`、
`executionBoundary.executorImplemented=false`。它本身不能授权启动 CAD。

只有 `execute` 模式要求由 worker 同时提供下列两个环境变量：

- `WINNSEN_NATIVE_EXECUTION_AUTH_PATH`：必须精确等于
  `<attempt>/execution_authorizations/native_door_module_888x14_v1.json`。
- `WINNSEN_NATIVE_EXECUTION_AUTH_SHA256`：该授权文件的 SHA-256。

授权文件 schema 为 `winnsen.native_execution_authorization.v1`，严格使用统一 camelCase
嵌套结构。`tool.id` 必须为 `native_door_module_888x14_v1`，且
`execution.phases` 必须严格等于 `["door_module_888x14"]`。授权同时绑定
`task.leaseExpiresAt`，授权到期时间不得晚于租约到期时间；最终证据提交完成前会再次验证
授权哈希、授权时间窗和租约时间窗。顶层及各嵌套对象不接受额外字段。

计划还必须包含共享合同规定的 9 项 `stageReceiptContracts` 与 6 项
`trustedToolchainManifests`，键集合必须精确匹配。工具会逐项读取 live manifest。seed、width、
door、root、final 五项必须是 exact generic schema，并复算源码规范化 SHA-256、EXE SHA-256
与 verifier SHA-256。lock 只接受
`winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1`，其主构建器、两个 inspector 与
validator 的路径、源码规范化 SHA-256 或文件 SHA-256 都必须逐项匹配。不会钉死其他阶段的旧
manifest 哈希，也不会把 lock 复合 manifest 当作 generic manifest 放宽处理。

门阶段运行前会沿固定路径消费 `clone_native_seed → dimensions → derived → base-hole → assemblies`
完整 receipt 链，逐项复核实际 evidence 字节 SHA-256、共享 semantic commitment、历史授权原始字节、历史 toolchain 身份、任务/配方/attempt 绑定及 inventory 前后链。当前门阶段授权的
`seed.inventoryDigest` 必须等于 width `assemblies` receipt 的 live `postInventoryDigest`，不能固定为原始 75 件基线摘要。

## 13 件输出与后续导入边界

本阶段保留 `parts/` 与 `assemblies/` 下的 13 个原生 CAD 文件，并额外生成不可变
`door_module_flat_import_manifest.json`。manifest 对每件 CAD 固定记录规范相对路径、
`flatTargetName`、文件类别、字节数和 SHA-256；13 个平铺目标名必须互不重复，并且不得与
受信 V37 `75 = 22 SLDASM + 53 SLDPRT` 工作包的任何文件名冲突。

本阶段不会把这些文件直接写入 `native_cad/working_pack`。后续 root assembler 必须读取并
复核 manifest 后，另行完成有备份、冲突门禁、引用重链和失败回滚的事务导入。门模块结果中的
边界值为
`ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK`，不能据此宣称
已经导入工作包。

静态验证只证明源码、编译与 CLI 边界；`static_verification.json` 不是 CAD 运行证据，
也不生成或修改任何 CAD 文件。已删除陈旧的 `current_source_gate.json`。

## CLI

仅做预检时使用独立 attempt；预检会写入该 attempt 的固定不可变 evidence 路径，因此同一 attempt 不再用于后续 `execute`：

```powershell
& 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.exe' `
  --mode preflight `
  --request 'D:\isolated-task\request.json' `
  --out 'D:\isolated-task\evidence\door_module_888x14.result.v1.json'
```

获得明确的隔离运行授权后才能执行：

```powershell
& 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.exe' `
  --mode execute `
  --request 'D:\isolated-task\request.json' `
  --out 'D:\isolated-task\evidence\door_module_888x14.result.v1.json' `
  --confirm-task 'the-exact-task-id'
```

同一份尚未到期的 door 授权必须先用于生成左门只读 proof。该子命令只读 attempt 内
`left_panel_seed`，固定提交 proof 与 audit，不消费授权，也不写 door 阶段 receipt：

```powershell
& 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.exe' `
  --panel-proof `
  --request 'D:\isolated-task\request.json' `
  --out 'D:\isolated-task\evidence\left-panel-proof.v1.json' `
  --confirm-task 'the-exact-task-id'
```

随后 `execute` 必须使用完全相同的授权 ID 与授权文件 SHA-256；proof 与 audit 会绑定这两个值。
授权和租约在 proof 提交后及 door 最终 evidence/receipt 提交前都会重新校验，绝不延长时间窗。
如果现有约 30 分钟授权不足以覆盖 proof 与完整 door 执行，本 attempt 必须失败关闭并新建 attempt、
重新签发授权和 proof；不能复用旧 proof，也不能放宽到期检查。

成功执行先以 CreateNew 方式提交并复读
`evidence/door_module_888x14.result.v1.json`，再提交并复读
`receipts/door_module_888x14.json`（schema
`winnsen.16029.native_door_module_receipt.v1`，phase `door_module_888x14`）。success evidence 不包含 receipt 路径、receipt SHA 或 committed 标志，避免形成哈希循环。

请求字段可参考 `request.mirror-part.example.json`。`left-panel-proof.example.json` 仅说明当前读取器要求的字段形状，不是可手写的运行证明，也不能作为执行输入。

## 左门 panel proof 受信闭环

`NativeDoorModule888x14.exe --panel-proof` 是唯一受信 producer，与 door 执行共用同一 generic
toolchain manifest 和同一 door 授权。它只接受 attempt 内固定 request/source/proof 路径，创建独占
`SldWorks.Application.28` 会话并以 `OpenDoc6(...ReadOnly)` 打开左门种子；逐项记录 owned PID、
SolidWorks revision/EXE、只读打开错误与警告、一个实体、SheetMetal、FlatPattern、完整递归特征遍历、
零 foreign feature、零特征错误/警告、零外部引用、D1/D2 驱动状态及源文件前后 SHA-256。

它先以 CreateNew 提交 `evidence/left-panel-readonly-audit.v1.json`，再提交
`evidence/left-panel-proof.v1.json`；两者都具有 exact key set 和排除自身字段后的 canonical
commitment。proof 再绑定 audit 实际字节 SHA-256、task/attempt、door tool source/EXE、授权 ID/SHA
及 left seed path/SHA。任何失败只清理由当前进程拥有的未配对 proof/audit，且不创建
`receipts/door_module_888x14.json`。不得手写 proof，也不得放宽 `ValidatePanelProof`。

## 当前验收边界

- 已完成：源码静态门禁、SolidWorks 2020 interop 契约反射、C# warn-as-error x64 编译、CLI `--help`、无参数负控、receipt 行为自测、5 generic + 1 lock composite toolchain 行为自测与 live 哈希核对、受信 panel-proof 合同行为自测、源码及 EXE SHA-256、无 CAD 进程前后对比。
- 未完成：真正运行 panel-proof、调用 SolidWorks 构建右门及装配、真实重开与工程师视觉/结构复核。因为本阶段明确禁止启动 SolidWorks，所以当前状态只能是 `STATIC_PASS_RUNTIME_NOT_RUN`，不能称为模型已生成或工程师已验收。
- 基准源审计见 `baseline_source_audit.json`；静态验证证据见 `static_verification.json`。
