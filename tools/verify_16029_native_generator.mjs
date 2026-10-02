import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_16029_GENERATOR,
  NATIVE_16029_PENDING_RECIPES,
  NATIVE_16029_SEEDS,
  is16029StructureAssistanceReady,
  native16029RequestFingerprint,
  normalize16029NativeModelRequest,
} from './locker_16029_native_generator.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const checks = []

function check(name, action) {
  try {
    action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error instanceof Error ? error.message : String(error) })
  }
}

function request(width, doorCount, overrides = {}) {
  return normalize16029NativeModelRequest({
    customerRequirementReference: '脱敏测试任务',
    widthInputMode: 'cabinet_outer_width',
    requestedWidthMm: width,
    cabinetDepth: 550,
    doorCount,
    ...overrides,
  })
}

check('generator_identity_is_native_seed_only', () => {
  assert.equal(NATIVE_16029_GENERATOR.cad, 'SolidWorks 2020')
  assert.equal(NATIVE_16029_GENERATOR.sourcePolicy, 'verified_native_seeds_only')
  assert.deepEqual(NATIVE_16029_GENERATOR.supportedDoorCounts, [4, 6])
  assert.deepEqual(NATIVE_16029_GENERATOR.nativeTaskDoorCountRange, { min: 2, max: 48, step: 2 })
  assert.equal(NATIVE_16029_GENERATOR.unmatchedRequestPolicy, 'create_native_build_task')
})

check('parametric_request_preserves_mixed_rows_and_binds_identity', () => {
  const parametricRequest = { cabinet: { widthMm: 839, heightMm: 1917, depthMm: 550 }, columns: [
    { side: 'L', doors: [{ heightUnits: 6 }, { heightUnits: 4 }, { heightUnits: 2 }] },
    { side: 'R', doors: [{ heightUnits: 2 }, { heightUnits: 4 }, { heightUnits: 6 }] },
  ] }
  const first = request(839, 6, { parametricRequest })
  assert.equal(first.ok, true)
  assert.equal(first.matchType, 'native_build_task')
  assert.deepEqual(first.normalizedRequest.parametricRequest, parametricRequest)
  const secondInput = structuredClone(parametricRequest)
  secondInput.columns[0].doors.reverse()
  const second = request(839, 6, { parametricRequest: secondInput })
  assert.notEqual(first.normalizedRequest.requestFingerprint, second.normalizedRequest.requestFingerprint)
  const unsupported = structuredClone(parametricRequest)
  assert.equal(request(839, 6, { parametricRequest: { ...parametricRequest, cabinet: { ...parametricRequest.cabinet, heightMm: 2017 } } }).ok, true)
  for (const depthMm of [550, 575, 600]) {
    const combined = { ...parametricRequest, cabinet: { widthMm: 877, heightMm: 1967, depthMm } }
    const accepted = request(877, 6, { parametricRequest: combined })
    assert.equal(accepted.ok, true)
    assert.deepEqual(accepted.normalizedRequest.parametricRequest.cabinet, combined.cabinet)
  }
  for (const depthMm of [249.9, 650.1]) assert.equal(request(839, 6, { parametricRequest: { ...parametricRequest, cabinet: { ...parametricRequest.cabinet, depthMm } } }).ok, false)
  unsupported.cabinet.heightMm = 2200.1
  assert.equal(request(839, 6, { parametricRequest: unsupported }).ok, false)
  assert.equal(request(839, 6, { parametricRequest: { ...parametricRequest, envelopeProbe: {} } }).ok, false)
})

