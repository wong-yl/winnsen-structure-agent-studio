import { createHash, randomBytes } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fstatSync,
  fsyncSync,
  mkdirSync,
  openSync,
  readFileSync,
  readdirSync,
  renameSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')

export const NATIVE_TASK_STORE_SCHEMA = 'winnsen.native_task_store_record.v1'
export const NATIVE_TASK_INDEX_SCHEMA = 'winnsen.native_model_request_index.v2'
export const NATIVE_BUILD_SCHEMA = 'winnsen.native_solidworks_build_task.v2'
export const NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA = 'winnsen.native_task_checkpoint.v1'

const INDEX_FILE_NAME = 'native_model_request_index.json'
const LOCK_FILE_NAME = 'native_model_request_store.lock'
const TASK_ID_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/
const RESERVED_TASK_ID_STEMS = new Set([
  INDEX_FILE_NAME.replace(/\.json$/i, '').toLowerCase(),
])
const WINDOWS_DEVICE_TASK_ID_PATTERN = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i
const DEFAULT_LEASE_MS = 90_000
const DEFAULT_LOCK_TIMEOUT_MS = 5_000
const DEFAULT_LOCK_RETRY_MS = 25
const DEFAULT_LOCK_STALE_MS = 30_000
const DEFAULT_MAX_ATTEMPTS = 3
const NATIVE_CHECKPOINT_DESCRIPTOR_KEYS = Object.freeze([
  'schema',
  'taskId',
  'attempt',
  'planSha256',
  'planWorkerId',
  'completedPhase',
  'completedReceiptPath',
  'completedReceiptSha256',
  'completedEvidencePath',
  'completedEvidenceSha256',
  'currentInventoryDigest',
  'workingPackRelativePath',
  'prefixReceiptChainSha256',
  'checkpointAuditPath',
  'checkpointAuditSha256',
  'toolchainManifests',
])
const NATIVE_CHECKPOINT_TOOL_IDS = Object.freeze([
  'native_seed_pack_888x14_v1',
  'native_width_888_v1',
  'native_door_module_888x14_v1',
  'native_lock_topology_888x14_v1',
  'native_root_assembly_888x14_v1',
  'native_final_pack_888x14_v1',
])
const NATIVE_CHECKPOINT_PHASES = new Set(['assemblies', 'door_module_888x14'])

const STATE_TO_STATUS = Object.freeze({
  created: 'native_task_created',
  claimed: 'native_task_claimed',
  planning: 'native_source_planning',
  building: 'native_build_in_progress',
  validating: 'native_validation_in_progress',
  ready: 'native_assistance_model_ready',
  needs_input: 'native_task_needs_input',
  blocked: 'native_task_blocked',
  failed: 'native_task_failed',
  cancel_requested: 'native_task_cancel_requested',
  cancelled: 'native_task_cancelled',
})

const STATUS_TO_STATE = Object.freeze(
  Object.fromEntries(Object.entries(STATE_TO_STATUS).map(([state, status]) => [status, state])),
)

const ALLOWED_TRANSITIONS = Object.freeze({
  claimed: new Set(['planning', 'needs_input', 'blocked', 'failed', 'cancelled']),
  planning: new Set(['building', 'needs_input', 'blocked', 'failed', 'cancelled']),
  building: new Set(['validating', 'needs_input', 'blocked', 'failed', 'cancelled']),
  validating: new Set(['ready', 'needs_input', 'blocked', 'failed', 'cancelled']),
  cancel_requested: new Set(['cancelled']),
})

const LEASED_STATES = new Set(['claimed', 'planning', 'building', 'validating', 'cancel_requested'])
export const DEFAULT_ACTIVE_NATIVE_TASK_STATES = Object.freeze([
  'created',
  'claimed',
  'planning',
  'building',
  'validating',
  'needs_input',
  'blocked',
  'cancel_requested',
])
const ARCHIVABLE_STATES = new Set(['ready', 'needs_input', 'blocked', 'failed', 'cancelled'])
const PATCHABLE_NATIVE_BUILD_FIELDS = new Set([
  'planArtifact',
  'blocker',
  'execution',
  'validation',
  'progress',
  'message',
  'checkpoint',
  'checkpointDigest',
])

export class NativeTaskStoreError extends Error {
  constructor(code, message, statusCode = 409, details = null) {
    super(message)
    this.name = 'NativeTaskStoreError'
    this.code = code
    this.statusCode = statusCode
    this.details = details
  }
}

function fail(code, message, statusCode = 409, details = null) {
  throw new NativeTaskStoreError(code, message, statusCode, details)
}

function cloneJson(value, label = 'value') {
  try {
    const encoded = JSON.stringify(value)
    if (encoded === undefined) fail('INVALID_JSON_VALUE', `${label} must be JSON serializable`, 422)
    return JSON.parse(encoded)
  } catch (error) {
    if (error instanceof NativeTaskStoreError) throw error
    fail('INVALID_JSON_VALUE', `${label} must be JSON serializable`, 422)
  }
}

