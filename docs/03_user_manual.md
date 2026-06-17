# User Manual — Winnsen Structure Agent Studio

This is the operator manual for installing, running, and using the Studio MVP.
It covers Windows (the engineering mainline) **and** macOS / Linux (development
and dashboard use). For the architectural overview and project scope, read the
root [`README.md`](../README.md) and [`docs/00_project_brief.md`](00_project_brief.md).

> The MVP is a local dashboard plus a controlled generation-queue API. It is not
> a CAD editor and never marks output as production-released. Native CAD
> generation (SolidWorks / FreeCAD) only works on a Windows host that has those
> programs installed; on macOS / Linux you can run the dashboard, the API, the
> data/snapshot tooling, and inspect everything except the Windows-only CAD
> execution steps.

---

## 1. System layout

| Component | Path | Stack |
|---|---|---|
| Web dashboard | [`apps/web`](../apps/web) | React 19 + Vite + TypeScript |
| Generation-queue API | [`services/api`](../services/api) | FastAPI + SQLite |
| Maintenance / CAD workers | [`workers/maintenance`](../workers/maintenance) | Python scripts |
| Local status snapshots & rule data | [`data/`](../data) | JSON / Markdown |
| Example configs | [`configs/`](../configs) | JSON |

The dashboard reads a generated snapshot committed in the repo, so it renders
without the API. Live features (generation tasks, rule extraction, local file
actions) require the API running.

---

## 2. Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| Python | **3.10+** (3.12 recommended) | API + worker scripts |
| Node.js | 18+ (20 LTS recommended) | Dashboard build/dev |
| Git | any recent | Cloning / updates |
| SolidWorks 2025 | — | Native SolidWorks generation/extraction (Windows only) |
| FreeCAD 1.1.x | — | FreeCAD generation + STEP bbox steps (Windows path baked in) |

> **Python version matters.** The API uses `X | None` type syntax that Pydantic
> evaluates at runtime, which requires Python **3.10 or newer**. macOS ships
> Python 3.9 by default — use a 3.10+ interpreter (Homebrew, python.org, pyenv,
> or conda). Running under 3.9 fails at import with
> `TypeError: unsupported operand type(s) for |: 'type' and 'NoneType'`.

---

## 3. Install & run the API

### 3.1 Windows (PowerShell)

```powershell
cd <repo>\services\api
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

### 3.2 macOS / Linux (bash/zsh)

```bash
cd <repo>/services/api
python3 -m venv .venv            # use a 3.10+ python; e.g. python3.12 -m venv .venv
.venv/bin/python -m pip install --upgrade pip
.venv/bin/python -m pip install -r requirements.txt
.venv/bin/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

### 3.3 Verify

```bash
curl http://127.0.0.1:8000/health
# -> {"status":"ok","database":".../data/studio.sqlite"}
curl http://127.0.0.1:8000/api/generation-tasks
# -> []   (empty on a fresh database)
```

The SQLite database is created automatically on first request at
`<repo>/data/studio.sqlite` (override with `STUDIO_DB_PATH`). It is gitignored.

---

## 4. Install & run the dashboard

```bash
cd <repo>/apps/web
npm install
npm run dev -- --host 127.0.0.1 --port 5173
```

Open <http://127.0.0.1:5173/>. By default the dashboard calls the API at
`http://127.0.0.1:8000`. To point it elsewhere, set `VITE_API_BASE_URL` before
`npm run dev`:

```bash
# macOS / Linux
VITE_API_BASE_URL=http://127.0.0.1:8000 npm run dev -- --host 127.0.0.1 --port 5173
```

```powershell
# Windows
$env:VITE_API_BASE_URL='http://127.0.0.1:8000'
npm run dev -- --host 127.0.0.1 --port 5173
```

Other scripts: `npm run build` (typecheck + production build),
`npm run lint`, `npm run build:snapshot` (see §7), `npm run preview`.

---

## 5. Using the dashboard

The dashboard has five pages.

1. **项目总览 / Overview** — project slots, metric cards, pipeline status, and
   maturity distribution from the committed snapshot.
2. **数据录入状态 / Intake** — DXF/BOM/STEP parsing progress per project family.
3. **规则库成熟度 / Rules** — rule families, rule-learning axes, and the
   SolidWorks template **rule-extraction** entry (needs the API + Windows
   SolidWorks to actually run).
