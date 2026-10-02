import { createHash, randomBytes } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fsyncSync,
  mkdirSync,
  openSync,
  readFileSync,
  renameSync,
  rmSync,
  statSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs'
import { hostname } from 'node:os'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  createNativeTaskStore,
  nativeCheckpointDescriptorDigest,
} from './lib/locker_16029_native_task_store.mjs'
import {
  buildTrustedNativePlan,
  nativeRecipeDigest,
  resolveTrustedNativeRecipe,
} from './locker_16029_native_recipe_registry.mjs'
import { buildNativeAssemblyContract } from './lib/locker_16029_native_assembly_contract.mjs'
import {
  buildNativeStageReceiptContracts,
  NATIVE_STAGE_RECEIPT_ORDER,
} from './lib/locker_16029_native_stage_contract.mjs'
import { NATIVE_EXECUTION_TOOL_CONTRACTS } from './lib/locker_16029_native_execution_authorization.mjs'
import {
  resolveTrustedNativeToolchainArtifacts,
} from './lib/locker_16029_native_toolchain_manifests.mjs'
import {
  buildNative888x14ExecutionBlueprint,
  canonicalFlatCadInventoryDigest,
  prepareNative888x14DoorInputs,
  runAuthorizedNativeStageUnit,
  runNative888x14LockValidation,
} from './lib/locker_16029_native_stage_executor.mjs'
import { loadNativeCheckpoint } from './lib/locker_16029_native_checkpoint.mjs'
import { publishNativeStructureAssistance } from './lib/locker_16029_native_publication.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const DEFAULT_POLL_MS = 5000
const MIN_POLL_MS = 100
const MAX_POLL_MS = 60000
const WORKER_LOCK_STALE_MS = 120000
const NATIVE_TASK_LEASE_MS = 30 * 60 * 1000

function isoNow(clock = () => new Date()) {
  const value = clock()
  return (value instanceof Date ? value : new Date(value)).toISOString()
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function stableJson(value) {
  return JSON.stringify(stableValue(value))
}

function assertSafeIdentifier(value, label) {
  const normalized = String(value || '')
  if (!/^[A-Za-z0-9_.-]{1,160}$/.test(normalized)) {
    const error = new Error(`${label} is not a safe identifier`)
    error.code = 'unsafe_identifier'
    throw error
  }
  return normalized
}

function ensureUnderRoot(candidate, root, label) {
  const rel = relative(resolve(root), resolve(candidate))
  if (rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))) return resolve(candidate)
  throw new Error(`${label} escaped its trusted root`)
}

function nativeTaskRoot(dataDir) {
  return ensureUnderRoot(resolve(dataDir, 'native_model_requests'), dataDir, 'worker task root')
}

