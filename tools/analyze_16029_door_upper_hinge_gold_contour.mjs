import { readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

const EVIDENCE_JSON = 'data/locker_16029_gold_sheetmetal_evidence.json'
const DECISION_JSON = 'data/locker_16029_door_upper_hinge_rule_decision.json'
const FORMAL_FLAT_CHECK_JSON = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json'
const OUT_JSON = 'data/locker_16029_door_upper_hinge_gold_contour_audit.json'
const OUT_MD = 'docs/16029_door_upper_hinge_gold_contour_audit.md'

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function round(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    if (index + 1 < argv.length && !argv[index + 1].startsWith('--')) args[item.slice(2)] = argv[++index]
    else args[item.slice(2)] = true
  }
  return args
}

function entityPairs(text) {
  const lines = text.split(/\r?\n/)
  const pairs = []
  for (let index = 0; index + 1 < lines.length; index += 2) {
    pairs.push([String(lines[index]).trim(), String(lines[index + 1]).trim()])
  }
  return pairs
}

function firstNumber(values, code) {
  const value = Number(values?.[code]?.[0])
  return Number.isFinite(value) ? value : null
}

function parseEntity(startIndex, pairs) {
  const type = pairs[startIndex][1]
  const values = {}
  let index = startIndex + 1
  while (index < pairs.length) {
    const [code, value] = pairs[index]
    if (code === '0') break
    if (!values[code]) values[code] = []
    values[code].push(value)
    index += 1
  }
  return { type, values, nextIndex: index }
}

function parseDxf(path) {
  const pairs = entityPairs(readFileSync(resolve(path), 'utf8').replace(/^\uFEFF/, ''))
  const entities = []
  let inEntities = false
  for (let index = 0; index < pairs.length;) {
    const [code, value] = pairs[index]
    if (code === '0' && value === 'SECTION' && pairs[index + 1]?.[0] === '2' && pairs[index + 1]?.[1] === 'ENTITIES') {
      inEntities = true
      index += 2
      continue
    }
    if (inEntities && code === '0' && value === 'ENDSEC') {
      inEntities = false
      index += 1
      continue
    }
    if (!inEntities || code !== '0') {
      index += 1
      continue
    }

    const entity = parseEntity(index, pairs)
    const values = entity.values
    if (entity.type === 'LINE') {
      entities.push({
        type: 'LINE',
        x1: firstNumber(values, '10'),
        y1: firstNumber(values, '20'),
        x2: firstNumber(values, '11'),
        y2: firstNumber(values, '21'),
      })
    } else if (entity.type === 'CIRCLE') {
      entities.push({
        type: 'CIRCLE',
        cx: firstNumber(values, '10'),
        cy: firstNumber(values, '20'),
        r: firstNumber(values, '40'),
      })
    } else if (entity.type === 'ARC') {
      entities.push({
        type: 'ARC',
        cx: firstNumber(values, '10'),
        cy: firstNumber(values, '20'),
        r: firstNumber(values, '40'),
        start: firstNumber(values, '50'),
        end: firstNumber(values, '51'),
      })
    }
    index = entity.nextIndex
  }
  return entities
}

function entityCounts(entities) {
  return entities.reduce((counts, entity) => {
    counts[entity.type] = (counts[entity.type] || 0) + 1
    return counts
  }, {})
}

function lineSummary(line) {
  return {
    x1_mm: round(line.x1),
    y1_mm: round(line.y1),
    x2_mm: round(line.x2),
    y2_mm: round(line.y2),
    length_mm: round(Math.hypot(line.x2 - line.x1, line.y2 - line.y1)),
  }
}

function verticalLineAt(entities, x, yMin, yMax) {
  return entities
    .filter((entity) => entity.type === 'LINE')
    .find((line) => (
      Math.abs(line.x1 - x) < 0.001 &&
      Math.abs(line.x2 - x) < 0.001 &&
      Math.min(line.y1, line.y2) <= yMin + 0.001 &&
      Math.max(line.y1, line.y2) >= yMax - 0.001
    ))
}

