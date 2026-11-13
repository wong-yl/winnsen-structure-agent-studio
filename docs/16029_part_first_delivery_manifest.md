# 16029 散件先行交付物收口 Manifest

生成时间：2026-06-19T04:58:04.023Z

## 总状态

- 状态：`MANAGED_READY_FOR_NEXT_ENGINEERING_GOAL`
- 路线：part-first scattered SW2020 sheet-metal pilot
- 当前工程结论：`TWO_PAIRS_AABB_SEPARATED_DOOR_FRONT_REQUIRES_EXACT_INTERFERENCE_OR_DATUM_GAP_REVIEW`
- 代码管理状态：NOT_STAGED_BY_CODEX，NOT_COMMITTED_BY_CODEX

## Gate 汇总

| Gate | Status | Checks | Failed | Engineering status |
|---|---:|---:|---:|---|
| pilot_default | PASS | 158 |  |  |
| pilot_strict | PASS | 158 |  |  |
| route_playbook | PASS | 57 | 0 |  |
| three_part_interface | PASS | 50 | 0 | NOT_CLOSED_REQUIRES_SW2020_NATIVE_RERUN |
| native_rerun | PASS | 36 | 0 | BLOCKED_BY_DOOR_FRONT_AABB_REQUIRES_EXACT_INTERFERENCE_OR_GAP_REVIEW |

## 交付物分组

| Group | Existing |
|---|---:|
| core_scripts | 11/11 |
| core_machine_reports | 11/11 |
| human_reports | 4/4 |
| solidworks_evidence | 14/14 |

## 仍未关闭的工程项

- `door_panel_2_12_right_v43_vs_front_frame_vertical_left_v43:aabb_overlap`：OPEN_REQUIRES_EXACT_INTERFERENCE_OR_ENGINEERING_GAP_REVIEW
- `supplier_dxf_release`：BLOCKED_NOT_READY

## Release 边界

- BOM：`pilot_only_not_released`
- supplier release：`supplier_release_excluded`
- supplier precheck：`BLOCKED`
- 不允许：supplier DXF、完整柜体、released BOM。

## Git 管理建议

Do not stage all dirty files. Stage only reviewed core scripts/reports after user approval.

建议先 review/stage：
1. core_scripts
2. core_machine_reports
3. human_reports
4. CAD 生成物只在仓库策略允许时纳入

暂不建议直接 stage all：`tmp/`, `tmp_chm/`, `older probe files not referenced by this manifest`, `unrelated modified files that predate this closeout`

## 下一目标

Run exact SolidWorks interference or engineering datum/gap review for door_panel_2_12_right_v43 vs front_frame_vertical_left_v43 only.
