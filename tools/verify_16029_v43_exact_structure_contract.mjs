import { readFileSync, writeFileSync } from 'node:fs'
import { basename } from 'node:path'
import { pathToFileURL } from 'node:url'

export const V43_EXACT_STRUCTURE_GATE_SCHEMA = 'winnsen.locker16029.v43_exact_structure_gate.v1'

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function textOf(value) {
  return String(value ?? '')
}

function componentText(component) {
  return `${textOf(component?.name)} ${basename(textOf(component?.path))}`
}

function visibleComponents(record) {
  return asArray(record?.components).filter((component) => !component.is_hidden && !component.is_suppressed)
}

function topLevelVisibleComponents(record) {
  return visibleComponents(record).filter((component) => Number(component.depth) === 0)
}

function normalizedInstanceName(value) {
  return textOf(value).replace(/-\d+$/, '')
}

function countMatching(components, pattern) {
  return components.filter((component) => pattern.test(componentText(component))).length
}

function check(id, ok, actual, expected, details = {}) {
  return { id, ok, actual, expected, ...details }
}

function finiteNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function shelfSide(component) {
  const match = componentText(component).match(/箱体横层板([LR])焊接/i)
  return match?.[1]?.toUpperCase() || null
}

function shelfYMid(component) {
  const ymin = finiteNumber(component?.box?.ymin_mm)
  const ymax = finiteNumber(component?.box?.ymax_mm)
  return ymin === null || ymax === null ? null : (ymin + ymax) / 2
}

function clusterValues(values, tolerance) {
  const sorted = values.filter(Number.isFinite).sort((a, b) => a - b)
  const clusters = []
  for (const value of sorted) {
    const current = clusters[clusters.length - 1]
    if (!current || Math.abs(value - current.values[current.values.length - 1]) > tolerance) {
      clusters.push({ values: [value] })
    } else {
      current.values.push(value)
    }
  }
  return clusters.map((cluster) => ({
    centerMm: Math.round((cluster.values.reduce((sum, value) => sum + value, 0) / cluster.values.length) * 1000) / 1000,
    count: cluster.values.length,
    minMm: Math.round(Math.min(...cluster.values) * 1000) / 1000,
    maxMm: Math.round(Math.max(...cluster.values) * 1000) / 1000,
  }))
}

function nearestDistance(value, candidates) {
  if (!candidates.length) return Number.POSITIVE_INFINITY
  return Math.min(...candidates.map((candidate) => Math.abs(value - Number(candidate))))
}

function analyzeShelfBands(topLevel, shelfContract) {
  const toleranceMm = Number(shelfContract?.toleranceMm ?? 30)
  const clusterToleranceMm = Number(shelfContract?.clusterToleranceMm ?? toleranceMm)
  const shelfComponents = topLevel.filter((component) => /箱体横层板[LR]焊接/i.test(componentText(component)))
  const sides = {}
  const checks = []

  for (const side of ['L', 'R']) {
    const expected = asArray(shelfContract?.sides?.[side]).map(Number).filter(Number.isFinite)
    const rows = shelfComponents
      .filter((component) => shelfSide(component) === side)
      .map((component) => ({ name: component.name, yMidMm: shelfYMid(component) }))
      .filter((item) => item.yMidMm !== null)
    const clusters = clusterValues(rows.map((item) => item.yMidMm), clusterToleranceMm)
    const unexpected = clusters.filter((cluster) => nearestDistance(cluster.centerMm, expected) > toleranceMm)
    const missing = expected.filter((boundary) => nearestDistance(boundary, clusters.map((cluster) => cluster.centerMm)) > toleranceMm)
    sides[side] = { expectedBoundaryYmm: expected, observedBands: clusters, unexpectedBands: unexpected, missingBoundaries: missing }
    checks.push(check(
      `shelf_bands_${side.toLowerCase()}_exact`,
      unexpected.length === 0 && missing.length === 0,
      clusters.map((cluster) => cluster.centerMm),
      expected,
      { toleranceMm, unexpectedBands: unexpected, missingBoundaries: missing },
    ))
  }

  return { checks, sides }
}

