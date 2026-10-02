import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  buildTrustedNativePlan,
  nativeRecipeDigest,
  resolveTrustedNativeRecipe,
} from './locker_16029_native_recipe_registry.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const fixtureOnly = process.argv.includes('--fixture-only')
const checks = []

function check(name, action) {
  try {
    action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error instanceof Error ? error.message : String(error) })
  }
}

function task(overrides = {}) {
  return {
    id: 'NATIVE-888-14-TEST',
    taskType: 'native_solidworks_build_task',
    requestFingerprint: 'a'.repeat(64),
    customerRequirementReference: '888宽14门测试任务',
    cabinetWidth: '888',
    cabinetHeight: '1917',
    cabinetDepth: '550',
    columns: '2',
    doorCount: '14',
    columnDoorCounts: [7, 7],
    rowSequence: 'L1111111-R1111111',
    doorWidth: '381',
    previewDimensionsValidated: false,
    ...overrides,
  }
}

check('registry_contains_only_compiled_888x14_recipe', () => {
  assert.deepEqual(Object.keys(TRUSTED_NATIVE_RECIPE_REGISTRY), [
    'winnsen-16029-888w-14door-native-v1',
  ])
  assert.equal(Object.isFrozen(TRUSTED_NATIVE_RECIPE_REGISTRY), true)
})

check('888x14_task_resolves_exact_trusted_geometry', () => {
  const recipe = resolveTrustedNativeRecipe(task())
  assert.equal(recipe.id, 'winnsen-16029-888w-14door-native-v1')
  assert.equal(recipe.version, 1)
  assert.deepEqual(recipe.geometry, {
    cabinetWidthMm: 888,
    cabinetHeightMm: 1917,
    cabinetDepthMm: 550,
    columns: 2,
    doorCount: 14,
    columnDoorCounts: [7, 7],
    rowSequence: 'L1111111-R1111111',
    doorPanelWidthMm: 381,
  })
})

check('exact_calculations_and_centers_are_not_rounded_preview_values', () => {
  const recipe = resolveTrustedNativeRecipe(task())
  const height = (12 / 7) * 152.5 - 7
  const pitch = height + 7
  const centers = Array.from({ length: 7 }, (_, index) => 32 + height / 2 + index * pitch)
  const boundaries = Array.from({ length: 6 }, (_, index) => 32 + (index + 1) * pitch - 3.5)
  assert.equal(recipe.calculations.doorHeightMm, height)
  assert.equal(recipe.calculations.doorPitchMm, pitch)
  assert.deepEqual(recipe.calculations.doorCenterYByColumnMm.L, centers)
  assert.deepEqual(recipe.calculations.doorCenterYByColumnMm.R, centers)
  assert.deepEqual(recipe.calculations.internalBoundaryYmm, boundaries)
  assert.deepEqual(recipe.calculations.shelfCenterYmm, boundaries)
  assert.deepEqual(recipe.calculations.frontFrameCrossbarCenterYmm, boundaries)
  assert.notEqual(recipe.calculations.doorHeightMm, 254.429)
})

check('topology_requires_fourteen_native_mechanical_interfaces', () => {
  const recipe = resolveTrustedNativeRecipe(task())
  assert.equal(recipe.expectedTopology.doorCount, 14)
  assert.equal(recipe.expectedTopology.mechanicalLockTongueCount, 14)
  assert.equal(recipe.expectedTopology.nativeLockSlotCount, 14)
  assert.equal(recipe.expectedTopology.pairedDiameter5CircleCount, 14)
  assert.equal(recipe.expectedTopology.shelfModuleCount, 12)
  assert.equal(recipe.expectedTopology.frontFrameCrossbarCount, 12)
  assert.equal(recipe.expectedTopology.lockSlotProfile.nativeCrossBendEdgeCountPerSlot, 22)
  assert.equal(recipe.expectedTopology.lockSlotProfile.pairedCircleDiameterMm, 5)
  assert.equal(recipe.hardwarePolicy.electricalComponentsAllowed, false)
  assert.equal(recipe.hardwarePolicy.lockKind, 'mechanical_lock_tongue')
})

check('trusted_sources_separate_v37_mechanics_from_1000w_14door_rules', () => {
  const sources = resolveTrustedNativeRecipe(task()).trustedSources
  assert.equal(sources.mechanicalAndLockSource.seedId, 'v37-760w-six-door-l642-r246')
  assert.match(sources.mechanicalAndLockSource.rootAssemblySha256, /^[A-F0-9]{64}$/)
  assert.equal(sources.layoutRuleEvidence.kind, 'gold_source_rule_evidence')
  assert.equal(sources.layoutRuleEvidence.scope, 'rules_only')
  const evidence = readFileSync(resolve(ROOT, fixtureOnly
    ? 'configs/locker_16029_layout_rule_evidence.status.txt'
    : sources.layoutRuleEvidence.sourceRelativePath))
  assert.equal(createHash('sha256').update(evidence).digest('hex').toUpperCase(),
    sources.layoutRuleEvidence.evidenceSha256)
  assert.ok(sources.layoutRuleEvidence.forbiddenReuse.includes('electrical_components'))
  assert.ok(sources.layoutRuleEvidence.forbiddenReuse.includes('legacy_step_or_freecad_generation'))
})

