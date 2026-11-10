# 原 envelope 研究续跑

这是临时研究脚本，只准备隔离的 `clone -> envelope` 实验。尚未执行 CAD。原 v6、所有旧候选、旧 probe、生产工具、EXE、默认 runtime 选择均保留；不会生成生产 receipt、生产 resume 来源、下载或放行结果。

在实际项目根目录执行静态检查：

```powershell
Set-Location -LiteralPath D:\Winnsen_Structure_Agent_Studio
node data\indoor_workbench_repair_20261110\obsolete_door_probe\stage_continuation_v1\static-check.mjs
node data\indoor_workbench_repair_20261110\obsolete_door_probe\stage_continuation_v1\run-stage-continuation.mjs --dry-run
```

`--dry-run` 只在本目录写自有 schema 的报告，不建 CAD 副本、attempt、task 或锁。固定输入哈希、精确 75 件库存、完整 source→destination 计划、冻结 build、源保护及当前 CAD 进程状态都会检查。

主代理审查后才执行：

```powershell
node data\indoor_workbench_repair_20261110\obsolete_door_probe\stage_continuation_v1\run-stage-continuation.mjs --run-native
```

真实运行复用全局 `acquireNativeWorkerInstanceLock`、真实独立 `task_store` 的 claim/lease/heartbeat、逐阶段授权，以及原 `run16029ParametricPhaseProcess`。拿锁后读取真实 portal store，6 种运行/排队状态或任何非空 lease 都拒绝抢占；旧 blocked/needs_input 无 lease 的记录只读留证，其问题仍未解决。CAD/monitor 必须为空；预存 `sldworks_fs` PID 13080 / ticks 639298656825633090 不动。

plan 的 contract/doorModules/原 sourceRight 字段来自原 plan；只更新任务及路径身份，`resumedEvidence=null`。每阶段 auth 的 right hash 绑定当前合法副本，与原 `ParametricContext` 的授权逻辑一致。原 v6 是已经 relink 的研究输入，right hash 与原 plan 初始值不同，不构造跨版本等式。

新 attempt 不建 `receipts/`；研究记录都在 `research/`，schema 以 `obsolete-door-stage-continuation-` 开头。固定 EXE 原样生成的 `evidence/` 和合法 plan/auth 仅用于执行。最终任务固定转 `blocked`，结果为 `candidate_requires_validation`、`modelAccepted=false`。

检查原生输出的 27 件派生摘要（rebuilt/healthy/saved）、26 件目标边界、母件高度 target/readback、左右侧板与母件保存后重开；母件组合高度/深度检查由固定源码执行，JSON 未单列深度 readback，因此明确记录该限制。整机冷开、warning 32、有限边界全面等价及后续验收仍未验证。clone 的替换 old/new 仅允许精确 75 件映射，且逐 owner 的替换多重集合必须覆盖冻结 relink 图的 140 条剩余边；root 恰好去掉六个旧门边，并与 edit 保存后的 19 个直组件、13 个唯一路径一致。非 root 74 件以相同 SHA 绑定原图；替换 API 完整覆盖仍不声称已独立重新枚举闭依赖。

失败保留副本及证据，不回滚、不清理 CAD 文件。`preserveCadOnFailure=true` 会在 CAD 或孤立 monitor 残留时保留全局锁并等待人工处置；脚本没有按进程名杀进程或自动处理 monitor 的逻辑。不得把阶段进程 exit=0 当成验收。
