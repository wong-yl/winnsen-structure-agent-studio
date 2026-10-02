# 888×14 原生总装执行器

`native_root_assembly_888x14_v1` 是 16029 `888W / 2列 / 14门（每列7门）` 的原生总装阶段执行器。它只服务于结构工程辅助流程，不复用 V35/V36/V37 旧生成器，也不把“文件已生成”表述为模型已可供结构工程师采用。

当前结论：源码、x64 EXE、共享合同快照、工具链 manifest 和静态负控均已实现并通过；本目录的验收没有启动 SolidWorks。真实 CAD 执行仍须由 worker 为一个具体 attempt 签发短时授权，并在后续 final pack、异地只读重开及结构工程师复核全部完成后再判断模型状态。

## 固定身份

- tool id：`native_root_assembly_888x14_v1`
- authorization phase：`root_assembly_888x14`
- recipe：`winnsen-16029-888w-14door-native-v1`
- recipe digest：`f07c5497f9de7d02727d8c084e5a7969a862c44c7990858cdef8e8e54feaceb8`
- purpose：`structure_engineering_assistance`
- SolidWorks：`SldWorks.Application.28`，只接受固定的 SolidWorks 2020 EXE 与 `sldProcMon` 身份

## 输入闭环

执行器要求 `native_build_plan.json` 保持 `planningOnly=true`、`executorImplemented=false`，并逐项验证：

1. `stageReceiptContracts` 是共享模块定义的完整 9-key 静态合同。
2. 前 7 份 live receipt 从 seed 到 lock 形成 SHA-256 前驱链，且相邻 `postInventoryDigest → preInventoryDigest` 连续。
3. 每份历史 receipt 的内嵌 authorization 原始字节、authorization ID、task/request/recipe/attempt、工具身份、历史完成时间和历史租约窗口一致；历史 authorization 不要求在 root 执行时仍未过期。
4. root 自己的 authorization 必须仍有效，且精确绑定当前 plan、当前 75 文件 inventory digest、当前 EXE/source identity 和唯一 phase。
5. 门模块 receipt 固定绑定 `evidence/door_module_888x14.result.v1.json`；锁阶段 receipt 固定绑定 `evidence/lock_topology_888x14.v2.json`。
6. `--door-module` 必须与门模块 evidence 中的 committed output 一致。其目录只能包含 manifest 加 13 个 CAD 文件，manifest 必须为 `NOT_IMPORTED`，且每个文件的大小、SHA-256、单硬链接和非 reparse 身份都通过。

## CAD 事务

事务开始前，工具保存并逐文件验证完整 75 文件备份。随后：

- 将门模块的 13 个文件逐一复制到 flat working pack，逐文件复核 SHA-256，无名称碰撞；物理 working pack 变为 88 个 CAD 文件（26 ASM + 62 PRT）。
- 将 4 个新门装配的依赖重定向到 flat working pack，并验证门模块 closure 恰好为这 13 个文件。
- 只在 frame 的直接子级按 12 个精确实例名删除旧横档，新增 L/R 各 6 个横档，位置来自受信 assembly contract，保持 unfixed。
- 只在 root 的直接子级按 6 个旧门和 4 个旧层板的精确实例名删除，新增左右各 7 个普通门（X=`±230.5 mm`），新增 L/R 各 6 个层板；门为 unfixed，层板为 fixed。
- frame/root 保存都要求 `errors=0, warnings=0`，关闭进程后再由全新只读会话重开。frame 重开要求 `0/0`；root 重开要求受信 V37 边界 `0/32`。

工具不使用模糊选择、通配符删除或递归 pattern 删除。原 75 个文件中只允许 root 与 frame 发生变化；导入的 13 个文件允许总装阶段为本地引用重定向而变化。旧的 33 个门文件在此阶段仍保留为物理文件，但 root closure 不得引用它们。

## 质量门

fresh read-only reopen 后必须同时满足：

- root top-level=`41`
- recursive=`294`，active=`294`，suppressed=`0`
- doors=`14`，shelves=`12`，active frame crossbars=`12`，mechanical tongues=`14`
- root closure=`55`（14 ASM + 41 PRT），missing=`0`，external=`0`
- 所有门、层板、横档的 source path、transform、fixed/active 状态逐行等于共享 assembly contract
- feature issue 为 0；或最多且只能是既有 tuple：`箱体右侧板焊接-1 / Reference / 51 / 51 / warning`

任何失败都会先把本工具拥有的 SolidWorks 2020 与 monitor 进程归零，再用完整备份恢复原 75 个 SHA-256，并删除精确导入的 13 个文件。无法证明 CAD 进程已归零时拒绝回滚，避免在 CAD 仍持有文件时写回。

## 成功证据顺序

成功路径严格按以下顺序提交：

1. `evidence/root_assembly_888x14.result.v1.json`
2. `receipts/root_assembly_888x14.json`
3. `evidence/root_assembly_888x14.v1.json`

root receipt 使用共享 28-key runtime receipt contract，同时绑定最终 evidence 的实际文件 SHA-256、共享 semantic commitment SHA-256 和 lock receipt SHA-256。receipt 提交失败时，未配对的 success evidence 会删除或隔离，然后回滚 CAD 事务。

## CLI

只能由已持有当前 attempt/task/plan/auth 的 worker 调用：

```powershell
$env:WINNSEN_NATIVE_16029_ATTEMPTS_ROOT = 'D:\...\attempts'
$env:WINNSEN_NATIVE_16029_PLAN_SHA256 = '<64-hex live native_build_plan.json sha256>'
$env:WINNSEN_NATIVE_EXECUTION_AUTH_PATH = 'D:\...\attempt-0001\execution_authorizations\native_root_assembly_888x14_v1.json'
$env:WINNSEN_NATIVE_EXECUTION_AUTH_SHA256 = '<64-hex live authorization sha256>'

& '.\NativeRootAssembly888x14.exe' `
  --working-pack 'D:\...\<task-id>\attempt-0001\native_cad\working_pack' `
  --door-module 'D:\...\<task-id>\attempt-0001\<door-committed-output>' `
  --out 'D:\...\<task-id>\attempt-0001\evidence\root_assembly_888x14.result.v1.json' `
  --confirm-task '<task-id>'
```

不要手工拼装 authorization 或跳过前序 receipt。`--working-pack`、`--door-module`、`--out` 和 `--confirm-task` 都有 attempt 内固定作用域校验。

## 构建和静态验证

以下命令会重建合同快照、嵌入 source/contract identity、编译 x64 EXE、重建 `toolchain_manifest.json` 并运行静态验证。它不会进入 CAD 执行 CLI：

```powershell
& '.\Build-NativeRootAssembly888x14.ps1'
```

也可单独运行：

```powershell
node '.\verify_static.mjs'
& '.\NativeRootAssembly888x14.exe' --static-self-test
```

静态验证包括编译后的 no-args/help/identity 分支、11 项内置行为负控、共享 receipt/commitment 合同、toolchain live hash、事务顺序和前后 CAD 进程零基线。报告写入 `static_verification.json`。
