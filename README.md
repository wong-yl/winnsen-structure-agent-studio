# Winnsen Structure Agent Studio

Winnsen 硬件结构知识与智能钣金模型生成平台。

## 16029 当前工程入口（2026-10-02）

工程师通过 `http://127.0.0.1:5180/` 的账号门户定义规格、查看任务、下载模型和提交反馈。`apps/web` 的 5173 控制台保留项目、规则与历史资料查询；16029 生成请求统一进入 5180 门户，不通过旧 SQLite 任务入口建模。

- 自动受理范围：柜宽 700–1200 mm、柜高 1700–2200 mm、柜深 250–650 mm；两列分别 1–24 门，总计 2–34 门，每门至少 100 mm。受理范围表示允许提交，具体订单仍须通过原生模型验收。
- 左右列独立设置门数和非等高门序，编号自下向上。按高度固定已知门高，自动门补齐余量；按比例填写配比，系统按门格节距（门高 + 7 mm）分配。精度为 0.001 mm，预览、确认与提交使用相同尺寸。
- 修改柜高保留固定门高并重算自动门；修改门数只均分本列。空间不足、门高过小或未闭合时阻止提交。
- 参数化 worker 串行执行 SOLIDWORKS 2020 建模，只有通过尺寸、装配、钣金展开、干涉及迁移重开等检查的任务才提供交付包。网页预览和登录页 1000 × 1917 × 550 mm 三维展示不替代 CAD 验收。

在配置好的 Windows 建模主机上，通过现有 `tools/install_16029_review_portal_autostart.ps1` 注册登录后自启；不要在缺少源模型或 SOLIDWORKS 授权的机器上启动生成队列。源码仓库不包含原始 CAD、SDK DLL、已生成模型、真实账号、邀请码和本机配置。Native 工具需在建模主机按对应构建脚本编译。

操作和能力边界见 [参数化交付记录](docs/16029_parametric_delivery_20260915.md)，本次检查见 [项目检查报告](docs/project_audit_20261002.md)。

## Current MVP

已在 `apps/web` 搭建本地 React/Vite 控制台 MVP，当前按三组整理为七个页面：

- 工程交付主线：`项目总览`、`模型生成与交接`、`图纸生成与钣金出图`、`待确认项`
- 证据与规则后台：`数据录入状态`、`规则库成熟度`
- Agent 控制台：`结构 Agent`

运行方式：

```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npm install
npm run dev -- --host 127.0.0.1 --port 5173
```

当前本地服务地址：

`http://127.0.0.1:5173/`

生成任务队列 API：

```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

默认数据库：

`D:\Winnsen_Structure_Agent_Studio\data\studio.sqlite`

前端默认连接 `http://127.0.0.1:8000`。如需调整：

```powershell
$env:VITE_API_BASE_URL='http://127.0.0.1:8000'
npm run dev -- --host 127.0.0.1 --port 5173
```

## Project Role

This project is the application layer for managing CAD evidence, sheet-metal rules, SolidWorks-native generation workflows, FreeCAD migration workflows, and structure-review agents.

The existing CAD data and extraction assets stay in:

`D:\机械结构工程师智能体`

New raw parametric template materials are indexed from:

`C:\Users\Administrator\Desktop\参数化模板素材`

This App should read from that asset workspace and present:

- imported engineering model projects
- BOM/DXF/SolidWorks/STEP/FreeCAD pipeline status
- reusable sheet-metal rule families
- model generation capability
- manual review queues
- LLM-based structure review and risk analysis

## CAD Strategy

Current engineering work stays centered on SolidWorks because Winnsen engineers use SolidWorks today. FreeCAD remains in the MVP as an open-source replacement and migration route for future cost reduction, not as the primary checker for SolidWorks-native output.

The platform keeps both entries visible:

- SolidWorks: current engineering CAD path for native `.SLDASM` / `.SLDPRT` outputs and engineer review.
- FreeCAD: open-source migration path for `FCStd`, STEP, and repeatable geometry experiments.

This makes the transition evidence-based while avoiding a false rule that SolidWorks output must be checked by FreeCAD.

## MVP Scope

The first version is a local web dashboard, not a full CAD editor.

Current implemented pages:

- Project overview with a software page map
- Model generation and SolidWorks/FreeCAD engineering handoff
- Drawing/image/model intake for sheet-metal reference-model, unfold, dimensioning, and drawing workflow planning
- Manual review queue
- Intake pipeline status
- Rule maturity board
- Structure Agent console for generation boundaries, rule closure, and next gates

The Structure Agent console is now implemented as a boundary page: it tells the user what can be generated today, what is only a reference, and which evidence gates block arbitrary door-size changes.

## 16029 Native Foundation-Model Boundary

The current 16029 entry resolves customer parameters against a controlled SolidWorks-native model family. Its purpose is to reduce repetitive modeling work before a structural engineer continues the order-specific design.

Current behavior:

