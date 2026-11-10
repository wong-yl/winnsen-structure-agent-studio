# stage_continuation_v3：接口之后的隔离研究续跑

临时研究脚本，仅准备供主代理审核和独占执行。当前未执行 v3 native CAD，未验收整机。

v2 `PARAM-20261110104244594-C292D9` 的 `repair-interfaces` 原生证据为 success、进程退出码为 0；随后 wrapper（外层校验脚本）把实际 `.SLDPRT` 与预期 `.sldprt` 当成不同路径而停止，错误为 `Expected unique interface part`。v2 失败现场及原有保护项保持原样。

v3 从 v2 接口后的 75 件 working CAD、placements 后且 interfaces 未改变的 30 件门 CAD、13 件 door_sources 独立复制。`source-binding.json` 固定全部源路径、SHA-256（文件内容校验值）、224 个 v2 attempt 文件、v2 脚本及六个祖先阶段的证据链。门的生成只属于祖先研究证据，本次不生成门，也不复制旧 flat-import-manifest 充当本次生成凭证。

本次只运行：

`clone → repair-base-foot → locks → locks-verify → 停止`

每次新建独立 task store（任务记录目录）、task、attempt、plan、lease（限时任务持有权）和每阶段授权；复用原全局锁、串行 heartbeat（续期）及原 process helper（进程执行工具），保持 `preserveCadOnFailure=true`。不新增 kill、rollback 或 cleanup，不进入 root/package，不写生产 receipts。

clone 校验完整的 working75 + doors30 复制映射，对原生实际观察到的每次替换检查精确 source alias→新副本路径和 `replaced=true`。131 个允许的 alias（已留证原路径）包括 105 个当前源路径和 26 个祖先门生成 staging 路径。当前完整依赖图缺失，因此完整图、全依赖覆盖及保存后全量依赖重枚举均保持 false；祖先 clone 的 140 条边不是 v3 的期望数量。

祖先 envelope/interface 的左右板 SHA 并列保留在 `source*AfterEnvelopeSha256`、`source*AfterInterfacesSha256`，继承的 `rightAfterEnvelopeSha256` / `envelopeLeftSha256` 不覆盖。新 clone 后另记 `postCloneRightSha256` / `postCloneLeftSha256`；locks 核对这些新哈希与运行前文件、事务备份和左板种子，不声称种子字节等于祖先 envelope 左板。

路径来自 Windows API 的位置用 `equalPath` 忽略大小写；门源 manifest 的 role/path/SHA 仍要求精确逐字绑定。复制保留的 `checkClone`、`checkEnvelope` 和旧 140 边辅助函数仅用于祖先证据，不参与 v3 主执行。

成功续跑也必须保持 task=`blocked`、result=`candidate_requires_validation`、`modelAccepted=false`、`modelReady=false`。仍未验证 warning32、整机 root 集成、严格冷重开、有限曲面、精确新右板外形和完整持久化依赖；捕获的端点反射不能代替上述验收。

从实际项目根目录运行，禁止使用另一个 worktree：

```powershell
Set-Location -LiteralPath 'D:\Winnsen_Structure_Agent_Studio'
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v3/run-stage-continuation.mjs
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v3/research-gates.mjs
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v3/static-check.mjs
node data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v3/static-check.mjs
node data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v3/run-stage-continuation.mjs --dry-run
```

静态检查含真实 v2 interfaces 与门源 manifest；clone/locks 使用内存构造的校验样本，不提供原生 CAD 行为证据。dry-run 只生成本目录报告，不创建 attempt/task、不复制 CAD、不启动 CAD。最终 preflight（运行前检查）保护整个 PROBE、祖先 attempt、脚本及冻结构建；其他并行写入须先暂停，不能宽泛排除并行目录。

主代理审核冻结清单并独占 native 后，才可运行同一 runner 的 `--run-native`。本执行者只完成离线准备并返回 `ready_for_primary_review`。
