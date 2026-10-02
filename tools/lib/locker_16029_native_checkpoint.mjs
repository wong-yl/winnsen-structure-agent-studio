import { createHash } from 'node:crypto'
import {
  existsSync, lstatSync, readFileSync, readdirSync, realpathSync, statSync,
} from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
  nativeCheckpointDescriptorDigest,
} from './locker_16029_native_task_store.mjs'
import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_SEED_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  nativeEvidenceCommitmentSha256,
} from './locker_16029_native_stage_contract.mjs'
import { NATIVE_EXECUTION_TOOL_CONTRACTS } from './locker_16029_native_execution_authorization.mjs'
import { canonicalFlatCadInventoryDigest } from './locker_16029_native_stage_executor.mjs'
import { resolveTrustedNativeToolchainManifests } from './locker_16029_native_toolchain_manifests.mjs'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../locker_16029_native_recipe_registry.mjs'

const DEFAULT_REPOSITORY_ROOT = resolve(fileURLToPath(new URL('../..', import.meta.url)))
const PURPOSE = 'structure_engineering_assistance'
const PLAN_SCHEMA = 'winnsen.native_build_plan.v1'
const AUTHORIZATION_SCHEMA = 'winnsen.native_execution_authorization.v1'
const TASK_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/
const CHECKPOINT_PHASES = new Set(['assemblies', 'door_module_888x14'])
const TRUSTED_BOOTSTRAP_ANCHORS = Object.freeze({
  'NATIVE-20260831T170546-C97F93': Object.freeze({
    attempt: 1,
    completedPhase: 'assemblies',
    planSha256: '037ED09F412D8B3797F0A21320EA9753E9AF4CA83C062FCD133B5F92F545A423',
    completedReceiptSha256: '4A6E53685B720AFFA9C24EFA51945D7B6346B85818A6DBD0F88972F8A45EF582',
    completedEvidenceSha256: '65F8A7CC39C7CAC60D9D25FDE72A9FD7292BE6B4E82C145A92902DD2D62B1CF8',
    currentInventoryDigest: '0A023426F7F274048B9A6771B341D274E6B9C6E2E7DB4305CF59556DF2CB514C',
    checkpointAuditSha256: 'E375E093E7BCD24882B4AA12BED45BD7CBC139EF3778D23468259E0A73A85B32',
  }),
})
const TOOL_IDS = Object.freeze([
  'native_seed_pack_888x14_v1',
  'native_width_888_v1',
  'native_door_module_888x14_v1',
  'native_lock_topology_888x14_v1',
  'native_root_assembly_888x14_v1',
  'native_final_pack_888x14_v1',
])
const LATER_ARTIFACTS = Object.freeze({
  door_module_888x14: Object.freeze([
    'inputs/door_module_888x14.request.json',
    'inputs/door_sources',
    'native_cad/door_module_888x14',
    'evidence/left-panel-proof.v1.json',
    'evidence/private/left-panel-proof.audit.v1.json',
    'evidence/door_module_888x14.result.v1.json',
  ]),
  lock_topology_888x14: Object.freeze([
    'native_cad/lock_topology_888x14',
    'evidence/lock_topology_888x14.v2.json',
    'evidence/lock_topology_888x14.inspector.v1.json',
    'evidence/assembly_lock_tongues_888x14.json',
    'evidence/lock_topology_888x14.validation.v2.json',
  ]),
  root_assembly_888x14: Object.freeze([
    'evidence/root_assembly_888x14.result.v1.json',
  ]),
  final_pack_and_relocated_reopen: Object.freeze([
    'evidence/final_pack_and_relocated_reopen.result.v1.json',
  ]),
})

export class NativeCheckpointError extends Error {
  constructor(code, message) {
    super(message)
    this.name = 'NativeCheckpointError'
    this.code = code
  }
}

function fail(code, message) {
  throw new NativeCheckpointError(code, message)
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex').toUpperCase()
}

function isDigest(value) {
  return /^[A-F0-9]{64}$/.test(String(value || '').toUpperCase())
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function stableJson(value) {
  return JSON.stringify(stableValue(value))
}

function exactKeys(value, keys) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false
  return JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...keys].sort())
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel !== '' && !rel.startsWith('..') && !isAbsolute(rel)
}

function samePath(left, right) {
  const a = resolve(left)
  const b = resolve(right)
  return process.platform === 'win32' ? a.toUpperCase() === b.toUpperCase() : a === b
}

