# 16029 项目续接：V35 目标质量与真正参数化生成

更新时间：2026-09-05 10:49（Asia/Shanghai）
工作目录：`D:\Winnsen_Structure_Agent_Studio`
当前分支：`codex/16029-door-rule-code-hygiene`
当前 HEAD：`5adb86b`
状态：项目执行已获用户授权；本次完成方向纠偏和续接记录，新的通用参数化改造尚未开始。

## 1. 用户最新目标与授权

用户明确要求：“你规划好，然后执行项目，别再绕了，这个项目持续好几个月了，一点成效都没有，要总结错误，以后避免这些错误。”

交付目标：同一 16029 柜体结构系列内，用户输入实际柜宽或门宽、柜高、柜深、各列门数与门高分配，程序自动生成可编辑的 SolidWorks 2020 原生零件和完整装配，质量达到用户认可的 V35，供结构工程师继续项目深化。

不再以“安排工程师试用、收集省时数据、再次全面人工审核现有模型”作为开发前提。CAD 和既有要求能确定的问题，由 agent 拆分比较、建模、检查和解决。仅对资料冲突或未定义的新结构要求提出具体问题。

## 2. 必须牢记的基准

- **1000 原始模型**：原始结构、模块层级、钣金及安装接口的依据。
- **V35**：用户明确看过并认可的目标质量，740W × 1917H × 550D、两列六门、L642-R246、一门一锁孔。
- **V36/V37**：后续四门和宽度变化的派生验证样本，不要再把“当前后台默认 V37”说成“用户明确确认的目标模型”。
- 用户已确认的删改优先于原始模型中的旧配置；不恢复被要求取消的电器板、电控锁、电控锁钩、衣杆等内容。保留相关安装孔和必要接口时须按最新已确认规则。
- “原生生成器”应复用可靠的原生零件、特征和建模步骤；不等于每次全部从零重画，也不等于调用旧通用生成器。

### 用户认可 V35 的直接证据

2026-08-09 21:01（本地时间），在 agent 打开 V35 后，用户说：“我看了模型，改的很好”。
21:11 用户明确：“我觉得如果能生成现在这样的模型就已经能很好的帮助结构工程师了，我的目的是帮助结构工程师减少重复的建模时间，提升效率”。
22:59 用户再次澄清：“客户想要门宽是多少，门的数量是多少……他们填入大致的参数，然后你生成相关的模型，然后他们拿你的模型来继续深改”。

原对话已核对，后续不必重新扫描约 300 MB 的历史会话文件。

### 基准路径

- 1000 原始模型工程目录：
  `D:\Winnsen_Structure_Agent_Studio\workers\analysis\desktop_reference\参数化模板素材_U盘原始_20260526\16029 寄存柜(标准组合式 1917×1000×550)\1.工程图`
- V35 模型目录：
  `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v35-one-door-one-lock-hole-fix-r1\native_cad\candidate_native_hierarchical_pack_and_go`
- V35 总装文件仍带旧名 `标准寄存柜1917×1000×550(总装配).SLDASM`，不要依据文件名推断实际宽度；V35 是 740W。
- V35 ZIP：
  `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v35-one-door-one-lock-hole-fix-r1\16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip`
- V37 当前交付绑定：
  `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_v37_engineering_assistance_delivery.json`

所有基准目录及已验证证据应保留；CAD 写操作使用隔离副本，验证前后源文件哈希。

## 3. 目前真实成果

2026-09-05 已核对：

- 5180 门户在线，V35/V36/V37 三个指定规格 ZIP 可用且完整性检查通过。
- V37 历史验证为 132/132，但保留已知 `箱体右侧板焊接-1 / Reference code 51`，打开需重建；不是无警告模型。
- R22 任务：`NATIVE-20260831T170546-C97F93`，revision 16，state `cancelled`，modelReady false，尚无结构化 checkpoint。
- R22 已完成 `clone_native_seed → dimensions → derived → base-hole → assemblies`。
- 独立审计 40/40：18 个本地 SplitBody 锁引用、4 个原生右件、底座孔位、22 个总成、72 个变换目标通过；没有意外改动文件。
- R22 **没有完成** door、lock、最终十四门总装、最终打包；不得说 888×14 整柜已经完成。
- R22 前五阶段真实运行约 14 分钟；这是某次开发验证用时，不是用户最终生成耗时承诺。

R22 attempt：
`D:\Winnsen_Structure_Agent_Studio\data\native_model_requests\attempts\NATIVE-20260831T170546-C97F93\attempt-0001`

重要文件：
- `native_build_plan.json`
- `native_cad/working_pack`：75 文件 = 53 SLDPRT + 22 SLDASM。
- `evidence/width-assemblies.receipt.json`：真实收据位置，不是 `receipts/assemblies.json`。
- `diagnostics/assemblies-r22-runtime-audit.json`。

