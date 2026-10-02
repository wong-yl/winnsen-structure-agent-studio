# ConfigureNativeWidth888

固定规格的 SolidWorks 2020 原生宽度阶段：把已验证的 V37 `760W` 最终 Pack-and-Go 克隆调整到 `888W`。用途仅为生成结构工程师可继续深化的辅助模型。

边界：

- 只负责柜体宽度相关尺寸、21 个派生钣金件、底座孔位和装配 X 变换。
- 不修改任何门板；新的 `1/12 W381` 门模块由独立门模块阶段生成。
- 不修改 `箱体竖隔板R.SLDPRT`，并强制校验其 SHA-256。
- 不创建或修改锁孔、锁槽、锁舌阵列拓扑。
- 不将根装配文件名从 `760` 改成 `888`；Pack/root 阶段必须另行精确重命名并重新验证引用闭包。
- 不使用 STEP、FreeCAD 或旧生成器。

## 可信输入目录

执行器必须设置：

```powershell
$env:WINNSEN_NATIVE_ATTEMPT_ROOT = '<data-dir>\native_model_requests\attempts'
$env:WINNSEN_NATIVE_PLAN_SHA256 = '<task-store-recorded native_build_plan.json SHA-256>'
$env:WINNSEN_NATIVE_EXECUTION_AUTH_PATH = '<attempt-dir>\execution_authorizations\native_width_888_v1.json'
$env:WINNSEN_NATIVE_EXECUTION_AUTH_SHA256 = '<worker-recorded authorization SHA-256>'
```

`<cad-dir>` 必须严格是：

```text
<attempt-root>\<task-id>\attempt-NNNN\native_cad\working_pack
```

证据 JSON 必须直接写入同一 attempt 的 `evidence` 目录。工具校验 immutable plan 的 SHA、recipe ID、recipe digest、用途和 `888×1917×550 / 2列 / 14门 / W381` 固定几何。

## CLI

四个阶段必须按顺序、逐个进程执行：

```powershell
ConfigureNativeWidth888.exe <cad-dir> <attempt-dir>\evidence\width-dimensions.json dimensions
ConfigureNativeWidth888.exe <cad-dir> <attempt-dir>\evidence\width-derived.json derived
ConfigureNativeWidth888.exe <cad-dir> <attempt-dir>\evidence\width-base-hole.json base-hole
ConfigureNativeWidth888.exe <cad-dir> <attempt-dir>\evidence\width-assemblies.json assemblies
```

该四阶段必须先在 seed 阶段签发的稳定化 75 文件克隆上完成，再进入 `1/12` 门模块、7 行锁拓扑和最终 Pack/root 重命名阶段。`dimensions` 要求固定路径 `receipts/clone_native_seed.json`：74 个非根文件必须保持 V37 原哈希，根装配仅接受 receipt/evidence/auth 明确绑定的稳定化 SHA；不会接受任意变化后的根文件。

每个阶段都会对 75 个 CAD 文件做完整哈希备份，只允许该阶段白名单内的文件变化；失败或发现意外写入时回滚。每阶段编辑完成后关闭所属 SolidWorks 进程，再用新的独占进程只读重开并验证尺寸、bbox、SheetMetal、FlatPattern、特征健康和文件哈希。

## 编译与静态自检

`/link` 将 SolidWorks interop 类型嵌入 EXE，因此 no-args 检查不依赖旁置 DLL。必须通过构建脚本更新归一化源码身份，再运行静态验证：

```powershell
$dir = 'workers\native_model_requests\development\v1\tools\width_888_native_v1'
powershell -NoProfile -ExecutionPolicy Bypass -File "$dir\Build-NativeWidth888.ps1"
node "$dir\verify_static.mjs"
```

授权文件只能是同一 attempt 下的
`execution_authorizations\native_width_888_v1.json`，并且必须由持有 lease 的 worker 单独签发；冻结计划仍保持 `planningOnly=true`、`executorImplemented=false`，计划本身不能启动此工具。

构建脚本会把归一化源码 SHA-256 编译进 EXE。可运行
`ConfigureNativeWidth888.exe --identity` 核对源码与 EXE 哈希；该命令不会启动 SolidWorks。

## 阶段事务补充门

- 四阶段使用 attempt 级互斥锁；每张 receipt 嵌入当次 authorization 原始快照并固定其 ID/SHA、最终 evidence 文件 SHA、evidence commitment、plan、tool、阶段前后 inventory digest，再用紧邻前一张 receipt 的 SHA 串联。成功 evidence 不含 receipt 路径/SHA/提交状态，从而避免循环哈希；先原子新建并逐字节复核 evidence，再原子新建 receipt，最后复核授权时间窗和双文件一致性。
- 历史 receipt 只校验自身证据及相邻链；仅“紧邻前一阶段 post inventory”允许与“当前阶段 pre inventory”比较。
- `working_pack` 必须保持扁平的 75 个 CAD 文件。工具递归枚举 CAD 树，拒绝子目录、reparse point 及 hardlink；失败回滚后再次核对原始 75 个名称和 SHA-256。
- 全链 inventory digest 统一为：文件名按 `OrdinalIgnoreCase` 排序，每行 `name|SHA256`、UTF-8、LF 且末尾 LF 后再做 SHA-256。每个宽度阶段 authorization 的 `seed.inventoryDigest` 必须等于该阶段实时 pre-inventory；不能把后续阶段继续钉在原始 `9E9CF348...F067B`。
- 冻结计划必须具有共享 `stageReceiptContracts`，以及 exact 六项 `trustedToolchainManifests`；宽度工具会实时复算 seed/width manifest、源码归一化 SHA、EXE SHA 和 verifier SHA，缺失、额外键、路径逃逸、reparse point、hardlink 或 live hash 漂移都会拒绝。
- execution authorization 最长 30 分钟，并绑定 `task.leaseExpiresAt`；authorization 的到期时间不得晚于 lease，到达完成/receipt/evidence 一致提交结束时两者都必须仍有效。worker 可在前一张授权确已过期、task lease 已续期且旧文件 CAS 未变化时，为下一阶段原子换发同一 tool 的授权。工具在阶段边界、每次保存前、证据提交前及 receipt 提交后复验；历史 receipt 按其内嵌授权快照校验，不依赖当前授权文件。该门只表示短期授权仍有效，不声称持续执行 task-store CAS。
- feature traversal 的深度、数量上限或 COM API 异常都会把 `traversal_complete` 置为 false，并使零件、派生件或装配体健康门失败。

静态通过只代表可以进入隔离 Pack-and-Go 的 SolidWorks 实跑阶段，不代表已经生成或验证了 888×14 模型。
