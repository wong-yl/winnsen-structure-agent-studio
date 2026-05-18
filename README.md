# Winnsen Structure Agent Studio

Winnsen 硬件结构知识与智能钣金模型生成平台。

## Current MVP

已在 `apps/web` 搭建本地 React/Vite 控制台 MVP，第一版包含五个页面：

- `项目总览`
- `数据录入状态`
- `规则库成熟度`
- `可生成模型`
- `待确认项`

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

- Project overview
- Intake pipeline status
- Rule maturity board
- Generatable model catalog with a persistent local generation task draft queue
- Manual review queue

The Structure Review Agent console is intentionally left for a later iteration after the status dashboard and data adapter are stable.

## Generation Queue Boundary

The current model-generation entry creates SQLite-backed worker tasks.
It is a controlled engineering-reference queue, not a production CAD release system.

Current behavior:

- `generatable` and `reference_only` capabilities can create task drafts.
- `blocked` and `queued` capabilities stay disabled until evidence gates close.
- New template assets first enter the rule-learning queue. Copying a top-level `.SLDASM` is not treated as parametric generation.
- The model-generation panel has two CAD entry buttons:
  - SOLIDWORKS 2025: `C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk`
  - FreeCAD 1.1.1: `C:\Users\Administrator\Desktop\FreeCAD 1.1.1.lnk`
- Task drafts store CAD runner, capability, editable parameters, evidence, maturity, output level, and the expected worker command.
- The 16029 standard locker generation entry accepts editable `door_count`; current two-column generation requires an even value and has verified 8/12/14/16/18-door cases.
- Task details can run a dry-run preflight that checks the selected CAD shortcut, executable, generator script path, parameters, evidence, and output boundary.
- SolidWorks is the current engineering-mainline runner; its manual package produces native assembly output when the local SolidWorks session and license are available.
- SolidWorks tasks can run the generated package directly through the API via PowerShell; the `.ps1` file is kept for inspection and fallback, not as the primary user action.
- SolidWorks execution now writes a quality diagnostic report (`*_solidworks_quality_report.md` / `.json`) that separates a normal component-tree assembly from a `Reference`-feature-only engineering reference. `Reference`-feature-only results can be opened for visual/placement review but are not presented as production-ready editable assemblies.
- FreeCAD tasks can execute through the local `FreeCADCmd.exe` worker after dry-run passes as the open-source migration route.
- FreeCAD engineering-reference outputs are written under `workers\generated_models\<task_id>`.
- SolidWorks tasks prepare a manual run package under `workers\manual_runs\<task_id>`. The direct runner cleans reference-plane/sketch/origin display before saving generated `.SLDASM` outputs, writes SolidWorks validation CSV/report files, and records quality diagnostics for component-tree vs reference-feature-only status.
- Dry-run and execution metadata write logs under `workers\generation_logs`.
- The API does not create production drawings or mark any output as production-released.

This keeps the MVP honest: users get real generation entries, persistent execution records, and local engineering-reference files while production CAD release remains gated by engineering validation.

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

Current highest-value rule-learning samples:

- `16038` 1917x1000x550: strongest same-size, different-door-count evidence set.
- `16029` 1917x1000x550: strongest standard locker baseline and historical order-variant evidence.
- `16028` 1917x1000x485 + `16029` 1917x1000x550: best cabinet-depth comparison pair.
- `25072` and `23054`: control-cabinet, operation-zone, electronics, stainless/order-variant rule sources.

Key current interpretation:

- 16029 standard locker: SolidWorks native assembly route is the current engineering mainline; FreeCAD regression `PASS` supports the open-source migration route. Strict queue `P0=0`, remaining `P2=37` W784 width-derived rows.
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
python -m py_compile services\api\app\main.py workers\maintenance\summarize_solidworks_rule_extraction.py workers\maintenance\compare_rule_extractions.py
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