- The active 16029 path uses only the verified V35, V36, and V37 native SolidWorks seeds.
- An exact verified specification returns the matching structure-engineering foundation model after a fresh file-integrity check.
- A valid new specification automatically creates a persisted native SolidWorks task. It stays in `native_task_created` until a native builder and the structural-interface checks complete; there is no fallback to the removed generic/FreeCAD generator chain.
- The 16029 native-model path uses SolidWorks 2020: `C:\Users\Public\Desktop\SOLIDWORKS 2020.lnk`.
- The current engineer-facing foundation model is V37: `760W / 1917H / 550D / 2 columns / 6 doors / L642-R246`, with a 317 mm door-leaf width and SolidWorks 2020 as the CAD mainline. Open it, rebuild, then continue the project-specific structural work.
- `tools/locker_16029_native_generator.mjs` pins the V35/V36/V37 verified seed dimensions, asset IDs, ZIP sizes, and SHA-256 values. New combinations are calculated by `tools/lib/locker_16029_parametric_contract.mjs` and validated individually by the native parametric pipeline; there is no FreeCAD scaling fallback.
- `tools\process_16029_native_generation_request.mjs` is the only command-line request resolver. It returns an existing verified native foundation model or a normalized native-build task; invalid geometry and unsupported electrical-hardware requests fail closed.
- The next combination recipe is `760W / 4 doors / L66-R66` (V38): it combines the validated V37 width family with the validated V36 four-door topology. Requests can now be recorded as native tasks, but no model is offered until the native model and all structural-interface checks are completed.
- The account-protected portal on port 5180 accepts a sanitized task reference, cabinet or installed-door-panel width, height, depth, and independent left/right door counts and heights, totaling 2–34 doors. V35/V36/V37 exact specifications return the matching structure-engineering foundation model; valid unmatched specifications create a `native_solidworks_build_task` in `data/native_model_requests` for the parametric worker. Queued tasks are not downloadable until native validation completes. Feedback and application-effect records bind to the selected foundation-model request while earlier rounds remain readable as history. Copy `configs/review_portal.local.example.json` to the ignored `data/review_portal.local.json` for workstation-specific settings.
- On the review host, run `tools\install_16029_review_portal_autostart.ps1` once to register the current-user, windowless logon watchdog; no PowerShell window needs to remain open. Then use `tools\verify_16029_review_portal_host.ps1` to check autostart, firewall coverage, port 5180, the LAN status endpoint, current review round, and current asset availability. This user-level startup requires the host workstation to be powered on with that Windows user signed in.
- Historical routes and experiment packages remain only for traceability. They are not offered as current parameter results.
- Expanding the family means adding a named native recipe, generating it from a verified V35/V36/V37 seed, and completing its SolidWorks structural-interface checks before it appears in the parameter table.
- FreeCAD migration and geometry experiments remain separate platform capabilities and are not part of the active 16029 native generator.

This keeps the entry simple: exact known parameters return a native foundation model; valid new parameters create a native task instead of invoking an older substitute generator.

## Rule Learning Evidence

The rules page now includes a SolidWorks-native rule extraction entry for source template assemblies under:

`C:\Users\Administrator\Desktop\参数化模板素材`

The extraction package is written to:

`workers\rule_extractions\<RULE_ID>`

Each run can produce:

- `*_sw_api_snapshot.json`
- `*_components.csv`
- `*_mates.csv`
- `*_features.csv`
- `*_dimensions.csv`
- `rule_learning_summary.json`
- `rule_learning_summary.md`

Current 16038 / 16029 / 16028 top-assembly comparison is written to:

- `data\rule_extraction_comparison.json`
- `data\rule_extraction_comparison.md`
- `data\rule_seed_candidates.json`
- `data\rule_seed_candidates.md`
- `data\rule_seed_review_ledger.json`
- `data\rule_seed_review_ledger.md`
- `data\rule_seed_evidence_checklist.json`
- `data\rule_seed_evidence_checklist.md`
- `data\rule_seed_quantity_formulas.json`
- `data\rule_seed_quantity_formulas.md`

Latest measured comparison:

| Template | Components | Transform | BBox | Mates | Pattern seeds | STEP bboxes | Role bindings | Quality gate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 16038 洗衣寄存柜同尺寸多门数 | 38 | 0/38 | 0/38 | 82 | 1 | 322 | 279 / 155 | `step_role_binding_available_needs_formula_derivation` |
| 16029 标准寄存柜 1917x1000x550 | 38 | 0/38 | 0/38 | 75 | 3 | 208 | 177 / 79 | `step_role_binding_available_needs_formula_derivation` |
| 16028 标准寄存柜 1917x1000x485 | 36 | 0/36 | 0/36 | 77 | 2 | 370 | 311 / 139 | `step_role_binding_available_needs_formula_derivation` |

Interpretation: the standard SolidWorks assemblies are not treated as missing component trees. They are useful master templates: the platform reads component names, mates, feature dimensions, and local pattern seeds, while STEP-derived bboxes and role bindings provide the placement evidence that SolidWorks transform export does not currently expose. For example, 16029 exposes `层板阵列=11 x 152.5mm`, `柜门阵列=6 x 305mm`, and `衣架钢管整列=2 x 915mm`. The next technical gate is to derive formula candidates for door panels, dividers, locks, hinges, and shelf panels, then close those formulas against BOM/DXF evidence before treating same-size door-count generation as rule-backed.