R22 绑定：
- inventory：`0A023426F7F274048B9A6771B341D274E6B9C6E2E7DB4305CF59556DF2CB514C`
- plan SHA：`037ED09F412D8B3797F0A21320EA9753E9AF4CA83C062FCD133B5F92F545A423`
- assemblies receipt SHA：`4A6E53685B720AFFA9C24EFA51945D7B6346B85818A6DBD0F88972F8A45EF582`
- audit SHA：`E375E093E7BCD24882B4AA12BED45BD7CBC139EF3778D23468259E0A73A85B32`

R21 的中断恢复已完成，75/75 哈希回滚、失败授权隔离、任务取消。不要重做该恢复。

## 4. 已证实的问题及防重犯约束

| 过去错误 | 今后执行约束 |
| --- | --- |
| 把目标漂移成无人审核的生产 CAD | 定位固定为结构工程辅助模型；以用户认可的 V35 为目标质量 |
| 每个尺寸编一套专用代码，误称参数化 | 一套规则驱动不同输入；未预制尺寸无需改代码才能生成 |
| 重复做已过关固定规格、迟迟不完成整柜 | 每个阶段必须落到具体模型能力；R22 和样板用作回归，不无限重跑 |
| 过度追求历史面数、边数和几何分割指纹完全一致 | 重点检查工程尺寸、孔槽轮廓、接口、实体干涉、钣金和可编辑性；细分指纹只用于有必要的复刻诊断 |
| 静态测试通过就判运行准备完成 | 静态、CAD 实跑、完整模型验证分别陈述；通过测试不等于完整模型完成 |
| 生成和检查硬编码两套相同答案 | 共用参数定义，另用独立测量及基准几何验证结果，避免自证 |
| 忽略正常路径之外的错误处理 | 先复现，再修根因；覆盖未持有租约、并发、失败回滚，不能借用其他 worker 租约 |
| 大量子 agent 长时间空转 | Astra 负责方向/验收，Terra 仅接清晰有界任务，Luna 只做原子工作；限制并发、不递归扩张，有结果再复用 |
| 总进程文档、默认入口和开发目标不一致 | 以本文件为 2026-09-05 接续依据；旧文档历史描述不自动成为当前指令 |
| 将“登记新任务”等同于“能自动生成” | 门户明确区分已支持生成和需求登记；不得用现成 ZIP 冒充新参数生成 |
| 原始参考曾被 CAD 隐式写入 | 所有 CAD 写入在隔离副本；源文件不写、用哈希验证 |
| 估计工期反复变成承诺 | 先给范围、按模型里程碑复估；5–10 工作日只是此前粗略预算，不能声称会无人值守自动持续数日 |

### 必须先修的确定 bug

`D:\Winnsen_Structure_Agent_Studio\tools\run_16029_native_worker.mjs` 的 `runNativeWorkerFromCheckpoint` catch（约 810 行）：
检查点校验在本次调用取得租约前抛错时，catch 读取正在运行任务的 lease，拿 `latest.nativeBuild.lease.workerId/id` 调用 `taskStore.fail`。2026-09-05 已用纯内存 store 替身复现，确实尝试把另一个 worker 的任务标成 failed。

修复要求：仅当本次调用已原子取得新 lease，且当前 workerId/leaseId 仍是本次持有者时才能修改任务状态；取得租约前失败直接抛出。补一条在修复前必失败的回归测试。禁止因这个 bug 再设计一整套新框架。

### 当前参数化缺口

- `tools/locker_16029_native_recipe_registry.mjs` 仅登记固定 888×1917×550、两列14门。
- `width_888_native_v1/ConfigureNativeWidth888.cs` 的 `DimensionPlans`（约 9052 行）写死 760→888、610→738、300→364。
- 同文件的 `DerivedPlans`、`AssemblyPlans` 含固定包络和坐标。
- `door_module_888x14_native_v1/NativeDoorModule888x14.cs` 固定 TargetCabinetWidthMm=888、TargetDoorWidthMm=381、TargetDoorHeightMm=1781/7。
- `tools/lib/locker_16029_native_assembly_contract.mjs` 固定 ±230.5、十四门/十二层板/十二横档、输出文件名。
- 锁、root、final 的 C# 同样固定规格、阶段收据和工具链绑定；不能只改前端和配方 JSON 就宣称参数化完成。
- 900×14 和 760×4 目前可以被参数入口接受为任务，但没有可执行新建配方；只代表登记需求。

## 5. 下一步执行顺序

1. **先完成可读且可执行的模块/参数联动表。**
   基于1000与V35的模型、现有测量和成功脚本，逐项写清部件角色、基准、驱动尺寸、固定工艺尺寸、公式、关联件及自动测量方法。模块至少覆盖柜体侧板/加强件、上盖、底座/脚、门框、左右隔板、层板/横档、门板/加强筋、铰链/锁舌/锁孔。
   有证据的直接复用；仅缺证据的部件启动隔离 SolidWorks 只读探测。不要先重建所有基准件。
