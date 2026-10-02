import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { buildNativeAssemblyContract } from './lib/locker_16029_native_assembly_contract.mjs'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from './locker_16029_native_recipe_registry.mjs'

const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
const contract = buildNativeAssemblyContract(recipe)
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const V37_HIERARCHY_PATH = resolve(
  ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/evidence/v37_final_native_hierarchy_reopen.json',
)
const tolerance = 1e-9
const near = (left, right) => Math.abs(left - right) <= tolerance
const fixtureOnly = process.argv.includes('--fixture-only')

const checks = []
function check(name, action) {
  if (fixtureOnly && name === 'component_counts_are_derived_from_the_verified_v37_hierarchy') {
    checks.push({ name, skipped: true, reason: 'fixture-only mode excludes the workstation V37 CAD hierarchy evidence' })
    return
  }
  try {
    action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error?.message || String(error) })
  }
}

check('contract_identity_is_bound_to_the_trusted_recipe', () => {
  assert.equal(contract.schema, 'winnsen.native_16029_assembly_contract.v1')
  assert.equal(contract.purpose, 'structure_engineering_assistance')
  assert.equal(contract.recipe.id, recipe.id)
  assert.equal(contract.recipe.version, recipe.version)
  assert.equal(contract.recipe.digest, nativeRecipeDigest(recipe))
  assert.equal(contract.executionBoundary.planningOnly, true)
  assert.equal(contract.executionBoundary.engineeringAssistanceReady, false)
})

check('fourteen_doors_have_exact_global_centers_and_one_tongue_each', () => {
  assert.equal(contract.doors.length, 14)
  assert.equal(contract.doors.filter((row) => row.side === 'L').length, 7)
  assert.equal(contract.doors.filter((row) => row.side === 'R').length, 7)
  for (const side of ['L', 'R']) {
    const rows = contract.doors.filter((row) => row.side === side)
    assert.deepEqual(rows.map((row) => row.row), [1, 2, 3, 4, 5, 6, 7])
    assert.deepEqual(rows.map((row) => row.yMm), recipe.calculations.doorCenterYByColumnMm[side])
    assert.equal(rows.every((row) => near(row.xMm, side === 'L' ? -230.5 : 230.5)), true)
    assert.equal(rows.every((row) => row.zMm === 0 && row.transform.slice(0, 9).join(',') === '1,0,0,0,1,0,0,0,1'), true)
    assert.equal(rows.every((row) => near(row.tongueGlobalXmm, side === 'L' ? -55 : 55)), true)
    assert.equal(rows.every((row) => near(row.tongueGlobalZmm, -11.3)), true)
    assert.equal(rows.every((row) => near(row.panelXMinMm, side === 'L' ? -421 : 40)), true)
    assert.equal(rows.every((row) => near(row.panelXMaxMm, side === 'L' ? -40 : 421)), true)
  }
})

check('shelves_and_frame_crossbars_follow_six_boundaries_per_side', () => {
  assert.equal(contract.shelves.length, 12)
  assert.equal(contract.frameCrossbars.length, 12)
  for (const side of ['L', 'R']) {
    const shelves = contract.shelves.filter((row) => row.side === side)
    const crossbars = contract.frameCrossbars.filter((row) => row.side === side)
    assert.deepEqual(shelves.map((row) => row.centerYmm), recipe.calculations.internalBoundaryYmm)
    assert.deepEqual(crossbars.map((row) => row.centerYmm), recipe.calculations.internalBoundaryYmm)
    assert.equal(shelves.every((row) => near(row.transformYmm, row.centerYmm - 1705)), true)
    assert.equal(crossbars.every((row) => near(row.transformYmm, row.centerYmm - 1700)), true)
  }
})

check('root_mutation_is_exact_and_does_not_use_patterns', () => {
  assert.equal(contract.remove.rootDoors.length, 6)
  assert.equal(contract.remove.rootShelfInstances.length, 4)
  assert.equal(contract.remove.frameCrossbarInstances.length, 12)
  assert.equal(new Set(contract.remove.rootDoors).size, 6)
  assert.equal(new Set(contract.remove.rootShelfInstances).size, 4)
  assert.equal(new Set(contract.remove.frameCrossbarInstances).size, 12)
  assert.equal(contract.mutationPolicy.directChildrenOnly, true)
  assert.equal(contract.mutationPolicy.exactInstanceNamesOnly, true)
  assert.equal(contract.mutationPolicy.recursivePatternDeleteForbidden, true)
})

