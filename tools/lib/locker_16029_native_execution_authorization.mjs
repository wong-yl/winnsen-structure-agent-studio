import { createHash, randomBytes } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fsyncSync,
  lstatSync,
  mkdirSync,
  openSync,
  readFileSync,
  realpathSync,
  renameSync,
  statSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_SEED_RECEIPT_KEYS,
  nativeEvidenceCommitmentSha256,
} from './locker_16029_native_stage_contract.mjs'

const AUTHORIZATION_SCHEMA = 'winnsen.native_execution_authorization.v1'
const PLAN_SCHEMA = 'winnsen.native_build_plan.v1'
const PURPOSE = 'structure_engineering_assistance'
const DEFAULT_TTL_MS = 10 * 60 * 1000
const MAX_TTL_MS = 30 * 60 * 1000
const AUTHORIZATION_KEYS = Object.freeze([
  'schema', 'authorizationId', 'issuedAt', 'expiresAt', 'purpose', 'workerId',
  'task', 'request', 'plan', 'recipe', 'tool', 'seed', 'execution', 'qualityBoundary',
])
const QUALITY_BOUNDARY = Object.freeze({
  engineeringAssistanceReady: false,
  readyOnlyAfterEveryRequiredCheckPasses: true,
})
export const NATIVE_EXECUTION_TOOL_CONTRACTS = Object.freeze({
  native_seed_pack_888x14_v1: Object.freeze(['clone_native_seed']),
  native_width_888_v1: Object.freeze(['dimensions', 'derived', 'base-hole', 'assemblies']),
  native_door_module_888x14_v1: Object.freeze(['door_module_888x14']),
  native_lock_topology_888x14_v1: Object.freeze(['lock_topology_888x14']),
  native_root_assembly_888x14_v1: Object.freeze(['root_assembly_888x14']),
  native_final_pack_888x14_v1: Object.freeze(['final_pack_and_relocated_reopen']),
})

