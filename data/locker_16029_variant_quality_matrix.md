# 16029 10/12/14 门生成质量矩阵

- Generated at: `2026-05-20T17:12:10+08:00`
- Scope: `1000W x 1917H x 550D FreeCAD engineering-reference variants`
- Rule packet: `D:\Winnsen_Structure_Agent_Studio\data\locker_16029_variant_rule_packet.json`

| 门数 | 状态 | bbox X | 门模块 | 锁孔 | 锁钩 | 层板 | 门框横隔板 | 结构规则 | STEP 几何 | FCStd 完整性 | 输出目录 |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |
| 10 | PASS_READY_FOR_ENGINEERING_REVIEW | 1000.0 | 10/10 PASS | 10/10 PASS | 10/10 PASS | 8/8 PASS | 8/8 PASS | PASS | geometry_check_pass | PASS | `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\FREECAD-16029-10DOOR-VARIANT-20260519` |
| 12 | PASS_READY_FOR_ENGINEERING_REVIEW | 1000.0 | 12/12 PASS | 12/12 PASS | 12/12 PASS | 10/10 PASS | 10/10 PASS | PASS | geometry_check_pass | PASS | `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260520042611-E8E6F4` |
| 14 | PASS_READY_FOR_ENGINEERING_REVIEW | 1000.0 | 14/14 PASS | 14/14 PASS | 14/14 PASS | 12/12 PASS | 12/12 PASS | PASS | geometry_check_pass | PASS | `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260517142633-D1E0AB` |

## 说明

- This matrix gates engineering-reference model quality; it is not a production drawing release.
- structural_rule_audit PASS verifies door grid, lock relation, shelf/frame offsets, L/R symmetry, and bbox X from verify.csv evidence.
- PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT means verify.csv counts and exported STEP geometry pass, but FCStd shape integrity has not yet passed.
- PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT means verify.csv counts and bbox pass, but FCStd shape integrity has not yet passed.
