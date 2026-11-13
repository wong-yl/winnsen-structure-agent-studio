结论：工程图创建时能读到正确尺寸，不代表保存后仍有几何关联。先冻结证据，再把“关联”“显示单位”“PDF内容”三条链分开检查。

本篇只记录Continue24已证流程与限定。案例为室内双列八门1000×2000×500，合格工程图仍0/4；不能当生产出图通过流程。

## 只读诊断顺序

1. 读当前授权/停止报告。核对图纸、模型、模板、EXE与保护清单的SHA/属性；确认无CAD或不明monitor。每份图纸独占会话，不另开CAD绕过驻留进程。
2. 精确闭文档依赖回查；必要时先显式只读打开引用模型，再只读OpenDoc6图纸。逐文档记录actualReadOnly和GetSaveFlag，不能把只读等同不脏；不得为读取旧证据改保护文件属性。
3. 创建/保存前、保存后未关闭、独立重开各采一次IsDangling、附着实体、配置、引用路径和可见几何。缺阶段就保留不确定范围，不能倒填。GetAttachedEntities3返回null时对照types和IsDangling；不能将null伪造成有效边或0/null通过。
4. 显示单位用LengthUnit、GetUnits、UserUnit和DisplayDimension.GetUseDocUnits/GetUnits交叉核验。图纸比例和视图比例另记。尺寸SystemValue为SI值，不等于屏上文字。
5. PDF分别看API布尔返回、错误码、页集合、图纸可见性和最终文件内容。返回false的PDF只能是失败证据；文件存在不表示成功。图纸有100条可见拓扑边但显示数据0，不等于不存在几何；GetBodiesCount针对多实体视图，不应把本单实体返回0误判为空模型。
6. 原生模型视图与Sheet容器视图区分：Sheet容器不适用的折弯线API返回RPC_E_SERVERFAULT时保留错误，不写0。不要因诊断字段异常去改模型。
7. CloseAllDocuments(true)丢弃内存改动，再ExitApp，核返回、文档归零和自然退出。只清归属明确且父已退出的本轮孤儿monitor；不强杀。复核所有SHA/属性与锁。

## 已证案例

- 两失败图纸和原模板LengthUnit=3、GetUnits=[3,1,8,2,0]，即英寸；标注沿用文档单位。原生单位转换直接得到19.99，来自507.65/25.4的两位小数，不是毫米尺寸错误。
- 历史内存中12个UiBend展开，未保存；磁盘相同派生配置仍压缩。当前图纸附着两项null且dangling=true。可证明前后状态不同，尚未证明保存动作或重开动作具体哪一个使关联失效。
- SetSheets(ExportAllSheets1,null)=false，空模板也复现；GetWhichSheets1/GetSheetsnull不等于文档无页。SaveAs帮助允许ExportData=null导出全部页，但本机实际SaveAs仍false/errors1，只能按swGenericSaveError记录。
- 本机SetSheets说明页称名称数组，2020官方C#示例实际使用Sheet对象的DispatchWrapper数组与ExportSpecifiedSheets3。下一轮可做最小接口对照，本轮未试修或出第三份图。

## 下一轮保存边界建议

仅建议下一轮新增副本内的左门SLDPRT以及引用它的新SLDDRW/PDF进入可保存白名单；保存内容只限被验证的派生展开配置状态及新图纸，不改孔位、公式、厚度、补偿或Default设计态。需要保存前后逐ID状态/几何/SHA差异与独立重开证据；源包、母版、保护3082/166/75、原attempt、旧测量副本、旧图纸/PDF/DXF、右门/L/R与总装都不保存。若SW要求保存任何白名单之外依赖，立即停止。本轮未建立保存的必要性，也未实施上述保存。

## 仍缺

未验证：缺 [保存后同会话关联采样、持久展开状态受控对照、PDF内部细分失败原因]。不能宣称“保存模型一定解决”或“改成毫米就能出图”。完整实体、warning、只读和健康门均不由本诊断放宽。

来源：[RESULT_Continue24_DrawingDiag_20261108.md](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/RESULT_Continue24_DrawingDiag_20261108.md>)，SHA `42F3CEF3DDF8B99BBBACA7F5FFFA3CF13B19D2DDA631EABB2A02F4944C11ABB3`；原始证据[comparison24.json](<D:/Winnsen_Structure_Agent_Studio/data/review_portal_runtime/readonly_open_repair_20261105_1920/continue24_drawing_diag_20261108/comparison24.json>)，SHA `C27288B0274DE9C9F56138195DC6AAE810F920AFB96756AFAAEEA3A5D4D30481`；本机官方CHM提取页与冻结探针均在该目录。
