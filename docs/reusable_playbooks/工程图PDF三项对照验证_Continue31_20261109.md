# 工程图PDF三项对照验证（Continue31）

适用范围：只读诊断“原生PDF只有尺寸文字、没有模型轮廓”。本轮案例为室内柜1000×2000×500双列八门左门。打印对照须有当轮明确授权；本轮流程不自动授权后续打印、字体替换或系统安装。

## 流程

1. 先核对输入路径、SHA/属性和原生关联。OpenDoc6 options3，记录实际只读、errors/warnings、GetSaveFlag、507.65mm尺寸dangling及附着顶点、展开配置/UiBend/边和折弯线。warning1/128、actualReadOnly和健康门保持原要求。冻结前轮证据只读引用。
2. 字体核对同时记录请求字体和GDI实际选中face。用GetGlyphIndicesW(flags1)检查目标码点；65535是直接缺字。字体目录须查询字体name表，注册表查HKLM/HKCU双视图，辅以EnumFontFamiliesExW；不能只查ARIALUNI文件名，也不能把GDI自动替换选中的宋体当Arial Unicode MS已安装。
3. 指定官网不可访问时保留HTTP状态，不编造网页摘录。可只读提取本机2020 sldworks.chm同名官方主题并记录SHA，区分官方规则、现场条件、实际导出内部调用与异常因果。字体告警和PDF例外同时出现不等于因果已证实。
4. 原生路径保持同一接口：真实ISheet DispatchWrapper数组 → SetSheets(3,wrapped) → Extension.SaveAs(pdf,0,1,pdfData,ref errors,ref warnings)。立即记录布尔值和原始errors/warnings、GetErrorMessages，不靠文件存在倒推成功。
5. 获明确授权的打印对照先读本机PrintOut4帮助。GetPrintSpecification仅设置打印任务的PrinterQueue、PrintToFile、非空PrintFile、页范围/份数，PrintOut4显式传打印机与绝对文件名。不要设置系统默认打印机/doc.Printer/sw.ActivePrinter，不改PageSetup或ConvertToHighQuality等选项。若无法静默指定路径，停止此分支。PrintOut4返回void且没有errors参数，记录是否异常和文件结果，禁止编写errors0回执。
6. 同一会话只读读取IView.GetDisplayMode2/GetFacettedHlrDisplay/GetDisplayEdgesInShadedMode/GetVisible、边数、ModelView.HlrQuality和系统/文档PDF质量偏好。纸页视图GetDisplayMode=-1不等于草稿；GetLineCount2=0不能覆盖GetVisibleEntityCount2=68。系统专属偏好在文档getter返回false/-1不代表有效独立设置。
7. 进程外解析大小、页数、Producer/Creator、全文、绘图/图片数量、字体和页面尺寸，渲染PNG实际查看。必须同时核对线条完整性、页边裁切、中文、毫米尺寸、图框标题栏；矢量对象数不是模型边数/折弯线数。
8. 不保存CAD，CloseAllDocuments(true)丢弃内存改动、ExitApp自然退出。只清父CAD已退出且PID/启动时间/SID精确归属的本轮孤儿monitor，既存fs保留。终检输入/保护及冻结证据SHA和属性。

## 本样本事实与局限

Century Gothic的U+4E0A缺字65535，微软雅黑1031；指定范围Arial Unicode MS未找到。原生false/errors1、矢量0；Microsoft打印116个矢量对象且中文“上”可见，但纸页从A3到约A4且左侧裁切，不能交付工厂。展开视图GetFacettedHlrDisplay=false，窗口swPreciseHlr；HighQuality=false没有经过开关对照，不能认定为根因。

“有线条”“关联通过”“打印调用无异常”均不能替代原生PDF errors0、完整门板轮廓及毫米文字的验收。未取得条件因果时使用“未验证：缺 [...]”；最多提出一项下轮最小候选，本轮不盲改。

证据入口：[RESULT_Continue31_PdfTripleCheck_20261109.md](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/RESULT_Continue31_PdfTripleCheck_20261109.md>)、[comparison31.json](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue31_font_source_20261109/comparison31.json>)、[raw.json](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue31_font_source_20261109/A_check31/raw.json>)、[font_inventory31.json](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue31_font_source_20261109/font_inventory31.json>)、[integrity_after31.json](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue31_font_source_20261109/integrity_after31.json>)。
