# 16029 Door Upper Hinge Patch Probe

Date: `2026-06-17`

## Result

- Unfolded local patch: creates `4/4` cuts and keeps flat bbox in the best run, but folded height becomes `424.15 mm`, so it is not release-valid.
- Folded local boss patch: does not become valid sheet-metal flat material; tested variants either keep `3/4` or inflate folded depth to `36.4 mm`.
- Conclusion: ordinary boss patch is rejected. The next valid CAD path must use a true sheet-metal flange/profile or a sketched-bend reconstruction.

## Probe Summary

| Probe | Patch | Cuts | Folded bbox | Flat bbox | Result |
|---|---:|---:|---|---|---|
| unfold_flat_patch_v1_4of4_but_folding_invalid | yes | 4 | 307 x 424.15 x 18.2 | 343.4 x 334.4 | upper cut yes |
| unfold_flat_patch_overlap_v2_invalid_edgeflange_state | yes | 2 | 269 x 405.15 x 0.8 | 269 x 260 | upper cut yes |
| folded_patch_xz_fallback_flip0_depth_invalid | yes | 3 | 307 x 298 x 36.4 | 343.4 x 334.4 | upper cut no |
| folded_patch_xz_fallback_flip1_depth_invalid | yes | 3 | 307 x 298 x 36.4 | 343.4 x 334.4 | upper cut no |
| folded_patch_lines_flip0_depth_invalid | yes | 3 | 307 x 298 x 36.4 | 343.4 x 334.4 | upper cut no |
| folded_patch_insertSketch2_flip0_unstable_no_patch | no | 1 | 269 x 260 x 0.8 | 269 x 260 | upper cut no |
