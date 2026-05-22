# SolidWorks Native Open Verification 2026-05-22

Scope: 16029 1000W x 1917H x 550D native enriched SolidWorks engineering references in the handoff package.

Method: opened 10/12/14 door `.SLDASM` files sequentially in SolidWorks 2025, saved preview PNGs, then closed each document. No parallel model loading was used.

Result: PASS.

| Door count | Opened | Preview saved | Closed | Preview |
| ---: | --- | --- | --- | --- |
| 10 | True | True | True | `solidworks_native_10door_open_check.png` |
| 12 | True | True | True | `solidworks_native_12door_open_check.png` |
| 14 | True | True | True | `solidworks_native_14door_open_check.png` |

Preview sanity:

- 10-door preview: 1293 x 698 px, nonblank pixel spread check PASS.
- 12-door preview: 1293 x 698 px, nonblank pixel spread check PASS.
- 14-door preview: 1293 x 698 px, nonblank pixel spread check PASS.

Evidence:

- Summary CSV: `solidworks_native_open_check_summary.csv`
- 10-door JSON: `solidworks_native_10door_open_check.png.json`
- 12-door JSON: `solidworks_native_12door_open_check.png.json`
- 14-door JSON: `solidworks_native_14door_open_check.png.json`

Cleanup:

- One source part was left active by SolidWorks after preview generation and was closed explicitly.
- The SolidWorks main process exited after documents were closed.
- Remaining `sldworks_fs` background service is normal SolidWorks support process, not an open model window.

Boundary:

- This verifies that the handoff SLDASM files open and render in SolidWorks on this workstation.
- This is still an engineering reference verification, not a release drawing/BOM/DXF approval.
