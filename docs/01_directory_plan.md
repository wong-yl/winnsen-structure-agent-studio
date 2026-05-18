# Directory Plan

## Root

`D:\Winnsen_Structure_Agent_Studio`

## Directories

| directory | purpose |
|---|---|
| `apps` | frontend applications |
| `services` | backend API services |
| `workers` | CAD/intake/model-generation task workers |
| `data` | local database, cache, and imported status snapshots |
| `configs` | provider config, CAD paths, model routing config |
| `docs` | architecture, handoff, planning, and schema docs |

## Expected Future Layout

```text
D:\Winnsen_Structure_Agent_Studio
  apps
    web
  services
    api
  workers
    cad_intake
    freecad_generator
    solidworks_extractor
    llm_review
  data
    studio.sqlite
    snapshots
  configs
    providers.example.json
    cad_paths.example.json
  docs
    00_project_brief.md
    01_directory_plan.md
    02_mvp_data_model.md
```

## External CAD Asset Workspace

Do not duplicate the full CAD asset workspace into this repo.

Use this as the read/source workspace:

`D:\机械结构工程师智能体`

The App should import normalized summaries, not copy raw company CAD files unless the user explicitly asks.
