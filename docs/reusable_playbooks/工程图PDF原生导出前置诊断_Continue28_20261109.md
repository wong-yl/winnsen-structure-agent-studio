# 工程图 PDF 原生导出前置诊断（Continue28）

## 用途与结果边界

用于SolidWorks原生PDF导出失败、PDF有尺寸文字但没有模型轮廓时的只读定位。Continue28左门样本中，图纸关联通过、PDF仍失败；此流程不等于工厂图纸验收。

## 先冻结输入与进程范围

1. 从结果记录中取得图纸、引用零件、样本身份和SHA。确认原目录只读，本轮工具和产物放进全新目录。
2. 导出前复核受保护路径SHA/属性和旧证据清单，记录当前SolidWorks及其监视进程。用全新、唯一拥有的SolidWorks会话，不能接管不明进程。
3. 零件和图纸优先`OpenDoc6(options=3)`（Silent+ReadOnly，不含ViewOnly）。记录路径、文档类型、`IsOpenedReadOnly`、`IsOpenedViewOnly`、`GetSaveFlag`、打开错误与警告；限定全部加载文件均是允许路径。实际可写、来源身份不符或警告门不通过即停止。

## 导出前只读字段

- Drawing：页总数、全部页名、当前页、页属性、`LengthUnit`及用户单位换算、PDF相关偏好、图层列表和可见性。
- Drawing view：`GetVisible`、`SuppressState`、`DisableAutoUpdate`、显示模式、引用模型完整路径、引用配置、压缩状态、根组件可见性、visible component/entity edge数量、折弯线数、`GetDisplayData3`。
- HLR视图可补采`GetPolyLineCount5(1)`和`GetPolylines7(1)`。非零说明API能枚举可见实体边对应的折线数据；不能由此保证PDF后端会绘制它。Shaded、ShadedWithEdges、DraftQuality或FastHLR/HLV模式下这两个接口可能不给数据。若窗口范围/缩放造成数据范围疑虑，先将其作为缺证记录；未经独立批准不要在只读诊断里自动重建或修改视图。
- 尺寸/注释：显示值、用户单位、`IsDangling`、附着实体数和类型、显示数据、图层、可见性。视图显示和尺寸关联要分开判断。
- SolidWorks：应用/文档可见性、active document、Detailing/RapidDraft/ViewOnly状态、active view和显示框、`NeedsRebuild`/`NeedsRebuild2`、选择数。所有这些均只读采集。

## 单次原生 PDF 诊断

1. 通过`GetExportFileData(swExportPdfData)`获取PDF数据对象，以**当前真实Sheet对象**构造`DispatchWrapper[]`，调用`SetSheets(swExportData_ExportSpecifiedSheets, wrappers)`。原样记录参数、返回值、`GetWhichSheets`、`GetSheets`、页名。SetSheets成功只表示页选择被接受。
2. 在独立新输出路径只调用一次`IModelDocExtension.SaveAs(pdf, swSaveAsCurrentVersion, swSaveAsOptions_Silent, exportData, ref errors, ref warnings)`。立即记录布尔结果、errors/warnings原值、API、路径和调用计数；不以PDF文件存在代替成功。
3. CAD会话退出后用进程外解析器读PDF：每页文字、页面尺寸、矢量对象数、图片数、文件SHA，并渲染预览。文本有尺寸、但矢量和图片都是0且预览空白时，记录“仅标注、无轮廓”，不可判合格。
4. 完全关闭不保存并自然退出本轮会话。按精确PID/启动时间/映像确认进程退出；只清本轮明确归属且父进程已退出的孤儿监视进程。不要触碰既有SolidWorks文件服务器进程。

## Continue28样本观察

- A：只读打开和页选择成功；一张「展开审阅草稿」；LengthUnit 0，毫米换算因子1000；视图可见、非压缩、HLR，引用指定左门的`DefaultSM-FLAT-PATTERN`；68条可见边与68条折线、12条折弯线；尺寸507.65mm，`dangling=false`，附着2个Vertex。`GetPolyLineCount5`的PointCount原值1209是double缓冲元素数。`GetPolylines7`返回边对象数组，而out参数是数值数组；采集器字段名字曾把两者颠倒，读原始JSON时必须按实际类型和官方签名解释，不能把1209报成边数或顶点数。
- A导出前应用和图纸文档均隐藏。`SaveAs`返回false/errors1/warnings0；PDF只含507.65文字，矢量/图片数0。
- 只在字节复制的新图纸副本上将应用与图纸设为可见做一次B；仍为只读、`GetSaveFlag=false`。相同API、同一页包装仍返回false/errors1/warnings0，PDF外观/内容相同。独立只读重开后关联尺寸和引用保持。
- 官方本机`swGenericSaveError=1`是通用错误；“可能因SolidWorks隐藏而不完整”的描述属于另一个枚举4096 `swFileSaveAsNotSupported`。本样本实际是1，因此不能把失败归因于隐藏。`SetSheets=true`、边数据非零、关联有效也都不能解释PDF为何未绘制轮廓。

## 停止与后续

若单项、有证据的显示前置条件试验后错误与PDF内容不变，停止，不追加第三种修法、不切打印机、不换API、不保存模型。下一轮先取得可归属本会话的保存通知、原生诊断日志或能细分`swGenericSaveError`的证据，再决定单个图纸侧试验；缺证时保持FAIL并写`未验证：缺 [...]`。

引用：`continue28_pdf_diag_20261109/official_api`下的本机SolidWorks 2020 API帮助；`continue28_pdf_diag_20261109/A_readonly28/raw.json`、`A_readonly28/pdf_parse28.json`、`B_visible28/raw.json`、`B_visible28/pdf_parse28.json`以及本轮RESULT。具体路径位于`data/review_portal_runtime/readonly_open_repair_20261105_1920`。
