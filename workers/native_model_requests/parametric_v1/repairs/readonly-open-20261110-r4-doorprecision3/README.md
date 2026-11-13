# precision3 进程身份清理候选

本候选由 `readonly-open-20261110-r4-doorprecision2` 的十个源码复制而来，仅修改 `NativeParametricWidth.cs` 的进程归属、身份核验和清理等待逻辑。其余九个源码的原始 SHA-256 与 precision2 一致，沿用 precision2 的数字转换修复和已验证结果。

## 触发问题

真实 `900×2000×500 L4/R9` 的 assemblies 功能已通过，但最终清理再次查看旧 monitor PID `24236`。该 PID 已被无关 `python` 复用，旧代码将其记录为 `cleanup_errors`，导致最终阶段失败与回滚。旧实现仅按 PID 归属 monitor，也无法安全区分同名进程复用。

同文件的 SLDWORKS 强制清理还存在身份核验后再次 `GetProcessById` 的间隙；旧等待函数把身份读取失败当成进程已不存在。本候选一并修正这些进程清理问题。

## 修改边界

- monitor 保存 PID、创建时间、可执行路径、捕获时间和已退出状态，按完整身份去重。捕获时要求精确的独占 owner SLDWORKS 身份，monitor 创建时间必须落在 owner 启动与捕获之间。
- 清理先获取同一个 `Process` 实例，固定其 `Handle`，再核对身份、执行 `Kill` 并等待。异常时只允许同实例的明确退出证据结束旧身份，不重新按 PID 获取替代进程。
- 已结束的 monitor 身份后续清理直接跳过；明确的 PID 复用不杀替代进程，也不误报旧身份清理失败。
- 读取身份或退出状态失败时保留失败；`WaitForExit` 返回 false 不能记录完成。基线进程保护与最终 global 进程列表/错误门禁保留。
- 建模功能、尺寸、孔位、数值容差、selection、任务和历史 evidence 均不在本候选修改范围。

## 本地验证

`node test_monitor_cleanup.mjs` 原样抽取生产方法、SessionRecord、MonitorProcessIdentity 和最终清理门禁，通过纯内存 `Process` 替身检查行为，不启动或停止 OS 进程。65 项用例全部通过，含 PID 24236 被 python 复用、同名不同创建时间、固定 handle 后原对象退出、Unknown 状态拒绝、等待失败、基线保护和最终 global 门禁。

该测试还验证只有上述进程相关代码块发生变化、另九个源码与 precision2 完全相同、最终清理门禁原文完全相同。

行为报告：`tests/runs/process-identity-ZZdIIG/report.json`。

`tests/final_validation.mjs` 绑定最终源码、build 验证库与 fixture 测试、生成源码和可执行文件 SHA-256，运行语法与 build identity 测试，并复用 hash 一致的 precision2 数字测试证据。

最终本地验证通过：`tests/runs/final-validation-CZSFPd/report.json`。该报告记录 fixture 全套退出码 0、5 个 Node 语法检查退出码 0、十源真实 build identity 核验通过，且检查前后的输入哈希全部一致。数字证据复用 213942 个值的 candidate 差异 0、旧实现对照差异 32991。

## 程序身份

构建命令：

```powershell
$env:WINNSEN_SOLIDWORKS_INTEROP_DIR = 'D:\soildworks2020\SOLIDWORKS\api\redist'
node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3/build.mjs
```

构建脚本拒绝覆盖已存在的 bin/build。使用 `.NET Framework 4` x64 编译器与本地 SOLIDWORKS 2020 Interop 编译成功，公开 `verify16029ParametricBuild` 核验通过。

- Build tag：`readonly-open-20261110-r4-doorprecision3`
- Executable SHA-256：`AD7E642311109DAE8ADD7463B7A8AEB60877301F59B6DDF08BBC3B97F7EB56FA`
- Manifest SHA-256：`E2AE899FDD6A37E3CCD8E067FCBBB766DDA02FE2EBECC5CD5F2C03622766988D`
- Width SHA-256：`835332B9A0DB25E98A7AFA83FE9E55D5E548C08312CE06A303FA287EE19D6E8F`
- ParametricContext SHA-256：`DACD0941306F9C85497B3C7F120A503DDC7C3C66C38EDB458163AA4CC185C6DA`

本候选等待主代理审查。离线测试不构成真实 Windows 进程清理、CAD 阶段或整机物理验收；这些运行验收由主代理负责。
