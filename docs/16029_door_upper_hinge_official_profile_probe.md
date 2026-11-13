# 16029 Door Upper Hinge Official EdgeFlange Profile Probe

Status: `OFFICIAL_EDGEFLANGE_PROFILE_PARTIAL_PROGRESS_NOT_4_OF_4`

## Result

The official SW2020 EdgeFlange profile sequence is stable:

`IInsertSketchForEdgeFlange -> EditSketch -> SketchUseEdge -> closed profile lines -> InsertSheetMetalEdgeFlange`

This is different from the earlier unsafe single-line sketch write. It creates a real profile sketch and keeps the sheet-metal envelope valid for the stable variants.

## Key Findings

| Variant | Flat contour | Hole cuts | Flat DXF | Folded bbox | Result |
|---|---|---:|---|---|---|
| rectangular top profile | unchanged | `3/4` | `PASS`, `CIRCLE=3` | `307 x 298 x 18.2` | stable but no fix |
| sloped global-left `14.1 mm` | free edge reaches `X=-8.9` only at top | `3/4` | `PASS`, `CIRCLE=3` | `307 x 298 x 18.2` | not enough at hole height |
| stepped global-left `14.1 mm` | vertical side reaches `X=-8.9` from `Y=257.95..275.15` | `3/4` | `PASS`, `CIRCLE=3` | `307 x 298 x 18.2` | contour sufficient, cut still missing |
| stepped global-left `18 mm` | too aggressive | `1/4` | `FAIL`, bbox `269 x 260` | `269 x 260 x 0.8` | top flange fails |
| stepped `14.1 mm` + folded top-return fallback | fourth circle sketch created on folded top return | `3/4` | `PASS`, `CIRCLE=3` | `307 x 298 x 18.2` | fourth CutFeature fails |
| stepped `14.1 mm` + folded top-return precut + `FeatureCut4` | face/circle OK | `3/4` or worse | `PASS`, `CIRCLE=3` on stable variant | `307 x 298 x 18.2` on stable variant | ordinary Cut4 does not close |
| stepped `14.1 mm` + manual top-return `X/Z` sketch | sketch coordinate `X=-3.8`, `Y=7.4` | `3/4` | `PASS`, `CIRCLE=3` | `307 x 298 x 18.2` | coordinate fallback does not close |

## Engineering Meaning

Candidate B is now better localized:

- The flat contour can be made materially sufficient without breaking flat/folded size at `14.1 mm`.
- The upper hinge face can be selected in folded state, and the fourth circle can be sketched.
- Manual `X/Z` sketch fallback proves the fourth circle can be placed inside the folded top return at the expected local coordinate.
- The remaining blocker is not relief, ClosedCorner, flat contour, or sketch coordinate mapping. It is the SW2020 cut-feature creation for the fourth folded top-return sketch.

Cut variants `1`, `2`, and `5` were checked for the fourth folded top-return sketch. All created the circle but returned no CutFeature. Variants `3` and `4` did not keep the edge-flange build stable enough to evaluate the fourth cut.

Additional cut probes:

- `FeatureCut4` variant `17`: stable envelope, face/circle created, still `holeCutCount=3`.
- `FeatureCut4` variants `19`/`21`: direction variants were worse and not release-valid.
- Manual `X/Z` sketch variants `33` and `49`: circle was created at `X=-3.8`, `Y=7.4`, but no upper CutFeature was created.
- `InsertSketch2` retry with manual `X/Z`: same `3/4` result.

Do not promote this to the formal door yet. Supplier DXF remains blocked until the fourth sketch becomes a real CutFeature and flat DXF exports at least four circles.

Next CAD path should be a recorded manual SW2020 macro or `SMNormalCutFeatureData2` / `CutDirection` workflow. Do not continue ordinary `FeatureCut3`/`FeatureCut4` parameter sweeps.
