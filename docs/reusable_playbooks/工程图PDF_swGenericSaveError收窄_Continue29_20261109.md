# 工程图PDF swGenericSaveError收窄

本流程来自Continue29左门样本，只证明如何取得原生错误证据、何时停止。样本PDF仍FAIL，不能当作成功导出流程。适用于原生PDF返回通用错误1且缺模型轮廓的诊断；不采用打印机绕过。

## 冻结输入与归属

1. 读取当前PROJECT_GOAL_FINAL和前轮RESULT，核对精确图纸、引用模型、EXE与证据SHA；确认受保护清单及进程门。所有诊断输出放全新D盘目录，原CAD只读引用。
2. 记录唯一CAD进程PID、启动时间、EXE、monitor的父PID和`--sid`。会话只加载允许路径，`OpenDoc6(options=3)`后读取实际只读、打开errors/warnings、GetSaveFlag，不用文件属性替代API状态。
3. 记录sheet集合、配置引用、可见边/折弯线、LengthUnit、关联尺寸/附着实体，避免把几何未加载、单位错误、关联悬空与PDF后端异常混在一起。

## 同一次原生导出采集

1. 查本机官方帮助，使用`ISldWorks.GetErrorMessages(out Msgs,out MsgIDs,out MsgTypes)`与`GetLastSaveError(out FilePath,out ErrorCode)`。`GetErrorMessages`读取后清空消息栈，应分导出前后立即采样；`GetLastSaveError`针对Microsoft保存错误，其null不能改写成0，也不能当作SolidWorks无错。
2. 对图纸订阅`FileSaveNotify`、`FileSaveAsNotify2`、`FileSavePostNotify`、`FileSavePostCancelNotify`，被动处理器返回0。不要同时订阅已废弃的FileSaveAsNotify和Notify2；不要改名、取消、触发额外保存。
3. 只进行一次原链导出：真实ISheet对象的`DispatchWrapper[]`传给`IExportPdfData.SetSheets(3,...)`；记录`GetWhichSheets/GetSheets`；调用`IModelDocExtension.SaveAs(pdf,0,1,pdfData,ref errors,ref warnings)`，立即纯数据记录布尔返回及原始码，再采消息。PDF存在不表示SaveAs成功。
4. 在进程外读PDF文字、矢量数、图片数，并渲染预览、核对SHA。有效尺寸文字、有效关联和视图可见边数不代替PDF轮廓验收。

## 原生日志与事件

- 用`GetUserPreferenceStringValue(swFileLocationsJournalFile=24)`读取journal目录；只读盘点journal的mtime、SHA。旧journal前后不变时，明确它不是本轮导出证据，不能为了“有日志”而归属给本轮。
- `SolidWorksPerformance.log`用精确`SWSID`与monitor命令行绑定；`FileAccess.log`用输入路径和时间补证。后续会话可能覆盖它们，必须先复制到D盘独立证据目录并冻结SHA。
- 查询Windows Application/System对应UTC窗口，保存RecordId、Provider、PID、XML及查询状态。`NoMatchingEventsFound`不等于没有内部异常；编码失真时另新增正确编码回执，不覆盖旧证据、不重新导出。
- 模块栈仅说明经过哪个模块。没有函数符号、异常子码和触发字段时，不能用模块名猜具体可控条件。退出段的RCW/Penimc异常也不能自动归因为更早的PDF失败。

## Continue29真实观察

- 原生导出返回false/errors1/warnings0；同会话消息为“Arial Unicode MS安装不正确”和“PDF输出：无法处理的例外情况”。MsgID都1122，MsgTypes为48/0。
- `GetLastSaveError`全null；保存通知只触发`FileSavePostCancelNotify`。性能日志有与PID对应的SID和AdobePDFL/sldpdfu栈。Windows无匹配条目；journal旧且不变。
- 图纸68条可见边、12条折弯线、507.65mm且dangling=false；PDF只有507.65文字，矢量0、图片0。
- 日志点名字体后，先在新字节副本读`Annotation.GetTextFormat(0)`、`GetUseDocTextFormat`、字号和文本；实际备注Microsoft YaHei、尺寸/折弯注释Century Gothic，显式Arial Unicode MS匹配0。工具在setter前停止。没有安装字体、没有全图换字体、没有保存图纸/零件或再导出。

## 日志驱动试验的边界

日志先提出候选，读取必须进一步把它对应到一个真实可控字段。只有映射成立，才对本轮新图纸副本做一项最小试验；不回写原输入，不保存零件。修改后的新图纸还须独立options=3回读，关联不得回退。若映射不成立，就记录“未验证：缺 [具体字符、富文本或字体回退来源映射]”，停止。

本样本字体警告已确认，但“字体警告导致无轮廓”的因果未验证。下一步只能先补映射或原生PDF异常子原因；不自动安装字体、换打印机/导出API、重建视图或扩大到其它零件。

## 收尾验收

CAD文档无保存关闭、ExitApp自然退出；核对精确PID消失。仅在父CAD消失后清理本轮已证实归属的孤儿monitor，既有sldworks_fs保留。保护SHA/属性与原输入前后必须一致；记录模型/图纸/PDF保存调用数。合格标准仍为原生PDF errors=0、门板轮廓与毫米尺寸文字同时存在，不能用消息采集成功宣称工程图完成。

本轮证据根：`D:\Winnsen_Structure_Agent_Studio\data\review_portal_runtime\readonly_open_repair_20261105_1920\continue29_swerror_narrow_20261109`。主要证据：`A_log29/raw.json`、`notifications29.jsonl`、`pdf_parse29.json`、`logs_A29`、`log_attribution29.json`、`windows_events_A29_v2.json`、`B_font29/raw_font_trial29.json`、`comparison29.json`、`integrity_final29.json`。读本机`official_api`说明，不引用未读取的接口。
