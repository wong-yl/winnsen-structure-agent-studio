# 规则种子定标台账

- Generated at: `2026-05-18T01:17:36.981844Z`
- Source: `D:\Winnsen_Structure_Agent_Studio\data\rule_seed_candidates.json`
- Items: `14`

这些项目默认不能进入生成器。系统先自动补 DXF/BOM/图纸证据闭环；只有冲突、缺失、或高风险项才进入人工定标。

| ID | Priority | Rule | Value | Templates | Gate | Required evidence |
| --- | --- | --- | --- | ---: | --- | --- |
| `SEED-014-SHELF_PITCH` | P0 | 层板节距 | 11 x 152.5mm | 2 | `blocked_pending_evidence_closure` | DXF 展开尺寸 / BOM/装配数量 / 证据闭环记录 |
| `SEED-006-DOOR_COLUMN_PITCH` | P0 | 柜门列距/阵列 | 6 x 305mm | 2 | `blocked_pending_evidence_closure` | DXF 展开尺寸 / BOM/装配数量 / 证据闭环记录 |
| `SEED-013-SERVICE_DOOR_CLEARANCE` | P1 | 应急维护门间隙 | 0.5mm | 2 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / DXF/工程图间隙 / 证据闭环记录 |
| `SEED-010-LEVELING_FOOT_OFFSET` | P1 | 调整脚高度/底部距离 | 55mm | 2 | `blocked_pending_evidence_closure` | 底座/调整脚工程图 / BOM 标准件 / 证据闭环记录 |
| `SEED-003-DISTANCE_MATE` | P1 | 距离配合 | 2mm | 3 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / 证据闭环记录 |
| `SEED-004-DISTANCE_MATE` | P1 | 距离配合 | 55mm | 2 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / 证据闭环记录 |
| `SEED-007-FRAME_GAP` | P1 | 门框局部间隙 | 0.5mm | 2 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / DXF/工程图间隙 / 证据闭环记录 |
| `SEED-008-FRAME_GAP` | P1 | 门框局部间隙 | 2mm | 3 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / DXF/工程图间隙 / 证据闭环记录 |
| `SEED-012-LOCAL_PATTERN` | P2 | 局部阵列 | 2 x 915mm | 1 | `blocked_pending_evidence_closure` | DXF 展开尺寸 / BOM/装配数量 / 证据闭环记录 |
| `SEED-009-HANGER_RAIL_SPACING` | P2 | 衣架钢管间距 | 2 x 915mm | 1 | `blocked_pending_evidence_closure` | DXF 展开尺寸 / BOM/装配数量 / 证据闭环记录 |
| `SEED-011-LEVELING_FOOT_OFFSET` | P2 | 调整脚高度/底部距离 | 60mm | 1 | `blocked_pending_evidence_closure` | 底座/调整脚工程图 / BOM 标准件 / 证据闭环记录 |
| `SEED-001-DISTANCE_MATE` | P2 | 距离配合 | 0.2mm | 1 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / 证据闭环记录 |
| `SEED-002-DISTANCE_MATE` | P2 | 距离配合 | 0.5mm | 1 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / 证据闭环记录 |
| `SEED-005-DISTANCE_MATE` | P2 | 距离配合 | 60mm | 1 | `blocked_pending_evidence_closure` | SolidWorks mate 角色绑定 / 证据闭环记录 |

## Recommended Actions

- `SEED-014-SHELF_PITCH` 层板节距 `11 x 152.5mm`: 自动核对层板 DXF 高度、BOM 层板数量和总装阵列数量，闭环后进入层板节距规则。
- `SEED-006-DOOR_COLUMN_PITCH` 柜门列距/阵列 `6 x 305mm`: 自动核对门框横隔板、门板宽高和门数量关系，闭环后再进入同尺寸补门数生成器。
- `SEED-013-SERVICE_DOOR_CLEARANCE` 应急维护门间隙 `0.5mm`: 自动分离应急维护门与普通门的间隙定义，避免误套到普通门。
- `SEED-010-LEVELING_FOOT_OFFSET` 调整脚高度/底部距离 `55mm`: 自动区分调整脚高度是装配状态、采购规格还是安装余量，不直接参与钣金展开。
- `SEED-003-DISTANCE_MATE` 距离配合 `2mm`: 先做 距离配合 的来源证据闭环，再决定是否进入生成器。
- `SEED-004-DISTANCE_MATE` 距离配合 `55mm`: 先做 距离配合 的来源证据闭环，再决定是否进入生成器。
- `SEED-007-FRAME_GAP` 门框局部间隙 `0.5mm`: 自动绑定距离配合两侧零件角色，再判断是否为可复用门缝/框缝规则。
- `SEED-008-FRAME_GAP` 门框局部间隙 `2mm`: 自动绑定距离配合两侧零件角色，再判断是否为可复用门缝/框缝规则。
- `SEED-012-LOCAL_PATTERN` 局部阵列 `2 x 915mm`: 先做 局部阵列 的来源证据闭环，再决定是否进入生成器。
- `SEED-009-HANGER_RAIL_SPACING` 衣架钢管间距 `2 x 915mm`: 自动核对衣架钢管数量、安装孔位和左右仓关系，先作为附件布置规则。
- `SEED-011-LEVELING_FOOT_OFFSET` 调整脚高度/底部距离 `60mm`: 自动区分调整脚高度是装配状态、采购规格还是安装余量，不直接参与钣金展开。
- `SEED-001-DISTANCE_MATE` 距离配合 `0.2mm`: 先做 距离配合 的来源证据闭环，再决定是否进入生成器。
- `SEED-002-DISTANCE_MATE` 距离配合 `0.5mm`: 先做 距离配合 的来源证据闭环，再决定是否进入生成器。
- `SEED-005-DISTANCE_MATE` 距离配合 `60mm`: 先做 距离配合 的来源证据闭环，再决定是否进入生成器。