function stableJson(value) {
  if (Array.isArray(value)) return `[${value.map(stableJson).join(',')}]`
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${stableJson(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

function digest(value) {
  return createHash('sha256').update(stableJson(value)).digest('hex')
}

function exactKeys(value, expectedKeys) {
  const actual = Object.keys(value).sort()
  const expected = [...expectedKeys].sort()
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

function normalizeCheckpointDescriptor(value, label = 'checkpoint') {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    fail('INVALID_CHECKPOINT_DESCRIPTOR', `${label} must be a JSON object`, 422)
  }
  const cloned = cloneJson(value, label)
  if (!exactKeys(cloned, NATIVE_CHECKPOINT_DESCRIPTOR_KEYS)) {
    fail(
      'CHECKPOINT_DESCRIPTOR_FIELDS_INVALID',
      `${label} does not contain the exact native checkpoint descriptor fields`,
      422,
    )
  }
  if (cloned.schema !== NATIVE_CHECKPOINT_DESCRIPTOR_SCHEMA) {
    fail('CHECKPOINT_DESCRIPTOR_SCHEMA_INVALID', `${label}.schema is not the trusted checkpoint schema`, 422)
  }
  if (typeof cloned.taskId !== 'string' || !TASK_ID_PATTERN.test(cloned.taskId)) {
    fail('CHECKPOINT_DESCRIPTOR_TASK_ID_INVALID', `${label}.taskId is not a valid native task id`, 422)
  }
  if (!Number.isInteger(cloned.attempt) || cloned.attempt < 0) {
    fail('CHECKPOINT_DESCRIPTOR_ATTEMPT_INVALID', `${label}.attempt must be a non-negative integer`, 422)
  }
  if (typeof cloned.planSha256 !== 'string' || !/^[A-Fa-f0-9]{64}$/.test(cloned.planSha256)) {
    fail('CHECKPOINT_DESCRIPTOR_PLAN_INVALID', `${label}.planSha256 must be a SHA-256 digest`, 422)
  }
  if (typeof cloned.planWorkerId !== 'string' || !cloned.planWorkerId.trim() || cloned.planWorkerId.length > 200) {
    fail('CHECKPOINT_DESCRIPTOR_WORKER_INVALID', `${label}.planWorkerId must be a non-empty string of at most 200 characters`, 422)
  }
  if (!NATIVE_CHECKPOINT_PHASES.has(cloned.completedPhase)) {
    fail('CHECKPOINT_DESCRIPTOR_PHASE_INVALID', `${label}.completedPhase is not an approved checkpoint phase`, 422)
  }
  const safeRelativePath = (value, field, { allowEmpty = false } = {}) => {
    const text = String(value ?? '')
    if (allowEmpty && text === '') return ''
    const normalized = text.replaceAll('\\', '/')
    if (!normalized || normalized !== text || normalized.startsWith('/') ||
        normalized.split('/').some((segment) => !segment || segment === '.' || segment === '..')) {
      fail('CHECKPOINT_DESCRIPTOR_PATH_INVALID', `${label}.${field} must be a normalized attempt-relative path`, 422)
    }
    return normalized
  }
  const safeDigest = (value, field, { allowEmpty = false } = {}) => {
    const text = String(value ?? '')
    if (allowEmpty && text === '') return ''
    if (!/^[A-Fa-f0-9]{64}$/.test(text)) {
      fail('CHECKPOINT_DESCRIPTOR_DIGEST_INVALID', `${label}.${field} must be a SHA-256 digest`, 422)
    }
    return text.toUpperCase()
  }
  const completedReceiptPath = safeRelativePath(cloned.completedReceiptPath, 'completedReceiptPath')
  const completedEvidencePath = safeRelativePath(cloned.completedEvidencePath, 'completedEvidencePath')
  const workingPackRelativePath = safeRelativePath(cloned.workingPackRelativePath, 'workingPackRelativePath')
  if (workingPackRelativePath !== 'native_cad/working_pack') {
    fail('CHECKPOINT_DESCRIPTOR_WORKING_PACK_INVALID', `${label}.workingPackRelativePath must be native_cad/working_pack`, 422)
  }
  const checkpointAuditPath = safeRelativePath(cloned.checkpointAuditPath, 'checkpointAuditPath', { allowEmpty: true })
  const checkpointAuditSha256 = safeDigest(cloned.checkpointAuditSha256, 'checkpointAuditSha256', { allowEmpty: true })
  if (Boolean(checkpointAuditPath) !== Boolean(checkpointAuditSha256)) {
    fail('CHECKPOINT_DESCRIPTOR_AUDIT_INVALID', `${label} checkpoint audit path and digest must be present together`, 422)
  }
  if (!cloned.toolchainManifests || typeof cloned.toolchainManifests !== 'object' ||
      Array.isArray(cloned.toolchainManifests) ||
      !exactKeys(cloned.toolchainManifests, NATIVE_CHECKPOINT_TOOL_IDS)) {
    fail('CHECKPOINT_DESCRIPTOR_TOOLCHAIN_INVALID', `${label}.toolchainManifests must contain the exact six native tools`, 422)
  }
  const toolchainManifests = Object.fromEntries(NATIVE_CHECKPOINT_TOOL_IDS.map((toolId) => [
    toolId,
    safeDigest(cloned.toolchainManifests[toolId], `toolchainManifests.${toolId}`),
  ]))
  return {
    schema: cloned.schema,
    taskId: cloned.taskId,
    attempt: cloned.attempt,
    planSha256: cloned.planSha256.toUpperCase(),
    planWorkerId: cloned.planWorkerId.trim(),
    completedPhase: cloned.completedPhase,
    completedReceiptPath,
    completedReceiptSha256: safeDigest(cloned.completedReceiptSha256, 'completedReceiptSha256'),
    completedEvidencePath,
    completedEvidenceSha256: safeDigest(cloned.completedEvidenceSha256, 'completedEvidenceSha256'),
    currentInventoryDigest: safeDigest(cloned.currentInventoryDigest, 'currentInventoryDigest'),
    workingPackRelativePath,
    prefixReceiptChainSha256: safeDigest(cloned.prefixReceiptChainSha256, 'prefixReceiptChainSha256'),
    checkpointAuditPath,
    checkpointAuditSha256,
    toolchainManifests,
  }
}

export function nativeCheckpointDescriptorDigest(checkpoint) {
  return digest(normalizeCheckpointDescriptor(checkpoint)).toUpperCase()
}

function randomToken(bytes = 16) {
  return randomBytes(bytes).toString('hex')
}

function asTimestamp(value, fallback = Date.now()) {
  const milliseconds = value instanceof Date
    ? value.getTime()
    : typeof value === 'number'
      ? value
      : typeof value === 'string' && value.trim()
        ? Date.parse(value)
        : Number.NaN
  const resolved = Number.isFinite(milliseconds) ? milliseconds : fallback
  return new Date(resolved).toISOString()
}

function asMilliseconds(value, fallback = Date.now()) {
  if (value instanceof Date) return value.getTime()
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim()) {
    const parsed = Date.parse(value)
    if (Number.isFinite(parsed)) return parsed
  }
  return fallback
}

function positiveInteger(value, fallback, minimum = 1) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed >= minimum ? parsed : fallback
}

function validatedTaskId(value) {
  const taskId = String(value || '').trim()
  const normalizedTaskId = taskId.toLowerCase()
  if (!TASK_ID_PATTERN.test(taskId) ||
      RESERVED_TASK_ID_STEMS.has(normalizedTaskId) ||
      WINDOWS_DEVICE_TASK_ID_PATTERN.test(taskId) ||
      `${normalizedTaskId}.json` === INDEX_FILE_NAME.toLowerCase() ||
      `${normalizedTaskId}.json` === LOCK_FILE_NAME.toLowerCase()) {
    fail('INVALID_TASK_ID', 'task id must use only letters, digits, dot, underscore, or hyphen', 422)
  }
  return taskId
}

function taskPathFor(dataDir, taskId) {
  const safeId = validatedTaskId(taskId)
  const path = resolve(dataDir, `${safeId}.json`)
  if (dirname(path) !== dataDir) fail('TASK_PATH_ESCAPE', 'task path escapes native task directory', 422)
  return path
}

function atomicWriteJson(path, value) {
  mkdirSync(dirname(path), { recursive: true })
  const temporary = `${path}.tmp-${process.pid}-${randomToken(6)}`
  let descriptor = null
  try {
    descriptor = openSync(temporary, 'wx')
    writeFileSync(descriptor, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    renameSync(temporary, path)
  } finally {
    if (descriptor !== null) {
      try { closeSync(descriptor) } catch {}
    }
    if (existsSync(temporary)) rmSync(temporary, { force: true })
  }
}

function readJsonObject(path, label) {
  let parsed
  try {
    parsed = JSON.parse(readFileSync(path, 'utf8'))
  } catch (error) {
    fail('CORRUPT_JSON', `${label} is not valid JSON: ${basename(path)}`, 500, {
      cause: error instanceof Error ? error.message : String(error),
    })
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    fail('CORRUPT_JSON', `${label} must contain a JSON object: ${basename(path)}`, 500)
  }
  return parsed
}

function inferredState(task) {
  const nativeState = String(task?.nativeBuild?.state || '').trim().toLowerCase()
  if (Object.hasOwn(STATE_TO_STATUS, nativeState)) return nativeState
  return STATUS_TO_STATE[String(task?.status || '').trim().toLowerCase()] || 'unknown'
}

function normalizedHistory(value) {
  return Array.isArray(value)
    ? value.filter((entry) => entry && typeof entry === 'object' && !Array.isArray(entry)).map((entry) => cloneJson(entry))
    : []
}

function normalizeTaskRecord(input, { fallbackCreatedAt = Date.now() } = {}) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) {
    fail('INVALID_TASK', 'task must be a JSON object', 422)
  }
  const task = cloneJson(input, 'task')
  task.id = validatedTaskId(task.id)
  const createdAt = asTimestamp(task.createdAt, fallbackCreatedAt)
  const state = inferredState(task)
  const nativeBuild = task.nativeBuild && typeof task.nativeBuild === 'object' && !Array.isArray(task.nativeBuild)
    ? cloneJson(task.nativeBuild)
    : {}
  const revision = Number.isInteger(Number(task.revision)) && Number(task.revision) >= 0
    ? Number(task.revision)
    : 0

  task.createdAt = createdAt
  task.updatedAt = asTimestamp(task.updatedAt, Date.parse(createdAt))
  task.revision = revision
  task.taskStoreSchema = NATIVE_TASK_STORE_SCHEMA
  task.storageMode = 'task_file_source'
  task.nativeBuild = {
    ...nativeBuild,
    schema: NATIVE_BUILD_SCHEMA,
    state,
    attempt: Number.isInteger(Number(nativeBuild.attempt)) && Number(nativeBuild.attempt) >= 0
      ? Number(nativeBuild.attempt)
      : 0,
    lease: nativeBuild.lease && typeof nativeBuild.lease === 'object' && !Array.isArray(nativeBuild.lease)
      ? cloneJson(nativeBuild.lease)
      : null,
    lastTransitionId: nativeBuild.lastTransitionId ? String(nativeBuild.lastTransitionId) : null,
    statusHistory: normalizedHistory(nativeBuild.statusHistory),
  }
  if (Object.hasOwn(STATE_TO_STATUS, state)) task.status = STATE_TO_STATUS[state]
  return task
}

