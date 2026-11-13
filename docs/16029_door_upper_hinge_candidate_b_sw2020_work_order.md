# 16029 Door Upper Hinge Candidate B SW2020 Work Order

Status: `WAITING_FOR_B_ENGINEERING_SIGNOFF`

This work order is for the `door_panel_2_12_right_v43` upper hinge 10.2 mm hole only. It does not authorize supplier DXF release.

## Prerequisite

- Signed file required: `data/locker_16029_door_upper_hinge_engineering_signoff.json`
- Required selected option: `B_ADJUST_TOP_LEFT_CORNER_RELIEF`
- The signoff template is not enough: `data/locker_16029_door_upper_hinge_engineering_signoff.template.json`
- Reference only: `workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_candidate_B_reference_NOT_FOR_SUPPLIER.dxf`

## Geometry Rule

- Preserve hinge datum: upper/lower hinge source X remains `-143.5 mm`.
- Restore top-left hinge-side flat strip edge from current `X=5.2 mm` to target `X=-12.8 mm`.
- Minimum full-circle limit is `X<=-8.9 mm`; use `X=-12.8 mm` unless structure engineering signs a different value.
- Keep flat bbox `343.4 x 334.4 mm`.
- Keep folded bbox `307 x 298 x 18.2 mm`.
- Create four real cut features in the formal SLDPRT, not datum-only circles.

## Not Allowed

- Do not use ordinary boss patch material as release evidence.
- Do not use the reference DXF as supplier DXF.
- Do not rerun crash-prone unattended `edge_flange_custom_profile_sketch_line_write`.
- Do not move only the upper hinge hole unless a separate hinge-axis approval exists.
- Do not mark option C as completing the current 4/4 true-cut objective.

## Acceptance Commands

```powershell
node tools\verify_16029_supplier_dxf_precheck.mjs --out data\locker_16029_supplier_dxf_precheck.json
node tools\verify_16029_part_first_pilot.mjs --out data\locker_16029_part_first_pilot_gate_report.json
node tools\verify_16029_part_first_pilot.mjs --strict-artifacts --out data\locker_16029_part_first_pilot_gate_report.strict.json
```

Release candidate is valid only if the formal door result proves `SheetMetal + FlatPattern`, `holeCutCount=4`, flat DXF has at least `4` circles, and supplier precheck is `READY`.