function assertSafeDirectory(path, root, label) {
  const absolute = resolve(path)
  const safeRoot = resolve(root)
  if ((!samePath(absolute, safeRoot) && !underRoot(absolute, safeRoot)) || !existsSync(absolute)) {
    fail('NATIVE_CHECKPOINT_PATH_INVALID', `${label} is missing or outside its trusted root`)
  }
  const rel = relative(safeRoot, absolute)
  let current = safeRoot
  if (lstatSync(current).isSymbolicLink()) fail('NATIVE_CHECKPOINT_PATH_UNSAFE', `${label} root is a link`)
  for (const segment of rel ? rel.split(sep) : []) {
    current = resolve(current, segment)
    if (lstatSync(current).isSymbolicLink()) fail('NATIVE_CHECKPOINT_PATH_UNSAFE', `${label} contains a link`)
  }
  const info = statSync(absolute)
  if (!info.isDirectory() || !samePath(realpathSync.native(absolute), absolute)) {
    fail('NATIVE_CHECKPOINT_PATH_UNSAFE', `${label} must be a real directory`)
  }
  return absolute
}

function assertSafeFile(path, root, label) {
  const absolute = resolve(path)
  const safeRoot = resolve(root)
  if (!underRoot(absolute, safeRoot) || !existsSync(absolute)) {
    fail('NATIVE_CHECKPOINT_PATH_INVALID', `${label} is missing or outside its trusted root`)
  }
  const rel = relative(safeRoot, absolute)
  let current = safeRoot
  for (const segment of rel.split(sep)) {
    current = resolve(current, segment)
    if (lstatSync(current).isSymbolicLink()) fail('NATIVE_CHECKPOINT_PATH_UNSAFE', `${label} contains a link`)
  }
  const info = statSync(absolute)
  if (!info.isFile() || Number(info.nlink) !== 1 ||
      !underRoot(realpathSync.native(absolute), realpathSync.native(safeRoot))) {
    fail('NATIVE_CHECKPOINT_PATH_UNSAFE', `${label} must be a single-link regular file`)
  }
  return absolute
}

function normalizedAttemptPath(attemptDir, value, label) {
  const text = String(value || '').replaceAll('\\', '/')
  if (!text || text.startsWith('/') || text.split('/').some((segment) => !segment || segment === '.' || segment === '..')) {
    fail('NATIVE_CHECKPOINT_PATH_INVALID', `${label} is not a normalized attempt-relative path`)
  }
  const absolute = resolve(attemptDir, ...text.split('/'))
  if (!underRoot(absolute, attemptDir)) fail('NATIVE_CHECKPOINT_PATH_INVALID', `${label} escaped the attempt`)
  return { relative: text, absolute }
}

function readJson(path, root, label) {
  const safe = assertSafeFile(path, root, label)
  const bytes = readFileSync(safe)
  try { return { value: JSON.parse(bytes.toString('utf8')), bytes, path: safe } }
  catch { fail('NATIVE_CHECKPOINT_JSON_INVALID', `${label} is not valid JSON`) }
}

function taskRoot(dataDir) {
  const root = resolve(dataDir)
  return basename(root).toLowerCase() === 'native_model_requests'
    ? root
    : resolve(root, 'native_model_requests')
}

function requireDigest(value, label) {
  const digest = String(value || '').toUpperCase()
  if (!isDigest(digest)) fail('NATIVE_CHECKPOINT_BINDING_INVALID', `${label} is not a SHA-256 digest`)
  return digest
}

function canonicalBase64(value) {
  const text = String(value || '')
  if (!text || !/^[A-Za-z0-9+/]+={0,2}$/.test(text) || text.length % 4 !== 0) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', 'authorization snapshot is not canonical base64')
  }
  const bytes = Buffer.from(text, 'base64')
  if (bytes.toString('base64') !== text) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', 'authorization snapshot base64 is not canonical')
  }
  return bytes
}

function receiptEvidenceValue(evidence, keys) {
  const values = keys.filter((key) => Object.hasOwn(evidence, key)).map((key) => String(evidence[key] || ''))
  if (values.length !== 1 || !values[0]) return ''
  return values[0]
}

