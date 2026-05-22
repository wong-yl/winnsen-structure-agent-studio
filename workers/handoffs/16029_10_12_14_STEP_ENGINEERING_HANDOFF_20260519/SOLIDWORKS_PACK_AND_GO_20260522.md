# SolidWorks Pack-and-Go Handoff 2026-05-22

Scope: 16029 1000W x 1917H x 550D native enriched SolidWorks engineering references.

Result: PASS

| Door count | OK | Document count | Files | SLDASM | SLDPRT | Total MB | Package folder |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | --- |
| 10 | True | 72 | 72 | 19 | 53 | 31.628 | D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\10door\solidworks_pack_and_go |
| 12 | True | 71 | 71 | 18 | 53 | 31.174 | D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\solidworks_pack_and_go |
| 14 | True | 72 | 72 | 19 | 53 | 31.639 | D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\14door\solidworks_pack_and_go |

Use:

- Prefer the top assembly inside each `solidworks_pack_and_go` folder when moving the handoff to another Windows workstation.
- The normal native handoff SLDASM remains useful on this workstation; Pack-and-Go is for safer transfer.
- This is still an engineering reference package, not a released drawing/BOM/DXF package.

Evidence:

- Summary CSV: `solidworks_pack_and_go_summary.csv`
- Per-door result JSON files are stored inside each `solidworks_pack_and_go` folder.
