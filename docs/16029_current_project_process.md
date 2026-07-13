# 16029 当前项目总进程

更新时间：`2026-07-13`

## 2026-07-13 当前判定

- 项目可行，但目标是“AI 结构协同 + 确定性 SolidWorks 2020 生成 + 工程门禁”，不是无人审核的生产 CAD 自动机。
- `v43-int-v18-lockfix` 保留为当前可下载的历史工程复核包；它通过的是旧结构 gate，不再称为生产交付包或 `engineer-ready`。
- `v43-int-v20-centerstrip` 是更新的受控技术候选，已补 6 个锁舌对齐 datum 和 1 个中间维护钣金，但新的精确结构 gate 判定为 `needs_structure_revision`：
  - 可见顶层锁孔基准 `27`，精确契约要求 `6`；
  - 左右柜体都残留不属于 `L642-R246` 当前门序的历史层板带；
  - 因此 v20 不进入当前下载清单，不升级为受控候选 PASS。
- `v43-int-v21-exact-normalized` 完成了历史层板带和重复锁孔 datum 清理，精确结构 gate 通过；但该次生成触碰了 gold/source 目录中的 5 个直接放置源/依赖文件，因此 v21 降级为技术证据，不作为最新下载候选。未找到可证明为 v21 运行前版本的字节级副本，未擅自覆盖恢复。
- `v43-int-v22-source-isolated` 虽然对已登记的 139 个受控源文件检查为 `PASS / changed=0`，但后续审计发现 placement TSV 仍直接引用 mirror template 目录，运行触碰了其中 2 个竖隔板文件，因此 v22 也降级为技术证据，不再作为最新下载候选。
- `v43-int-v23-all-sources-isolated` 将全部 placement 外部目录复制到短路径候选工作区后重写 TSV：302 个受控源文件前后 SHA256、长度、修改时间检查 `PASS / changed=0`；39 个模块放置和 7 个 restored-v43 放置均来自候选副本，最终装配外部引用 `0`。
- v23 的预签核证据包从本次 Pack-and-Go 主装配只读打开后精确隐藏 6 个门组件，生成 6 张内部结构视图；同时对 v21/v22 触碰文件的隔离副本做实体数、包络、体积、表面积和有限装配指标对比。指标相似不等于历史字节版本一致，不自动覆盖源文件。
- 新生成路线必须同时通过：1000W gold/source 最小结构 gate、结构反馈 gate、v43 精确结构契约。精确契约 PASS 只允许标记 `controlled_candidate_pass`，并固定 `release_eligible=false`。
- SolidWorks 放置来源件必须使用候选区副本；工具打开被放置组件时请求只读，并在证据中记录 `read_only_requested=true`。

当前执行结果：v23 已完成生成、精确门禁、全受控源完整性门禁、内部六视图、历史来源对比、工程预签核清单和两个 ZIP；结构签核 gate 当前为 `AWAITING_ENGINEERING_SIGNOFF`。下一步是结构工程师完成 7 项审查并作出“进入样机”或“退回结构修改”的明确决定。不扩展 800W/900W，不发布 DXF/BOM。

## 当前主线

- 当前工程复核包：`16029 / 740W / L642-R246 / v43 / v43-int-v18-lockfix`
- CAD 主线：`SolidWorks 2020`
- 结构参考：`1000W x 1917H x 550D` 是 gold/source reference，用于约束内部钣金结构，不是当前下载交付包。
- 柜门边界：沿用已确认的 `740W / L642-R246 / v43` 柜门路线，不再回到历史 direct assembly 逐零件乱装配路线。
- 生成排除：电器板、电控锁、电控锁钩不进入生成包；锁侧孔位、定位孔、安装界面和 datum 必须保留。

## 当前交付资产

- 最终请求：`v43-int-v18-lockfix`
- 主装配：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v18-lockfix\sw2020_full_740W_parametric_template\pack_and_go\candidate_16029_740W_L642_R246_v43_internal_sheetmetal_flat_full.SLDASM`
- ZIP：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generation_logs\review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip`
- 交付 manifest：
  - `data/locker_16029_v43_internal_sheetmetal_delivery.json`
- 交付记录：
  - `workers/maintenance/16029_v43_internal_sheetmetal_delivery_handoff_20260603.md`
- 截图目录：
  - `C:\Users\Administrator\Desktop\16029_v43_internal_steps_20260603\step_v18_lock_tongue_restored`

## 最新受控候选资产

- 请求：`v43-int-v23-all-sources-isolated`
- 状态：`controlled_candidate_pass`；`releaseEligible=false`
- 主装配：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v23-all-sources-isolated\sw2020_full_740W_parametric_template\pack_and_go\candidate_16029_740W_L642_R246_v43_internal_sheetmetal_flat_full.SLDASM`
- ZIP：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generation_logs\review_generation_v43-int-v23-all-sources-isolated_solidworks2020_full_assembly.zip`
- 工程预签核证据 ZIP：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generation_logs\review_generation_v43-int-v23-all-sources-isolated_pre_signoff_review.zip`
- 内部六视图与签核清单：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v23-all-sources-isolated\sw2020_full_740W_parametric_template\evidence\pre_signoff_review`
- v18 仍保留为已有人目视确认的历史复核入口；v23 作为最新受控候选单独展示，未替代生产释放或用户签核。

## v23 结构工程签核