function requestIdentity(task) {
  return {
    username: String(task.username || ''),
    purpose: String(task.purpose || ''),
    taskType: String(task.taskType || ''),
    requestFingerprint: String(task.requestFingerprint || ''),
    customerParameterFlow: String(task.customerParameterFlow || ''),
    customerRequirementReference: String(task.customerRequirementReference || ''),
    widthInputMode: String(task.widthInputMode || ''),
    requestedWidthSemantics: String(task.requestedWidthSemantics || ''),
    generatorId: String(task.generatorId || ''),
    candidateRecipeId: String(task.candidateRecipeId || ''),
    candidateSourceSeedIds: task.candidateSourceSeedIds || [],
    requestedWidthMm: task.requestedWidthMm ?? null,
    cabinetWidth: task.cabinetWidth ?? task.cabinetWidthMm ?? null,
    cabinetHeight: task.cabinetHeight ?? task.cabinetHeightMm ?? null,
    cabinetDepth: task.cabinetDepth ?? task.cabinetDepthMm ?? null,
    columns: task.columns ?? null,
    doorCount: task.doorCount ?? null,
    doorWidth: task.doorWidth ?? task.estimatedDoorPanelWidthMm ?? null,
    columnDoorCounts: task.columnDoorCounts || [],
    rowSequence: String(task.rowSequence || ''),
    ...(task.parametricRequest ? { parametricRequest: task.parametricRequest } : {}),
    doorType: String(task.doorType || ''),
    lockType: String(task.lockType || ''),
    hingeType: String(task.hingeType || ''),
    latchType: String(task.latchType || ''),
    reinforcement: String(task.reinforcement || ''),
    openings: String(task.openings || ''),
    material: String(task.material || ''),
    thickness: String(task.thickness || ''),
    validationRequired: task.validationRequired || [],
  }
}

function ensureSameImportedTask(existing, incoming) {
  if (digest(requestIdentity(existing)) !== digest(requestIdentity(incoming))) {
    fail('TASK_ID_COLLISION', `task id already exists with a different immutable request: ${existing.id}`)
  }
}

function immutableRequestDigest(task) {
  return digest(requestIdentity(task))
}

function normalizedActiveStates(activeStates) {
  const values = activeStates === undefined ? DEFAULT_ACTIVE_NATIVE_TASK_STATES : activeStates
  if (!Array.isArray(values) && !(values instanceof Set)) {
    fail('INVALID_ACTIVE_STATES', 'activeStates must be an array or Set of native task states', 422)
  }
  const result = new Set([...values].map((value) => String(value || '').trim().toLowerCase()))
  for (const state of result) {
    if (!Object.hasOwn(STATE_TO_STATUS, state)) fail('UNKNOWN_TASK_STATE', `unknown native task state: ${state}`, 422)
  }
  return result
}

function indexPayload(records) {
  return {
    schema: NATIVE_TASK_INDEX_SCHEMA,
    rebuiltAt: new Date().toISOString(),
    requests: [...records].sort((left, right) =>
      String(right.createdAt || '').localeCompare(String(left.createdAt || '')) ||
      String(left.id).localeCompare(String(right.id)),
    ),
  }
}

function scanTaskFilesUnlocked(dataDir) {
  mkdirSync(dataDir, { recursive: true })
  return readdirSync(dataDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.json') && entry.name !== INDEX_FILE_NAME)
    .map((entry) => {
      const path = resolve(dataDir, entry.name)
      const raw = readJsonObject(path, 'native task detail')
      const expectedId = entry.name.slice(0, -'.json'.length)
      if (String(raw.id || '') !== expectedId) {
        fail('TASK_FILE_ID_MISMATCH', `task detail id does not match file name: ${entry.name}`, 500)
      }
      const stats = statSync(path)
      return normalizeTaskRecord(raw, { fallbackCreatedAt: stats.birthtimeMs || stats.mtimeMs })
    })
}

function archiveCorruptIndexUnlocked(indexPath) {
  if (!existsSync(indexPath)) return
  try {
    const current = JSON.parse(readFileSync(indexPath, 'utf8'))
    if (current && typeof current === 'object' && Array.isArray(current.requests)) return
  } catch {}
  renameSync(indexPath, `${indexPath}.corrupt-${Date.now()}-${randomToken(4)}`)
}

function rebuildIndexUnlocked(dataDir, indexPath) {
  const records = scanTaskFilesUnlocked(dataDir)
  archiveCorruptIndexUnlocked(indexPath)
  atomicWriteJson(indexPath, indexPayload(records))
  return records
}

function writeTaskUnlocked(dataDir, task) {
  const normalized = normalizeTaskRecord(task)
  atomicWriteJson(taskPathFor(dataDir, normalized.id), normalized)
  return normalized
}

function readTaskUnlocked(dataDir, taskId) {
  const path = taskPathFor(dataDir, taskId)
  if (!existsSync(path)) fail('TASK_NOT_FOUND', 'native task not found', 404)
  const raw = readJsonObject(path, 'native task detail')
  if (String(raw.id || '') !== validatedTaskId(taskId)) {
    fail('TASK_FILE_ID_MISMATCH', `task detail id does not match file name: ${basename(path)}`, 500)
  }
  const stats = statSync(path)
  return normalizeTaskRecord(raw, { fallbackCreatedAt: stats.birthtimeMs || stats.mtimeMs })
}

function defaultProcessProbe(pid) {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error && error.code === 'ESRCH') return false
    if (error && error.code === 'EPERM') return true
    return null
  }
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds))
}

function lockOwnerPayload(token) {
  return {
    schema: 'winnsen.native_task_store_lock.v1',
    token,
    pid: process.pid,
    processStartedAt: new Date(Date.now() - Math.floor(process.uptime() * 1000)).toISOString(),
    createdAt: new Date().toISOString(),
  }
}

function sameLockOwner(lockPath, expectedToken) {
  try {
    const owner = JSON.parse(readFileSync(lockPath, 'utf8'))
    return owner && owner.token === expectedToken
  } catch {
    return false
  }
}

