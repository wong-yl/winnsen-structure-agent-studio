# 16029 Door Upper Hinge Candidate B Probe

Date: `2026-06-17`

## Result

- Gold contour evidence supports candidate B: keep the hinge datum and restore top-left hinge-side material.
- Simple SW EdgeFlange parameter changes did not close the release condition.
- Best valid geometry remains `3/4` cut features with flat `343.4 x 334.4` and folded `307 x 298 x 18.2`.

## Tested Parameters

| Probe | Edge defs | Hole cuts | Folded bbox | Result |
|---|---:|---:|---|---|
| edge_order:native | 4 | 3 | 307 x 298 x 18.2 | formal flat/folded preserved but upper hole still not cut |
| edge_order:reverse | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| edge_order:top-first | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| edge_order:bottom-first | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| edge_order:left-first | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| edge_order:right-first | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| edge_order:horizontal-first | 4 | 3 | 307 x 298 x 18.2 | formal flat/folded preserved but upper hole still not cut |
| edge_order:vertical-first | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| trim_side_bends_mode:0 | 0 | 1 | 269 x 260 x 0.8 | degenerated to base plate or invalid edge flange result |
| trim_side_bends_mode:1 | 4 | 3 | 307 x 298 x 18.2 | formal flat/folded preserved but upper hole still not cut |

## Next CAD Path

Use a custom top-left corner relief/flange sketch rule, not another broad hole-position sweep. If that is not approved, the upper hinge hole must remain a process-signed post-operation and supplier DXF precheck stays blocked.