function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  const evidence = readJson(args.evidence || EVIDENCE_JSON)
  const decision = readJson(args.decision || DECISION_JSON)
  const formalFlatCheck = readJson(args.formalFlatCheck || FORMAL_FLAT_CHECK_JSON)

  const sourceRow = evidence.dxf_files.find((row) => (
    row.source_bucket === 'current_flat_pattern' &&
    row.door_ratio === '2/12' &&
    Array.isArray(row.roles) &&
    row.roles.includes('door_panel')
  ))
  if (!sourceRow) throw new Error('2/12 current_flat_pattern door_panel source DXF was not found')

  const sourceEntities = parseDxf(sourceRow.relative_path)
  const formalEntities = parseDxf(formalFlatCheck.input)
  const upperHinge = decision.holes.find((hole) => hole.role === 'hinge_upper')
  const lowerHinge = decision.holes.find((hole) => hole.role === 'hinge_lower')
  const radiusMm = upperHinge.diameter_mm / 2

  const sourceUpperHole = sourceRow.dxf.hole_candidates.find((hole) => Math.abs(hole.diameter_mm - 10.2) < 0.001 && hole.cy_mm > 0)
  const sourceLowerHole = sourceRow.dxf.hole_candidates.find((hole) => Math.abs(hole.diameter_mm - 10.2) < 0.001 && hole.cy_mm < 0)

  const sourceUpperMaterialEdgeX = -217.5
  const sourceLowerMaterialEdgeX = -217.5
  const sourceUpperEdgeLine = verticalLineAt(sourceEntities, sourceUpperMaterialEdgeX, 162.6, 167.2)
  const sourceLowerEdgeLine = verticalLineAt(sourceEntities, sourceLowerMaterialEdgeX, -167.2, -162.6)
  const formalUpperMaterialEdgeX = 5.2
  const formalLowerMaterialEdgeX = -12.8
  const formalUpperEdgeLine = verticalLineAt(formalEntities, formalUpperMaterialEdgeX, 255.95, 275.15)
  const formalLowerEdgeLine = verticalLineAt(formalEntities, formalLowerMaterialEdgeX, -59.25, -22.05)

  const sourceUpperCenterClearance = round(sourceUpperHole.cx_mm - sourceUpperMaterialEdgeX)
  const sourceLowerCenterClearance = round(sourceLowerHole.cx_mm - sourceLowerMaterialEdgeX)
  const expectedFormalUpperEdgeX = round(upperHinge.mapped_center_mm[0] - sourceUpperCenterClearance)
  const requiredFormalUpperEdgeForFullCircleX = round(upperHinge.mapped_center_mm[0] - radiusMm)
  const formalUpperEdgeOvercutMm = round(formalUpperMaterialEdgeX - expectedFormalUpperEdgeX)
  const formalUpperCircleDeficitMm = round(formalUpperMaterialEdgeX - requiredFormalUpperEdgeForFullCircleX)

  const report = {
    schema: 'winnsen.locker16029.door_upper_hinge_gold_contour_audit.v1',
    created_at: '2026-06-17',
    status: 'B_CORNER_RELIEF_RULE_SUPPORTED_BY_GOLD_DXF_D_OPTION_NOT_PRIMARY',
    source_gold_dxf: sourceRow.relative_path,
    formal_flat_dxf: formalFlatCheck.input,
    source_entity_counts: entityCounts(sourceEntities),
    formal_entity_counts: entityCounts(formalEntities),
    gold_source_upper_hinge: {
      center_mm: [round(sourceUpperHole.cx_mm), round(sourceUpperHole.cy_mm)],
      diameter_mm: round(sourceUpperHole.diameter_mm),
      contour_edge_line: sourceUpperEdgeLine ? lineSummary(sourceUpperEdgeLine) : null,
      center_clearance_to_hinge_side_top_strip_edge_mm: sourceUpperCenterClearance,
      full_circle_clearance_to_edge_mm: round(sourceUpperCenterClearance - radiusMm),
    },
    gold_source_lower_hinge: {
      center_mm: [round(sourceLowerHole.cx_mm), round(sourceLowerHole.cy_mm)],
      diameter_mm: round(sourceLowerHole.diameter_mm),
      contour_edge_line: sourceLowerEdgeLine ? lineSummary(sourceLowerEdgeLine) : null,
      center_clearance_to_hinge_side_bottom_strip_edge_mm: sourceLowerCenterClearance,
      full_circle_clearance_to_edge_mm: round(sourceLowerCenterClearance - radiusMm),
    },
    formal_current_upper_hinge: {
      mapped_center_mm: upperHinge.mapped_center_mm,
      diameter_mm: upperHinge.diameter_mm,
      current_top_strip_edge_line: formalUpperEdgeLine ? lineSummary(formalUpperEdgeLine) : null,
      expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm: expectedFormalUpperEdgeX,
      required_top_strip_edge_x_for_full_circle_inside_mm: requiredFormalUpperEdgeForFullCircleX,
      actual_top_strip_edge_x_mm: formalUpperMaterialEdgeX,
      top_strip_material_missing_vs_gold_rule_mm: formalUpperEdgeOvercutMm,
      top_strip_material_missing_vs_full_circle_minimum_mm: formalUpperCircleDeficitMm,
      material_status: 'OUTSIDE_CURRENT_FLAT_PATTERN',
    },
    formal_current_lower_hinge: {
      mapped_center_mm: lowerHinge.mapped_center_mm,
      diameter_mm: lowerHinge.diameter_mm,
      current_bottom_strip_edge_line: formalLowerEdgeLine ? lineSummary(formalLowerEdgeLine) : null,
      actual_bottom_strip_edge_x_mm: formalLowerMaterialEdgeX,
      center_clearance_to_edge_mm: round(lowerHinge.mapped_center_mm[0] - formalLowerMaterialEdgeX),
      material_status: 'INSIDE_CURRENT_FLAT_PATTERN',
    },
    conclusion: {
      d_handing_or_mirror: 'Gold DXF has symmetric upper/lower hinge holes on one hinge datum; current failure is not explained by moving only the upper hole or by a simple mirror swap.',
      b_corner_relief: 'Current SW flat pattern removes about 18.0 mm more top-left strip material than the gold contour rule. Restoring that strip is the narrow candidate to test before supplier DXF release.',
      candidate_b_rule: 'Preserve the hinge-side top-strip edge at mapped X <= -12.8 mm; minimum for the full 10.2 mm upper circle is X <= -8.9 mm.',
    },
  }

  writeFileSync(args.outJson || OUT_JSON, `${JSON.stringify(report, null, 2)}\n`, 'utf8')
  writeFileSync(args.outMd || OUT_MD, markdown(report), 'utf8')
  process.stdout.write(`${args.outJson || OUT_JSON}\n${args.outMd || OUT_MD}\n`)
}

