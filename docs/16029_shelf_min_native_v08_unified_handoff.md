# 16029 shelf_min_native unified handoff through v19

Current state: v19 portable builder produces a saved disposable native shelf-min artifact with `SheetMetal + SMBaseFlange + FlatPattern + body`, K-factor `0.4`, thickness `0.0008 m`, bend radius `0.0008 m`, and `300×200×0.8 mm` body envelope. The related `SLDWORKS/sldProcMon` process set is clean.

## Confirmed results

- v08: official positive control created `SheetMetal + SMBaseFlange + FlatPattern + body`.
- v09: exact project corner rectangle plus project insertion profile returned `null`; rectangle construction alone was not the cause.
- v10: exact project 300×200 rectangle plus official insertion profile succeeded. Project rectangle geometry is exonerated.
- v11/v12/v13: post-creation SMBaseFlange correction reliably produced:
  - thickness readback `0.0008 m`
  - radius readback `0.0008 m`
  - body bounding box `300×200×0.8 mm`
  - native feature tree and one solid body
- v17: PCBA `K=0.4` created a native base flange and fresh SMBaseFlange readback was `0.4`.
- v18 combines PCBA K=0.4 with fresh SMBaseFlange thickness/radius correction in one native artifact.
- v19 packages that successful route as a portable builder with separate source, executable, output, and evidence.
- No original-shelf replacement, OpenDoc6, assembly, interference, DXF/BOM, or release PASS exists.

## Latest selection diagnosis

- v13 `Feature.Select2(false, 0)` returned `false`.
- v14 used the official `SelectByID2(name, "BODYFEATURE")` mechanism, but localized display name `钣金6` also returned `false`.
- `AccessSelections` and SheetMetal `ModifyDefinition` therefore remain unreached.
- v15 confirmed that neither localized nor tried English BODYFEATURE names selected the top SheetMetal feature. This route is no longer the preferred K-factor path.

## Latest v19 portable-builder artifacts

- part SHA256: `43376004802D2D5FBD6CFE3210B5C947041285F80670B07199397B802D84F35B`
- evidence SHA256: `49D395331C389612F2057B6A0C6F6A6D2817A106E0CE53EC58E028A9009D03E1`
- v19 is portable-builder runtime evidence, not a release artifact.

## Process state

- All attributable v09–v19 `sldProcMon` residuals were identity-checked and individually cleaned under standing authorization.
- Current related process count: `0`.

## Next target

`SHELF_MIN_NATIVE_V20_PORTABLE_BUILDER_DELIVERY_AUDIT_GATE`

Perform a read-only v20 delivery audit. Do not run OpenDoc6, assembly, exact/ClosestDistance, DXF/BOM, release, or modify the original shelf/candidate placement.