check('expected_component_and_final_closure_counts_are_exact', () => {
  assert.deepEqual(contract.expectedWorkingAssembly, {
    topLevelComponentCount: 41,
    recursiveComponentCount: 294,
    activeRecursiveComponentCount: 294,
    suppressedComponentCount: 0,
    doorModuleCount: 14,
    shelfModuleCount: 12,
    activeFrameCrossbarCount: 12,
    mechanicalTongueCount: 14,
  })
  assert.deepEqual(contract.expectedFinalClosure, {
    cadFileCount: 55,
    assemblyFileCount: 14,
    partFileCount: 41,
    missingFileCount: 0,
    externalFileCount: 0,
  })
  assert.equal(contract.workingPackInventory.beforeDoorModuleImport, 75)
  assert.equal(contract.workingPackInventory.afterDoorModuleImport, 88)
  assert.equal(contract.doorModuleInventory.cadFileCount, 13)
  assert.deepEqual(contract.doorModuleInventory.importManifest, {
    schema: 'winnsen.16029.native_door_module_flat_import_manifest.v1',
    fileName: 'door_module_flat_import_manifest.json',
    workingPackImportStateBeforeRootAssembly: 'NOT_IMPORTED',
    rootAssemblerBoundary: 'ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK',
  })
})

check('feature_health_policy_allows_only_one_exact_known_issue_tuple', () => {
  assert.deepEqual(contract.featureHealthPolicy, {
    mode: 'exact_unique_known_issue_only',
    maximumIssueCount: 1,
    allowedIssue: {
      name: '箱体右侧板焊接-1',
      type: 'Reference',
      errorCode: 51,
      errorCode2: 51,
      warning: true,
    },
  })
  assert.equal(Object.keys(contract.featureHealthPolicy).length, 3)
  assert.equal(Object.keys(contract.featureHealthPolicy.allowedIssue).length, 5)
})

check('component_counts_are_derived_from_the_verified_v37_hierarchy', () => {
  const hierarchy = JSON.parse(readFileSync(V37_HIERARCHY_PATH, 'utf8'))
  assert.equal(hierarchy.open_errors, 0)
  assert.equal(hierarchy.rebuilt, true)
  assert.equal(hierarchy.component_count, 158)
  const components = hierarchy.components
  const topLevel = components.filter((row) => row.depth === 0)
  const suppressed = components.filter((row) => row.is_suppressed === true)
  const doorComponents = components.filter((row) =>
    row.name.split('/')[0].startsWith('储物柜门装配_'))
  const shelfComponents = components.filter((row) =>
    row.name.split('/')[0].startsWith('箱体横层板'))
  const frameCrossbars = components.filter((row) =>
    row.depth === 1 && row.name.startsWith('门框焊接-1/') &&
    ['门框 横隔板.sldprt', '门框 横隔板R.SLDPRT'].includes(row.path.split('\\').at(-1)))
  const referencedPaths = new Set(components.map((row) => row.path.toLowerCase()))
  referencedPaths.add(hierarchy.assembly_path.toLowerCase())
  const oldDoorPaths = new Set(doorComponents.map((row) => row.path.toLowerCase()))
  assert.equal(topLevel.length, 25)
  assert.equal(suppressed.length, 8)
  assert.equal(doorComponents.length, 78)
  assert.equal(shelfComponents.length, 16)
  assert.equal(frameCrossbars.length, 12)
  assert.equal(frameCrossbars.filter((row) => row.is_suppressed).length, 8)
  assert.equal(referencedPaths.size, 75)
  assert.equal(oldDoorPaths.size, 33)
  assert.equal(158 - 78 - 16 + (14 * 13) + (12 * 4), 294)
  assert.equal(25 - 6 - 4 + 14 + 12, 41)
  assert.equal(75 - 33 + contract.doorModuleInventory.cadFileCount, 55)
})

check('final_name_and_forbidden_old_door_references_are_explicit', () => {
  assert.equal(contract.finalRootFileName, '标准寄存柜1917×888×550(总装配).SLDASM')
  assert.equal(contract.workingRootFileName, '标准寄存柜1917×760×550(总装配).SLDASM')
  assert.equal(contract.forbiddenFinalReferenceLeafNames.length, 33)
  assert.equal(new Set(contract.forbiddenFinalReferenceLeafNames.map((value) => value.toLowerCase())).size, 33)
  assert.equal(contract.requiredChecks.includes('one_door_one_lock'), true)
  assert.equal(contract.requiredChecks.includes('relocated_reopen'), true)
})

check('untrusted_or_mutated_recipe_is_rejected', () => {
  assert.throws(() => buildNativeAssemblyContract(null), /trusted 888x14 recipe/i)
  const changed = structuredClone(recipe)
  changed.geometry.doorCount = 12
  assert.throws(() => buildNativeAssemblyContract(changed), /trusted 888x14 recipe/i)
  const electrical = structuredClone(recipe)
  electrical.hardwarePolicy.electricalComponentsAllowed = true
  assert.throws(() => buildNativeAssemblyContract(electrical), /electrical/i)
})

const failed = checks.filter((item) => !item.ok && !item.skipped)
process.stdout.write(`${JSON.stringify({
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  mode: fixtureOnly ? 'fixture-only' : 'full',
  checksSkipped: checks.filter(item => item.skipped).length,
  checks,
}, null, 2)}\n`)
if (failed.length) process.exit(1)