function atomicWriteJson(filePath, value, { immutable = false } = {}) {
  mkdirSync(dirname(filePath), { recursive: true })
  const content = `${JSON.stringify(value, null, 2)}\n`
  if (immutable && existsSync(filePath)) {
    const existing = readFileSync(filePath, 'utf8')
    if (existing !== content) {
      const error = new Error(`immutable artifact already exists with different content: ${basename(filePath)}`)
      error.code = 'immutable_artifact_conflict'
      throw error
    }
    return { created: false, sha256: sha256(existing) }
  }
  const tempPath = `${filePath}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`
  let descriptor = null
  try {
    descriptor = openSync(tempPath, 'wx')
    writeFileSync(descriptor, content, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    if (immutable && existsSync(filePath)) {
      const existing = readFileSync(filePath, 'utf8')
      if (existing !== content) {
        const error = new Error(`immutable artifact already exists with different content: ${basename(filePath)}`)
        error.code = 'immutable_artifact_conflict'
        throw error
      }
      unlinkSync(tempPath)
      return { created: false, sha256: sha256(existing) }
    }
    renameSync(tempPath, filePath)
    return { created: true, sha256: sha256(content) }
  } finally {
    if (descriptor !== null) closeSync(descriptor)
    if (existsSync(tempPath)) unlinkSync(tempPath)
  }
}

function isProcessLive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

export function acquireNativeWorkerInstanceLock({
  dataDir,
  workerId,
  clock = () => new Date(),
  staleMs = WORKER_LOCK_STALE_MS,
} = {}) {
  const safeWorkerId = assertSafeIdentifier(workerId, 'workerId')
  const currentMs = () => {
    const value = clock()
    return (value instanceof Date ? value : new Date(value)).getTime()
  }
  const taskRoot = nativeTaskRoot(dataDir)
  const lockDir = ensureUnderRoot(resolve(taskRoot, '.native-worker-instance.lock'), taskRoot, 'worker lock')
  const ownerPath = resolve(lockDir, 'owner.json')
  const token = randomBytes(16).toString('hex')
  mkdirSync(taskRoot, { recursive: true })

  const attemptAcquire = () => {
    try {
      mkdirSync(lockDir)
      return true
    } catch (error) {
      if (error?.code !== 'EEXIST') throw error
      return false
    }
  }

  if (!attemptAcquire()) {
    let owner = null
    try {
      owner = JSON.parse(readFileSync(ownerPath, 'utf8'))
    } catch {
      // The directory timestamp remains the fallback for an interrupted owner write.
    }
    const heartbeatMs = Date.parse(String(owner?.heartbeatAt || ''))
    const fallbackMs = statSync(lockDir).mtimeMs
    const stale = currentMs() - (Number.isFinite(heartbeatMs) ? heartbeatMs : fallbackMs) > staleMs
    if (!stale || isProcessLive(Number(owner?.pid))) {
      const error = new Error(`native worker instance is already active: ${owner?.workerId || 'unknown'}`)
      error.code = 'native_worker_already_active'
      throw error
    }
    rmSync(lockDir, { recursive: true, force: true })
    if (!attemptAcquire()) {
      const error = new Error('native worker instance lock was claimed concurrently')
      error.code = 'native_worker_already_active'
      throw error
    }
  }

  const owner = {
    schema: 'winnsen.native_worker_instance_lock.v1',
    workerId: safeWorkerId,
    token,
    pid: process.pid,
    host: hostname(),
    acquiredAt: isoNow(clock),
    heartbeatAt: isoNow(clock),
  }
  atomicWriteJson(ownerPath, owner)

  return {
    lockDir,
    heartbeat() {
      const current = JSON.parse(readFileSync(ownerPath, 'utf8'))
      if (current.token !== token || current.workerId !== safeWorkerId) {
        const error = new Error('native worker instance lock ownership changed')
        error.code = 'native_worker_lock_lost'
        throw error
      }
      current.heartbeatAt = isoNow(clock)
      atomicWriteJson(ownerPath, current)
      return current
    },
    release() {
      if (!existsSync(ownerPath)) return false
      const current = JSON.parse(readFileSync(ownerPath, 'utf8'))
      if (current.token !== token || current.workerId !== safeWorkerId) return false
      rmSync(lockDir, { recursive: true, force: true })
      return true
    },
  }
}

function requestDigestForPlan(task, trustedPlan) {
  return sha256(stableJson({
    requestFingerprint: String(task.requestFingerprint || trustedPlan.requestFingerprint || ''),
    customerRequirementReference: String(task.customerRequirementReference || ''),
    geometry: trustedPlan.geometry,
    calculations: trustedPlan.calculations,
  }))
}

function taskDigestForPlan(task) {
  return sha256(stableJson({
    id: String(task.id || ''),
    revision: Number(task.revision || 0),
    status: String(task.status || ''),
    taskType: String(task.taskType || ''),
    requestFingerprint: String(task.requestFingerprint || ''),
    nativeBuildAttempt: Number(task.nativeBuild?.attempt || 0),
  }))
}

function validatedToolchainManifestMap(value) {
  const expected = Object.keys(NATIVE_EXECUTION_TOOL_CONTRACTS)
  const actual = value && typeof value === 'object' && !Array.isArray(value) ? Object.keys(value) : []
  if (JSON.stringify(actual) !== JSON.stringify(expected) ||
      actual.some((id) => !/^[A-F0-9]{64}$/i.test(String(value[id] || '')))) {
    const error = new Error('trusted native toolchain manifest map is incomplete or invalid')
    error.code = 'native_toolchain_manifest_map_invalid'
    throw error
  }
  return Object.fromEntries(expected.map((id) => [id, String(value[id]).toUpperCase()]))
}

function buildImmutablePlanArtifact({ task, recipe, trustedPlan, workerId, now,
  trustedToolchainManifests }) {
  const recipeDigest = nativeRecipeDigest(recipe)
  const derivedGeometry = trustedPlan.derivedGeometry || {
    installedDoorPanelWidthMm: trustedPlan.geometry?.doorPanelWidthMm,
    installedDoorPanelHeightMm: trustedPlan.calculations?.doorHeightMm,
    doorPitchMm: trustedPlan.calculations?.doorPitchMm,
    doorBottomYMinMm: trustedPlan.calculations?.doorBottomYMinMm,
    doorCenterYByColumnMm: trustedPlan.calculations?.doorCenterYByColumnMm,
    internalBoundaryYmm: trustedPlan.calculations?.internalBoundaryYmm,
    shelfCenterYmm: trustedPlan.calculations?.shelfCenterYmm,
    frontFrameCrossbarCenterYmm: trustedPlan.calculations?.frontFrameCrossbarCenterYmm,
    rowSequence: trustedPlan.geometry?.rowSequence,
  }
  const artifact = {
    schema: 'winnsen.native_build_plan.v1',
    createdAt: now,
    workerId,
    purpose: trustedPlan.purpose || recipe.purpose,
    task: {
      id: task.id,
      revisionAtPlanning: task.revision,
      digest: taskDigestForPlan(task),
    },
    request: {
      fingerprint: String(task.requestFingerprint || trustedPlan.requestFingerprint || ''),
      digest: requestDigestForPlan(task, trustedPlan),
    },
    recipe: {
      id: trustedPlan.recipeId || recipe.id,
      version: trustedPlan.recipeVersion || recipe.version || recipe.schema,
      digest: trustedPlan.recipeDigest || recipeDigest,
    },
    geometry: trustedPlan.geometry,
    derivedGeometry,
    assemblyContract: buildNativeAssemblyContract(recipe),
    stageContracts: trustedPlan.stages,
    trustedToolchainManifests: validatedToolchainManifestMap(trustedToolchainManifests),
    stageReceiptContracts: buildNativeStageReceiptContracts(recipe),
    requiredChecks: trustedPlan.requiredChecks,
    qualityBoundary: trustedPlan.qualityBoundary,
    executionBoundary: {
      executorImplemented: false,
      cadStarted: false,
      modelGenerated: false,
      modelReady: false,
      legacyFallbackUsed: false,
    },
  }
  return { artifact, digest: sha256(stableJson(artifact)) }
}

function transitionId(taskId, suffix) {
  return `worker-${taskId}-${suffix}-${randomBytes(5).toString('hex')}`
}

function leaseArgs(task, workerId) {
  return {
    taskId: task.id,
    workerId,
    leaseId: task.nativeBuild?.lease?.id,
    expectedRevision: task.revision,
  }
}

function toolchainManifestMap(toolchainArtifacts) {
  if (!toolchainArtifacts || typeof toolchainArtifacts !== 'object' || Array.isArray(toolchainArtifacts)) {
    return validatedToolchainManifestMap(toolchainArtifacts)
  }
  const values = Object.values(toolchainArtifacts)
  if (values.every((value) => typeof value === 'string')) {
    return validatedToolchainManifestMap(toolchainArtifacts)
  }
  return validatedToolchainManifestMap(Object.fromEntries(Object.entries(toolchainArtifacts)
    .map(([id, artifact]) => [id, artifact?.manifestSha256])))
}

function requiredDigest(value, label) {
  const digest = String(value || '').trim().toUpperCase()
  if (!/^[A-F0-9]{64}$/.test(digest)) {
    const error = new Error(`${label} must be a SHA-256 digest`)
    error.code = 'native_worker_evidence_binding_invalid'
    throw error
  }
  return digest
}

function publicManifestReference(value, label) {
  const path = String(value?.path || '').trim()
  if (!path) {
    const error = new Error(`${label} path is required`)
    error.code = 'native_worker_evidence_binding_invalid'
    throw error
  }
  return Object.freeze({ path, sha256: requiredDigest(value?.sha256, `${label} sha256`) })
}

function readStageReceiptReference(unit, phase, result) {
  if (result?.receipt) {
    if (String(result.receipt.phase || '') !== phase) {
      const error = new Error(`stage receipt phase does not match ${phase}`)
      error.code = 'native_worker_stage_result_invalid'
      throw error
    }
    return {
      receipt: publicManifestReference(result.receipt, `${phase} receipt`),
      evidence: publicManifestReference(result.evidence, `${phase} evidence`),
    }
  }

  const receiptPath = String(unit?.receiptPath || '')
  const consumed = result?.consumed
  if (!receiptPath || consumed?.consumed !== true) {
    const error = new Error(`stage result is incomplete for ${phase}`)
    error.code = 'native_worker_stage_result_invalid'
    throw error
  }
  let receiptPayload
  try {
    receiptPayload = JSON.parse(readFileSync(receiptPath, 'utf8'))
  } catch {
    const error = new Error(`stage receipt is unreadable for ${phase}`)
    error.code = 'native_worker_stage_result_invalid'
    throw error
  }
  if (String(receiptPayload?.phase || '') !== phase) {
    const error = new Error(`persisted stage receipt phase does not match ${phase}`)
    error.code = 'native_worker_stage_result_invalid'
    throw error
  }
  return {
    receipt: publicManifestReference({
      path: receiptPath,
      sha256: consumed.receiptSha256,
    }, `${phase} receipt`),
    evidence: publicManifestReference({
      path: receiptPayload.evidencePath,
      sha256: consumed.evidenceSha256,
    }, `${phase} evidence`),
  }
}

function normalizedStageResult({ unit, phase, result }) {
  const references = readStageReceiptReference(unit, phase, result)
  return Object.freeze({
    phase,
    receipt: Object.freeze({ phase, ...references.receipt }),
    evidence: references.evidence,
    postInventoryDigest: requiredDigest(result?.postInventoryDigest, `${phase} post inventory`),
  })
}

function lockValidationEvidence(result, blueprint) {
  if (result?.finalEvidence) {
    return publicManifestReference(result.finalEvidence, 'lock validation evidence')
  }
  const path = resolve(blueprint?.attemptDir || '', 'evidence/lock_topology_888x14.validation.v2.json')
  if (!existsSync(path)) {
    const error = new Error('lock validation evidence is missing')
    error.code = 'native_worker_evidence_binding_invalid'
    throw error
  }
  return publicManifestReference({ path, sha256: sha256(readFileSync(path)) }, 'lock validation evidence')
}

function archiveReference(value) {
  const path = String(value?.path || '').trim()
  const fileName = basename(String(value?.fileName || path))
  const sizeBytes = Number(value?.sizeBytes)
  if (!path || !fileName.toLowerCase().endsWith('.zip') || !Number.isSafeInteger(sizeBytes) || sizeBytes <= 0) {
    const error = new Error('native publication archive reference is invalid')
    error.code = 'native_worker_publication_invalid'
    throw error
  }
  return Object.freeze({
    path,
    fileName,
    sha256: requiredDigest(value?.sha256, 'publication archive sha256'),
    sizeBytes,
  })
}

function completionResult(stageResults, validationEvidence, publication) {
  const phases = stageResults.map((result) => result.phase)
  if (JSON.stringify(phases) !== JSON.stringify(NATIVE_STAGE_RECEIPT_ORDER)) {
    const error = new Error('native stage receipt order is incomplete')
    error.code = 'native_worker_stage_result_invalid'
    throw error
  }
  for (const result of stageResults) {
    publicManifestReference(result.receipt, `${result.phase} receipt`)
    publicManifestReference(result.evidence, `${result.phase} evidence`)
    requiredDigest(result.postInventoryDigest, `${result.phase} post inventory`)
  }
  const finalStage = stageResults.at(-1)
  const finalReceipt = publicManifestReference(finalStage.receipt, 'final receipt')
  const packageManifest = publicManifestReference(publication?.manifest, 'final package manifest')
  const archive = archiveReference(publication?.archive)
  return Object.freeze({
    schema: 'winnsen.native_structure_engineering_assistance_result.v1',
    validationEvidence: publicManifestReference(validationEvidence, 'final validation evidence'),
    finalReceipt,
    packageManifest,
    archive,
  })
}

function failureCode(error) {
  const code = String(error?.code || '').trim()
  return /^[A-Za-z0-9_.-]{1,160}$/.test(code) ? code : 'native_worker_execution_failed'
}

async function cancelClaimedTask({ taskStore, task, workerId, clock, suffix }) {
  const cancelled = await taskStore.transition({
    ...leaseArgs(task, workerId),
    transitionId: transitionId(task.id, suffix),
    toState: 'cancelled',
    patch: {
      execution: {
        started: task.nativeBuild?.executionStarted === true,
        cadStarted: task.nativeBuild?.execution?.cadStarted === true,
        modelGenerated: false,
        modelReady: false,
        legacyFallbackUsed: false,
      },
      progress: { stage: 'cancelled', percent: Number(task.nativeBuild?.progress?.percent || 0) },
      message: '已在当前原子阶段安全收口后取消结构工程辅助任务。',
    },
    now: isoNow(clock),
  })
  return { status: 'cancelled', claimed: true, task: cancelled }
}

function checkpointError(code, message) {
  const error = new Error(message)
  error.code = code
  return error
}

function checkpointPhaseIndex(value) {
  const phase = String(value || '')
  const index = NATIVE_STAGE_RECEIPT_ORDER.indexOf(phase)
  if (index < 0) throw checkpointError('native_checkpoint_phase_invalid', 'checkpoint phase is not registered')
  return index
}

function requiredCheckpointSnapshot(value, completedPhase) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw checkpointError('native_checkpoint_invalid', 'checkpoint loader did not return a snapshot')
  }
  const descriptor = value.descriptor
  const prefixStageResults = value.prefixStageResults
  const currentInventoryDigest = requiredDigest(value.currentInventoryDigest, 'checkpoint inventory')
  if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor) ||
      !Array.isArray(prefixStageResults) || !/^[A-F0-9]{64}$/i.test(String(value.checkpointDigest || ''))) {
    throw checkpointError('native_checkpoint_invalid', 'checkpoint loader returned an incomplete snapshot')
  }
  const completedIndex = checkpointPhaseIndex(completedPhase)
  const actualPrefix = prefixStageResults.map((row) => row?.phase)
  const expectedPrefix = NATIVE_STAGE_RECEIPT_ORDER.slice(0, completedIndex + 1)
  if (stableJson(actualPrefix) !== stableJson(expectedPrefix) ||
      String(descriptor.taskId || '') === '' || Number(descriptor.attempt) < 1 ||
      String(descriptor.completedPhase || '') !== String(completedPhase || '') ||
      !/^[A-F0-9]{64}$/i.test(String(descriptor.planSha256 || '')) ||
      !String(descriptor.planWorkerId || '')) {
    throw checkpointError('native_checkpoint_invalid', 'checkpoint descriptor does not match its fixed prefix')
  }
  return Object.freeze({
    ...value,
    descriptor: Object.freeze(descriptor),
    checkpointDigest: String(value.checkpointDigest).toUpperCase(),
    prefixStageResults: Object.freeze([...prefixStageResults]),
    currentInventoryDigest,
    completedIndex,
  })
}

