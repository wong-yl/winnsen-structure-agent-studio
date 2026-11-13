import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const PATHS = {
  decision: 'data/locker_16029_door_upper_hinge_rule_decision.json',
  doorFlatCheck:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flat_check.json',
  outJson: 'data/locker_16029_door_upper_hinge_candidate_b_reference.json',
  outMd: 'docs/16029_door_upper_hinge_candidate_b_reference.md',
  outDxf:
    'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_candidate_B_reference_NOT_FOR_SUPPLIER.dxf',
}

function abs(path) {
  return resolve(ROOT, path)
}

function readJson(path) {
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function roundMm(value) {
  return Math.round(asNumber(value) * 1000) / 1000
}

function line(x1, y1, x2, y2, role) {
  return {
    type: 'LINE',
    role,
    x1_mm: roundMm(x1),
    y1_mm: roundMm(y1),
    x2_mm: roundMm(x2),
    y2_mm: roundMm(y2),
  }
}

function circle(cx, cy, diameter, role) {
  return {
    type: 'CIRCLE',
    role,
    cx_mm: roundMm(cx),
    cy_mm: roundMm(cy),
    diameter_mm: roundMm(diameter),
    radius_mm: roundMm(diameter / 2),
  }
}

function bboxFromEntities(entities) {
  const points = []
  for (const entity of entities) {
    if (entity.type === 'LINE') {
      points.push([entity.x1_mm, entity.y1_mm], [entity.x2_mm, entity.y2_mm])
    } else if (entity.type === 'CIRCLE') {
      points.push(
        [entity.cx_mm - entity.radius_mm, entity.cy_mm - entity.radius_mm],
        [entity.cx_mm + entity.radius_mm, entity.cy_mm + entity.radius_mm],
      )
    }
  }
  const xs = points.map(([x]) => x)
  const ys = points.map(([, y]) => y)
  const xmin = Math.min(...xs)
  const xmax = Math.max(...xs)
  const ymin = Math.min(...ys)
  const ymax = Math.max(...ys)
  return {
    xmin_mm: roundMm(xmin),
    ymin_mm: roundMm(ymin),
    xmax_mm: roundMm(xmax),
    ymax_mm: roundMm(ymax),
    width_mm: roundMm(xmax - xmin),
    height_mm: roundMm(ymax - ymin),
  }
}

function dxfPair(code, value) {
  return `${code}\n${value}\n`
}

function dxfLine(entity) {
  return [
    dxfPair(0, 'LINE'),
    dxfPair(8, 'CANDIDATE_B_REFERENCE_CONTOUR'),
    dxfPair(10, entity.x1_mm),
    dxfPair(20, entity.y1_mm),
    dxfPair(30, 0),
    dxfPair(11, entity.x2_mm),
    dxfPair(21, entity.y2_mm),
    dxfPair(31, 0),
  ].join('')
}

function dxfCircle(entity) {
  return [
    dxfPair(0, 'CIRCLE'),
    dxfPair(8, 'CANDIDATE_B_REFERENCE_HOLES'),
    dxfPair(10, entity.cx_mm),
    dxfPair(20, entity.cy_mm),
    dxfPair(30, 0),
    dxfPair(40, entity.radius_mm),
  ].join('')
}

function writeDxf(path, entities) {
  const body = entities
    .map((entity) => {
      if (entity.type === 'LINE') return dxfLine(entity)
      if (entity.type === 'CIRCLE') return dxfCircle(entity)
      return ''
    })
    .join('')

  const header = [
    dxfPair(0, 'SECTION'),
    dxfPair(2, 'HEADER'),
    dxfPair(9, '$INSUNITS'),
    dxfPair(70, 4),
    dxfPair(0, 'ENDSEC'),
    dxfPair(0, 'SECTION'),
    dxfPair(2, 'ENTITIES'),
  ].join('')
  const footer = [dxfPair(0, 'ENDSEC'), dxfPair(0, 'EOF')].join('')
  writeFileSync(abs(path), `${header}${body}${footer}`, 'utf8')
}

function buildReference() {
  const decision = readJson(PATHS.decision)
  const flatCheck = readJson(PATHS.doorFlatCheck)
  const gold = decision.gold_contour_audit || {}
  const topEdgeX = asNumber(gold.formal_current_top_strip_edge_x_mm)
  const targetTopEdgeX = asNumber(gold.expected_top_strip_edge_x_if_gold_clearance_is_preserved_mm)
  const minimumTopEdgeX = asNumber(gold.required_top_strip_edge_x_for_full_circle_inside_mm)
  const missingVsGold = roundMm(topEdgeX - targetTopEdgeX)
  const missingVsMinimum = roundMm(topEdgeX - minimumTopEdgeX)

  const contour = [
    line(targetTopEdgeX, 275.15, targetTopEdgeX, 255.95, 'candidate_b_repaired_top_left_vertical'),
    line(targetTopEdgeX, 275.15, 292.2, 275.15, 'candidate_b_repaired_top_edge'),
    line(292.2, 275.15, 292.2, 237.95, 'current_top_right_vertical'),
    line(311.4, 237.95, 292.2, 237.95, 'current_top_right_step'),
    line(311.4, 237.95, 311.4, -22.05, 'current_right_outer_vertical'),
    line(292.2, -22.05, 311.4, -22.05, 'current_bottom_right_step'),
    line(292.2, -59.25, 292.2, -22.05, 'current_bottom_right_vertical'),
    line(292.2, -59.25, -12.8, -59.25, 'current_bottom_edge'),
    line(-12.8, -59.25, -12.8, -22.05, 'current_bottom_left_vertical'),
    line(-32, -22.05, -12.8, -22.05, 'current_bottom_left_step'),
    line(-32, -22.05, -32, 255.95, 'current_left_outer_vertical'),
    line(-32, 255.95, targetTopEdgeX, 255.95, 'candidate_b_repaired_top_left_step'),
  ]

  const holes = asArray(decision.holes).map((hole) =>
    circle(hole.mapped_center_mm?.[0], hole.mapped_center_mm?.[1], hole.diameter_mm, hole.role),
  )
  const entities = [...contour, ...holes]
  const bbox = bboxFromEntities(entities)
  const report = {
    schema: 'winnsen.locker16029.door_upper_hinge_candidate_b_reference.v1',
    generated_at: new Date().toISOString(),
    status: 'REFERENCE_READY_NOT_FOR_SUPPLIER_RELEASE',
    release_use: 'NOT_FOR_SUPPLIER_RELEASE_NOT_CAD_EVIDENCE',
    scope: {
      product: '16029 locker',
      part: 'door_panel_2_12_right_v43',
      purpose:
        'Reference contour for engineering confirmation and manual SW2020 rebuild. This file does not replace true SolidWorks cut features.',
    },
    source_paths: {
      decision: PATHS.decision,
      doorFlatCheck: PATHS.doorFlatCheck,
    },
    output_paths: {
      machine_report: PATHS.outJson,
      human_report: PATHS.outMd,
      reference_dxf: PATHS.outDxf,
    },
    candidate_b_rule: {
      current_top_left_strip_edge_x_mm: topEdgeX,
      target_top_left_strip_edge_x_mm: targetTopEdgeX,
      minimum_edge_x_for_full_10p2_circle_mm: minimumTopEdgeX,
      restored_strip_vs_current_mm: missingVsGold,
      restored_strip_minimum_required_mm: missingVsMinimum,
      upper_hinge_datum_preserved: true,
      upper_hinge_source_x_mm: asArray(decision.holes).find((hole) => hole.role === 'hinge_upper')?.source_center_mm?.[0] ?? null,
      lower_hinge_source_x_mm: asArray(decision.holes).find((hole) => hole.role === 'hinge_lower')?.source_center_mm?.[0] ?? null,
    },
    reference_flat: {
      bbox_mm: bbox,
      expected_bbox_mm: flatCheck.bbox_mm,
      entity_counts: {
        LINE: contour.length,
        CIRCLE: holes.length,
      },
      contour,
      holes,
    },
    acceptance_boundary: [
      'Use this DXF only as a visual/reference target for B signoff and SW2020 manual rebuild.',
      'Do not feed this DXF into supplier precheck as production evidence.',
      'Final closure still requires the formal SLDPRT to report SheetMetal + FlatPattern, holeCutCount=4, flat 343.4 x 334.4 mm, folded 307 x 298 x 18.2 mm, and >=4 circles in the exported flat DXF.',
    ],
  }
  return report
}

function markdown(report) {
  const rule = report.candidate_b_rule
  const flat = report.reference_flat
  return `# 16029 Door Upper Hinge Candidate B Reference

Status: \`${report.status}\`

This is a reference artifact only. It is not supplier-release DXF and not proof of SolidWorks cut features.

## Candidate B Rule

| Item | Value |
|---|---:|
| current top-left strip edge X | ${rule.current_top_left_strip_edge_x_mm} mm |
| target top-left strip edge X | ${rule.target_top_left_strip_edge_x_mm} mm |
| minimum edge X for full 10.2 mm circle | ${rule.minimum_edge_x_for_full_10p2_circle_mm} mm |
| restored strip vs current | ${rule.restored_strip_vs_current_mm} mm |
| minimum restoration required | ${rule.restored_strip_minimum_required_mm} mm |
| upper hinge source X | ${rule.upper_hinge_source_x_mm} mm |
| lower hinge source X | ${rule.lower_hinge_source_x_mm} mm |

## Reference Outputs

- Machine report: \`${report.output_paths.machine_report}\`
- Reference DXF: \`${report.output_paths.reference_dxf}\`
- Reference bbox: \`${flat.bbox_mm.width_mm} x ${flat.bbox_mm.height_mm} mm\`
- Reference entities: \`LINE=${flat.entity_counts.LINE}\`, \`CIRCLE=${flat.entity_counts.CIRCLE}\`

## Use Boundary

${report.acceptance_boundary.map((item) => `- ${item}`).join('\n')}
`
}

const report = buildReference()
for (const path of [PATHS.outJson, PATHS.outMd, PATHS.outDxf]) mkdirSync(dirname(abs(path)), { recursive: true })
writeDxf(PATHS.outDxf, [...report.reference_flat.contour, ...report.reference_flat.holes])
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(report), 'utf8')

console.log(`${report.status}: ${PATHS.outJson}`)
console.log(`reference_dxf: ${PATHS.outDxf}`)
