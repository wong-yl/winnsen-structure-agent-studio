# 16029 Project Route Manifest

## Current Route

- Route: 16029 800W gold-variable LMS/SML
- Role: current engineer review route
- Outer size: 800W x 1917H x 550D
- Door width: W337
- Internal gap rule: 2 + 3 + 2 = 7
- CAD mainline: SolidWorks 2020
- SolidWorks shortcut: `C:\Users\Public\Desktop\SOLIDWORKS 2020.lnk`
- SolidWorks exe: `D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe`
- Generation entry: `workers/maintenance/generate_16029_800w_gold_variable_model_freecad.py`
- Finalize entry: `workers/maintenance/finalize_16029_800w_gold_variable_handoff.py`
- Scope gate: `workers/maintenance/validate_16029_current_handoff_scope.ps1`

SolidWorks 2025 is historical environment evidence only; it is not the current engineer-facing CAD mainline.

## Gold Source Reference

- Baseline: 16029 1000W x 1917H x 550D / 10/12/14
- Role: gold/source reference for rule extraction, source geometry comparison, and traceability
- Engineer-facing decision: not a current LMS/SML handoff package, but not obsolete and not a trial waste line

## Current Engineer-Facing Package Candidates

Current gate status on 2026-05-28: PASS. These packages are the current review candidates for the engineer workflow; LMS and SML both have SolidWorks 2020 open screenshot evidence and the current handoff scope gate passes.

- `workers/handoffs/16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip`
- `workers/handoffs/16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip`
- `workers/handoffs/16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip`

SolidWorks 2020 open evidence:

- LMS screenshot: `workers/generation_logs/cad_open_screenshots/solidworks2020_lms_step_open.png`
- LMS JSON: `workers/generation_logs/cad_open_screenshots/solidworks2020_lms_step_open.png.json`
- SML screenshot: `workers/generation_logs/cad_open_screenshots/solidworks2020_sml_step_open.png`
- SML JSON: `workers/generation_logs/cad_open_screenshots/solidworks2020_sml_step_open.png.json`
- Scope gate: `data/locker_16029_current_handoff_scope_gate.json`, PASS, 60 checks, 0 failed

Each current engineer-facing variant must include STEP, self-review preview, verify CSV, model gate, STEP bbox gate, SolidWorks 2020 open screenshot evidence, and a handoff zip. FCStd may remain internal generation evidence only; it is not the current engineer review format.

## Legacy Routes

| Pattern | Role | Decision |
|---|---|---|
| `16029_WIDTH_CANDIDATE*` | legacy feedback evidence only | Keep for traceability; do not use as current engineer handoff. |
| `16029_HEIGHT_CANDIDATE*` | legacy feedback evidence only | Keep for traceability; do not use as current 1917H route. |
| `16029_800W_*RULE_REVIEW*` | legacy layout review only | Useful for layout explanation, not formal assembly review. |
| `16029_10_12_14*` | gold/source same-size reference | Keep as source baseline for rule extraction; do not list as current 800W LMS/SML review package. |

## API Boundary

Current engineer-facing endpoints:

- `/api/review-downloads`
- `/api/locker-16029-current-handoff-scope`

Gold/source reference evidence endpoints:

- `/api/locker-16029-variant-rule-packet`
- `/api/locker-16029-verified-rule-packet`
- `/api/locker-16029-variant-quality-matrix`
- `/api/locker-16029-engineering-handoff-bundle`

The reference endpoints must return `current_delivery_role = gold_source_reference_only` and `engineer_facing = false`.

## Decision Rules

- Engineer-facing downloads come only from `/api/review-downloads`.
- New 16029 variants may change only the variant token and row units.
- Every handoff must pass model gate, STEP bbox gate, and current handoff scope gate.
- Preview images or layout-only packages are not formal structure review output.
