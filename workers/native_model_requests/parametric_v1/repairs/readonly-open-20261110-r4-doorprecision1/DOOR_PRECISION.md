# 门高精度候选后端

本候选的 10 个 `source` 文件复制自 `readonly-open-20261110-r4-reargeometry1/source`，仅修改 `NativeParametricAssembly.cs` 和 `NativeParametricDoorPlacement.cs`。旧候选及历史模型保持原值。

实际门高始终读取合同的 `rowsByColumn`。新 profile 的执行计划必须包含 JS 生成的 `doorModules: [{ token, heightMm }]`；它受既有计划 SHA-256 和授权链保护。C# 检查模块标识唯一、同高仅有一个模块、全部合同高度恰好覆盖、模块高度与合同完整值相同，再将阶段名、目录名与生成文件名映射到合同值。缺少映射、冲突或未知值直接拒绝，不从六位名称反解析实际尺寸，也不在 C# 重新四舍五入模块标识。

运行桥接只对 `readonly-open-20261110-r4-doorprecision1` 的前段使用候选程序，并核对前后两段的完整模块映射；其余旧 profile 的前段继续使用 baseline。原有阶段顺序、程序身份、回执链和停止条件继续检查。

验证命令（在项目根目录运行）：

```powershell
node --check tools/lib/locker_16029_parametric_runtime.mjs
node --check tools/verify_16029_parametric_runtime.test.mjs
node --test tools/verify_16029_parametric_runtime.test.mjs
node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision1/test_door_precision.mjs
```

C# 行为测试从候选源码原样抽取高度解析方法、门阶段调用和组件修正时的目录解析语句，以合成合同和记录生成参数的替身运行。覆盖自动 L4/R9、完整 `205.55555555`、历史显式 `heightMm`、同高去重、标识碰撞、缺失映射及 JS/C# 中点命名差异 `100.0000005`。每次运行的源码、抽取片段及编译结果记录在 `tests/runs/*/report.json`。

这些检查未运行 CAD、COM 或实际组件几何验收，不能代替整套编译、模型生成、干涉检查及交付包验收。本候选未改 CAD 碰撞容差。