function validateAuthorization({ authorization, authorizationBytes, receipt, plan, planSha256,
  phase, preInventoryDigest, attempt, consumedPath }) {
  const expectedPhases = NATIVE_EXECUTION_TOOL_CONTRACTS[receipt.toolId]
  const quality = authorization?.qualityBoundary
  const valid = authorization?.schema === AUTHORIZATION_SCHEMA &&
    authorization.authorizationId === receipt.authorizationId &&
    authorization.workerId === plan.workerId &&
    authorization.task?.id === plan.task.id &&
    Number(authorization.task?.revision) === Number(plan.task.revisionAtPlanning) &&
    String(authorization.task?.digest || '').toUpperCase() === String(plan.task.digest || '').toUpperCase() &&
    authorization.task?.leaseId === receipt.leaseId &&
    authorization.task?.leaseExpiresAt === receipt.leaseExpiresAt &&
    authorization.request?.fingerprint === plan.request.fingerprint &&
    String(authorization.request?.digest || '').toUpperCase() === String(plan.request.digest || '').toUpperCase() &&
    String(authorization.plan?.sha256 || '').toUpperCase() === planSha256 &&
    authorization.recipe?.id === plan.recipe.id &&
    Number(authorization.recipe?.version) === Number(plan.recipe.version) &&
    String(authorization.recipe?.digest || '').toUpperCase() === String(plan.recipe.digest || '').toUpperCase() &&
    authorization.tool?.id === receipt.toolId &&
    String(authorization.tool?.sourceNormalizedSha256 || '').toUpperCase() === String(receipt.toolSourceNormalizedSha256 || '').toUpperCase() &&
    String(authorization.tool?.executableSha256 || '').toUpperCase() === String(receipt.toolExecutableSha256 || '').toUpperCase() &&
    String(authorization.seed?.inventoryDigest || '').toUpperCase() === preInventoryDigest &&
    authorization.execution?.authorized === true &&
    Number(authorization.execution?.attempt) === attempt &&
    stableJson(authorization.execution?.phases) === stableJson(expectedPhases) &&
    authorization.purpose === PURPOSE && quality?.engineeringAssistanceReady === false &&
    quality?.readyOnlyAfterEveryRequiredCheckPasses === true
  if (!valid || !Array.isArray(expectedPhases) || !expectedPhases.includes(phase)) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', `consumed authorization binding is invalid for ${phase}`)
  }
  if (authorization.issuedAt !== receipt.authorizationIssuedAt ||
      authorization.expiresAt !== receipt.authorizationExpiresAt ||
      sha256(authorizationBytes) !== String(receipt.authorizationSha256 || '').toUpperCase() ||
      !readFileSync(consumedPath).equals(authorizationBytes)) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', `authorization bytes are not immutable for ${phase}`)
  }
  const issued = Date.parse(authorization.issuedAt)
  const expires = Date.parse(authorization.expiresAt)
  const leaseExpires = Date.parse(receipt.leaseExpiresAt)
  const completed = Date.parse(receipt.completedAt)
  if (![issued, expires, leaseExpires, completed].every(Number.isFinite) ||
      completed < issued || completed > expires || expires > leaseExpires) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', `authorization time window is invalid for ${phase}`)
  }
}

