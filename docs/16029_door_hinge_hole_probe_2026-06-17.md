# 16029 door hinge hole probe - 2026-06-17

## Conclusion

The door panel supplier-DXF precheck is still blocked. The new probe narrowed the issue:

- Lower hinge 10.2 mm hole: feasible as a real cut feature after mapping the datum into the current FlatPattern coordinate space and selecting the Z-facing unfolded sheet face.
- Upper hinge 10.2 mm hole: not feasible with the current datum and flat-pattern corner relief. Its mapped center falls outside the current 12-line flat-pattern material boundary.
- Therefore the next step is not another broad SolidWorks `FeatureCut3` sweep. The structure rule must be confirmed: upper hinge datum, top-left corner relief, or flange interpretation is inconsistent.

## Evidence

Current door formal artifact remains valid for core geometry:

- Folded envelope: `307 x 298 x 18.2 mm`
- FlatPattern DXF bbox: `343.4 x 334.4 mm`
- SW2020 feature tree: `SheetMetal + FlatPattern + EdgeFlange`

Additional probes:

| Probe | Result |
|---|---|
| `C:/tmp/door_v34_edge_unfold_holecuts_perhole_probe` | Lock-side 2.0 mm holes cut as 2/4 physical cuts; flat/folded dimensions preserved |
| `C:/tmp/door_flatmapped_hole3_variant_{1,2,11,12}` | Lower hinge hole cuts successfully; flat `343.4 x 334.4`, folded `307 x 298 x 18.2` preserved |
| `C:/tmp/door_flatmapped_hole4_variant_{1..16}` | Upper hinge hole never selects a Z-facing material face; no cut created |
| `C:/tmp/door_flatmapped_all_variant_1` | Not promoted: only 1/4 cut, geometry not release-valid |

FlatPattern polygon mapping:

| Hole | Source datum mm | Mapped FlatPattern center mm | Status |
|---|---:|---:|---|
| Lock lower | `(138.5, -25)` | `(278.2, 82.95)` | inside material |
| Lock upper | `(138.5, 25)` | `(278.2, 132.95)` | inside material |
| Hinge lower | `(-143.5, -154.6)` | `(-3.8, -46.65)` | inside material, nearest edge `9.0 mm`, radius `5.1 mm` |
| Hinge upper | `(-143.5, 154.6)` | `(-3.8, 262.55)` | outside material, nearest edge `6.6 mm`, radius `5.1 mm` |

## Engineering Decision

Keep the narrow pilot conclusion: part-first improves model quality and engineering control because this conflict surfaced at single-part validation instead of being hidden inside a full assembly.

Do not release supplier DXF/BOM yet. The structure engineer must decide whether to:

- move the upper hinge hole datum,
- change the top-left corner relief / flange rule,
- define the feature as a separate post-process operation,
- or explicitly accept datum-only evidence for this pilot.
