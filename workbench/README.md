# Winnsen订单工作台

## 快速启动

### Windows
双击运行 `启动工作台.bat`

### 手动启动

#### 后端
```bash
cd backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

访问: http://localhost:8000/docs (API文档)

#### 前端
```bash
cd frontend
npm install
npm run dev
```

访问: http://localhost:3000

---

## 功能模块

### 已实现 ✓
- [x] 项目信息展示
- [x] 零件清单查询（支持按类别/批次筛选）
- [x] 统计汇总（工程图/PDF/DXF完成度）
- [x] 部件分类可视化
- [x] 质量检查API

### 开发中 🚧
- [ ] 零件详情页
- [ ] PDF在线预览
- [ ] 批次追溯时间线
- [ ] Evidence数据展示
- [ ] 展开图批量生成（SolidWorks API）

### 计划中 📋
- [ ] 用户权限系统
- [ ] BOM自动生成
- [ ] 实时日志推送（WebSocket）
- [ ] 数据库集成（SQLite/PostgreSQL）
- [ ] Docker部署

---

## 技术栈

### 后端
- FastAPI (Python 3.8+)
- Pandas (数据处理)
- PyWin32 (SolidWorks API)

### 前端
- Vue 3 (Composition API)
- Vite (构建工具)
- TailwindCSS (样式)
- Axios (HTTP客户端)

---

## 项目结构

```
workbench/
├── backend/
│   ├── main.py              # FastAPI应用
│   ├── requirements.txt     # Python依赖
│   └── venv/                # 虚拟环境（自动创建）
├── frontend/
│   ├── src/
│   │   ├── App.vue         # 主界面
│   │   ├── main.js         # 入口
│   │   └── style.css       # 全局样式
│   ├── package.json        # Node依赖
│   └── node_modules/       # 依赖包（自动创建）
├── ARCHITECTURE.md         # 架构设计文档
└── 启动工作台.bat          # 一键启动脚本
```

---

## API端点

### 项目信息
- `GET /api/project/info` - 获取项目配置

### 零件管理
- `GET /api/parts` - 零件列表（支持筛选）
  - 参数: `category` (部件类别), `batch` (批次号)
- `GET /api/parts/{part_number}` - 零件详情

### 统计分析
- `GET /api/statistics/summary` - 统计汇总

### 质量检查
- `GET /api/quality/checks` - 质量检查结果

---

## 下一步

### 轨道1：补充展开图和BOM
运行自动化脚本:
```bash
cd ../scripts
python generate_flatpattern_bom.py
```

### 轨道2：完善工作台功能
1. 添加零件详情页
2. 集成PDF.js预览
3. 导入evidence.json数据
4. 实现批次追溯时间线

---

## 故障排查

### 后端启动失败
- 检查Python版本: `python --version` (需要3.8+)
- 检查依赖安装: `pip list`
- 查看错误日志

### 前端启动失败
- 检查Node版本: `node --version` (需要18+)
- 清理缓存: `npm cache clean --force`
- 重新安装: `rm -rf node_modules && npm install`

### API请求失败
- 确认后端已启动: http://localhost:8000
- 检查浏览器控制台Network标签
- 检查CORS配置

---

## 贡献者
- Eli (项目负责人)
- Claude Opus 5.5 (开发助手)