function validateReceipt({ phase, index, contract, receiptPath, receiptData, attemptDir,
  task, plan, planSha256, attempt, predecessorReceiptSha256, predecessorPostDigest }) {
  const receipt = receiptData.value
  const keys = index === 0 ? NATIVE_SEED_RECEIPT_KEYS : NATIVE_RUNTIME_RECEIPT_KEYS
  if (!exactKeys(receipt, keys) || receipt.schema !== contract.schema || receipt.phase !== phase ||
      receipt.success !== true || receipt.taskId !== task.id ||
      Number(receipt.taskRevision) !== Number(plan.task.revisionAtPlanning) ||
      String(receipt.taskDigest || '').toUpperCase() !== String(plan.task.digest || '').toUpperCase() ||
      String(receipt.requestDigest || '').toUpperCase() !== String(plan.request.digest || '').toUpperCase() ||
      Number(receipt.attempt) !== attempt || String(receipt.planSha256 || '').toUpperCase() !== planSha256 ||
      receipt.recipeId !== plan.recipe.id ||
      String(receipt.recipeDigest || '').toUpperCase() !== String(plan.recipe.digest || '').toUpperCase() ||
      receipt.toolId !== contract.producerToolId ||
      String(receipt.predecessorReceiptSha256 || '').toUpperCase() !== predecessorReceiptSha256) {
    fail('NATIVE_CHECKPOINT_RECEIPT_INVALID', `receipt envelope or predecessor is invalid for ${phase}`)
  }
  const preInventoryDigest = requireDigest(index === 0 ? receipt.sourceInventoryDigest : receipt.preInventoryDigest,
    `${phase} pre-inventory`)
  const postInventoryDigest = requireDigest(index === 0 ? receipt.targetInventoryDigest : receipt.postInventoryDigest,
    `${phase} post-inventory`)
  if (index > 0 && preInventoryDigest !== predecessorPostDigest) {
    fail('NATIVE_CHECKPOINT_INVENTORY_CHAIN_INVALID', `inventory chain is broken before ${phase}`)
  }
  const evidenceLocation = normalizedAttemptPath(attemptDir, receipt.evidencePath, `${phase} evidence`)
  if (!evidenceLocation.relative.startsWith('evidence/') || samePath(evidenceLocation.absolute, receiptPath)) {
    fail('NATIVE_CHECKPOINT_EVIDENCE_INVALID', `evidence path is invalid for ${phase}`)
  }
  const evidenceData = readJson(evidenceLocation.absolute, attemptDir, `${phase} evidence`)
  const evidence = evidenceData.value
  const commitment = nativeEvidenceCommitmentSha256(evidence)
  if (sha256(evidenceData.bytes) !== String(receipt.evidenceSha256 || '').toUpperCase() ||
      commitment !== String(receipt.evidenceCommitmentSha256 || '').toUpperCase() ||
      evidence.success !== true || evidence.phase !== phase ||
      receiptEvidenceValue(evidence, ['task_id', 'taskId']) !== task.id ||
      String(receiptEvidenceValue(evidence, ['plan_sha256', 'planSha256'])).toUpperCase() !== planSha256 ||
      receiptEvidenceValue(evidence, ['authorization_id', 'authorizationId']) !== receipt.authorizationId ||
      String(receiptEvidenceValue(evidence, ['source_inventory_digest', 'initial_inventory_digest',
        'pre_inventory_digest', 'preInventoryDigest'])).toUpperCase() !== preInventoryDigest ||
      String(receiptEvidenceValue(evidence, ['post_inventory_digest', 'target_inventory_digest',
        'postInventoryDigest', 'targetInventoryDigest'])).toUpperCase() !== postInventoryDigest ||
      receiptEvidenceValue(evidence, ['completed_at_utc', 'completedAtUtc', 'completedAt',
        'evidence_commit_completed_at_utc']) !== receipt.completedAt ||
      String(receiptEvidenceValue(evidence, ['evidence_commitment_sha256', 'evidenceCommitmentSha256'])).toUpperCase() !== commitment) {
    fail('NATIVE_CHECKPOINT_EVIDENCE_INVALID', `evidence commitment or binding is invalid for ${phase}`)
  }
  const authorizationBytes = canonicalBase64(receipt.authorizationJsonBase64)
  const authorization = (() => {
    try { return JSON.parse(authorizationBytes.toString('utf8')) }
    catch { fail('NATIVE_CHECKPOINT_AUTHORIZATION_INVALID', `authorization snapshot is not JSON for ${phase}`) }
  })()
  const consumedPath = resolve(attemptDir, 'execution_authorizations', 'consumed',
    `${phase}-${receipt.authorizationId}.json`)
  assertSafeFile(consumedPath, attemptDir, `${phase} consumed authorization`)
  validateAuthorization({ authorization, authorizationBytes, receipt, plan, planSha256,
    phase, preInventoryDigest, attempt, consumedPath })
  return {
    receipt,
    receiptSha256: sha256(receiptData.bytes),
    evidence,
    evidenceSha256: sha256(evidenceData.bytes),
    evidencePath: evidenceLocation.relative,
    postInventoryDigest,
    consumedFileName: basename(consumedPath),
  }
}

