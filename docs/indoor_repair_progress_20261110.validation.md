# 本次进度备份的上传检查

日期：2026-11-10，Asia/Shanghai。执行目录为 `codex/platform-recovery-20261110` 的独立工作树。以下检查只验证源码备份及离线逻辑，不代表整机修复完成。

| 检查 | 本次实际结果 |
|---|---|
| 运行与续跑 | `node --test tools/verify_16029_parametric_runtime.test.mjs tools/verify_16029_parametric_resume.test.mjs`：443项，442通过、0失败、1跳过。跳过本机历史16阶段记录回放，因为真实任务没有上传 |
| 构建身份 | `node tools/verify_16029_parametric_build.test.mjs`：退出码0；测试夹具核对十源、默认保护、身份漂移拒绝，不编译生产程序或启动CAD |
| 数字往返 | `node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision2/test_numeric_roundtrip.mjs`：213942值，新实现差异0、旧实现负对照差异32991 |
| 进程身份逻辑 | `node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3/test_monitor_cleanup.mjs`：65通过、0失败；使用内存替身，没有停止真实系统进程 |
| 源码语法 | 本次选入的29份 `.mjs` 全部 `node --check` 通过；选入Python源码AST编译、JSON解析通过 |
| 文件快照 | 104份来源文件与候选逐份尺寸、SHA256一致；精确来源和上传字节记录在相邻sources清单 |
| 上传范围与凭据 | 选入文件无CAD/EXE/DLL/压缩包/账号/原始任务或授权正文；高置信凭据模式扫描0命中，另做只读范围核对 |

原始检查日志及本轮临时测试输出留在本机，不随源码上传。`reargeometry1/test_rear_width.mjs`、`doorprecision3/tests/final_validation.mjs` 和研究runner依赖本机几何、原始构建、回执及已冻结文件；`obsolete_door_probe/v6/static-check.py` 还需未上传的历史对照源码 `v5/IdentityProbe.cs`，该项本次未复跑。本次没有伪造或去除这些输入，也没有声称它们能从GitHub单独运行。

网页、API和未变基线的旧检查结果见 [原平台验证记录](platform_recovery_20261110.validation.md)。本次没有重跑浏览器操作或原生CAD。新的右板重开故障仍然失败，详见 [当前状态](indoor_repair_progress_20261110.md)。

此分支上传不自动触发只针对 `main` 合并申请的GitHub工作流；没有以本次上传宣称远端CI通过。未合并主分支、未部署、未启用新候选、未生产放行。