function fail(code, message) {
  const error = new Error(message)
  error.code = code
  throw error
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function stableJson(value) {
  return JSON.stringify(stableValue(value))
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256(readFileSync(path))
}

function assertSingleLinkRegularFile(path, label) {
  let info
  try {
    info = statSync(path)
  } catch {
    fail('NATIVE_AUTHORIZATION_PATH_INVALID', `${label} cannot be inspected`)
  }
  if (!info.isFile()) {
    fail('NATIVE_AUTHORIZATION_PATH_INVALID', `${label} must be a regular file`)
  }
  if (Number(info.nlink) !== 1) {
    fail('NATIVE_AUTHORIZATION_HARDLINK_PATH', `${label} must not be hardlinked`)
  }
}

function isSha256(value) {
  return /^[A-F0-9]{64}$/i.test(String(value || ''))
}

function safeIdentifier(value, label) {
  const normalized = String(value || '')
  if (!/^[A-Za-z0-9_.-]{1,160}$/.test(normalized)) {
    fail('NATIVE_AUTHORIZATION_IDENTIFIER_INVALID', `${label} is not a safe identifier`)
  }
  return normalized
}

function safePhases(phases) {
  if (!Array.isArray(phases) || phases.length < 1 || phases.length > 32) {
    fail('NATIVE_AUTHORIZATION_PHASES_INVALID', 'authorization phases must be a non-empty array')
  }
  const normalized = phases.map((value) => safeIdentifier(value, 'phase'))
  if (new Set(normalized).size !== normalized.length) {
    fail('NATIVE_AUTHORIZATION_PHASES_INVALID', 'authorization phases must be unique')
  }
  return normalized
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function assertNoReparsePath(path, stopAt) {
  const root = resolve(stopAt)
  let current = resolve(path)
  if (!underRoot(current, root)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', `${current} escaped ${root}`)
  }
  const rows = []
  while (underRoot(current, root)) {
    rows.push(current)
    if (current === root) break
    const parent = dirname(current)
    if (parent === current) break
    current = parent
  }
  for (const row of rows.reverse()) {
    if (!existsSync(row)) continue
    const info = lstatSync(row)
    if (info.isSymbolicLink()) {
      fail('NATIVE_AUTHORIZATION_REPARSE_PATH', `authorization path contains a reparse point: ${row}`)
    }
  }
  if (existsSync(root)) {
    const realRoot = realpathSync.native(root)
    for (const row of rows) {
      if (!existsSync(row)) continue
      const real = realpathSync.native(row)
      if (!underRoot(real, realRoot)) {
        fail('NATIVE_AUTHORIZATION_REPARSE_PATH', `authorization path resolves outside attempt: ${row}`)
      }
    }
  }
}

function parseTime(value, label) {
  const milliseconds = Date.parse(String(value || ''))
  if (!Number.isFinite(milliseconds)) fail('NATIVE_AUTHORIZATION_TIME_INVALID', `${label} is invalid`)
  return milliseconds
}

function nowMilliseconds(clock) {
  const value = clock()
  const milliseconds = (value instanceof Date ? value : new Date(value)).getTime()
  if (!Number.isFinite(milliseconds)) fail('NATIVE_AUTHORIZATION_TIME_INVALID', 'clock returned an invalid time')
  return milliseconds
}

function taskDigestForPlan(task) {
  return sha256(stableJson({
    id: String(task?.id || ''),
    revision: Number(task?.revision || 0),
    status: String(task?.status || ''),
    taskType: String(task?.taskType || ''),
    requestFingerprint: String(task?.requestFingerprint || ''),
    nativeBuildAttempt: Number(task?.nativeBuild?.attempt || 0),
  }))
}

function readAndValidatePlan({ task, planPath, workerId }) {
  if (!existsSync(planPath)) fail('NATIVE_AUTHORIZATION_PLAN_MISSING', 'native build plan is missing')
  assertSingleLinkRegularFile(planPath, 'native build plan')
  let plan
  try {
    plan = JSON.parse(readFileSync(planPath, 'utf8'))
  } catch {
    fail('NATIVE_AUTHORIZATION_PLAN_INVALID', 'native build plan is not valid JSON')
  }
  if (plan?.schema !== PLAN_SCHEMA || plan?.purpose !== PURPOSE) {
    fail('NATIVE_AUTHORIZATION_PLAN_INVALID', 'native build plan schema or purpose is invalid')
  }
  if (plan?.qualityBoundary?.planningOnly !== true || plan?.executionBoundary?.executorImplemented !== false ||
      plan?.executionBoundary?.cadStarted !== false || plan?.executionBoundary?.modelGenerated !== false ||
      plan?.executionBoundary?.modelReady !== false || plan?.executionBoundary?.legacyFallbackUsed !== false) {
    fail('NATIVE_AUTHORIZATION_PLAN_BOUNDARY_INVALID', 'native build plan must remain planning-only')
  }
  const planSha256 = sha256File(resolve(planPath))
  const exactPlanningBinding = Number(plan?.task?.revisionAtPlanning) === Number(task.revision) &&
    String(plan?.task?.digest || '').toUpperCase() === taskDigestForPlan(task)
  const historicalPlanningDigest = sha256(stableJson({
    id: String(task?.id || ''),
    revision: Number(plan?.task?.revisionAtPlanning || 0),
    status: 'native_source_planning',
    taskType: String(task?.taskType || ''),
    requestFingerprint: String(task?.requestFingerprint || ''),
    nativeBuildAttempt: Number(task?.nativeBuild?.attempt || 0),
  }))
  const continuedLeaseBinding = Number.isInteger(Number(plan?.task?.revisionAtPlanning)) &&
    Number(plan.task.revisionAtPlanning) > 0 && Number(plan.task.revisionAtPlanning) <= Number(task.revision) &&
    String(plan?.task?.digest || '').toUpperCase() === historicalPlanningDigest &&
    Number(task?.nativeBuild?.planArtifact?.attempt) === Number(task?.nativeBuild?.attempt) &&
    String(task?.nativeBuild?.planArtifact?.fileSha256 || '').toUpperCase() === planSha256
  if (String(plan?.workerId || '') !== workerId || String(plan?.task?.id || '') !== String(task.id || '') ||
      (!exactPlanningBinding && !continuedLeaseBinding) ||
      String(plan?.request?.fingerprint || '') !== String(task.requestFingerprint || '') ||
      !isSha256(plan?.request?.digest) || !isSha256(plan?.recipe?.digest)) {
    fail('NATIVE_AUTHORIZATION_PLAN_BINDING_INVALID', 'native build plan no longer matches the leased task')
  }
  if (!Array.isArray(plan?.requiredChecks) || !plan.requiredChecks.includes('one_door_one_lock') ||
      !plan.requiredChecks.includes('rebuild_save_reopen') || !plan.requiredChecks.includes('relocated_reopen')) {
    fail('NATIVE_AUTHORIZATION_PLAN_CHECKS_INVALID', 'native build plan is missing mandatory validation checks')
  }
  return plan
}

function validateLease({ task, workerId, nowMs }) {
  if (!['planning', 'building', 'validating'].includes(task?.nativeBuild?.state)) {
    fail('NATIVE_AUTHORIZATION_TASK_STATE_INVALID', 'only an active leased native task can be authorized')
  }
  const lease = task?.nativeBuild?.lease
  if (!lease || String(lease.workerId || '') !== workerId || !String(lease.id || '')) {
    fail('NATIVE_AUTHORIZATION_LEASE_MISMATCH', 'task lease does not belong to this worker')
  }
  const expiresAtMs = parseTime(lease.expiresAt, 'lease.expiresAt')
  if (expiresAtMs <= nowMs) fail('NATIVE_AUTHORIZATION_LEASE_EXPIRED', 'task lease has expired')
  return { lease, expiresAtMs }
}

function validateTool(tool) {
  const normalized = {
    id: safeIdentifier(tool?.id, 'tool.id'),
    sourceNormalizedSha256: String(tool?.sourceNormalizedSha256 || '').toUpperCase(),
    executableSha256: String(tool?.executableSha256 || '').toUpperCase(),
  }
  if (!isSha256(normalized.sourceNormalizedSha256) || !isSha256(normalized.executableSha256)) {
    fail('NATIVE_AUTHORIZATION_TOOL_INVALID', 'tool source and executable SHA-256 are required')
  }
  if (!Object.hasOwn(NATIVE_EXECUTION_TOOL_CONTRACTS, normalized.id)) {
    fail('NATIVE_AUTHORIZATION_TOOL_NOT_REGISTERED', `native execution tool is not registered: ${normalized.id}`)
  }
  return normalized
}

function validateToolPhaseContract(toolId, phases) {
  const expected = NATIVE_EXECUTION_TOOL_CONTRACTS[toolId]
  if (!expected || stableJson(phases) !== stableJson(expected)) {
    fail('NATIVE_AUTHORIZATION_PHASE_CONTRACT_MISMATCH', `phases do not match ${toolId}`)
  }
}

function expectedBindings({ task, plan, planSha256, workerId, tool, seedInventoryDigest, attempt, phases }) {
  return {
    workerId,
    task: {
      id: String(plan.task.id || ''),
      revision: Number(plan.task.revisionAtPlanning),
      digest: String(plan.task.digest).toUpperCase(),
      leaseId: String(task.nativeBuild.lease.id),
      leaseExpiresAt: String(task.nativeBuild.lease.expiresAt),
    },
    request: {
      fingerprint: String(plan.request.fingerprint),
      digest: String(plan.request.digest).toUpperCase(),
    },
    plan: { sha256: planSha256 },
    recipe: {
      id: String(plan.recipe.id),
      version: Number(plan.recipe.version),
      digest: String(plan.recipe.digest).toUpperCase(),
    },
    tool,
    seed: { inventoryDigest: seedInventoryDigest },
    execution: { authorized: true, attempt, phases },
  }
}

function bindingDigest(value) {
  return sha256(stableJson(value))
}

function atomicWriteImmutable(path, content) {
  if (existsSync(path)) {
    const existing = readFileSync(path, 'utf8')
    if (existing !== content) {
      fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', `authorization already exists with different content: ${basename(path)}`)
    }
    return { created: false, sha256: sha256(existing) }
  }
  mkdirSync(dirname(path), { recursive: true })
  const temp = `${path}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`
  let descriptor = null
  try {
    descriptor = openSync(temp, 'wx')
    writeFileSync(descriptor, content, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    if (existsSync(path)) {
      const existing = readFileSync(path, 'utf8')
      if (existing !== content) {
        fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', `authorization was created concurrently with different content: ${basename(path)}`)
      }
      unlinkSync(temp)
      return { created: false, sha256: sha256(existing) }
    }
    renameSync(temp, path)
    return { created: true, sha256: sha256(content) }
  } finally {
    if (descriptor !== null) closeSync(descriptor)
    if (existsSync(temp)) unlinkSync(temp)
  }
}

function atomicReplaceAuthorization(path, content) {
  const temp = `${path}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`
  let descriptor = null
  try {
    descriptor = openSync(temp, 'wx')
    writeFileSync(descriptor, content, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    renameSync(temp, path)
    return { created: true, sha256: sha256(content) }
  } finally {
    if (descriptor !== null) closeSync(descriptor)
    if (existsSync(temp)) unlinkSync(temp)
  }
}

function exactKeys(value, expected) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function readJsonFile(path, label, code = 'NATIVE_AUTHORIZATION_RECEIPT_INVALID') {
  try {
    return JSON.parse(readFileSync(path, 'utf8'))
  } catch {
    fail(code, `${label} is not valid JSON`)
  }
}

function normalizedAttemptRelativePath(path, attemptDir, label) {
  const absolute = resolve(attemptDir, String(path || ''))
  if (!underRoot(absolute, attemptDir) || absolute === resolve(attemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', `${label} escaped the attempt`)
  }
  const normalized = relative(resolve(attemptDir), absolute).replaceAll('\\', '/')
  if (String(path || '').replaceAll('\\', '/') !== normalized || normalized.startsWith('../')) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', `${label} is not a canonical attempt-relative path`)
  }
  return { absolute, relative: normalized }
}

function evidenceValue(evidence, keys) {
  for (const key of keys) {
    if (Object.hasOwn(evidence, key)) return evidence[key]
  }
  return undefined
}

function requireEvidenceText(evidence, keys, expected, label, { sha = false } = {}) {
  const actual = String(evidenceValue(evidence, keys) ?? '')
  const wanted = String(expected ?? '')
  const matches = sha
    ? actual.toUpperCase() === wanted.toUpperCase() && isSha256(actual)
    : actual === wanted
  if (!matches) fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', `stage evidence ${label} does not match its receipt`)
}

function validateStageReceiptAndEvidence({
  attemptDir,
  plan,
  authorization,
  authorizationBytes,
  authorizationSha256,
  receiptPath,
  expectedPhase,
  expectedPostInventoryDigest,
  nowMs,
}) {
  const contract = plan?.stageReceiptContracts?.[expectedPhase]
  if (!exactKeys(contract, ['schema', 'path', 'producerToolId', 'predecessor']) ||
      contract.producerToolId !== authorization.tool.id) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', `plan has no trusted receipt contract for ${expectedPhase}`)
  }
  const expectedReceipt = normalizedAttemptRelativePath(contract.path, attemptDir, 'stage receipt')
  if (resolve(receiptPath) !== expectedReceipt.absolute) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage receipt path does not match the immutable plan')
  }
  assertNoReparsePath(expectedReceipt.absolute, attemptDir)
  assertSingleLinkRegularFile(expectedReceipt.absolute, 'native stage receipt')
  const receipt = readJsonFile(expectedReceipt.absolute, 'native stage receipt')
  const expectedKeys = expectedPhase === 'clone_native_seed'
    ? NATIVE_SEED_RECEIPT_KEYS
    : NATIVE_RUNTIME_RECEIPT_KEYS
  if (!exactKeys(receipt, expectedKeys) || receipt.schema !== contract.schema ||
      receipt.phase !== expectedPhase || receipt.success !== true) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage receipt schema, keys, phase, or success is invalid')
  }
  const expectedPost = String(expectedPostInventoryDigest || '').toUpperCase()
  if (!isSha256(expectedPost)) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'expected post-stage inventory digest is invalid')
  }
  const receiptPre = String(expectedPhase === 'clone_native_seed'
    ? receipt.sourceInventoryDigest
    : receipt.preInventoryDigest).toUpperCase()
  const receiptPost = String(expectedPhase === 'clone_native_seed'
    ? receipt.targetInventoryDigest
    : receipt.postInventoryDigest).toUpperCase()
  let predecessorValid = receipt.predecessorReceiptSha256 === ''
  if (contract.predecessor !== '') {
    const predecessorContract = plan?.stageReceiptContracts?.[contract.predecessor]
    if (!exactKeys(predecessorContract, ['schema', 'path', 'producerToolId', 'predecessor'])) {
      fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'predecessor receipt contract is missing or malformed')
    }
    const predecessorLocation = normalizedAttemptRelativePath(
      predecessorContract.path,
      attemptDir,
      'predecessor stage receipt',
    )
    if (predecessorLocation.absolute === expectedReceipt.absolute) {
      fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage receipt cannot be its own predecessor')
    }
    assertNoReparsePath(predecessorLocation.absolute, attemptDir)
    assertSingleLinkRegularFile(predecessorLocation.absolute, 'predecessor native stage receipt')
    predecessorValid = String(receipt.predecessorReceiptSha256 || '').toUpperCase() ===
      sha256File(predecessorLocation.absolute)
  }
  const authorizationBase64 = String(receipt.authorizationJsonBase64 || '')
  let decodedAuthorization = null
  try {
    decodedAuthorization = Buffer.from(authorizationBase64, 'base64')
  } catch {
    decodedAuthorization = null
  }
  if (!decodedAuthorization || decodedAuthorization.toString('base64') !== authorizationBase64 ||
      !decodedAuthorization.equals(authorizationBytes) ||
      String(receipt.authorizationSha256 || '').toUpperCase() !== authorizationSha256 ||
      String(receipt.taskId || '') !== authorization.task.id ||
      Number(receipt.taskRevision) !== Number(authorization.task.revision) ||
      String(receipt.taskDigest || '').toUpperCase() !== authorization.task.digest ||
      String(receipt.requestDigest || '').toUpperCase() !== authorization.request.digest ||
      String(receipt.leaseId || '') !== authorization.task.leaseId ||
      String(receipt.leaseExpiresAt || '') !== authorization.task.leaseExpiresAt ||
      Number(receipt.attempt) !== Number(authorization.execution.attempt) ||
      String(receipt.planSha256 || '').toUpperCase() !== authorization.plan.sha256 ||
      String(receipt.authorizationId || '') !== authorization.authorizationId ||
      String(receipt.authorizationIssuedAt || '') !== authorization.issuedAt ||
      String(receipt.authorizationExpiresAt || '') !== authorization.expiresAt ||
      String(receipt.recipeId || '') !== authorization.recipe.id ||
      String(receipt.recipeDigest || '').toUpperCase() !== authorization.recipe.digest ||
      String(receipt.toolId || '') !== authorization.tool.id ||
      String(receipt.toolSourceNormalizedSha256 || '').toUpperCase() !== authorization.tool.sourceNormalizedSha256 ||
      String(receipt.toolExecutableSha256 || '').toUpperCase() !== authorization.tool.executableSha256 ||
      receiptPre !== authorization.seed.inventoryDigest || receiptPost !== expectedPost || !predecessorValid) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage receipt does not match the authorization or inventory transition')
  }
  const issuedAtMs = parseTime(receipt.authorizationIssuedAt, 'receipt.authorizationIssuedAt')
  const expiresAtMs = parseTime(receipt.authorizationExpiresAt, 'receipt.authorizationExpiresAt')
  const leaseExpiresAtMs = parseTime(receipt.leaseExpiresAt, 'receipt.leaseExpiresAt')
  const completedAtMs = parseTime(receipt.completedAt, 'receipt.completedAt')
  if (completedAtMs < issuedAtMs || completedAtMs > expiresAtMs || completedAtMs > leaseExpiresAtMs ||
      completedAtMs > nowMs) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage receipt completion time is outside its authorization window')
  }
  const evidenceLocation = normalizedAttemptRelativePath(receipt.evidencePath, attemptDir, 'stage evidence')
  if (!evidenceLocation.relative.startsWith('evidence/') || evidenceLocation.absolute === expectedReceipt.absolute) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage evidence path is not an isolated evidence artifact')
  }
  assertNoReparsePath(evidenceLocation.absolute, attemptDir)
  assertSingleLinkRegularFile(evidenceLocation.absolute, 'native stage evidence')
  const evidenceSha256 = sha256File(evidenceLocation.absolute)
  if (evidenceSha256 !== String(receipt.evidenceSha256 || '').toUpperCase()) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage evidence SHA-256 does not match its receipt')
  }
  const evidence = readJsonFile(evidenceLocation.absolute, 'native stage evidence')
  const commitment = nativeEvidenceCommitmentSha256(evidence)
  if (evidence.success !== true || commitment !== String(receipt.evidenceCommitmentSha256 || '').toUpperCase()) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'stage evidence is not a committed successful artifact')
  }
  requireEvidenceText(evidence, ['phase'], expectedPhase, 'phase')
  requireEvidenceText(evidence, ['task_id', 'taskId'], authorization.task.id, 'task')
  requireEvidenceText(evidence, ['plan_sha256', 'planSha256'], authorization.plan.sha256, 'plan', { sha: true })
  requireEvidenceText(evidence, ['authorization_id', 'authorizationId'], authorization.authorizationId, 'authorization')
  requireEvidenceText(evidence, [
    'source_inventory_digest', 'initial_inventory_digest',
    'pre_inventory_digest', 'preInventoryDigest',
  ], receiptPre,
    'pre-stage inventory', { sha: true })
  requireEvidenceText(evidence, ['post_inventory_digest', 'target_inventory_digest', 'postInventoryDigest', 'targetInventoryDigest'],
    receiptPost, 'post-stage inventory', { sha: true })
  requireEvidenceText(evidence, ['completed_at_utc', 'completedAtUtc', 'completedAt', 'evidence_commit_completed_at_utc'],
    receipt.completedAt, 'completion time')
  requireEvidenceText(evidence, ['evidence_commitment_sha256', 'evidenceCommitmentSha256'], commitment,
    'commitment', { sha: true })
  return { receipt, receiptSha256: sha256File(expectedReceipt.absolute), evidence, evidenceSha256, commitment }
}

