# 16029 shelf_min_native v07 unified handoff

Current state: v06 controlled runtime is INCOMPLETE. v07 static correction and clean runtime preflight are ready, but v07 has not been run.

## Result

- v06 ran exactly once; exit code `10`; no retry.
- The selected fallback used exactly one selected `草图1 / ProfileFeature`, matching the expected sketch by COM identity.
- `InsertSheetMetalBaseFlange2` completed without exception but returned `null`.
- No BaseFlange, SheetMetal, FlatPattern, or body was created.
- v06 SLDPRT is disposable and invalid for downstream use.
- v06 left `sldProcMon PID 11340`, attributable to SolidWorks PID `6276`.

## v06 artifacts

- SLDPRT SHA256: `2A803251E87F26B2CF5D095C5045E9845A203E093433111292CAA189063EAD79`
- Evidence SHA256: `1A16129C1C9206A20A7266DD056486C39795826D617947B4B59952DB41876773`
- Runtime gate SHA256: `6B96FC7C684C2DD9883FC681835A62EFF2F5F1E0EE82293C0D875C839A0ECF9F`

## Diagnostic conclusion

The v06 failure is now narrowed to an unexplained API null return:

- no swallowed exception;
- no wrong selected-sketch identity;
- PCBA is not a sufficient root cause;
- exact null-return cause remains unresolved.

The v06 evidence mislabeled parameters 16–19. Based on the local SW2020 Interop signature, the actual values were:

- `UseReliefRatio=false`
- `Merge=true`
- `UseFeatScope=false`
- `UseAutoSelect=true`

## v07 static correction

v07 preserves the call values and records all 19 arguments under their exact Interop names.

- Source SHA256: `EC810795350CF1F33330EAE67C4D0698FFE9F19C8B772141050F71E9781540BB`
- EXE SHA256: `29A5B528D2A604095A1AB1B975D70A230DB944B65C7FAA6CAEE8FE027D5824DF`
- Contract SHA256: `4DB4BED91D5CE7CFBC2493ACA34C2864E09374750F1E0273F09C65BD7DFE46F7`
- Static gate SHA256: `BCDD8E8609A0EC432095435DC665D7D082CE05AB493616F9F7E771F12D1A2845`
- Contract status: `SHELF_MIN_NATIVE_BUILDER_CONTRACT_READY`
- CAD started by compile/contract-check: no
- v07 outputs exist: no

## Current blocker

PID 11340 exited naturally before the authorized termination command. `Stop-Process` was not executed. The related process set is empty.

Cleanup eligibility gate SHA256: `B8D73F3F8ED361A78F75087424C3A2837C5B84A7B7312044F5AC9B337AEAC091`

Next safe target:

`SHELF_MIN_NATIVE_V07_CONTROLLED_RUNTIME_AUTHORIZATION_GATE`

Do not run v07 automatically after cleanup. A clean preflight and separate one-run authorization are required. Do not proceed to OpenDoc6, assembly, exact/ClosestDistance, DXF/BOM, release, or full-cabinet work.
