# 16029 V29 Feedback Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a separate SolidWorks 2020 V29 review candidate that applies the two confirmed dimension corrections, preserves the already-concentric lower-frame interface, verifies the native assembly, and publishes a new feedback round.

**Architecture:** Copy the complete V28 native CAD tree into an isolated V29 root, then use V29-scoped SolidWorks API utilities to change two driving dimensions in the upper door-frame part and move four native sketch-block instances in the base-bottom part. Verify the result through reopened feature dimensions, exact assembly geometry, recursive component inventory, package hashes, and an isolated portal integration test before restarting the LAN portal.

**Tech Stack:** SolidWorks 2020 COM API, C#/.NET Framework compiler, PowerShell, Node.js, existing review portal.

## Global Constraints

- Baseline is `v43-int-v28-five-feedback-fix-r1`; target is `v43-int-v29-three-feedback-fix-r1`.
- Do not overwrite or save the V28 native CAD.
- Change only `D2@草图5` and `D3@草图5` in `门框 上.sldprt`, plus the four `草图9` knockout-block positions in `底座底板.sldprt`.
- Preserve `D2@草图9 = 426 mm` and the already-concentric bottom-plate/lower-frame hole pair.
- Existing V28 rebuild findings are release blockers, not permission to expand this feedback fix.
- The result remains `NON_PRODUCTION_ENGINEERING_REVIEW_ONLY`.
- Do not add dependencies or push Git changes.

---

### Task 1: Isolate the V29 Native CAD Baseline

