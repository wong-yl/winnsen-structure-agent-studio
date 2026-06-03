import { readFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'
import { GOLD_SHEETMETAL_RULES_SCHEMA } from './build_16029_gold_sheetmetal_rules.mjs'

const REQUIRED_CLASSES = Object.freeze(['1/12', '2/12', '3/12', '4/12', '5/12', '6/12'])

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function almostEqual(a, b, tolerance = 0.001) {
  return Math.abs(Number(a) - Number(b)) <= tolerance
}

function issue(id, severity, message, details = {}) {
  return { id, severity, message, ...details }
}

export function analyzeGoldSheetmetalRules(rules) {
  const issues = []
  if (rules?.schema !== GOLD_SHEETMETAL_RULES_SCHEMA) {
    issues.push(issue('schema_mismatch', 'P0', 'Gold sheet-metal rules schema is not recognized.', {
      actual: rules?.schema,
      expected: GOLD_SHEETMETAL_RULES_SCHEMA,
    }))
  }

  for (const classId of REQUIRED_CLASSES) {
    const row = rules?.doorClasses?.[classId]
    if (!row) {
      issues.push(issue(`missing_door_class_${classId.replace('/', '_')}`, 'P0', `Missing door sheet-metal rule for ${classId}.`))
      continue
    }
    if (!row.sourceDxf) {
      issues.push(issue(`missing_source_dxf_${classId.replace('/', '_')}`, 'P0', `Door sheet-metal rule ${classId} has no source DXF.`))
    }
    if (asNumber(row.holeCount) < 2) {
      issues.push(issue(`too_few_hole_datums_${classId.replace('/', '_')}`, 'P1', `Door sheet-metal rule ${classId} has too few hole candidates.`, {
        actual: asNumber(row.holeCount),
      }))
    }
    const expectedHeight = asNumber(row.slotUnits) * asNumber(rules?.baseline?.unitPitchMm) - asNumber(rules?.baseline?.visualGapMm)
    if (!almostEqual(row.baselineInstalledHeightMm, expectedHeight, 0.01)) {
      issues.push(issue(`installed_height_rule_mismatch_${classId.replace('/', '_')}`, 'P0', `Door class ${classId} does not match the 12-unit height rule.`, {
        actual: row.baselineInstalledHeightMm,
        expected: expectedHeight,
      }))
    }
  }

  if (!almostEqual(rules?.baseline?.doorFlatWidthExtraMm, 36.4, 0.01)) {
    issues.push(issue('flat_width_extra_mismatch', 'P0', 'Door flat-width extra is not the 1000W gold DXF value.', {
      actual: rules?.baseline?.doorFlatWidthExtraMm,
      expected: 36.4,
    }))
  }
  if (!almostEqual(rules?.baseline?.doorFlatHeightExtraMm, 36.4, 0.01)) {
    issues.push(issue('flat_height_extra_mismatch', 'P0', 'Door flat-height extra is not the 1000W gold DXF value.', {
      actual: rules?.baseline?.doorFlatHeightExtraMm,
      expected: 36.4,
    }))
  }
  if (!almostEqual(rules?.doorClasses?.['3/12']?.baselineInstalledHeightMm, 450.5, 0.01)) {
    issues.push(issue('three_twelfth_height_mismatch', 'P0', '3/12 native generated door height does not match gold DXF-derived rule.', {
      actual: rules?.doorClasses?.['3/12']?.baselineInstalledHeightMm,
      expected: 450.5,
    }))
  }

  const common = rules?.commonDoorHoleRules || {}
  if (common.status !== 'ready_from_gold_dxf') {
    issues.push(issue('common_hole_rules_not_ready', 'P1', 'Common door hole rules are not ready from gold DXF.', {
      actual: common.status,
    }))
  }
  if (!almostEqual(common.hingePinHoleDiameterMm, 10.2, 0.25)) {
    issues.push(issue('hinge_hole_diameter_mismatch', 'P1', 'Common hinge/axis hole diameter is not the expected gold DXF value.', {
      actual: common.hingePinHoleDiameterMm,
      expected: 10.2,
    }))
  }
  if (!almostEqual(common.hingeHoleFromSideMm, 28.2, 0.25)) {
    issues.push(issue('hinge_hole_side_offset_mismatch', 'P1', 'Common hinge/axis hole side offset is not the expected gold DXF value.', {
      actual: common.hingeHoleFromSideMm,
      expected: 28.2,
    }))
  }

  return {
    schema: 'winnsen.locker16029.gold_sheetmetal_rules_gate.v1',
    status: issues.length ? 'FAIL' : 'PASS',
    summary: {
      doorClassCount: Object.keys(rules?.doorClasses || {}).length,
      classes: Object.keys(rules?.doorClasses || {}),
      flatWidthExtraMm: rules?.baseline?.doorFlatWidthExtraMm,
      flatHeightExtraMm: rules?.baseline?.doorFlatHeightExtraMm,
      totalDoorHoleCount: asNumber(rules?.summary?.totalDoorHoleCount),
      commonHoleStatus: common.status,
      issueCount: issues.length,
      p0Count: issues.filter((item) => item.severity === 'P0').length,
      p1Count: issues.filter((item) => item.severity === 'P1').length,
    },
    issues,
  }
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    if (index + 1 < argv.length && !argv[index + 1].startsWith('--')) {
      args[item.slice(2)] = argv[index + 1]
      index += 1
    } else {
      args[item.slice(2)] = true
    }
  }
  return args
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  const rulesPath = args.rules || 'data/locker_16029_gold_sheetmetal_rules.json'
  const result = analyzeGoldSheetmetalRules(readJson(rulesPath))
  if (args.json) {
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
  } else {
    process.stdout.write(`16029 gold sheet-metal rules gate: ${result.status}\n`)
    process.stdout.write(`classes: ${result.summary.classes.join(', ')}; holes: ${result.summary.totalDoorHoleCount}; issues: ${result.summary.issueCount}\n`)
    for (const item of result.issues) {
      process.stdout.write(`- ${item.severity} ${item.id}: ${item.message}\n`)
    }
  }
  if (result.status !== 'PASS') process.exitCode = 1
  return result
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
