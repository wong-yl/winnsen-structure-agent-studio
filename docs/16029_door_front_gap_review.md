# 16029 门板-前框 Gap / Blocker 定位包

生成时间：2026-06-19T05:15:19.789Z

## 结论

- 状态：`BLOCKER_LOCALIZED_GAP_REVIEW_READY_NOT_CAD_CHANGED`
- 当前 X 向间隙：`-10.15 mm`
- 当前 X 向重叠：`10.15 mm`
- 目标门缝：`7 mm`
- 结论：当前 placement 下前框右边界进入门板 bbox，不能用 AABB 放行；需要 exact interference 或工程 datum/gap 复核。

## 当前 BBox 读数

| Item | tx mm | xmin | xmax | xlen |
|---|---:|---:|---:|---:|
| Door | 0 | -153.5000000000001 | 153.5000000000001 | 307.0000000000002 |
| Front frame | -168 | -192.65 | -143.35 | 49.30000000000001 |

## 候选偏移

固定门板，移动前框：
- 0mm 接触：front tx = `-178.15 mm`，相对当前 shift = `-10.15 mm`
- 7mm 目标门缝：front tx = `-185.15 mm`，相对当前 shift = `-17.15 mm`

固定前框，移动门板：
- 0mm 接触：door tx = `10.15 mm`，相对当前 shift = `10.15 mm`
- 7mm 目标门缝：door tx = `17.15 mm`，相对当前 shift = `17.15 mm`

## 历史参考

- historical envelope gap：`8.284 mm`
- 用途：Reference only. The old native-envelope run had a positive gap, but it used envelope geometry and cannot release the current formal parts.

## 下一步检查

1. Confirm which datum is fixed: door module datum or front-frame vertical datum.
2. If door is fixed, test front-frame tx=-185.15 mm for the 7 mm target gap.
3. Rerun SW2020 native component enumeration and exact interference, not only AABB.
4. Only after exact check passes should the interface gate be upgraded from blocker to closed.

## 边界

- 本报告没有修改 CAD：`false`
- supplier release：`supplier_release_excluded`
- supplier precheck：`BLOCKED`
- 不进入完整柜体，不进入供应商 DXF release。