check('required_checks_cover_lock_rebuild_and_relocated_reopen', () => {
  const required = resolveTrustedNativeRecipe(task()).requiredChecks
  for (const name of [
    'one_door_one_lock',
    'rebuild_save_reopen',
    'relocated_reopen',
    'component_reference_closure',
    'no_electrical_components',
  ]) assert.ok(required.includes(name), name)
})

check('build_plan_contains_data_only_tool_ids_and_stays_not_ready', () => {
  const plan = buildTrustedNativePlan(task())
  assert.equal(plan.schema, 'winnsen.trusted_native_build_plan.v1')
  assert.equal(plan.recipeId, 'winnsen-16029-888w-14door-native-v1')
  assert.equal(plan.recipeVersion, 1)
  assert.equal(plan.purpose, 'structure_engineering_assistance')
  assert.match(plan.recipeDigest, /^[a-f0-9]{64}$/)
  assert.equal(plan.requestFingerprint, 'a'.repeat(64))
  assert.equal(plan.qualityBoundary.planningOnly, true)
  assert.equal(plan.qualityBoundary.engineeringAssistanceReady, false)
  for (const stage of plan.stages) {
    assert.match(stage.toolId, /^[a-z0-9_]+_v1$/)
    for (const forbidden of ['command', 'script', 'path', 'args']) {
      assert.equal(forbidden in stage, false, `${stage.id}:${forbidden}`)
    }
  }
})

check('stage_contract_uses_only_the_new_native_executor_ids', () => {
  const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
  assert.deepEqual(recipe.stages.map((stage) => stage.toolId), [
    'native_task_guard_v1',
    'native_seed_pack_888x14_v1',
    'native_width_888_v1',
    'native_door_module_888x14_v1',
    'native_lock_topology_888x14_v1',
    'native_root_assembly_888x14_v1',
    'native_final_pack_888x14_v1',
  ])
})

check('recipe_digest_is_stable_and_sensitive', () => {
  const recipe = resolveTrustedNativeRecipe(task())
  const first = nativeRecipeDigest(recipe)
  const second = nativeRecipeDigest('winnsen-16029-888w-14door-native-v1')
  assert.match(first, /^[a-f0-9]{64}$/)
  assert.equal(first, second)
  recipe.geometry.doorCount = 12
  assert.notEqual(nativeRecipeDigest(recipe), first)
})

check('generic_unregistered_geometry_returns_null', () => {
  for (const overrides of [
    { cabinetWidth: '887' },
    { cabinetHeight: '1900' },
    { cabinetDepth: '500' },
    { columns: '3' },
    { doorCount: '12', columnDoorCounts: [6, 6], rowSequence: 'L111111-R111111' },
    { columnDoorCounts: [8, 6] },
    { rowSequence: 'L642-R246' },
    { doorWidth: '380' },
  ]) assert.equal(resolveTrustedNativeRecipe(task(overrides)), null)
})

check('candidate_recipe_and_execution_injection_are_rejected', () => {
  for (const injection of [
    { candidateRecipeId: 'winnsen-16029-888w-14door-native-v1' },
    { candidateRecipe: { id: 'winnsen-16029-888w-14door-native-v1' } },
    { command: 'powershell -File injected.ps1' },
    { script: 'injected.ps1' },
    { path: 'D:\\injected' },
    { nativeBuild: { commandLine: 'anything' } },
    { normalizedRequest: { ...task(), outputPath: 'D:\\injected' } },
  ]) {
    assert.equal(resolveTrustedNativeRecipe(task(injection)), null, JSON.stringify(injection))
    assert.equal(buildTrustedNativePlan(task(injection)), null, JSON.stringify(injection))
  }
})

check('empty_portal_candidate_recipe_field_is_ignored', () => {
  assert.equal(resolveTrustedNativeRecipe(task({ candidateRecipeId: '' })).id,
    'winnsen-16029-888w-14door-native-v1')
})

check('registry_source_has_no_process_or_filesystem_execution_surface', () => {
  const source = readFileSync(resolve(ROOT, 'tools/locker_16029_native_recipe_registry.mjs'), 'utf8')
  for (const forbidden of [
    "from 'node:child_process'",
    "from 'node:fs'",
    'execSync(',
    'spawn(',
    'powershell.exe',
    'FreeCADCmd.exe',
  ]) assert.equal(source.includes(forbidden), false, forbidden)
})

const failed = checks.filter((item) => !item.ok)
const result = {
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  mode: fixtureOnly ? 'fixture-only' : 'full',
  layoutEvidenceSource: fixtureOnly ? 'versioned-snapshot' : 'workstation-handoff',
  checks,
}
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exit(1)
