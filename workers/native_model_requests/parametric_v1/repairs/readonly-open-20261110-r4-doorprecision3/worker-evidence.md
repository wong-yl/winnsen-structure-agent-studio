# precision3 worker evidence

状态：`ready_for_primary_review`。真实 CAD 阶段、物理验收及整体完成决定由主代理负责。

## 改动与版本

- 新候选目录：`workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3`，含十源、build、行为测试、说明和本轮离线 evidence。
- 相对 precision2 仅 `source/NativeParametricWidth.cs` 内容变化，限定为进程捕获、身份核验、清理、等待与身份记录；其余九源逐字 SHA-256 一致。最终 global 清理门禁原文一致。
- `tools/lib/locker_16029_parametric_build.mjs` 注册 p3，继承 p2 的 Context/PhysicalAudit 许可，仅 p3 新增 Width 许可。
- `tools/verify_16029_parametric_build.test.mjs` 增加 p3 的构建、runtime acceptance fixture、来源漂移和受保护源码验证。
- 新构建输出：`bin-readonly-open-20261110-r4-doorprecision3` 与 `build-readonly-open-20261110-r4-doorprecision3`；构建未覆盖已有目录。

| Artifact | SHA-256 |
| --- | --- |
| NativeParametricAssembly.exe | `AD7E642311109DAE8ADD7463B7A8AEB60877301F59B6DDF08BBC3B97F7EB56FA` |
| build.json | `E2AE899FDD6A37E3CCD8E067FCBBB766DDA02FE2EBECC5CD5F2C03622766988D` |
| NativeParametricWidth.cs | `835332B9A0DB25E98A7AFA83FE9E55D5E548C08312CE06A303FA287EE19D6E8F` |
| locker_16029_parametric_build.mjs | `76E0447FA860D8DB447765BE788AC1C3EE05BBEF9E5E8617042C4E91254F4B48` |
| verify_16029_parametric_build.test.mjs | `D54BDCF3A5A49128FDFDE116C63D28670882EE0B07796B88AB7809BA3A72E943` |

## 已执行命令和结果

所有命令 cwd 为 `D:\Winnsen_Structure_Agent_Studio`。

1. `node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3/test_monitor_cleanup.mjs`：退出 0，65/65 行为测试通过；报告 `tests/runs/process-identity-ZZdIIG/report.json`。生产函数原样抽取，C# 替身不导入 System.Diagnostics、不操作 OS 进程。
2. 配置本地 Interop 后运行 p3 `build.mjs`：退出 0，x64 `.NET Framework 4` 编译成功。首次构建，输出身份如上。
3. 公开 `verify16029ParametricBuild(tag, { root: process.cwd(), repairProfile: tag })`：通过，十个源与生成源、exe、manifest 和来源匹配。
4. `node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3/tests/final_validation.mjs`：退出 0，5 个语法检查及完整 `node tools/verify_16029_parametric_build.test.mjs` 退出 0。报告 `tests/runs/final-validation-CZSFPd/report.json` 绑定最终文件哈希，`inputsUnchanged=true`。
5. `git diff --check -- tools/lib/locker_16029_parametric_build.mjs tools/verify_16029_parametric_build.test.mjs`：退出 0，无空白错误。Git 提示现有混合换行之后会规范为 LF，不影响本轮实际内容哈希。

## 关键行为证据

- 历史 monitor PID `24236` 被 python 复用：替代进程 Kill 次数 0，无虚假 cleanup_errors；已确认退出的旧身份重复 final sweep 不再查询 PID。
- 同名不同创建时间、不同 executable path 均不杀；仍存在的 global monitor/SLDWORKS 最终门禁继续拒绝。
- `Process.Handle` 固定后才读取创建时间/路径，Kill/Wait 使用同一个实例；核验后原对象退出，替代进程 Kill 次数 0，GetById 仅一次。
- monitor 捕获要求精确 owner PID/创建时间及监视器创建时间范围，按完整身份去重；旧 PID 被后续 owned monitor 复用可重新捕获。
- 身份、handle、lookup、wait 或 exit 状态读取失败不记已退出；属性 ArgumentException 不冒充 PID 不存在。Wait false 与成功等待但 HasExited false 都保留失败。
- baseline 进程保护、最终 global 非空/cleanup_errors 门禁保留。
- precision2 的 ParametricContext 原始 SHA-256 相同；复用其 213942 个数值严格往返证据：candidate 差异 0、旧实现对照差异 32991。

## 验证边界

本执行单元未启动 CAD，未操作真实进程清理。未验证：缺主代理真实 Windows/CAD 阶段与物理验收 evidence。纯内存替身检查控制流和保护条件，不替代实际 Process.Handle/COM 运行语义验收。没有向 build-selection、runtime selection、任务或旧 evidence 写入；没有提交或推送。
