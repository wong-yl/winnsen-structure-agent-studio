import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const BASE =
  'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/minimum_interface_assembly'

const PATHS = {
  gapReview: 'data/locker_16029_door_front_gap_review.json',
  nativeRerun: 'data/locker_16029_three_part_native_rerun_report.json',
  manifest: 'data/locker_16029_part_first_delivery_manifest.json',
  sourcePlacements: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_placements.tsv`,
  candidatePlacements: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_placements.tsv`,
  candidateAssembly: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate.SLDASM`,
  candidateBuild: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_build.json`,
  candidateComponents: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_components.json`,
  candidateClearance: `${BASE}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_clearance.json`,
  outJson: 'data/locker_16029_door_front_candidate_placement_review.json',
  outMd: 'docs/16029_door_front_candidate_placement_review.md',
}

function abs(path) {
  return resolve(ROOT, path)
}

function rel(path) {
  return path.replaceAll('\\', '/').replace(ROOT.replaceAll('\\', '/') + '/', '')
}

function readJson(path, fallback = {}) {
  if (!existsSync(abs(path))) return fallback
  return JSON.parse(readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, ''))
}

function readText(path) {
  return readFileSync(abs(path), 'utf8').replace(/^\uFEFF/, '')
}

function round(value) {
  return Number.isFinite(Number(value)) ? Math.round(Number(value) * 1000) / 1000 : null
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function parseTsv(text) {
  const lines = text.split(/\r?\n/).filter((line) => line.trim())
  const headers = lines.shift().split('\t').map((value) => value.trim())
  const index = new Map(headers.map((header, i) => [header, i]))
  return {
    headers,
    rows: lines.map((line) => {
      const values = line.split('\t')
      return Object.fromEntries(headers.map((header) => [header, values[index.get(header)] ?? '']))
    }),
  }
}

function writeTsv(path, headers, rows) {
  const body = [
    headers.join('\t'),
    ...rows.map((row) => headers.map((header) => row[header] ?? '').join('\t')),
  ].join('\n')
  mkdirSync(dirname(abs(path)), { recursive: true })
  writeFileSync(abs(path), `${body}\n`, 'utf8')
}

function shiftedBox(box, dxMm) {
  return {
    ...box,
    xmin_mm: round(Number(box.xmin_mm) + dxMm),
    xmax_mm: round(Number(box.xmax_mm) + dxMm),
    xlen_mm: round(Number(box.xmax_mm) - Number(box.xmin_mm)),
    ylen_mm: round(Number(box.ymax_mm) - Number(box.ymin_mm)),
    zlen_mm: round(Number(box.zmax_mm) - Number(box.zmin_mm)),
  }
}

function axisRelation(aMin, aMax, bMin, bMax) {
  if (aMax < bMin) return { separated: true, gap_mm: round(bMin - aMax), overlap_mm: 0 }
  if (bMax < aMin) return { separated: true, gap_mm: round(aMin - bMax), overlap_mm: 0 }
  return { separated: false, gap_mm: 0, overlap_mm: round(Math.min(aMax, bMax) - Math.max(aMin, bMin)) }
}

function candidatePair(doorBox, frontBox) {
  const x = axisRelation(doorBox.xmin_mm, doorBox.xmax_mm, frontBox.xmin_mm, frontBox.xmax_mm)
  const y = axisRelation(doorBox.ymin_mm, doorBox.ymax_mm, frontBox.ymin_mm, frontBox.ymax_mm)
  const z = axisRelation(doorBox.zmin_mm, doorBox.zmax_mm, frontBox.zmin_mm, frontBox.zmax_mm)
  const separatedAxes = Object.entries({ x, y, z }).filter(([, value]) => value.separated)
  return {
    roles: ['door_panel_2_12_right_v43', 'front_frame_vertical_left_v43'],
    aabb_intersects: separatedAxes.length === 0,
    clearance_conclusion:
      separatedAxes.length === 0
        ? 'AABB_OVERLAPS_REQUIRES_EXACT_SOLID_INTERFERENCE_CHECK'
        : 'CANDIDATE_AABB_SEPARATED_NO_SOLID_INTERSECTION_POSSIBLE_AFTER_SW2020_RERUN',
    minimum_separating_gap_mm: round(
      separatedAxes
        .map(([, value]) => value.gap_mm)
        .filter((value) => value > 0)
        .sort((a, b) => a - b)[0] || 0,
    ),
    separating_axes: separatedAxes.map(([axis]) => axis),
    axes: { x, y, z },
  }
}

function maybeReadEvidence(path) {
  const absolute = abs(path)
  if (!existsSync(absolute)) return { exists: false, path }
  const json = readJson(path)
  return { exists: true, path, json }
}

function buildReport() {
  const gapReview = readJson(PATHS.gapReview)
  const nativeRerun = readJson(PATHS.nativeRerun)
  const manifest = readJson(PATHS.manifest)
  const tsv = parseTsv(readText(PATHS.sourcePlacements))

  const candidate = gapReview.transform_candidates?.preferred_fixed_door_adjust_front_frame || {}
  const currentFrontTx = Number(candidate.current_front_tx_mm)
  const targetFrontTx = Number(candidate.front_tx_for_target_gap_mm)
  const shiftMm = round(targetFrontTx - currentFrontTx)
  const targetGapMm = Number(candidate.target_gap_mm)

  const candidateRows = tsv.rows.map((row) => {
    if (row.role !== 'front_frame_vertical_left_v43') return row
    return { ...row, tx_mm: String(targetFrontTx) }
  })
  writeTsv(PATHS.candidatePlacements, tsv.headers, candidateRows)

  const doorBox = gapReview.current_native_bbox?.door?.bbox_mm || {}
  const currentFrontBox = gapReview.current_native_bbox?.front_frame?.bbox_mm || {}
  const shiftedFrontBox = shiftedBox(currentFrontBox, shiftMm)
  const expectedPair = candidatePair(doorBox, shiftedFrontBox)

  const swEvidence = {
    build: maybeReadEvidence(PATHS.candidateBuild),
    components: maybeReadEvidence(PATHS.candidateComponents),
    clearance: maybeReadEvidence(PATHS.candidateClearance),
  }
  const swClearance = swEvidence.clearance.json || {}
  const swDoorFrontPair = asArray(swClearance.pairwise).find((pair) => {
    const roles = asArray(pair.roles)
    return roles.includes('door_panel_2_12_right_v43') && roles.includes('front_frame_vertical_left_v43')
  }) || null
  const swRerunComplete =
    swEvidence.build.json?.build_status === 'completed' &&
    swEvidence.components.json?.opened === true &&
    swEvidence.components.json?.rebuilt === true &&
    Number(swEvidence.components.json?.component_count) === 3 &&
    swClearance.status === 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED'

  const status = swRerunComplete
    ? 'CANDIDATE_SW2020_NATIVE_AABB_SEPARATED_ENGINEERING_GAP_REVIEW_READY'
    : 'CANDIDATE_PLACEMENT_READY_FOR_SW2020_NATIVE_RERUN'

  return {
    schema: 'winnsen.locker16029.door_front_candidate_placement_review.v1',
    generated_at: new Date().toISOString(),
    status,
    scope: {
      pair: 'door_panel_2_12_right_v43 vs front_frame_vertical_left_v43',
      candidate_id: 'front_tx_m185p15_fixed_door',
      purpose:
        'Prepare the minimum candidate placement that should turn the door-front X AABB overlap into a 7 mm X clearance before exact engineering gap review.',
      non_goals: [
        'do not edit formal part geometry',
        'do not overwrite the formal placement TSV',
        'do not issue supplier DXF',
        'do not expand to full cabinet',
      ],
    },
    source_evidence: {
      gap_review: PATHS.gapReview,
      gap_review_status: gapReview.status || null,
      native_rerun_report: PATHS.nativeRerun,
      native_rerun_status: nativeRerun.status || null,
      delivery_manifest: PATHS.manifest,
      manifest_status: manifest.status || null,
    },
    candidate_files: {
      source_placements_tsv: PATHS.sourcePlacements,
      candidate_placements_tsv: PATHS.candidatePlacements,
      candidate_assembly_path: PATHS.candidateAssembly,
      candidate_build_json: PATHS.candidateBuild,
      candidate_components_json: PATHS.candidateComponents,
      candidate_clearance_json: PATHS.candidateClearance,
    },
    placement_delta: {
      fixed_datum: 'door_panel_2_12_right_v43',
      adjusted_component: 'front_frame_vertical_left_v43',
      current_front_tx_mm: currentFrontTx,
      candidate_front_tx_mm: targetFrontTx,
      front_shift_mm: shiftMm,
      target_x_gap_mm: targetGapMm,
    },
    expected_bbox_screen: {
      method: 'analytical shift of the current SW2020-native component bbox before rerun',
      door_bbox_mm: doorBox,
      shifted_front_frame_bbox_mm: shiftedFrontBox,
      door_to_front_frame: expectedPair,
      expected_x_gap_mm: expectedPair.axes.x.gap_mm,
      expected_read:
        expectedPair.aabb_intersects === false
          ? 'The candidate should be AABB-separated on X by the target gap when SW2020 rebuild preserves the same geometry and transform convention.'
          : 'The candidate still overlaps by AABB and should not be run as a clearance candidate.',
    },
    sw2020_candidate_rerun: {
      build_json_exists: swEvidence.build.exists,
      components_json_exists: swEvidence.components.exists,
      clearance_json_exists: swEvidence.clearance.exists,
      build_status: swEvidence.build.json?.build_status || null,
      opened: swEvidence.components.json?.opened === true,
      rebuilt: swEvidence.components.json?.rebuilt === true,
      component_count: swEvidence.components.json?.component_count ?? null,
      clearance_status: swClearance.status || null,
      door_front_pair: swDoorFrontPair,
      rerun_complete: swRerunComplete,
    },
    exact_or_engineering_gap_review: {
      status: swRerunComplete
        ? 'SW2020_NATIVE_AABB_SCREEN_CLEAR_EXACT_GAP_DATUM_REVIEW_STILL_REQUIRED'
        : 'SW2020_NATIVE_RERUN_REQUIRED_BEFORE_EXACT_GAP_DATUM_REVIEW',
      reason:
        'AABB separation proves no solid intersection is possible only after SW2020 rerun confirms the candidate transform; it does not sign off real door reveal, coating allowance, hinge swing, or production tolerance stack.',
      next_commands: [
        `workers\\solidworks_tools\\bin\\BuildPlacedComponentsModule.exe ${PATHS.candidatePlacements} ${PATHS.candidateAssembly} ${PATHS.candidateBuild}`,
        `workers\\solidworks_tools\\bin\\InspectAssemblyComponents.exe ${PATHS.candidateAssembly} ${PATHS.candidateComponents} --require-rebuild`,
        `node tools\\build_16029_min_interface_sw_bbox_clearance_report.mjs --components ${PATHS.candidateComponents} --out ${PATHS.candidateClearance} --assembly-id door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate`,
      ],
    },
    release_boundary: {
      supplier_release_status: 'supplier_release_excluded',
      supplier_precheck_status: 'BLOCKED',
      can_issue_supplier_dxf_now: false,
      can_expand_full_cabinet_now: false,
      cad_modified_by_this_report: false,
      formal_placement_overwritten: false,
    },
  }
}

function markdown(report) {
  const pair = report.expected_bbox_screen.door_to_front_frame
  const sw = report.sw2020_candidate_rerun
  return `# 16029 门板-前框候选 Placement 复核包

生成时间：${report.generated_at}

## 结论

- 状态：\`${report.status}\`
- 候选：固定门板，前框 tx 从 \`${report.placement_delta.current_front_tx_mm} mm\` 调到 \`${report.placement_delta.candidate_front_tx_mm} mm\`
- 前框相对偏移：\`${report.placement_delta.front_shift_mm} mm\`
- 目标 X 门缝：\`${report.placement_delta.target_x_gap_mm} mm\`
- 预期 AABB：\`${pair.clearance_conclusion}\`
- 预期 X 间隙：\`${report.expected_bbox_screen.expected_x_gap_mm} mm\`

## 文件

- 候选 placement：\`${report.candidate_files.candidate_placements_tsv}\`
- 候选 SLDASM：\`${report.candidate_files.candidate_assembly_path}\`
- 候选 build：\`${report.candidate_files.candidate_build_json}\`
- 候选 components：\`${report.candidate_files.candidate_components_json}\`
- 候选 clearance：\`${report.candidate_files.candidate_clearance_json}\`

## 预期 BBox

| Item | xmin | xmax |
|---|---:|---:|
| Door | ${report.expected_bbox_screen.door_bbox_mm.xmin_mm} | ${report.expected_bbox_screen.door_bbox_mm.xmax_mm} |
| Shifted front frame | ${report.expected_bbox_screen.shifted_front_frame_bbox_mm.xmin_mm} | ${report.expected_bbox_screen.shifted_front_frame_bbox_mm.xmax_mm} |

## SW2020 候选 rerun

- build exists：\`${sw.build_json_exists}\`
- components exists：\`${sw.components_json_exists}\`
- clearance exists：\`${sw.clearance_json_exists}\`
- build status：\`${sw.build_status ?? 'not_run'}\`
- opened/rebuilt/count：\`${sw.opened}/${sw.rebuilt}/${sw.component_count ?? 'not_run'}\`
- clearance status：\`${sw.clearance_status ?? 'not_run'}\`
- rerun complete：\`${sw.rerun_complete}\`

## 下一步命令

${report.exact_or_engineering_gap_review.next_commands.map((command, index) => `${index + 1}. \`${command}\``).join('\n')}

## 边界

- 不修改正式 CAD：\`${report.release_boundary.cad_modified_by_this_report}\`
- 不覆盖正式 placement：\`${report.release_boundary.formal_placement_overwritten}\`
- supplier release：\`${report.release_boundary.supplier_release_status}\`
- supplier precheck：\`${report.release_boundary.supplier_precheck_status}\`
- 不进入完整柜体，不进入供应商 DXF release。
`
}

const report = buildReport()
for (const path of [PATHS.outJson, PATHS.outMd]) mkdirSync(dirname(abs(path)), { recursive: true })
writeFileSync(abs(PATHS.outJson), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
writeFileSync(abs(PATHS.outMd), markdown(report), 'utf8')
console.log(`${report.status}: ${PATHS.outJson}`)
console.log(`candidate placement: ${PATHS.candidatePlacements}`)
