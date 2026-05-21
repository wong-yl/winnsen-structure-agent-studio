# 结构拆分任务 — Codex 分步执行指令

## 总目标

将 `D:\Winnsen_Structure_Agent_Studio` 项目中过大的单文件拆分为模块化结构。
**保持功能完全不变，不新增功能，不引入新依赖。**

## 核心约束（每一步都必须遵守）

- 不加新功能、不改 UI 样式、不改 API 接口签名
- 不改数据库 schema、不引入新的 npm/pip 依赖
- 不动 `data/`、`workers/`、`configs/` 目录
- 不动 `studioData.ts` 和 `generatedSnapshot.ts`
- CSS 文件不动，className 不改
- 每一步完成后必须运行验证命令，通过后再进入下一步

---

## 第 1 步：后端 — 提取 config.py

从 `services/api/app/main.py` 提取所有 `Path(...)` / `os.getenv(...)` 常量和顶层常量集合（如 `SUPPORTED_LOCKER_DOOR_COUNTS` 等）到 `services/api/app/config.py`。

main.py 中用 `from app.config import ...` 替换原来的常量定义。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m py_compile app\config.py
python -m py_compile app\main.py
python -c "from app.config import ROOT_DIR, DB_PATH, CAD_WORKSPACE; print('config OK')"
```

---

## 第 2 步：后端 — 提取 models.py

从 main.py 提取所有 `class Xxx(BaseModel)` 到 `services/api/app/models.py`。
包括：DryRunCheck, DryRunResult, SolidWorksRunSummary, WorkerExecutionResult, GenerationTaskCreate, GenerationTask, LocalOpenRequest, FreeCadOpenRequest, LocalActionResult, 以及所有 DrawingSheetMetal* 和 RuleExtraction* 模型。

也把 `CadRunner` 和 `TaskStatus` 类型别名放到 models.py。

main.py 中用 `from app.models import ...` 替换。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m py_compile app\models.py
python -m py_compile app\main.py
```

---

## 第 3 步：后端 — 提取 database.py 和 utils.py

- `database.py` — 提取 `connect()`, `init_db()`, `ensure_column()`, `now_iso()`
- `utils.py` — 提取纯工具函数：`parse_float`, `first_match`, `make_task_id`, `make_rule_extraction_id`, `make_drawing_sheetmetal_intake_id`, `safe_upload_filename`, `decode_upload_base64`, `path_exists`, `unique_output_path`, `drawing_sheetmetal_source_category`, `drawing_sheetmetal_next_action`

