# 16029 工程师打开清单

用途：给结构工程师快速打开 10/12/14 门工程参考模型。这里的模型用于复核结构规则和方案，不是正式生产图纸、BOM 或 DXF。

## 推荐操作

1. 先打开对应门数的根目录脚本，例如 `open_12door_in_solidworks.cmd`。
2. 脚本会先启动 SolidWorks 主程序，再尝试 API 打开已审计 `.stp` 交接文件。
3. 如果 API 打开未确认，脚本会在资源管理器中选中 STEP 文件，由工程师在 SolidWorks 里手动 File > Open。
4. 只在需要看 FreeCAD 原生参考时，再进入对应门数目录打开 `.FCStd`。
5. 不要再使用历史 direct assembly 逐零件装配任务；该路线已因装配基准/transform 错乱停用。

## 可打开模型

| 门数 | SolidWorks 安全脚本 | STP 交接文件 | 门高 | 门距 | 质量结论 |
| ---: | --- | --- | ---: | ---: | --- |
| 10 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_10door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\16029_1000W_1917H_550D_10door_solidworks_import.stp` | 359.0 | 366.0 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |
| 12 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_12door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\16029_1000W_1917H_550D_12door_solidworks_import.stp` | 298.0 | 305.0 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |
| 14 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_14door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_solidworks_import.stp` | 254.429 | 261.429 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |

## 软件实测状态

- 已做 SolidWorks 2025 受控打开验证：主程序可见启动通过，但 12 门 STP 自动导入未确认成功。
- 结论：STEP/FCStd 几何质量可作为工程参考，SolidWorks API/一键可视化打开仍按阻塞项跟踪。
- 验证记录：`D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_OPEN_VERIFICATION_20260520.md`


## 交付边界

- 当前可靠范围：16029 外形 1000W x 1917H x 550D，10/12/14 门。
- 当前模型类型：工程参考模型，适合方案复核、规则验证、结构对比。
- 未完成项：正式 SolidWorks 可编辑装配树、工程图、BOM、DXF、钣金展开仍需后续工程化。
