# 16029 Door Upper Hinge Candidate B Reference

Status: `REFERENCE_READY_NOT_FOR_SUPPLIER_RELEASE`

This is a reference artifact only. It is not supplier-release DXF and not proof of SolidWorks cut features.

## Candidate B Rule

| Item | Value |
|---|---:|
| current top-left strip edge X | 5.2 mm |
| target top-left strip edge X | -12.8 mm |
| minimum edge X for full 10.2 mm circle | -8.9 mm |
| restored strip vs current | 18 mm |
| minimum restoration required | 14.1 mm |
| upper hinge source X | -143.5 mm |
| lower hinge source X | -143.5 mm |

## Reference Outputs

- Machine report: `data/locker_16029_door_upper_hinge_candidate_b_reference.json`
- Reference DXF: `workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_candidate_B_reference_NOT_FOR_SUPPLIER.dxf`
- Reference bbox: `343.4 x 334.4 mm`
- Reference entities: `LINE=12`, `CIRCLE=4`

## Use Boundary

- Use this DXF only as a visual/reference target for B signoff and SW2020 manual rebuild.
- Do not feed this DXF into supplier precheck as production evidence.
- Final closure still requires the formal SLDPRT to report SheetMetal + FlatPattern, holeCutCount=4, flat 343.4 x 334.4 mm, folded 307 x 298 x 18.2 mm, and >=4 circles in the exported flat DXF.
