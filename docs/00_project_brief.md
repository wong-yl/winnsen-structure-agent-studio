# Project Brief

## Goal

Build a long-term structure-engineering intelligence platform for Winnsen hardware products.

The platform should turn historical cabinet, locker, vending-machine, and self-service equipment CAD data into reusable structure knowledge and parameterized sheet-metal model generators.

## Current Asset Workspace

Source data and CAD extraction scripts live in:

`D:\机械结构工程师智能体`

Important current assets:

- `outputs\intake\intake_pipeline_status_v1.md`
- `outputs\intake\outdoor_courier_family_intake_status.md`
- `outputs\freecad\locker_16029_strict_geometry_upgrade_queue.md`
- `outputs\freecad\outdoor_courier_production_gate_queue.md`

## Immediate Product Need

Create visibility before adding more engineering drawings.

The user needs to see:

- what model packages have been imported
- which files were parsed
- which modules have rule candidates
- what can already be generated
- what is still blocked
- what needs engineering review
- what future products are queued for intake

## MVP Deliverables

1. Local dashboard project skeleton.
2. Database schema for imported projects, files, modules, rules, generation capability, and review queues.
3. Data adapters that read existing CSV/MD/YAML outputs from `D:\机械结构工程师智能体`.
4. Frontend pages:
   - project overview
   - intake pipeline
   - module/rule library
   - generatable models
   - manual review queue
   - structure Agent console
5. Clear separation between:
   - evidence-backed geometry
   - engineering reference models
   - production candidates
   - manual review items

## Non-goals For MVP

- Do not build a full CAD editor.
- Do not promise production-ready drawings.
- Do not ingest every future product before the dashboard exists.
- Do not let LLM output become geometry source of truth.

## Decision

Start from the App dashboard. Continue CAD intake only after the visibility layer is working.
