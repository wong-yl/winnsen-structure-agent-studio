import { createHash } from 'node:crypto'

const RECIPE_ID = 'winnsen-16029-888w-14door-native-v1'
const DOOR_HEIGHT_MM = (12 / 7) * 152.5 - 7
const DOOR_PITCH_MM = DOOR_HEIGHT_MM + 7
const DOOR_BOTTOM_Y_MM = 32
const DOOR_CENTER_Y_MM = Object.freeze(Array.from(
  { length: 7 },
  (_, index) => DOOR_BOTTOM_Y_MM + (DOOR_HEIGHT_MM / 2) + (index * DOOR_PITCH_MM),
))
const INTERNAL_BOUNDARY_Y_MM = Object.freeze(Array.from(
  { length: 6 },
  (_, index) => DOOR_BOTTOM_Y_MM + ((index + 1) * DOOR_PITCH_MM) - 3.5,
))

const FORBIDDEN_TASK_KEYS = Object.freeze(new Set([
  'args',
  'arguments',
  'binary',
  'binarypath',
  'cadpath',
  'candidatepath',
  'candidaterecipe',
  'command',
  'commandline',
  'cwd',
  'destination',
  'destinationpath',
  'executable',
  'executablepath',
  'inputpath',
  'outputpath',
  'path',
  'paths',
  'powershell',
  'recipe',
  'recipeoverride',
  'rootpath',
  'script',
  'scriptpath',
  'shell',
  'sourcepath',
  'toolpath',
  'workingdirectory',
]))
const FORBIDDEN_TASK_BINDING_KEYS = Object.freeze(new Set([
  'candidaterecipeid',
  'recipeid',
]))

