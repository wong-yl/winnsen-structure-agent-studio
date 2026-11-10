# 门高数值读取候选 precision2

`source` 完整复制 precision1 的 10 文件，仅 `ParametricContext.Number` 一处变动：从 JSON 读取的 `decimal` 通过框架内置 `double.Parse(decimal.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)` 转为 double；其他类型保留原 `Convert.ToDouble`。precision1 的源码与已编译产物保持原值。

precision1 中 CLR4 的 `JavaScriptSerializer` 先把部分 JSON 小数读取为 `decimal`，原 `Convert.ToDouble` 可令 `205.55555555555554` 变为 `205.55555555555551`。此前 C# 高度测试的预期值也经过该转换，未能发现差异。precision2 的测试以 Node 原始数字为唯一预期，完整走 JSON 写入、真实 production `Read`/`Number`、真实 serializer 输出、Node 解析及严格相等比较。旧方法作为对照必须出现差异，从而证明测试能够检出该缺陷。

在项目根目录执行：

```powershell
node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision2/test_numeric_roundtrip.mjs
node --test tools/verify_16029_parametric_runtime.test.mjs
```

数值矩阵为柜高 1700–2200 mm、每列 1–24 门、两列等分、宽 900 mm、深 500 mm。真实合同 builder 接受 8481 组、拒绝 3543 组不满足最小门高的输入。递归收集全部合同数字，涵盖门高、行位置、共享边界及派生结构尺寸，共 2455195 次数字出现、213937 个不同数值；再加入 6 个指定边界值，去重后共 213942 个。CLR 端不生成预期值，不进行近似比较。报告、精确 Node 输入、生成的 C#、程序及原始输出均保存在 `tests/runs/*`，报告包含输入与源码 SHA-256。

运行桥接对 precision1 和 precision2 均保留前后段候选程序身份、模块映射及完整门高严格校验。测试另注入已知的 `205.55555555555551` 漂移，并重新绑定回执，要求校验仍然拒绝。

数值测试未执行 CAD、COM、真实装配、干涉及交付包验收，不能代替原生模型验收。