async function acquireLock({ lockPath, lockTimeoutMs, lockRetryMs, lockStaleMs, processProbe }) {
  mkdirSync(dirname(lockPath), { recursive: true })
  const startedAt = Date.now()
  const token = randomToken()
  let lastReason = 'lock is already held'

  while (true) {
    let descriptor = null
    try {
      descriptor = openSync(lockPath, 'wx')
      const owner = lockOwnerPayload(token)
      writeFileSync(descriptor, `${JSON.stringify(owner)}\n`, 'utf8')
      fsyncSync(descriptor)
      closeSync(descriptor)
      return owner
    } catch (error) {
      if (descriptor !== null) {
        try { closeSync(descriptor) } catch {}
      }
      if (!error || error.code !== 'EEXIST') throw error

      let stats = null
      let owner = null
      try {
        stats = statSync(lockPath)
        owner = JSON.parse(readFileSync(lockPath, 'utf8'))
      } catch {
        lastReason = 'lock owner is unreadable; refusing stale removal'
      }

      if (stats && owner && typeof owner === 'object') {
        const ageMs = Math.max(0, Date.now() - stats.mtimeMs)
        if (ageMs > lockStaleMs) {
          const ownerPid = Number(owner.pid)
          if (!Number.isInteger(ownerPid) || ownerPid <= 0 || !String(owner.token || '')) {
            lastReason = 'stale lock owner metadata is invalid; refusing removal'
          } else {
            let ownerAlive = null
            try {
              ownerAlive = await processProbe(ownerPid, owner)
            } catch {
              ownerAlive = null
            }
            if (ownerAlive === false) {
              if (sameLockOwner(lockPath, owner.token)) {
                rmSync(lockPath, { force: true })
                continue
              }
              lastReason = 'lock owner changed during stale verification'
            } else if (ownerAlive === true) {
              lastReason = `stale-age lock is still owned by live pid ${ownerPid}`
            } else {
              lastReason = `could not verify stale lock owner pid ${ownerPid}; refusing removal`
            }
          }
        }
      }

      if (Date.now() - startedAt >= lockTimeoutMs) {
        fail('STORE_LOCK_TIMEOUT', `native task store lock timed out: ${lastReason}`, 503)
      }
      await delay(Math.min(lockRetryMs, Math.max(1, lockTimeoutMs - (Date.now() - startedAt))))
    }
  }
}

function releaseLock(lockPath, owner) {
  if (!existsSync(lockPath)) {
    fail('STORE_LOCK_LOST', 'native task store lock disappeared before release', 503)
  }
  if (!sameLockOwner(lockPath, owner.token)) {
    fail('STORE_LOCK_LOST', 'native task store lock ownership changed before release', 503)
  }
  rmSync(lockPath, { force: true })
}

async function withLock(config, action) {
  const owner = await acquireLock(config)
  let result
  let actionError = null
  try {
    result = await action()
  } catch (error) {
    actionError = error
  }
  let releaseError = null
  try {
    releaseLock(config.lockPath, owner)
  } catch (error) {
    releaseError = error
  }
  if (actionError) throw actionError
  if (releaseError) throw releaseError
  return result
}

function requireRevision(task, expectedRevision) {
  if (!Number.isInteger(Number(expectedRevision)) || Number(expectedRevision) < 0) {
    fail('EXPECTED_REVISION_REQUIRED', 'expectedRevision must be a non-negative integer', 422)
  }
  if (task.revision !== Number(expectedRevision)) {
    fail('REVISION_CONFLICT', `expected revision ${expectedRevision}, current revision is ${task.revision}`, 409, {
      expectedRevision: Number(expectedRevision),
      currentRevision: task.revision,
    })
  }
}

function requireTransitionId(value) {
  const transitionId = String(value || '').trim()
  if (!transitionId || transitionId.length > 200) {
    fail('TRANSITION_ID_REQUIRED', 'transitionId must be a non-empty string of at most 200 characters', 422)
  }
  return transitionId
}

function idempotentTransition(task, transitionId, operation, toState, payloadDigest) {
  const prior = [...task.nativeBuild.statusHistory].reverse().find((entry) => entry.transitionId === transitionId)
  if (!prior) return null
  if (prior.operation !== operation || prior.to !== toState || prior.payloadDigest !== payloadDigest) {
    fail('TRANSITION_ID_REUSED', `transitionId was already used for a different mutation: ${transitionId}`)
  }
  return task
}

function requireLease(task, leaseId, workerId, nowMs) {
  const lease = task.nativeBuild.lease
  if (!lease || String(lease.id || '') !== String(leaseId || '')) {
    fail('LEASE_MISMATCH', 'native task lease does not match', 409)
  }
  if (workerId && String(lease.workerId || '') !== String(workerId)) {
    fail('LEASE_WORKER_MISMATCH', 'native task lease belongs to another worker', 409)
  }
  if (!LEASED_STATES.has(task.nativeBuild.state)) {
    fail('TASK_NOT_LEASED', `task state does not accept worker mutations: ${task.nativeBuild.state}`)
  }
  if (asMilliseconds(lease.expiresAt, 0) <= nowMs) {
    fail('LEASE_EXPIRED', 'native task lease has expired', 409)
  }
  return lease
}

function normalizeNativeBuildPatch(patch) {
  if (patch === undefined || patch === null) return {}
  if (!patch || typeof patch !== 'object' || Array.isArray(patch)) {
    fail('INVALID_TRANSITION_PATCH', 'transition patch must be a JSON object', 422)
  }
  const cloned = cloneJson(patch, 'transition patch')
  const candidate = Object.keys(cloned).length === 1 && cloned.nativeBuild && typeof cloned.nativeBuild === 'object'
    ? cloned.nativeBuild
    : cloned
  for (const key of Object.keys(candidate)) {
    if (!PATCHABLE_NATIVE_BUILD_FIELDS.has(key)) {
      fail('PROTECTED_TRANSITION_FIELD', `transition patch cannot update nativeBuild.${key}`, 422)
    }
  }
  if (Object.hasOwn(candidate, 'checkpoint') || Object.hasOwn(candidate, 'checkpointDigest')) {
    if (!Object.hasOwn(candidate, 'checkpoint') || !Object.hasOwn(candidate, 'checkpointDigest')) {
      fail('CHECKPOINT_PATCH_INCOMPLETE', 'transition checkpoint and checkpointDigest must be updated together', 422)
    }
    const normalizedCheckpoint = normalizeCheckpointDescriptor(candidate.checkpoint, 'transition checkpoint')
    const checkpointDigest = String(candidate.checkpointDigest || '').toUpperCase()
    if (!/^[A-F0-9]{64}$/.test(checkpointDigest) ||
        nativeCheckpointDescriptorDigest(normalizedCheckpoint) !== checkpointDigest) {
      fail('CHECKPOINT_PATCH_DIGEST_MISMATCH', 'transition checkpoint digest does not match its descriptor', 422)
    }
    candidate.checkpoint = normalizedCheckpoint
    candidate.checkpointDigest = checkpointDigest
  }
  return candidate
}

function renewedLease(lease, nowMs) {
  const durationMs = positiveInteger(lease.durationMs, DEFAULT_LEASE_MS, 100)
  return {
    ...lease,
    heartbeatAt: new Date(nowMs).toISOString(),
    expiresAt: new Date(nowMs + durationMs).toISOString(),
    durationMs,
  }
}

