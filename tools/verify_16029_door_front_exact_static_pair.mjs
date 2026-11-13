import fs from 'node:fs'
import path from 'node:path'

const evidencePath = path.resolve(process.argv[2] || 'data/locker_16029_door_front_exact_static_pair.json')
const gatePath = path.resolve(process.argv[3] || 'data/locker_16029_door_front_exact_static_pair_gate.json')
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'))
const checks = []

function check(name, ok, actual, expected, category) {
  checks.push({ name, ok: Boolean(ok), actual, expected, category })
}

check('schema', evidence.schema === 'winnsen.locker16029.sw2020_exact_static_pair.v1', evidence.schema, 'exact static pair schema', 'evidence')
check('assembly_hash', /^[0-9A-F]{64}$/.test(evidence.assembly_sha256 || ''), evidence.assembly_sha256, 'SHA256', 'input')
check('opened_rebuilt', evidence.opened === true && evidence.rebuilt === true, { opened: evidence.opened, rebuilt: evidence.rebuilt }, 'true/true', 'sw2020')
check('three_component_candidate', evidence.assembly_component_count === 3, evidence.assembly_component_count, 3, 'sw2020')
check('door_unique', evidence.component_a_matches?.length === 1 && /door_panel_2_12_right_v43/i.test(evidence.component_a?.path || ''), evidence.component_a_matches, 'one door target', 'pair')
check('front_unique', evidence.component_b_matches?.length === 1 && /front_frame_vertical_left_v43/i.test(evidence.component_b?.path || ''), evidence.component_b_matches, 'one front-frame target', 'pair')
check('front_transform', Math.abs(Number(evidence.component_b?.total_transform?.tx_mm) - (-185.15)) < 1e-6, evidence.component_b?.total_transform?.tx_mm, -185.15, 'pair')
check('solid_bodies_present', Number(evidence.component_a?.solid_body_count) > 0 && Number(evidence.component_b?.solid_body_count) > 0, { a: evidence.component_a?.solid_body_count, b: evidence.component_b?.solid_body_count }, '>0 each', 'geometry')
check('exact_api', evidence.exact_interference_api === 'IModeler.CheckInterferenceBetweenTwoBodies', evidence.exact_interference_api, 'native exact API', 'geometry')
check('exact_result_explicit', ['EXACT_STATIC_INTERFERENCE_PASS', 'EXACT_STATIC_INTERFERENCE_FAIL'].includes(evidence.exact_static_status), evidence.exact_static_status, 'explicit PASS/FAIL', 'geometry')
check('minimum_distance_api', evidence.minimum_distance_api === 'IModelDoc2.ClosestDistance', evidence.minimum_distance_api, 'native closest distance API', 'geometry')
check('minimum_distance_explicit', evidence.minimum_distance_available === true ? Number.isFinite(Number(evidence.minimum_solid_distance_mm)) : evidence.minimum_solid_distance_mm == null, evidence.minimum_solid_distance_mm, 'number or explicit unavailable', 'geometry')
check('bbox_not_exact_claim', evidence.exact_interference_api !== 'bbox', evidence.exact_interference_api, 'not bbox', 'boundary')
check('door_gap_not_validated', evidence.engineering_door_gap_datum_status === 'NOT_VALIDATED', evidence.engineering_door_gap_datum_status, 'NOT_VALIDATED', 'boundary')
check('hinge_sweep_not_validated', evidence.hinge_sweep_status === 'NOT_VALIDATED', evidence.hinge_sweep_status, 'NOT_VALIDATED', 'boundary')
check('tolerance_chain_not_validated', evidence.tolerance_chain_status === 'NOT_VALIDATED', evidence.tolerance_chain_status, 'NOT_VALIDATED', 'boundary')
check('release_excluded', /formal CAD, placement, DXF, BOM, and full-cabinet release are excluded/i.test(evidence.release_boundary || ''), evidence.release_boundary, 'release exclusions', 'boundary')
check('no_runtime_error', !evidence.error && !evidence.hresult, { error: evidence.error, hresult: evidence.hresult }, 'empty', 'runtime')

const failed = checks.filter((item) => !item.ok)
const incomplete = evidence.status?.includes('INCOMPLETE') || evidence.exact_static_status === 'INCOMPLETE_NOT_RUN'
const gate = {
  schema: 'winnsen.locker16029.door_front_exact_static_pair_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length === 0 ? 'PASS' : incomplete ? 'INCOMPLETE' : 'FAIL',
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  native_bbox_status: 'SEPARATE_PRIOR_SCREEN_NOT_USED_AS_EXACT_RESULT',
  exact_static_interference_status: evidence.exact_static_status,
  minimum_solid_distance_mm: evidence.minimum_solid_distance_mm,
  engineering_door_gap_datum_status: evidence.engineering_door_gap_datum_status,
  hinge_sweep_status: evidence.hinge_sweep_status,
  tolerance_chain_status: evidence.tolerance_chain_status,
  supplier_release_status: 'EXCLUDED',
  blocker_classification: evidence.session_diagnostic?.blocker_classification || '',
  next_minimum_repair: evidence.session_diagnostic?.minimum_repair || '',
  checks,
}

fs.mkdirSync(path.dirname(gatePath), { recursive: true })
fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = failed.length === 0 ? 0 : 1