function normalizeStopAfterPhase(value, completedIndex) {
  const phase = String(value || '').trim()
  if (!phase) return ''
  // This controlled route deliberately creates a reusable door checkpoint only.
  if (phase !== 'door_module_888x14') {
    throw checkpointError('native_checkpoint_stop_phase_invalid', 'only door_module_888x14 can be a checkpoint stop phase')
  }
  const index = checkpointPhaseIndex(phase)
  if (index <= completedIndex) {
    throw checkpointError('native_checkpoint_stop_phase_invalid', 'checkpoint stop phase must follow the loaded prefix')
  }
  return phase
}

async function cancelAtCheckpoint({ taskStore, task, workerId, checkpoint, phase, clock, suffix }) {
  const cancelled = await taskStore.transition({
    ...leaseArgs(task, workerId),
    transitionId: transitionId(task.id, suffix),
    toState: 'cancelled',
    patch: {
      checkpoint,
      checkpointDigest: nativeCheckpointDescriptorDigest(checkpoint),
      execution: {
        started: true,
        cadStarted: true,
        modelGenerated: false,
        modelReady: false,
        legacyFallbackUsed: false,
      },
      progress: { stage: 'checkpoint', percent: phase === 'door_module_888x14' ? 60 : 20 },
      message: `结构工程辅助检查点已在 ${phase} 原子阶段收口。`,
    },
    now: isoNow(clock),
  })
  return { status: 'cancelled', claimed: true, task: cancelled }
}