export function validateNativeExecutionAuthorization({
  authorization,
  task,
  planPath,
  workerId,
  tool,
  seedInventoryDigest,
  attempt,
  phases,
  clock = () => new Date(),
} = {}) {
  const safeWorkerId = safeIdentifier(workerId, 'workerId')
  const safeTool = validateTool(tool)
  const safeSeedDigest = String(seedInventoryDigest || '').toUpperCase()
  if (!isSha256(safeSeedDigest)) fail('NATIVE_AUTHORIZATION_SEED_INVALID', 'seed inventory digest is required')
  const safeAttempt = Number(attempt)
  if (!Number.isInteger(safeAttempt) || safeAttempt < 1) fail('NATIVE_AUTHORIZATION_ATTEMPT_INVALID', 'attempt must be positive')
  const safePhaseList = safePhases(phases)
  validateToolPhaseContract(safeTool.id, safePhaseList)
  const nowMs = nowMilliseconds(clock)
  const { expiresAtMs: leaseExpiresAtMs } = validateLease({ task, workerId: safeWorkerId, nowMs })
  const plan = readAndValidatePlan({ task, planPath: resolve(planPath), workerId: safeWorkerId })
  const planSha256 = sha256File(resolve(planPath))
  if (String(authorization?.plan?.sha256 || '').toUpperCase() !== planSha256) {
    fail('NATIVE_AUTHORIZATION_PLAN_MISMATCH', 'native build plan no longer matches the authorized SHA-256')
  }
  const bindings = expectedBindings({
    task,
    plan,
    planSha256,
    workerId: safeWorkerId,
    tool: safeTool,
    seedInventoryDigest: safeSeedDigest,
    attempt: safeAttempt,
    phases: safePhaseList,
  })
  const actualKeys = authorization && typeof authorization === 'object' && !Array.isArray(authorization)
    ? Object.keys(authorization).sort()
    : []
  if (!authorization || JSON.stringify(actualKeys) !== JSON.stringify([...AUTHORIZATION_KEYS].sort()) ||
      authorization.schema !== AUTHORIZATION_SCHEMA ||
      authorization.purpose !== PURPOSE || stableJson(authorization.qualityBoundary) !== stableJson(QUALITY_BOUNDARY) ||
      stableJson({
        workerId: authorization.workerId,
        task: authorization.task,
        request: authorization.request,
        plan: authorization.plan,
        recipe: authorization.recipe,
        tool: authorization.tool,
        seed: authorization.seed,
        execution: authorization.execution,
      }) !== stableJson(bindings)) {
    fail('NATIVE_AUTHORIZATION_BINDING_MISMATCH', 'authorization bindings do not match the task, plan, tool, or seed')
  }
  const issuedAtMs = parseTime(authorization.issuedAt, 'authorization.issuedAt')
  const expiresAtMs = parseTime(authorization.expiresAt, 'authorization.expiresAt')
  if (issuedAtMs > nowMs || expiresAtMs <= nowMs || expiresAtMs > leaseExpiresAtMs || expiresAtMs <= issuedAtMs) {
    fail('NATIVE_AUTHORIZATION_EXPIRED', 'authorization time window is invalid or exceeds the task lease')
  }
  const expectedId = `native-auth-${bindingDigest({ ...bindings, issuedAt: authorization.issuedAt, expiresAt: authorization.expiresAt }).slice(0, 32).toLowerCase()}`
  if (authorization.authorizationId !== expectedId) {
    fail('NATIVE_AUTHORIZATION_BINDING_MISMATCH', 'authorization identity does not match its immutable bindings')
  }
  return { ok: true, authorizationId: expectedId, planSha256, bindings }
}