**Files:**
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/native_cad/**`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/work/backups/**`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/**`

**Interfaces:**
- Consumes: complete V28 `native_cad` tree containing 85 CAD files.
- Produces: byte-identical V29 starting copy and SHA256 backups of both target parts.

- [x] **Step 1: Close the visible V28 SolidWorks document without saving**

Use Windows application control to close SolidWorks. If prompted for `标准寄存柜1917×1000×550(总装配).SLDASM`, choose “No/不保存”, then verify no `SLDWORKS` process remains.

- [x] **Step 2: Copy the native CAD baseline**

```powershell
$v28 = 'workers\generated_models\review_generation_requests\v43-int-v28-five-feedback-fix-r1'
$v29 = 'workers\generated_models\review_generation_requests\v43-int-v29-three-feedback-fix-r1'
New-Item -ItemType Directory -Force "$v29\evidence","$v29\work\backups","$v29\work\tools","$v29\bin" | Out-Null
Copy-Item -LiteralPath "$v28\native_cad" -Destination "$v29\native_cad" -Recurse
```

- [x] **Step 3: Verify copy completeness**

Run a recursive count and SHA256 comparison keyed by relative path. Expected: 85 files, zero missing paths, zero SHA mismatches.

- [x] **Step 4: Back up target parts**

Copy `门框 上.sldprt` and `底座底板.sldprt` from the V29 native CAD tree to `work/backups`, preserving their pre-change SHA256 values.

### Task 2: Build a V29-Scoped Dimension Modifier

**Files:**
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/work/tools/SetPartDimensionsInPlaceV29.cs`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/work/tools/SolidWorksPidRotConnector.cs`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/bin/SetPartDimensionsInPlaceV29.exe`
- Copy: SolidWorks interop DLLs and read-only inspectors into the V29 `bin` directory.

**Interfaces:**
- Consumes: `<part> <out-json> <dimension=value-mm>` followed by any additional `dimension=value-mm` arguments.
- Produces: saved native part plus JSON containing before/after values, hashes, feature health, body count, and save status.

- [x] **Step 1: Create the V29 source from the reviewed V28 utility**

Copy the existing source and change its allow-list from `\v43-int-v28-five-feedback-fix-r1\` to `\v43-int-v29-three-feedback-fix-r1\`; rename the class and usage text to V29. No other behavior changes.

- [x] **Step 2: Compile with the existing SolidWorks interop assemblies**

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 `
  /out:"$v29\bin\SetPartDimensionsInPlaceV29.exe" `
  /reference:"$v29\bin\SolidWorks.Interop.sldworks.dll" `
  /reference:"$v29\bin\SolidWorks.Interop.swconst.dll" `
  "$v29\work\tools\SolidWorksPidRotConnector.cs" `
  "$v29\work\tools\SetPartDimensionsInPlaceV29.cs"
```

- [x] **Step 3: Verify the scope guard**

Run the V29 executable against the V28 `门框 上.sldprt`. Expected: exit code 3 and JSON error `refusing to modify a part outside the isolated v29 feedback-fix candidate`.

### Task 3: Record the Failing V29 Baseline

**Files:**
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_before_feature_dimensions.txt`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_before_base_bottom_vs_reinforcement_1.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_before_base_bottom_vs_reinforcement_2.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_before_base_bottom_vs_lower_frame.json`

**Interfaces:**
- Consumes: untouched V29 copy.
- Produces: reproducible pre-change evidence for the requested corrections.

- [x] **Step 1: Inspect the target feature dimensions**

Run `InspectPartFeatureDimensions.exe` on both target parts. Expected pre-change values:

```text
D2@草图5 = 0.25
D3@草图5 = 0.25
D1@草图9 = 0.59
D2@草图9 = 0.426
```

- [x] **Step 2: Run exact assembly checks**

Run `InspectExactStaticPairV28.exe` for:

```text
底座焊接-1/底座底板-1 vs 底座焊接-1/底座加强筋-1
底座焊接-1/底座底板-1 vs 底座焊接-1/底座加强筋-2
底座焊接-1/底座底板-1 vs 门框焊接-1/门框 下-1
```

Expected baseline: knockout centers at `x=±295 mm`, reinforcement holes at `x=±335 mm`, while the identified bottom/lower-frame rivet axes remain at `x=±125 mm, z=-25 mm` with 0 mm axis offset.

### Task 4: Apply the Minimal Native CAD Changes

**Files:**
- Modify: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/native_cad/candidate_native_hierarchical_pack_and_go/门框 上.sldprt`
- Modify: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/native_cad/candidate_native_hierarchical_pack_and_go/底座底板.sldprt`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_set_door_frame_top_pitch_200.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_set_base_knockout_span_670.json`

**Interfaces:**
- Consumes: V29-scoped dimension modifier and active empty SolidWorks 2020 session.
- Produces: two saved, rebuilt native parts.

- [x] **Step 1: Start an empty SolidWorks session**

Start SolidWorks 2020 with no document open and verify a single active `SldWorks.Application.28` session.

- [x] **Step 2: Change the upper door-frame dimensions**

```powershell
& "$v29\bin\SetPartDimensionsInPlaceV29.exe" $doorTop "$v29\evidence\v29_set_door_frame_top_pitch_200.json" 'D2@草图5=200' 'D3@草图5=200'
```

Expected: both dimensions found/applied, rebuild and save succeed, zero part feature errors.

- [x] **Step 3: Change the base-bottom knockout span**

```powershell
& "$v29\bin\MoveBaseKnockoutBlocksV29.exe" $baseBottom "$v29\evidence\v29_move_base_knockout_blocks_to_x335.json"
```

Expected: four stale placement relations removed, four block instances moved to `x=±335 mm`, `D1@草图9` recalculates to 670 mm, rebuild/save/reopen/second rebuild succeed, and part feature errors remain zero.

- [x] **Step 4: Confirm modification scope**

Compare all 85 V29 files to V28 by relative-path SHA256. Expected: exactly two changed files, matching the two target parts.

### Task 5: Verify the Reopened Native Model

**Files:**
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_after_feature_dimensions.txt`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_exact_base_bottom_vs_reinforcement_1.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_exact_base_bottom_vs_reinforcement_2.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_exact_base_bottom_vs_lower_frame.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_after_base_cut3_vs_lower_frame_cut2_feature_holes.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/evidence/v29_full_assembly_inventory.json`

**Interfaces:**
- Consumes: modified V29 native parts and unchanged V29 assemblies.
- Produces: post-change feature, geometry, and hierarchy evidence.

- [x] **Step 1: Reopen and inspect target parts**

Expected values:

```text
D2@草图5 = 0.2
D3@草图5 = 0.2
D1@草图9 = 0.67
D2@草图9 = 0.426
```

Also require nonzero solid bodies, no target-part feature errors, and retained `SheetMetal`/`FlatPattern` features.

- [x] **Step 2: Verify the corrected knockout interfaces**

Run the same two reinforcement exact checks with `--require-rebuild`. Require:

```text
bottom knockout x centers = -335, 335 mm
reinforcement corresponding x centers = -335, 335 mm
axis offset = 0 mm
exact static interference count = 0
```

- [x] **Step 3: Verify the preserved lower-frame interface**

Run the bottom/lower-frame exact check with `--require-rebuild`, then use the read-only feature-face inspector on `底座底板/切除-拉伸3` and `门框 下/切除-拉伸2`. Require the identified rivet axes to remain at `x=-125,125 mm`, `z=-25 mm`, with 0 mm axis offset and no exact body interference; record the Ø3.2/Ø3.5 diameter difference for engineering disposition.

- [x] **Step 4: Verify full-assembly inventory**

Run `workers/solidworks_tools/bin/InspectAssemblyComponents.exe` on the V29 top assembly. Require 154 recursive components, zero missing component files, and zero references outside the V29 `native_cad` root.

- [x] **Step 5: Compare rebuild findings to V28**

Document the V27/V28 pre-existing `底座焊接 / MateGroup error=51`, the V29 full-assembly open-warning count, and zero feature errors in both changed parts. Do not report the baseline issue as fixed.

### Task 6: Build the V29 Engineering Review Package

**Files:**
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/review_package_v29_20260730/README_结构工程师复核.md`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/review_package_v29_20260730/package_manifest_v29.json`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/review_package_v29_20260730/feedback_reference/*.png`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/review_package_v29_20260730/review_evidence/**`
- Create: `workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/16029_v29_三项反馈原生修正复核候选_非生产_20260730.zip`

**Interfaces:**
- Consumes: verified V29 native CAD, three feedback screenshots, and post-change evidence.
- Produces: one hash-pinned nonproduction ZIP.

- [x] **Step 1: Assemble package contents**

Include the complete V29 `native_cad`, three feedback screenshots, selected evidence, the review README, and manifest.

- [x] **Step 2: Write the review boundary**

State that two confirmed items were modified, the third item was already concentric for the identified hole pair, existing rebuild findings remain engineering blockers, and production release eligibility is false.

- [x] **Step 3: Create and verify the ZIP**

Create the ZIP, reopen its central directory, confirm required paths exist, record byte size and SHA256, and ensure no `.lock`, temporary, backup, `bin`, or `work` files are included.

### Task 7: Publish the V29 Feedback Round

**Files:**
- Modify: `tools/serve_16029_review_downloads.mjs`
- Modify: `tools/verify_16029_review_portal_feedback_upload.mjs`
- Modify: `tools/verify_16029_review_portal_host.ps1`
- Copy through portal sync: `\\192.168.100.243\ys8870\参数化模型下载及反馈\模型下载\16029_v29_三项反馈原生修正复核候选_非生产_20260730.zip`

**Interfaces:**
- Consumes: V29 ZIP SHA256 and review metadata.
- Produces: current V29 download, V29 feedback IDs, preserved V28 history, and a verified LAN portal.

- [x] **Step 1: Update the portal current round**

Set the current round to `16029-v43-v29-three-feedback-native-rereview-20260730`, add a SHA-pinned V29 asset, keep V28 as the first historical asset, change generated IDs to `V29-Q-*`, and update visible V29 instructions.

- [x] **Step 2: Update the isolated portal integration test**

Change the dummy current asset, expected review round, asset ID, feedback ID prefix, and visible text assertions from V28 to V29.

- [x] **Step 3: Run portal tests before live restart**

```powershell
node --check tools/serve_16029_review_downloads.mjs
node tools/verify_16029_review_portal_feedback_upload.mjs
node tools/verify_16029_review_portal_manual_generation_control.mjs
```

Expected: all exit 0 and print PASS.

- [x] **Step 4: Restart the live portal**

Stop only the Node process listening on port 5180. Allow the existing watchdog to restart it with the updated source and synchronize the SHA-pinned V29 ZIP to shared storage.

- [x] **Step 5: Verify live LAN delivery**

Run `tools/verify_16029_review_portal_host.ps1` with V29 defaults, request `http://127.0.0.1:5180/status.json` and `http://192.168.100.117:5180/status.json`, and verify the shared ZIP SHA256 equals the source ZIP SHA256.

### Task 8: Final Diff and Evidence Review

**Files:**
- Review only: all files above and the final Git diff.

**Interfaces:**
- Consumes: completed V29 model, evidence, package, and live portal.
- Produces: concise handoff with verified facts and explicit remaining blockers.

- [x] **Step 1: Inspect the final Git diff**

Confirm the intended V29 portal, test, and current-handoff verification lines; distinguish and preserve unrelated pre-existing dirty-worktree changes.

- [x] **Step 2: Re-run the final verification set**

Recheck dimension JSON, exact-pair JSON, 85-file CAD count, 154-component inventory, ZIP SHA256, live status, and shared-copy SHA256 using fresh outputs.

- [x] **Step 3: Report verified and unverified boundaries**

Report the two applied dimension changes, the preserved 0 mm interface, package and platform locations, test results, and any pre-existing rebuild, process, tolerance, DXF, BOM, drawing, or prototype items that remain unverified.
