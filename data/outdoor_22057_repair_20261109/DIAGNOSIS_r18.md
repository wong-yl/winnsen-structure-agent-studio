# 保存门采证结论

v18 只给 SaveDocs 补充逐文档原始字段；27 份 C# 中仅一份追加采证，其它源文件和保存保护检查不变。2026-11-09 同规格实跑得到原始错误 `Refuse save outside writable copy`。

- 已加载155份文档，路径越界0份。
- 待保存、非固定采购件、非审计豁免的内部只读文档87份；首份为 `防水寄存柜 模型.SLDPRT`。这些文件磁盘属性不是 ReadOnly。
- `ReadOnlyAuditPaths` 数量0，`saved_documents=[]`。因此本次不是 Snapshot 的审计只读文档，也不是路径越界。
- 原始证据：`runtime_v18/evidence/save_probe_r18_generate.json`，SHA256 `ed3af1fc65e3dafbb0c7a8ec3f12cb8cd59aa71a349242459796b4f0508c327a`。
- PID12720 通过原有身份核验及自然退出等待，UTC 01:26:47.5102641 确认消失，回执 `runtime_v18/save_probe_r18_generate_owned_cleanup_poll_r4.json`。

独立只读专家复核了实际记录及 r19 修复脚本。修复采用官方 `SetReadOnlyState(false)`，只作用于当前全新 `generated_` 目录的原生副本，排除固定采购件及审计只读文档；核对方法返回值及实际只读状态。保存门本身不变。官方说明该方法不改变磁盘权限，磁盘只读或其它用户写锁存在时仍失败。本机官方资料提取在 `api_reference_r18/`。

Main 入口补充 `[STAThread]` 并记录实际线程模型，仅用于规范原生自动化线程。不能凭保存和退出改善断言它是原驻留根因。

未验证：缺 [依赖首次以只读状态加载的具体内部原因]。此限制不允许删除原只读门或跳过待保存文档。