function validatePlanContracts(plan, trustedRecipe) {
  const contracts = plan?.stageReceiptContracts
  if (!exactKeys(contracts, NATIVE_STAGE_RECEIPT_ORDER)) {
    fail('NATIVE_CHECKPOINT_PLAN_INVALID', 'plan does not contain the exact stage receipt contracts')
  }
  const expectedSchemaByPhase = {
    clone_native_seed: 'winnsen.16029.native_seed_pack_receipt.v1',
    dimensions: 'winnsen.native_width_888.phase_receipt.v1',
    derived: 'winnsen.native_width_888.phase_receipt.v1',
    'base-hole': 'winnsen.native_width_888.phase_receipt.v1',
    assemblies: 'winnsen.native_width_888.phase_receipt.v1',
    door_module_888x14: 'winnsen.16029.native_door_module_receipt.v1',
    lock_topology_888x14: 'winnsen.16029.native_lock_topology_receipt.v1',
    root_assembly_888x14: 'winnsen.16029.native_root_assembly_receipt.v1',
    final_pack_and_relocated_reopen: 'winnsen.16029.native_final_pack_receipt.v1',
  }
  const expectedToolByPhase = {
    clone_native_seed: 'native_seed_pack_888x14_v1',
    dimensions: 'native_width_888_v1', derived: 'native_width_888_v1',
    'base-hole': 'native_width_888_v1', assemblies: 'native_width_888_v1',
    door_module_888x14: 'native_door_module_888x14_v1',
    lock_topology_888x14: 'native_lock_topology_888x14_v1',
    root_assembly_888x14: 'native_root_assembly_888x14_v1',
    final_pack_and_relocated_reopen: 'native_final_pack_888x14_v1',
  }
  for (let index = 0; index < NATIVE_STAGE_RECEIPT_ORDER.length; index += 1) {
    const phase = NATIVE_STAGE_RECEIPT_ORDER[index]
    const contract = contracts[phase]
    const predecessor = index ? NATIVE_STAGE_RECEIPT_ORDER[index - 1] : ''
    if (!exactKeys(contract, ['schema', 'path', 'producerToolId', 'predecessor']) ||
        contract.schema !== expectedSchemaByPhase[phase] || contract.producerToolId !== expectedToolByPhase[phase] ||
        contract.predecessor !== predecessor) {
      fail('NATIVE_CHECKPOINT_PLAN_INVALID', `plan stage receipt contract is invalid for ${phase}`)
    }
    normalizedAttemptPath('C:\checkpoint-attempt-root', contract.path, `${phase} receipt contract`)
  }
  if (!trustedRecipe || plan.recipe?.id !== trustedRecipe.id || Number(plan.recipe?.version) !== trustedRecipe.version ||
      String(plan.recipe?.digest || '').toUpperCase() !== nativeRecipeDigest(trustedRecipe).toUpperCase()) {
    fail('NATIVE_CHECKPOINT_PLAN_INVALID', 'plan recipe binding is not trusted')
  }
}

function validateNoLaterArtifacts(attemptDir, plan, completedIndex, expectedConsumedNames) {
  for (const phase of NATIVE_STAGE_RECEIPT_ORDER.slice(completedIndex + 1)) {
    const receiptPath = normalizedAttemptPath(attemptDir, plan.stageReceiptContracts[phase].path,
      `${phase} future receipt`).absolute
    if (existsSync(receiptPath)) fail('NATIVE_CHECKPOINT_LATER_ARTIFACT_PRESENT', `future receipt already exists: ${phase}`)
    for (const relativePath of LATER_ARTIFACTS[phase] || []) {
      if (existsSync(resolve(attemptDir, ...relativePath.split('/')))) {
        fail('NATIVE_CHECKPOINT_LATER_ARTIFACT_PRESENT', `future artifact already exists: ${relativePath}`)
      }
    }
  }
  const authorizationDir = assertSafeDirectory(resolve(attemptDir, 'execution_authorizations'), attemptDir,
    'execution authorization directory')
  const directAuthorizationFiles = readdirSync(authorizationDir, { withFileTypes: true })
    .filter((entry) => entry.isFile())
  if (directAuthorizationFiles.length) {
    fail('NATIVE_CHECKPOINT_LIVE_AUTHORIZATION_PRESENT', 'a live execution authorization still exists')
  }
  const failedDir = resolve(authorizationDir, 'failed')
  if (existsSync(failedDir) && readdirSync(assertSafeDirectory(failedDir, attemptDir,
    'failed authorization directory')).length) {
    fail('NATIVE_CHECKPOINT_FAILED_AUTHORIZATION_PRESENT', 'a failed authorization exists in the checkpoint attempt')
  }
  const consumedDir = assertSafeDirectory(resolve(authorizationDir, 'consumed'), attemptDir,
    'consumed authorization directory')
  const consumedEntries = readdirSync(consumedDir, { withFileTypes: true })
  if (consumedEntries.some((entry) => !entry.isFile()) ||
      stableJson(consumedEntries.map((entry) => entry.name).sort()) !== stableJson([...expectedConsumedNames].sort())) {
    fail('NATIVE_CHECKPOINT_AUTHORIZATION_SET_INVALID', 'consumed authorization set does not equal the checkpoint prefix')
  }
  const evidenceDir = assertSafeDirectory(resolve(attemptDir, 'evidence'), attemptDir, 'evidence directory')
  if (readdirSync(evidenceDir).some((name) => name.startsWith('.ConfigureNativeWidth888-') ||
      name === '.native-width-888.lock')) {
    fail('NATIVE_CHECKPOINT_PHASE_LOCK_PRESENT', 'an interrupted width phase lock or backup directory remains')
  }
}

