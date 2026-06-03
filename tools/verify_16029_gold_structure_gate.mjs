import { readFileSync, writeFileSync } from 'node:fs'
import { basename } from 'node:path'
import { pathToFileURL } from 'node:url'

export const GOLD_STRUCTURE_GATE_SCHEMA = 'winnsen.locker16029.gold_structure_gate.v1'

export const GOLD_SOURCE_BASELINE = Object.freeze({
  source: '1000W x 1917H x 550D gold/source SolidWorks 2020 assembly',
  componentCount: 422,
  doorCount: 12,
  topLevelVisibleCount: 40,
  maxDepth: 2,
  minComponentCountRatio: 0.6,
  minTopLevelVisibleRatio: 0.75,
})

const REAR_OVERLAY_PANEL_PATTERN = /\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d|centered[_ -]?back[_ -]?panel|back[_ -]?seam[_ -]?center/i
const LEFT_SIDE_WELDMENT_PATTERN = /\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5/i
const RIGHT_SIDE_WELDMENT_PATTERN = /\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5/i

const ROLE_CHECKS = Object.freeze([
  {
    id: 'cabinet_left_side_weldment',
    label: 'left side panel weldment',
    pattern: /\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'cabinet_right_side_weldment',
    label: 'right side panel weldment',
    pattern: /\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'cabinet_left_shelf_weldment',
    label: 'left shelf weldment',
    pattern: /\u7bb1\u4f53\u6a2a\u5c42\u677fL\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'cabinet_right_shelf_weldment',
    label: 'right shelf weldment',
    pattern: /\u7bb1\u4f53\u6a2a\u5c42\u677fR\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'cabinet_left_partition_weldment',
    label: 'left vertical partition weldment',
    pattern: /\u7bb1\u4f53\u7ad6\u9694\u677fL\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'cabinet_right_partition_weldment',
    label: 'right vertical partition weldment',
    pattern: /\u7bb1\u4f53\u7ad6\u9694\u677fR\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'front_frame_weldment',
    label: 'front frame weldment',
    pattern: /\u95e8\u6846\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'base_weldment',
    label: 'base weldment',
    pattern: /\u5e95\u5ea7\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'top_cover_weldment',
    label: 'top cover weldment',
    pattern: /\u4e0a\u76d6\u710a\u63a5/i,
    min: 1,
  },
  {
    id: 'lock_mounting_hole_datum',
    label: 'lock and electrical mounting hole datum',
    pattern: /\u9501\u5b54\u57fa\u51c6|\u9501\u5b89\u88c5\u5b54\u4f4d|lock[_ -]?(?:mounting[_ -]?)?hole[_ -]?datum/i,
    min: 1,
  },
  {
    id: 'door_lock_tongue',
    label: 'mechanical door lock tongue',
    pattern: /\u9501\u820c|lock[_ -]?tongue/i,
    min: 1,
  },
  {
    id: 'shelf_front_frame_locating_interface',
    label: 'shelf locating feet and front-frame notches',
    pattern: /\u5c42\u677f\u5b9a\u4f4d\u811a|\u95e8\u6846\u5b9a\u4f4d\u7f3a\u53e3|shelf[_ -]?locating|front[_ -]?frame[_ -]?locating[_ -]?notch/i,
    min: 1,
  },
  {
    id: 'inner_vertical_partition_stiffeners',
    label: 'inner vertical partition reinforcement plates',
    pattern: /\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f|partition[_ -]?stiffener/i,
    min: 4,
  },
  {
    id: 'leveling_feet',
    label: 'adjustable leveling feet',
    pattern: /\u8c03\u6574\u811a|\u8c03\u8282\u811a|level(?:l)?ing[_ -]?foot|level(?:l)?ing[_ -]?feet/i,
    min: 4,
  },
  {
    id: 'door_modules',
    label: 'locker door modules',
    pattern: /\u50a8\u7269\u67dc\u95e8.*\u88c5\u914d/i,
    min: 1,
  },
  {
    id: 'door_hinge_pins',
    label: 'door hinge pins',
    pattern: /\u95e8\u8f74\u9500/i,
    min: 1,
  },
])