4. **可生成模型 / Models** — the generatable-capability catalog and the
   **generation task** workflow (the live, API-backed part — see §6).
5. **待确认项 / Review** — the manual-review queue.

Capability gating: `generatable` / `reference_only` can create task drafts;
`blocked` / `queued` stay disabled until evidence gates close.

---

## 6. The generation task workflow

This is the core live workflow on the **Models** page.

1. **Pick a capability** and a CAD runner (FreeCAD or SolidWorks). Edit the
   exposed parameters (e.g. `door_count` for the 16029 locker — must be an even
   value; verified for 8/12/14/16/18 doors).
2. **Create draft** → `POST /api/generation-tasks`. Stored in SQLite with
   runner, parameters, evidence, maturity, output level, and worker command.
3. **Dry-run** → `POST /api/generation-tasks/{id}/dry-run`. A preflight that
   checks the CAD shortcut/executable, generator script path, parameter
   completeness, attached evidence, and the engineering-reference output
   boundary. A task must pass dry-run before it can execute.
4. **Execute** → `POST /api/generation-tasks/{id}/execute`:
   - **FreeCAD** runs `FreeCADCmd.exe` via a generated Python wrapper and writes
     outputs to `workers/generated_models/<task_id>`.
   - **SolidWorks** prepares a manual PowerShell package under
     `workers/manual_runs/<task_id>`.
5. **Run SolidWorks package** (SolidWorks only) →
   `POST /api/generation-tasks/{id}/run-solidworks-package` runs the `.ps1`
   directly, refreshes the `.SLDASM` / reports / manifest, and records a quality
   diagnostic (`reference_feature_only` = opens visually but lacks a traversable
   component tree).

> **Cross-platform note:** steps 4–5 invoke `FreeCADCmd.exe`, `cscript.exe`, and
> `powershell.exe`. They only succeed on a Windows host with the CAD software
> installed. On macOS / Linux you can create drafts and inspect tasks; execution
> will report the missing tool rather than producing CAD output.

Local file actions (`POST /api/local-actions/open-path`, `.../open-freecad-model`)
open output folders/files using the host's native opener
(`explorer.exe` on Windows, `open` on macOS, `xdg-open` on Linux), restricted to
the output-directory allowlist.

---

## 7. Data & snapshot tooling

The dashboard's CAD-status numbers come from a generated snapshot,
[`apps/web/src/data/generatedSnapshot.ts`](../apps/web/src/data/generatedSnapshot.ts).
Regenerate it from the source status files with:

```bash
cd <repo>/apps/web
npm run build:snapshot
```

The builder reads Markdown/CSV status files from the external CAD workspace
(`D:\机械结构工程师智能体\outputs\...`). If a source file is missing (e.g. on a
machine without that workspace), the builder now degrades gracefully — that
field reads empty/zero instead of crashing the build.

Rule-learning data files under [`data/`](../data) are produced by the
maintenance scripts (run from the repo root):

```bash
python workers/maintenance/index_parametric_template_assets.py      # template catalog
python workers/maintenance/compare_rule_extractions.py              # cross-template comparison
python workers/maintenance/build_rule_seed_candidates.py
python workers/maintenance/build_rule_seed_review_ledger.py
python workers/maintenance/build_rule_seed_evidence_checklist.py
python workers/maintenance/build_rule_seed_quantity_formulas.py
```

The API serves these as `GET /api/rule-seed-candidates`,
`/api/rule-seed-review-ledger`, `/api/rule-seed-evidence-checklist`, and
`/api/rule-seed-quantity-formulas`.

---

## 8. API reference

| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | Liveness + DB path |
| GET | `/api/template-assets` | Parametric template catalog |
| POST | `/api/template-assets/rescan` | Re-index template assets (Windows workspace) |
| GET | `/api/template-rule-extractions` | List rule-extraction runs |
| POST | `/api/template-rule-extractions` | Prepare a rule-extraction package |
| POST | `/api/template-rule-extractions/{run_id}/run` | Run the extraction (Windows + SolidWorks) |
| GET | `/api/rule-seed-candidates` | Rule seed candidates |
| GET | `/api/rule-seed-review-ledger` | Rule seed review ledger |
| GET | `/api/rule-seed-evidence-checklist` | Rule seed evidence checklist |
| GET | `/api/rule-seed-quantity-formulas` | Rule seed quantity formulas |
| GET | `/api/generation-tasks` | List tasks (`?limit=1..100`) |
| GET | `/api/generation-tasks/{task_id}` | Task detail |
| POST | `/api/generation-tasks` | Create draft |
| POST | `/api/generation-tasks/{task_id}/dry-run` | Preflight check |
| POST | `/api/generation-tasks/{task_id}/execute` | Run worker (FreeCAD/SolidWorks pkg) |
| POST | `/api/generation-tasks/{task_id}/run-solidworks-package` | Run SolidWorks `.ps1` directly |
| POST | `/api/local-actions/open-path` | Open a folder/file via the native opener |
| POST | `/api/local-actions/open-freecad-model` | Launch FreeCAD on a task's model |

