import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

export const GOLD_SHEETMETAL_RULES_SCHEMA = 'winnsen.locker16029.gold_sheetmetal_rules.v1'

const DEFAULT_EVIDENCE = 'data/locker_16029_gold_sheetmetal_evidence.json'
const DEFAULT_JSON = 'data/locker_16029_gold_sheetmetal_rules.json'
const DEFAULT_MD = 'data/locker_16029_gold_sheetmetal_rules.md'

const BASELINE = Object.freeze({
  productFamily: '16029',
  sourceWidthMm: 1000,
  sourceHeightMm: 1917,
  sourceDepthMm: 550,
  baselineDoorWidthMm: 437,
  totalVerticalUnits: 12,
  unitPitchMm: 152.5,
  visualGapMm: 7,
})

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function compactNumber(value) {
  return Number(value).toFixed(3).replace(/\.?0+$/, '')
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value, fallback = 0) {
  const number = Number(value)
  return Number.isFinite(number) ? number : fallback
}

function expectedInstalledHeight(slotUnits) {
  return roundMm(slotUnits * BASELINE.unitPitchMm - BASELINE.visualGapMm)
}

function pickDoorPanelRows(evidence) {
  return asArray(evidence.dxf_files)
    .filter((item) => item.source_bucket === 'current_flat_pattern')
    .filter((item) => asArray(item.roles).includes('door_panel'))
    .filter((item) => /^\d+\/12$/.test(String(item.door_ratio || '')))
    .filter((item) => {
      const units = Number(String(item.door_ratio).split('/')[0])
      return units >= 1 && units <= 6
    })
    .sort((a, b) => {
      const au = Number(String(a.door_ratio).split('/')[0])
      const bu = Number(String(b.door_ratio).split('/')[0])
      return au - bu || String(a.relative_path).localeCompare(String(b.relative_path))
    })
}

function normalizeHole(hole, bbox) {
  const cx = asNumber(hole.cx_mm)
  const cy = asNumber(hole.cy_mm)
  return {
    diameterMm: roundMm(asNumber(hole.diameter_mm)),
    radiusMm: roundMm(asNumber(hole.radius_mm)),
    cxFromCenterMm: roundMm(cx),
    cyFromCenterMm: roundMm(cy),
    distanceToLeftMm: roundMm(asNumber(hole.distance_to_left_mm, cx - asNumber(bbox.xmin_mm))),
    distanceToRightMm: roundMm(asNumber(hole.distance_to_right_mm, asNumber(bbox.xmax_mm) - cx)),
    distanceToBottomMm: roundMm(asNumber(hole.distance_to_bottom_mm, cy - asNumber(bbox.ymin_mm))),
    distanceToTopMm: roundMm(asNumber(hole.distance_to_top_mm, asNumber(bbox.ymax_mm) - cy)),
  }
}

function classifyDoorHole(hole, bbox) {
  const diameter = asNumber(hole.diameterMm)
  const fromLeft = asNumber(hole.distanceToLeftMm)
  const fromRight = asNumber(hole.distanceToRightMm)
  const fromTop = asNumber(hole.distanceToTopMm)
  const fromBottom = asNumber(hole.distanceToBottomMm)
  if (Math.abs(diameter - 10.2) <= 0.25 && fromLeft < 40 && (fromTop < 25 || fromBottom < 25)) {
    return 'hinge_pin_or_door_axis_hole'
  }
  if (diameter <= 3 && fromRight < 50) {
    return 'lock_or_label_pilot_hole'
  }
  if (fromRight < 80 || fromLeft < 80) {
    return 'edge_interface_hole'
  }
  return 'unclassified_door_hole'
}

