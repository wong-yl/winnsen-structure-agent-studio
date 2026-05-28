# Project Code Management Policy

Updated: 2026-05-28

This repository is managed as an engineering workflow, not as a dump folder. The current active product line is:

- Project: 16029 800W gold-variable LMS/SML
- CAD mainline: SolidWorks 2020
- Size: 800W x 1917H x 550D
- Door width: W337
- Gap rule: 2 + 3 + 2 = 7
- Current gate: PASS, 60 checks, 0 failed

Gold/source reference baseline:

- 1000W x 1917H x 550D / 10/12/14 is the verified source baseline for rule extraction and geometry comparison.
- It may appear in internal source evidence, scripts, manifests, and traceability docs.
- It must not be listed as a current engineer-facing review package for the 800W handoff.
- It must not be mislabeled as a trial route or waste line.

## Management Rules

### Keep In Current Code

These are current project code and documentation:

- Engineer-facing web UI and API changes for the current 16029 route
- Review login/download portal code
- SolidWorks 2020 config and open-evidence automation
- Current route manifest and project process docs
- Fixed generator/finalizer entries:
  - `workers/maintenance/generate_16029_800w_gold_variable_model_freecad.py`
  - `workers/maintenance/finalize_16029_800w_gold_variable_handoff.py`
  - `workers/maintenance/validate_16029_current_handoff_scope.ps1`

### Do Not Commit By Default

These files may stay on disk but should not go into the first Git cleanup commit:

- Handoff zip files and extracted handoff folders
- Generated model files, STEP, FCStd, SLDPRT, SLDASM
- Gate output CSV/JSON/MD files
- Screenshot evidence files
- Review login users, invite codes, and feedback uploads
- Compiled EXE/DLL files under `workers/solidworks_tools/bin`
- Temporary probe output and `workers/tmp_*`

### Treat As Historical Evidence

These are not deleted automatically, but they are not current delivery sources:

- `16029_WIDTH_CANDIDATE*`
- `16029_HEIGHT_CANDIDATE*`
- `16029_800W_*RULE_REVIEW*`
- R3/R4/R5/R6/R7 feedback repair branches
- SolidWorks 2025 environment traces
- FreeCAD-only screenshots or layout-only packages

### Keep As Gold Source Reference

These are kept for source traceability and rule extraction, but not shown as current engineer-facing handoff packages:

- `16029_10_12_14*`
- 1000W x 1917H x 550D source assembly, DXF, BOM, STEP, and supporting evidence

### Never Reintroduce As Current Mainline

- 1200W / 2117H / W537 trial routes
- 1000W / 10/12/14 mislabeled as current handoff or waste; it is gold/source reference only
- Lightweight rule-review packages as formal engineer handoff
- SolidWorks 2025 as current CAD mainline
- Screenshot-only proof without SolidWorks 2020 open JSON evidence

## Git Discipline

The first cleanup commit should be small and focused:

- UI/API/review portal wording and routes
- `.gitignore` cleanup
- Current route manifest
- Current process and cleanup docs
- SolidWorks 2020 open-evidence source code
- Current 16029 scope gate script

Do not include model packages, screenshots, generated gates, or binary outputs unless there is a separate explicit decision.

Before any first cleanup commit, run:

```powershell
tools/check_16029_first_commit_scope.ps1
tools/audit_16029_needs_decision.ps1
tools/verify_16029_current_mainline.ps1
tools/stage_16029_first_commit.ps1
tools/guard_16029_staged_scope.ps1
```

The scope script must report `0 uncategorized`. Files in `include` may enter the first commit candidate. Files in `exclude` and `needs_decision` must not be staged automatically.

The needs-decision audit script must report `PASS`. It records which tracked legacy SolidWorks and older native-route source changes are intentionally deferred out of the first cleanup commit.

The verification script must report `PASS`. It runs API compile, review portal syntax, first-commit scope classification, needs-decision audit, current handoff scope gate, and web build.

The staging script defaults to dry-run. It must not be run with `-Apply` unless staging is explicitly approved. If anything is staged manually, run `tools/guard_16029_staged_scope.ps1` before commit.

## Verification Required Before Handoff

Before a package is treated as engineer-review ready:

1. Run model gate.
2. Run STEP bbox gate.
3. Open LMS and SML STEP in SolidWorks 2020.
4. Save readable screenshots and JSON open evidence.
5. Run current handoff scope gate.
6. Confirm web UI and review portal no longer show stale blockers.

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
