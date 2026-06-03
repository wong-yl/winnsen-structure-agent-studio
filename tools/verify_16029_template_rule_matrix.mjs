import { planTemplateFullAssemblyRequest } from './locker_16029_template_rules.mjs'
import { buildFullCandidatePlacementRows } from './locker_16029_gold_module_targets.mjs'

const templatePlacementText = [
  ['role', 'path', 'tx_mm', 'ty_mm', 'tz_mm', 'rotation'],
  ['gold_source_shell_frame_shelf', 'C:/gold/candidate_16029_740W_gold_shell_frame_shelf_only_v37.SLDPRT', '0', '0', '0', '1,0,0,0,1,0,0,0,1'],
  ['gold_electronics_module', 'C:/gold/gold_electronics_module.SLDPRT', '0', '0', '0', '1,0,0,0,1,0,0,0,1'],
  ['L_row01_6_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_6_12_W307_left_v43_variable_rib_restored_tongue.SLDASM', '-193.5', '486', '0', '1,0,0,0,1,0,0,0,1'],
  ['L_row02_4_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_4_12_W307_left_v43_variable_rib_restored_tongue.SLDASM', '-193.5', '1248.5', '0', '1,0,0,0,1,0,0,0,1'],
  ['L_row03_2_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_2_12_W307_left_v43_variable_rib_restored_tongue.SLDASM', '-193.5', '1706', '0', '1,0,0,0,1,0,0,0,1'],
  ['R_row01_2_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_2_12_W307_right_v43_variable_rib_restored_tongue.SLDASM', '193.5', '181', '0', '1,0,0,0,1,0,0,0,1'],
  ['R_row02_4_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_4_12_W307_right_v43_variable_rib_restored_tongue.SLDASM', '193.5', '638.5', '0', '1,0,0,0,1,0,0,0,1'],
  ['R_row03_6_12_door_module', 'C:/gold/v43_doors_variable_rib_restored_tongue/gold_ordinary_door_6_12_W307_right_v43_variable_rib_restored_tongue.SLDASM', '193.5', '1401', '0', '1,0,0,0,1,0,0,0,1'],
  ['cabinet_lock_body_L_row01', 'C:/gold/electric_lock_body_zja_s500_from_23035_18door.SLDPRT', '-55.2', '486', '-101.5', '1,0,0,0,1,0,0,0,1'],
].map((row) => row.join('\t')).join('\n') + '\n'

const cases = [
  {
    name: 'exact_verified_740_l642_r246',
    input: {
      cabinetWidthMm: 740,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 6,
      rowSequence: 'L642-R246',
    },
    expect: {
      mode: 'native_template_pack_and_go',
      compatible: true,
      doorWidthMm: 307,
      doorCount: 6,
      placementCount: 13,
      outputToken: '740W_L6-4-2_R2-4-6',
      bindingStatus: 'ready_from_template_modules',
      missingUnits: [],
      fullCandidateDoorCount: 6,
      fullCandidateLockCount: 0,
      placementDatumCount: 6,
      firstLeftDoorX: -193.5,
      firstRightDoorX: 193.5,
    },
  },
  {
    name: 'handoff_width_800_l642_r246',
    input: {
      cabinetWidthMm: 800,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 6,
      rowSequence: 'L642-R246',
    },
    expect: {
      mode: 'template_rule_parameter_plan',
      compatible: false,
      doorWidthMm: 337,
      doorCount: 6,
      placementCount: 13,
      outputToken: '800W_L6-4-2_R2-4-6',
      bindingStatus: 'ready_from_template_modules',
      missingUnits: [],
      fullCandidateDoorCount: 6,
      fullCandidateLockCount: 0,
      placementDatumCount: 6,
      firstLeftDoorX: -208.5,
      firstRightDoorX: 208.5,
    },
  },
  {
    name: 'gold_source_width_1000_reversed_columns',
    input: {
      cabinetWidthMm: 1000,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 6,
      rowSequence: 'L246-R642',
    },
    expect: {
      mode: 'template_rule_parameter_plan',
      compatible: false,
      doorWidthMm: 437,
      doorCount: 6,
      placementCount: 13,
      outputToken: '1000W_L2-4-6_R6-4-2',
      bindingStatus: 'ready_from_template_modules',
      missingUnits: [],
      fullCandidateDoorCount: 6,
      fullCandidateLockCount: 0,
      placementDatumCount: 6,
      firstLeftDoorX: -258.5,
      firstRightDoorX: 258.5,
    },
  },
  {
    name: 'custom_seven_door_ratio_split',
    input: {
      cabinetWidthMm: 900,
      cabinetHeightMm: 1917,
      cabinetDepthMm: 550,
      columns: 2,
      doorCount: 7,
      rowSequence: 'L3333-R444',
    },
    expect: {
      mode: 'template_rule_parameter_plan',
      compatible: false,
      doorWidthMm: 387,
      doorCount: 7,
      placementCount: 15,
      outputToken: '900W_L3-3-3-3_R4-4-4',
      bindingStatus: 'needs_native_door_generation',
      missingUnits: ['3'],
      fullCandidateDoorCount: 3,
      fullCandidateLockCount: 0,
      placementDatumCount: 7,
      firstLeftDoorX: null,
      firstRightDoorX: 233.5,
      generatedDoorModules: [
        { side: 'L', unit: '3', assembly: 'C:/generated/review_single_ordinary_door_W387_H450p5_left.SLDASM' },
      ],
      generatedFullCandidateDoorCount: 7,
    },
  },
]

