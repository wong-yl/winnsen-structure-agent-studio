# 规则种子数量公式候选

- Generated at: `2026-05-18T01:22:44.627134Z`
- Source: `D:\Winnsen_Structure_Agent_Studio\data\rule_seed_evidence_checklist.json`
- Items: `6`

这里不放行生产模型，只记录 BOM 行级数量能推导出的门数/层板数量公式候选。

## SEED-014-SHELF_PITCH 层板节距 `11 x 152.5mm`

- Formula status: `quantity_formula_partial`
- Summary: BOM 支持每列 5 块横层板、推导 6 行门格；SolidWorks 的 11 个阵列实例还需要继续绑定特征角色。

- BOM: `16029 1917X1000X550标准柜BOM表.xlsx`
  - Status: `quantity_formula_partial`
  - Derived: `{"patternInstanceCount": 11, "pitchMm": 152.5, "shelfPanelsPerColumn": 5, "inferredDoorRows": 6, "totalShelfPanels": 10}`
  - PASS: shelf_panels_per_column_consistent actual=5 expected=L/R 横层板数量一致
  - PASS: shelf_rows_inferred_from_panels actual=6 expected=横层板数量 + 1
  - row 22: 箱体横层板L焊接 qty=5
  - row 23: 箱体横层板L qty=5
  - row 24: 箱体横层板加强筋 qty=20
  - row 25: 箱体横层板R焊接 qty=5
  - row 26: 箱体横层板R qty=5
  - row 27: 箱体横层板加强筋 qty=20
- BOM: `16028 标准寄存柜BOM表.xlsx`
  - Status: `quantity_formula_partial`
  - Derived: `{"patternInstanceCount": 11, "pitchMm": 152.5, "shelfPanelsPerColumn": 5, "inferredDoorRows": 6, "totalShelfPanels": 10}`
  - PASS: shelf_panels_per_column_consistent actual=5 expected=L/R 横层板数量一致
  - PASS: shelf_rows_inferred_from_panels actual=6 expected=横层板数量 + 1
  - row 18: 箱体横层板L焊接 qty=5
  - row 19: 箱体横层板L qty=5
  - row 20: 箱体横层板加强筋 qty=20
  - row 21: 箱体横层板R焊接 qty=5
  - row 22: 箱体横层板R qty=5
  - row 23: 箱体横层板加强筋 qty=20

## SEED-006-DOOR_COLUMN_PITCH 柜门列距/阵列 `6 x 305mm`

- Formula status: `formula_consistent_candidate`
- Summary: 门框横隔板数量推导 6 行，BOM 门名分母推导 12 门；可形成 2 列 x 6 行的阵列公式候选。

- BOM: `16029 1917X1000X550标准柜BOM表.xlsx`
  - Status: `formula_consistent_candidate`
  - Derived: `{"patternInstanceCount": 6, "pitchMm": 305.0, "horizontalDividersPerColumn": 5, "inferredDoorRows": 6, "doorTotalFromBomName": 12, "inferredColumns": 2}`
  - PASS: horizontal_dividers_infer_rows actual=6 expected=横隔板数量 + 1
  - PASS: pattern_count_matches_inferred_rows actual=6 expected=6
  - PASS: door_total_divisible_by_rows actual=2 expected=总门数 / 行数 = 整数列
  - row 45: 门框 横隔板 qty=5
  - row 46: 门框 横隔板R qty=5
  - row 50: 储物柜门1╱12装配 qty=2
  - row 51: 储物柜门1╱12焊接 qty=2
  - row 52: 储物柜门板1╱12 qty=2
  - row 59: 储物柜门2╱12装配 qty=12
- BOM: `16028 标准寄存柜BOM表.xlsx`
  - Status: `formula_consistent_candidate`
  - Derived: `{"patternInstanceCount": 6, "pitchMm": 305.0, "horizontalDividersPerColumn": 5, "inferredDoorRows": 6, "doorTotalFromBomName": 12, "inferredColumns": 2}`
  - PASS: horizontal_dividers_infer_rows actual=6 expected=横隔板数量 + 1
  - PASS: pattern_count_matches_inferred_rows actual=6 expected=6
  - PASS: door_total_divisible_by_rows actual=2 expected=总门数 / 行数 = 整数列
  - row 43: 门框 横隔板 qty=5
  - row 44: 门框 横隔板R qty=5
  - row 70: 储物柜门1╱12装配 qty=2
  - row 71: 储物柜门1╱12焊接 qty=2
  - row 72: 储物柜门板1╱12 qty=2
  - row 79: 储物柜门2╱12装配 qty=12

## STEP-ROLE-RULE-20260517023937-37D2A4-DOOR-LOCK 16029 标准寄存柜 1917x1000x550 门/锁角色数量 `门装配3; 锁?; 锁钩4`

- Formula status: `step_role_formula_candidate`
- Summary: STEP 角色证据自动抽到门装配 3 组、门板 3 个、其中 3 组未归入小门/高门分类、电控锁 待定 组、U 型锁钩 4 个、插销固定板 5 个；可作为 16029 标准寄存柜 1917x1000x550 的门/锁/分隔件规则候选。