- 当前状态：`AWAITING_ENGINEERING_SIGNOFF`；自动证据有效，尚无工程师签字副本。
- 签核模板：`data/locker_16029_v23_engineering_signoff.template.json`
- 完整签字副本保存位置：`data/locker_16029_v23_engineering_signoff.json`
- 模板绑定当前 generation summary、精确结构 gate、受控源完整性报告、来源报告及两个 ZIP 的文件长度和 SHA256；候选证据变化后旧签核不能沿用。
- 必须完成 7 项审查：锁/锁钩/定位孔共同基准、层板/前框定位界面、竖隔板加强与焊接可达性、非预期外穿孔、门缝/下垂/碰撞、钣金工艺与公差、历史受触碰源文件处置。
- 只允许两个决定：`ACCEPT_FOR_PROTOTYPE` 或 `RETURN_FOR_STRUCTURE_REVISION`。前者只允许进入样机阶段，仍固定 `production_release_eligible=false`；后者必须带失败项和修改证据。
- 工程师应在模板副本中完成全部字段后再保存为签字文件，避免半填写文件被门禁判为无效。项目程序不代签，也不提供匿名签核写入接口。

运行签核门禁：

```powershell
node tools\verify_16029_v23_engineering_signoff.mjs
```

## 当前判定

- Gold/source structure gate：`PASS`
- Structure feedback：`clean`
- 锁舌：`6` 个机械锁舌已恢复。
- 电器/电控锁残留：`0`
- 后背接缝：按侧板钣金 back flange 居中，不是贴 box。
- 用户已目视确认：锁舌位置、后背居中接缝、内部层板/前框配合。
- v23 精确结构 gate：`PASS`，11/11；门模块 `6`、锁舌 `6`、锁孔基准 `6`、中心维护钣金 `1`、电器/电控锁残留 `0`。
- v23 受控源完整性 gate：`PASS`；检查 `302` 个文件，变化 `0`；本地化放置 `39 + 7`；最终装配外部引用 `0`。
- 历史源对比：current 与 mirror 的 4 个目标零件几何指标一致；current 维护门与 v20 快照几何指标一致；backup 20190423 仅部分一致。以上结果不证明历史字节版本或特征树一致。

当前包可以进入 `SolidWorks 2020` 工程复核和交付整理，但不是生产图纸释放包。

## 平台入口

- FastAPI `/api/review-downloads` 首位展示 `16029-v43-internal-sheetmetal-lockfix-zip`。
- 同一目录展示 `16029-v43-internal-sheetmetal-v23-controlled-candidate-zip`，明确标记“最新受控候选（未释放）”。
- 同一目录展示 `16029-v43-internal-sheetmetal-v23-pre-signoff-review-zip`，只包含内部视图、来源对比和签核清单，不作为生产 CAD 包。
- 同一目录展示绑定当前 v23 证据的工程签核模板；API 和前端只读显示签核 gate 状态、自动证据状态、签字文件状态、样机资格和固定的非生产释放边界。
- 独立审核页 `tools/serve_16029_review_downloads.mjs` 运行在 `http://192.168.100.117:5180/`，当前轮次为 `16029-v43-v23-engineering-feedback-20260713`。登录后默认审核 v23 受控候选，每个结构问题单独生成 `V23-Q-*` 编号。
- 该审核页的模型下载文件统一同步到 `\\192.168.100.243\ys8870\参数化模型下载及反馈\模型下载`；工程师上传的 PNG/JPG/WEBP/PDF、结构字段和反馈 JSON 统一保存到同一共享根目录下的 `工程反馈`。登录用户可查看本轮团队问题和附件。
- 平台反馈固定记录 `feedbackIsEngineeringSignoff=false` 与 `productionReleaseEligible=false`；它用于整改沟通，不能代替结构工程师最终签核。
- 默认生成提示词和参数已切到 `740W / L642-R246 / v43`，并明确排除电器板、电控锁、电控锁钩。
- 同路线旧请求如 `v43-int-v16-*`、`v43-int-v17-*`、`v43-int-v15-*` 不作为默认当前任务展示；保留直链和历史证据，不删除。
- 800W LMS/SML/DUAL 包只保留为历史参考，不再是当前工程主线。

## 验证入口

每次代码/平台规则修改后运行：

```powershell
tools\verify_16029_current_mainline.ps1 -SkipWebBuild
```

该 gate 现在覆盖：

- API 编译和审核页语法检查；
- 1000W gold/source sheet-metal evidence 和 rules；
- v43 delivery manifest gate；
- 锁舌恢复规则和 gate；
- 当前 handoff scope gate；
- 当前候选只读六视图生成与独立 SolidWorks 会话退出；
- v23 预签核证据脚本语法、只读零件探针、精确门组件隐藏和来源零写入边界；
- v23 工程签核模板、证据 SHA 绑定、两种合法决定、7 项完整性校验、API/前端只读状态和 `production_release_eligible=false` 边界；
- 5180 审核平台的登录保护、v23 结构化反馈字段、附件类型/数量/大小校验、共享目录落盘、团队回显和附件读取边界；
- 首提交范围和 staged scope guard。

## 不允许混入

- 生成模型、日志、截图、zip；
- v15/v16/v17 试错包；
- 1200W、2117H、W537 历史试错线；
- SolidWorks 2025 当前主线说法；
- FreeCAD 作为工程师交付主线；
- 仅凭截图或能打开就标成生产 release。

## 一句话结论

16029 当前已收敛到 `740W / L642-R246 / v43`：v18 保留为已目视确认的历史工程复核入口，v23 已形成精确结构、全受控源隔离、预签核证据和防代签门禁闭环的最新受控候选。项目技术路线可行；当前等待结构工程师签核，生产释放仍需单独完成图纸、DXF/展开图、BOM、材料厚度、公差、供应商工艺和样机验证。
