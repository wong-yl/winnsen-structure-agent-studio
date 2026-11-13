# 16029 Door Upper Hinge Closed Corner Probe

Status: `REAL_CORNERFEAT_PROVED_BUT_NOT_RELEASE_VALID`

## What Changed

The Closed Corner route was retested with the official SW2020 selection marks:

- first face mark: `1`
- second face mark: `1073741824`

This matters because earlier no-feature results were partly false negatives. With end-face selections, SW2020 creates a real `CornerFeat`.

## Results

| Variant | CornerFeat | Hole cuts | Flat DXF | Folded bbox | Release valid |
|---|---:|---:|---|---|---:|
| endfaces overlap, gap `0`, open bend | yes | `3/4` | `FAIL`, bbox `307 x 298`, `CIRCLE=3` | `307 x 298 x 18.2` | no |
| endfaces underlap, gap `0`, open bend | yes | `3/4` | `FAIL`, bbox `307 x 298`, `CIRCLE=3` | `307 x 298 x 18.2` | no |
| innerfaces overlap, gap `0`, open bend | no | `3/4` | `PASS`, bbox `343.4 x 334.4`, `CIRCLE=3` | `307 x 298 x 18.2` | no |

## Unfold Finding

`InsertSheetMetalUnfold()` is not giving a reliable flat edit state for this generated four-edge-flange part. Face probes after the call still show folded-state return faces:

- central faces remain in the `307 x 298` folded coordinate envelope
- top return faces remain vertical around `y=148.2..149`, `z=1..18.2`
- left return faces remain vertical around `x=-153.5..-152.7`, `z=1..18.2`

So flat-mapped hole coordinates cannot be used as if the model were actually unfolded.

## Engineering Meaning

Closed Corner is no longer a simple false-negative API issue. It is a real but unsuitable route:

- when it creates a real `CornerFeat`, it breaks the usable flat DXF and still does not make the upper hinge cut
- when the flat DXF remains correct, the Closed Corner feature was not created

Do not promote any Closed Corner probe to the formal door. Candidate B still needs a controlled custom flange/profile reconstruction or recorded manual SW2020 macro evidence before supplier DXF release.
