# 16029 Vibe Coding Video Runbook

## Recording Goal

Show a real engineer-facing loop:

1. Review portal receives a cabinet request.
2. Browser preview updates from parameters.
3. Queue hands the task to the SolidWorks 2020 worker.
4. The final Pack-and-Go download contains SW2020 assemblies, parts, evidence JSON, and review captures.
5. Structure feedback returns clean before handoff.

## Preflight

Run these before recording:

```powershell
git status --short --branch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\verify_16029_current_mainline.ps1 -SkipWebBuild
node tools\start_16029_review_portal.mjs
```

Portal:

```text
http://127.0.0.1:5180/
```

Use a fresh local recording account. Do not show the invite code or password on camera.

## Fast Demo Path

1. Open the portal and log in.
2. Scroll to `模型任务`.
3. Click `录制用 740W 已验证模板`.
4. Show the fields:
   - `740W x 1917H x 550D`
   - `2` columns
   - `6` doors
   - `L642-R246`
   - `W307`
5. Rotate the preview and switch `ISO / 前视 / 右视 / 俯视`.
6. Submit the task and show the queue card.
7. Use the latest ready task if live SolidWorks generation would take too long.
8. Download the SW2020 full-assembly package.

Known ready task from the June 1 smoke run:

```text
20260601T002057-5df7b4
solidworks2020_full_assembly_ready
```

## Talking Points

- This is not a static screenshot generator. The UI produces a parameter payload, queues a worker task, and produces a SolidWorks 2020 Pack-and-Go package.
- The route is template-rule based, derived from the verified `740W / L642-R246 / v43` gold source. It does not use the historical direct per-part assembly route.
- FreeCAD is internal only. Engineer-facing output is SolidWorks 2020.
- The engineer feedback is now represented as rules:
  - cabinet body weldment modules are rebuilt as assemblies;
  - left/right side-board weldments stay separate;
  - shelf weldment boundary is checked;
  - door modules keep sheet-metal naming where the gold source has it;
  - Pack-and-Go file names are mapped to Chinese engineer-facing names.
- The final gate checks `P0/P1/P2` structure feedback before marking the package ready.

## Evidence To Show

From the latest successful E2E:

```text
status: solidworks2020_full_assembly_ready
handoffReadinessStatus: engineer_ready
structureFeedbackStatus: clean
structureFeedbackP0/P1/P2: 0/0/0
packAndGoChineseSaveNameMapCount: 27
```

The generated ZIP contains SW2020 `.SLDASM` / `.SLDPRT` files plus evidence JSON. Do not commit generated ZIPs, screenshots, logs, or model outputs.

## Avoid Overclaiming

Use this wording for the current state:

```text
The verified 740W native template is fully runnable end to end. Wider and alternate left/right row sequences now produce deterministic template-rule plans, and the next SolidWorks rule-binding pass is the remaining step for broad native geometry mutation.
```

Avoid saying:

```text
Any cabinet size is already fully native-generated in SolidWorks.
```