## Current Data Snapshot

The MVP data model is currently implemented as a local TypeScript snapshot in:

`apps\web\src\data\studioData.ts`

The generated CAD status snapshot is written to:

`apps\web\src\data\generatedSnapshot.ts`

Refresh it with:

```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npm run build:snapshot
```

It summarizes these existing CAD status assets without copying raw CAD files:

- `D:\机械结构工程师智能体\outputs\intake\intake_pipeline_status_v1.md`
- `D:\机械结构工程师智能体\outputs\intake\outdoor_courier_family_intake_status.md`
- `D:\机械结构工程师智能体\outputs\freecad\locker_16029_strict_geometry_upgrade_queue.md`
- `D:\机械结构工程师智能体\outputs\freecad\outdoor_courier_production_gate_queue.md`
- `D:\机械结构工程师智能体\outputs\freecad\outdoor_courier_single_door\outdoor_courier_waterproof_door_1_12_R_single_panel_v1_source_signature_audit.md`

The new parametric template catalog is generated by:

```powershell
cd D:\Winnsen_Structure_Agent_Studio
python workers\maintenance\index_parametric_template_assets.py
```

It writes:

- `data\parametric_template_catalog.json`
- `data\parametric_template_catalog.md`

The SolidWorks rule extraction comparison is generated by:

```powershell
cd D:\Winnsen_Structure_Agent_Studio
python workers\maintenance\compare_rule_extractions.py
python workers\maintenance\build_rule_seed_candidates.py
python workers\maintenance\build_rule_seed_review_ledger.py
python workers\maintenance\build_rule_seed_evidence_checklist.py
python workers\maintenance\build_rule_seed_quantity_formulas.py
```

The current 16029 SolidWorks handoff and component-role summary are generated by:

```powershell
cd D:\Winnsen_Structure_Agent_Studio
python workers\maintenance\build_16029_engineering_handoff.py
python workers\maintenance\build_16029_10door_mutator_recipe.py
```

It writes:

- `data\solidworks_16029_engineering_handoff.json`
- `data\solidworks_16029_engineering_handoff.md`
- `data\solidworks_16029_role_rules.json`
- `data\solidworks_16029_role_rules.md`
- `data\solidworks_16029_10door_mutator_recipe.json`
- `data\solidworks_16029_10door_mutator_recipe.md`

Current highest-value rule-learning samples:

- `16038` 1917x1000x550: strongest same-size, different-door-count evidence set.
- `16029` 1917x1000x550: strongest standard locker baseline and historical order-variant evidence.
- `16028` 1917x1000x485 + `16029` 1917x1000x550: best cabinet-depth comparison pair.
- `25072` and `23054`: control-cabinet, operation-zone, electronics, stainless/order-variant rule sources.

Key current interpretation:

- 16029 standard locker: SolidWorks 12-door baseline clone and 10-door 2/10 practice-template clone are the current usable handoffs; the 10-door clone has passed a native-save smoke run. Other SolidWorks door counts are blocked until a transform/mate-backed mutator passes QA; the next technical step is Pack-and-Go for 10/12 plus converting the 10-door practice recipe into a real mutator rather than broadening door counts.
- Outdoor courier family: Stage 2 and formed STEP bbox gate built, but `P0=32` STEP export blockers remain.
- Outdoor `1/12 R` waterproof door: bbox reference model passed, topology still needs upgrade.
- No row is currently presented as production-ready automatic drawing output.

## Verification

Last checked with:

```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npm run build:snapshot
npm run build
npm run lint
```

API checked with:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:8000/health
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/generation-tasks
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:8000/api/generation-tasks/{task_id}/dry-run
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:8000/api/generation-tasks/{task_id}/execute
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/template-rule-extractions
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/rule-seed-candidates
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/rule-seed-review-ledger
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/rule-seed-evidence-checklist
Invoke-RestMethod -Uri http://127.0.0.1:8000/api/rule-seed-quantity-formulas
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:8000/api/template-rule-extractions/{run_id}/run
```

Python maintenance scripts checked with:

```powershell
python -m py_compile services\api\app\main.py workers\maintenance\summarize_solidworks_rule_extraction.py workers\maintenance\compare_rule_extractions.py workers\maintenance\build_16029_engineering_handoff.py workers\maintenance\build_16029_10door_mutator_recipe.py
```

Playwright visual QA artifacts are saved under:

`docs\design\qa`

The concept reference is saved at:

`docs\design\mvp-dashboard-concept.png`

## Recommended Stack

- Frontend: React / Next.js
- Backend: Python FastAPI
- Database: SQLite first, PostgreSQL later
- CAD workers: FreeCADCmd, SolidWorks automation, DXF/BOM parsers
- LLM providers: configurable API provider layer

## Key Principle

Engineering files are the geometry evidence. Large language models provide guidance, review, planning, and risk analysis, but they must not become the source of production geometry.

## Bootstrap Context

Read this handoff document first:

`C:\Users\Administrator\Desktop\Winnsen_Structure_Agent_Studio_项目启动说明.md`