- BOM: `RULE-20260517023937-37D2A4_step_component_role_bindings.json`
  - Status: `step_role_formula_candidate`
  - Derived: `{"smallDoorAssemblies": 0, "tallDoorAssemblies": 0, "doorAssemblyRows": 3, "doorPanelRows": 3, "doorWeldmentRows": 3, "classifiedDoorAssemblies": 0, "unclassifiedDoorAssemblies": 3, "electricLockSets": null, "lockHookCount": 4, "latchPlateCount": 5, "horizontalDividers": 10, "verticalDividers": 2, "shelfPanelObjects": 20, "repeatedRoleGroups": 34}`
  - PASS: door_panel_rows_match_assembly_rows actual=3 expected=3
  - OPEN: lock_hook_matches_electric_lock_sets actual=4 expected=None
  - OPEN: latch_plates_two_per_lock_hook actual=5 expected=8
  - PASS: frame_dividers_present actual=10 horizontal / 2 vertical expected=frame divider evidence
  - OPEN: variant_formula_requires_more_templates actual=doorAssemblies=3, classified=0, unclassified=3 expected=bind same-family target door-count variants before generation

## STEP-ROLE-RULE-20260517023937-F290B0-DOOR-LOCK 16028 标准寄存柜 1917x1000x485 门/锁角色数量 `门装配12; 锁?; 锁钩24`

- Formula status: `step_role_formula_candidate`
- Summary: STEP 角色证据自动抽到门装配 12 组、门板 12 个、其中 12 组未归入小门/高门分类、电控锁 待定 组、U 型锁钩 24 个、插销固定板 25 个；可作为 16028 标准寄存柜 1917x1000x485 的门/锁/分隔件规则候选。

- BOM: `RULE-20260517023937-F290B0_step_component_role_bindings.json`
  - Status: `step_role_formula_candidate`
  - Derived: `{"smallDoorAssemblies": 0, "tallDoorAssemblies": 0, "doorAssemblyRows": 12, "doorPanelRows": 12, "doorWeldmentRows": 12, "classifiedDoorAssemblies": 0, "unclassifiedDoorAssemblies": 12, "electricLockSets": null, "lockHookCount": 24, "latchPlateCount": 25, "horizontalDividers": 10, "verticalDividers": 2, "shelfPanelObjects": 20, "repeatedRoleGroups": 30}`
  - PASS: door_panel_rows_match_assembly_rows actual=12 expected=12
  - OPEN: lock_hook_matches_electric_lock_sets actual=24 expected=None
  - OPEN: latch_plates_two_per_lock_hook actual=25 expected=48
  - PASS: frame_dividers_present actual=10 horizontal / 2 vertical expected=frame divider evidence
  - OPEN: variant_formula_requires_more_templates actual=doorAssemblies=12, classified=0, unclassified=12 expected=bind same-family target door-count variants before generation

## STEP-ROLE-RULE-20260517073616-E10EA1-DOOR-LOCK 16038 洗衣寄存柜同尺寸多门数 门/锁角色数量 `小门6+高门1; 锁7; 锁钩7`

- Formula status: `step_role_formula_candidate`
- Summary: STEP 角色证据自动抽到小门 6 组、高门 1 组、其中 1 组未归入小门/高门分类、电控锁 7 组、U 型锁钩 7 个、插销固定板 14 个；可作为 16038 洗衣寄存柜同尺寸多门数 的门/锁/分隔件规则候选。

- BOM: `RULE-20260517073616-E10EA1_step_component_role_bindings.json`
  - Status: `step_role_formula_candidate`
  - Derived: `{"smallDoorAssemblies": 6, "tallDoorAssemblies": 1, "doorAssemblyRows": 8, "doorPanelRows": 8, "doorWeldmentRows": 8, "classifiedDoorAssemblies": 7, "unclassifiedDoorAssemblies": 1, "electricLockSets": 7, "lockHookCount": 7, "latchPlateCount": 14, "horizontalDividers": 3, "verticalDividers": 3, "shelfPanelObjects": 13, "repeatedRoleGroups": 46}`
  - PASS: small_door_panel_matches_assembly actual=6 expected=6
  - PASS: lock_hook_matches_electric_lock_sets actual=7 expected=7
  - PASS: latch_plates_two_per_lock_hook actual=14 expected=14
  - PASS: frame_dividers_present actual=3 horizontal / 3 vertical expected=frame divider evidence
  - OPEN: variant_formula_requires_more_templates actual=doorAssemblies=8, classified=7, unclassified=1 expected=bind 4/7/8/12-door variants before generation

## STEP-VARIANT-16038-4-7-8-12 16038 4/7/8/12 门变体公式 `4 / 7 / 8门总装 + 12/12单门模块`

- Formula status: `variant_formula_candidate`
- Summary: 16038 STEP 已闭环 4 / 7 / 8 门总装：doorTotal=小门装配+高门装配，锁钩数=门数，插销固定板=2×门数；12/12 单门模块已有门板/装配证据。

- BOM: `16038 STEP variant role evidence`
  - Status: `variant_formula_candidate`
  - Derived: `{"closedFullAssemblyVariants": "4 / 7 / 8", "doorTotalFormula": "smallDoorAssemblies + tallDoorAssemblies", "lockHookFormula": "lockHookCount = doorTotal", "latchPlateFormula": "latchPlateCount = 2 x doorTotal", "module12DoorAssemblies": 1, "module12DoorPanels": 1}`
  - PASS: variant_door_total_matches_step actual=4 expected=4
  - PASS: variant_lock_hook_matches_door_total actual=4 expected=4
  - PASS: variant_latch_plates_two_per_door actual=8 expected=8
  - PASS: variant_door_total_matches_step actual=7 expected=7
  - PASS: variant_lock_hook_matches_door_total actual=7 expected=7
  - PASS: variant_latch_plates_two_per_door actual=14 expected=14
  - PASS: variant_door_total_matches_step actual=8 expected=8
  - PASS: variant_lock_hook_matches_door_total actual=8 expected=8
  - PASS: variant_latch_plates_two_per_door actual=16 expected=16
  - PASS: module_12door_evidence_available actual=assembly=1, panel=1 expected=12/12 door assembly and panel evidence