function appendTransition(task, { transitionId, operation, fromState, toState, payloadDigest, at, leaseId }) {
  task.nativeBuild.statusHistory.push({
    state: toState,
    from: fromState,
    to: toState,
    at,
    transitionId,
    operation,
    payloadDigest,
    leaseId: leaseId || null,
  })
  task.nativeBuild.lastTransitionId = transitionId
}

function setTaskState(task, toState) {
  task.nativeBuild.state = toState
  task.status = STATE_TO_STATUS[toState]
  if (['planning', 'building', 'validating', 'ready'].includes(toState)) task.nativeBuild.executionStarted = true
  if (toState === 'ready') task.nativeBuild.modelReady = true
  if (!LEASED_STATES.has(toState)) task.nativeBuild.lease = null
}

function bumpRevision(task, nowMs) {
  task.revision += 1
  task.updatedAt = new Date(nowMs).toISOString()
  return task
}

function isClaimableTask(task) {
  return task.storageMode === 'task_file_source' &&
    task.taskType === 'native_solidworks_build_task' &&
    task.status === 'native_task_created' &&
    task.nativeBuild.state === 'created' &&
    !task.nativeBuild.lease &&
    task.modelReady !== true &&
    task.legacyFallbackUsed !== true &&
    task.nativeBuild.legacyFallbackUsed !== true
}

function isLeaseExpired(task, nowMs) {
  const lease = task.nativeBuild.lease
  return LEASED_STATES.has(task.nativeBuild.state) &&
    lease &&
    asMilliseconds(lease.expiresAt, Number.POSITIVE_INFINITY) <= nowMs
}

async function recoverExpiredLeasesUnlocked(dataDir, tasks, nowMs, maxAttempts, processProbe) {
  const at = new Date(nowMs).toISOString()
  for (const task of tasks) {
    if (!isLeaseExpired(task, nowMs)) continue
    const fromState = task.nativeBuild.state
    const expiredLease = task.nativeBuild.lease
    const expiredLeaseId = String(expiredLease?.id || '')
    const ownerPid = Number(expiredLease?.workerPid)
    if (!Number.isInteger(ownerPid) || ownerPid <= 0) continue
    let ownerAlive = null
    try {
      ownerAlive = await processProbe(ownerPid, expiredLease, task)
    } catch {
      ownerAlive = null
    }
    if (ownerAlive !== false) continue
    const transitionId = `lease-expired:${expiredLeaseId || task.revision}`
    const attempt = positiveInteger(task.nativeBuild.attempt, 0, 0)
    let toState
    let reason
    if (fromState === 'cancel_requested') {
      toState = 'cancelled'
      reason = 'cancel_requested_lease_expired'
    } else if (attempt >= maxAttempts) {
      toState = 'failed'
      reason = 'lease_expired_max_attempts'
      task.nativeBuild.error = {
        code: 'LEASE_EXPIRED_MAX_ATTEMPTS',
        message: `Native task lease expired after ${attempt} attempts.`,
        retryable: false,
        at,
      }
    } else {
      toState = 'created'
      reason = 'lease_expired_requeued'
      task.nativeBuild.error = {
        code: 'LEASE_EXPIRED_REQUEUED',
        message: `Native task lease expired during ${fromState}; task was safely requeued.`,
        retryable: true,
        at,
      }
    }
    task.nativeBuild.lease = null
    task.nativeBuild.modelReady = false
    setTaskState(task, toState)
    appendTransition(task, {
      transitionId,
      operation: 'lease_recovery',
      fromState,
      toState,
      payloadDigest: digest({ expiredLeaseId, attempt, reason }),
      at,
      leaseId: expiredLeaseId || null,
    })
    bumpRevision(task, nowMs)
    writeTaskUnlocked(dataDir, task)
  }
  return tasks
}

function assertAllowedTransition(task, toState) {
  if (!Object.hasOwn(STATE_TO_STATUS, toState)) {
    fail('UNKNOWN_TASK_STATE', `unknown native task state: ${toState}`, 422)
  }
  const allowed = ALLOWED_TRANSITIONS[task.nativeBuild.state]
  if (!allowed || !allowed.has(toState)) {
    fail('INVALID_TRANSITION', `cannot transition native task from ${task.nativeBuild.state} to ${toState}`)
  }
}

function mutationNow(clock, suppliedNow) {
  return asMilliseconds(suppliedNow, asMilliseconds(clock(), Date.now()))
}

function requiredMutationNow(clock, suppliedNow) {
  if (suppliedNow === undefined || suppliedNow === null) {
    fail('NOW_REQUIRED', 'now is required for checkpoint resume', 422)
  }
  const nowMs = asMilliseconds(suppliedNow, Number.NaN)
  if (!Number.isFinite(nowMs)) fail('NOW_INVALID', 'now must be a valid timestamp', 422)
  return nowMs
}

