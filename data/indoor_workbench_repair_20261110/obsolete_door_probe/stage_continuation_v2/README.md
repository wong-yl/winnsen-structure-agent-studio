# 阶段续行研究 v2

这是一次性研究脚本，只准备及执行原冻结工具的下列阶段：

`clone → envelope → door-471p25 → door-205p555556 → repair-door-placements → repair-interfaces → repair-base-foot → locks → locks-verify`

到 `locks-verify` 即停止；最终任务状态仍为 `blocked`，研究状态为 `candidate_requires_validation`，`modelAccepted=false`、`modelReady=false`、`releaseEligible=false`。不进入 root/package，不生成阶段或生产 receipts、不发布、不下载、不 push、不修改生产源码、EXE 或默认 runtime 选择。门生成原工具自身的 `flat-import-manifest.json` 只是原生门组件清单。

## 使用

所有命令必须从实际项目 `D:\Winnsen_Structure_Agent_Studio` 执行，不能从 worktree 执行。

```powershell
Set-Location -LiteralPath 'D:\Winnsen_Structure_Agent_Studio'
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v2/run-stage-continuation.mjs
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v2/research-gates.mjs
node data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v2/static-check.mjs
node data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v2/run-stage-continuation.mjs --dry-run
```

本次准备没有启动 CAD。原生运行由 primary 审查冻结文件后独占进行：

```powershell
node data/indoor_workbench_repair_20261110/obsolete_door_probe/stage_continuation_v2/run-stage-continuation.mjs --run-native
```

`--dry-run` 只读来源并在本目录写报告，不创建 CAD 副本、attempt、授权、租约或任务。`--run-native` 才在 `data/parametric_attempts/PARAM-...` 创建全新独立 attempt。每阶段从真实独立 task store claim/lease 获取授权；全局原 worker 锁、串行 heartbeat（租约续期）、每阶段新授权、原 process helper 都继承自 v1。`preserveCadOnFailure=true`，本脚本没有新增进程终止、自动回滚或 CAD 清理逻辑；原冻结 EXE 已有的严格会话退出与精确自有 monitor 处理保持原样。残留 CAD/monitor 时原 helper 保留全局锁等待处理。

## 来源和门槛

固定 v6 是 `candidate_pack_v6` 的精确 flat75，root SHA 为 `84ADA16033D0AC42FCA7461348D9E692DDE3DE154E755995AC097081F27B4F3A`。clone 逐 owner 要求冻结 140 条 replacement multiset（包含重复项的替换集合）精确一致、全部原生 replacement API 返回 true；不声称独立完成 clone 后的全依赖重枚举。

冻结 profile 为 `readonly-open-20261110-r4-doorprecision3`，EXE SHA 为 `AD7E642311109DAE8ADD7463B7A8AEB60877301F59B6DDF08BBC3B97F7EB56FA`。仍由 `verify16029ParametricBuild` 验证完整冻结 build/source 链，不直接调用未验证 EXE。原 plan 的 contract、contractSha、sourceRightPartitionSha256 初始来源和完整精度 doorModules 保持；新 taskId/taskPath/workerId 独立，`resumedEvidence=null`。

| 阶段 | 原生来源和本研究后验 |
| --- | --- |
| clone | `NativeParametricAssembly.cs:493` 原 Relink，冻结 75 owner/140 边；只改独立副本引用。 |
| envelope | `NativeParametricEnvelope.cs` 原生保存、27 个健康 summary、26 个目标边界、master/左右侧板保存重开门照旧。只登记下述唯一右板待替换，其他 broken link 拒绝。 |
| 两个 door 阶段 | `NativeParametricAssembly.cs:846` Generate → `NativeParametricDoor.cs:2712` Execute；复制 8 个源到独立 staging，在副本门板原生核对/移除已审 `草图12` 的两条旧引用，再原生生成装配，并执行 3 轮共 21 个重开门槛。 |
| repair-door-placements | `NativeParametricDoorPlacement.cs:970`，两种模块、两侧共 4 个装配行；复制精确 left/right top latch 源，原生使用 `ResolveContractDoorHeight`。 |
| repair-interfaces | `NativeParametricAssembly.cs:613`，右侧板旧接口的原生关系/几何门及焊接/总装保存；其他文件变化拒绝。 |
| repair-base-foot | `NativeParametricDoorPlacement.cs:873` 原特征和真实圆柱门，核对 x=±415 mm；其他文件变化拒绝。 |
| locks | `NativeParametricLocks.cs:21` Run，原生 L4/R9 孔形、源钣金和镜向门保留；核对本地 R 参数源、关联状态、正式右板 File.Replace 的实际 SHA/备份、原生反射 stdout 与捕获端点双向反射。 |
| locks-verify | 同一原 Run(readOnly=true)，要求原生两侧均只复用新生成的正确孔形；正式 L/R 与 R 参数源 SHA 不变，并复核 L4/R9。 |

