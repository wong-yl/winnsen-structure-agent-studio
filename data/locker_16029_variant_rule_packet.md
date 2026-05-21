# 16029 10/12/14 门规则包

- Generated at: `2026-05-21T10:41:30+08:00`
- Source evidence: `D:\Winnsen_Structure_Agent_Studio\data\sheetmetal_rule_evidence_16029.json`
- FreeCAD counts: `[10, 12, 14]`
- SolidWorks native skeleton counts: `[10, 12, 14]`

| 门数 | 每列排数 | 门高 | 门展开估算 | 门模块 | 层板 | 门框横隔板 | FreeCAD | SolidWorks |
| ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | --- |
| 10 | 5 | 359.0 mm | 475.4 x 395.4 mm | 10 | 8 | 8 | enabled | enabled |
| 12 | 6 | 298.0 mm | 475.4 x 334.4 mm | 12 | 10 | 10 | enabled | enabled |
| 14 | 7 | 254.429 mm | 475.4 x 290.829 mm | 14 | 12 | 12 | enabled | enabled |

## 质量门槛

- bbox X must remain 1000mm for the standard-width route
- door_modules, lock_holes and lock_hooks must equal door_count
- shelves and door_frame_horizontal_dividers must equal (rows_per_column - 1) x 2
- invalid_shape_count must be 0 before promoting to SolidWorks handoff

## 说明

- This packet is a generator input, not a production drawing release.
- 10/12/14 are the convergence targets; the system is not generating all door-count inventory variants.
- FreeCAD remains the rule-validation route; SolidWorks native skeleton v2 is the current engineering reference route for 10/12/14.