export function analyzeV43ExactStructureContract({ structureRecord, contract } = {}) {
  if (!structureRecord) throw new Error('structureRecord is required')
  if (!contract) throw new Error('contract is required')

  const visible = visibleComponents(structureRecord)
  const topLevel = topLevelVisibleComponents(structureRecord)
  const expected = contract.expected || {}
  const checks = []

  const expectedDoorNames = asArray(expected.topLevelDoorModules?.names)
  const topLevelDoorModules = topLevel.filter((component) => /^储物柜门装配_[LR][0-9.p]+-\d+$/i.test(textOf(component.name)))
  const actualDoorNames = topLevelDoorModules.map((component) => normalizedInstanceName(component.name)).sort()
  const missingDoorNames = expectedDoorNames.filter((name) => !actualDoorNames.includes(name))
  const unexpectedDoorNames = actualDoorNames.filter((name) => !expectedDoorNames.includes(name))
  checks.push(check(
    'top_level_door_modules_exact',
    topLevelDoorModules.length === Number(expected.topLevelDoorModules?.exactCount) && missingDoorNames.length === 0 && unexpectedDoorNames.length === 0,
    actualDoorNames,
    expectedDoorNames,
    { missingDoorNames, unexpectedDoorNames },
  ))

  const lockTongueCount = countMatching(visible, /锁舌|lock[_ -]?tongue/i)
  checks.push(check(
    'door_lock_tongues_exact',
    lockTongueCount === Number(expected.doorLockTongues?.exactVisibleCount),
    lockTongueCount,
    Number(expected.doorLockTongues?.exactVisibleCount),
  ))

  const lockDatumComponents = topLevel.filter((component) => /锁孔基准|lock[_ -]?(?:mounting[_ -]?)?hole[_ -]?datum/i.test(componentText(component)))
  checks.push(check(
    'lock_hole_datums_exact',
    lockDatumComponents.length === Number(expected.lockHoleDatums?.exactTopLevelCount),
    lockDatumComponents.length,
    Number(expected.lockHoleDatums?.exactTopLevelCount),
    { examples: lockDatumComponents.slice(0, 30).map((component) => component.name) },
  ))

  const centerMaintenanceCount = countMatching(
    topLevel,
    /锁控维护条|锁控维护板|应急维护门|center[_ -]?lock[_ -]?maintenance[_ -]?sheet(?:metal)?[_ -]?strip/i,
  )
  checks.push(check(
    'center_maintenance_sheetmetal_exact',
    centerMaintenanceCount === Number(expected.centerMaintenanceSheetMetal?.exactTopLevelCount),
    centerMaintenanceCount,
    Number(expected.centerMaintenanceSheetMetal?.exactTopLevelCount),
  ))

  const electricalCount = countMatching(
    visible,
    /电控锁体|电控U型锁钩|锁控板装配组件|电路板支架|ZJA-S500|LK-4-6|M9 V1\.1|electric[_ -]?lock[_ -]?(?:body|hook)|lock[_ -]?control[_ -]?board/i,
  )
  checks.push(check(
    'electrical_or_electric_lock_components_exact',
    electricalCount === Number(expected.electricalOrElectricLockComponents?.exactVisibleCount),
    electricalCount,
    Number(expected.electricalOrElectricLockComponents?.exactVisibleCount),
  ))

  for (const role of asArray(expected.exactTopLevelRoles)) {
    const actual = topLevel.filter((component) => componentText(component).includes(textOf(role.contains))).length
    checks.push(check(`top_level_role_${role.id}_exact`, actual === Number(role.exactCount), actual, Number(role.exactCount)))
  }

  const shelfAnalysis = analyzeShelfBands(topLevel, expected.shelfBands)
  checks.push(...shelfAnalysis.checks)

  const failedChecks = checks.filter((item) => !item.ok)
  const pass = failedChecks.length === 0
  const statusBoundary = contract.statusBoundary || {}
  return {
    schema: V43_EXACT_STRUCTURE_GATE_SCHEMA,
    contractSchema: contract.schema,
    route: contract.route,
    status: pass ? 'PASS' : 'FAIL',
    classification: pass
      ? statusBoundary.passClassification || 'controlled_candidate_pass'
      : statusBoundary.failureClassification || 'needs_structure_revision',
    releaseEligible: statusBoundary.releaseEligible === true,
    summary: {
      checksTotal: checks.length,
      checksFailed: failedChecks.length,
      topLevelVisibleCount: topLevel.length,
      visibleComponentCount: visible.length,
      topLevelDoorModuleCount: topLevelDoorModules.length,
      doorLockTongueCount: lockTongueCount,
      lockHoleDatumTopLevelCount: lockDatumComponents.length,
      centerMaintenanceSheetMetalTopLevelCount: centerMaintenanceCount,
      electricalOrElectricLockComponentCount: electricalCount,
    },
    shelfBands: shelfAnalysis.sides,
    checks,
    failedChecks,
    manualValidationStillRequired: asArray(contract.manualValidationStillRequired),
    statusNote: statusBoundary.note || '',
  }
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    args[item.slice(2)] = argv[index + 1]
    index += 1
  }
  return args
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  if (!args.candidate || !args.contract) {
    throw new Error('Usage: node tools/verify_16029_v43_exact_structure_contract.mjs --candidate <structure_record.json> --contract <contract.json> [--out <result.json>]')
  }
  const result = analyzeV43ExactStructureContract({
    structureRecord: readJson(args.candidate),
    contract: readJson(args.contract),
  })
  const output = `${JSON.stringify(result, null, 2)}\n`
  if (args.out) writeFileSync(args.out, output, 'utf8')
  else process.stdout.write(output)
  return result
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const result = main()
    if (result.status !== 'PASS') process.exitCode = 1
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
