# 16029 工程师打开清单

用途：给结构工程师快速打开 10/12/14 门工程参考模型。这里的模型用于复核结构规则和方案，不是正式生产图纸、BOM 或 DXF。

## 推荐操作

1. 先打开对应门数的根目录脚本，例如 `open_12door_in_solidworks.cmd`。
2. 脚本会优先打开原生 SolidWorks 增强样机 `.SLDASM`，这是当前给结构工程师复核的主文件。
3. 如果 API 打开未确认，脚本会在资源管理器中选中原生装配体，由工程师在 SolidWorks 里手动 File > Open。
4. 只有需要中性格式复核时，再进入对应门数目录打开 `.stp`；需要看 FreeCAD 参考时再打开 `.FCStd`。
5. 不要再使用历史 direct assembly 逐零件装配任务；该路线已因装配基准 transform 错乱停用。

## 可打开模型

| 门数 | SolidWorks 安全脚本 | 原生增强装配 | STP 备用 | 门高 mm | 门距 mm | 质量结论 |
| ---: | --- | --- | --- | ---: | ---: | --- |
| 10 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_10door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_native\16029_1000W_1917H_550D_10door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\16029_1000W_1917H_550D_10door_solidworks_import.stp` | 359.0 | 366.0 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |
| 12 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_12door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\solidworks_native\16029_1000W_1917H_550D_12door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\16029_1000W_1917H_550D_12door_solidworks_import.stp` | 298.0 | 305.0 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |
| 14 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_14door_in_solidworks.cmd` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_native\16029_1000W_1917H_550D_14door_enriched_v2.SLDASM` | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\16029_1000W_1917H_550D_14door_solidworks_import.stp` | 254.429 | 261.429 | STEP geometry_check_pass; FCStd PASS; 结构 PASS |

## 软件实测状态

- 已做 SolidWorks 2025 受控打开验证：主程序可启动，原生 SLDASM 是当前优先交付通道。
- STEP/FCStd 作为中性格式和开源复核备选，不作为当前 SolidWorks 主交付入口。
- 验证记录：`D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_OPEN_VERIFICATION_20260520.md`
- 原生验证记录：`D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_NATIVE_OPEN_VERIFICATION_20260521.md`

## 交付边界

- 当前可靠范围：16029 外形 1000W x 1917H x 550D，10/12/14 门。
- 当前模型类型：工程参考模型，适合方案复核、规则验证、结构对比。
- 未完成项：正式 SolidWorks 可编辑装配树、工程图、BOM、DXF、钣金展开仍需后续工程化。