export function createNativeTaskStore(options = {}) {
  const dataDir = resolve(options.dataDir || resolve(ROOT, 'data/native_model_requests'))
  const indexPath = resolve(options.indexPath || resolve(dataDir, INDEX_FILE_NAME))
  const lockPath = resolve(options.lockPath || resolve(dataDir, LOCK_FILE_NAME))
  if (dirname(indexPath) !== dataDir || dirname(lockPath) !== dataDir) {
    fail('STORE_PATH_ESCAPE', 'indexPath and lockPath must be direct children of dataDir', 422)
  }
  const clock = typeof options.clock === 'function' ? options.clock : () => Date.now()
  const processProbe = typeof options.processProbe === 'function'
    ? options.processProbe
    : typeof options.workerProcessProbe === 'function'
      ? options.workerProcessProbe
      : defaultProcessProbe
  const leaseMsDefault = positiveInteger(options.leaseMs, DEFAULT_LEASE_MS, 100)
  const maxAttempts = positiveInteger(options.maxAttempts, DEFAULT_MAX_ATTEMPTS, 1)
  const lockConfig = {
    lockPath,
    lockTimeoutMs: positiveInteger(options.lockTimeoutMs, DEFAULT_LOCK_TIMEOUT_MS, 10),
    lockRetryMs: positiveInteger(options.lockRetryMs, DEFAULT_LOCK_RETRY_MS, 1),
    lockStaleMs: positiveInteger(options.lockStaleMs, DEFAULT_LOCK_STALE_MS, 1),
    processProbe,
  }

  const rebuildIndexBestEffort = () => {
    try { return rebuildIndexUnlocked(dataDir, indexPath) } catch { return null }
  }

  async function list() {
    return withLock(lockConfig, async () => {
      const records = rebuildIndexUnlocked(dataDir, indexPath)
      return cloneJson(indexPayload(records).requests)
    })
  }

  async function listSnapshot() {
    const records = scanTaskFilesUnlocked(dataDir)
      .sort((left, right) =>
        String(right.createdAt || '').localeCompare(String(left.createdAt || '')) ||
        String(left.id).localeCompare(String(right.id)),
      )
    return cloneJson(records)
  }

  async function read(taskId) {
    return cloneJson(readTaskUnlocked(dataDir, taskId))
  }

  async function createOrImport(input) {
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock)
      const incoming = normalizeTaskRecord(input, { fallbackCreatedAt: nowMs })
      const path = taskPathFor(dataDir, incoming.id)
      if (existsSync(path)) {
        const existing = readTaskUnlocked(dataDir, incoming.id)
        ensureSameImportedTask(existing, incoming)
        rebuildIndexBestEffort()
        return cloneJson(existing)
      }
      incoming.updatedAt = incoming.createdAt
      const saved = writeTaskUnlocked(dataDir, incoming)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function createOrReuseByFingerprint(input, { username, activeStates } = {}) {
    const scopedUsername = String(username === undefined ? input?.username || '' : username).trim()
    if (!scopedUsername) fail('USERNAME_REQUIRED', 'username is required for fingerprint deduplication', 422)
    const active = normalizedActiveStates(activeStates)
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock)
      const incoming = normalizeTaskRecord({ ...cloneJson(input, 'task'), username: scopedUsername }, {
        fallbackCreatedAt: nowMs,
      })
      const fingerprint = String(incoming.requestFingerprint || '').trim()
      if (!fingerprint) fail('REQUEST_FINGERPRINT_REQUIRED', 'requestFingerprint is required for task deduplication', 422)
      const matches = scanTaskFilesUnlocked(dataDir)
        .filter((task) =>
          String(task.username || '') === scopedUsername &&
          String(task.requestFingerprint || '') === fingerprint &&
          active.has(task.nativeBuild.state) &&
          task.nativeBuild.archived !== true
        )
        .sort((left, right) =>
          String(left.createdAt || '').localeCompare(String(right.createdAt || '')) ||
          String(left.id).localeCompare(String(right.id)),
        )
      if (matches.length) {
        const incomingDigest = immutableRequestDigest(incoming)
        for (const existing of matches) {
          if (immutableRequestDigest(existing) !== incomingDigest) {
            fail(
              'REQUEST_FINGERPRINT_COLLISION',
              'active task uses the same fingerprint with a different immutable request',
              409,
              { taskId: existing.id, requestFingerprint: fingerprint },
            )
          }
        }
        rebuildIndexBestEffort()
        return { task: cloneJson(matches[0]), created: false }
      }
      const path = taskPathFor(dataDir, incoming.id)
      if (existsSync(path)) {
        const existing = readTaskUnlocked(dataDir, incoming.id)
        ensureSameImportedTask(existing, incoming)
        rebuildIndexBestEffort()
        return { task: cloneJson(existing), created: false }
      }
      incoming.updatedAt = incoming.createdAt
      const saved = writeTaskUnlocked(dataDir, incoming)
      rebuildIndexBestEffort()
      return { task: cloneJson(saved), created: true }
    })
  }

  async function claimMatching({ workerId, workerPid = process.pid, leaseMs, now, taskId } = {}) {
    const normalizedWorkerId = String(workerId || '').trim()
    if (!normalizedWorkerId || normalizedWorkerId.length > 200) {
      fail('WORKER_ID_REQUIRED', 'workerId must be a non-empty string of at most 200 characters', 422)
    }
    const normalizedWorkerPid = positiveInteger(workerPid, process.pid, 1)
    const durationMs = positiveInteger(leaseMs, leaseMsDefault, 100)
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const pool = taskId ? [readTaskUnlocked(dataDir, validatedTaskId(taskId))] : scanTaskFilesUnlocked(dataDir).filter(task => !task.parametricRequest)
      if(taskId && !pool[0].parametricRequest) fail('PARAMETRIC_REQUEST_REQUIRED', 'exact parametric claim refuses legacy tasks', 422)
      const tasks = await recoverExpiredLeasesUnlocked(
        dataDir,
        pool,
        nowMs,
        maxAttempts,
        processProbe,
      )
      const candidates = tasks
        .filter(isClaimableTask)
        .sort((left, right) =>
          String(left.createdAt).localeCompare(String(right.createdAt)) || String(left.id).localeCompare(String(right.id)),
        )
      if (!candidates.length) {
        rebuildIndexBestEffort()
        return null
      }
      const task = candidates[0]
      const leaseId = randomToken()
      const at = new Date(nowMs).toISOString()
      const payloadDigest = digest({ workerId: normalizedWorkerId, attempt: task.nativeBuild.attempt + 1 })
      task.nativeBuild.attempt += 1
      task.nativeBuild.error = null
      task.nativeBuild.lease = {
        id: leaseId,
        workerId: normalizedWorkerId,
        workerPid: normalizedWorkerPid,
        claimedAt: at,
        heartbeatAt: at,
        expiresAt: new Date(nowMs + durationMs).toISOString(),
        durationMs,
      }
      setTaskState(task, 'claimed')
      appendTransition(task, {
        transitionId: `claim:${leaseId}`,
        operation: 'claim',
        fromState: 'created',
        toState: 'claimed',
        payloadDigest,
        at,
        leaseId,
      })
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function claimNext(args = {}) { return claimMatching({ ...args, taskId: undefined }) }
  async function claimTask(args = {}) {
    validatedTaskId(args.taskId)
    return claimMatching(args)
  }

  async function resumeCancelledFromCheckpoint({
    taskId,
    expectedRevision,
    transitionId: suppliedTransitionId,
    workerId,
    workerPid = process.pid,
    leaseMs,
    checkpoint,
    checkpointDigest,
    now,
  } = {}) {
    const transitionId = requireTransitionId(suppliedTransitionId)
    const normalizedWorkerId = String(workerId || '').trim()
    if (!normalizedWorkerId || normalizedWorkerId.length > 200) {
      fail('WORKER_ID_REQUIRED', 'workerId must be a non-empty string of at most 200 characters', 422)
    }
    const durationMs = Number(leaseMs)
    if (!Number.isInteger(durationMs) || durationMs < 100) {
      fail('LEASE_MS_REQUIRED', 'leaseMs must be an integer of at least 100 milliseconds', 422)
    }
    const normalizedWorkerPid = positiveInteger(workerPid, 0, 1)
    if (!Number.isInteger(normalizedWorkerPid) || normalizedWorkerPid < 1) {
      fail('WORKER_PID_REQUIRED', 'workerPid must be a positive integer', 422)
    }
    const normalizedCheckpoint = normalizeCheckpointDescriptor(checkpoint)
    if (typeof checkpointDigest !== 'string' || !/^[A-F0-9]{64}$/.test(checkpointDigest)) {
      fail('CHECKPOINT_DIGEST_INVALID', 'checkpointDigest must be the uppercase SHA-256 descriptor digest', 422)
    }
    const expectedCheckpointDigest = nativeCheckpointDescriptorDigest(normalizedCheckpoint)
    if (checkpointDigest !== expectedCheckpointDigest) {
      fail('CHECKPOINT_DIGEST_MISMATCH', 'checkpointDigest does not match the checkpoint descriptor', 409, {
        expectedDigest: expectedCheckpointDigest,
      })
    }
    const normalizedTaskId = validatedTaskId(taskId)
    const requestedNowMs = requiredMutationNow(clock, now)
    const payloadDigest = digest({
      checkpoint: normalizedCheckpoint,
      checkpointDigest,
      leaseMs: durationMs,
      workerId: normalizedWorkerId,
      workerPid: normalizedWorkerPid,
    })

    return withLock(lockConfig, async () => {
      const task = readTaskUnlocked(dataDir, normalizedTaskId)
      const repeated = idempotentTransition(
        task,
        transitionId,
        'checkpoint_resume',
        'building',
        payloadDigest,
      )
      if (repeated) return cloneJson(repeated)
      requireRevision(task, expectedRevision)
      if (task.nativeBuild.state !== 'cancelled') {
        fail('CHECKPOINT_RESUME_STATE_INVALID', 'only a cancelled native task can resume from a checkpoint', 409)
      }
      if (task.nativeBuild.lease !== null) {
        fail('CHECKPOINT_RESUME_LEASE_PRESENT', 'cancelled native task must not have an active lease', 409)
      }
      if (task.archived === true || task.nativeBuild.archived === true) {
        fail('CHECKPOINT_RESUME_ARCHIVED', 'archived native task cannot resume from a checkpoint', 409)
      }
      if (task.modelReady === true || task.nativeBuild.modelReady === true) {
        fail('CHECKPOINT_RESUME_MODEL_READY', 'native task with a ready model cannot resume from a checkpoint', 409)
      }
      if (task.nativeBuild.result !== undefined && task.nativeBuild.result !== null) {
        fail('CHECKPOINT_RESUME_RESULT_PRESENT', 'native task with a result cannot resume from a checkpoint', 409)
      }
      if (normalizedCheckpoint.taskId !== task.id) {
        fail('CHECKPOINT_TASK_MISMATCH', 'checkpoint taskId does not match the native task', 409)
      }
      if (normalizedCheckpoint.attempt !== task.nativeBuild.attempt) {
        fail('CHECKPOINT_ATTEMPT_MISMATCH', 'checkpoint attempt does not match the native task attempt', 409)
      }
      const planArtifact = task.nativeBuild.planArtifact
      if (!planArtifact || typeof planArtifact !== 'object' || Array.isArray(planArtifact) ||
          typeof planArtifact.fileSha256 !== 'string' ||
          normalizedCheckpoint.planSha256 !== planArtifact.fileSha256) {
        fail('CHECKPOINT_PLAN_MISMATCH', 'checkpoint planSha256 does not match the immutable native plan artifact', 409)
      }
      if (normalizedCheckpoint.planWorkerId !== normalizedWorkerId) {
        fail('CHECKPOINT_WORKER_MISMATCH', 'checkpoint planWorkerId does not match workerId', 409)
      }
      const hasExistingCheckpoint = task.nativeBuild.checkpoint !== undefined && task.nativeBuild.checkpoint !== null
      if (hasExistingCheckpoint) {
        const existingCheckpoint = normalizeCheckpointDescriptor(task.nativeBuild.checkpoint, 'nativeBuild.checkpoint')
        if (stableJson(existingCheckpoint) !== stableJson(normalizedCheckpoint) ||
            nativeCheckpointDescriptorDigest(existingCheckpoint) !== checkpointDigest) {
          fail('CHECKPOINT_EXISTING_MISMATCH', 'existing nativeBuild.checkpoint does not match the resume checkpoint', 409)
        }
        if (task.nativeBuild.checkpointDigest !== undefined && task.nativeBuild.checkpointDigest !== null &&
            task.nativeBuild.checkpointDigest !== checkpointDigest) {
          fail('CHECKPOINT_EXISTING_DIGEST_MISMATCH', 'existing nativeBuild.checkpointDigest does not match the resume checkpoint', 409)
        }
      } else if (task.nativeBuild.checkpointDigest !== undefined && task.nativeBuild.checkpointDigest !== null) {
        fail('CHECKPOINT_EXISTING_MISMATCH', 'nativeBuild.checkpointDigest exists without its checkpoint descriptor', 409)
      }

      const at = new Date(requestedNowMs).toISOString()
      const leaseId = randomToken()
      task.nativeBuild.lease = {
        id: leaseId,
        workerId: normalizedWorkerId,
        workerPid: normalizedWorkerPid,
        claimedAt: at,
        heartbeatAt: at,
        expiresAt: new Date(requestedNowMs + durationMs).toISOString(),
        durationMs,
      }
      task.nativeBuild.checkpoint = cloneJson(normalizedCheckpoint, 'checkpoint')
      task.nativeBuild.checkpointDigest = checkpointDigest
      task.nativeBuild.executionStarted = true
      task.nativeBuild.modelReady = false
      task.nativeBuild.error = null
      task.nativeBuild.execution = {
        started: true,
        cadStarted: false,
        modelGenerated: false,
        modelReady: false,
        legacyFallbackUsed: false,
      }
      task.nativeBuild.progress = { stage: 'checkpoint_resume', percent: 55 }
      task.nativeBuild.message = `结构工程辅助任务从 ${normalizedCheckpoint.completedPhase} 检查点继续。`
      task.modelReady = false
      task.engineeringAssistanceReady = false
      task.legacyFallbackUsed = false
      task.deliveryMode = 'native_task_pending'
      setTaskState(task, 'building')
      appendTransition(task, {
        transitionId,
        operation: 'checkpoint_resume',
        fromState: 'cancelled',
        toState: 'building',
        payloadDigest,
        at,
        leaseId,
      })
      bumpRevision(task, requestedNowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function heartbeat({ taskId, workerId, leaseId, expectedRevision, now } = {}) {
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const task = readTaskUnlocked(dataDir, taskId)
      requireRevision(task, expectedRevision)
      const lease = requireLease(task, leaseId, workerId, nowMs)
      task.nativeBuild.lease = renewedLease(lease, nowMs)
      task.nativeBuild.heartbeatCount = positiveInteger(task.nativeBuild.heartbeatCount, 0, 0) + 1
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function performTransition({
    taskId,
    workerId,
    leaseId,
    expectedRevision,
    transitionId: suppliedTransitionId,
    toState: suppliedToState,
    patch,
    now,
    operation = 'transition',
    operationPayload = null,
  }) {
    const transitionId = requireTransitionId(suppliedTransitionId)
    const toState = String(suppliedToState || '').trim().toLowerCase()
    const nativeBuildPatch = normalizeNativeBuildPatch(patch)
    const payloadDigest = digest({
      leaseId: String(leaseId || ''),
      toState,
      patch: nativeBuildPatch,
      operationPayload,
    })
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const task = readTaskUnlocked(dataDir, taskId)
      const repeated = idempotentTransition(task, transitionId, operation, toState, payloadDigest)
      if (repeated) return cloneJson(repeated)
      requireRevision(task, expectedRevision)
      const lease = requireLease(task, leaseId, workerId, nowMs)
      assertAllowedTransition(task, toState)
      const fromState = task.nativeBuild.state
      for (const [key, value] of Object.entries(nativeBuildPatch)) task.nativeBuild[key] = value
      task.nativeBuild.lease = renewedLease(lease, nowMs)
      if (operation === 'complete') {
        task.nativeBuild.result = cloneJson(operationPayload, 'result manifest')
        task.nativeBuild.error = null
      } else if (operation === 'fail') {
        task.nativeBuild.error = {
          ...cloneJson(operationPayload, 'failure'),
          at: new Date(nowMs).toISOString(),
        }
        task.nativeBuild.modelReady = false
      }
      setTaskState(task, toState)
      const at = new Date(nowMs).toISOString()
      appendTransition(task, { transitionId, operation, fromState, toState, payloadDigest, at, leaseId })
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function transition(args = {}) {
    const toState = String(args.toState || '').trim().toLowerCase()
    if (toState === 'ready') fail('COMPLETE_REQUIRED', 'use complete() to enter ready state', 422)
    if (toState === 'failed') fail('FAIL_REQUIRED', 'use fail() to enter failed state', 422)
    return performTransition({ ...args, toState, operation: 'transition' })
  }

  async function complete(args = {}) {
    const result = args.result ?? args.resultManifest
    if (!result || typeof result !== 'object' || Array.isArray(result)) {
      fail('RESULT_MANIFEST_REQUIRED', 'complete requires a result manifest object', 422)
    }
    return performTransition({
      ...args,
      toState: 'ready',
      operation: 'complete',
      operationPayload: cloneJson(result, 'result manifest'),
    })
  }

  async function failTask(args = {}) {
    const code = String(args.code || '').trim()
    const message = String(args.message || '').trim()
    if (!code || !message) fail('FAILURE_DETAILS_REQUIRED', 'fail requires code and message', 422)
    return performTransition({
      ...args,
      toState: 'failed',
      operation: 'fail',
      operationPayload: {
        code,
        message,
        retryable: args.retryable === true,
      },
    })
  }

  async function requeueBlockedForExecutor({
    taskId,
    expectedRevision,
    transitionId: suppliedTransitionId,
    expectedBlockerCode,
    executorRevision,
    now,
  } = {}) {
    const transitionId = requireTransitionId(suppliedTransitionId)
    const blockerCode = String(expectedBlockerCode || '').trim()
    if (blockerCode !== 'native_recipe_executor_not_implemented') {
      fail('BLOCKER_CODE_NOT_REQUEUEABLE', 'only the exact not-implemented executor blocker can be requeued', 422)
    }
    const revisionDigest = String(executorRevision || '').trim().toUpperCase()
    if (!/^[A-F0-9]{64}$/.test(revisionDigest)) {
      fail('EXECUTOR_REVISION_REQUIRED', 'executorRevision must be a trusted SHA-256', 422)
    }
    const toState = 'created'
    const payloadDigest = digest({ blockerCode, executorRevision: revisionDigest })
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const task = readTaskUnlocked(dataDir, taskId)
      const repeated = idempotentTransition(task, transitionId, 'executor_requeue', toState, payloadDigest)
      if (repeated) return cloneJson(repeated)
      requireRevision(task, expectedRevision)
      if (task.nativeBuild.state !== 'blocked' || task.nativeBuild.lease || task.nativeBuild.archived === true) {
        fail('TASK_NOT_REQUEUEABLE', `cannot requeue native task in state ${task.nativeBuild.state}`, 409)
      }
      if (String(task.nativeBuild.blocker?.code || '') !== blockerCode) {
        fail('BLOCKER_CODE_MISMATCH', 'native task blocker no longer matches the executor requeue request', 409)
      }
      if (task.modelReady === true || task.nativeBuild.modelReady === true || task.nativeBuild.result) {
        fail('TASK_NOT_REQUEUEABLE', 'a task with a model result cannot be requeued', 409)
      }
      const fromState = task.nativeBuild.state
      task.nativeBuild.blocker = null
      task.nativeBuild.error = null
      task.nativeBuild.planArtifact = null
      task.nativeBuild.validation = null
      task.nativeBuild.result = null
      task.nativeBuild.executionStarted = false
      task.nativeBuild.modelReady = false
      task.nativeBuild.execution = {
        started: false,
        cadStarted: false,
        modelGenerated: false,
        modelReady: false,
        legacyFallbackUsed: false,
      }
      task.nativeBuild.progress = { stage: 'created', percent: 0 }
      task.nativeBuild.executorRevision = revisionDigest
      task.nativeBuild.message = 'Trusted native executor revision is available; task returned to the native build queue.'
      task.modelReady = false
      task.engineeringAssistanceReady = false
      task.deliveryMode = 'native_task_pending'
      setTaskState(task, toState)
      const at = new Date(nowMs).toISOString()
      appendTransition(task, {
        transitionId,
        operation: 'executor_requeue',
        fromState,
        toState,
        payloadDigest,
        at,
        leaseId: null,
      })
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function cancel({ taskId, username, expectedRevision, transitionId: suppliedTransitionId, reason, now } = {}) {
    const transitionId = requireTransitionId(suppliedTransitionId)
    const normalizedReason = String(reason || '').trim().slice(0, 1000)
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const task = readTaskUnlocked(dataDir, taskId)
      if (username !== undefined && String(task.username || '') !== String(username)) {
        fail('TASK_NOT_FOUND', 'native task not found', 404)
      }
      const toState = LEASED_STATES.has(task.nativeBuild.state) ? 'cancel_requested' : 'cancelled'
      const payloadDigest = digest({ toState, username: String(username || ''), reason: normalizedReason })
      const repeated = idempotentTransition(task, transitionId, 'cancel', toState, payloadDigest)
      if (repeated) return cloneJson(repeated)
      requireRevision(task, expectedRevision)
      if (!['created', 'claimed', 'planning', 'building', 'validating', 'needs_input', 'blocked', 'failed', 'cancel_requested'].includes(task.nativeBuild.state)) {
        fail('INVALID_TRANSITION', `cannot cancel native task in state ${task.nativeBuild.state}`)
      }
      if (task.nativeBuild.state === 'cancel_requested') {
        fail('CANCEL_ALREADY_REQUESTED', 'native task cancellation is already requested')
      }
      const fromState = task.nativeBuild.state
      task.nativeBuild.cancel = {
        requestedBy: username === undefined ? null : String(username),
        requestedAt: new Date(nowMs).toISOString(),
        reason: normalizedReason || null,
      }
      setTaskState(task, toState)
      appendTransition(task, {
        transitionId,
        operation: 'cancel',
        fromState,
        toState,
        payloadDigest,
        at: new Date(nowMs).toISOString(),
        leaseId: task.nativeBuild.lease?.id || null,
      })
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  async function archive({ taskId, username, expectedRevision, transitionId: suppliedTransitionId, reason, now } = {}) {
    const transitionId = requireTransitionId(suppliedTransitionId)
    const scopedUsername = String(username || '').trim()
    if (!scopedUsername) fail('USERNAME_REQUIRED', 'username is required to archive a native task', 422)
    const normalizedReason = String(reason || '').trim().slice(0, 1000)
    return withLock(lockConfig, async () => {
      const nowMs = mutationNow(clock, now)
      const task = readTaskUnlocked(dataDir, taskId)
      if (String(task.username || '') !== scopedUsername) fail('TASK_NOT_FOUND', 'native task not found', 404)
      const state = task.nativeBuild.state
      const payloadDigest = digest({ state, username: scopedUsername, reason: normalizedReason })
      const prior = [...task.nativeBuild.statusHistory].reverse().find((entry) => entry.transitionId === transitionId)
      if (prior) {
        if (prior.operation !== 'archive' || prior.from !== state || prior.to !== state || prior.payloadDigest !== payloadDigest) {
          fail('TRANSITION_ID_REUSED', `transitionId was already used for a different mutation: ${transitionId}`)
        }
        return cloneJson(task)
      }
      requireRevision(task, expectedRevision)
      if (!ARCHIVABLE_STATES.has(state)) {
        fail('TASK_NOT_ARCHIVABLE', `cannot archive native task in state ${state}`)
      }
      task.nativeBuild.archived = true
      task.nativeBuild.archivedAt = new Date(nowMs).toISOString()
      task.nativeBuild.archive = {
        archivedBy: scopedUsername,
        archivedAt: task.nativeBuild.archivedAt,
        reason: normalizedReason || null,
      }
      appendTransition(task, {
        transitionId,
        operation: 'archive',
        fromState: state,
        toState: state,
        payloadDigest,
        at: task.nativeBuild.archivedAt,
        leaseId: null,
      })
      bumpRevision(task, nowMs)
      const saved = writeTaskUnlocked(dataDir, task)
      rebuildIndexBestEffort()
      return cloneJson(saved)
    })
  }

  return Object.freeze({
    dataDir,
    indexPath,
    lockPath,
    list,
    listSnapshot,
    read,
    createOrImport,
    createOrReuseByFingerprint,
    claimNext,
    claimTask,
    resumeCancelledFromCheckpoint,
    heartbeat,
    transition,
    complete,
    fail: failTask,
    requeueBlockedForExecutor,
    cancel,
    archive,
  })
}
