import assert from 'node:assert/strict'
import {
  NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS,
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_SEED_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  NATIVE_TOOLCHAIN_MANIFEST_PATHS,
  buildNativeStageReceiptContracts,
  nativeEvidenceCommitmentSha256,
} from './lib/locker_16029_native_stage_contract.mjs'
import { TRUSTED_NATIVE_RECIPE_REGISTRY } from './locker_16029_native_recipe_registry.mjs'

const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
const contracts = buildNativeStageReceiptContracts(recipe)

assert.deepEqual(NATIVE_STAGE_RECEIPT_ORDER, [
  'clone_native_seed',
  'dimensions',
  'derived',
  'base-hole',
  'assemblies',
  'door_module_888x14',
  'lock_topology_888x14',
  'root_assembly_888x14',
  'final_pack_and_relocated_reopen',
])
assert.deepEqual(Object.keys(contracts), NATIVE_STAGE_RECEIPT_ORDER)

let predecessor = ''
for (const stageId of NATIVE_STAGE_RECEIPT_ORDER) {
  const row = contracts[stageId]
  assert.deepEqual(Object.keys(row), ['schema', 'path', 'producerToolId', 'predecessor'])
  assert.equal(row.predecessor, predecessor)
  assert.match(row.schema, /^winnsen\./)
  assert.match(row.path, /^(?:receipts|evidence)\/[A-Za-z0-9_.-]+\.json$/)
  assert.match(row.producerToolId, /^native_[a-z0-9_]+_v1$/)
  predecessor = stageId
}

assert.equal(contracts.clone_native_seed.path, 'receipts/clone_native_seed.json')
assert.equal(contracts.dimensions.path, 'evidence/width-dimensions.receipt.json')
assert.equal(contracts.assemblies.path, 'evidence/width-assemblies.receipt.json')
assert.equal(contracts.door_module_888x14.path, 'receipts/door_module_888x14.json')
assert.equal(contracts.lock_topology_888x14.path, 'receipts/lock_topology_888x14.json')
assert.equal(contracts.root_assembly_888x14.path, 'receipts/root_assembly_888x14.json')
assert.equal(contracts.final_pack_and_relocated_reopen.path,
  'receipts/final_pack_and_relocated_reopen.json')
assert.equal(Object.isFrozen(contracts), true)
assert.equal(Object.values(contracts).every(Object.isFrozen), true)
assert.deepEqual(NATIVE_SEED_RECEIPT_KEYS, [
  'schema', 'phase', 'success', 'completedAt', 'taskId', 'taskRevision',
  'taskDigest', 'requestDigest', 'leaseId', 'leaseExpiresAt', 'attempt',
  'planSha256', 'authorizationId', 'authorizationSha256',
  'authorizationJsonBase64', 'authorizationIssuedAt', 'authorizationExpiresAt',
  'recipeId', 'recipeDigest', 'toolId', 'toolSourceNormalizedSha256',
  'toolExecutableSha256', 'evidencePath', 'evidenceSha256',
  'evidenceCommitmentSha256', 'sourceInventoryDigest', 'targetInventoryDigest',
  'nonRootFileCount', 'nonRootExactSource', 'rootFileName', 'rootShaBefore',
  'rootShaAfterStable', 'initialOpenErrors', 'initialOpenWarnings',
  'stabilizeSaveErrors', 'stabilizeSaveWarnings', 'reopenErrors',
  'reopenWarnings', 'dependencyClosureCount', 'dependenciesAllTargetLocal',
  'knownRootIssueGate', 'predecessorReceiptSha256',
])
assert.deepEqual(NATIVE_RUNTIME_RECEIPT_KEYS, [
  'schema', 'phase', 'success', 'completedAt', 'taskId', 'taskRevision',
  'taskDigest', 'requestDigest', 'leaseId', 'leaseExpiresAt', 'attempt',
  'planSha256', 'authorizationId', 'authorizationSha256',
  'authorizationJsonBase64', 'authorizationIssuedAt', 'authorizationExpiresAt',
  'recipeId', 'recipeDigest', 'toolId', 'toolSourceNormalizedSha256',
  'toolExecutableSha256', 'evidencePath', 'evidenceSha256',
  'evidenceCommitmentSha256', 'preInventoryDigest', 'postInventoryDigest',
  'predecessorReceiptSha256',
])
assert.deepEqual(Object.keys(NATIVE_TOOLCHAIN_MANIFEST_PATHS), [
  'native_seed_pack_888x14_v1',
  'native_width_888_v1',
  'native_door_module_888x14_v1',
  'native_lock_topology_888x14_v1',
  'native_root_assembly_888x14_v1',
  'native_final_pack_888x14_v1',
])
assert.equal(Object.isFrozen(NATIVE_TOOLCHAIN_MANIFEST_PATHS), true)
assert.deepEqual(NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS, [
  'evidence_write_attempted', 'evidence_write_succeeded',
  'evidence_finalize_write_succeeded', 'evidence_finalize_error',
  'failure_evidence_write_succeeded', 'phase_receipt_path',
  'phase_receipt_sha256', 'phase_receipt_committed', 'authorization_checkpoints',
  'completed_at_utc', 'commit_completed_at_utc', 'evidence_commitment_sha256',
  'backup_deleted', 'backup_delete_error',
])
const commitmentFixture = {
  z: 7,
  nested: { beta: false, alpha: ['L', 381, { y: 2, x: 1 }] },
  phase: 'clone_native_seed',
  completed_at_utc: 'ignored',
  phase_receipt_sha256: 'ignored',
}
const commitment = nativeEvidenceCommitmentSha256(commitmentFixture)
assert.equal(commitment, '684D7B713A5BF06A65B65A82BDA13B0B9566C139B7B088D5EC0A74EDFC934DE1')
assert.equal(nativeEvidenceCommitmentSha256({
  ...commitmentFixture,
  completed_at_utc: 'changed but excluded',
  phase_receipt_sha256: 'changed but excluded',
}), commitment)
assert.notEqual(nativeEvidenceCommitmentSha256({ ...commitmentFixture, z: 8 }), commitment)

assert.throws(() => buildNativeStageReceiptContracts({ ...recipe, id: 'untrusted' }),
  /trusted 888x14 recipe is required/)

console.log(JSON.stringify({ status: 'PASS', checksTotal: 17, checksFailed: 0 }, null, 2))
