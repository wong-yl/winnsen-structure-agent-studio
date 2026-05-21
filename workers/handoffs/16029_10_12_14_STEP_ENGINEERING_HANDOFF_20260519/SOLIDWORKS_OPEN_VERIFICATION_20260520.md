# SolidWorks open verification - 2026-05-20

Scope: 16029 / 1000W x 1917H x 550D / 12 door STP handoff.

Test file:

`D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\16029_1000W_1917H_550D_12door_solidworks_import.stp`

## Result

Status: `solidworks_step_api_blocked_manual_open_required`

SolidWorks was launched during verification, and the 12 door STEP reached a SolidWorks repair prompt. The model was not confirmed as an active SolidWorks document after automated confirmation attempts, so the SolidWorks one-click STEP API route remains blocked.

Additional controlled check:

- Launching `SLDWORKS.exe` without a model reached a visible `SOLIDWORKS Premium 2025 SP4.0` main window.
- Sending the 12 door STP to that running session created a second hidden `SLDWORKS.exe` process.
- After waiting 180 seconds, no document title for the 12 door STP was confirmed in a visible SolidWorks window.
- The SolidWorks processes started by this verification were stopped, leaving only the normal `sldworks_fs` background service.

## Evidence

- First attempt used PowerShell COM automation from `open_12door_step_in_solidworks.ps1`.
- SolidWorks started as `SLDWORKS.exe -Embedding`.
- COM failed at `$sw.Visible = $true` with `TYPE_E_ELEMENTNOTFOUND`.
- The process had no main window handle and only exposed a dummy/broadcast window.
- Second attempt launched the real executable directly:
  `D:\软件安装录\soildworks\SOLIDWORKS\SLDWORKS.exe <12door STP>`.
- The process stayed alive and memory rose to about 675 MB, but no visible main window appeared after waiting.
- The non-visible SolidWorks process was stopped to avoid leaving load on the workstation.
- A later main-window startup test confirmed the application itself can show normally, but file handoff still fails to confirm a visible imported document.
- Command-line STEP open created a second hidden process with the STEP argument instead of loading into the already visible main session.
- `sw_open_and_activate.js` was tested against the same 12 door STEP. It opened the SolidWorks main window but returned `open_failed`.
- `sw_probe_step_open.vbs` was tested against the same STEP. `OpenDoc6` and `LoadFile4` attempts returned type mismatch errors.
- The same `sw_open_and_activate.js` API route was tested with a small native SolidWorks part, `中控内侧板.sldprt` (~48 KB). It returned `active=中控内侧板.sldprt`, so native SolidWorks API open works on this workstation.
- The regenerated 12 door safe launcher was executed after writing PowerShell scripts with UTF-8 BOM. It exited normally, wrote `manual_open_required`, selected the STEP file in Explorer, and did not leave a hidden `SLDWORKS.exe` process running.
- A typed C# SolidWorks interop probe was added at `D:\Winnsen_Structure_Agent_Studio\workers\solidworks_tools\StepOpenProbe.cs` and compiled to `D:\Winnsen_Structure_Agent_Studio\workers\solidworks_tools\bin\StepOpenProbe.exe`.
- The typed probe can reliably call `OpenDoc7`, `OpenDoc6`, and `LoadFile4` without JScript/VBS byref type mismatch.
- The typed probe result for the 12 door STEP is saved at `D:\Winnsen_Structure_Agent_Studio\workers\handoffs\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\12door\solidworks_step_open_probe_12door_interactive.json`.
- Typed probe result: `document_count=0`, `active_title=""`, no active document.
- `OpenDoc6_part_silent` and `OpenDoc6_part_interactive` returned error code `2097152`, which maps to `swFileRequiresRepairError`.
- `OpenDoc7_part_silent` returned warning code `262144`, which maps to `swFileLoadWarning_AutomaticRepair`.
- During the interactive attempt, SolidWorks displayed: "SOLIDWORKS encountered a problem in the following file ... 是否要让 SOLIDWORKS 修复此文件?".
- Sending Enter / Alt+Y from automation did not result in an active document; `GetDocumentCount` remained `0`.
- The typed probe was updated to wait 180 seconds before declaring a STEP import failure, avoiding premature shutdown while SolidWorks may still be loading.
- A small original STEP from the source library, `C:\sw16029_standard_ascii\plastic_bushing.step`, was also tested with the same 180 second wait.
- The original small STEP still returned `swFileRequiresRepairError=2097152` / `swFileLoadWarning_AutomaticRepair=262144` and no active document after 180 seconds.
- A non-silent auto-repair probe against the same original small STEP caused the SolidWorks COM connection to return RPC failures (`0x800706BA` / `0x800706BE`) and did not produce an active document.
- Five re-exported 12 door STEP variants were generated from FreeCAD (`AP214IS`, `AP203`, `Part.export`, `Import.export`, compound export). All returned the same SolidWorks STEP repair/error signature.
- A production-only STEP excluding rule reference objects (`REF_*`, `hinge_axis_reference_*`) was generated and tested. It preserved the 1000 mm width and 550 mm depth but still returned the same SolidWorks STEP repair/error signature.
- Module-level STEP slices were exported for shell, verticals/door frame, base/electrical, door panels/ribs, door hardware, shelves, and door-frame horizontals. The slices tested through SolidWorks API returned the same repair/error signature.

## Fix Applied

The handoff opener generator was changed so SolidWorks launch scripts no longer pass the STEP path directly to `SLDWORKS.exe`, because that created a hidden second SolidWorks process during verification. The regenerated scripts now:

1. Start the SolidWorks main window first.
2. Try the existing `sw_open_and_activate.js` API route.
3. If the API route does not confirm an active document, select the STEP file in Explorer and write `manual_open_required` status for engineer hand open.

PowerShell launcher encoding was also changed to UTF-8 BOM so Windows PowerShell can read the Chinese local SolidWorks install path correctly.

Updated generator:

`D:\Winnsen_Structure_Agent_Studio\workers\maintenance\build_16029_engineering_handoff_bundle.py`

## Next Action

Run a manual visual check from the desktop session:

1. Open SolidWorks 2025 normally from the desktop shortcut.
2. Use File > Open and select the 12 door STP above.
3. When SolidWorks asks whether to repair the STEP, click "是".
4. Confirm whether the repair/import completes into an active document and whether the repaired model can be saved as native `.SLDPRT`/`.SLDASM`.

Do not treat the SolidWorks one-click STEP opener as production-ready until a visible SolidWorks open is confirmed.

Current diagnosis: SolidWorks API can open native `.sldprt` files, but STEP import is not reliable through automation on this workstation, including a small original source STEP. Keep STEP handoff as manual engineer-open until the repair prompt can be handled reliably, the workstation STEP import settings are corrected, or the flow is replaced with native SolidWorks macro generation.