const OPTIONAL_ROLE_CHECKS = Object.freeze([
  {
    id: 'service_access_door',
    label: 'service access door',
    pattern: /\u5e94\u6025\u7ef4\u62a4\u95e8|\u540e\u4e0b\u95e8\u677f/i,
    min: 1,
  },
])

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
  const componentPath = textOf(component?.path)
  return `${textOf(component?.name)} ${basename(componentPath)}`
}

function visibleComponents(record) {
  return asArray(record?.components).filter((component) => !component.is_hidden && !component.is_suppressed)
}

function topLevelVisibleComponents(record) {
  return visibleComponents(record).filter((component) => Number(component.depth) === 0)
}

function maxDepth(record) {
  return asArray(record?.components).reduce((max, component) => Math.max(max, Number(component.depth) || 0), 0)
}

function componentCount(record) {
  const explicit = Number(record?.component_count)
  return Number.isFinite(explicit) && explicit > 0 ? explicit : asArray(record?.components).length
}

function countMatching(record, pattern) {
  return visibleComponents(record).filter((component) => pattern.test(componentText(component))).length
}

function sidePanelBackSheetMetalSeam(record) {
  const components = visibleComponents(record).filter((component) => component?.box)
  const overlayPanels = components.filter((component) => REAR_OVERLAY_PANEL_PATTERN.test(componentText(component)))
  const left = components
    .filter((component) => LEFT_SIDE_WELDMENT_PATTERN.test(componentText(component)))
    .filter((component) => Number(component.box.xmin_mm) <= -360 && Number(component.box.ylen_mm) > 1700 && Number(component.box.zlen_mm) > 500)
    .sort((a, b) => Number(b.box.ylen_mm) - Number(a.box.ylen_mm))[0] || null
  const right = components
    .filter((component) => RIGHT_SIDE_WELDMENT_PATTERN.test(componentText(component)))
    .filter((component) => Number(component.box.xmax_mm) >= 360 && Number(component.box.ylen_mm) > 1700 && Number(component.box.zlen_mm) > 500)
    .sort((a, b) => Number(b.box.ylen_mm) - Number(a.box.ylen_mm))[0] || null

  const leftXMin = left ? Number(left.box.xmin_mm) : null
  const leftXMax = left ? Number(left.box.xmax_mm) : null
  const rightXMin = right ? Number(right.box.xmin_mm) : null
  const rightXMax = right ? Number(right.box.xmax_mm) : null
  const leftOuterOk = Number.isFinite(leftXMin) && Math.abs(leftXMin + 370) <= 2
  const rightOuterOk = Number.isFinite(rightXMax) && Math.abs(rightXMax - 370) <= 2
  const leftCenterLapOk = Number.isFinite(leftXMax) && leftXMax >= 0 && leftXMax <= 20
  const rightCenterEdgeOk = Number.isFinite(rightXMin) && Math.abs(rightXMin - 0.5) <= 2

  return {
    source: 'side_panel_sheetmetal_back_flange_bbox',
    overlayPanelCount: overlayPanels.length,
    overlayPanelNames: overlayPanels.map((component) => componentText(component)),
    hasLeftSidePanel: Boolean(left),
    hasRightSidePanel: Boolean(right),
    leftXMinMm: Number.isFinite(leftXMin) ? leftXMin : null,
    leftXMaxMm: Number.isFinite(leftXMax) ? leftXMax : null,
    rightXMinMm: Number.isFinite(rightXMin) ? rightXMin : null,
    rightXMaxMm: Number.isFinite(rightXMax) ? rightXMax : null,
    seamDatumXMm: Number.isFinite(rightXMin) ? rightXMin : null,
    leftOuterOk,
    rightOuterOk,
    leftCenterLapOk,
    rightCenterEdgeOk,
    centered: overlayPanels.length === 0 && Boolean(left) && Boolean(right) && leftOuterOk && rightOuterOk && leftCenterLapOk && rightCenterEdgeOk,
  }
}

