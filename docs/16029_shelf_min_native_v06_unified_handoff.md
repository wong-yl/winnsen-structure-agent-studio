# 16029 shelf_min_native v06 unified handoff

Current state: v05 controlled runtime is INCOMPLETE. v06 insertion-call observability correction and clean runtime preflight are ready, but v06 has not been run.

This is not a CAD, K=0.4, OpenDoc6, assembly, DXF/BOM, or release PASS.

## Current classification

- v05 runtime: `SHELF_MIN_NATIVE_V05_CONTROLLED_RUNTIME_INCOMPLETE`
- v05 regression diagnosis: `SHELF_MIN_NATIVE_V05_V03_CREATION_PATH_REGRESSION_DIAGNOSTIC_PASS`
- v06 static correction: `SHELF_MIN_NATIVE_V06_INSERT_CALL_OBSERVABILITY_CORRECTION_PASS`
- PID 14112 cleanup eligibility: `SHELF_MIN_NATIVE_V06_PID14112_RESIDUAL_REMEDIATION_ELIGIBILITY_PASS`
- PID 14112 remediation result: `SHELF_MIN_NATIVE_V06_PID14112_RESIDUAL_REMEDIATION_PASS_NATURAL_EXIT`
- v06 runtime preflight: `SHELF_MIN_NATIVE_V06_RUNTIME_PREFLIGHT_PASS`
- Current related process count: `0`
- Next safe target: `SHELF_MIN_NATIVE_V06_CONTROLLED_RUNTIME_AUTHORIZATION_GATE`

## v05 controlled runtime result

The user authorized one v05 controlled run. It was executed exactly once and was not retried.

- Builder exit code: `10`
- Wall time: `15.742 s`
- Base sketch and rectangle: created
- Active insertion root-feature delta: `18 -> 18`
- Selected-sketch insertion root-feature delta: `18 -> 18`
- Native BaseFlange/SMBaseFlange: absent
- SheetMetal: absent
- FlatPattern: absent
- Body count: `0`
- K=0.4 readback: not reached
- OpenDoc6: not attempted

Artifacts:

- v05 SLDPRT SHA256: `69BCC40CB4B92F248A554157908B4D34406D3363296D6998C0144C8B90791951`
- v05 evidence SHA256: `0CC48A17FE62B0D3A21FCA5CD4D1C2113FB9A4BD599543FF2C0F6054310E93DA`

The v05 SLDPRT is disposable and invalid for downstream use because it contains no body.

## Diagnostic correction

v05 used `PCBA=null` but reproduced the same no-feature failure as v04. Therefore the prior claim that insertion-time non-null PCBA was the high-confidence primary mechanism is no longer sufficient and has been downgraded.

The current confirmed evidence does not distinguish between:

- an `InsertSheetMetalBaseFlange2` COM exception swallowed by the old wrapper;
- a null API return caused by selection state;
- another source-level behavioral delta between the v03 and v05 source hashes; or
- SW2020 session/template nondeterminism.

## v06 static correction

v06 preserves the current native-only route and adds evidence at the API boundary:

- active sketch state;
- selected-object count and types;
- selected feature identity versus the expected sketch;
- exact insertion arguments;
- pre/post root-feature counts;
- returned-object nullness and COM/runtime type;
- returned feature name/type;
- exception type, HRESULT, and message.

No extrusion, ConvertToSheetMetal, alternate geometry, OpenDoc6, or retry path was added.
PCBA contribution and the native-creation root cause are both graded `UNRESOLVED` until an instrumented runtime produces direct evidence.

Hashes:

- v06 source SHA256: `9AFAADF127BCABB4547F5597765F2A808EEE22662ADB764537E5A9E46A5D83D3`
- v06 EXE SHA256: `F9A41D1EABDF2B181E29D406BE93751D12A2027C68B6C7B098126ED7FF90DBAC`
- v06 contract evidence SHA256: `1A89C0B47B85350BD4432BCAB00A0B01E3FA0F9EFDD71075617F9BC26B361198`

The contract check passed and started no CAD process. v06 SLDPRT and runtime evidence do not exist.

## Evidence

| Evidence | Status | SHA256 |
| --- | --- | --- |
| `data/locker_16029_shelf_min_native_v05_controlled_runtime_authorization_gate.json` | AUTHORIZED ONCE | `69BB51FEA41BF25A51193C9F40BBE3FE278199D709A4D2A02B22A9EE3E7EE57C` |
| `data/locker_16029_shelf_min_native_v05_controlled_runtime_gate.json` | INCOMPLETE | `88945CF4C0C41D3616843A6C4F9D77E8F52025DC85B367FBF79614C29A2A1F3D` |
| `data/locker_16029_shelf_min_native_v05_v03_creation_path_regression_diagnostic_gate.json` | PASS | `0AE6E84C6AE92279BF8A2425537234B2B0F50A98940CBFD7818AFD94AE36DB91` |
| `data/locker_16029_shelf_min_native_v06_builder_contract_check.json` | CONTRACT READY | `1A89C0B47B85350BD4432BCAB00A0B01E3FA0F9EFDD71075617F9BC26B361198` |
| `data/locker_16029_shelf_min_native_v06_insert_call_observability_correction_gate.json` | PASS | `888FD7475D8C58471AAF38206657230992342D599162E32EA52C747D5F332C87` |
| `data/locker_16029_shelf_min_native_v06_pid14112_residual_remediation_eligibility_gate.json` | PASS — eligibility only | `701F32790F9DAAA971D0690443F15912C96FA4DF3B9E5C18610E78F44539AF78` |
| `data/locker_16029_shelf_min_native_v06_pid14112_residual_remediation_gate.json` | PASS — natural exit, no termination executed | `68FC55625150BEBE82A98740D0CBCAA99A044CE0DE76DF84610A08BBD4C0FC2D` |
| `data/locker_16029_shelf_min_native_v06_runtime_preflight_after_residual_exit_gate.json` | PASS | `BB94A9A175A9BFB986F6B7D55D89F157422EC9E23682A2607881D980D45E26EF` |

## Process boundary

PID 14112 exited naturally before the authorized termination command. `Stop-Process` was not executed. The related process set is now empty.

## Next safe step

Obtain separate authorization for one v06 controlled run. If authorized, run exactly once with no automatic retry and classify the API-boundary evidence before any further parameter change.

Do not proceed to OpenDoc6, assembly/placement, exact/ClosestDistance, DXF/BOM, release, or full-cabinet work.
