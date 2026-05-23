# 16029 已验证门数变化规则包

- Generated at: `2026-05-23T19:49:23+08:00`
- Status: `PASS`
- Scope: `1000W x 1917H x 550D; currently verified for 10/12/14 doors only.`

## 一句话结论

10/12/14 门已经形成同外形 16029 柜体的已验证规则包；后续生成器必须按这里的门高、门距、左右镜像、层板/横隔板、锁具/合页数量规则生成，8/16 门暂时只能作为公式候选，不能交给工程师复核。

## 已验证规则

| 门数 | 每列 | 门高 | 门距 | 门宽 | 门模块 | 层板 | 横隔板 | 锁/钩/合页 | 右门镜像 | Pack-and-Go | 外部引用 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: |
| 10 | 5 | 359.0 mm | 366.0 mm | 437.0 mm | 10 | 8 | 8 | 10 | yes; bbox_d=0.0mm | True | 0 |
| 12 | 6 | 298.0 mm | 305.0 mm | 437.0 mm | 12 | 10 | 10 | 12 | yes; bbox_d=0.0mm | True | 0 |
| 14 | 7 | 254.429 mm | 261.429 mm | 437.0 mm | 14 | 12 | 12 | 14 | yes; bbox_d=0.0mm | True | 0 |

## 固化给生成器的约束

- `rows_per_column = door_count / 2`，当前仅允许 10/12/14 门进入工程交接。
- `door_height = (1827 - 2 - 2 - (rows_per_column - 1) * 7) / rows_per_column`。
- `door_pitch = door_height + 7`，层板和门框横隔板必须跟随同一 pitch。
- 层板数和门框横隔板数必须等于 `(rows_per_column - 1) * 2`。
- 门模块、门板特征、合页销、锁钩垫、电控 U 型锁钩数量必须等于 `door_count`。
- 右侧门列必须使用 180deg Z 旋转 + source-origin Y compensation，且左右门导出 bbox Y delta <= 0.1mm。
- Pack-and-Go 交接包必须外部顶层引用为 0 才能交给工程师。

## 暂不放开的公式候选

| 门数 | 每列 | 门高 | 门距 | 状态 |
| ---: | ---: | ---: | ---: | --- |
| 8 | 4 | 450.5 mm | 457.5 mm | 未通过 CAD 验证，不能工程交接 |
| 16 | 8 | 221.75 mm | 228.75 mm | 未通过 CAD 验证，不能工程交接 |

## 证据文件

- base_rule_packet: `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_variant_rule_packet.json`
- solidworks_native_skeleton_gate: `D:\Winnsen_Structure_Agent_Studio\data\solidworks_16029_native_skeleton_geometry_gate.json`
- handoff_quality_summary: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\handoff_quality_summary.csv`
- pack_and_go_summary: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_summary.csv`
- pack_and_go_independence_summary: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\solidworks_pack_and_go_independence_summary.csv`