check('parametric_expanded_range_accepts_boundaries_and_preserves_pending_tasks', () => {
  const columns = [
    { side: 'L', doors: [{ heightUnits: 6 }, { heightUnits: 4 }, { heightUnits: 2 }] },
    { side: 'R', doors: [{ heightUnits: 2 }, { heightUnits: 4 }, { heightUnits: 6 }] },
  ]
  for (const cabinet of [
    { widthMm: 700, heightMm: 1700, depthMm: 250 },
    { widthMm: 1200, heightMm: 2200, depthMm: 650 },
    { widthMm: 700, heightMm: 2200, depthMm: 250 },
    { widthMm: 1200, heightMm: 1700, depthMm: 650 },
    { widthMm: 850, heightMm: 1950, depthMm: 380 },
  ]) {
    const parametricRequest = { cabinet, columns }
    const result = request(cabinet.widthMm, 6, { parametricRequest })
    assert.equal(result.ok, true)
    assert.equal(result.status, 'native_task_created')
    assert.equal(result.matchType, 'native_build_task')
    assert.deepEqual(result.normalizedRequest.parametricRequest, parametricRequest)
    assert.equal(result.normalizedRequest.deliveryMode, 'native_task_pending')
    assert.equal(result.normalizedRequest.engineeringAssistanceReady, false)
    assert.equal(result.normalizedRequest.previewDimensionsValidated, false)
  }
  for (const cabinet of [
    { widthMm: 699.9, heightMm: 1950, depthMm: 380 },
    { widthMm: 1200.1, heightMm: 1950, depthMm: 380 },
    { widthMm: 850, heightMm: 1699.9, depthMm: 380 },
    { widthMm: 850, heightMm: 2200.1, depthMm: 380 },
    { widthMm: 850, heightMm: 1950, depthMm: 249.9 },
    { widthMm: 850, heightMm: 1950, depthMm: 650.1 },
  ]) {
    const result = request(cabinet.widthMm, 6, { parametricRequest: { cabinet, columns } })
    assert.equal(result.ok, false)
    assert.equal(result.reason, 'invalid_parametric_request')
  }
})

check('exact_v35_v36_v37_requests_resolve', () => {
  const v35 = request(740, 6)
  const v36 = request(740, 4)
  const v37 = request(760, 6)
  assert.equal(v35.ok, true)
  assert.equal(v35.seed.version, 'V35')
  assert.equal(v36.ok, true)
  assert.equal(v36.seed.version, 'V36')
  assert.equal(v37.ok, true)
  assert.equal(v37.seed.version, 'V37')
  for (const result of [v35, v36, v37]) {
    assert.equal(result.normalizedRequest.deliveryMode, 'verified_native_model')
    assert.equal(result.normalizedRequest.engineeringAssistanceReady, true)
    assert.equal(result.normalizedRequest.engineeringDeepeningRequired, true)
  }
})

check('door_panel_width_maps_to_same_seed', () => {
  const result = request(317, 6, { widthInputMode: 'installed_door_panel_width' })
  assert.equal(result.ok, true)
  assert.equal(result.seed.version, 'V37')
  assert.equal(result.normalizedRequest.cabinetWidth, 760)
})

check('v38_combination_is_named_but_not_claimed_ready', () => {
  const result = request(760, 4)
  assert.equal(result.ok, true)
  assert.equal(result.status, 'native_task_created')
  assert.equal(result.matchType, 'native_build_task')
  assert.equal(result.pendingRecipe.id, 'v38-760w-four-door-l66-r66')
  assert.deepEqual(result.pendingRecipe.sourceSeedIds, [
    'v37-760w-six-door-l642-r246',
    'v36-740w-four-door-l66-r66',
  ])
  assert.equal(result.normalizedRequest.deliveryMode, 'native_task_pending')
  assert.equal(result.normalizedRequest.engineeringAssistanceReady, false)
  assert.equal(result.normalizedRequest.previewDimensionsValidated, false)
})