function markdown(report) {
  return [
    '# 16029 Door Upper Hinge Gold Contour Audit',
    '',
    `Date: \`${report.created_at}\``,
    '',
    '## Result',
    '',
    '- D option is not the primary blocker: the gold 2/12 DXF keeps the upper and lower 10.2 mm hinge holes symmetric on one hinge datum.',
    '- B option is now the preferred narrow candidate: the current SW top-left corner relief removes too much top strip material.',
    '',
    '## Key Measurements',
    '',
    '| Item | Value |',
    '|---|---:|',
    `| gold upper hinge center | (${report.gold_source_upper_hinge.center_mm.join(', ')}) |`,
    `| gold upper center clearance to top strip edge | ${report.gold_source_upper_hinge.center_clearance_to_hinge_side_top_strip_edge_mm} mm |`,
    `| formal upper mapped center | (${report.formal_current_upper_hinge.mapped_center_mm.join(', ')}) |`,
    `| formal current top strip edge X | ${report.formal_current_upper_hinge.actual_top_strip_edge_x_mm} mm |`,
    `| expected top strip edge X if gold clearance is preserved | ${report.formal_current_upper_hinge.expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm} mm |`,
    `| minimum top strip edge X for full circle | ${report.formal_current_upper_hinge.required_top_strip_edge_x_for_full_circle_inside_mm} mm |`,
    `| top strip material missing vs gold rule | ${report.formal_current_upper_hinge.top_strip_material_missing_vs_gold_rule_mm} mm |`,
    `| top strip material missing vs full-circle minimum | ${report.formal_current_upper_hinge.top_strip_material_missing_vs_full_circle_minimum_mm} mm |`,
    '',
    '## Engineering Reading',
    '',
    'The source DXF supports keeping the hinge datum and correcting the top-left relief/return-flange interpretation. Moving only the upper hole remains a non-default option because it breaks the hinge-hole datum relationship.',
    '',
  ].join('\n')
}

main()
