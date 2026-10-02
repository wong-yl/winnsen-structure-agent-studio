# 888×14 原生最终封装阶段

`native_final_pack_888x14_v1` 只承担 `final_pack_and_relocated_reopen` 阶段：把 root 阶段已经完成的 flat working pack 通过本目录专用的 SolidWorks 2020 PackAndGo 封装为结构工程辅助模型包，并完成两次独立只读重开。它不生成柜体、门、锁孔或总装结构，也不调用旧生成器。

当前边界：源码、x64 EXE、共享合同快照、toolchain manifest、内置负控和外层静态验证已建立；尚未在真实 attempt 上启动 SolidWorks，因此 CAD 封装结果、0/32 重开、55 文件闭包和异地重开均未做运行验收。

## 固定身份与路径

- tool id：`native_final_pack_888x14_v1`
- phase：`final_pack_and_relocated_reopen`
- purpose：`structure_engineering_assistance`
- input：`<attempt>/native_cad/working_pack`
- input root：`标准寄存柜1917×760×550(总装配).SLDASM`
- output：`<attempt>/final_native_package`，执行前必须不存在
- output root：`标准寄存柜1917×888×550(总装配).SLDASM`
- success evidence：`evidence/final_pack_and_relocated_reopen.result.v1.json`
- receipt：`receipts/final_pack_and_relocated_reopen.json`

CLI 不接受输出路径参数；所有读写位置都从已验证的 attempt 唯一派生：

```powershell
$env:WINNSEN_NATIVE_16029_ATTEMPTS_ROOT = 'D:\...\attempts'
$env:WINNSEN_NATIVE_16029_PLAN_SHA256 = '<native_build_plan.json SHA-256>'
$env:WINNSEN_NATIVE_EXECUTION_AUTH_SHA256 = '<authorization JSON SHA-256>'

& '.\FinalPack888x14Native.exe' `
  --attempt 'D:\...\<task-id>\attempt-0002' `
  --plan 'D:\...\<task-id>\attempt-0002\native_build_plan.json' `
  --authorization 'D:\...\<task-id>\attempt-0002\execution_authorizations\native_final_pack_888x14_v1.json' `
  --confirm-task '<task-id>'
```

## 强制门槛

- plan 必须包含共享 9 阶段 receipt 合同、六份 live toolchain manifest SHA 和当前 root 阶段。
- root receipt 必须是当前 attempt 的 28-key 成功 receipt；它的 evidence、授权原始字节、工具身份、前驱 lock receipt 与 live working-pack digest 必须一致。
- final 授权只能包含 `final_pack_and_relocated_reopen`，并精确绑定 task、plan、当前 88 文件 inventory、工具 source/EXE 和租约窗口。
- working pack 必须是 flat `88 = 26 SLDASM + 62 SLDPRT`；工具先建立逐文件 SHA-256 备份，PackAndGo 全程只读打开源 root，不调用 `Save3`。
- PackAndGo document count、目标名状态和保存状态必须分别为 55、全 0；最终物理闭包必须是 flat `55 = 14 SLDASM + 41 SLDPRT`。
- 两次全新只读会话都只接受 root `errors=0, warnings=32`，并要求 `41/294/294/0`、门/层板/横档/锁舌 `14/12/12/14`、依赖全部本地、没有旧门文件名。
- 第二次重开使用 `<attempt>/.final-pack-relocated-probe/package_copy`；验证完成后探针必须删除，最终只保留 `final_native_package`。
- 共享 assembly contract 的 `featureHealthPolicy` 必须精确为 `mode`、`maximumIssueCount`、`allowedIssue` 三键；仅允许一个大小写敏感的 `箱体右侧板焊接-1/Reference/51/51/warning` tuple。零个 issue 也必须携带这份有效策略，任何额外顶层或嵌套键都会被拒绝。
- 成功提交顺序固定为 evidence CreateNew → 实际 evidence SHA-256 → receipt CreateNew；evidence 不含 receipt 路径、receipt SHA 或 committed 字段。receipt 保存原始授权字节的 Base64、共享 ECMAScript commitment、88 文件 pre digest、55 文件 post digest 和 root receipt SHA。
- 任何失败都会先证明所拥有 CAD 进程已经退出，再删除输出、异地探针和未配对 evidence/receipt，并在需要时用完整备份恢复 working pack。PID、启动时刻、EXE 路径、EXE SHA 和 `sldProcMon --ppid=<PID>` 均需精确匹配；未知进程不会被终止。

## 静态构建与验证

下列命令会生成合同快照、嵌入 source/contract identity、以 x64 `/warnaserror+` 编译、刷新 manifest，并执行静态验证。它不会进入 CAD CLI：

```powershell
& '.\Build-FinalPack888x14Native.ps1'
```

也可以单独运行：

```powershell
& '.\FinalPack888x14Native.exe' --static-self-test
node '.\verify_static.mjs'
```

静态通过只说明执行器合同与负控闭环成立，不代表已经得到结构工程辅助模型；真实 CAD 运行、封装证据、异地重开及结构工程师复核仍是后续门槛。
