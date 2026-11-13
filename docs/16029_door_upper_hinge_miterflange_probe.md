# 16029 Door Upper Hinge MiterFlange Probe

Date: `2026-06-17`

## Result

The MiterFlange route did not close the upper hinge hole conflict.

- SW2020 interop exposes both `FeatureManager.InsertSheetMetalMiterFlange` and legacy `ModelDoc.InsertSheetMetalMiterFlange`.
- The smoke tool can create a profile sketch and select edge candidates.
- Tested variants did not create a MiterFlange feature.
- Best checked variant: `top-first + single edge + edges first + legacy ModelDoc fallback`.
- Best result still stayed at converted base sheet geometry: `269 x 260 x 0.8`, not folded `307 x 298 x 18.2`.

## Evidence

| Probe | Path | Result |
|---|---|---|
| profile + 4 edges | `C:/tmp/door_miterflange_probe_v1/result.json` | `miterFlangeCreated=false` |
| profile + one edge | `C:/tmp/door_miterflange_oneedge_v2/result.json` | `miterFlangeCreated=false` |
| edges first | `C:/tmp/door_miterflange_edgesfirst_v2/result.json` | `miterFlangeCreated=false` |
| legacy ModelDoc API fallback | `C:/tmp/door_miterflange_modeldoc_v2/result.json` | `miterFlangeCreated=false` |
| top-first one-edge legacy fallback | `C:/tmp/door_miterflange_topfirst_oneedge_modeldoc_v3/result.json` | `miterFlangeCreated=false` |

## Engineering Conclusion

MiterFlange is not a current automation candidate for the formal door. The next route should remain either:

1. manual SW2020 macro evidence for a valid custom flange / closed-corner operation, then translate that sequence into the generator; or
2. structure/process signoff that the upper hinge `10.2 mm` hole is post-process.

Supplier DXF precheck remains `BLOCKED` until the formal door has `4/4` true cut features with `flat 343.4 x 334.4 mm` and `folded 307 x 298 x 18.2 mm`, or an explicit process signoff changes the rule.
