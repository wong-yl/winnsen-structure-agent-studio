# 16029 Door Upper Hinge True Sheet-Metal Feature Probe

Date: `2026-06-17`
Updated: `2026-06-18`

## Result

The low-risk true sheet-metal API routes still do not close the upper hinge conflict.

- Sketched-bend selected-segment route: `4/4` holes can exist on the flat blank and all four bend segments plus fixed face are selected, but SW2020 does not create the sketched-bend feature; folded bbox stays `343.4 x 334.4 x 0.8`.
- Native base-flange probe: setting `InsertSheetMetalBaseFlange2` `ExtrudeDist1` to `0.8 mm` or `18.2 mm` still does not create a native base flange; the tool falls back to extrusion plus ConvertToSheetMetal.
- Edge-flange Closed Corner route: official selection marks can create a real `CornerFeat`, but the successful end-face variants still leave `3/4` cuts and export an unusable flat DXF bbox of `307 x 298`. The flat-preserving inner-face variant creates no Closed Corner feature.
- Edge-flange official profile route: the official `SketchUseEdge` profile sequence is stable. A stepped global-left `14.1 mm` profile creates the needed flat contour while preserving flat `343.4 x 334.4` and folded `307 x 298 x 18.2`. The upper hinge circle can be sketched on the folded top return face, but the fourth `FeatureCut3` still fails, so DXF remains `CIRCLE=3`.
- Edge-flange Unfold route: `InsertSheetMetalUnfold()` is called, but face probes show the return flanges remain in folded-state coordinates. It is not a reliable flat edit state for this model.
- Edge-flange relief FeatureData route: `relief_none`, `relief_tear_rip`, and `relief_rect_0p1` all preserve `SheetMetal + FlatPattern`, flat `343.4 x 334.4`, and folded `307 x 298 x 18.2`, but all stay at `3/4` cuts and `CIRCLE=3`.
- Edge-flange custom profile sketch route: read-only `IInsertSketchForEdgeFlange` probing is stable and returns four empty `ProfileFeature` sketches. A narrow line-write probe can create a line, but the result is invalid; horizontal or long profile-line writes triggered SW2020/CLR application errors before JSON output. This write path is disabled by preflight.
- Miter flange route: SW2020 interop exposes `InsertSheetMetalMiterFlange`, but the tested profile-sketch + edge-selection variants did not create a MiterFlange feature. The result stays as a converted base sheet `269 x 260 x 0.8`.

## Probe Summary

| Route | Best positive result | Blocker |
|---|---|---|
| sketched-bend selected segments | `4/4` holes, `4` bend lines selected, preselect count `5` | no sketched-bend feature; folded bbox `343.4 x 334.4 x 0.8` |
| native base flange | sheet-metal and flat-pattern still export through fallback | native base flange never created |
| edge-flange Closed Corner | real `CornerFeat` can be created with official marks | hole cut count remains `3/4`; real CornerFeat breaks flat DXF to `307 x 298` |
| edge-flange official profile | stepped `14.1 mm` contour is sufficient; fourth circle sketch can be made on folded top return | fourth circle does not become a CutFeature |
| edge-flange Unfold | command returns and rebuilds | flanges remain in folded coordinates, so flat-mapped edit coordinates are unreliable |
| edge-flange relief FeatureData | all three variants preserve flat/folded bbox | all remain `3/4`, flat DXF `CIRCLE=3` |
| edge-flange custom profile sketch | read-only probe stable; four profile sketch containers returned | sketches are empty; write attempts are not release-valid and unsafe for unattended automation |
| miter flange | profile sketch and edges can be selected | neither FeatureManager nor legacy ModelDoc API creates MiterFlange in the smoke model |

## Engineering Meaning

The gold-contour rule still points to missing top-left hinge-side material. The official profile route now proves that the material contour can be made in SW2020 without losing the flat/folded envelope. The remaining automation blocker is narrower: creating the fourth upper hinge `CutFeature` from the folded top-return sketch.

## Recommended Next Decision

Choose one narrow path:

1. Finish the folded top-return cut-feature creation for the official profile route.
2. Record the equivalent manual SW2020 macro for the fourth folded top-return cut, then translate that exact sequence into the generator.
3. Rebuild the door from a controlled custom sheet-metal profile if the per-corner repair remains unreliable.
4. Get structure/process signoff that the upper hinge `10.2 mm` hole is post-process, keeping the current formal DXF blocked for the `4/4` true-cut supplier objective.

Until one of these is selected, supplier DXF precheck remains blocked for the `4/4` hole requirement.
