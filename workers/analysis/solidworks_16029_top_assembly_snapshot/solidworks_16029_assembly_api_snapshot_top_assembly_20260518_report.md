# SolidWorks 16029 Assembly API Snapshot

- Generated at: `Mon May 18 23:40:03 UTC+0800 2026`
- Source assembly: `C:\sw16029_direct_18door\source\top_assembly.SLDASM`
- Document title: `top_assembly.SLDASM`
- SolidWorks revision: `33.4.0`
- OpenDoc6 errors/warnings: `0` / `0`

## Extracted Files

- JSON: `D:\Winnsen_Structure_Agent_Studio\workers\analysis\solidworks_16029_top_assembly_snapshot\solidworks_16029_assembly_api_snapshot_top_assembly_20260518.json`
- Components CSV: `D:\Winnsen_Structure_Agent_Studio\workers\analysis\solidworks_16029_top_assembly_snapshot\solidworks_16029_assembly_api_snapshot_top_assembly_20260518_components.csv`
- Mates CSV: `D:\Winnsen_Structure_Agent_Studio\workers\analysis\solidworks_16029_top_assembly_snapshot\solidworks_16029_assembly_api_snapshot_top_assembly_20260518_mates.csv`
- Features CSV: `D:\Winnsen_Structure_Agent_Studio\workers\analysis\solidworks_16029_top_assembly_snapshot\solidworks_16029_assembly_api_snapshot_top_assembly_20260518_features.csv`

## Counts

- Component tree rows: `0`
- Flat GetComponents(false) count: `0`
- Max component tree depth: `0`
- Mate feature count: `75`
- Feature count: `98`

## Notes

- Component transforms are taken from `IComponent2.Transform2` with a fallback to `GetTotalTransform(true)`.
- Translation fields use SolidWorks transform array indices 9, 10, and 11 converted from meters to millimeters.
- Mate rows are feature/entity level data. Semantic names such as door gap, hinge axis, and lock datum still need rule binding in the next pass.
- This file is extraction evidence only; it is not a production release drawing.