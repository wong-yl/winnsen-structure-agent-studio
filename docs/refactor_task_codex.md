# 结构拆分任务 - 安全分阶段执行指令

## 当前策略

本文件替代一次性 8 步全量重构方案。当前项目已有可验证的 16029 `10/12/14` 门 SolidWorks 原生骨架入口，拆分必须以不破坏现有生成链路为第一约束。

先只执行后端第 1 步：提取配置常量。完成并验证后停下，不继续拆前端、不拆 routes。

## 已建立的回退点

- 当前分支：`codex/safe-refactor-config-checkpoint`
- checkpoint 提交：`checkpoint: stabilize 16029 skeleton workflow`

如后续拆分出问题，先回到该提交再排查。

## 全局约束

- 不加新功能。
- 不改 UI 样式。
- 不改 API 接口签名。
- 不改数据库 schema。
- 不引入新的 npm/pip 依赖。
- 不动 CAD 生成输出目录和模型源文件。
- 不改 16029 `10/12/14` SolidWorks 原生骨架入口行为。
- 不改 FreeCAD 规则验证入口行为。
- 每完成一步都必须验证，通过后再考虑下一步。

## 本轮只执行：第 1 步，后端提取 config.py

### 目标

从 `services/api/app/main.py` 提取后端配置常量到：

`services/api/app/config.py`

提取范围：

- 所有顶层 `Path(...)` / `os.getenv(...)` 配置。
- 所有后端常量集合，例如 `SUPPORTED_LOCKER_DOOR_COUNTS`、`SUPPORTED_16029_*`、`SUPPORTED_DRAWING_SHEETMETAL_SUFFIXES`。
- 与本地软件路径、worker 输出目录、规则数据文件路径相关的常量。

暂不提取：

- Pydantic model。
- SQLite 函数。
- worker 逻辑。
- API routes。
- 前端类型、常量、组件和页面。

### 实现要求

- `main.py` 只通过 import 使用这些配置常量。
- 保持现有变量名不变，避免业务逻辑改动。
- 使用相对导入，例如 `from .config import ...`，保证从仓库根目录启动 `services.api.app.main:app` 时可用。
- 不修改 `data/`、`workers/`、`configs/` 目录内容。
- 不修改 `apps/web/src/App.tsx` 或 `studioData.ts`。

### 验证命令

在仓库根目录执行：

```powershell
python -m py_compile services\api\app\config.py
python -m py_compile services\api\app\main.py
python -c "from services.api.app.config import ROOT_DIR, DB_PATH, CAD_WORKSPACE; print('config OK', ROOT_DIR, DB_PATH, CAD_WORKSPACE)"
npm run build --prefix apps\web
```

如本地 API 正在运行，验证通过后重启 API 并确认：

```powershell
Invoke-WebRequest -UseBasicParsing -Uri http://127.0.0.1:8000/health
```

### 验收标准

- Python 编译通过。
- Web 构建通过。
- `/health` 返回正常。
- `可生成模型 -> 16029` 页面仍能显示 `生成 10/12/14 原生骨架`。
- 不启动 SolidWorks，不跑 CAD 生成任务。

## 后续步骤，暂不执行

只有第 1 步稳定后，才考虑继续：

1. 提取 `models.py`。
2. 提取 `database.py` 和纯工具函数。
3. 小批量拆 routes。
4. 再考虑前端类型和常量拆分。

前端 `ModelsPage` 和 SolidWorks/FreeCAD 任务队列暂时不要拆，避免影响当前最关键的模型生成入口。
