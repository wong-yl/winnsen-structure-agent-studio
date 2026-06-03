# 16029 Memory: Gold Sheet Metal Rules And No Electric Lock Door Modules

Updated: 2026-06-02

This note is the current memory point for the 16029 door-rule branch. It records the rule decisions that must be checked before generating or reviewing new models.

## Source Boundaries

- SolidWorks 2020 remains the engineer-facing CAD mainline.
- FreeCAD is internal evidence and parameter assistance only.
- The verified template seed is `740W / L642-R246 / v43`.
- The `1000W x 1917H x 550D` source set is gold/source reference. It is not obsolete and must not be treated as a current engineer download package.
- Generated models, logs, screenshots, ZIP files, and smoke output folders stay local and are not commit candidates.

## Gold DXF Evidence

The collector scans the project-local 1000W gold/source folder:

```powershell
python tools\collect_16029_gold_sheetmetal_evidence.py --quiet
```

Current evidence summary:

- DXF parsed: `142/142`
- Hole candidates: `4661`
- Current flat-pattern rows: `57`
- Door panel role rows: `19`
- Door ratio classes observed: `1/12`, `2/12`, `3/12`, `4/12`, `5/12`, `6/12`, `12/12`

Rule generation:

```powershell
node tools\build_16029_gold_sheetmetal_rules.mjs --quiet
```

Current generated rule facts:

- Door classes bound for generation: `1/12` through `6/12`
- Flat width rule: `installedDoorWidthMm + 36.4`
- Flat height rule: `installedDoorHeightMm + 36.4`
- Common hole status: `ready_from_gold_dxf`
- SolidWorks cut-feature status: `template_cut_features_configured_from_gold_dxf_datums_pending_direct_rebuild`

Important limitation: the generator now carries hole positions as datum/evidence and can suppress template cut features that are not present in the 1000W gold DXF class. It has not yet created every hole directly from datum coordinates as new SolidWorks cut features.

## No Electric Lock Rule

Current generated models do not include:

- electrical boards
- cabinet-side electric lock bodies
- electric lock hooks such as `电控U型锁钩ZJA-S500`

The model must preserve interface information through:

- lock mounting hole datums
- sheet-metal hole datum evidence from the 1000W gold DXF rules
- row-plan-derived door and shelf coordinates

Because older 2/4/6 template door modules can carry `电控U型锁钩ZJA-S500`, the full-assembly worker now uses this policy:

```text
nativeDoorModuleGenerationPolicy = force_generated_modules_without_cabinet_electric_lock
generatedModulesRequiredForNoElectricLock = true
```

Do not revert to direct template door reuse unless a new gate proves the template module no longer contains electric-lock hook geometry.

## Verified Smoke On 2026-06-02

Single-door smoke:

- Door unit: `3/12`
- Installed size: `387 x 450.5`
- Expected flat size from gold DXF rule: `423.4 x 486.9`
- Rule status: `bound_to_1000w_gold_dxf`
- Hole datum count: `2`
- Active template cut features: `4`
- Suppressed template cut features: `1` (`切除-拉伸6`)
- Hole feature status: `template_cut_features_configured_from_gold_dxf_datums_pending_direct_rebuild`

Full-assembly smoke:

- Variant: `900W / L3333-R444 / 7 doors`
- Generated native door modules: `2` (`L 3/12`, `R 4/12`)
- Native door module binding: `ready_from_generated_modules`
- Pack-and-Go generated: yes
- Search result for `ZJA-S500`, `电控U型锁钩`, `electric_lock_hook`, `电控锁体`, `锁控板`: zero matches in the new Pack-and-Go and structure evidence.
- Generated native door module summaries now carry `sheetMetalHoleDatums`, `sheetMetalDetectedCutFeatureCount`, `sheetMetalSuppressedCutFeatureCount`, `sheetMetalAppliedFeatureOperations`, and `sheetMetalHoleFeatureStatus`; full assembly and queue summaries must preserve `generatedNativeDoorModuleHoleDatumCount`, `generatedNativeDoorModuleDetectedCutFeatureCount`, `generatedNativeDoorModuleSuppressedCutFeatureCount`, and `generatedNativeDoorModuleHoleFeatureStatuses`.
- Status remains `needs_structure_revision`, because the candidate still uses provisional parametric scaffold and is below the 1000W gold/source component-count floors.

Portal E2E on 2026-06-02:

- Frontend request: `20260602T070030-083183`
- Variant: `900W / L3333-R444 / 7 doors`
- Final queue status: `solidworks2020_full_assembly_needs_structure_revision`
- Result kind: `solidworks2020_structure_revision_evidence_package`
- Download route: `/generation-download/20260602T070030-083183`
- Download probe: `200 application/zip`, `11966313` bytes
- Pack-and-Go inventory: `31` files (`5` SLDASM, `26` SLDPRT)
- Generated native door modules: `2`
- Generated door hole datums: `4`
- Active template cut features: `8`
- Suppressed template cut features: `2`
- Gold structure gate: `FAIL`, `3` issues (`P0=2`, `P1=1`)
- Structure feedback: `issues_found`, `1` issue (`P1=1`)
- Final package electrical/electric-lock residue search: zero matches for `ZJA-S500`, `电控U型锁钩`, `electric_lock_hook`, `电控锁体`, `锁控板`, `电器件`, `电控`

## Current Gates

Use this as the minimum check before reporting a model as current:

```powershell
tools\verify_16029_current_mainline.ps1 -SkipWebBuild
```

Current result after this node: `41/41 PASS`.

Do not mark a generated package engineer-ready while `goldStructureGateStatus` is `FAIL` or `handoffReadinessStatus` is `needs_structure_revision`.