main.py 中替换对应 import。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m py_compile app\database.py
python -m py_compile app\utils.py
python -m py_compile app\main.py
```

---

## 第 4 步：后端 — 拆分 routes

在 `services/api/app/routes/` 下创建 `__init__.py`，然后按 URL 前缀拆分路由文件。

先读取 main.py 中所有 `@app.get` / `@app.post` / `@app.put` / `@app.delete`，按照以下分组：

| 文件 | 包含的端点路径前缀 |
|------|-------------------|
| `health.py` | `/health` |
| `generation.py` | `/api/generation-tasks` |
| `intake.py` | `/api/drawing-sheetmetal-intake` |
| `rules.py` | `/api/template-rule-extractions`, `/api/rule-seed-*` |
| `local_actions.py` | `/api/local/*` |
| `evidence.py` | 其余 `/api/*` 数据读取端点（16029 相关、sheetmetal evidence 等） |

每个文件用 `router = APIRouter()`，端点装饰器从 `@app.xxx` 改为 `@router.xxx`。

main.py 中 `app.include_router(xxx.router)` 注册。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\services\api
python -m py_compile app\routes\__init__.py
python -m py_compile app\routes\health.py
python -m py_compile app\routes\generation.py
python -m py_compile app\routes\intake.py
python -m py_compile app\routes\rules.py
python -m py_compile app\routes\local_actions.py
python -m py_compile app\routes\evidence.py
python -m py_compile app\main.py
# main.py 此时应该 < 100 行
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
# 启动后手动测试 GET http://127.0.0.1:8000/health 返回正常
```

---

## 第 5 步：前端 — 提取 types.ts

从 `apps/web/src/App.tsx` 提取所有 `type` 定义到 `apps/web/src/types.ts`，统一 export。
包括：PageId, CadRunner, LocalActionMode, ParameterValues, StatusTone, GenerationFeedback, DryRunCheck, DryRunResult, SolidWorksRunSummary, WorkerExecutionResult, 以及所有 Locker16029*, DrawingSheetMetal*, SheetMetalRule*, TemplateCatalog, RuleExtraction*, RuleSeed*, QuantityFormula*, GenerationTask 等。

App.tsx 中用 `import type { ... } from './types'` 替换。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npx tsc --noEmit
```

---

## 第 6 步：前端 — 提取 constants.ts

从 App.tsx 提取所有顶层 `const`（非 React 组件）到 `apps/web/src/constants.ts`。
包括：API_BASE_URL, BRAND_MARK_SRC, FREECAD_CMD, FREECAD_SHORTCUT, SOLIDWORKS_SHORTCUT, DEFAULT_MODEL_CAPABILITY_ID, 所有 LOCKER_16029_* 常量, DRAWING_UPLOAD_*, cadRunners, pages, maturityLabels, evidenceTone 等。

App.tsx 中用 `import { ... } from './constants'` 替换。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npx tsc --noEmit
```

---

## 第 7 步：前端 — 提取 components

从 App.tsx 中提取所有**非 Page 级别**的 function 组件到 `apps/web/src/components/` 目录。
至少包括：StatusPill, StatusDot, ProgressDial, ProjectCard, EvidenceRow。
如有其他小组件（如 SectionBlock 等）一并提取。

每个组件一个文件，从 types.ts 和 constants.ts import 需要的类型/常量。

App.tsx 中用 `import { StatusPill } from './components/StatusPill'` 等替换。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npx tsc --noEmit
```

---

## 第 8 步：前端 — 提取 pages

从 App.tsx 中提取所有 `function XxxPage(...)` 到 `apps/web/src/pages/` 目录：

| 文件 | 组件 |
|------|------|
| `OverviewPage.tsx` | `OverviewPage` |
| `IntakePage.tsx` | `IntakePage` |
| `RulesPage.tsx` | `RulesPage` |
| `ModelsPage.tsx` | `ModelsPage` |
| `DrawingSheetMetalPage.tsx` | `DrawingSheetMetalPage` |
| `AgentConsolePage.tsx` | `AgentConsolePage` |
| `ReviewPage.tsx` | `ReviewPage` |

每个页面文件 import 自己需要的 types、constants、components、data。
如果某个页面内部有只被该页面使用的子组件，子组件跟着页面走（放在同一文件或同一目录下）。

最终 App.tsx 只保留：imports + `function App()` (Shell：sidebar + topbar + page switch)，**目标 < 200 行**。

**验证**：
```powershell
cd D:\Winnsen_Structure_Agent_Studio\apps\web
npm run build
npm run lint
```

---

## 最终目录结构参考

### 后端 (第 1-4 步完成后)

```
services/api/app/
├── __init__.py
├── main.py          # < 100 行: app 创建 + 中间件 + include_router + startup
├── config.py        # 所有 Path/env 常量
├── database.py      # SQLite 连接和初始化
├── models.py        # 所有 Pydantic 模型
├── utils.py         # 纯工具函数
└── routes/
    ├── __init__.py
    ├── health.py
    ├── generation.py
    ├── intake.py
    ├── rules.py
    ├── local_actions.py
    └── evidence.py
```

### 前端 (第 5-8 步完成后)

```
apps/web/src/
├── App.tsx          # < 200 行: Shell + 页面切换
├── App.css
├── main.tsx
├── types.ts
├── constants.ts
├── components/
│   ├── StatusPill.tsx
│   ├── StatusDot.tsx
│   ├── ProgressDial.tsx
│   ├── ProjectCard.tsx
│   ├── EvidenceRow.tsx
│   └── ...
├── pages/
│   ├── OverviewPage.tsx
│   ├── IntakePage.tsx
│   ├── RulesPage.tsx
│   ├── ModelsPage.tsx
│   ├── DrawingSheetMetalPage.tsx
│   ├── AgentConsolePage.tsx
│   └── ReviewPage.tsx
└── data/
    ├── studioData.ts
    └── generatedSnapshot.ts
```

---

## 不做的事（再次强调）

- 不加新功能
- 不改 UI 样式
- 不改 API 接口签名
- 不改数据库 schema
- 不引入新的 npm/pip 依赖
- 不动 `data/`、`workers/`、`configs/`、`docs/` 目录
- 不动 `studioData.ts` 和 `generatedSnapshot.ts`
- 不提取 hooks（本次不做，留后续优化）
- 不加 react-router（本次不做，留后续优化）