Interactive docs are available at <http://127.0.0.1:8000/docs> while the API runs.

---

## 9. Configuration (environment variables)

All paths default to Windows-style values appropriate for the engineering host;
override them with environment variables when running elsewhere.

| Variable | Default | Purpose |
|---|---|---|
| `STUDIO_DB_PATH` | `data/studio.sqlite` | SQLite database location |
| `STUDIO_CORS_ORIGINS` | `http://127.0.0.1:5173,http://localhost:5173` | Allowed dashboard origins |
| `STUDIO_CAD_WORKSPACE` | `D:\机械结构工程师智能体` | External CAD scripts/assets root |
| `STUDIO_WORKER_LOG_DIR` | `workers/generation_logs` | Dry-run / execution logs |
| `STUDIO_GENERATED_MODEL_DIR` | `workers/generated_models` | FreeCAD outputs |
| `STUDIO_MANUAL_RUN_DIR` | `workers/manual_runs` | SolidWorks packages |
| `STUDIO_RULE_EXTRACTION_DIR` | `workers/rule_extractions` | Rule-extraction outputs |
| `STUDIO_PARAMETRIC_TEMPLATE_ROOT` | `C:\Users\Administrator\Desktop\参数化模板素材` | Raw template materials |
| `STUDIO_FREECAD_EXE` / `STUDIO_FREECAD_CMD` | Windows FreeCAD paths | FreeCAD GUI / `FreeCADCmd` resolution (falls back to `PATH`) |
| `STUDIO_SOLIDWORKS_EXE` | Windows SolidWorks path | SolidWorks executable check |
| `STUDIO_*_TIMEOUT_SECONDS` | 60–900 | Worker / extraction timeouts |
| `VITE_API_BASE_URL` (frontend) | `http://127.0.0.1:8000` | Dashboard → API base URL |

LLM provider config is templated in
[`configs/providers.example.json`](../configs/providers.example.json) (all
providers disabled by default).

---

## 10. Verification checklist

```bash
# API (any OS, Python 3.10+)
.venv/bin/python -m py_compile services/api/app/main.py
curl http://127.0.0.1:8000/health
curl http://127.0.0.1:8000/api/generation-tasks

# Dashboard (needs Node)
cd apps/web && npm install && npm run build && npm run lint && npm run build:snapshot
```

Playwright visual-QA artifacts live under
[`docs/design/qa`](design/qa); the concept reference is
[`docs/design/mvp-dashboard-concept.png`](design/mvp-dashboard-concept.png).

---

## 11. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `TypeError: unsupported operand type(s) for \|` on API start | Python < 3.10. Recreate the venv with a 3.10+ interpreter. |
| `ModuleNotFoundError: fastapi` | Dependencies not installed in the active venv. Run `pip install -r requirements.txt` inside `.venv`. |
| Dashboard shows data but live actions fail | API not running or `VITE_API_BASE_URL` wrong / CORS origin not in `STUDIO_CORS_ORIGINS`. |
| Dry-run fails on `freecad_command` / `solidworks_*` | CAD software not installed or paths not set. Expected on macOS / Linux; set `STUDIO_FREECAD_CMD` / `STUDIO_*_EXE` on Windows. |
| `npm run build:snapshot` produces empty fields | Source status files from `STUDIO_CAD_WORKSPACE` aren't present on this machine (non-fatal by design). |
| Execution returns "cscript.exe not found" / FreeCAD launch fails | Windows-only CAD steps invoked off-Windows, or the tool isn't on `PATH`. |

---

## 12. Key principle

Engineering files are the geometry evidence. LLMs provide guidance, review,
planning, and risk analysis — they must **not** become the source of production
geometry. The API never marks any output as production-released.