function validateCheckpointAudit(attemptDir, task, currentInventoryDigest, completedPhase) {
  const auditRelativePath = 'diagnostics/assemblies-r22-runtime-audit.json'
  const auditPath = resolve(attemptDir, ...auditRelativePath.split('/'))
  if (!existsSync(auditPath)) {
    if (completedPhase === 'assemblies') {
      fail('NATIVE_CHECKPOINT_AUDIT_MISSING', 'assemblies checkpoint requires its runtime audit')
    }
    return { path: '', sha256: '' }
  }
  const audit = readJson(auditPath, attemptDir, 'assemblies runtime audit')
  if (audit.value?.schema !== 'winnsen.native_888x14_assemblies_runtime_audit.v1' ||
      audit.value?.status !== 'ASSEMBLIES_RUNTIME_EVIDENCE_VERIFIED' ||
      audit.value?.taskId !== task.id || Number(audit.value?.checksFailed) !== 0 ||
      Number(audit.value?.checksPassed) !== Number(audit.value?.checksTotal) ||
      Number(audit.value?.checksTotal) < 40 ||
      (completedPhase === 'assemblies' && String(audit.value?.finalInventoryDigest || '').toUpperCase() !== currentInventoryDigest)) {
    fail('NATIVE_CHECKPOINT_AUDIT_INVALID', 'assemblies runtime audit is not bound to this verified checkpoint')
  }
  return { path: auditRelativePath, sha256: sha256(audit.bytes) }
}

function validateStoredCheckpoint(task, descriptor, checkpointDigest, completedIndex, allowAdvance) {
  const stored = task.nativeBuild?.checkpoint
  const storedDigest = String(task.nativeBuild?.checkpointDigest || '').toUpperCase()
  if (!stored && !storedDigest) {
    const anchor = TRUSTED_BOOTSTRAP_ANCHORS[task.id]
    const anchoredProjection = anchor && {
      attempt: descriptor.attempt,
      completedPhase: descriptor.completedPhase,
      planSha256: descriptor.planSha256,
      completedReceiptSha256: descriptor.completedReceiptSha256,
      completedEvidenceSha256: descriptor.completedEvidenceSha256,
      currentInventoryDigest: descriptor.currentInventoryDigest,
      checkpointAuditSha256: descriptor.checkpointAuditSha256,
    }
    if (task.nativeBuild?.state !== 'cancelled' ||
        completedIndex !== NATIVE_STAGE_RECEIPT_ORDER.indexOf('assemblies') ||
        !anchor || stableJson(anchoredProjection) !== stableJson(anchor)) {
      fail('NATIVE_CHECKPOINT_BOOTSTRAP_ANCHOR_MISMATCH',
        'checkpoint without a stored descriptor does not match a trusted bootstrap anchor')
    }
    return
  }
  if (!stored || !isDigest(storedDigest) || nativeCheckpointDescriptorDigest(stored) !== storedDigest) {
    fail('NATIVE_CHECKPOINT_STORED_BINDING_INVALID', 'stored checkpoint descriptor or digest is invalid')
  }
  if (!allowAdvance) {
    if (storedDigest !== checkpointDigest || stableJson(stored) !== stableJson(descriptor)) {
      fail('NATIVE_CHECKPOINT_STORED_BINDING_INVALID', 'stored checkpoint differs from live verified checkpoint')
    }
    return
  }
  const storedIndex = NATIVE_STAGE_RECEIPT_ORDER.indexOf(stored.completedPhase)
  if (task.nativeBuild?.state !== 'building' || storedIndex + 1 !== completedIndex ||
      stored.taskId !== descriptor.taskId || Number(stored.attempt) !== Number(descriptor.attempt) ||
      stored.planSha256 !== descriptor.planSha256 || stored.planWorkerId !== descriptor.planWorkerId ||
      stableJson(stored.toolchainManifests) !== stableJson(descriptor.toolchainManifests) ||
      stored.checkpointAuditPath !== descriptor.checkpointAuditPath ||
      stored.checkpointAuditSha256 !== descriptor.checkpointAuditSha256) {
    fail('NATIVE_CHECKPOINT_ADVANCE_INVALID', 'checkpoint advance is not the exact next phase')
  }
}