2. **修复续跑器租约 bug，保持原有工作流程。**
   修改范围优先限定 worker 和 worker verifier。不要提交代码，也不改 R22 历史计划/收据。
3. **完成宽度与门序驱动的完整整柜闭环。**
   初始保持已验证的 1917H/550D/两列结构以控制变量，但必须支持连续宽度、门数、等高与既有混合门序联动。用原生参数或小范围重建驱动真实特征，复用可靠零件。先完成一个完整非缓存模型，再对第二个新宽度复验。
4. **完成高度和深度的联动。**
   提取并实现相关侧板/隔板/顶底/门框/安装接口规则，确定可用参数范围；未验证的范围明确拒绝并说明具体原因，不能默认 120–2000 范围全部已支持。
5. **接通后台自动执行。**
   客户输入 → 计算规则 → 原生零件 → 总装 → 自动结构检查 → 打包下载。合格任务不需要 agent 手动改源码/摆组件。检查点服务于减少重跑，不主导产品设计。
6. **验收。**
   同一套代码生成 887W、913W 等未预制尺寸；覆盖 4/6/14 门及 V35 混合门高基线，随后覆盖高度/深度变化。检查外形、数量、孔位对中、配合、干涉、钣金/展开、保存重开、解压迁移后的本地引用与源不变。
   新模型要能说明与1000原结构和V35目标哪些一致、哪些是参数导致的合理变化。
   不把这些样本做成新的固定规格白名单来假装通用生成。

此前的 0.5–1 天拆分、2–4 天宽度/门序、1–3 天高深、1–2 天后台只是初估。第一阶段结束须用实际剩余问题修正，不要求用户再批准常规可逆实施步骤。

## 6. 现有工具与验证入口

目录根均为 `D:\Winnsen_Structure_Agent_Studio`。

- 参考映射：`tools/locker_16029_gold_source_manifest.mjs`、`tools/locker_16029_gold_module_targets.mjs`。
- 固定原生工具：`workers/native_model_requests/development/v1/tools/`。
- 任务与执行：`tools/run_16029_native_worker.mjs`、`tools/lib/locker_16029_native_stage_executor.mjs`。
- 历史检查点代码：`tools/lib/locker_16029_native_checkpoint.mjs`、`tools/lib/locker_16029_native_task_store.mjs`。
- 参数入口：`tools/locker_16029_native_generator.mjs`；门户：`tools/serve_16029_review_downloads.mjs`。
- 主线检查：`tools/verify_16029_current_mainline.ps1 -SkipWebBuild`；注意该脚本会编译/生成验证材料，先读再运行，最终变更后按相关范围验证。

历史最近测试（不是新通用参数化能力验收）：
- worker 12/12，task store 18/18，checkpoint 13/13，authorization 18/18，stage executor 26/26。
- 新发现的租约 bug 没有被上述测试覆盖，仍待修。
- checkpoint verifier 使用真实 R22 作为输入再复制临时副本，通用化前须了解这个依赖。
- 静态检查可能重编 exe/重写 manifest；R22 计划锁定旧 toolchain，不能无意让已验证检查点失效。

## 7. 续接时的工作区边界

- 工作区有大量历史未提交更改；`git diff --stat` 仅显示 tracked 27 个文件，不能忽略 untracked 原生代码。
- 旧生成器的删除和门户改动是已有工作；不得 git reset/checkout 恢复。
- 不自动 commit/push/tag/release/amend；生成 CAD、截图、日志、ZIP、二进制不混入提交。
- 此处结束前现场无正在运行的 SLDWORKS/sldProcMon/原生 CAD executor。新任务操作前重新核对。
- 不 fork 当前巨大会话作为新上下文；新建同项目任务，读取本文件即可。
- 使用当前本地目录最直接；如果新建 worktree，须先明确如何带入未提交及 ignored 的参考模型/实跑证据，不能默认这些存在于新 worktree。
- 新任务遵循用户最新 Astra/Terra/Luna 分工，最终结果由 Astra 复核；当前没有需要接续的活跃子 agent。

## 8. 新任务可直接使用的开场指令

在当前本地项目 `D:\Winnsen_Structure_Agent_Studio` 继续执行。
先完整读取 `D:\Winnsen_Structure_Agent_Studio\docs\16029_execution_handoff_20260905.md`。
按其中确认的1000原始模型与V35目标质量推进真正的参数化原生生成。
先落实模块/参数联动表和已复现的续跑租约 bug，随后生成未预制尺寸的完整模型。
用户已授权规划并执行，无需再问是否开始；不要重读数月历史、重做已完成验收或扩张辅助框架。每个里程碑报告实际模型/代码证据及剩余问题。

