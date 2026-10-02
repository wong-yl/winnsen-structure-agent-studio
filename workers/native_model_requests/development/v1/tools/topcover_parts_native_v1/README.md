# 上盖零件新原生生成器 v1

当前 Phase 2 只允许新建 `760W` 上盖前侧板原生试点。它不导入旧 CAD，不调用旧生成器，也不提供 FreeCAD、STEP、FCStd、派生零件或拆链回退。

运行必须同时满足：

- 固定 `topcover_front_native_rule_v2.json` 及其 SHA-256；
- 生成器目录内两份 SolidWorks 2020 interop DLL 必须匹配固定 SHA-256；
- 30 分钟内有效、绑定 rule/source/exe/output 的 attempt-local 授权；
- `SLDWORKS` 与 `sldProcMon` 全局稳定为零，并证明本次 PID、启动时间、路径、哈希及 `--ppid` 所有权；
- 20 条线闭环、10 个孔、0.8 mm / R0.2 / K0.5 原生 base flange；
- 先执行 y=1894.528761 mm 的 90° sharp bend，再执行 y=1916.856861 mm 的 180° round return bend；
- 保存前及 fresh read-only reopen 后均通过单实体、无外链、两道 `OneBend`、folded bbox、质量属性及 `48 faces / 112 edges / 14 cylinders` 回归；
- evidence 与 receipt 均采用 `CreateNew + Flush(true) + reread`；失败只回滚本次新建且白名单内的 attempt-local 文件。

通过这些门只表示 `760W` 前侧板原生试点回归通过，不表示整柜模型完成，也不替代结构工程师评审。

普通运行参数：

```text
--mode front-760-pilot --rule <fixed-rule> --attempt-dir <TOPCOVER-PILOT-.../attempt-0001> --authorization <attempt/execution_authorizations/topcover_parts_native_v1.json> --authorization-sha256 <SHA256> --out <attempt/evidence/private/topcover_front_760_native_pilot.json>
```

构建与验证：

```powershell
& '.\Build-TopCoverPartsNative.ps1'
& '.\Verify-TopCoverPartsNative.ps1'
```
