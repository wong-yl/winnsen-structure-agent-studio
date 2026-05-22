# 16029 工程交付包打开前检查

用途：在结构工程师打开 SolidWorks 前，先确认 10/12/14 门原生增强装配体的依赖和结构质量门是否通过。

- Generated at: `2026-05-23T00:04:01+08:00`
- One-click check: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\CHECK_HANDOFF_READY.cmd`
- Quality CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_quality_summary.csv`
- Dependency CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_dependency_summary.csv`
- Pack-and-Go: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_PACK_AND_GO_20260522.md`
- Pack-and-Go independence: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\SOLIDWORKS_PACK_AND_GO_INDEPENDENCE_20260522.md`

## 快速结论

| 门数 | 根目录打开脚本 | 依赖 | 原生验证 | 门模块 | 右门镜像 | 门/铰链/锁 | 层板/横档 | bbox X/Z |
| ---: | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_10door_in_solidworks.cmd` | 10/10 missing=0 | PASS 31/31 | 10 (5/5) | PASS bbox_d=0.0mm | weld 10; panel 10; hinge 10; lock 10 | shelf 8; crossbar 8 | 1000.0/550.0 mm |
| 12 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_12door_in_solidworks.cmd` | 10/10 missing=0 | PASS 31/31 | 12 (6/6) | PASS bbox_d=0.0mm | weld 12; panel 12; hinge 12; lock 12 | shelf 10; crossbar 10 | 1000.0/550.0 mm |
| 14 | `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\open_14door_in_solidworks.cmd` | 10/10 missing=0 | PASS 31/31 | 14 (7/7) | PASS bbox_d=0.0mm | weld 14; panel 14; hinge 14; lock 14 | shelf 12; crossbar 12 | 1000.0/550.0 mm |

## 当前工程边界

- 这些文件是工程参考模型，用于结构规则复核、门数变化对比和方案沟通。
- 当前可复核范围是 16029 外形 1000W x 1917H x 550D 的 10/12/14 门。
- 依赖清单为当前工作站路径校验，不等同于独立 Pack-and-Go 包。
- 如果要把模型移到其他 Windows 工作站，优先使用每个门数目录下的 `solidworks_pack_and_go` 文件夹。
- 工程图、BOM、DXF、钣金展开仍属于下一阶段工程化输出。