function checkpointToolchainMatches(snapshot, artifacts) {
  const expected = snapshot?.descriptor?.toolchainManifests
  if (!expected || typeof expected !== 'object' || Array.isArray(expected)) {
    throw checkpointError('native_checkpoint_toolchain_invalid', 'checkpoint has no trusted toolchain manifest map')
  }
  if (stableJson(expected) !== stableJson(toolchainManifestMap(artifacts))) {
    throw checkpointError('native_checkpoint_toolchain_drift', 'live toolchain manifest map differs from checkpoint')
  }
}

export async function runNativeWorkerFromCheckpoint({
  dataDir = process.env.STUDIO_REVIEW_DATA_DIR || resolve(ROOT, 'data'),
  taskId,
  completedPhase = 'assemblies',
  stopAfterPhase = '',
  workerId = '',
  store = null,
  clock = () => new Date(),
  instanceLock = true,
  checkpointLoader = loadNativeCheckpoint,
  toolchainArtifactResolver = resolveTrustedNativeToolchainArtifacts,
  executionBlueprintBuilder = buildNative888x14ExecutionBlueprint,
  prepareDoorInputs = prepareNative888x14DoorInputs,
  runStageUnit = runAuthorizedNativeStageUnit,
  runLockValidation = runNative888x14LockValidation,
  publishNativeModel = publishNativeStructureAssistance,
} = {}) {
  const safeTaskId = assertSafeIdentifier(taskId, 'taskId')
  const taskRoot = nativeTaskRoot(dataDir)
  const taskStore = store || createNativeTaskStore({ dataDir: taskRoot, clock })
  const phaseIndex = checkpointPhaseIndex(completedPhase)
  let task = null
  let lock = null
  let ownedLease = null
  try {
    task = await taskStore.read(safeTaskId)
    const initial = requiredCheckpointSnapshot(await checkpointLoader({
      dataDir,
      taskId: task.id,
      completedPhase,
      repositoryRoot: ROOT,
    }), completedPhase)
    if (initial.descriptor.taskId !== task.id || Number(initial.descriptor.attempt) !== Number(task.nativeBuild?.attempt)) {
      throw checkpointError('native_checkpoint_task_mismatch', 'checkpoint descriptor does not belong to the requested task attempt')
    }
    const checkpointWorkerId = assertSafeIdentifier(initial.descriptor.planWorkerId, 'checkpoint plan workerId')
    if (workerId && String(workerId) !== checkpointWorkerId) {
      throw checkpointError('native_checkpoint_worker_mismatch', 'checkpoint resume worker must equal the immutable plan worker')
    }
    const safeWorkerId = checkpointWorkerId
    const safeStopAfterPhase = normalizeStopAfterPhase(stopAfterPhase, phaseIndex)
    lock = instanceLock ? acquireNativeWorkerInstanceLock({ dataDir, workerId: safeWorkerId, clock }) : null
    lock?.heartbeat()
    task = await taskStore.resumeCancelledFromCheckpoint({
      taskId: task.id,
      expectedRevision: task.revision,
      transitionId: transitionId(task.id, `checkpoint-resume-${completedPhase}`),
      workerId: safeWorkerId,
      workerPid: process.pid,
      leaseMs: NATIVE_TASK_LEASE_MS,
      checkpoint: initial.descriptor,
      checkpointDigest: initial.checkpointDigest,
      now: isoNow(clock),
    })
    if (!task.nativeBuild?.lease?.id || task.nativeBuild.lease.workerId !== safeWorkerId) {
      throw checkpointError('native_checkpoint_lease_invalid', 'atomic resume did not return this invocation\'s lease')
    }
    ownedLease = { workerId: safeWorkerId, leaseId: task.nativeBuild.lease.id }
    lock?.heartbeat()

    const checkpoint = requiredCheckpointSnapshot(await checkpointLoader({
      dataDir,
      taskId: task.id,
      completedPhase,
      repositoryRoot: ROOT,
    }), completedPhase)
    if (checkpoint.checkpointDigest !== initial.checkpointDigest) {
      throw checkpointError('native_checkpoint_changed', 'checkpoint changed between atomic resume and revalidation')
    }
    const checkpointRecipeTask = { ...task }
    delete checkpointRecipeTask.nativeBuild
    delete checkpointRecipeTask.revision
    delete checkpointRecipeTask.taskStoreSchema
    const recipe = resolveTrustedNativeRecipe(checkpointRecipeTask)
    const trustedPlan = recipe ? buildTrustedNativePlan(checkpointRecipeTask) : null
    if (!recipe || !trustedPlan) {
      throw checkpointError('native_checkpoint_recipe_invalid', 'trusted recipe no longer matches checkpoint task')
    }
    const toolchainArtifacts = await toolchainArtifactResolver({ repositoryRoot: ROOT })
    checkpointToolchainMatches(checkpoint, toolchainArtifacts)
    const blueprint = await executionBlueprintBuilder({
      attemptDir: checkpoint.attemptDir,
      planPath: resolve(checkpoint.attemptDir, 'native_build_plan.json'),
      taskId: task.id,
      repositoryRoot: ROOT,
      toolchainArtifacts,
      task,
      recipe,
      trustedPlan,
      planArtifact: checkpoint.plan,
    })
    let currentInventoryDigest = checkpoint.currentInventoryDigest
    const seedInventoryDigest = currentInventoryDigest
    const stageResults = [...checkpoint.prefixStageResults]
    let finalEvidence = null
    let doorInputs = null

    for (const phase of NATIVE_STAGE_RECEIPT_ORDER.slice(phaseIndex + 1)) {
      let latest = await taskStore.read(task.id)
      if (latest.nativeBuild?.state === 'cancel_requested') {
        return await cancelClaimedTask({ taskStore, task: latest, workerId: safeWorkerId, clock, suffix: `checkpoint-cancel-before-${phase}` })
      }
      if (latest.nativeBuild?.state !== 'building') {
        throw checkpointError('native_checkpoint_task_state_invalid', `native task left building state before ${phase}`)
      }
      task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      lock?.heartbeat()

      if (phase === 'door_module_888x14') {
        doorInputs = await prepareDoorInputs({
          blueprint,
          task,
          lease: task.nativeBuild.lease,
          workerId: safeWorkerId,
          repositoryRoot: ROOT,
        })
        latest = await taskStore.read(task.id)
        if (latest.nativeBuild?.state === 'cancel_requested') {
          return await cancelClaimedTask({ taskStore, task: latest, workerId: safeWorkerId, clock, suffix: 'checkpoint-cancel-after-door-inputs' })
        }
        task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      }

      const unit = Array.isArray(blueprint?.units)
        ? blueprint.units.find((candidate) => candidate?.receiptPhase === phase)
        : null
      const rawStageResult = await runStageUnit({
        blueprint,
        unit,
        phase,
        task,
        lease: task.nativeBuild.lease,
        workerId: safeWorkerId,
        attempt: Number(task.nativeBuild?.attempt),
        toolchainArtifacts,
        seedInventoryDigest,
        preInventoryDigest: currentInventoryDigest,
        doorInputs,
        isCancellationRequested: () => false,
        onInstanceLockHeartbeat: () => lock?.heartbeat(),
      })
      const stageResult = normalizedStageResult({ unit, phase, result: rawStageResult })
      stageResults.push(stageResult)
      currentInventoryDigest = stageResult.postInventoryDigest

      latest = await taskStore.read(task.id)
      if (phase === safeStopAfterPhase || (phase === 'door_module_888x14' && latest.nativeBuild?.state === 'cancel_requested')) {
        const advanced = requiredCheckpointSnapshot(await checkpointLoader({
          dataDir,
          taskId: latest.id,
          completedPhase: phase,
          repositoryRoot: ROOT,
          allowCheckpointAdvance: true,
        }), phase)
        return await cancelAtCheckpoint({
          taskStore,
          task: latest,
          workerId: safeWorkerId,
          checkpoint: advanced.descriptor,
          phase,
          clock,
          suffix: `checkpoint-stop-after-${phase}`,
        })
      }
      if (latest.nativeBuild?.state === 'cancel_requested') {
        return await cancelClaimedTask({ taskStore, task: latest, workerId: safeWorkerId, clock, suffix: `checkpoint-cancel-after-${phase}` })
      }
      task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      lock?.heartbeat()

      if (phase === 'root_assembly_888x14') {
        const lockResult = await runLockValidation({
          blueprint,
          task,
          lease: task.nativeBuild.lease,
          workerId: safeWorkerId,
          toolchainArtifacts,
          preInventoryDigest: currentInventoryDigest,
          receipts: stageResults.map((result) => result.receipt),
          onInstanceLockHeartbeat: () => lock?.heartbeat(),
        })
        finalEvidence = lockValidationEvidence(lockResult, blueprint)
      }
    }

    task = await taskStore.transition({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'checkpoint-validating'),
      toState: 'validating',
      patch: {
        message: '结构工程辅助任务已续跑全部原子阶段，正在绑定最终证据。',
        progress: { stage: 'validating', percent: 95 },
        validation: {
          stageReceiptCount: stageResults.length,
          stageReceiptOrder: [...NATIVE_STAGE_RECEIPT_ORDER],
          lockValidationBound: Boolean(finalEvidence),
        },
      },
      now: isoNow(clock),
    })
    const publication = await publishNativeModel({
      dataDir,
      blueprint,
      task,
      recipe,
      stageResults,
      finalValidationEvidence: finalEvidence,
      clock,
    })
    const result = completionResult(stageResults, finalEvidence, publication)
    task = await taskStore.complete({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'checkpoint-complete'),
      result,
      patch: {
        message: '结构工程辅助包已续跑全部原子收据并绑定最终证据。',
        progress: { stage: 'ready', percent: 100 },
        execution: {
          started: true,
          cadStarted: runStageUnit === runAuthorizedNativeStageUnit,
          modelGenerated: true,
          modelReady: true,
          legacyFallbackUsed: false,
        },
        validation: {
          stageReceiptCount: stageResults.length,
          stageReceiptOrder: [...NATIVE_STAGE_RECEIPT_ORDER],
          lockValidationBound: true,
          finalReceiptBound: true,
          packageManifestBound: true,
        },
      },
      now: isoNow(clock),
    })
    return { status: 'completed', claimed: true, task }
  } catch (error) {
    if (!task || !ownedLease) throw error
    let latest
    try { latest = await taskStore.read(task.id) } catch { throw error }
    if (latest.nativeBuild?.lease?.id !== ownedLease.leaseId ||
        latest.nativeBuild?.lease?.workerId !== ownedLease.workerId) throw error
    if (latest.nativeBuild?.state === 'cancel_requested') {
      return await cancelClaimedTask({ taskStore, task: latest, workerId: ownedLease.workerId, clock, suffix: 'checkpoint-cancel-after-execution-error' })
    }
    if (!['claimed', 'planning', 'building', 'validating'].includes(latest.nativeBuild?.state)) throw error
    const code = failureCode(error)
    task = await taskStore.fail({
      ...leaseArgs(latest, ownedLease.workerId),
      transitionId: transitionId(latest.id, 'checkpoint-failed'),
      code,
      message: error instanceof Error ? error.message : String(error),
      retryable: false,
      patch: {
        message: '结构工程辅助检查点续跑已失败关闭，未标记模型就绪。',
        progress: { stage: 'failed', percent: Number(latest.nativeBuild?.progress?.percent || 0) },
        execution: {
          started: latest.nativeBuild?.executionStarted === true,
          cadStarted: latest.nativeBuild?.execution?.cadStarted === true,
          modelGenerated: false,
          modelReady: false,
          legacyFallbackUsed: false,
        },
      },
      now: isoNow(clock),
    })
    return { status: 'failed', claimed: true, code, task }
  } finally {
    lock?.release()
  }
}

