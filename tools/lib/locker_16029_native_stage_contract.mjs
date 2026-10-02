import { createHash } from 'node:crypto'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../locker_16029_native_recipe_registry.mjs'

const RECIPE_ID = 'winnsen-16029-888w-14door-native-v1'

export const NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS = Object.freeze([
  'evidence_write_attempted',
  'evidence_write_succeeded',
  'evidence_finalize_write_succeeded',
  'evidence_finalize_error',
  'failure_evidence_write_succeeded',
  'phase_receipt_path',
  'phase_receipt_sha256',
  'phase_receipt_committed',
  'authorization_checkpoints',
  'completed_at_utc',
  'commit_completed_at_utc',
  'evidence_commitment_sha256',
  'backup_deleted',
  'backup_delete_error',
])

function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value
  for (const child of Object.values(value)) deepFreeze(child)
  return Object.freeze(value)
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

export function nativeEvidenceCommitmentSha256(evidence) {
  if (!evidence || typeof evidence !== 'object' || Array.isArray(evidence)) {
    throw new TypeError('evidence must be an object')
  }
  const projection = { ...evidence }
  for (const key of NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS) delete projection[key]
  return createHash('sha256').update(JSON.stringify(stableValue(projection)), 'utf8')
    .digest('hex').toUpperCase()
}

export const NATIVE_STAGE_RECEIPT_ORDER = Object.freeze([
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

export const NATIVE_SEED_RECEIPT_KEYS = Object.freeze([
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

export const NATIVE_RUNTIME_RECEIPT_KEYS = Object.freeze([
  'schema', 'phase', 'success', 'completedAt', 'taskId', 'taskRevision',
  'taskDigest', 'requestDigest', 'leaseId', 'leaseExpiresAt', 'attempt',
  'planSha256', 'authorizationId', 'authorizationSha256',
  'authorizationJsonBase64', 'authorizationIssuedAt', 'authorizationExpiresAt',
  'recipeId', 'recipeDigest', 'toolId', 'toolSourceNormalizedSha256',
  'toolExecutableSha256', 'evidencePath', 'evidenceSha256',
  'evidenceCommitmentSha256', 'preInventoryDigest', 'postInventoryDigest',
  'predecessorReceiptSha256',
])

export const NATIVE_TOOLCHAIN_MANIFEST_PATHS = deepFreeze({
  native_seed_pack_888x14_v1:
    'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/toolchain_manifest.json',
  native_width_888_v1:
    'workers/native_model_requests/development/v1/tools/width_888_native_v1/toolchain_manifest.json',
  native_door_module_888x14_v1:
    'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/toolchain_manifest.json',
  native_lock_topology_888x14_v1:
    'workers/native_model_requests/development/v1/tools/lock_toolchain_manifest.json',
  native_root_assembly_888x14_v1:
    'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/toolchain_manifest.json',
  native_final_pack_888x14_v1:
    'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/toolchain_manifest.json',
})

const CONTRACTS = deepFreeze({
  clone_native_seed: {
    schema: 'winnsen.16029.native_seed_pack_receipt.v1',
    path: 'receipts/clone_native_seed.json',
    producerToolId: 'native_seed_pack_888x14_v1',
    predecessor: '',
  },
  dimensions: {
    schema: 'winnsen.native_width_888.phase_receipt.v1',
    path: 'evidence/width-dimensions.receipt.json',
    producerToolId: 'native_width_888_v1',
    predecessor: 'clone_native_seed',
  },
  derived: {
    schema: 'winnsen.native_width_888.phase_receipt.v1',
    path: 'evidence/width-derived.receipt.json',
    producerToolId: 'native_width_888_v1',
    predecessor: 'dimensions',
  },
  'base-hole': {
    schema: 'winnsen.native_width_888.phase_receipt.v1',
    path: 'evidence/width-base-hole.receipt.json',
    producerToolId: 'native_width_888_v1',
    predecessor: 'derived',
  },
  assemblies: {
    schema: 'winnsen.native_width_888.phase_receipt.v1',
    path: 'evidence/width-assemblies.receipt.json',
    producerToolId: 'native_width_888_v1',
    predecessor: 'base-hole',
  },
  door_module_888x14: {
    schema: 'winnsen.16029.native_door_module_receipt.v1',
    path: 'receipts/door_module_888x14.json',
    producerToolId: 'native_door_module_888x14_v1',
    predecessor: 'assemblies',
  },
  lock_topology_888x14: {
    schema: 'winnsen.16029.native_lock_topology_receipt.v1',
    path: 'receipts/lock_topology_888x14.json',
    producerToolId: 'native_lock_topology_888x14_v1',
    predecessor: 'door_module_888x14',
  },
  root_assembly_888x14: {
    schema: 'winnsen.16029.native_root_assembly_receipt.v1',
    path: 'receipts/root_assembly_888x14.json',
    producerToolId: 'native_root_assembly_888x14_v1',
    predecessor: 'lock_topology_888x14',
  },
  final_pack_and_relocated_reopen: {
    schema: 'winnsen.16029.native_final_pack_receipt.v1',
    path: 'receipts/final_pack_and_relocated_reopen.json',
    producerToolId: 'native_final_pack_888x14_v1',
    predecessor: 'root_assembly_888x14',
  },
})

export function buildNativeStageReceiptContracts(recipe) {
  const trusted = TRUSTED_NATIVE_RECIPE_REGISTRY[RECIPE_ID]
  if (!recipe || recipe.id !== RECIPE_ID || recipe.version !== 1 ||
      nativeRecipeDigest(recipe) !== nativeRecipeDigest(trusted)) {
    throw new Error('trusted 888x14 recipe is required')
  }
  return CONTRACTS
}
