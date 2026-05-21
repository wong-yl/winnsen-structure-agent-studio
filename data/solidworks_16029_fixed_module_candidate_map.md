# 16029 Fixed Module Candidate Map

- Generated at: `2026-05-21T13:10:11+08:00`
- Source verify CSV: `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\locker_16029_12door_rule_driven_verify.csv`
- Transform-backed candidates: `9`
- Native-placeable candidates: `9`
- Identity-transform-ready candidates: `3`
- Requires rotation/full-matrix candidates: `6`
- Recommended first enriched model candidates: `3`
- Blocked local-only candidates: `3`

## Recommended First Enriched 12-Door Model

| role | module | tx | ty | tz | native | bbox evidence |
|---|---:|---:|---:|---:|---|---|
| maintenance_door_lock_hole | maintenance_door | 0.0 | 1751.5 | -0.8 | yes | transform + local bbox, max error 0.0 mm |
| maintenance_door_panel | maintenance_door | 0.0 | 943.0 | -16.0 | yes | transform + local bbox, max error 0.0 mm |
| lock_control_board_24ch | lock_system | 295.0 | 1872.6 | -179.0 | yes | transform + local bbox, max error 0.0 mm |

## Not Ready For Translation-Only Placement

| role | module | strategy | max bbox error mm |
|---|---|---|---:|
| auto_lock_latch | latch_system | requires_rotation_or_full_matrix | 61.0 |
| push_latch_mount_plate | latch_system | requires_rotation_or_full_matrix | 34.2 |
| electronics_board_bracket | lock_system | requires_rotation_or_full_matrix | 10.0 |
| m9_v11_board | lock_system | requires_rotation_or_full_matrix | 15.5 |
| switching_power_supply_assembly | lock_system | requires_rotation_or_full_matrix | 78.0 |
| wifi_serial_server | lock_system | requires_rotation_or_full_matrix | 5.0 |

## Blocked Until More Evidence

| id | reason |
|---|---|
| rear_lower_door_assembly | Only local STEP bbox was found; no transform-backed assembly placement row was found in the 12-door verification CSV. |
| rear_lower_door_weld | Only local STEP bbox was found; no transform-backed assembly placement row was found in the 12-door verification CSV. |
| clothes_rail | Only local STEP bbox was found; no transform-backed assembly placement row was found in the 12-door verification CSV. |

## Rule

Only transform-backed rows should enter the SolidWorks generator. Local STEP bbox can describe part size, but it is not enough to place the part in the cabinet.