check('unmatched_valid_dimensions_create_native_tasks', () => {
  for (const result of [
    request(750, 6),
    request(760, 10),
    request(760, 6, { cabinetDepth: 500 }),
  ]) {
    assert.equal(result.ok, true)
    assert.equal(result.status, 'native_task_created')
    assert.equal(result.matchType, 'native_build_task')
    assert.equal(result.normalizedRequest.resultKind, 'solidworks2020_native_build_task')
    assert.equal(result.normalizedRequest.taskType, 'native_solidworks_build_task')
    assert.equal(result.normalizedRequest.engineeringAssistanceReady, false)
    assert.equal(result.normalizedRequest.engineeringDeepeningRequired, true)
    assert.equal(result.normalizedRequest.legacyFallbackUsed, false)
  }
})

check('888w_fourteen_door_request_has_explicit_unvalidated_preview', () => {
  const result = request(888, 14)
  assert.equal(result.ok, true)
  assert.equal(result.status, 'native_task_created')
  assert.equal(result.matchType, 'native_build_task')
  assert.equal(result.normalizedRequest.columns, 2)
  assert.deepEqual(result.normalizedRequest.columnDoorCounts, [7, 7])
  assert.equal(result.normalizedRequest.rowSequence, 'L1111111-R1111111')
  assert.equal(result.normalizedRequest.estimatedDoorPanelWidthMm, 381)
  assert.equal(result.normalizedRequest.estimatedDoorHeightMm, 254.429)
  assert.equal(result.normalizedRequest.doorWidth, 381)
  assert.equal(result.normalizedRequest.doorHeight, 254.429)
  assert.equal(result.normalizedRequest.previewDimensionsValidated, false)
  assert.equal(result.normalizedRequest.validationRequired.includes('one_door_one_lock_opening'), true)
  assert.equal(result.preview.previewDimensionsValidated, false)
  assert.equal('installedDoorPanelWidthMm' in result.preview, false)
  assert.equal('referenceDoorHeightMm' in result.preview, false)
})

check('invalid_geometry_and_uneven_door_counts_remain_rejected', () => {
  assert.equal(request(0, 6).reason, 'invalid_requested_width')
  assert.equal(request(2001, 6).reason, 'invalid_requested_width')
  assert.equal(request(760, 6, { cabinetDepth: 0 }).reason, 'invalid_cabinet_depth')
  assert.equal(request(760, 6, { cabinetDepth: 801 }).reason, 'invalid_cabinet_depth')
  assert.equal(request(760, 5).reason, 'uneven_two_column_door_count')
  assert.equal(request(760, 50).reason, 'door_count_out_of_range')
  assert.equal(request(760, 6, { columns: 3 }).reason, 'unsupported_column_count')
})

check('request_fingerprint_is_stable_for_same_customer_and_geometry', () => {
  const cabinetWidthRequest = {
    customerRequirementReference: '脱敏客户任务A',
    widthInputMode: 'cabinet_outer_width',
    requestedWidthMm: 888,
    cabinetDepth: 550,
    columns: 2,
    doorCount: 14,
  }
  const installedDoorWidthRequest = {
    ...cabinetWidthRequest,
    widthInputMode: 'installed_door_panel_width',
    requestedWidthMm: 381,
  }
  const first = native16029RequestFingerprint(cabinetWidthRequest)
  const second = native16029RequestFingerprint(installedDoorWidthRequest)
  assert.match(first, /^[a-f0-9]{64}$/)
  assert.equal(first, second)
  assert.equal(request(888, 14, {
    customerRequirementReference: '脱敏客户任务A',
  }).normalizedRequest.requestFingerprint, first)
  assert.notEqual(first, native16029RequestFingerprint({
    ...cabinetWidthRequest,
    customerRequirementReference: '脱敏客户任务B',
  }))
  assert.equal(native16029RequestFingerprint({ ...cabinetWidthRequest, doorCount: 5 }), '')
})

