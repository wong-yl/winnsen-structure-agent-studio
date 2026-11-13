# SolidWorks 宏手动执行指南

## 问题总结
通过脚本自动运行宏失败（类型不匹配错误），需要手动在 SolidWorks 中创建和运行宏。

## 方案：手动创建宏并运行

### 步骤 1：打开 SolidWorks 宏编辑器
1. 在 SolidWorks 中，点击：**工具 > 宏 > 新建**
2. 保存位置：`D:\Winnsen_Structure_Agent_Studio\scripts\BatchExportR59.swp`
3. 模块名：`BatchExportR59`

### 步骤 2：复制宏代码
将以下文件的全部内容复制到宏编辑器：
```
D:\Winnsen_Structure_Agent_Studio\scripts\BatchExportFlatPattern.bas
```

具体操作：
1. 用记事本或 VS Code 打开 `BatchExportFlatPattern.bas`
2. 全选（Ctrl+A），复制（Ctrl+C）
3. 切换到 SolidWorks 宏编辑器
4. 粘贴（Ctrl+V）
5. 保存（Ctrl+S）

### 步骤 3：运行宏
1. 在宏编辑器中，点击工具栏的 **▶ 运行**按钮（或按 F5）
2. 应该会弹出对话框：
   ```
   找到 33 个SLDDRW文件
   
   将导出到:
   DXF: D:\参数化试验出图1\...\2.钣金展开图\DXF
   PDF: D:\参数化试验出图1\...\2.钣金展开图\PDF
   
   预计时间: 66 分钟
   
   开始处理？
   ```
3. 点击**确定**开始批量导出

### 预期结果
- 宏会逐个打开 33 个 SLDDRW 文件
- 每个文件导出 DXF 和 PDF 到对应目录
- 完成后弹出统计信息
- 自动打开输出文件夹

### 输出位置
- DXF: `D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\2.钣金展开图\DXF\`
- PDF: `D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\2.钣金展开图\PDF\`
- 日志: `D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\展开图导出日志_宏.txt`

### 替代方案：逐个导出
如果宏还是有问题，可以手动逐个导出（参见下一部分）。

---

## 为什么脚本自动化失败？

1. **`.swb` 格式不被 RunMacro2 API 支持**
   - `.swb` 是文本格式，不是 SolidWorks 的原生宏格式
   
2. **`.swp` + `.bas` 组合也失败**
   - 可能需要完整的 VBA 项目结构
   - 或者 SolidWorks 版本的 API 限制

3. **最可靠的方式：在 SolidWorks UI 中手动创建和运行宏**

---

## 下一步

如果手动创建宏成功运行，我们可以：
1. 将其保存为标准宏文件供将来使用
2. 考虑用 Python + win32com 直接操作 SolidWorks API（绕过宏系统）