export function consumeNativeExecutionAuthorization({
  attemptDir,
  planPath,
  task,
  workerId,
  tool,
  seedInventoryDigest,
  attempt,
  phases,
  authorizationPath,
  receiptPath,
  expectedPhase,
  expectedPostInventoryDigest,
  clock = () => new Date(),
} = {}) {
  const safeAttemptDir = resolve(attemptDir)
  const safePlanPath = resolve(planPath)
  const safeTool = validateTool(tool)
  const safePhase = safeIdentifier(expectedPhase, 'expectedPhase')
  const expectedAuthorizationPath = resolve(safeAttemptDir, 'execution_authorizations', `${safeTool.id}.json`)
  const safeAuthorizationPath = resolve(authorizationPath)
  const safeReceiptPath = resolve(receiptPath)
  if (safeAuthorizationPath !== expectedAuthorizationPath ||
      !underRoot(safeAuthorizationPath, safeAttemptDir) || !underRoot(safeReceiptPath, safeAttemptDir) ||
      !underRoot(safePlanPath, safeAttemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', 'authorization consumption paths escaped the attempt contract')
  }
  assertNoReparsePath(safePlanPath, safeAttemptDir)
  assertNoReparsePath(safeAuthorizationPath, safeAttemptDir)
  assertNoReparsePath(safeReceiptPath, safeAttemptDir)
  assertSingleLinkRegularFile(safeAuthorizationPath, 'native execution authorization')
  const authorizationBytes = readFileSync(safeAuthorizationPath)
  const authorizationSha256 = sha256(authorizationBytes)
  const authorization = readJsonFile(safeAuthorizationPath, 'native execution authorization',
    'NATIVE_AUTHORIZATION_BINDING_MISMATCH')
  const verification = validateNativeExecutionAuthorization({
    authorization,
    task,
    planPath: safePlanPath,
    workerId,
    tool: safeTool,
    seedInventoryDigest,
    attempt,
    phases,
    clock,
  })
  if (!authorization.execution.phases.includes(safePhase)) {
    fail('NATIVE_AUTHORIZATION_RECEIPT_INVALID', 'completed phase is not covered by the authorization')
  }
  const plan = readAndValidatePlan({ task, planPath: safePlanPath, workerId: safeIdentifier(workerId, 'workerId') })
  const nowMs = nowMilliseconds(clock)
  const stage = validateStageReceiptAndEvidence({
    attemptDir: safeAttemptDir,
    plan,
    authorization,
    authorizationBytes,
    authorizationSha256,
    receiptPath: safeReceiptPath,
    expectedPhase: safePhase,
    expectedPostInventoryDigest,
    nowMs,
  })
  if (sha256File(safeAuthorizationPath) !== authorizationSha256 ||
      !readFileSync(safeAuthorizationPath).equals(authorizationBytes)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'authorization changed before it could be consumed')
  }
  const consumedDir = resolve(safeAttemptDir, 'execution_authorizations', 'consumed')
  const archivedAuthorizationPath = resolve(consumedDir, `${safePhase}-${verification.authorizationId}.json`)
  if (!underRoot(archivedAuthorizationPath, safeAttemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', 'consumed authorization path escaped the attempt')
  }
  assertNoReparsePath(dirname(consumedDir), safeAttemptDir)
  mkdirSync(consumedDir, { recursive: true })
  assertNoReparsePath(consumedDir, safeAttemptDir)
  if (existsSync(archivedAuthorizationPath)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'consumed authorization archive already exists')
  }
  renameSync(safeAuthorizationPath, archivedAuthorizationPath)
  assertNoReparsePath(archivedAuthorizationPath, safeAttemptDir)
  assertSingleLinkRegularFile(archivedAuthorizationPath, 'consumed native execution authorization')
  if (existsSync(safeAuthorizationPath) || sha256File(archivedAuthorizationPath) !== authorizationSha256 ||
      !readFileSync(archivedAuthorizationPath).equals(authorizationBytes)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'consumed authorization archive verification failed')
  }
  return {
    consumed: true,
    authorizationId: verification.authorizationId,
    authorizationSha256,
    archivedAuthorizationPath,
    receiptSha256: stage.receiptSha256,
    evidenceSha256: stage.evidenceSha256,
    postInventoryDigest: String(expectedPostInventoryDigest).toUpperCase(),
  }
}

