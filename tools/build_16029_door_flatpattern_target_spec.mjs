import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const DEFAULT_CONTRACT = 'data/locker_16029_part_first_pilot_contract.json'
const DEFAULT_GOLD_RULES = 'data/locker_16029_gold_sheetmetal_rules.json'
const DEFAULT_PART_ID = 'door_panel_2_12_right_v43'
const DEFAULT_FLAT_CHECK = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json'
const DEFAULT_OUT_JSON = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json'
const DEFAULT_OUT_MD = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.md'

function parseArgs(argv) {
  const options = {
    contract: DEFAULT_CONTRACT,
    goldRules: DEFAULT_GOLD_RULES,
    partId: DEFAULT_PART_ID,
    flatCheck: DEFAULT_FLAT_CHECK,
    outJson: DEFAULT_OUT_JSON,
    outMd: DEFAULT_OUT_MD,
  }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    const next = () => argv[++index] || ''
    if (arg === '--contract') options.contract = next()
    else if (arg === '--gold-rules') options.goldRules = next()
    else if (arg === '--part-id') options.partId = next()
    else if (arg === '--flat-check') options.flatCheck = next()
    else if (arg === '--out-json') options.outJson = next()
    else if (arg === '--out-md') options.outMd = next()
  }
  return options
}

function readJson(path) {
  return JSON.parse(readFileSync(resolve(ROOT, path), 'utf8'))
}

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function edgeAnchoredCoordinate({ flatSizeMm, distanceToMinMm, distanceToMaxMm }) {
  const half = flatSizeMm / 2
  if (distanceToMinMm <= distanceToMaxMm) {
    return {
      anchor: 'min_edge',
      from_anchor_mm: roundMm(distanceToMinMm),
      coordinate_from_center_mm: roundMm(-half + distanceToMinMm),
    }
  }
  return {
    anchor: 'max_edge',
    from_anchor_mm: roundMm(distanceToMaxMm),
    coordinate_from_center_mm: roundMm(half - distanceToMaxMm),
  }
}

function buildTargetHole(hole, target) {
  const x = edgeAnchoredCoordinate({
    flatSizeMm: target.flat_width_mm,
    distanceToMinMm: Number(hole.distanceToLeftMm),
    distanceToMaxMm: Number(hole.distanceToRightMm),
  })
  const y = edgeAnchoredCoordinate({
    flatSizeMm: target.flat_height_mm,
    distanceToMinMm: Number(hole.distanceToBottomMm),
    distanceToMaxMm: Number(hole.distanceToTopMm),
  })
  return {
    role: hole.role || 'unknown',
    diameter_mm: roundMm(Number(hole.diameterMm)),
    radius_mm: roundMm(Number(hole.radiusMm)),
    target_cx_from_center_mm: x.coordinate_from_center_mm,
    target_cy_from_center_mm: y.coordinate_from_center_mm,
    horizontal_anchor: x.anchor === 'min_edge' ? 'left_flat_edge' : 'right_flat_edge',
    horizontal_offset_from_anchor_mm: x.from_anchor_mm,
    vertical_anchor: y.anchor === 'min_edge' ? 'bottom_flat_edge' : 'top_flat_edge',
    vertical_offset_from_anchor_mm: y.from_anchor_mm,
    source_cx_from_center_mm: roundMm(Number(hole.cxFromCenterMm)),
    source_cy_from_center_mm: roundMm(Number(hole.cyFromCenterMm)),
  }
}