function summarizeRecord(record) {
  const roleCounts = {}
  for (const check of [...ROLE_CHECKS, ...OPTIONAL_ROLE_CHECKS]) {
    roleCounts[check.id] = countMatching(record, check.pattern)
  }
  roleCounts.lock_control_strip_placeholder = countMatching(record, /\u9501\u63a7\u6761|lock[_ -]?control[_ -]?strip/i)
  roleCounts.electrical_or_electric_lock_component = countMatching(record, /\u7535\u63a7\u9501\u4f53|\u7535\u63a7U\u578b\u9501\u94a9|\u9501\u63a7\u677f\u88c5\u914d\u7ec4\u4ef6|\u7535\u8def\u677f\u652f\u67b6|ZJA-S500|LK-4-6|M9 V1\.1|electric[_ -]?lock[_ -]?(?:body|hook)|lock[_ -]?control[_ -]?board/i)
  roleCounts.provisional_parametric_scaffold = countMatching(record, /\u53c2\u6570\u5316|parametric|scaffold/i)
  roleCounts.external_through_hole_candidate = countMatching(record, /\u5916\u4fa7.*\u8d2f\u7a7f\u5b54|external[_ -]?through[_ -]?hole|outside[_ -]?face[_ -]?hole/i)

  return {
    componentCount: componentCount(record),
    topLevelVisibleCount: topLevelVisibleComponents(record).length,
    maxDepth: maxDepth(record),
    roleCounts,
  }
}

function issue(id, severity, message, details = {}) {
  return { id, severity, message, ...details }
}

function normalizeServiceAccessDoorPolicy(value = 'not_applicable') {
  const normalized = String(value || 'not_applicable').trim().toLowerCase().replace(/_/g, '-')
  if (normalized === 'required') return 'required'
  if (normalized === 'optional') return 'optional'
  if (normalized === 'not-applicable' || normalized === 'notapplicable' || normalized === 'omit' || normalized === 'omitted') {
    return 'not_applicable'
  }
  throw new Error(`Unsupported service access door policy: ${value}`)
}

function baselineFromGoldRecord(goldRecord = null) {
  if (!goldRecord) return { ...GOLD_SOURCE_BASELINE }
  const summary = summarizeRecord(goldRecord)
  return {
    ...GOLD_SOURCE_BASELINE,
    componentCount: Math.max(summary.componentCount, GOLD_SOURCE_BASELINE.componentCount),
    topLevelVisibleCount: Math.max(summary.topLevelVisibleCount, GOLD_SOURCE_BASELINE.topLevelVisibleCount),
    maxDepth: Math.max(summary.maxDepth, GOLD_SOURCE_BASELINE.maxDepth),
    roleCounts: summary.roleCounts,
  }
}

