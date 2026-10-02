# Original Drawing Door Modeling Rules

Updated: 2026-05-30

Purpose: keep the usable 16029 / 23035 / 24030 / 24067 door and cabinet rules in one clean notebook. This is rule evidence for modeling and review. It is not a released production drawing.

## Source Priority

Use sources in this order:

1. `23035 24-door side cabinet` and `23035 18-door side cabinet` production drawings. These are confirmed production-standard references for this order family.
2. `16029 standard locker 1917x1000x550`. This is the gold/source baseline for ordinary door, lock, shelf, front frame, and shell rules.
3. `24030 control main cabinet 1917x482x550`. Use only for 15-inch screen, RFID, camera/reader, electronics, service door, and wiring interface logic.
4. `24067 outdoor 14-door main cabinet 1939x1000x550`. Use for 14-door organization only. Do not copy outdoor height, rain shed, waterproof, or solar roof rules into the current indoor order by default.
5. Historical 1200W, 2117H, W537, SW2025, lightweight/review-only routes are evidence only. Do not use them as the current delivery route unless they are explicitly reopened and revalidated.

## Fixed Mainline Decisions

- CAD review mainline is SolidWorks 2020.
- FreeCAD may be used internally for parameterized evidence, preview, STEP, and bbox checks. It is not the engineer review software.
- Generated packages, screenshots, STEP/FCStd/SLDPRT/SLDASM outputs, and ZIP files are local artifacts and should not be committed by default.
- Current 16029 engineer-review scope remains 800W x 1917H x 550D, W337, LMS / SML / DUAL only.
- 1000W x 1917H x 550D with 10/12/14 doors is gold/source reference, not a waste line.

## Width Rule

For the standard two-column 16029 family:

```text
door_width = (outer_width - 46 - 80) / 2
column_center_x = +/- (outer_width / 4 + 8.5)
```

Interpretation:

- `46` means two side margins of 23 mm.
- `80` means the center control/lock gap.
- Examples: 800W -> W337, 1000W -> W437, 1200W -> W537.
- A new width is only a candidate until door panel, frame, hinge, lock, hook, shelf, bbox, and SolidWorks 2020 gates pass.

## Vertical Door Grid

For variable-height exercises, use the 12-unit grid unless a source drawing says otherwise:

```text
unit_pitch = 152.5
column_slot_units_sum = 12
installed_door_height = slot_units * 152.5 - 7
flat_door_height = installed_door_height + 36.4
top_boundary_clearance = 2
bottom_boundary_clearance = 2
internal_row_boundary = 2 + 3 + 2 = 7
```

Slot classes:

| Class | Units | Installed height | Flat height |
|---|---:|---:|---:|
| 1/12 | 1 | 145.5 | 181.9 |
| 2/12 | 2 | 298.0 | 334.4 |
| 3/12 | 3 | 450.5 | 486.9 |
| 4/12 | 4 | 603.0 | 639.4 |
| 5/12 | 5 | 755.5 | 791.9 |
| 6/12 | 6 | 908.0 | 944.4 |

Valid mixed stacks include `[6,4,2]` and `[2,4,6]` bottom-to-top. Block any column whose units do not sum to 12.

## Door, Shelf, and Frame Counts

For a two-column ordinary storage area:

```text
door_count = left_rows + right_rows
shelf_count = (left_rows - 1) + (right_rows - 1)
front_frame_horizontal_crossbar_count = shelf_count
one hinge set per door
one lock/hook relationship per door
left column uses left-hand door module
right column uses right-hand door module with identity transform
```

## Current Ordinary-Door Rule

The current known-good ordinary-door route is the `v35` route from the 740W mixed-stack test model:

- Left doors use left native panels; right doors use right native panels.
- The door panel itself is not mirrored by an accessory flag. The source panel must already match handedness.
- Stiffener, U-hook pad, latch plates, bushings, hinge pin, circlips, and electric lock hook all sit on the inner side of the door.
- Lower latch plate is placed by edge offset. Upper latch plate must be a separate baked mirrored part from the lower latch, not the same lower part rotated in assembly.
- Plastic bushing big face stays outward.
- Hinge pin and bushings stay on the outer side of the cabinet column.
- Electric lock hook and U-hook pad stay on the center lock-control side.

Validated v35 placement checks for the 740W mixed stack:

| Door class | Left hinge X | Right hinge X | Left lock X | Right lock X |
|---:|---:|---:|---:|---:|
| 2/12 | -337 | 337 | -55 | 55 |
| 4/12 | -337 | 337 | -55 | 55 |
| 6/12 | -337 | 337 | -55 | 55 |

Use `workers/solidworks_tools/bin/InspectAssemblyComponents.exe` as the SolidWorks 2020 assembly inspection gate. The older JScript inspection helper is not reliable for nested SolidWorks 2020 components when it returns zero components.

## Structural Follow Rules

- Internal shelf and front-frame crossbar positions follow the lower door boundary.
- Door rows are generated bottom-to-top by cumulative slot units.
- Door stack top/bottom and every internal gap must be checked numerically.
- Fixed center vertical structures do not scale with door height unless a source rule confirms that they should.

## Hard Gates Before Review

A generated door/cabinet model is not reviewable until all of these pass:

- Root bbox matches the requested nominal envelope or the envelope exception is explicitly documented.
- Door count, row count, row order, and per-row heights match the contract.
- Each mixed-height column sums to 12 units.
- Internal row boundaries preserve the 2 + 3 + 2 = 7 rule.
- Shelf and front-frame crossbar counts match row boundaries.
- Left/right handedness is correct.
- Hinge, lock, U-hook pad, and lock-control relationships stay attached to each door.
- No floating or outboard hardware.
- SolidWorks 2020 can open the generated review source.
- Screenshots/previews are readable before anything is sent out.
- Component-build helpers fail hard on missing source, open failure, add failure, transform creation failure, transform apply failure, or zero-component assembly inspection.
- Generated files stay local unless explicitly approved for Git.
