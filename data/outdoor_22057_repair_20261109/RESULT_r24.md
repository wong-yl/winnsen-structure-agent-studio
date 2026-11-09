# 22057 工作台恢复验收 R24

此次精确规格的工作台生成、原生保存、独立重开、九项校核和实际下载闭环已恢复，最终验收为 PASS。

- 验收日期：2026-11-09（Asia/Shanghai）。
- 任务：`OUT22057-8258be04-1492-4b2d-8c12-9ddb7e304a17`。
- 工作台任务名：`1111111-恢复验证R24-860宽左7右9`。
- 规格：860W × 1939H × 550D，左 7 门、右 9 门，共 16 门。
- 左列自下而上门高：`318 / 268.667 / 219.334 / 219.333 / 219.333 / 219.333 / 318` mm。
- 右列自下而上门高：`196.667 × 6 / 196.666 × 3` mm。

## 修复内容

R24 在 R23 基础上清除保存后的旧装配对象引用；请求 SOLIDWORKS 退出后，仅释放一次本轮持有的应用连接，再等待自然退出；异常清理在应用连接存在时才关闭文档，避免遮蔽原始错误。

本轮没有放宽原有保存、90 秒自然退出、队列、锁或几何校核要求，没有强制终止 CAD。当前工作台入口指向 `runtime_v24`，输入范围没有扩大。

## 已验证结果

- 本地完整样本和工作台实际新任务均完成原生保存、两次独立重开及九项校核，结果 PASS。
- 实际任务建模进程为 `21492`；保存后独立导出进程为 `9900`；两次重开进程分别为 `35740`、`30308`。
- 180 份原生文档、399 个实体；三份 STEP 各 23,844 条边，错误端点引用均为 0，399 对 399 双向几何比较 PASS。
- 保存后中央右筋的直属配合独立读回 PASS：保留原有 3 个配合，完全约束，候选文件未变。
- 385 个受保护原文件及 808 个 v17 支持文件核验通过，原文件变更列表为空。
- 三条历史失败任务保持原有 `failed` 状态及校验值；旧失败证据保留。
- 最终 SOLIDWORKS 进程数为 0。

上述 CAD 验证已完成。本次收到下载文件后，只执行 ZIP、已有证据和受保护文件的最终核验，没有再启动 CAD。

## 实际下载交付

- 下载文件：`C:\Users\Administrator\Desktop\WINNSEN-22057-1939H-860W-550D-trial.zip`。
- 文件大小：`86947028` 字节。
- SHA256：`ae653de932f0a5ac58c2c9156ae4ffd35a2397a1c9be21acc54344c5114ca03e`。
- 实际下载与服务端发布 ZIP 的校验值一致；ZIP 完整性及包内清单逐项校验 PASS，共 199 项文件。
- 实际下载回执已记录，任务状态为 `ready`。
- 最终验收记录：`D:\Winnsen_Structure_Agent_Studio\data\outdoor_22057_repair_20261109\OUT22057-8258be04-1492-4b2d-8c12-9ddb7e304a17_acceptance.json`。

## 证据与版本

- 生成器：`runtime_v24\OutdoorWidth600R440.exe`，SHA256 `82732c3830a14c938878c9cef3d230aa56fc94c0fba58a061093a350868ecd99`。
- 源码变更：`source_diff_r24_from_r23.patch`。
- 入口变更：`release_route_diff_r24.json`。
- 原生任务结果：`runtime_v24\evidence\portal_8258be0414924b2d8c129ddb7e304a17_result.json`，SHA256 `942802521598840813adc3c08af50f8b47a01b57c4e2eae5d65fdf39975b92f4`。
- 导出会话补证：`runtime_v24\evidence\portal_8258be0414924b2d8c129ddb7e304a17_export_session_acceptance.json`。
- 保存后配合补证：`runtime_v24\evidence\portal_8258be0414924b2d8c129ddb7e304a17_saved_divider_acceptance.json`。
- 最终核验命令：使用现有 Python 运行 `finalize_restored_task_r24.py OUT22057-8258be04-1492-4b2d-8c12-9ddb7e304a17 --downloaded "C:\Users\Administrator\Desktop\WINNSEN-22057-1939H-860W-550D-trial.zip"`，退出码 0，结果 PASS。

## 验收边界

此次完成的是上述精确规格的工作台闭环恢复，不扩大连续尺寸范围，也不构成工厂生产放行。`production_release=false` 保持不变。

底层 STEP 导出端点错连的原始原因尚未证实；本轮经独立导出和原有校核确认该交付通过，未将原始原因表述为已定位修复。