export function analyzeGoldStructureGate({
  candidateRecord,
  goldRecord = null,
  expectedDoorCount = 0,
  serviceAccessDoorPolicy = 'not_applicable',
} = {}) {
  if (!candidateRecord) throw new Error('candidateRecord is required')
  const baseline = baselineFromGoldRecord(goldRecord)
  const candidate = summarizeRecord(candidateRecord)
  const backSeam = sidePanelBackSheetMetalSeam(candidateRecord)
  const issues = []
  const warnings = []
  const servicePolicy = normalizeServiceAccessDoorPolicy(serviceAccessDoorPolicy)
  const expectedDoorCountNumber = Number(expectedDoorCount)
  const baselineDoorCount = Number(baseline.doorCount || GOLD_SOURCE_BASELINE.doorCount)
  const useDoorScaledComponentFloor = Number.isFinite(expectedDoorCountNumber) && expectedDoorCountNumber > 0 && baselineDoorCount > 0
  const componentCountScale = useDoorScaledComponentFloor
    ? Math.min(1, expectedDoorCountNumber / baselineDoorCount)
    : 1
  const minComponentCount = Math.floor(baseline.componentCount * baseline.minComponentCountRatio * componentCountScale)
  const minTopLevelVisibleCount = Math.floor(baseline.topLevelVisibleCount * baseline.minTopLevelVisibleRatio)

  if (candidate.componentCount < minComponentCount) {
    issues.push(issue(
      'component_count_below_gold_floor',
      'P0',
      useDoorScaledComponentFloor
        ? 'Candidate assembly is below the door-count-scaled 1000W gold/source component-count floor.'
        : 'Candidate assembly is far below the 1000W gold/source component-count floor.',
      {
        actual: candidate.componentCount,
        expectedMinimum: minComponentCount,
        goldComponentCount: baseline.componentCount,
        goldDoorCount: baselineDoorCount,
        expectedDoorCount: useDoorScaledComponentFloor ? expectedDoorCountNumber : 0,
        comparisonMode: useDoorScaledComponentFloor ? 'door_count_scaled_gold_floor' : 'absolute_gold_floor',
      },
    ))
  }

  if (candidate.topLevelVisibleCount < minTopLevelVisibleCount) {
    issues.push(issue(
      'top_level_visible_count_below_gold_floor',
      'P0',
      'Candidate top-level visible component count is below the 1000W gold/source floor.',
      { actual: candidate.topLevelVisibleCount, expectedMinimum: minTopLevelVisibleCount, goldTopLevelVisibleCount: baseline.topLevelVisibleCount },
    ))
  }

  if (candidate.maxDepth < baseline.maxDepth) {
    issues.push(issue(
      'assembly_depth_below_gold_floor',
      'P0',
      'Candidate assembly hierarchy is flatter than the 1000W gold/source assembly.',
      { actual: candidate.maxDepth, expectedMinimum: baseline.maxDepth },
    ))
  }

  for (const check of ROLE_CHECKS) {
    const actual = Number(candidate.roleCounts[check.id] || 0)
    const expectedMinimum = check.id === 'door_modules' && expectedDoorCount > 0
      ? Math.min(check.min, expectedDoorCount)
      : check.id === 'lock_mounting_hole_datum' && expectedDoorCount > 0
      ? expectedDoorCount
      : check.id === 'door_lock_tongue' && expectedDoorCount > 0
      ? expectedDoorCount
      : check.min
    if (actual < expectedMinimum) {
      issues.push(issue(
        `missing_${check.id}`,
        'P0',
        `Candidate is missing required 1000W gold/source structure role: ${check.label}.`,
        { actual, expectedMinimum },
      ))
    }
  }

  if (!backSeam.centered) {
    issues.push(issue(
      'back_panel_seam_not_centered',
      'P0',
      'Candidate rear back seam must be centered by the cabinet side-panel sheet metal, not by added rear overlay panels.',
      backSeam,
    ))
  }

  for (const check of OPTIONAL_ROLE_CHECKS) {
    const actual = Number(candidate.roleCounts[check.id] || 0)
    if (servicePolicy === 'required' && actual < check.min) {
      issues.push(issue(
        `missing_required_${check.id}`,
        'P0',
        `Candidate is missing required variant structure role: ${check.label}.`,
        { actual, expectedMinimum: check.min },
      ))
    } else if (servicePolicy === 'optional' && actual < check.min) {
      warnings.push(issue(
        `missing_optional_${check.id}`,
        'P2',
        `Candidate has no visible ${check.label}; verify whether this variant intentionally omits it.`,
        { actual, expectedMinimum: check.min },
      ))
    }
  }

  if (candidate.roleCounts.provisional_parametric_scaffold > 0) {
    issues.push(issue(
      'provisional_parametric_scaffold_components',
      'P1',
      'Candidate contains provisional parametric/scaffold components and cannot be treated as gold-structure equivalent.',
      { count: candidate.roleCounts.provisional_parametric_scaffold },
    ))
  }

  if (candidate.roleCounts.lock_control_strip_placeholder > 0) {
    issues.push(issue(
      'legacy_lock_control_strip_placeholder',
      'P1',
      'Candidate uses legacy lock-control strip placeholders; generated models should expose lock-hole datum geometry instead of lock-control parts.',
      { lockStripCount: candidate.roleCounts.lock_control_strip_placeholder },
    ))
  }

  if (candidate.roleCounts.electrical_or_electric_lock_component > 0) {
    issues.push(issue(
      'electrical_or_electric_lock_components_present',
      'P1',
      'Candidate contains electrical or electric-lock components; generated structure packages should keep only sheet-metal structure and hole/interface datums.',
      { count: candidate.roleCounts.electrical_or_electric_lock_component },
    ))
  }

  if (candidate.roleCounts.external_through_hole_candidate > 0) {
    issues.push(issue(
      'external_through_hole_candidates_present',
      'P0',
      'Candidate exposes exterior top/side/rear through-hole candidates; these datums must stay internal-only.',
      { count: candidate.roleCounts.external_through_hole_candidate },
    ))
  }

  return {
    schema: GOLD_STRUCTURE_GATE_SCHEMA,
    status: issues.length ? 'FAIL' : 'PASS',
    baseline: {
      source: baseline.source,
      componentCount: baseline.componentCount,
      doorCount: baselineDoorCount,
      topLevelVisibleCount: baseline.topLevelVisibleCount,
      maxDepth: baseline.maxDepth,
      minComponentCount,
      componentCountComparisonMode: useDoorScaledComponentFloor ? 'door_count_scaled_gold_floor' : 'absolute_gold_floor',
      componentCountScale,
      minTopLevelVisibleCount,
      serviceAccessDoorPolicy: servicePolicy,
    },
    candidate,
    expectedDoorCount,
    summary: {
      componentCount: candidate.componentCount,
      topLevelVisibleCount: candidate.topLevelVisibleCount,
      maxDepth: candidate.maxDepth,
      lockMountingHoleDatumCount: candidate.roleCounts.lock_mounting_hole_datum,
      doorLockTongueCount: candidate.roleCounts.door_lock_tongue,
      lockControlStripPlaceholderCount: candidate.roleCounts.lock_control_strip_placeholder,
      electricalOrElectricLockComponentCount: candidate.roleCounts.electrical_or_electric_lock_component,
      provisionalParametricScaffoldCount: candidate.roleCounts.provisional_parametric_scaffold,
      shelfFrontFrameLocatingInterfaceCount: candidate.roleCounts.shelf_front_frame_locating_interface,
      innerVerticalPartitionStiffenerCount: candidate.roleCounts.inner_vertical_partition_stiffeners,
      externalThroughHoleCandidateCount: candidate.roleCounts.external_through_hole_candidate,
      levelingFeetCount: candidate.roleCounts.leveling_feet,
      doorModuleCount: candidate.roleCounts.door_modules,
      serviceAccessDoorCount: candidate.roleCounts.service_access_door,
      serviceAccessDoorPolicy: servicePolicy,
      centeredBackSeam: backSeam,
      issueCount: issues.length,
      warningCount: warnings.length,
      p0Count: issues.filter((item) => item.severity === 'P0').length,
      p1Count: issues.filter((item) => item.severity === 'P1').length,
      p2WarningCount: warnings.filter((item) => item.severity === 'P2').length,
    },
    issues,
    warnings,
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
  if (!args.candidate) {
    throw new Error('Usage: node tools/verify_16029_gold_structure_gate.mjs --candidate <structure_record.json> [--gold <gold_structure_record.json>] [--expected-door-count <n>] [--service-access-door-policy not-applicable|optional|required] [--out <result.json>]')
  }
  const result = analyzeGoldStructureGate({
    candidateRecord: readJson(args.candidate),
    goldRecord: args.gold ? readJson(args.gold) : null,
    expectedDoorCount: Number(args['expected-door-count'] || 0),
    serviceAccessDoorPolicy: args['service-access-door-policy'] || 'not_applicable',
  })
  const text = `${JSON.stringify(result, null, 2)}\n`
  if (args.out) writeFileSync(args.out, text, 'utf8')
  else process.stdout.write(text)
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
