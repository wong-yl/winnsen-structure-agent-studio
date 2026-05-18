# SolidWorks rule extraction comparison

- Generated at: `2026-05-18T01:17:36.857562Z`
- Source: `D:\Winnsen_Structure_Agent_Studio\workers\rule_extractions`

| Run | Template | Components | Transform | BBox | Mates | Features | Dimensions | Pattern seeds | STEP bboxes | Role bindings | Quality gate |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `RULE-20260517022242-1D60F3` | 16038 洗衣寄存柜同尺寸多门数 | 38 | 0/38 | 0/38 | 82 | 86 | 2 | 1 | 0 | 0 / 0 | `component_tree_available_needs_position_evidence` |
| `RULE-20260517023937-37D2A4` | 16029 标准寄存柜 1917x1000x550 | 38 | 0/38 | 0/38 | 75 | 98 | 6 | 3 | 208 | 177 / 79 | `step_role_binding_available_needs_formula_derivation` |
| `RULE-20260517023937-F290B0` | 16028 标准寄存柜 1917x1000x485 | 36 | 0/36 | 0/36 | 77 | 106 | 4 | 2 | 370 | 311 / 139 | `step_role_binding_available_needs_formula_derivation` |
| `RULE-20260517054623-175516` | 16038 洗衣寄存柜同尺寸多门数 | 38 | 0/38 | 0/38 | 82 | 86 | 2 | 1 | 322 | 279 / 155 | `step_role_binding_available_needs_formula_derivation` |
| `RULE-20260517073616-E10EA1` | 16038 洗衣寄存柜同尺寸多门数 | 38 | 0/38 | 0/38 | 82 | 86 | 2 | 1 | 322 | 279 / 155 | `step_role_binding_available_needs_formula_derivation` |

## Next actions

- `RULE-20260517022242-1D60F3`: SolidWorks 组件树、配合和阵列尺寸已可读；下一步补 STEP/bbox 或 transform 位置证据，再推导摆放和门数规则。
- `RULE-20260517023937-37D2A4`: STEP 角色绑定证据已可用；下一步抽取门板、层板、锁具、铰链和分隔件公式，再用 BOM/DXF 做数量闭环。
- `RULE-20260517023937-F290B0`: STEP 角色绑定证据已可用；下一步抽取门板、层板、锁具、铰链和分隔件公式，再用 BOM/DXF 做数量闭环。
- `RULE-20260517054623-175516`: STEP 角色绑定证据已可用；下一步抽取门板、层板、锁具、铰链和分隔件公式，再用 BOM/DXF 做数量闭环。
- `RULE-20260517073616-E10EA1`: STEP 角色绑定证据已可用；下一步抽取门板、层板、锁具、铰链和分隔件公式，再用 BOM/DXF 做数量闭环。
