# 16029 组件角色与变体规则表

- Generated at: `2026-05-18T23:36:28.390684+00:00`
- Quality gate: `step_role_binding_available_needs_formula_derivation`
- Purpose: 给后续门数变体生成器使用，不作为生产图纸放行。

## SolidWorks 阵列种子

| Feature | Instances | Spacing mm |
| --- | ---: | ---: |
| 层板阵列 | 11 | 152.5 |
| 柜门阵列 | 6 | 305.0 |
| 衣架钢管整列 | 2 | 915.0 |

## STEP 角色数量

| Role | Count |
| --- | ---: |
| base_or_leveling | 11 |
| cabinet_body | 16 |
| door_frame | 17 |
| door_module | 9 |
| electronics_or_power | 5 |
| hanger_rail | 12 |
| hinge_or_pivot | 14 |
| lock_system | 28 |
| reinforcement | 5 |
| shelf_or_partition | 46 |
| top_cover | 6 |
| unclassified | 8 |

## 重复角色组

| Role | Subrole | BBox mm | Count | Samples |
| --- | --- | ---: | ---: | --- |
| shelf_or_partition | shelf_panel | 433.2 x 25.0 x 527.2 | 20 | 箱体横层板L, 箱体横层板L焊接, 箱体横层板L, 箱体横层板L焊接 |
| shelf_or_partition | shelf_reinforcement | 429.0 x 11.0 x 53.4 | 20 | 箱体横层板加强筋, 箱体横层板加强筋, 箱体横层板加强筋, 箱体横层板加强筋 |
| door_frame | horizontal_divider | 445.0 x 15.0 x 19.7 | 10 | 门框 横隔板, 门框 横隔板, 门框 横隔板, 门框 横隔板 |
| cabinet_body | side_panel_reinforcement | 4.8 x 1805.8 x 50.0 | 8 | 箱体侧板加强筋1, 箱体侧板加强筋, 箱体侧板加强筋, 箱体侧板加强筋 |
| hanger_rail | hanger_or_bracket | 20.0 x 30.0 x 88.4 | 8 | 衣杆固定支架, 衣杆固定支架, 衣杆固定支架, 衣杆固定支架 |
| base_or_leveling | base_leveling_part | 20.8 x 10.8 x 20.8 | 4 | 螺母M12, 螺母M001, 螺母M002, 螺母M003 |
| base_or_leveling | base_leveling_part | 50.0 x 79.0 x 50.0 | 4 | 调整脚 M12X60(模型), 调整脚 M12X60(模型), 调整脚 M12X60(模型), 调整脚 M12X60(模型) |
| hanger_rail | hanger_or_bracket | 430.0 x 16.0 x 16.0 | 4 | 衣架钢管, 衣架钢管, 衣架钢管, 衣架钢管 |
| hinge_or_pivot | pin_bushing_retainer | 13.0 x 7.0 x 13.0 | 4 | 塑料轴套(云绅模具), 塑料轴套(云绅模具), 塑料轴套(云绅模具), 塑料轴套(云绅模具) |
| hinge_or_pivot | pin_bushing_retainer | 10.0 x 0.8 x 8.6 | 4 | 开口挡圈 5, 开口挡圈, 开口挡圈, 开口挡圈 |
| hinge_or_pivot | pin_bushing_retainer | 6.0 x 60.0 x 6.0 | 4 | 门轴销(短), 门轴销(短), 门轴销(短), 门轴销(短) |
| lock_system | lock_or_latch | 14.0 x 80.8 x 80.8 | 4 | 电控锁ZJA-S500, 电控锁ZJA-S502, 电控锁ZJA-S503, 电控锁ZJA-S505 |
| lock_system | lock_or_latch | 18.0 x 28.0 x 13.2 | 4 | 插销固定板, 插销固定板, 插销固定板, 插销固定板 |
| cabinet_body | side_panel | 514.7 x 1834.4 x 530.5 | 2 | 箱体左侧板, 箱体左侧板焊接 |
| cabinet_body | side_panel | 499.5 x 1834.4 x 530.5 | 2 | 箱体右侧板, 箱体右侧板焊接 |
| cabinet_body | side_panel_reinforcement | 34.2 x 1807.4 x 24.0 | 2 | 箱体侧板加强筋2, 箱体侧板加强筋 |
| cabinet_body | side_panel_reinforcement | 50.0 x 1805.8 x 4.8 | 2 | 箱体侧板加强筋, 箱体侧板加强筋 |
| door_frame | frame_part | 20.0 x 1917.0 x 45.0 | 2 | 门框 左, 门框 右 |

