# 测量工具误判：对象生命周期与局部凸起

本篇补充 Continue19 同名主题，使用 Continue20 已记录的数据，不替代原工具门禁。

## 证据写入前调用失效对象，会丢掉真正返回值

事件构造中的 getter 同样会执行。Continue19 导出后先读取旧 `ModelDoc2.GetPathName()`，异常导致原始 API bool 没有写入；收尾虽然有 DXF，仍不能回填未知返回为 true。正确顺序是先落盘原始 bool/异常，再由不依赖旧对象的文件检查和进程外解析取证。

`RPC_E_DISCONNECTED` 说明所调对象不可用，不自动证明 CAD 进程崩溃。本例旧对象断开后，应用层仍可返回空文档列表、CloseAllDocuments 和自然退出。内部为什么关文档或替换对象，缺生命周期日志就保持未验证。

## exit1、文件通过和健康门分别记录

本轮原生导出链可完成，而原 EXE 因 `warnings=2` 返回1；launcher保留 `operationComplete=false`。不能把这个退出码改为0，也不能因此忽略已取证的 API true、文件单位/尺寸/折弯线和自然退出。报告同时列局部导出结果与原健康 FAIL，避免跨层覆盖。

同样，磁盘 SHA 未变不代表内存 `saveFlag=false`。本轮只读副本内的获准展开操作可令 saveFlag=true，关闭丢弃即可；必须由导出前后字节、属性、锁与进程证据说明没有保存，不能声称所有配置/展开/重建调用为0。

## 数量相等不等于折弯线身份一致

DXF 中12条中心线不能只因数量等于12个子折弯，就认作原生折弯线。Continue20采本门 `IsBendLine=true` 的12组端点，再做单位尺度轴置换/反射和平移的端点集合匹配；不用缩放、公式拟合或写回DXF。

匹配对称解仍不能消除视图手性歧义，工厂的折弯方向、切割/打标和公差仍要单独确认。

## 小面有规律，不能当成随机噪声

R的6个平行小面已由原生所属特征与共享边证据绑到 `MirrorStock id81`。每个约12.566370614 mm²、圆柱半径2 mm；4处从最近主板伸出9.0 mm，2处伸出9.2 mm，整个实体跨度仍为10 mm，材料主板层为0.8 mm。

这些端面通过共享边连到圆柱再连到主板，支持“局部圆柱凸起”的几何判断。不能只看端面面积等于圆面积就猜压铆、定位或焊接用途。规则尺寸、成对位置和L对应更支持有意建模的局部构造，也可能是旧构造被继承；当前是否需要、如何加工仍缺证据。

R与镜向L的四对中心在原1e−6 mm证据容差内；另两对Z各差约+0.004276765 mm，原容差内判否。不能因为它小于尺寸报告的0.5 mm阈值就把两个对象视为同一原生面；身份容差、主板尺寸比较与制造公差是不同标准。

R实际引用链为 `R → R参数源 → 标准寄存柜模型`，不是直接 `R → L`。几何相似不能冒充原生源面谱系。主板面 `533.70855353×1939.60855353 mm` 的限定尺寸证据保留，完整实体厚向/平行面/补偿的 FAIL 不改变。

## 复用证据，别重做已通过的慢步骤

从中断续接时，先核对已有源码/EXE/输入/输出 SHA和精确运行归属，再接收原会话。工具已在跑就不再开第二个CAD；已完成的副本、诊断与残留分析保持原样。离线字段适配问题只修读取工具，不重开CAD补一模一样的数据。

本轮没有重测L、没有重开R CAD、没有抑制凸台，也没有把主板面通过改写成完整实体通过。

## 证据入口

- [Continue20详细结果](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/RESULT_Continue20_DoorExportChain_20261108.md>)。
- [R只读归属报告](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108/r_residual/RESIDUAL20.md>)。
- [R原始离线证据](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue20_door_export_20261108/r_residual/residual_evidence20.json>)。

未验证：缺 [凸起设计用途和工艺说明、R源面完整谱系、两处0.004276765 mm差值原因、完整有效平板/生效补偿、工厂制造确认]。
