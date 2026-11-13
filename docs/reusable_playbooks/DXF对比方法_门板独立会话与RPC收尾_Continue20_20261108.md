# 门板独立会话导出与 RPC 收尾

本篇补充 Continue19 文档，保留其历史 FAIL。适用对象是室内柜样本 `PARAM-20261107121230852-8D5DE5` 的左右 0.8 门板；本机 SolidWorks 2020 的已验证流程以 Continue20 原始记录和两份独立文件证书为准，不能直接外推到其它版本或零件。

## 从真实平板出发

1. 由冻结 package 建独立测量副本，闭文档重定位引用后冻结 SHA。每块门使用自己的副本和独立 CAD 会话；不要用左门文件替代右门。
2. 本轮门板只读打开使用 `options=3`，实际只读状态必须为 true。依赖与目标仅在新副本内临时设置 Windows 只读属性，结束后恢复原属性并核对全部字节。warning1/128 与原零 warning 健康门保持原标准。
3. 按本门原生身份绑定已有派生配置、平板特征、固定面和唯一钣金实体；仅在获准的内存范围内展开相应 12 个 `UiBend`。核对其它配置及所有非授权特征状态不变。不能只因配置名包含 `FLAT-PATTERN` 就认定已展开。
4. 导出前必须量到有效 `473.4×507.65×0.8 mm` 平板，原厚度、双平行面和生效补偿门不变。采集本门 12 条 `IsBendLine=true` 原生线的端点、坐标变换和身份，供进程外比对。

## 把 API 返回与后续对象访问分开

Continue19 的异常发生于导出调用后的事件构造：为了记录状态，代码先查询旧 `ModelDoc2.GetPathName()`，抛 `RPC_E_DISCONNECTED`，使已获得的 API 返回值也没有落盘。因此事件应先记录纯数据：调用开始/结束时间、原始 bool、异常与 HRESULT、预声明输出路径和选项。不要把可能失效的 COM getter 放进这条事件。

Continue20 方法1调用一次 `ExportFlatPatternView(output, 0)`，随后不再查询旧文档或旧特征对象。此处 0 是旧接口的 `swExportFlatPatternOption_None`，不是 `ExportToDWG2` 钣金位选项。两类选项不能混用。

- API true、无异常且已直接产生非空 DXF：保留这份原生文件，不再 SaveAs 覆盖，不修改图层或单位。
- 只有 API true、无异常且完全没有文件，才允许重新取得当前活动文档；重新核对精确路径、配置、实际只读、完整特征状态和有效平板后，使用预声明的新 DXF 路径走格式导出。没有满足条件就停止，不能拿文件存在替代未知或 false 返回。
- 本轮实际走的是直接落盘分支；DXF SaveAs 实际调用数为 0。备用分支仅编译和静态检查，没有运行验证，不应写成已验证方法。

## 会话收尾与文件认证

导出后通过应用层检查加载文档范围，关闭并丢弃未保存内存变化，再请求 CAD 自然退出。最多等待 120 秒，不靠强杀制造正常结束。仅清理有精确归属、且父 CAD 已退的本轮孤儿 monitor，保留 `sldworks_fs`。

在 CAD 退出后，由独立 Python 解析 DXF，保持文件读取前后 SHA 一致，分别核对：

- 原生 API 返回成功、文件路径/时间/大小/SHA、会话自然退出及属性恢复。
- `$INSUNITS=4`，外闭环 `473.4×507.65 mm`，两轴与公式偏差均在 0.5 mm 内；同时保留原始 X/Y 与比较轴映射。
- 12 条 DXF `CENTERX2` 与本门 12 条原生折弯线端点，在单位尺度轴置换/反射与整体平移下全部匹配；不得缩放或按公式拟合。外轮廓 `Continuous` 与折弯线可区分，即使它们同在 0 层。
- 所有候选孔、切口、开链及不支持实体如实列出；折弯线身份匹配不等于工厂已认可图层/折弯方向/视图手性。

本轮 native JSON 的 `exportChainComplete/operationComplete=true` 与原健康门是不同字段。`warnings=2`、`warningGatePassed=false`、原 EXE/launcher exit1 保留，不加入允许清单。文件尺寸、单位与折弯线通过不能改写原健康门，也不是工厂放行。

## 如何判断 RPC 断开

先看同一精确归属的 CAD 进程是否仍活着，应用接口是否还能枚举文档并执行自然退出，再查 Windows/SW 日志。Continue19 原始记录显示旧文档对象失效后，应用能返回空文档列表并正常退出，故不能仅凭 RPC 错误称为整个 CAD 崩溃。

“文档关闭或内部替换使旧对象失效”仍是限定推断；缺历史文档生命周期日志时，不能断言是哪一个内部动作。Continue20 的修复是避免再次访问可能失效的旧对象，既没有证明内部根因，也没有修改模型来消除断连。

## 本轮入口

- [详细结果](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/RESULT_Continue20_DoorExportChain_20261108.md>)。
- [旧调用诊断](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108/diagnosis/previous_export_diagnosis20.json>)。
- [左门文件证书](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108/door_dxf_certificate20_door_panel_left_W437_H471p25.json>)。
- [右门文件证书](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108/door_dxf_certificate20_door_panel_right_W437_H471p25.json>)。

本篇只新增流程文档；没有修改被冻结的旧文档、原 DXF、CAD 或公式。
