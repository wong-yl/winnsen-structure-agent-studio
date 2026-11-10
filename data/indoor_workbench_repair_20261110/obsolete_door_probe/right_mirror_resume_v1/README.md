# 已保存镜向候选续验

复用 `right_mirror_fresh_v1/runs/20261110125058267-28268/candidate/lock_right_native.SLDPRT`，固定 SHA256 `66F594147670F7BDD6FA835D874D2E8BDE10DC81B7905A6C3B8B9BA5C79B95FF`。原 `mirror-create` 超时会话继续保持 FAIL；自然完成后的保存成功记录只作为本次候选来源。

新入口仅运行 `temporary-cold` → `final-cold`。先把存量候选复制到原 v1 的唯一新 `runs/resume-*` 输出目录；临时冷开通过后才复制旧右板到新 final 目录，再执行原 `File.Replace(temp,final,backup)`，最后在新会话严格冷开最终名。原候选不移动、不覆盖，不重新 Mirror。

新 Probe 与冻结 v1 只有两处数值差异：Deadline 及 request 日志的 `180` 改为 `600`。`source-diff.json` 和 `.patch` 记录精确行；OwnedRoot、119 库存、源只读、0/0、saveFlag=false、原几何门、实际 source/status 3/4 规则均不变。Observer 复用原文件，600 秒诊断加 45 秒自然退出；全局锁、心跳、portal 和空 CAD 门沿用。

原 v1 全部 runs、超时证据、候选和全 PROBE 取当前保护哈希，仅精确排除本次尚不存在的新输出目录；PROGRESS 也取当前哈希。失败即停，超时保持锁并等自然退出，不杀 CAD。

本执行者只编译、语法检查、两处源差异核对并冻结，未运行 CAD。主窗口从 `D:\Winnsen_Structure_Agent_Studio` 执行：

```powershell
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_mirror_resume_v1/run-right-mirror-resume.mjs --run-native
```

`modelAccepted`、`operationComplete`、`strictColdAcceptancePassed` 始终为 false；本诊断不产生生产验收。