export function containFailedNativeExecutionAuthorization({
  attemptDir,
  planPath,
  task,
  workerId,
  tool,
  seedInventoryDigest,
  attempt,
  phases,
  authorizationPath,
  failedPhase,
} = {}) {
  const safeAttemptDir = resolve(attemptDir)
  const safePlanPath = resolve(planPath)
  const safeTool = validateTool(tool)
  const safePhase = safeIdentifier(failedPhase, 'failedPhase')
  const expectedAuthorizationPath = resolve(safeAttemptDir, 'execution_authorizations', `${safeTool.id}.json`)
  const safeAuthorizationPath = resolve(authorizationPath)
  if (safeAuthorizationPath !== expectedAuthorizationPath ||
      !underRoot(safeAuthorizationPath, safeAttemptDir) || !underRoot(safePlanPath, safeAttemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', 'failed authorization containment paths escaped the attempt contract')
  }
  assertNoReparsePath(safePlanPath, safeAttemptDir)
  assertNoReparsePath(safeAuthorizationPath, safeAttemptDir)
  assertSingleLinkRegularFile(safeAuthorizationPath, 'failed native execution authorization')
  const authorizationBytes = readFileSync(safeAuthorizationPath)
  const authorizationSha256 = sha256(authorizationBytes)
  const authorization = readJsonFile(safeAuthorizationPath, 'failed native execution authorization',
    'NATIVE_AUTHORIZATION_BINDING_MISMATCH')
  const issuedAtMs = parseTime(authorization.issuedAt, 'authorization.issuedAt')
  const historicalClock = () => new Date(issuedAtMs + 1)
  const verification = validateNativeExecutionAuthorization({
    authorization,
    task,
    planPath: safePlanPath,
    workerId,
    tool: safeTool,
    seedInventoryDigest,
    attempt,
    phases,
    clock: historicalClock,
  })
  if (!authorization.execution.phases.includes(safePhase)) {
    fail('NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED', 'failed phase is not covered by the authorization')
  }
  const plan = readAndValidatePlan({ task, planPath: safePlanPath, workerId: safeIdentifier(workerId, 'workerId') })
  const contract = plan?.stageReceiptContracts?.[safePhase]
  if (!exactKeys(contract, ['schema', 'path', 'producerToolId', 'predecessor']) ||
      contract.producerToolId !== safeTool.id) {
    fail('NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED', 'failed phase has no exact receipt contract')
  }
  const receiptLocation = normalizedAttemptRelativePath(contract.path, safeAttemptDir, 'failed stage receipt')
  assertNoReparsePath(dirname(receiptLocation.absolute), safeAttemptDir)
  if (existsSync(receiptLocation.absolute)) {
    fail('NATIVE_AUTHORIZATION_FAILURE_CONTAINMENT_REFUSED',
      'a stage receipt exists; the authorization must be consumed or investigated instead of failure-archived')
  }
  if (sha256File(safeAuthorizationPath) !== authorizationSha256 ||
      !readFileSync(safeAuthorizationPath).equals(authorizationBytes)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'authorization changed before failure containment')
  }
  const failedDir = resolve(safeAttemptDir, 'execution_authorizations', 'failed')
  const archivedAuthorizationPath = resolve(failedDir, `${safePhase}-${verification.authorizationId}.json`)
  if (!underRoot(archivedAuthorizationPath, safeAttemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', 'failed authorization archive escaped the attempt')
  }
  assertNoReparsePath(dirname(failedDir), safeAttemptDir)
  mkdirSync(failedDir, { recursive: true })
  assertNoReparsePath(failedDir, safeAttemptDir)
  if (existsSync(archivedAuthorizationPath)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'failed authorization archive already exists')
  }
  renameSync(safeAuthorizationPath, archivedAuthorizationPath)
  assertNoReparsePath(archivedAuthorizationPath, safeAttemptDir)
  assertSingleLinkRegularFile(archivedAuthorizationPath, 'failed native execution authorization archive')
  if (existsSync(safeAuthorizationPath) || sha256File(archivedAuthorizationPath) !== authorizationSha256 ||
      !readFileSync(archivedAuthorizationPath).equals(authorizationBytes)) {
    fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'failed authorization archive verification failed')
  }
  return {
    contained: true,
    authorizationId: verification.authorizationId,
    authorizationSha256,
    archivedAuthorizationPath,
    failedPhase: safePhase,
  }
}