function summarizeDoorPanel(row) {
  const bbox = row?.dxf?.manufacturing_bbox_mm || {}
  const slotUnits = Number(String(row.door_ratio).split('/')[0])
  const installedHeightMm = expectedInstalledHeight(slotUnits)
  const flatWidthMm = roundMm(asNumber(bbox.width_mm))
  const flatHeightMm = roundMm(asNumber(bbox.height_mm))
  const flatWidthExtraMm = roundMm(flatWidthMm - BASELINE.baselineDoorWidthMm)
  const flatHeightExtraMm = roundMm(flatHeightMm - installedHeightMm)
  const holes = asArray(row?.dxf?.hole_candidates).map((hole) => {
    const normalized = normalizeHole(hole, bbox)
    return {
      ...normalized,
      role: classifyDoorHole(normalized, bbox),
    }
  })
  const holeRoleCounts = holes.reduce((counts, hole) => {
    counts[hole.role] = (counts[hole.role] || 0) + 1
    return counts
  }, {})

  return {
    classId: `${slotUnits}/12`,
    slotUnits,
    sourceDxf: row.relative_path,
    sourceFileName: row.file_name,
    thicknessMmFromName: row.thickness_mm_from_name ?? null,
    baselineInstalledWidthMm: BASELINE.baselineDoorWidthMm,
    baselineInstalledHeightMm: installedHeightMm,
    baselineFlatWidthMm: flatWidthMm,
    baselineFlatHeightMm: flatHeightMm,
    flatWidthExtraMm,
    flatHeightExtraMm,
    generatedFlatWidthRule: 'doorWidthMm + flatWidthExtraMm',
    generatedFlatHeightRule: 'doorHeightMm + flatHeightExtraMm',
    manufacturingBboxMm: bbox,
    holeCount: holes.length,
    holeRoleCounts,
    holes,
    qualityFlags: asArray(row?.dxf?.quality_flags),
  }
}

function commonDoorHoleRules(classes) {
  const rows = Object.values(classes)
  const hingeRows = rows.map((item) => item.holes.filter((hole) => hole.role === 'hinge_pin_or_door_axis_hole'))
  const allHaveTwoHingeHoles = hingeRows.every((holes) => holes.length >= 2)
  const hingeSample = hingeRows.find((holes) => holes.length >= 2)?.slice(0, 2) || []
  const first = hingeSample[0] || {}
  return {
    status: allHaveTwoHingeHoles ? 'ready_from_gold_dxf' : 'needs_engineering_review',
    hingePinHoleDiameterMm: first.diameterMm ?? null,
    hingeHoleFromSideMm: first.distanceToLeftMm ?? null,
    hingeHoleFromTopBottomMm: first.distanceToBottomMm ?? null,
    hingeHolePattern: 'two holes on hinge side, offset from upper/lower flat edge',
    lockPilotEvidence: rows
      .flatMap((item) => item.holes.filter((hole) => hole.role === 'lock_or_label_pilot_hole').map((hole) => ({ classId: item.classId, ...hole })))
      .slice(0, 12),
  }
}

export function buildGoldSheetmetalRules(evidence) {
  const doorRows = pickDoorPanelRows(evidence)
  const doorClasses = Object.fromEntries(doorRows.map((row) => {
    const item = summarizeDoorPanel(row)
    return [item.classId, item]
  }))
  const offsets = Object.values(doorClasses).map((item) => item.flatHeightExtraMm)
  const flatHeightExtraMm = offsets.length ? roundMm(offsets.reduce((total, value) => total + value, 0) / offsets.length) : null
  const flatWidthExtraMm = Object.values(doorClasses)[0]?.flatWidthExtraMm ?? null

  return {
    schema: GOLD_SHEETMETAL_RULES_SCHEMA,
    generated_at: evidence.generated_at,
    generated_at_basis: 'source_evidence_generated_at',
    sourceEvidence: 'data/locker_16029_gold_sheetmetal_evidence.json',
    goldSourceReference: '1000W x 1917H x 550D / 16029 source-reference DXF flat patterns',
    boundary: {
      cadMainline: 'SolidWorks 2020',
      freeCadRole: 'internal parameter evidence only',
      generatedModelsExclude: 'electrical boards and cabinet-side electric locks',
      generatedModelsCarry: 'sheet-metal hole/interface datums from DXF rules',
    },
    baseline: {
      ...BASELINE,
      doorFlatWidthExtraMm: flatWidthExtraMm,
      doorFlatHeightExtraMm: flatHeightExtraMm,
      installedDoorHeightRule: 'slotUnits * unitPitchMm - visualGapMm',
      flatDoorHeightRule: 'installedDoorHeightMm + doorFlatHeightExtraMm',
      flatDoorWidthRule: 'doorWidthMm + doorFlatWidthExtraMm',
    },
    doorClasses,
    commonDoorHoleRules: commonDoorHoleRules(doorClasses),
    generatorBinding: {
      status: Object.keys(doorClasses).length >= 6 ? 'ready_for_rule_plan_binding' : 'needs_more_dxf_classes',
      dimensionInputs: 'Use installed doorWidthMm/doorHeightMm for SolidWorks model dimensions; keep flat offsets and hole datums as reference evidence until direct SW cut features are implemented.',
      solidWorksHoleFeatureStatus: 'datum_ready_not_cut_feature',
    },
    summary: {
      doorClassCount: Object.keys(doorClasses).length,
      classes: Object.keys(doorClasses),
      flatWidthExtraMm,
      flatHeightExtraMm,
      totalDoorHoleCount: Object.values(doorClasses).reduce((total, item) => total + item.holeCount, 0),
      commonHoleStatus: commonDoorHoleRules(doorClasses).status,
    },
  }
}

