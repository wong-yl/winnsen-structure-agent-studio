import { planTemplateFullAssemblyRequest } from './locker_16029_template_rules.mjs'

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
      placementCount: 14,
      outputToken: '740W_L6-4-2_R2-4-6',
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
      placementCount: 14,
      outputToken: '800W_L6-4-2_R2-4-6',
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
      placementCount: 14,
      outputToken: '1000W_L2-4-6_R6-4-2',
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
      placementCount: 16,
      outputToken: '900W_L3-3-3-3_R4-4-4',
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
  assertEqual(plan.derived.outputToken, expect.outputToken, `${testCase.name} output token`)
  assertEqual(plan.columns.length, 2, `${testCase.name} column count`)
  for (const column of plan.columns) {
    const sum = column.units.reduce((total, unit) => total + unit, 0)
    assertAlmostEqual(sum, 12, `${testCase.name} ${column.side} column ratio sum`)
  }
  return {
    name: testCase.name,
    mode: plan.route.mode,
    compatibleWithNativeTemplate: plan.derived.compatibleWithNativeTemplate,
    doorCount: plan.derived.doorCount,
    doorWidthMm: plan.derived.doorWidthMm,
    outputToken: plan.derived.outputToken,
    placements: plan.placements.length,
    boundary: plan.boundary,
  }
})

console.log(JSON.stringify({
  status: 'PASS',
  cases: results.length,
  results,
}, null, 2))
