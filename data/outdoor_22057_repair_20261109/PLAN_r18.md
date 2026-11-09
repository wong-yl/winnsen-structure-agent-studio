# 保存门修复续验

1. 核对原自有 CAD 已退出、队列空闲、服务健康，冻结原始失败证据。
2. 从完整校验的 runtime_v17 建立全新 runtime_v18。只在 SaveDocs 前记录每份文档的路径、GetSaveFlag、IsOpenedReadOnly、Within、ReadOnlyAuditPaths 命中及候选根路径；保存保护语义不变。编译并实跑原 860×1939×550、左7右9门参数。
3. 依据明确失败文档，在另一个全新候选中做最小修复；每次失败保留现场。母版、R/U、旧v16/v17、用户其它修改不动；不强杀或绕过进程门。
4. 完成原生生成保存、三个独立原生阶段和所有原有几何审计、ZIP/manifest SHA 核对，再接回工作台并验证实际下载。未通过时 release_gate 保持原版。
