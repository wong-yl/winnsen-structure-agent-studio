# 新会话镜向右板诊断候选

本目录只交付研究候选，未运行 CAD；`modelAccepted`、`operationComplete` 始终为 `false`。

三阶段各用独立空会话，必须逐项通过后继续：

1. `mirror-create`：固定参数源以 `OpenDoc6` options `3` 只读打开，要求 errors/warnings 为 `0`、saveFlag 为 `false`；复用原 `MirrorPart2(false)`、折弯及几何门，只保存 owned `runs` 下的新临时右板。
2. `temporary-cold`：新会话严格只读打开临时右板，执行原 `CaptureGeometry`、`GeometryGate(R9)`、`MirrorGeometryGate`。通过后才复制旧右板到 owned final 目录，再用原 `[IO.File]::Replace(temp, final, backup)` 路径语义替换。
3. `final-cold`：新会话对最终名右板执行同样的严格冷开和原几何门。

镜向结果保存前及两次冷开的实际引用必须全部指向固定参数源，引用文件与状态数组长度一致，状态仅允许 `3/4`。每个 loaded 文档只允许固定 119 库存或本次候选；记录路径、readOnly、saveFlag 及原始引用。源文件不保存、不重建；临时冷开失败即停止替换。

每阶段诊断上限 180 秒，自然退出观察 45 秒。沿用原全局 worker 锁、10 秒心跳、portal 活动任务/lease 拒绝、精确 owner/外部观察及超时保锁；不强杀 CAD。保护清单重新采全 PROBE、固定失败 attempt、历史路径和 `PROGRESS.md` 的当前哈希。

从 `D:\Winnsen_Structure_Agent_Studio` 运行：

```powershell
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_mirror_fresh_v1/run-right-mirror.mjs --dry-run
```

主窗口审阅冻结证据后，原生执行参数为 `--run-native`。本执行者只完成编译、现有静态门、语法核对和离线预演。

`OriginalGeometry.cs` 保留 `extraction.json` 中 36 个原方法/类型文本；核对器显式用 UTF-8 读取 JSON，避免 Windows PowerShell 5.1 按 GB2312 解码中文。`prepare.py` 是一次性初稿生成器，后续审查小修直接落在交付文件，禁止重跑。`bin/6FE6E4CBD9F2`、`bin/9C0D962E4763` 保留早期编译证据；当前版本以 `build.json` 为准。
