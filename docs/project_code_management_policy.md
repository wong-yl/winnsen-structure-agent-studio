# Project Code Management Policy

Updated: `2026-06-03`

This repository is managed as an engineering workflow, not as a dump folder. The current active product line is:

- Project: `16029 / 740W / L642-R246 / v43`
- CAD mainline: `SolidWorks 2020`
- Current package: `v43-int-v18-lockfix`
- Current status: SW2020 engineering review / handoff organization ready, not production drawing release
- Current gate: v43 delivery manifest + current handoff scope gate must pass

Gold/source reference baseline:

- `1000W x 1917H x 550D / 10/12/14` remains the verified source baseline for rule extraction and structural comparison.
- It may appear in internal source evidence, scripts, manifests, and traceability docs.
- It must not be listed as the current engineer-facing download package.
- It must not be mislabeled as a trial route or waste line.

## Keep In Current Code

These are current project code and documentation:

- Engineer-facing web UI and API changes for the current v43 route.
- Review login/download portal code.
- Review login generation queue and task-download code.
- SolidWorks 2020 generator and evidence automation.
- 1000W gold/source sheet-metal evidence and rule builders.
- v43 delivery manifest and delivery verifier:
  - `data/locker_16029_v43_internal_sheetmetal_delivery.json`
  - `tools/verify_16029_v43_delivery_manifest.mjs`
- Current process and handoff docs:
  - `docs/16029_current_project_process.md`
  - `docs/16029_memory_v43_internal_sheetmetal_repair_plan_20260602.md`
  - `workers/maintenance/16029_v43_internal_sheetmetal_delivery_handoff_20260603.md`
- Fixed generator / validation entries:
  - `tools/process_16029_review_generation_queue.mjs`
  - `tools/generate_review_solidworks_full_assembly.ps1`
  - `tools/generate_review_solidworks_single_door.ps1`
  - `workers/maintenance/validate_16029_current_handoff_scope.ps1`
  - `tools/verify_16029_current_mainline.ps1`
  - `tools/check_16029_first_commit_scope.ps1`

## Do Not Commit By Default

These files may stay on disk but should not go into the Git cleanup commit:

- Generated model folders under `workers/generated_models/`
- Generated logs and ZIPs under `workers/generation_logs/`
- Desktop screenshots
- SolidWorks binary model files: `.SLDASM`, `.SLDPRT`, `.SLDDRW`
- STEP/STP/FCStd generated geometry
- Gate output CSV/JSON/MD files generated during verification
- Review login users, invite codes, and feedback uploads
- Compiled EXE/DLL files under `workers/solidworks_tools/bin`
- Temporary probe output and `workers/tmp_*`

## Treat As Historical Evidence

These are not deleted automatically, but they are not current delivery sources:

- `v43-int-v15-*`, `v43-int-v16-*`, `v43-int-v17-*` same-route trial packages
- `16029_WIDTH_CANDIDATE*`
- `16029_HEIGHT_CANDIDATE*`
- `16029_800W_*RULE_REVIEW*`
- 800W LMS/SML/DUAL review packages from 2026-05-28
- R3/R4/R5/R6/R7 feedback repair branches
- SolidWorks 2025 environment traces
- FreeCAD-only screenshots or layout-only packages

## Never Reintroduce As Current Mainline

- 1200W / 2117H / W537 trial routes
- 1000W / 10/12/14 mislabeled as current handoff or waste; it is gold/source reference only
- 800W / 900W smoke packages mislabeled as standards
- Lightweight rule-review packages as formal engineer handoff
- SolidWorks 2025 as current CAD mainline
- Screenshot-only proof without SolidWorks 2020 model/evidence checks

## Git Discipline

The cleanup commit should be small and focused:

- UI/API/review portal wording and current v43 route.
- v43 delivery manifest and verifier.
- Current process and cleanup docs.
- SolidWorks 2020 generator, gate, and portal source code.
- Current 16029 scope gate script.

Do not include model packages, screenshots, generated gates, generated ZIPs, or binary outputs unless there is a separate explicit decision.

Before any cleanup commit, run:

```powershell
tools/check_16029_first_commit_scope.ps1
tools/audit_16029_needs_decision.ps1
tools/verify_16029_current_mainline.ps1 -SkipWebBuild
tools/guard_16029_staged_scope.ps1
```

The scope script must report `0 uncategorized`. Files in `include` may enter the commit candidate. Files in `exclude` and `needs_decision` must not be staged automatically.

## Verification Required Before Handoff

Before a package is treated as the current SW2020 review package:

1. Run the gold/source structure gate.
2. Run structure feedback.
3. Verify lock tongue count and electric/electric-lock residual count.
4. Open in SolidWorks 2020 and capture readable screenshots.
5. Run `tools/verify_16029_v43_delivery_manifest.mjs`.
6. Run current handoff scope gate.
7. Confirm the platform UI and review portal show v18 as current and old same-route packages as history.

## Approval Boundary

I can do these directly:

- Add or update docs, scripts, UI text, API metadata, validation gates, and ignore rules.
- Run local builds, tests, gates, and browser checks.
- Classify files as current, historical, generated, or local-only.

I will not do these without explicit approval:

- Delete source files.
- Revert unknown user changes.
- Commit, stage, push, or open a PR.
- Remove tracked binary files from Git history.