export function loadNativeCheckpoint({
  repositoryRoot = DEFAULT_REPOSITORY_ROOT,
  dataDir = resolve(repositoryRoot, 'data'),
  taskId,
  completedPhase = 'assemblies',
  allowCheckpointAdvance = false,
} = {}) {
  const root = assertSafeDirectory(resolve(repositoryRoot), resolve(repositoryRoot), 'repository root')
  const tasks = assertSafeDirectory(taskRoot(dataDir), root, 'native task root')
  const safeTaskId = String(taskId || '')
  if (!TASK_PATTERN.test(safeTaskId)) fail('NATIVE_CHECKPOINT_TASK_INVALID', 'taskId is not a safe native task id')
  if (!CHECKPOINT_PHASES.has(completedPhase)) {
    fail('NATIVE_CHECKPOINT_PHASE_INVALID', 'completedPhase is not an approved checkpoint phase')
  }
  const taskData = readJson(resolve(tasks, `${safeTaskId}.json`), tasks, 'native task')
  const task = taskData.value
  if (task.id !== safeTaskId || task.taskStoreSchema !== 'winnsen.native_task_store_record.v1' ||
      !['cancelled', 'building'].includes(task.nativeBuild?.state) || task.modelReady === true ||
      task.nativeBuild?.modelReady === true || task.nativeBuild?.result) {
    fail('NATIVE_CHECKPOINT_TASK_INVALID', 'native task is not an eligible cancelled/building checkpoint task')
  }
  if (task.nativeBuild.state === 'cancelled' && task.nativeBuild.lease !== null) {
    fail('NATIVE_CHECKPOINT_TASK_INVALID', 'cancelled checkpoint task still has a lease')
  }
  const attempt = Number(task.nativeBuild?.attempt)
  if (!Number.isInteger(attempt) || attempt < 1) fail('NATIVE_CHECKPOINT_TASK_INVALID', 'task attempt is invalid')
  const attemptDir = assertSafeDirectory(resolve(tasks, 'attempts', safeTaskId,
    `attempt-${String(attempt).padStart(4, '0')}`), tasks, 'native attempt')
  const planPath = resolve(attemptDir, 'native_build_plan.json')
  const planData = readJson(planPath, attemptDir, 'immutable native plan')
  const plan = planData.value
  const planSha256 = sha256(planData.bytes)
  const trustedRecipe = TRUSTED_NATIVE_RECIPE_REGISTRY[plan.recipe?.id]
  if (plan.schema !== PLAN_SCHEMA || plan.purpose !== PURPOSE || plan.task?.id !== task.id ||
      !Number.isInteger(Number(plan.task?.revisionAtPlanning)) || !isDigest(plan.task?.digest) ||
      plan.workerId !== String(plan.workerId || '').trim() || !plan.workerId ||
      String(task.nativeBuild?.planArtifact?.fileSha256 || '').toUpperCase() !== planSha256 ||
      Number(task.nativeBuild?.planArtifact?.attempt) !== attempt ||
      plan.qualityBoundary?.planningOnly !== true ||
      plan.executionBoundary?.executorImplemented !== false || plan.executionBoundary?.cadStarted !== false ||
      plan.executionBoundary?.modelGenerated !== false || plan.executionBoundary?.modelReady !== false ||
      plan.executionBoundary?.legacyFallbackUsed !== false) {
    fail('NATIVE_CHECKPOINT_PLAN_INVALID', 'immutable native plan or task binding is invalid')
  }
  validatePlanContracts(plan, trustedRecipe)
  const liveToolchainManifests = resolveTrustedNativeToolchainManifests({ repositoryRoot: root })
  if (!exactKeys(liveToolchainManifests, TOOL_IDS) ||
      stableJson(liveToolchainManifests) !== stableJson(plan.trustedToolchainManifests)) {
    fail('NATIVE_CHECKPOINT_TOOLCHAIN_DRIFT', 'live six-tool manifest map differs from the immutable plan')
  }
  if (task.nativeBuild.state === 'building' &&
      (task.nativeBuild.lease?.workerId !== plan.workerId || !task.nativeBuild.lease?.id)) {
    fail('NATIVE_CHECKPOINT_TASK_INVALID', 'building checkpoint lease does not belong to the immutable plan worker')
  }

  const completedIndex = NATIVE_STAGE_RECEIPT_ORDER.indexOf(completedPhase)
  const prefixPhases = NATIVE_STAGE_RECEIPT_ORDER.slice(0, completedIndex + 1)
  let predecessorReceiptSha256 = ''
  let predecessorPostDigest = ''
  const chainRows = []
  const prefixStageResults = []
  const consumedNames = []
  let completed = null
  for (let index = 0; index < prefixPhases.length; index += 1) {
    const phase = prefixPhases[index]
    const contract = plan.stageReceiptContracts[phase]
    const receiptLocation = normalizedAttemptPath(attemptDir, contract.path, `${phase} receipt`)
    const receiptData = readJson(receiptLocation.absolute, attemptDir, `${phase} receipt`)
    const validated = validateReceipt({ phase, index, contract, receiptPath: receiptLocation.absolute,
      receiptData, attemptDir, task, plan, planSha256, attempt, predecessorReceiptSha256,
      predecessorPostDigest })
    predecessorReceiptSha256 = validated.receiptSha256
    predecessorPostDigest = validated.postInventoryDigest
    consumedNames.push(validated.consumedFileName)
    chainRows.push({
      phase,
      receiptPath: receiptLocation.relative,
      receiptSha256: validated.receiptSha256,
      evidencePath: validated.evidencePath,
      evidenceSha256: validated.evidenceSha256,
      postInventoryDigest: validated.postInventoryDigest,
    })
    prefixStageResults.push(Object.freeze({
      phase,
      receipt: Object.freeze({ phase, path: receiptLocation.absolute, sha256: validated.receiptSha256 }),
      evidence: Object.freeze({ path: validated.evidencePath, sha256: validated.evidenceSha256 }),
      postInventoryDigest: validated.postInventoryDigest,
    }))
    completed = { receiptLocation, ...validated }
  }

  const workingPackRelativePath = 'native_cad/working_pack'
  const workingPack = assertSafeDirectory(resolve(attemptDir, 'native_cad', 'working_pack'), attemptDir,
    'checkpoint working pack')
  const currentInventoryDigest = canonicalFlatCadInventoryDigest(workingPack, { expectedFileCount: 75 })
  if (currentInventoryDigest !== predecessorPostDigest) {
    fail('NATIVE_CHECKPOINT_INVENTORY_DRIFT', 'current 75-file CAD inventory differs from checkpoint receipt')
  }
  validateNoLaterArtifacts(attemptDir, plan, completedIndex, consumedNames)
  const audit = validateCheckpointAudit(attemptDir, task, currentInventoryDigest, completedPhase)
  const prefixReceiptChainSha256 = sha256(Buffer.from(JSON.stringify(chainRows), 'utf8'))
  const descriptor = Object.freeze({
    schema: NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA,
    taskId: task.id,
    attempt,
    planSha256,
    planWorkerId: plan.workerId,
    completedPhase,
    completedReceiptPath: completed.receiptLocation.relative,
    completedReceiptSha256: completed.receiptSha256,
    completedEvidencePath: completed.evidencePath,
    completedEvidenceSha256: completed.evidenceSha256,
    currentInventoryDigest,
    workingPackRelativePath,
    prefixReceiptChainSha256,
    checkpointAuditPath: audit.path,
    checkpointAuditSha256: audit.sha256,
    toolchainManifests: Object.freeze({ ...liveToolchainManifests }),
  })
  const checkpointDigest = nativeCheckpointDescriptorDigest(descriptor)
  validateStoredCheckpoint(task, descriptor, checkpointDigest, completedIndex, allowCheckpointAdvance === true)
  return Object.freeze({
    descriptor,
    checkpoint: descriptor,
    checkpointDigest,
    plan: Object.freeze(plan),
    prefixStageResults: Object.freeze(prefixStageResults),
    currentInventoryDigest,
    nextPhase: NATIVE_STAGE_RECEIPT_ORDER[completedIndex + 1] || '',
    attemptDir,
    planPath,
    workerId: plan.workerId,
  })
}