function assertEqual(actual, expected, message) {
  if (actual !== expected) {
    throw new Error(`${message}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`)
  }
}

function assertAlmostEqual(actual, expected, message) {
  if (Math.abs(Number(actual) - Number(expected)) > 0.001) {
    throw new Error(`${message}: expected ${expected}, got ${actual}`)
  }
}

const results = cases.map((testCase) => {
  const plan = planTemplateFullAssemblyRequest(testCase.input)
  const { expect } = testCase
  assertEqual(plan.route.mode, expect.mode, `${testCase.name} route.mode`)
  assertEqual(plan.route.directAssemblyInsertion, false, `${testCase.name} direct assembly route must stay disabled`)
  assertEqual(plan.route.freeCadRole, 'internal evidence and parameter assistance only', `${testCase.name} FreeCAD role`)
  assertEqual(plan.derived.compatibleWithNativeTemplate, expect.compatible, `${testCase.name} native template compatibility`)
  assertAlmostEqual(plan.derived.doorWidthMm, expect.doorWidthMm, `${testCase.name} door width`)
  assertEqual(plan.derived.doorCount, expect.doorCount, `${testCase.name} door count`)
  assertEqual(plan.placements.length, expect.placementCount, `${testCase.name} placement count`)
  assertEqual(plan.placements.filter((row) => /^lock_mounting_hole_datum_/i.test(row.role)).length, expect.placementDatumCount, `${testCase.name} lock-hole datum placement count`)
  assertEqual(plan.placements.some((row) => /electric_lock_body|gold_electronics_module|template_electric_lock_body/i.test(`${row.role} ${row.source}`)), false, `${testCase.name} plan must not emit electrical/electric-lock placements`)
  assertEqual(plan.derived.outputToken, expect.outputToken, `${testCase.name} output token`)
  assertEqual(plan.derived.doorModuleBinding.status, expect.bindingStatus, `${testCase.name} native door module binding status`)
  assertEqual(JSON.stringify(plan.derived.doorModuleBinding.missingUnits), JSON.stringify(expect.missingUnits), `${testCase.name} missing native door module units`)
  assertEqual(plan.derived.sheetMetalRuleEvidence.status, 'ready_for_rule_plan_binding', `${testCase.name} sheet-metal rule binding status`)
  assertAlmostEqual(plan.derived.sheetMetalRuleEvidence.doorFlatWidthExtraMm, 36.4, `${testCase.name} sheet-metal flat-width extra`)
  assertAlmostEqual(plan.derived.sheetMetalRuleEvidence.doorFlatHeightExtraMm, 36.4, `${testCase.name} sheet-metal flat-height extra`)
  assertEqual(plan.derived.sheetMetalRuleEvidence.commonHoleStatus, 'ready_from_gold_dxf', `${testCase.name} sheet-metal common hole status`)
  assertEqual(plan.columns.length, 2, `${testCase.name} column count`)
  for (const column of plan.columns) {
    const sum = column.units.reduce((total, unit) => total + unit, 0)
    assertAlmostEqual(sum, 12, `${testCase.name} ${column.side} column ratio sum`)
    for (const row of column.rows) {
      assertEqual(row.sheetMetal.status, 'bound_to_1000w_gold_dxf', `${testCase.name} ${column.side} ${row.label} sheet-metal row binding`)
      assertAlmostEqual(row.sheetMetal.generatedFlatWidthMm, plan.derived.doorWidthMm + 36.4, `${testCase.name} ${column.side} ${row.label} generated flat width`)
      assertAlmostEqual(row.sheetMetal.generatedFlatHeightMm, row.heightMm + 36.4, `${testCase.name} ${column.side} ${row.label} generated flat height`)
      if (Number(row.sheetMetal.holeDatumCount) < 2) {
        throw new Error(`${testCase.name} ${column.side} ${row.label} must carry at least two DXF hole datum records`)
      }
    }
  }
  const fullCandidateRows = buildFullCandidatePlacementRows({ targets: [], templatePlacementText, plan })
  assertEqual(fullCandidateRows.some((row) => /shell_frame_shelf/i.test(`${row.role} ${row.path}`)), false, `${testCase.name} full candidate must remove shell black-box`)
  assertEqual(fullCandidateRows.some((row) => /gold_electronics_module|electric_lock_body|cabinet_lock_body/i.test(`${row.role} ${row.path}`)), false, `${testCase.name} full candidate must omit electrical/electric-lock rows`)
  const doorRows = fullCandidateRows.filter((row) => /_door_module/i.test(row.role))
  const lockRows = fullCandidateRows.filter((row) => /^cabinet_lock_body_/i.test(row.role))
  assertEqual(doorRows.length, expect.fullCandidateDoorCount, `${testCase.name} full candidate door row count`)
  assertEqual(lockRows.length, expect.fullCandidateLockCount, `${testCase.name} full candidate lock row count`)
  const firstLeftDoor = doorRows.find((row) => /^L_row01_/i.test(row.role))
  const firstRightDoor = doorRows.find((row) => /^R_row01_/i.test(row.role))
  if (expect.firstLeftDoorX === null) {
    assertEqual(Boolean(firstLeftDoor), false, `${testCase.name} unsupported left 3/12 door must not be emitted as build-ready`)
  } else {
    assertAlmostEqual(firstLeftDoor?.tx_mm, expect.firstLeftDoorX, `${testCase.name} first left door candidate X`)
  }
  assertAlmostEqual(firstRightDoor?.tx_mm, expect.firstRightDoorX, `${testCase.name} first right door candidate X`)
  if (expect.generatedDoorModules) {
    const generatedRows = buildFullCandidatePlacementRows({
      targets: [],
      templatePlacementText,
      plan,
      generatedDoorModules: { modules: expect.generatedDoorModules },
    })
    const generatedDoorRows = generatedRows.filter((row) => /_door_module/i.test(row.role) || /generated_native_door_module/i.test(row.role))
    const generatedLeftDoor = generatedRows.find((row) => /^L_row01_3_12_generated_native_door_module$/i.test(row.role))
    assertEqual(generatedDoorRows.length, expect.generatedFullCandidateDoorCount, `${testCase.name} generated native door fallback row count`)
    assertEqual(generatedLeftDoor?.path, expect.generatedDoorModules[0].assembly, `${testCase.name} generated native door fallback path`)
    assertAlmostEqual(generatedLeftDoor?.tx_mm, -233.5, `${testCase.name} generated native left door candidate X`)
  }
  return {
    name: testCase.name,
    mode: plan.route.mode,
    compatibleWithNativeTemplate: plan.derived.compatibleWithNativeTemplate,
    nativeDoorModuleBindingStatus: plan.derived.doorModuleBinding.status,
    missingNativeDoorModuleUnits: plan.derived.doorModuleBinding.missingUnits,
    doorCount: plan.derived.doorCount,
    doorWidthMm: plan.derived.doorWidthMm,
    outputToken: plan.derived.outputToken,
    placements: plan.placements.length,
    fullCandidateDoorRows: doorRows.length,
    fullCandidateLockRows: lockRows.length,
    boundary: plan.boundary,
    sheetMetalRuleEvidence: plan.derived.sheetMetalRuleEvidence,
  }
})

console.log(JSON.stringify({
  status: 'PASS',
  cases: results.length,
  results,
}, null, 2))