function canonicalize(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalize).join(',')}]`
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalize(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

function sha256(value) {
  return createHash('sha256').update(value, 'utf8').digest('hex')
}

function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value
  for (const child of Object.values(value)) deepFreeze(child)
  return Object.freeze(value)
}

function clone(value) {
  return structuredClone(value)
}

function numberFrom(record, keys) {
  for (const key of keys) {
    const value = Number(record?.[key])
    if (Number.isFinite(value)) return value
  }
  return null
}

function stringFrom(record, keys) {
  for (const key of keys) {
    if (typeof record?.[key] === 'string' && record[key].trim()) return record[key].trim()
  }
  return ''
}

function equalNumbers(actual, expected, tolerance = 1e-9) {
  return Number.isFinite(actual) && Math.abs(actual - expected) <= tolerance
}

function equalNumberArray(actual, expected) {
  return Array.isArray(actual) && actual.length === expected.length &&
    actual.every((value, index) => equalNumbers(Number(value), expected[index]))
}

function containsForbiddenTaskKey(value, seen = new Set()) {
  if (!value || typeof value !== 'object') return false
  if (seen.has(value)) return true
  seen.add(value)
  for (const [key, child] of Object.entries(value)) {
    const normalized = key.toLowerCase().replace(/[^a-z0-9]/g, '')
    if (FORBIDDEN_TASK_KEYS.has(normalized)) return true
    if (FORBIDDEN_TASK_BINDING_KEYS.has(normalized) && String(child ?? '').trim()) return true
    if (containsForbiddenTaskKey(child, seen)) return true
  }
  return false
}

const RECIPE = deepFreeze({
  schema: 'winnsen.trusted_native_recipe.v1',
  id: RECIPE_ID,
  version: 1,
  product: '16029',
  purpose: 'structure_engineering_assistance',
  lifecycle: 'planning',
  contractKind: 'solidworks2020_native_builder_contract',
  geometry: {
    cabinetWidthMm: 888,
    cabinetHeightMm: 1917,
    cabinetDepthMm: 550,
    columns: 2,
    doorCount: 14,
    columnDoorCounts: [7, 7],
    rowSequence: 'L1111111-R1111111',
    doorPanelWidthMm: 381,
  },
  calculations: {
    source: 'compiled_trusted_recipe',
    doorHeightFormula: '(12 / 7) * 152.5 - 7',
    doorHeightMm: DOOR_HEIGHT_MM,
    doorPitchFormula: 'doorHeightMm + 7',
    doorPitchMm: DOOR_PITCH_MM,
    doorBottomYMinMm: DOOR_BOTTOM_Y_MM,
    doorCenterYFormula: 'doorBottomYMinMm + doorHeightMm / 2 + rowIndexZeroBased * doorPitchMm',
    doorCenterYByColumnMm: {
      L: DOOR_CENTER_Y_MM,
      R: DOOR_CENTER_Y_MM,
    },
    internalBoundaryYFormula: 'doorBottomYMinMm + boundaryIndexOneBased * doorPitchMm - gapMm / 2',
    internalBoundaryYmm: INTERNAL_BOUNDARY_Y_MM,
    shelfCenterYmm: INTERNAL_BOUNDARY_Y_MM,
    frontFrameCrossbarCenterYmm: INTERNAL_BOUNDARY_Y_MM,
    gapMm: 7,
  },
  expectedTopology: {
    doorCount: 14,
    mechanicalLockTongueCount: 14,
    nativeLockSlotCount: 14,
    pairedDiameter5CircleCount: 14,
    shelfModuleCount: 12,
    frontFrameCrossbarCount: 12,
    perColumn: {
      L: { doors: 7, mechanicalLockTongues: 7, nativeLockSlots: 7, pairedDiameter5Circles: 7, shelves: 6, frontFrameCrossbars: 6 },
      R: { doors: 7, mechanicalLockTongues: 7, nativeLockSlots: 7, pairedDiameter5Circles: 7, shelves: 6, frontFrameCrossbars: 6 },
    },
    lockSlotProfile: {
      nativeCrossBendEdgeCountPerSlot: 22,
      pairedCircleDiameterMm: 5,
      pairedCircleEdgeCountPerOpening: 2,
      pairedCircleCenterOffsetFromSlotMm: -60,
    },
  },
  hardwarePolicy: {
    lockKind: 'mechanical_lock_tongue',
    oneDoorOneLockOpening: true,
    electricalComponentsAllowed: false,
    electricLockAllowed: false,
    controlBoardAllowed: false,
  },
  trustedSources: {
    mechanicalAndLockSource: {
      kind: 'verified_native_seed',
      seedId: 'v37-760w-six-door-l642-r246',
      verifiedGeometry: '760x1917x550 / 2 columns / 6 doors / L642-R246',
      rootAssemblySha256: '5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B',
      allowedReuse: ['mechanical_lock_tongue', 'native_lock_slot_profile', 'paired_diameter_5_circle_profile'],
      forbiddenInference: 'V37 does not validate the 888 mm width or seven equal-height rows.',
    },
    layoutRuleEvidence: {
      kind: 'gold_source_rule_evidence',
      scope: 'rules_only',
      evidenceId: '16029-1000w-14door-rule-evidence-20260519',
      verifiedGeometry: '1000x1917x550 / 2 columns / 14 doors',
      sourceRelativePath: 'workers/handoffs/16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519/CHECK_HANDOFF_READY.status.txt',
      evidenceSha256: 'A1C15076110AF9954039B66599C1558F0B6F4C2F0FA04F3048656B06F46E5D21',
      allowedReuse: ['seven_rows_per_column', 'door_height_formula', 'door_pitch_formula', 'six_internal_boundaries', 'twelve_shelves', 'twelve_front_frame_crossbars'],
      forbiddenReuse: ['electrical_components', 'electric_lock_hook', 'legacy_step_or_freecad_generation'],
    },
  },
  stages: [
    { id: 'claim_and_validate', toolId: 'native_task_guard_v1', outputs: ['validated_input', 'trusted_recipe_binding'] },
    { id: 'clone_native_seed', toolId: 'native_seed_pack_888x14_v1', outputs: ['isolated_native_cad_staging', 'seed_inventory'] },
    { id: 'configure_native_width', toolId: 'native_width_888_v1', outputs: ['dimensions', 'derived_parts', 'base_hole', 'width_assemblies'] },
    { id: 'build_native_door_module', toolId: 'native_door_module_888x14_v1', outputs: ['verified_13_file_door_module', 'flat_import_manifest'] },
    { id: 'build_native_lock_topology', toolId: 'native_lock_topology_888x14_v1', outputs: ['seven_row_lock_openings', 'mirrored_native_right_partition'] },
    { id: 'assemble_native_model', toolId: 'native_root_assembly_888x14_v1', outputs: ['fourteen_doors', 'twelve_shelves', 'twelve_frame_crossbars', 'root_assembly'] },
    { id: 'relocate_reopen_and_package', toolId: 'native_final_pack_888x14_v1', outputs: ['relocated_reopen_evidence', 'native_assistance_model_package', 'receipt'] },
  ],
  requiredChecks: [
    'trusted_recipe_digest_match',
    'solidworks_2020_native_only',
    'no_legacy_fallback',
    'no_electrical_components',
    'measured_cabinet_envelope_matches_recipe',
    'door_count_and_row_centers_match_recipe',
    'shelf_and_front_frame_boundaries_match_recipe',
    'one_door_one_lock',
    'native_lock_slot_edge_profile',
    'paired_diameter_5_circle_per_lock_opening',
    'mechanical_lock_tongue_alignment',
    'sheet_metal_and_flat_pattern_preserved',
    'rebuild_save_reopen',
    'component_reference_closure',
    'zero_missing_or_external_component_files',
    'relocated_reopen',
    'final_hash_inventory_receipt',
  ],
  qualityBoundary: {
    planningOnly: true,
    previewDimensionsValidated: false,
    engineeringAssistanceReady: false,
    readyOnlyAfterEveryRequiredCheckPasses: true,
  },
})

export const TRUSTED_NATIVE_RECIPE_REGISTRY = deepFreeze({ [RECIPE_ID]: RECIPE })

export function nativeRecipeDigest(recipeOrId) {
  const recipe = typeof recipeOrId === 'string'
    ? TRUSTED_NATIVE_RECIPE_REGISTRY[recipeOrId]
    : recipeOrId
  if (!recipe || typeof recipe !== 'object') return ''
  return sha256(canonicalize(recipe))
}

function normalizedTaskGeometry(task) {
  const source = task?.normalizedRequest && typeof task.normalizedRequest === 'object'
    ? task.normalizedRequest
    : task
  if (!source || typeof source !== 'object') return null
  return {
    cabinetWidthMm: numberFrom(source, ['cabinetWidthMm', 'cabinetWidth']),
    cabinetHeightMm: numberFrom(source, ['cabinetHeightMm', 'cabinetHeight']),
    cabinetDepthMm: numberFrom(source, ['cabinetDepthMm', 'cabinetDepth']),
    columns: numberFrom(source, ['columns']),
    doorCount: numberFrom(source, ['doorCount']),
    columnDoorCounts: source.columnDoorCounts,
    rowSequence: stringFrom(source, ['rowSequence']),
    doorPanelWidthMm: numberFrom(source, ['estimatedDoorPanelWidthMm', 'doorWidth']),
  }
}

function exactRecipeGeometryMatch(geometry, recipe) {
  const expected = recipe.geometry
  return equalNumbers(geometry.cabinetWidthMm, expected.cabinetWidthMm) &&
    equalNumbers(geometry.cabinetHeightMm, expected.cabinetHeightMm) &&
    equalNumbers(geometry.cabinetDepthMm, expected.cabinetDepthMm) &&
    equalNumbers(geometry.columns, expected.columns) &&
    equalNumbers(geometry.doorCount, expected.doorCount) &&
    equalNumberArray(geometry.columnDoorCounts, expected.columnDoorCounts) &&
    geometry.rowSequence === expected.rowSequence &&
    equalNumbers(geometry.doorPanelWidthMm, expected.doorPanelWidthMm)
}

export function resolveTrustedNativeRecipe(task = {}) {
  if (!task || typeof task !== 'object' || Array.isArray(task)) return null
  if (containsForbiddenTaskKey(task)) return null
  const taskType = stringFrom(task, ['taskType']) || stringFrom(task.normalizedRequest, ['taskType'])
  if (taskType && taskType !== 'native_solidworks_build_task') return null
  const geometry = normalizedTaskGeometry(task)
  if (!geometry) return null
  for (const recipe of Object.values(TRUSTED_NATIVE_RECIPE_REGISTRY)) {
    if (exactRecipeGeometryMatch(geometry, recipe)) return clone(recipe)
  }
  return null
}

function taskIdentity(task) {
  const normalized = task?.normalizedRequest && typeof task.normalizedRequest === 'object'
    ? task.normalizedRequest
    : {}
  return {
    taskId: stringFrom(task, ['id', 'taskId', 'requestId']),
    requestFingerprint: stringFrom(task, ['requestFingerprint']) || stringFrom(normalized, ['requestFingerprint']),
    customerRequirementReference: stringFrom(task, ['customerRequirementReference']) ||
      stringFrom(normalized, ['customerRequirementReference']),
  }
}

export function buildTrustedNativePlan(task = {}) {
  const recipe = resolveTrustedNativeRecipe(task)
  if (!recipe) return null
  const identity = taskIdentity(task)
  return {
    schema: 'winnsen.trusted_native_build_plan.v1',
    taskId: identity.taskId,
    requestFingerprint: identity.requestFingerprint,
    customerRequirementReference: identity.customerRequirementReference,
    recipeId: recipe.id,
    recipeVersion: recipe.version,
    recipeDigest: nativeRecipeDigest(recipe),
    purpose: recipe.purpose,
    lifecycle: 'planning',
    geometry: clone(recipe.geometry),
    calculations: clone(recipe.calculations),
    expectedTopology: clone(recipe.expectedTopology),
    hardwarePolicy: clone(recipe.hardwarePolicy),
    trustedSources: clone(recipe.trustedSources),
    stages: clone(recipe.stages),
    requiredChecks: [...recipe.requiredChecks],
    qualityBoundary: clone(recipe.qualityBoundary),
  }
}