function markdown(report) {
  const holeRows = report.target_hole_datums.map((hole, index) => (
    `| ${index + 1} | ${hole.role} | ${hole.diameter_mm} | ${hole.target_cx_from_center_mm} | ${hole.target_cy_from_center_mm} | ${hole.horizontal_anchor} ${hole.horizontal_offset_from_anchor_mm} | ${hole.vertical_anchor} ${hole.vertical_offset_from_anchor_mm} |`
  ))
  return [
    '# 16029 Door FlatPattern Target Spec',
    '',
    `Part: \`${report.part_id}\``,
    '',
    '## Target',
    '',
    `- Installed size: \`${report.target.installed_width_mm} x ${report.target.installed_height_mm} mm\``,
    `- Required flat blank: \`${report.target.flat_width_mm} x ${report.target.flat_height_mm} mm\``,
    `- Flat allowance: \`+${report.target.flat_width_extra_mm} / +${report.target.flat_height_extra_mm} mm\``,
    `- Thickness target: \`${report.target.thickness_mm_target} mm\``,
    '',
    '## Current Pilot Flat',
    '',
    `- Measured DXF bbox: \`${report.current_pilot_flat.bbox_width_mm} x ${report.current_pilot_flat.bbox_height_mm} mm\``,
    `- Delta to target: \`${report.current_pilot_flat.width_delta_to_target_mm} / ${report.current_pilot_flat.height_delta_to_target_mm} mm\``,
    '',
    '## Hole Datums',
    '',
    '| # | role | dia | cx | cy | x anchor | y anchor |',
    '|---:|---|---:|---:|---:|---|---|',
    ...holeRows,
    '',
    '## Closure Rule',
    '',
    '- Passing evidence must be a SW2020 FlatPattern DXF exported from the rebuilt SLDPRT.',
    '- Scaling the current DXF or copying the 1000W source DXF is not accepted as release closure.',
    '- If engineering changes the gold/source rule, update the rule source and rerun the gate instead of overriding this blocker.',
    '',
  ].join('\n')
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  const contract = readJson(options.contract)
  const goldRules = readJson(options.goldRules)
  const flatCheck = existsSync(resolve(ROOT, options.flatCheck)) ? readJson(options.flatCheck) : null
  const part = (contract.selected_parts || []).find((item) => item.id === options.partId)
  if (!part) throw new Error(`part not found in contract: ${options.partId}`)
  const doorUnit = part.source_rule?.door_unit
  const gold = goldRules.doorClasses?.[doorUnit]
  if (!gold) throw new Error(`gold rule not found for door unit: ${doorUnit}`)
  const expected = part.expected_parameters || {}
  const target = {
    installed_width_mm: roundMm(expected.installed_width_mm),
    installed_height_mm: roundMm(expected.installed_height_mm),
    flat_width_mm: roundMm(expected.flat_width_mm),
    flat_height_mm: roundMm(expected.flat_height_mm),
    flat_width_extra_mm: roundMm(expected.flat_width_extra_mm),
    flat_height_extra_mm: roundMm(expected.flat_height_extra_mm),
    thickness_mm_target: roundMm(expected.thickness_mm_target),
    handedness: expected.handedness || part.source_rule?.side || '',
  }
  const currentWidth = Number(flatCheck?.bbox_mm?.width_mm)
  const currentHeight = Number(flatCheck?.bbox_mm?.height_mm)
  const report = {
    schema: 'winnsen.locker16029.door_flatpattern_target_spec.v1',
    part_id: options.partId,
    status: 'TARGET_SPEC_READY_RELEASE_BLOCKER_STILL_OPEN',
    release_blocker: true,
    source_rule: {
      door_unit: doorUnit,
      gold_rules_path: options.goldRules,
      source_dxf: gold.sourceDxf,
      baseline_installed_width_mm: gold.baselineInstalledWidthMm,
      baseline_installed_height_mm: gold.baselineInstalledHeightMm,
      baseline_flat_width_mm: gold.baselineFlatWidthMm,
      baseline_flat_height_mm: gold.baselineFlatHeightMm,
      derivation: 'target flat size = v43 installed size + gold/source flat extra',
    },
    target,
    current_pilot_flat: {
      flat_check_json: options.flatCheck,
      bbox_width_mm: Number.isFinite(currentWidth) ? roundMm(currentWidth) : null,
      bbox_height_mm: Number.isFinite(currentHeight) ? roundMm(currentHeight) : null,
      width_delta_to_target_mm: Number.isFinite(currentWidth) ? roundMm(target.flat_width_mm - currentWidth) : null,
      height_delta_to_target_mm: Number.isFinite(currentHeight) ? roundMm(target.flat_height_mm - currentHeight) : null,
    },
    target_hole_datums: (gold.holes || []).map((hole) => buildTargetHole(hole, target)),
    closure_acceptance: [
      'Rebuilt SW2020 SLDPRT opens and rebuilds.',
      'SheetMetal and FlatPattern features exist in the rebuilt SLDPRT.',
      'FlatPattern DXF exported from that SLDPRT parses to 343.4 x 334.4 mm within tolerance.',
      'The four target hole datums are represented as cut features or explicitly reported datum evidence.',
      'Do not close this blocker with scaled/copy-only DXF evidence.',
    ],
  }
  writeFileSync(resolve(ROOT, options.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
  writeFileSync(resolve(ROOT, options.outMd), markdown(report), 'utf8')
  process.stdout.write(`${JSON.stringify({ out_json: options.outJson, out_md: options.outMd, status: report.status }, null, 2)}\n`)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) {
  main()
}
