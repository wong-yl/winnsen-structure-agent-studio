# 上盖右侧板 760 原生生成器 v1

本工具是结构工程辅助模型的受控原生试点，只新建 `760W` 上盖右侧板。它不导入旧 CAD，不调用旧生成器，也不提供 FreeCAD、STEP、FCStd、派生零件或拆链回退。

运行必须同时满足：

- 固定 `topcover_right_native_rule_v2.json` 及其 SHA-256；
- 固定 `topcover_right_native_signature_correction_v1.json`，详细几何签名只消除边方向和整圆参数缝噪声，其余几何门禁不变；
- 生成器目录内两份 SolidWorks 2020 interop DLL 匹配固定 SHA-256；
- 30 分钟内有效、绑定 rule/source/exe/output 的 attempt-local 授权；
- `SLDWORKS` 与 `sldProcMon` 全局稳定为零，并证明本次 PID、启动时间、路径、哈希及 `--ppid` 所有权；
- 按规则建立 22 线外轮廓、4 个 12 线敲落孔轮廓、11 个通孔、3 个合并 PEM 环形凸台；
- 使用 `0.8 mm / R0.2 / K0.5 / bend deduction 1.4 mm` 原生 base flange；
- 依次建立 5 道 `R0.5 / 90°` 原生 sketched bend，并核验方向、顺序和自定义折弯扣除；
- 保存前与 fresh read-only reopen 后均通过单实体、无外链、特征健康、folded bbox、质量属性、B-rep 拓扑和详细几何签名回归；
- evidence 与 receipt 均采用 `CreateNew + Flush(true) + reread`；失败只回滚本次新建且白名单内的 attempt-local 文件。

通过这些门只表示 `760W` 右侧板原生试点回归通过，不表示整柜模型完成，也不替代结构工程师评审。

普通运行参数：

```text
--mode right-760-pilot --rule <fixed-rule> --attempt-dir <TOPCOVER-RIGHT-PILOT-.../attempt-0001> --authorization <attempt/execution_authorizations/topcover_right_760_native_v1.json> --authorization-sha256 <SHA256> --out <attempt/evidence/private/topcover_right_760_native_pilot.json>
```

构建与验证：

```powershell
& '.\Build-TopCoverRight760Native.ps1'
& '.\Verify-TopCoverRight760Native.ps1'
```
