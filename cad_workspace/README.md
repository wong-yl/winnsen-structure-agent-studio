# CAD Workspace

This is the local CAD asset/script workspace (`STUDIO_CAD_WORKSPACE`). It is the
default location the API resolves generator scripts against and the snapshot
builder reads status files from.

- API default: `services/api/app/main.py` → `CAD_WORKSPACE = ROOT_DIR / "cad_workspace"`
- Snapshot builder default: `apps/web/scripts/buildStudioSnapshot.mjs` → `repoRoot/cad_workspace`
- Override either with the `STUDIO_CAD_WORKSPACE` environment variable.

## Expected layout

```text
cad_workspace/
  scripts/                 # FreeCAD/SolidWorks generator + automation scripts
    generate_locker_16029_freecad.py
    generate_outdoor_courier_single_waterproof_door_freecad.py
    ...
  outputs/
    intake/                # *_intake_status.md status files read by the snapshot builder
    freecad/               # *_queue.csv / *_queue.md status files
```

## Notes

- These scripts and status files are **not committed** to the repo. Populate this
  directory from the engineering workspace (formerly `D:\机械结构工程师智能体`)
  or generate them locally.
- Running the worker/generation flow also needs FreeCAD (and SolidWorks for the
  SolidWorks routes) installed. Point the API at FreeCAD via `STUDIO_FREECAD_CMD`
  / `STUDIO_FREECAD_EXE`, or add it to `PATH`.
- The snapshot builder degrades gracefully: if a status file is missing, that
  field is emitted empty rather than crashing the build.
