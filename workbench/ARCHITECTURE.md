# 16029订单工作台 - 技术架构方案

## 技术栈

### 后端
- **FastAPI** (Python 3.10+)
  - 高性能异步框架
  - 自动生成API文档
  - 支持WebSocket（实时日志推送）

- **SQLite** / **PostgreSQL**
  - 轻量级：SQLite（开发/小规模）
  - 生产级：PostgreSQL（多用户/高并发）

- **Pydantic**
  - 数据验证
  - JSON Schema生成

### 前端
- **Vue 3** + **Vite**
  - 组合式API
  - 快速热重载
  - TypeScript支持

- **TailwindCSS**
  - 实用优先的CSS框架
  - 响应式设计
  - 深色模式支持

- **Pinia**
  - 状态管理

- **Vue Router**
  - 页面路由

### 文件服务
- **PDF.js** - PDF预览
- **Three.js** / **Babylon.js** - 3D模型预览（未来）
- **MinIO** / **本地文件系统** - 文件存储

---

## 目录结构

```
winnsen_workbench/
├── backend/
│   ├── app/
│   │   ├── main.py              # FastAPI入口
│   │   ├── config.py            # 配置
│   │   ├── database.py          # 数据库连接
│   │   ├── models/              # SQLAlchemy模型
│   │   │   ├── part.py          # 零件表
│   │   │   ├── batch.py         # 批次表
│   │   │   ├── evidence.py      # 质量证据表
│   │   │   └── order.py         # 订单表
│   │   ├── schemas/             # Pydantic schemas
│   │   ├── routers/             # API路由
│   │   │   ├── parts.py         # 零件CRUD
│   │   │   ├── batches.py       # 批次管理
│   │   │   ├── files.py         # 文件服务
│   │   │   └── quality.py       # 质量检查
│   │   └── services/            # 业务逻辑
│   │       ├── evidence_parser.py  # 解析evidence.json
│   │       ├── pdf_generator.py    # PDF生成
│   │       └── sw_automation.py    # SolidWorks自动化
│   ├── migrations/              # 数据库迁移
│   ├── tests/
│   └── requirements.txt
├── frontend/
│   ├── src/
│   │   ├── main.ts
│   │   ├── App.vue
│   │   ├── router/
│   │   ├── stores/              # Pinia stores
│   │   ├── views/               # 页面组件
│   │   │   ├── Dashboard.vue    # 仪表盘
│   │   │   ├── PartList.vue     # 零件列表
│   │   │   ├── PartDetail.vue   # 零件详情
│   │   │   ├── BatchView.vue    # 批次管理
│   │   │   ├── QualityCheck.vue # 质量检查
│   │   │   └── FileViewer.vue   # 文件预览
│   │   ├── components/          # 复用组件
│   │   │   ├── PDFViewer.vue
│   │   │   ├── EvidenceCard.vue
│   │   │   ├── PartCard.vue
│   │   │   └── BatchTimeline.vue
│   │   └── assets/
│   ├── public/
│   ├── package.json
│   └── vite.config.ts
├── scripts/                     # 自动化脚本
│   ├── generate_flatpattern_bom.py  # 展开图和BOM生成
│   ├── import_evidence.py           # 导入evidence数据
│   └── sync_files.py                # 文件同步
└── docker-compose.yml           # Docker部署配置
```

---

## 核心功能模块

### 1. 仪表盘 (Dashboard)
- 订单概览
- 完成度统计（33个零件的状态）
- 批次进度可视化
- 待办事项（展开图、BOM、质量审核）

### 2. 零件管理 (Parts)
- 零件列表（可按部件类别、批次筛选）
- 零件详情页
  - 基本信息（图号、名称、材料、重量）
  - 文件预览（SLDDRW路径、PDF预览）
  - Evidence数据（视图数、尺寸数、待办事项）
  - 批次追溯

### 3. 批次管理 (Batches)
- R47/R51/R55/R59/R5门板批次
- 时间线视图
- 批次对比分析
- 源文件路径追溯

### 4. 质量检查 (Quality)
- 缺失展开图检查
- 重复注释检查
- Evidence TODO汇总
- 签审状态跟踪

### 5. 文件服务 (Files)
- PDF在线预览
- DXF下载
- SLDDRW路径快速访问
- 批量打包下载

### 6. 自动化任务 (Automation)
- 展开图批量生成（调用SolidWorks API）
- BOM自动更新
- Evidence数据同步
- 实时日志推送（WebSocket）

---

## 数据库设计

### 表结构

