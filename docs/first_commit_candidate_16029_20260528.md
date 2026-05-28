# First Commit Candidate - 16029 Current Mainline

Updated: 2026-05-28

This is the proposed first clean commit scope. It is intentionally narrow.

## Include

### Project Direction

- `.gitignore`
- `README.md`
- `docs/16029_current_project_process.md`
- `docs/16029_git_cleanup_boundary.md`
- `docs/project_code_management_policy.md`
- `docs/first_commit_candidate_16029_20260528.md`
- `data/locker_16029_project_route_manifest.json`
- `data/locker_16029_project_route_manifest.md`

### Engineer UI And API

- `apps/web/index.html`
- `apps/web/src/App.tsx`
- `apps/web/src/App.css`
- `apps/web/src/data/studioData.ts`
- `services/api/README.md`
- `services/api/app/config.py`
- `services/api/app/main.py`

### Review Portal

- `tools/serve_16029_review_downloads.mjs`
- `tools/start_16029_review_portal.mjs`
- `tools/run_16029_review_portal_watchdog.ps1`
- `tools/render_16029_project_flow_pdf.mjs`
- `tools/check_16029_first_commit_scope.ps1`
- `tools/audit_16029_needs_decision.ps1`
- `tools/verify_16029_current_mainline.ps1`
- `tools/stage_16029_first_commit.ps1`
- `tools/guard_16029_staged_scope.ps1`

### Current 16029 Scripts

- `workers/maintenance/generate_16029_800w_gold_variable_model_freecad.py`
- `workers/maintenance/finalize_16029_800w_gold_variable_handoff.py`
- `workers/maintenance/validate_16029_current_handoff_scope.ps1`
- `workers/maintenance/build_16029_dimension_contract.py`
- `workers/maintenance/validate_16029_dimension_contract.py`
- `workers/maintenance/build_16029_variable_door_stack_contract.py`
- `workers/maintenance/build_16029_800w_lms_contract.py`

### SolidWorks 2020 Evidence Source

- `workers/solidworks_tools/StepOpenProbe.cs`
- `workers/solidworks_tools/build_step_open_probe.ps1`

## Exclude From First Commit

- `workers/handoffs/**`
- `workers/generated_models/**`
- `workers/generation_logs/**`
- `workers/analysis/**`
- `workers/tmp_*`
- `data/*_gate*.*`
- `data/*_validation*.*`
- `data/review_download_users.json`
- `data/review_download_invite_code.txt`
- `data/review_feedback/**`
- `workers/solidworks_tools/bin/**`
- `*.step`, `*.FCStd`, `*.SLDPRT`, `*.SLDASM`

## Needs Separate Decision

These are tracked modifications and should not be included just because they are dirty:

- `workers/solidworks_tools/BuildDoorArrayModule.cs`
- `workers/solidworks_tools/BuildDoorWeldModule.cs`
- `workers/solidworks_tools/BuildOrdinaryDoorModule.cs`
- `workers/solidworks_tools/PackAndGoAssembly.cs`
- `workers/solidworks_tools/sw_make_parametric_part_dimension.js`
- Existing compiled outputs under `workers/solidworks_tools/bin`
- Older `build_native_16029_*` scripts not tied to the current gold-variable route

Current decision: keep these files on disk, classify them with `tools/audit_16029_needs_decision.ps1`, and defer them out of the first commit unless a later native SolidWorks route is explicitly reopened.

## Verification Already Run

- `npm run build`: PASS
- API Python compile: PASS
- Review portal syntax check: PASS
- Current handoff scope gate: PASS, 60 checks, 0 failed
- Web handoff page browser check: PASS, no console errors
- Review portal browser check: PASS, login works and shows scope gate PASS
- `tools/check_16029_first_commit_scope.ps1`: PASS, 33 include, 10 exclude, 15 needs_decision, 0 uncategorized
- `tools/audit_16029_needs_decision.ps1`: PASS, 15 deferred, 0 unclassified
- `tools/verify_16029_current_mainline.ps1`: PASS, 7 checks, 0 failed