export async function runNativeWorkerOnce({
  dataDir = process.env.STUDIO_REVIEW_DATA_DIR || resolve(ROOT, 'data'),
  workerId = `native-worker-${process.pid}-${randomBytes(4).toString('hex')}`,
  store = null,
  clock = () => new Date(),
  instanceLock = true,
  toolchainArtifactResolver = resolveTrustedNativeToolchainArtifacts,
  executionBlueprintBuilder = buildNative888x14ExecutionBlueprint,
  prepareDoorInputs = prepareNative888x14DoorInputs,
  runStageUnit = runAuthorizedNativeStageUnit,
  runLockValidation = runNative888x14LockValidation,
  publishNativeModel = publishNativeStructureAssistance,
  inventoryDigest = canonicalFlatCadInventoryDigest,
} = {}) {
  const safeWorkerId = assertSafeIdentifier(workerId, 'workerId')
  const taskRoot = nativeTaskRoot(dataDir)
  const taskStore = store || createNativeTaskStore({ dataDir: taskRoot, clock })
  const lock = instanceLock ? acquireNativeWorkerInstanceLock({ dataDir, workerId: safeWorkerId, clock }) : null
  let task = null
  try {
    lock?.heartbeat()
    task = await taskStore.claimNext({
      workerId: safeWorkerId,
      leaseMs: NATIVE_TASK_LEASE_MS,
      now: isoNow(clock),
    })
    if (!task) return { status: 'idle', claimed: false, workerId: safeWorkerId }

    task = await taskStore.heartbeat({ ...leaseArgs(task, safeWorkerId), now: isoNow(clock) })
    task = await taskStore.transition({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'planning'),
      toState: 'planning',
      patch: {
        execution: {
          started: false,
          cadStarted: false,
          modelGenerated: false,
          modelReady: false,
          legacyFallbackUsed: false,
        },
        progress: { stage: 'planning', percent: 10 },
        message: '结构工程辅助任务已领取，正在解析可信配方。',
      },
      now: isoNow(clock),
    })
    lock?.heartbeat()

    const recipe = resolveTrustedNativeRecipe(task)
    const trustedPlan = recipe ? buildTrustedNativePlan(task) : null
    if (!recipe || !trustedPlan) {
      task = await taskStore.transition({
        ...leaseArgs(task, safeWorkerId),
        transitionId: transitionId(task.id, 'recipe-not-available'),
        toState: 'blocked',
        patch: {
          message: '未找到与任务几何精确匹配的可信配方；结构工程辅助任务已安全停止。',
          blocker: { code: 'native_recipe_not_available', retryable: false },
          execution: {
            started: false,
            cadStarted: false,
            modelGenerated: false,
            modelReady: false,
            legacyFallbackUsed: false,
          },
          progress: { stage: 'blocked', percent: 10 },
        },
        now: isoNow(clock),
      })
      return { status: 'blocked', claimed: true, code: 'native_recipe_not_available', task }
    }

    let toolchainArtifacts
    let trustedToolchainManifests
    try {
      toolchainArtifacts = await toolchainArtifactResolver({ repositoryRoot: ROOT })
      trustedToolchainManifests = toolchainManifestMap(toolchainArtifacts)
    } catch (error) {
      task = await taskStore.transition({
        ...leaseArgs(task, safeWorkerId),
        transitionId: transitionId(task.id, 'toolchain-not-ready'),
        toState: 'blocked',
        patch: {
          message: '可信工具链尚未完整通过身份核对；结构工程辅助任务已安全停止。',
          blocker: {
            code: 'native_toolchain_not_ready',
            retryable: true,
            detailCode: String(error?.code || 'native_toolchain_manifest_map_invalid'),
          },
          execution: {
            started: false,
            cadStarted: false,
            modelGenerated: false,
            modelReady: false,
            legacyFallbackUsed: false,
          },
          progress: { stage: 'blocked', percent: 15 },
        },
        now: isoNow(clock),
      })
      return { status: 'blocked', claimed: true, code: 'native_toolchain_not_ready', task }
    }

    const attempt = Math.max(1, Number(task.nativeBuild?.attempt || 1))
    const safeTaskId = assertSafeIdentifier(task.id, 'taskId')
    const attemptDir = ensureUnderRoot(
      resolve(taskRoot, 'attempts', safeTaskId, `attempt-${String(attempt).padStart(4, '0')}`),
      taskRoot,
      'native build attempt',
    )
    const planPath = resolve(attemptDir, 'native_build_plan.json')
    const { artifact, digest } = buildImmutablePlanArtifact({
      task,
      recipe,
      trustedPlan,
      workerId: safeWorkerId,
      now: isoNow(clock),
      trustedToolchainManifests,
    })
    const writeResult = atomicWriteJson(planPath, artifact, { immutable: true })
    const blueprint = await executionBlueprintBuilder({
      attemptDir,
      planPath,
      taskId: task.id,
      repositoryRoot: ROOT,
      toolchainArtifacts,
      task,
      recipe,
      trustedPlan,
      planArtifact: artifact,
    })
    task = await taskStore.heartbeat({ ...leaseArgs(task, safeWorkerId), now: isoNow(clock) })
    task = await taskStore.transition({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'building'),
      toState: 'building',
      patch: {
        message: '不可变结构工程辅助计划已冻结，正按原子阶段执行。',
        execution: {
          started: true,
          cadStarted: false,
          modelGenerated: false,
          modelReady: false,
          legacyFallbackUsed: false,
        },
        progress: { stage: 'building', percent: 20 },
        planArtifact: {
          attempt,
          fileName: 'native_build_plan.json',
          digest,
          fileSha256: writeResult.sha256,
          immutable: true,
          recipeId: artifact.recipe.id,
          recipeVersion: artifact.recipe.version,
          recipeDigest: artifact.recipe.digest,
        },
      },
      now: isoNow(clock),
    })

    let currentInventoryDigest = requiredDigest(
      await inventoryDigest(blueprint?.source, { expectedFileCount: null }),
      'initial native inventory',
    )
    const seedInventoryDigest = currentInventoryDigest
    const stageResults = []
    let finalEvidence = null
    let doorInputs = null

    for (const phase of NATIVE_STAGE_RECEIPT_ORDER) {
      let latest = await taskStore.read(task.id)
      if (latest.nativeBuild?.state === 'cancel_requested') {
        return await cancelClaimedTask({
          taskStore,
          task: latest,
          workerId: safeWorkerId,
          clock,
          suffix: `cancel-before-${phase}`,
        })
      }
      if (latest.nativeBuild?.state !== 'building') {
        const error = new Error(`native task left building state before ${phase}`)
        error.code = 'native_worker_task_state_invalid'
        throw error
      }
      task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      lock?.heartbeat()

      if (phase === 'door_module_888x14') {
        doorInputs = await prepareDoorInputs({
          blueprint,
          task,
          lease: task.nativeBuild.lease,
          workerId: safeWorkerId,
          repositoryRoot: ROOT,
        })
        latest = await taskStore.read(task.id)
        if (latest.nativeBuild?.state === 'cancel_requested') {
          return await cancelClaimedTask({
            taskStore,
            task: latest,
            workerId: safeWorkerId,
            clock,
            suffix: 'cancel-after-door-inputs',
          })
        }
        task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      }

      const unit = Array.isArray(blueprint?.units)
        ? blueprint.units.find((candidate) => candidate?.receiptPhase === phase)
        : null
      const rawStageResult = await runStageUnit({
        blueprint,
        unit,
        phase,
        task,
        lease: task.nativeBuild.lease,
        workerId: safeWorkerId,
        attempt,
        toolchainArtifacts,
        seedInventoryDigest,
        preInventoryDigest: currentInventoryDigest,
        inventoryDigest,
        doorInputs,
        isCancellationRequested: () => false,
        onInstanceLockHeartbeat: () => lock?.heartbeat(),
      })
      const stageResult = normalizedStageResult({ unit, phase, result: rawStageResult })
      stageResults.push(stageResult)
      currentInventoryDigest = stageResult.postInventoryDigest

      latest = await taskStore.read(task.id)
      if (latest.nativeBuild?.state === 'cancel_requested') {
        return await cancelClaimedTask({
          taskStore,
          task: latest,
          workerId: safeWorkerId,
          clock,
          suffix: `cancel-after-${phase}`,
        })
      }
      task = await taskStore.heartbeat({ ...leaseArgs(latest, safeWorkerId), now: isoNow(clock) })
      lock?.heartbeat()

      if (phase === 'root_assembly_888x14') {
        const lockResult = await runLockValidation({
          blueprint,
          task,
          lease: task.nativeBuild.lease,
          workerId: safeWorkerId,
          toolchainArtifacts,
          preInventoryDigest: currentInventoryDigest,
          receipts: stageResults.map((result) => result.receipt),
          onInstanceLockHeartbeat: () => lock?.heartbeat(),
        })
        finalEvidence = lockValidationEvidence(lockResult, blueprint)
      }
    }

    task = await taskStore.transition({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'validating'),
      toState: 'validating',
      patch: {
        message: '九个原子阶段已收口，正在绑定最终结构工程辅助证据。',
        progress: { stage: 'validating', percent: 95 },
        validation: {
          stageReceiptCount: stageResults.length,
          stageReceiptOrder: [...NATIVE_STAGE_RECEIPT_ORDER],
          lockValidationBound: Boolean(finalEvidence),
        },
      },
      now: isoNow(clock),
    })
    const publication = await publishNativeModel({
      dataDir,
      blueprint,
      task,
      recipe,
      stageResults,
      finalValidationEvidence: finalEvidence,
      clock,
    })
    const result = completionResult(stageResults, finalEvidence, publication)
    task = await taskStore.complete({
      ...leaseArgs(task, safeWorkerId),
      transitionId: transitionId(task.id, 'complete'),
      result,
      patch: {
        message: '结构工程辅助包已通过全部原子收据与最终证据绑定。',
        progress: { stage: 'ready', percent: 100 },
        execution: {
          started: true,
          cadStarted: runStageUnit === runAuthorizedNativeStageUnit,
          modelGenerated: true,
          modelReady: true,
          legacyFallbackUsed: false,
        },
        validation: {
          stageReceiptCount: stageResults.length,
          stageReceiptOrder: [...NATIVE_STAGE_RECEIPT_ORDER],
          lockValidationBound: true,
          finalReceiptBound: true,
          packageManifestBound: true,
        },
      },
      now: isoNow(clock),
    })
    return { status: 'completed', claimed: true, task }
  } catch (error) {
    if (!task) throw error
    let latest = task
    try { latest = await taskStore.read(task.id) } catch {}
    if (latest.nativeBuild?.state === 'cancel_requested') {
      return await cancelClaimedTask({
        taskStore,
        task: latest,
        workerId: safeWorkerId,
        clock,
        suffix: 'cancel-after-execution-error',
      })
    }
    if (!['claimed', 'planning', 'building', 'validating'].includes(latest.nativeBuild?.state)) throw error
    const code = failureCode(error)
    task = await taskStore.fail({
      ...leaseArgs(latest, safeWorkerId),
      transitionId: transitionId(latest.id, 'failed'),
      code,
      message: error instanceof Error ? error.message : String(error),
      retryable: false,
      patch: {
        message: '结构工程辅助任务已失败关闭，未标记模型就绪。',
        progress: { stage: 'failed', percent: Number(latest.nativeBuild?.progress?.percent || 0) },
        execution: {
          started: latest.nativeBuild?.executionStarted === true,
          cadStarted: latest.nativeBuild?.execution?.cadStarted === true,
          modelGenerated: false,
          modelReady: false,
          legacyFallbackUsed: false,
        },
      },
      now: isoNow(clock),
    })
    return { status: 'failed', claimed: true, code, task }
  } finally {
    lock?.release()
  }
}