export function issueNativeExecutionAuthorization({
  attemptDir,
  planPath,
  task,
  workerId,
  tool,
  seedInventoryDigest,
  attempt,
  phases,
  clock = () => new Date(),
  ttlMs = DEFAULT_TTL_MS,
} = {}) {
  const safeAttemptDir = resolve(attemptDir)
  const safePlanPath = resolve(planPath)
  if (!underRoot(safePlanPath, safeAttemptDir) || dirname(safePlanPath) !== safeAttemptDir || basename(safePlanPath) !== 'native_build_plan.json') {
    fail('NATIVE_AUTHORIZATION_PLAN_PATH_INVALID', 'native build plan must be the direct attempt artifact')
  }
  assertNoReparsePath(safePlanPath, safeAttemptDir)
  const safeWorkerId = safeIdentifier(workerId, 'workerId')
  const safeTool = validateTool(tool)
  const safeSeedDigest = String(seedInventoryDigest || '').toUpperCase()
  if (!isSha256(safeSeedDigest)) fail('NATIVE_AUTHORIZATION_SEED_INVALID', 'seed inventory digest is required')
  const safeAttempt = Number(attempt)
  if (!Number.isInteger(safeAttempt) || safeAttempt < 1) fail('NATIVE_AUTHORIZATION_ATTEMPT_INVALID', 'attempt must be positive')
  const safePhaseList = safePhases(phases)
  validateToolPhaseContract(safeTool.id, safePhaseList)
  const nowMs = nowMilliseconds(clock)
  const { expiresAtMs: leaseExpiresAtMs } = validateLease({ task, workerId: safeWorkerId, nowMs })
  const duration = Number(ttlMs)
  if (!Number.isInteger(duration) || duration < 1000 || duration > MAX_TTL_MS) {
    fail('NATIVE_AUTHORIZATION_TTL_INVALID', `authorization ttl must be 1000..${MAX_TTL_MS} ms`)
  }
  const expiresAtMs = Math.min(nowMs + duration, leaseExpiresAtMs)
  if (expiresAtMs <= nowMs) fail('NATIVE_AUTHORIZATION_LEASE_EXPIRED', 'task lease expires before authorization can be issued')
  const plan = readAndValidatePlan({ task, planPath: safePlanPath, workerId: safeWorkerId })
  const planSha256 = sha256File(safePlanPath)
  const bindings = expectedBindings({
    task,
    plan,
    planSha256,
    workerId: safeWorkerId,
    tool: safeTool,
    seedInventoryDigest: safeSeedDigest,
    attempt: safeAttempt,
    phases: safePhaseList,
  })
  const issuedAt = new Date(nowMs).toISOString()
  const expiresAt = new Date(expiresAtMs).toISOString()
  const authorizationId = `native-auth-${bindingDigest({ ...bindings, issuedAt, expiresAt }).slice(0, 32).toLowerCase()}`
  const authorization = {
    schema: AUTHORIZATION_SCHEMA,
    authorizationId,
    issuedAt,
    expiresAt,
    purpose: PURPOSE,
    ...bindings,
    qualityBoundary: { ...QUALITY_BOUNDARY },
  }
  const authorizationDir = resolve(safeAttemptDir, 'execution_authorizations')
  const authorizationPath = resolve(authorizationDir, `${safeTool.id}.json`)
  if (!underRoot(authorizationPath, safeAttemptDir)) {
    fail('NATIVE_AUTHORIZATION_PATH_ESCAPE', 'authorization path escaped the attempt')
  }
  assertNoReparsePath(authorizationDir, safeAttemptDir)
  mkdirSync(authorizationDir, { recursive: true })
  assertNoReparsePath(authorizationPath, safeAttemptDir)
  if (existsSync(authorizationPath)) {
    assertSingleLinkRegularFile(authorizationPath, 'native execution authorization')
    const existingContent = readFileSync(authorizationPath, 'utf8')
    let existing
    try {
      existing = JSON.parse(existingContent)
      validateNativeExecutionAuthorization({
        authorization: existing,
        task,
        planPath: safePlanPath,
        workerId: safeWorkerId,
        tool: safeTool,
        seedInventoryDigest: safeSeedDigest,
        attempt: safeAttempt,
        phases: safePhaseList,
        clock,
      })
    } catch (error) {
      const existingExpiredAt = Date.parse(String(existing?.expiresAt || ''))
      if (Number.isFinite(existingExpiredAt) && existingExpiredAt <= nowMs) {
        const content = `${JSON.stringify(authorization, null, 2)}\n`
        const currentContent = readFileSync(authorizationPath, 'utf8')
        if (currentContent !== existingContent) {
          fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT', 'authorization changed before expired replacement')
        }
        const replaced = atomicReplaceAuthorization(authorizationPath, content)
        assertSingleLinkRegularFile(authorizationPath, 'native execution authorization')
        return { ...replaced, authorizationPath, authorization, replacedExpired: true }
      }
      fail('NATIVE_AUTHORIZATION_IMMUTABLE_CONFLICT',
        `existing authorization cannot be reused: ${error?.code || error?.message || error}`)
    }
    return {
      created: false,
      sha256: sha256File(authorizationPath),
      authorizationPath,
      authorization: existing,
    }
  }
  const content = `${JSON.stringify(authorization, null, 2)}\n`
  const written = atomicWriteImmutable(authorizationPath, content)
  assertSingleLinkRegularFile(authorizationPath, 'native execution authorization')
  return { ...written, authorizationPath, authorization }
}