#### orders (订单表)
```sql
CREATE TABLE orders (
    id INTEGER PRIMARY KEY,
    order_number VARCHAR(20) UNIQUE,  -- 16029
    name VARCHAR(200),
    specs JSONB,  -- {width, height, depth, doors}
    created_date DATE,
    status VARCHAR(50)
);
```

#### parts (零件表)
```sql
CREATE TABLE parts (
    id INTEGER PRIMARY KEY,
    part_number VARCHAR(50) UNIQUE,   -- 16029-10-01
    name VARCHAR(200),
    category VARCHAR(50),             -- 门板/门框/箱体/上盖/底座
    material VARCHAR(100),
    material_spec VARCHAR(50),        -- T1.2mm
    weight_g FLOAT,
    batch_code VARCHAR(20),           -- R47/R51/...
    slddrw_path TEXT,
    pdf_path TEXT,
    dxf_path TEXT,
    generated_date DATE,
    status VARCHAR(50),
    order_id INTEGER REFERENCES orders(id)
);
```

#### batches (批次表)
```sql
CREATE TABLE batches (
    id INTEGER PRIMARY KEY,
    code VARCHAR(20) UNIQUE,          -- R47
    name VARCHAR(100),
    source_path TEXT,
    created_date TIMESTAMP,
    part_count INTEGER,
    order_id INTEGER REFERENCES orders(id)
);
```

#### evidence_data (质量证据表)
```sql
CREATE TABLE evidence_data (
    id INTEGER PRIMARY KEY,
    part_id INTEGER REFERENCES parts(id),
    views_count INTEGER,
    dimensions_count INTEGER,
    annotations_count INTEGER,
    has_flat_pattern BOOLEAN,
    todo_items JSONB,                 -- [{type, status, message}]
    duplicate_keys JSONB,
    raw_json TEXT,                    -- 完整evidence.json
    created_date TIMESTAMP
);
```

---

## API端点设计

### 零件相关
- `GET  /api/parts`              - 获取零件列表（支持筛选）
- `GET  /api/parts/{id}`         - 获取零件详情
- `POST /api/parts`              - 创建零件
- `PUT  /api/parts/{id}`         - 更新零件
- `GET  /api/parts/{id}/evidence` - 获取质量证据

### 批次相关
- `GET  /api/batches`            - 获取批次列表
- `GET  /api/batches/{code}`     - 获取批次详情
- `GET  /api/batches/{code}/parts` - 获取批次下的零件

### 文件相关
- `GET  /api/files/pdf/{part_id}` - 预览PDF
- `GET  /api/files/dxf/{part_id}` - 下载DXF
- `POST /api/files/batch-download` - 批量下载

### 质量检查
- `GET  /api/quality/missing-flat-patterns` - 缺失展开图列表
- `GET  /api/quality/duplicate-annotations` - 重复注释检查
- `GET  /api/quality/todos`                 - 待办事项汇总

### 自动化任务
- `POST /api/automation/generate-flat-patterns` - 批量生成展开图
- `POST /api/automation/sync-evidence`          - 同步evidence数据
- `GET  /ws/automation/logs`                    - WebSocket日志推送

---

## 部署方案

### 开发环境
```bash
# 后端
cd backend
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload

# 前端
cd frontend
npm install
npm run dev
```

### 生产环境（Docker）
```yaml
# docker-compose.yml
version: '3.8'
services:
  backend:
    build: ./backend
    ports:
      - "8000:8000"
    volumes:
      - D:/参数化试验出图1:/data
    environment:
      - DATABASE_URL=postgresql://user:pass@db:5432/winnsen

  frontend:
    build: ./frontend
    ports:
      - "80:80"
    depends_on:
      - backend

  db:
    image: postgres:15
    volumes:
      - postgres_data:/var/lib/postgresql/data
    environment:
      - POSTGRES_PASSWORD=your_password

volumes:
  postgres_data:
```

---

## 下一步执行计划

### 第一周：基础框架
1. 搭建FastAPI后端 + SQLite
2. 创建数据库表结构
3. 导入零件清单CSV
4. 实现零件CRUD API

### 第二周：前端界面
1. Vue3项目初始化
2. Dashboard仪表盘
3. 零件列表和详情页
4. PDF预览功能

### 第三周：质量系统
1. Evidence数据解析和导入
2. 质量检查模块
3. 批次追溯功能

### 第四周：自动化集成
1. SolidWorks API集成
2. 展开图批量生成
3. 实时日志推送
4. Docker打包部署
