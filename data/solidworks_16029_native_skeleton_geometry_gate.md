# 16029 native SolidWorks cabinet skeleton geometry gate

- generated_at: `2026-05-23T19:09:35+08:00`
- status: `PASS`
- source_dir: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\SW-NATIVE-16029-CABINET-SKELETON-SERIES-20260521`

## Variant summary

| doors | rows/column | door height | pitch | status | failed errors |
| ---: | ---: | ---: | ---: | --- | ---: |
| 10 | 5 | 359.0 mm | 366.0 mm | PASS | 0 |
| 12 | 6 | 298.0 mm | 305.0 mm | PASS | 0 |
| 14 | 7 | 254.429 mm | 261.429 mm | PASS | 0 |

## Gate coverage

- Root bbox must stay near 1000W x 1917H x 550D.
- Left/right door columns must contain the same row count and aligned Y positions.
- Each SolidWorks door placement must be added and have its transform applied.
- Left/right door column X placements must stay at the learned 16029 datum.
- Door pitch must match the configured door height plus 7 mm visual gap.
- Door weldments, door panels, hinge pins, U-lock hook pads, and electric lock hooks must match the door count.
- Shelf modules and front-frame crossbars must form left/right pairs at each internal level.
- Shelf and front-frame crossbar pitch must match the same row pitch, catching flying or collapsed arrays.

## Failed checks

- None.
