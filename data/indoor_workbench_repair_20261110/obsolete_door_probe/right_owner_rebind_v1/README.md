# Right owner reference research

Throwaway research only. Preparation is frozen; no CAD, dry-run, or CAD copy was executed by the preparation worker.

The sole question is whether rebinding the one right-panel instance in the isolated right-weld assembly clears warning bit 1. `modelAccepted`, `engineeringAccepted`, `operationComplete`, and `strictColdAcceptancePassed` remain false. `finiteMateBoundaryWitnessComplete=false`; full finite mate boundaries remain unverified. The original production Open/Rebuild/Save gates are unchanged.

Run only from `D:\Winnsen_Structure_Agent_Studio`, sequentially under the primary agent's control:

```powershell
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v1/run.mjs relink --run-native
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v1/run.mjs repair --run-native <relink-result.json>
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v1/run.mjs cold --run-native <repair-result.json>
```

Use the exact `resultPath` printed by the prior stage. No dry-run entry is supplied. Preserve an existing candidate; the relink stage refuses to overwrite it. A failed stage leaves its evidence and candidate in place for review.

Relink creates the existing 119-file relative layout plus one byte-identical fresh panel named `right_rebound_native.SLDPRT`. All exact source dependency paths are mapped to the candidate. It independently re-reads every candidate's dependencies after relinking and requires zero old-source edges. Source inventory is 119; fresh is protected separately; candidate inventory is 120. API-empty part dependencies retain the existing byte-identical source cross-check policy and do not independently prove absence of an API read failure.

Repair opens only the isolated right-weld owner for writing. Exact warning 33 permits the selected-instance `ReplaceComponents2(newPath, originalConfiguration, false, 0, true)` call. Warning 0 or 32 records `no_rebind_needed` and skips replacement, rebuilding, and saving. Other warnings or any open error stop the stage without replacement. The three cold sessions then inspect either the new rebound panel or the unchanged original panel according to the recorded disposition.

Snapshots preserve all raw feature/mate states, `EntityParams`, persistent references, component counts, configurations, and transforms. Raw Equal results are recorded unchanged. Research comparison explicitly normalizes only the replaced instance's path/name and its mate-entity persistent reference. The same feature may improve to zero error or lose a warning; every such change is logged. New errors/warnings, semantic mate changes, or non-target changes stop the experiment. Original geometry/mirror gates and the 568 reflected-vertex check remain in use. Only the candidate owner may receive strict `ForceRebuild3(false)` and `Save3(1)`; save return=true, errors=0, warnings=0 and cleared saveFlag are required.

Cold results report the owner, total assembly, and active right-panel warnings individually. `allThreeBit1Cleared` and `allThreeWarningsZero` are observations, never model acceptance. Each cold arm runs in a new owned CAD session and never saves or rebuilds.

The existing global lock, 10-second heartbeat, empty-CAD/monitor gate, portal active-state/nonnull-lease gate, exact native owner observation, preserved PID 13080 `sldworks_fs`, and unknown-child timeout lock retention are inherited. Timeout never terminates CAD. An exactly witnessed orphan monitor may be cleaned only by the inherited observer after its owned CAD process naturally exits.

Preparation verification: both C# executables compiled with exit code 0 (CS0649 serializer/original-field warnings only), runner `node --check` passed, observer `ast.parse` passed, original geometry and observer are byte-identical, all 119 source pins and fresh pin matched. Runtime behavior, cold warnings, mate finite boundaries, and engineering acceptance have not been verified.
