# 项目检查与缺陷修复记录（2026-10-02）

本轮检查覆盖当前 16029 门户、原生/参数化任务与交付链、SOLIDWORKS 工具源码、React Studio、FastAPI、Git 提交边界和 GitHub CI。已复现的阻断问题完成修复；本机汇总门禁 90 项通过，API 20 项行为测试和 Studio 12 项浏览器检查通过。

## 已修复的问题

| 范围 | 复现的问题 | 修复后的行为 |
| --- | --- | --- |
| 门户会话 | 登出只清除浏览器 cookie，旧 cookie 仍可访问 | 同时撤销服务端会话；畸形 cookie 安全忽略 |
| 参数计算 | 极端比例之和溢出，NaN 门高绕过检查 | 拒绝非有限合计和计算结果，HTTP 提交同样阻断 |
| 原生文件保护 | attempt 内硬链接可指向外部文件 | 拒绝多链接文件，保持外部源文件不变 |
| 原生执行入口 | 未知阶段或无效门高可能进入 CAD 会话 | 在打开 CAD 前校验阶段、有限门高及授权绑定 |
| API 任务创建 | 客户端可传入运行状态，绕过 dry-run | 只接受初始草稿/证据阻断状态；禁用生成器统一拒绝 |
| API 任务并发 | 同一任务可重复执行，dry-run 可覆盖运行状态 | 按状态与版本时间条件领取，冲突返回 409；启动异常记录失败 |
| 超时处理 | TimeoutExpired 的 bytes 日志触发 TypeError | 解码后记录日志和失败状态，释放执行锁 |
| 数据库 | 事务上下文没有关闭 SQLite 连接 | 提交或回滚后关闭连接 |
| 规则提取 | 缺少共享 CAD 锁，run_id 可用反斜杠越界 | 共用 SOLIDWORKS 执行锁；路径与任务绑定，启动失败记录失败 |
| 模板校验 | 名称以 .SLDASM 结尾的目录被当作文件 | 要求实际文件 |
| 16029 旧入口 | 部分 Studio 卡片及历史任务仍可调用旧 worker | 16029 生成入口统一指向原生门户，旧 API 创建/执行返回 410，历史查询保留 |
| 源码复现 | 参数化编译依赖被忽略的生成目录源代码和本机 DLL | 固定源代码逐字节纳入仓库；SDK DLL 从本机 SOLIDWORKS 安装或显式路径取得 |
| 验证流程 | 浏览器脚本仍编辑只读总门数，断言旧页面标题；Git 范围漏掉当前源码 | 改为左右列输入与行为/DOM 检查；逐文件分类，补齐源码和隔离测试 |

固定输入源 SHA-256 和既有 recipe digest 保持，当前登录三维展示未改变。原始 CAD、真实账号、邀请码、订单、生成包和编译输出不进入本次源码提交。

## 验证证据

| 检查 | 结果与范围 |
| --- | --- |
| 当前主线 | 90 项汇总 PASS，包含 native 契约、任务/租约、阶段/授权/发布、HTTP、已有交付包完整性、C# 编译、Git 范围、API、web build/lint。旧静态入口断言修正后单独复验 23 项 scope gate 通过，其余未改变检查复用本轮结果 |
| API | 20 项 unittest，通过临时数据库、ASGI 请求和 mock worker 验证并发领取、失败、超时、路径、退役入口及连接生命周期 |
| Studio | 12 项浏览器检查：八个页面、三个 16029 入口、390 px 手机导航，无页面异常或横向溢出；API 返回隔离只读 fixture |
| 门户 | 隔离 HTTP 验证账号隔离、畸形 cookie、溢出输入和登出撤销；浏览器验证左右列提交、返改、状态轮询、已有校核包下载与 SHA-256、搜索和手机布局 |
| 原生执行器 | SDK 编译通过；14 项纯路径/入口/授权检查通过，CAD 进程集合前后相同；固定源适配器拒绝源码漂移 |
| 干净源码树 | 12 组 CI 核心测试在没有 CAD、历史任务或预编译 bin 的隔离目录通过；源适配器另行验证 |
| 自动配高 | 本轮前已验收的 20 组功能、37 组布局及 10,000 组算法案例复用；本轮仅增加数值异常拒绝，未改门高分配公式 |
| Git 边界 | 无未分类文件；凭证模式扫描未发现命中；模型、运行记录、SDK/EXE/DLL 和本机配置保持排除 |

本机完整日志保存在忽略的 `output/github-audit-20261002/`，不把账号 fixture、运行记录或生成截图发布到公开仓库。可复现的测试代码随源码提交。

## 如何复验

在已配置源模型和 SOLIDWORKS 2020 的 Windows 建模主机上运行：

```powershell
tools/verify_16029_current_mainline.ps1
node workers/native_model_requests/parametric_v1/build.mjs audit-safety
powershell -NoProfile -File workers/native_model_requests/parametric_v1/verify_context_safety.ps1 -BuildTag audit-safety
node tools/verify_16029_workbench_browser.mjs
```

源码或 CI 环境不具备原始 CAD/SDK 时，使用 GitHub workflow 中的隔离测试集合；recipe、assembly 和 stage executor 的 `--fixture-only` 明确跳过需要实机 CAD/Interop 的部分，默认完整实机门禁不降低。

```powershell
cd apps/web
npm ci
npm run build
npm run lint
cd ../../services/api
python -m pip install -r requirements.txt
python -m unittest discover -s tests -v
```

## 未验证的范围

- 未验证：缺本轮修复后真实 CAD 新订单构建、左右不对称订单完整原生实跑及工程师接收证据。本轮没有启动真实生成队列。
- 未验证：缺修改后源码在现有长期运行服务中的加载证据。本次 GitHub 更新不等于部署，未停止或重载现有服务。
- 未验证：缺异机模型/SDK配置、备份恢复和同事电脑端完整下载重开演练。

本记录是软件检查结果，不构成机械结构、开门运动、生产图纸或生产放行验收。
