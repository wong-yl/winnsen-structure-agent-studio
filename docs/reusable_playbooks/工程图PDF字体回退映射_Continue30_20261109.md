# 工程图PDF字体回退映射：Continue30只读诊断

适用：原生PDF返回swGenericSaveError并提示Arial Unicode MS安装不正确，但图纸显式字体没有该字体。本样本PDF仍FAIL；以下是诊断与停止流程，不是成功修复方法。

## 冻结入口

先读PROJECT_GOAL_FINAL及前轮RESULT，核对输入SLDDRW/引用SLDPRT、失败日志SHA/SID与保护基线。输出放新D盘目录；独立空CAD会话核对PID/启动时间/EXE。OpenDoc6 options=3后读errors/warnings、actualReadOnly、GetSaveFlag、依赖范围。不要在Drawing上读取零件ActiveConfiguration。

## 五层证据不能跳级

1. 原文字：逐视图Annotation→Note.GetText、PropertyLinkedText，保留逐字符码点、中文、°、×、CR/LF、全角及不可见字符。
2. 显式格式：GetTextFormatCount、逐段GetTextFormat/GetUseDocTextFormat、Note.HasMultipleFonts、分段字体、Compound和可支持的富文本字段。null和异常原样保留。
3. 显示数据：Annotation/DisplayDimension.GetDisplayData→IDisplayData.GetTextAtIndex/GetTextFontAtIndex，补充旧Note getter的null；仍不是PDF后端实际字体选择。
4. 字形覆盖：独立GDI上下文，GetTextFaceW确认实际字体，GetGlyphIndicesW(flags=1, GGI_MARK_NONEXISTING_GLYPHS)记录字符索引。65535说明直接缺字，不证明PDF用了哪个替代字体。FontLink/FontSubstitutes只读查表亦不能代替PDF记录。
5. PDF回退与异常：必须有失败SID、具体Annotation/字符、实际回退字体及异常子码的原始归属，才能将缺字候选连到告警和输出缺轮廓。前四层不自动补出第五层。

## 本机API陷阱

- 本机SW2020帮助：INote.GetTextAtIndex从1开始，GetTextFontAtIndex说明从0开始，IDisplayData文本/字体从0开始，分别记录下标。
- 旧GetTextFontAtIndex(0)会null；ITextFormat、IDisplayData仍可能有字体，不断言“没有字体”。
- GetCompoundTextAtIndex对2016后文档已标过时；GetTextFormatAtIndex由Annotation接口替代。读取失败不能伪造为空。
- GetTextTokens存在于interop不代表实际可读。Continue30获得外层TargetInvocationException 0x80131604，缺内层详情，保留“未验证”，没有再开CAD填空。
- 文档级GetAnnotationCount=0不代表图纸无注释；本样本视图17项、14条Note。尺寸还可经Annotation出现，避免重复计数。
- 文档GetUserPreferenceTextFormat的pref/option、null和所有字体值一起记录。70组合查询不是70个使用对象。未使用默认字体不能当根因。
- 方法检索不要用裸rtf不区分大小写子串，GetDimXpertFeature/StartFormatPainter会误中。核对完整API含义；本轮离线v2清单已纠正。

## Continue30实证

12条折弯Note的PropertyLinkedText为<bend-direction> <bend-angle> R <bend-radius>，显示“上 90° R 0.2”；显式/显示字体Century Gothic，U+4E0A直接字形65535。Microsoft YaHei实际选择微软雅黑，U+4E0A=1031；°、×、507.65均有字形。折弯注释1/2/12用文档默认，3—11自有格式；改默认不能自动代表所有注释改变。

备注雅黑所用138种非控制字符无缺字，尺寸Century Gothic数字无缺字。14条Note HasMultipleFonts=false，已读格式无Arial Unicode MS。Continue29同SID消息点名字体，RAWSTACK含AdobePDFL/sldpdfu，却没有对象、字符或异常子码。实际PDF回退映射未取得，字体setter=0、Save=0、PDF=0、修法=0。

## 缺证停止与支持清单

无法连到第五层时，写“未验证：缺 [具体字符/Annotation到PDF实际字体回退的记录]”，停止。不全图换字体、不装字体、不删注释、不改视图、不换API或打印机。只有明确映射和当前授权同时成立，才在新图纸副本做一项最小对照，零件不得保存；新图纸仍须独立只读复核关联。

支持清单至少列：精确CAD输入、失败PDF及解析/预览、SaveAs/GetErrorMessages/保存通知、PID启动时间/SID归属、冻结性能和FileAccess日志、旧journal/无事件的限制、字符/格式/显示数据/字形证据、每项SHA、原生复现步骤、缺失的异常子码和字体选择记录。整理清单不代表已外发工单。

CloseAllDocuments(true)丢弃内存改动、ExitApp自然退出；只在父CAD退出且归属明确后清自己孤儿monitor。前后输入/保护SHA及属性不变、最终SLDWORKS=0。工程图通过仍要求原生PDF errors=0且轮廓与毫米尺寸同时存在。

证据目录：D:\Winnsen_Structure_Agent_Studio\data\review_portal_runtime\readonly_open_repair_20261105_1920\continue30_font_trace_20261109。核心：A_trace30/raw_font_trace30.json、comparison30.json、font_coverage30.json、glyph_crosscheck30.json、api_surface30_v2.json、SUPPORT_PACKAGE30.md、support_manifest30.json、integrity_after30.json、session30.json。一轮探针留作证据，不推广成生产工具。