### 唯一右板待替换

精确 v6 right 来源 SHA 为 `1DB48AC81F9D48B6BE43E86348E0363CCC5195A8BD21AC7C282063C7A2C6384A`。它不等于原 plan 初始 right SHA，不能混同两层来源。clone 后当前 right SHA 由本次新授权绑定。

只接受 `箱体竖隔板R.SLDPRT` 的唯一 `箱体竖隔板L镜向` / `MirrorStock`，七条引用均精确指向本次 working_pack 的 `箱体竖隔板L.sldprt`，七个 `sourceStatuses=0`，且 before/after 完全等于冻结 v1 所见旧几何（1 body、58 features、零特征错误/警告、钣金/展开保留和精确旧 part_box）。这里 `0` 表示 Broken，不把它写成健康的新关联。只记录 `pending_native_locks_replacement`，不作几何验收。

后续 locks 必须真正创建本地 `箱体竖隔板R参数源.SLDPRT` 并替换正式 right。核对 envelope 后 L SHA = pre-lock L SHA = `lock_seed_left.SLDPRT` SHA = L backup SHA，pre-lock R SHA = R backup SHA，正式新 R SHA 已变化并区别于参数源 SHA。原生 `MIRROR_VERTICES_VERIFIED`、`PARAMETRIC_LOCK_COLUMN_VERIFIED L/R` 必须出现在本次 process evidence 中。镜向 `referenceFiles` 全部绑定本次参数源，状态只接受 `3=InContext` / `4=OutOfContext`；Broken=0、Locked=1、Dangling=5 和未知值拒绝。

状态来源是实际本机 `SolidWorks.Interop.swconst.dll` 枚举及只读历史 `PARAM-20261110005000585-0301BA/evidence/locks.json` 的成功 R 行 `[3,3]`，二者均 SHA pin。历史证据只用于状态语义，不作为本次运行结果。新 `locks-verify` 必须原生成功、文件不变，才能把右板待替换状态记为研究范围已交接到新原生几何。

### 门源完整绑定和精度

原 `PARAM-20261110035409405-0C0A4D/door_sources.json` 整体 SHA 为 `B7E325E05A87E5161D12D63C3F14B086CBBD71A90FBC5E8CE543C31AD63C95E9`。原 manifest 精确 13 role，原目录恰好 13 件，逐 role 核对完整绝对路径、扩展名和 SHA；新 attempt 按同一个 role 独立复制，再逐项核对并写新 manifest。没有 basename 宽匹配，没有复用旧授权，没有靠改 JSON 路径冒充 Relink。

原 `sourceInputs` 同样先复制 13 源，原 clone 不扫描 door_sources。门生成的合法现有路线在 Execute 的副本准备、已审外引用处理、原生装配及三轮本地路径/变换重开中；本脚本保留这一路线和所有原生门槛。没有独立伪造 door_sources 全依赖闭包证据。maintenance_weldment、maintenance_lock_bracket、base_stiffener 源仍完整绑定并保护，本轮阶段不会打开这些额外源装配；后续工程反馈阶段不在本轮范围。

完整精度固定为 `471p25 → 471.25` 和 `205p555556 → 205.55555555555554`，token 只是目录标签，不能反推成舍入高度。原 contract 每门高度、Generate wrapper、panel/stiffener target 和 placement rows 都校验精确数值。

## 保护及证据边界

沿用 v1 原 18,612 个保护项的 SHA baseline，额外保护整个原 attempt、v1 attempt、全部 v1 脚本/报告、新 v2 当前文件、冻结工具/源码、原 13 门源和 PROBE 其它研究目录。重复路径若 SHA 冲突直接拒绝，不以当前快照覆盖旧 baseline。唯一跳过的是本 runner 自己的新研究目录在全 PROBE 递归时的重复遍历，随后本目录单独完整纳入保护。并行 mate helper 在最终 dry-run 前已冻结。

portal 原任务只读 `listSnapshot`，拒绝运行态或任意非空 lease；旧 blocked/needs_input 无 lease 的任务保持未解决。v1 历史 portal 只有 selected fields，没有历史整任务文件 hash；本 v2 dry-run 才新增保存当时实际整文件 SHA，不能回写成 v1 历史事实。

未验证：缺本次原生运行证据、整机 root 装配、warning32 解决、整机严格冷重开、全部有限曲面边界、独立持久化依赖重枚举及精确新右板外形边界。LockBase snapshot 没有 part_box，本脚本不把端点包围盒冒充精确外形，也不计算该包围盒。捕获端点反射只是补充原生镜向门，不是完整曲面或工程验收。

静态 synthetic fixtures（内存构造的测试输入）只证明证据拒绝行为。静态测试中另用真实 v1 envelope 检查唯一精确例外、用真实原门源检查完整 role/path/SHA；没有把它们当本次 CAD 运行。所有失败保留独立候选和证据，不自动回滚，不动旧实验。
