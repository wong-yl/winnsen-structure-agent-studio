# Winnsen Structure Agent Studio API

Local FastAPI service for the MVP generation queue.

## Run

```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

The default SQLite database is:

`D:\Winnsen_Structure_Agent_Studio\data\studio.sqlite`

The service records generation tasks, dry-run readiness checks, and controlled
worker execution metadata. SolidWorks is the current engineering-mainline CAD
route for native output packages; FreeCAD is kept as the open-source migration
route for repeatable `FCStd` / STEP experiments. It does not generate production
drawings.

New raw template packs under `C:\Users\Administrator\Desktop\参数化模板素材` are
indexed as rule-learning evidence first. They should be promoted into generation
only after SolidWorks assembly evidence, BOM, DXF/STEP/PDF support, and the
relevant same-size variation rule have been extracted.

## CAD Entries

- SOLIDWORKS 2025 shortcut: `C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk`
- SOLIDWORKS target: `C:\WINDOWS\Installer\{DB2C3F1B-3025-4743-AAA8-1B5E20047E34}\i386_SldWorks.exe`
- FreeCAD 1.1.1 shortcut: `C:\Users\Administrator\Desktop\FreeCAD 1.1.1.lnk`
- FreeCAD target: `D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCAD.exe`
- FreeCAD command target: `D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCADCmd.exe`

The API treats these as two CAD routes, not a required SolidWorks-to-FreeCAD
check chain. SolidWorks validation/report files are native SolidWorks-generation
feedback; FreeCAD validation remains useful for the future open-source migration
path.

## Endpoints

- `GET /health`
- `GET /api/template-assets`
- `POST /api/template-assets/rescan`
- `GET /api/template-rule-extractions`
- `POST /api/template-rule-extractions`
- `POST /api/template-rule-extractions/{run_id}/run`
- `GET /api/rule-seed-candidates`
- `GET /api/rule-seed-review-ledger`
- `GET /api/rule-seed-evidence-checklist`
- `GET /api/rule-seed-quantity-formulas`
- `GET /api/generation-tasks`
- `GET /api/generation-tasks/{task_id}`
- `POST /api/generation-tasks`
- `POST /api/generation-tasks/{task_id}/dry-run`
- `POST /api/generation-tasks/{task_id}/execute`
- `POST /api/generation-tasks/{task_id}/run-solidworks-package`

The dry-run endpoint checks local readiness only:

- selected CAD shortcut and executable
- `FreeCADCmd.exe` resolution via bundled FreeCAD path, `PATH`, or `STUDIO_FREECAD_CMD`
- SOLIDWORKS COM host availability through `cscript.exe`
- generator script existence under `STUDIO_CAD_WORKSPACE`
- task parameter completeness and model-specific validation, including even `door_count` for the current 16029 two-column locker generator
- attached evidence
- engineering-reference output boundary

## Execution Boundary

- Rule extraction runs are SolidWorks-native, read-only evidence collection jobs for source `.SLDASM` assemblies under `C:\Users\Administrator\Desktop\参数化模板素材`.
- A rule extraction package is written to `workers\rule_extractions\<RULE_ID>` and calls `D:\机械结构工程师智能体\scripts\sw_extract_structure_queue_item.js`.
- Successful rule extraction writes component, mate, feature, dimension, JSON, stdout/stderr, and `rule_learning_summary` files. The API returns `learning_summary` so the UI can show whether the run is actually ready for rule learning.
- `needs_component_tree_or_bbox_repair` means the run is useful as component-name, mate, and local-pattern evidence only. Pattern dimensions such as shelf pitch or door-array spacing still need automated DXF/BOM/quantity evidence closure, or repaired transform/bbox coverage, before a generator can use them.
- SolidWorks tasks write a PowerShell execution package to `workers\manual_runs\<task_id>`. The package can be inspected manually, or run directly through `POST /api/generation-tasks/{task_id}/run-solidworks-package`.
- Direct SolidWorks package execution calls `powershell.exe -ExecutionPolicy Bypass -File run-solidworks-worker.ps1`, then refreshes `.SLDASM`, build report, validation CSV/report, and component manifest outputs in the task result.
- SolidWorks execution also runs `sw_diagnose_assembly_quality.js`. The API records `solidworks_quality_status` and `solidworks_quality_summary`; `reference_feature_only` means the file opens visually but exposes `Reference` features instead of a traversable component tree.
- FreeCAD tasks run through `FreeCADCmd.exe` using a generated Python wrapper so script arguments do not collide with FreeCAD command-line options.
- FreeCAD outputs are written to `workers\generated_models\<task_id>`.
- Worker logs are written to `workers\generation_logs` by default.

## Rule Extraction Maintenance

Summarize one extraction folder:

```powershell
cd D:\Winnsen_Structure_Agent_Studio
python workers\maintenance\summarize_solidworks_rule_extraction.py workers\rule_extractions\<RULE_ID>
```

Compare all extraction folders:

```powershell
cd D:\Winnsen_Structure_Agent_Studio
python workers\maintenance\compare_rule_extractions.py
python workers\maintenance\build_rule_seed_candidates.py
python workers\maintenance\build_rule_seed_review_ledger.py
python workers\maintenance\build_rule_seed_evidence_checklist.py
python workers\maintenance\build_rule_seed_quantity_formulas.py
```

Current measured baseline:

| Template | Components | Transform | BBox | Mates | Pattern seeds | Quality gate |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 16038 洗衣寄存柜同尺寸多门数 | 38 | 0/38 | 0/38 | 82 | 1 | `needs_component_tree_or_bbox_repair` |
| 16029 标准寄存柜 1917x1000x550 | 38 | 0/38 | 0/38 | 75 | 3 | `needs_component_tree_or_bbox_repair` |
| 16028 标准寄存柜 1917x1000x485 | 36 | 0/36 | 0/36 | 77 | 2 | `needs_component_tree_or_bbox_repair` |

Current pattern seeds:

- 16029: `层板阵列=11 x 152.5mm`, `柜门阵列=6 x 305mm`, `衣架钢管整列=2 x 915mm`.
- 16028: `层板阵列=11 x 152.5mm`, `柜门阵列=6 x 305mm`.
- 16038: `局部线性阵列2=2 x 915mm`.