## SolidWorks 引用特征角色

| Role | Count | Samples |
| --- | ---: | --- |
| base_or_leveling | 5 | 底座焊接-1, 调整脚 M12X60(模型)-1, 调整脚 M12X60(模型)-2, 调整脚 M12X60(模型)-3, 调整脚 M12X60(模型)-4 |
| cabinet_body | 3 | 标准寄存柜 模型-1, 箱体左侧板焊接-1, 箱体右侧板焊接-1 |
| door_frame | 1 | 门框焊接-1 |
| door_module | 20 | 储物柜门1╱12装配-1, 储物柜门1╱12装配-2, 储物柜门2╱12装配-3, 储物柜门2╱12装配-2, 储物柜门3╱12装配-1 |
| electronics_or_power | 2 | 锁控板装配组件-1, 电源插座(滤波器)-1 |
| hanger_rail | 4 | 衣架钢管-1, 衣架钢管-2, 衣架钢管-3, 衣架钢管-4 |
| hinge_or_pivot | 4 | 门轴销(短)-1, 门轴销(短)-2, 门轴销(短)-3, 门轴销(短)-4 |
| lock_system | 13 | 电控锁ZJA-S500-1, 电控锁ZJA-S500-7, 电控锁ZJA-S500-2, 电控锁ZJA-S500-3, 电控锁ZJA-S500-4 |
| service_rear_door | 1 | 应急维护门焊接-1 |
| shelf_or_partition | 16 | 箱体竖隔板L焊接-1, 箱体竖隔板R焊接-1, 箱体横层板L焊接-1, 箱体横层板R焊接-1, 箱体横层板L焊接-3 |
| top_cover | 2 | 上盖焊接-1, 顶棚组件-1 |

## 距离配合种子

| Mate | Dimension | Value mm |
| --- | --- | ---: |
| 距离6 | D1@距离6 | 0.5 |
| 距离11 | D1@距离11 | 2.0 |
| 距离15 | D1@距离15 | 2.0 |
| 距离16 | D1@距离16 | 2.0 |
| 距离4 | D1@距离4 | 2.0 |
| 距离7 | D1@距离7 | 2.0 |
| 距离8 | D1@距离8 | 2.0 |
| 距离9 | D1@距离9 | 2.0 |
| 距离17 | D1@距离17 | 60.0 |

## 变体放行顺序

| Target | Status | Reason |
| --- | --- | --- |
| 12-door | usable_baseline | Standard top assembly clone exists and opens as SolidWorks-native baseline. |
| 10-door first mutator | template_clone_available_mutator_pending | Practice source and 2/10 recipe exist: 5 rows per column, 366mm door pitch, 359mm door panel height, 10 locks, 5 shelves/dividers per side. |
| 14/16/18+ doors | blocked_until_10door_passes | Adding rows/components is higher risk than suppressing/remapping from 12-door; wait for one successful transform/mate mutator. |

## 阻塞原因

- SolidWorks component transforms are not exposed by the current API extraction.
- STEP role bboxes are usable evidence, but must be bound into editable SolidWorks placement operations.
- Door frame divider, lock, hinge, shelf, and BOM/DXF formulas must close before enabling arbitrary door size changes.