function markdown(rules) {
  const lines = [
    '# 16029 Gold Sheet-Metal Rules',
    '',
    `- Generated at: \`${rules.generated_at}\``,
    `- Source evidence: \`${rules.sourceEvidence}\``,
    `- Generator binding: \`${rules.generatorBinding.status}\``,
    `- SW hole feature status: \`${rules.generatorBinding.solidWorksHoleFeatureStatus}\``,
    '',
    '## Baseline',
    '',
    '| Item | Value |',
    '|---|---:|',
    `| source width | ${rules.baseline.sourceWidthMm} |`,
    `| baseline installed door width | ${rules.baseline.baselineDoorWidthMm} |`,
    `| unit pitch | ${rules.baseline.unitPitchMm} |`,
    `| visual gap | ${rules.baseline.visualGapMm} |`,
    `| door flat width extra | ${rules.baseline.doorFlatWidthExtraMm} |`,
    `| door flat height extra | ${rules.baseline.doorFlatHeightExtraMm} |`,
    '',
    '## Door Classes',
    '',
    '| Class | Installed H | Flat W | Flat H | Holes | Source |',
    '|---|---:|---:|---:|---:|---|',
  ]
  for (const item of Object.values(rules.doorClasses).sort((a, b) => a.slotUnits - b.slotUnits)) {
    lines.push(`| ${item.classId} | ${item.baselineInstalledHeightMm} | ${item.baselineFlatWidthMm} | ${item.baselineFlatHeightMm} | ${item.holeCount} | \`${item.sourceDxf}\` |`)
  }
  lines.push('', '## Common Hole Rules', '')
  lines.push(`- Status: \`${rules.commonDoorHoleRules.status}\``)
  lines.push(`- Hinge hole diameter: \`${rules.commonDoorHoleRules.hingePinHoleDiameterMm}\``)
  lines.push(`- Hinge hole from side: \`${rules.commonDoorHoleRules.hingeHoleFromSideMm}\``)
  lines.push(`- Hinge hole from top/bottom: \`${rules.commonDoorHoleRules.hingeHoleFromTopBottomMm}\``)
  lines.push('', '## Boundary', '')
  lines.push('- This is rule evidence for generated reference models, not a production drawing release.')
  lines.push('- Electrical boards and cabinet-side electric locks remain excluded from generated models.')
  lines.push('- Current output carries hole/interface datum data; direct SolidWorks cut-feature generation is the next implementation step.')
  lines.push('')
  return `${lines.join('\n')}\n`
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
  const evidencePath = args.evidence || DEFAULT_EVIDENCE
  const outJson = resolve(args.out || DEFAULT_JSON)
  const outMd = resolve(args.md || DEFAULT_MD)
  const rules = buildGoldSheetmetalRules(readJson(evidencePath))
  mkdirSync(dirname(outJson), { recursive: true })
  writeFileSync(outJson, `${JSON.stringify(rules, null, 2)}\n`, 'utf8')
  mkdirSync(dirname(outMd), { recursive: true })
  writeFileSync(outMd, markdown(rules), 'utf8')
  if (!args.quiet) {
    process.stdout.write(`wrote ${outJson}\n`)
    process.stdout.write(`door classes: ${rules.summary.classes.join(', ')}; holes: ${rules.summary.totalDoorHoleCount}\n`)
  }
  return rules
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
