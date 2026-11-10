# Right owner rebind v2 research

This throwaway v2 changes only the isolated directory and the closed-dependency owner scope. V1, its failed probe/session, and its complete candidate are preserved and never reclassified as successful.

The candidate still contains exact copies of the original 119 CAD files plus the fresh right panel (120 total). Only 107 runtime files are dependency owners: 77 in `native_cad/working_pack` including the fresh panel, and 30 in `doors`. All 107 must parse with 180 direct dependency edges before any mutation. Dependencies to either original or candidate `door_sources`, or any path outside the exact runtime mapping, block mutation. Independent post-reading requires all 107 runtime owners, zero old-source edges, and 214 candidate dependency calls in total.

All 13 `door_sources` templates remain byte-identical protected copies. They are never opened, dependency-read, relinked, rebuilt, or saved by this experiment. Source inventory 119, fresh pin, and candidate inventory 120 remain fully hash-protected. Dependency isolation is claimed only for the 107 runtime files. `excludedProtectedDoorSources13` and `noRuntimeEdgesToExcluded` are recorded explicitly.

The owner repair/mate comparison, mother panel, geometry, Name2 behavior, production Open gate, strict experimental owner rebuild/save, three independent cold sessions, global lock/heartbeat, external observer, exact owner identity, protected fs process, and timeout lock retention remain as in v1. No model or engineering acceptance is claimed; finite mate boundaries remain unverified.

Run sequentially from `D:\Winnsen_Structure_Agent_Studio`; pass the exact result path printed by the preceding stage:

```powershell
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v2/run.mjs relink --run-native
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v2/run.mjs repair --run-native <relink-result.json>
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_owner_rebind_v2/run.mjs cold --run-native <repair-result.json>
```

Preparation verified two C# compile exits 0, `node --check`, observer `ast.parse`, current source/build pins, exact runtime layout/count gates, original owner logic except path, and preserved v1 artifacts. Preparation did not execute CAD, a dry-run, acquire a lock, or copy CAD. Runtime and cold warning results remain unverified.