function parseCliArguments(argv) {
  let mode = ''
  let pollMs = DEFAULT_POLL_MS
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index]
    if (argument === '--once' || argument === '--daemon') {
      if (mode) throw new Error('choose exactly one worker mode: --once or --daemon')
      mode = argument.slice(2)
      continue
    }
    if (argument === '--poll-ms') {
      pollMs = Number(argv[index + 1])
      index += 1
      continue
    }
    if (argument.startsWith('--poll-ms=')) {
      pollMs = Number(argument.slice('--poll-ms='.length))
      continue
    }
    throw new Error(`unsupported worker argument: ${argument}`)
  }
  if (!mode) throw new Error('worker mode is required: --once or --daemon')
  if (!Number.isInteger(pollMs) || pollMs < MIN_POLL_MS || pollMs > MAX_POLL_MS) {
    throw new Error(`--poll-ms must be an integer from ${MIN_POLL_MS} to ${MAX_POLL_MS}`)
  }
  return { mode, pollMs }
}

function wait(milliseconds) {
  return new Promise((resolveWait) => setTimeout(resolveWait, milliseconds))
}

async function runCli() {
  const options = parseCliArguments(process.argv.slice(2))
  const dataDir = process.env.STUDIO_REVIEW_DATA_DIR || resolve(ROOT, 'data')
  const workerId = `native-worker-${process.pid}-${randomBytes(4).toString('hex')}`
  if (options.mode === 'once') {
    const result = await runNativeWorkerOnce({ dataDir, workerId })
    process.stdout.write(`${JSON.stringify(result)}\n`)
    return
  }

  let stopping = false
  process.once('SIGINT', () => { stopping = true })
  process.once('SIGTERM', () => { stopping = true })
  const daemonLock = acquireNativeWorkerInstanceLock({ dataDir, workerId })
  try {
    while (!stopping) {
      daemonLock.heartbeat()
      const result = await runNativeWorkerOnce({ dataDir, workerId, instanceLock: false })
      process.stdout.write(`${JSON.stringify(result)}\n`)
      await wait(options.pollMs)
    }
  } finally {
    daemonLock.release()
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : ''
if (invokedPath && invokedPath === fileURLToPath(import.meta.url)) {
  runCli().catch((error) => {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`)
    process.exitCode = 1
  })
}
