# 16029 Candidate B Edge-Flange Relief Probe

Status: `EXECUTED_NOT_RELEASE_VALID_THREE_VARIANTS_STILL_3_OF_4`

## What Was Tested

The SW2020 generator exposes native `IEdgeFlangeFeatureData` bend-relief controls:

- relief type, width, depth, ratio, and tear type
- default-relief and relief-ratio switches
- flange gap

After SW2020 startup recovered, three isolated Candidate B variants were run without overwriting the formal door:

| Variant | Relief | Folded bbox | Flat DXF | Hole cuts | Release valid |
|---|---|---|---|---:|---:|
| `relief_none` | no relief, gap `0` | `307 x 298 x 18.2` | `PASS`, `343.4 x 334.4`, `CIRCLE=3` | `3/4` | no |
| `relief_tear_rip` | tear/rip, gap `0` | `307 x 298 x 18.2` | `PASS`, `343.4 x 334.4`, `CIRCLE=3` | `3/4` | no |
| `relief_rect_0p1` | rectangular `0.1 x 0.1`, gap `0.1` | `307 x 298 x 18.2` | `PASS`, `343.4 x 334.4`, `CIRCLE=3` | `3/4` | no |

## Conclusion

Simple EdgeFlange relief FeatureData changes do not restore the missing top-left hinge-side material. They preserve the current sheet-metal envelope, but the upper hinge cut remains absent.

This route is ruled out as a direct release path. Candidate B still requires a controlled custom flange/profile reconstruction or recorded manual SW2020 macro evidence before any formal door promotion.
