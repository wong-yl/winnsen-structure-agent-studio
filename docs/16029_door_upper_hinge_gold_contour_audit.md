# 16029 Door Upper Hinge Gold Contour Audit

Date: `2026-06-17`

## Result

- D option is not the primary blocker: the gold 2/12 DXF keeps the upper and lower 10.2 mm hinge holes symmetric on one hinge datum.
- B option is now the preferred narrow candidate: the current SW top-left corner relief removes too much top strip material.

## Key Measurements

| Item | Value |
|---|---:|
| gold upper hinge center | (-208.5, 154.6) |
| gold upper center clearance to top strip edge | 9 mm |
| formal upper mapped center | (-3.8, 262.55) |
| formal current top strip edge X | 5.2 mm |
| expected top strip edge X if gold clearance is preserved | -12.8 mm |
| minimum top strip edge X for full circle | -8.9 mm |
| top strip material missing vs gold rule | 18 mm |
| top strip material missing vs full-circle minimum | 14.1 mm |

## Engineering Reading

The source DXF supports keeping the hinge datum and correcting the top-left relief/return-flange interpretation. Moving only the upper hole remains a non-default option because it breaks the hinge-hole datum relationship.
