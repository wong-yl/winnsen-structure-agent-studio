# 16029 12-door rule-driven FreeCAD model

- FreeCAD file: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260520042611-E8E6F4\locker_16029_12door_rule_driven.FCStd`
- STEP file: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260520042611-E8E6F4\locker_16029_12door_rule_driven.step`
- Verify CSV: `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\GEN-20260520042611-E8E6F4\locker_16029_12door_rule_driven_verify.csv`
- Door count: 12
- Cabinet width: 1000.000 mm
- Rows per column: 6
- Door width: 437.000 mm
- Door height: 298.000 mm
- Door pitch: 305.000 mm
- Door source: validated SolidWorks door panel STEP
- Derived door template: door_2_12_panel_template, scale_y=1.0
- Door flat pattern rule: width=door_width+38.4 mm, height=door_height+36.4 mm, thickness=0.8 mm
- Door panel geometry: production-signature FreeCAD FCStd/STEP template is used when exact width/height candidate exists; exact SolidWorks STEP is imported when a matching door height exists; otherwise rebuilt candidates or FreeCAD reference pan geometry are used with DXF-derived metadata rows
- Width variant: {'is_width_variant': False, 'cabinet_width': 1000.0, 'door_width': 437.0}
- Cabinet shell mode: {'status': 'gold_shell_step_unmodified'}
- Door rib source: rib_2_12_gold_step
- Door hardware templates from validated 3/12 assembly: 9
- SolidWorks API v002 small module references: {'status': 'solidworks_api_v002_small_modules_added', 'rows_added': 18, 'objects_added': 18, 'step_objects_added': 18, 'marker_objects_added': 0, 'source': 'D:\\机械结构工程师智能体\\outputs\\solidworks_api\\locker_16029_assembly_component_transforms_mm_v002.csv'}
- Door-driven parts: lock hole, electric lock hook, U lock hook pad, bushings, hinge pin; rib uses rebuilt SolidWorks candidate when available
- Removed seed objects from standard STEP: 53
- Valid solid objects: 435

Rule hierarchy:
1. Cabinet datum fixes outer frame, center band and full cabinet coordinate system.
2. Door local coordinate drives door panel, door weldment, rib, lock references and hinge references.
3. Adjacent door boundary drives shelf weldments and door-frame horizontal dividers.

Limit: derived non-8-door or non-1000-width layouts are reference geometry unless matching SolidWorks/flat-pattern source parts are added.