check('electrical_hardware_request_is_rejected', () => {
  const result = request(760, 6, { prompt: 'include electric lock body and control board' })
  assert.equal(result.ok, false)
  assert.equal(result.reason, 'unsupported_electrical_hardware_request')
  const chineseResult = request(760, 6, { prompt: '增加电控锁' })
  assert.equal(chineseResult.ok, false)
  assert.equal(chineseResult.reason, 'unsupported_electrical_hardware_request')
  assert.equal(chineseResult.message, '当前原生模型族只处理机械锁舌结构，不自动加入电器板或电控锁实体。')
  for (const overrides of [
    { lockType: '电控锁' },
    { lockType: 'electric lock' },
    { openings: '电器板' },
    { prompt: 'no electrical board, add electric lock' },
    { lockType: '电控锁', openings: '不含电器板' },
    { lockType: 'electric lock', openings: 'without control board' },
  ]) {
    assert.equal(request(760, 6, overrides).reason, 'unsupported_electrical_hardware_request')
  }
  assert.equal(request(760, 6, {
    lockType: '机械锁舌；排除电控锁实体',
    openings: '一门一锁孔；不含电器板',
  }).seed.version, 'V37')
})

check('three_seed_artifacts_are_pinned', () => {
  assert.equal(NATIVE_16029_SEEDS.length, 3)
  assert.equal(NATIVE_16029_PENDING_RECIPES.length, 1)
  for (const seed of NATIVE_16029_SEEDS) {
    assert.match(seed.zipSha256, /^[A-F0-9]{64}$/)
    assert.ok(seed.zipSizeBytes > 20_000_000)
    assert.ok(seed.validatedCapabilities.includes('one_door_one_lock_opening'))
    assert.ok(seed.validatedCapabilities.includes('rebuild_save_reopen'))
  }
})

check('ready_helper_accepts_only_native_seed_binding', () => {
  const result = request(760, 6)
  const readyRecord = {
    ...result.normalizedRequest,
    status: 'native_assistance_model_ready',
    taskType: 'verified_native_structure_assistance_model',
    modelReady: true,
    verifiedRecipeMatched: true,
    downloadUrl: `/download/${result.seed.assetId}`,
  }
  assert.equal(is16029StructureAssistanceReady(readyRecord), true)
  assert.equal(is16029StructureAssistanceReady({
    ...readyRecord,
    deliveryMode: 'legacy_generator',
    downloadUrl: '/anything',
  }), false)
  assert.equal(is16029StructureAssistanceReady({
    ...readyRecord,
    status: 'native_task_created',
    taskType: 'native_solidworks_build_task',
    cabinetWidth: 888,
    doorCount: 14,
    rowSequence: 'L1111111-R1111111',
  }), false)
  assert.equal(is16029StructureAssistanceReady({
    ...readyRecord,
    downloadUrl: '/download/16029-v43-v36-four-door-engineering-assistance-zip',
  }), false)
})

check('old_generator_files_are_absent', () => {
  const oldPaths = [
    'tools/generate_review_solidworks_full_assembly.ps1',
    'tools/generate_review_solidworks_single_door.ps1',
    'tools/generate_16029_parametric_scaffold_freecad.py',
    'tools/generate_review_task_simple_freecad_model.py',
    'tools/scale_16029_source_step_freecad.py',
    'tools/locker_16029_template_rules.mjs',
    'tools/locker_16029_controlled_generation_policy.mjs',
    'tools/process_16029_review_generation_queue.mjs',
    'tools/locker_16029_generation_cache.mjs',
    'tools/verify_16029_generation_cache.mjs',
    'tools/verify_16029_template_rule_matrix.mjs',
  ]
  assert.deepEqual(oldPaths.filter((path) => existsSync(resolve(ROOT, path))), [])
})

check('new_generator_has_no_legacy_pipeline_tokens', () => {
  const source = readFileSync(resolve(ROOT, 'tools/locker_16029_native_generator.mjs'), 'utf8')
  for (const forbidden of ['FreeCAD', 'parametric_scaffold', 'gold_rule_derived', 'manual_cad_worker']) {
    assert.equal(source.includes(forbidden), false, forbidden)
  }
})

const failed = checks.filter((item) => !item.ok)
const result = {
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  checks,
}
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exit(1)
