import { createHash, randomBytes } from 'node:crypto'
import {
  closeSync,
  existsSync,
  fstatSync,
  fsyncSync,
  lstatSync,
  linkSync,
  mkdirSync,
  openSync,
  readSync,
  readFileSync,
  readdirSync,
  realpathSync,
  statSync,
  unlinkSync,
  writeFileSync,
  writeSync,
} from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  nativeEvidenceCommitmentSha256,
} from './locker_16029_native_stage_contract.mjs'
import { nativeRecipeDigest } from '../locker_16029_native_recipe_registry.mjs'

const PURPOSE = 'structure_engineering_assistance'
const MANIFEST_SCHEMA = 'winnsen.native_structure_assistance_package_manifest.v1'
const RESULT_SCHEMA = 'winnsen.16029.native_final_pack_result.v1'
const RECEIPT_SCHEMA = 'winnsen.16029.native_final_pack_receipt.v1'
const VALIDATION_SCHEMA = 'winnsen.locker16029.native_888x14_lock_topology_validation.v2'
const FINAL_PHASE = 'final_pack_and_relocated_reopen'
const FINAL_EVIDENCE_RELATIVE = 'evidence/final_pack_and_relocated_reopen.result.v1.json'
const FINAL_RECEIPT_RELATIVE = 'receipts/final_pack_and_relocated_reopen.json'
const VALIDATION_RELATIVE = 'evidence/lock_topology_888x14.validation.v2.json'
const PACKAGE_RELATIVE = 'final_native_package'
const EXPECTED_FILE_COUNT = 55
const EXPECTED_ASSEMBLY_COUNT = 14
const EXPECTED_PART_COUNT = 41

const FINAL_EVIDENCE_KEYS = Object.freeze([
  'schema', 'purpose', 'status', 'success', 'engineerReviewRequired', 'completed_at_utc',
  'task', 'recipe', 'plan', 'tool', 'authorization', 'predecessorRootReceiptSha256',
  'paths', 'preInventory', 'postInventory', 'packaging', 'finalVerification',
  'relocatedVerification', 'sessions', 'processes', 'transaction', 'quality_boundary',
  'authorization_checkpoints', 'backup_deleted', 'backup_delete_error',
  'evidence_commitment_sha256',
])
const MANIFEST_KEYS = Object.freeze([
  'schema', 'purpose', 'createdAt', 'task', 'request', 'recipe', 'attempt',
  'validationEvidence', 'finalStageEvidence', 'finalStageReceipt', 'inventory',
  'archive', 'qualityBoundary',
])

function fail(code, message) {
  const error = new Error(message)
  error.code = code
  throw error
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256(readFileSync(path))
}

function exactKeys(value, expected) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function safeIdentifier(value, label) {
  const text = String(value || '')
  if (!/^[A-Za-z0-9_.-]{1,128}$/.test(text)) fail('NATIVE_PUBLICATION_ID_INVALID', `${label} is invalid`)
  return text
}

function digest(value, label) {
  const text = String(value || '').toUpperCase()
  if (!/^[A-F0-9]{64}$/.test(text)) fail('NATIVE_PUBLICATION_BINDING_INVALID', `${label} must be SHA-256`)
  return text
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function samePath(left, right) {
  const a = resolve(left)
  const b = resolve(right)
  return process.platform === 'win32' ? a.toUpperCase() === b.toUpperCase() : a === b
}

function assertSafeDirectory(path, label) {
  const absolute = resolve(path)
  if (!existsSync(absolute)) fail('NATIVE_PUBLICATION_PATH_INVALID', `${label} is missing`)
  if (lstatSync(absolute).isSymbolicLink() || !statSync(absolute).isDirectory() ||
      !samePath(realpathSync.native(absolute), absolute)) {
    fail('NATIVE_PUBLICATION_PATH_UNSAFE', `${label} must be a real directory`)
  }
  return absolute
}

function assertSafeFile(path, root, label) {
  const absolute = resolve(path)
  const safeRoot = assertSafeDirectory(root, `${label} root`)
  if (!underRoot(absolute, safeRoot) || absolute === safeRoot || !existsSync(absolute)) {
    fail('NATIVE_PUBLICATION_PATH_INVALID', `${label} is missing or outside its root`)
  }
  let current = absolute
  while (!samePath(current, safeRoot)) {
    if (lstatSync(current).isSymbolicLink()) fail('NATIVE_PUBLICATION_PATH_UNSAFE', `${label} contains a link`)
    current = dirname(current)
  }
  const info = statSync(absolute)
  if (!info.isFile() || Number(info.nlink) !== 1 || !underRoot(realpathSync.native(absolute), safeRoot)) {
    fail('NATIVE_PUBLICATION_PATH_UNSAFE', `${label} must be a single-link regular file`)
  }
  return absolute
}

function ordinalCompare(left, right) {
  const limit = Math.min(left.length, right.length)
  for (let index = 0; index < limit; index += 1) {
    const difference = left.charCodeAt(index) - right.charCodeAt(index)
    if (difference) return difference
  }
  return left.length - right.length
}

function ordinalIgnoreCaseKey(value) {
  let result = ''
  for (let index = 0; index < value.length; index += 1) {
    const original = value[index]
    const upper = original.toUpperCase()
    result += upper.length === 1 ? upper : original
  }
  return result
}

function ordinalIgnoreCase(left, right) {
  return ordinalCompare(ordinalIgnoreCaseKey(left), ordinalIgnoreCaseKey(right)) || ordinalCompare(left, right)
}

function captureInventory(directory) {
  const root = assertSafeDirectory(directory, 'final native package')
  const rows = []
  const names = new Set()
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    if (!entry.isFile() || entry.isSymbolicLink?.()) {
      fail('NATIVE_PUBLICATION_INVENTORY_INVALID', 'final package must remain flat and contain only files')
    }
    const extension = entry.name.slice(entry.name.lastIndexOf('.')).toUpperCase()
    if (!['.SLDASM', '.SLDPRT'].includes(extension)) {
      fail('NATIVE_PUBLICATION_INVENTORY_INVALID', 'final package contains a non-CAD file')
    }
    const key = ordinalIgnoreCaseKey(entry.name)
    if (names.has(key)) fail('NATIVE_PUBLICATION_INVENTORY_INVALID', 'final package contains a case-colliding file name')
    names.add(key)
    const path = assertSafeFile(resolve(root, entry.name), root, `final CAD ${entry.name}`)
    const info = statSync(path)
    rows.push(Object.freeze({ name: entry.name, path, sizeBytes: info.size, sha256: sha256File(path), extension }))
  }
  rows.sort((left, right) => ordinalIgnoreCase(left.name, right.name))
  const assemblyFileCount = rows.filter((row) => row.extension === '.SLDASM').length
  const partFileCount = rows.filter((row) => row.extension === '.SLDPRT').length
  if (rows.length !== EXPECTED_FILE_COUNT || assemblyFileCount !== EXPECTED_ASSEMBLY_COUNT ||
      partFileCount !== EXPECTED_PART_COUNT) {
    fail('NATIVE_PUBLICATION_INVENTORY_INVALID', 'final package must be exact 55 = 14 SLDASM + 41 SLDPRT')
  }
  const inventoryDigest = sha256(Buffer.from(rows.map((row) => `${row.name}|${row.sha256}\n`).join(''), 'utf8'))
  return Object.freeze({ rows: Object.freeze(rows), digest: inventoryDigest, assemblyFileCount, partFileCount })
}

function readJsonFile(path, root, label) {
  const safePath = assertSafeFile(path, root, label)
  let value
  try { value = JSON.parse(readFileSync(safePath, 'utf8')) } catch {
    fail('NATIVE_PUBLICATION_EVIDENCE_INVALID', `${label} is not JSON`)
  }
  return { path: safePath, sha256: sha256File(safePath), value }
}

function manifestReference(row, schema) {
  return Object.freeze({ path: row.path, sha256: row.sha256, schema })
}

function validateValidationEvidence(row, supplied) {
  const value = row.value
  const suppliedPath = supplied?.path ? resolve(supplied.path) : row.path
  if (suppliedPath !== row.path || (supplied?.sha256 && digest(supplied.sha256, 'validation evidence') !== row.sha256) ||
      value?.schema !== VALIDATION_SCHEMA || value?.purpose !== PURPOSE || value?.status !== 'PASS' ||
      value?.structureEngineeringEvidencePass !== true || Number(value?.failedCheckCount) !== 0 ||
      !Array.isArray(value?.failedChecks) || value.failedChecks.length !== 0) {
    fail('NATIVE_PUBLICATION_VALIDATION_INVALID', 'lock validation evidence is not an exact PASS')
  }
}

function validateFinalEvidence(row, { task, recipe, recipeDigest, attempt, planSha256, inventory, finalPackage }) {
  const value = row.value
  if (!exactKeys(value, FINAL_EVIDENCE_KEYS) || value.schema !== RESULT_SCHEMA || value.purpose !== PURPOSE ||
      value.status !== 'STRUCTURE_ENGINEERING_ASSISTANCE_FINAL_PACK_PASS' || value.success !== true ||
      value.engineerReviewRequired !== true || value.task?.id !== task.id || Number(value.task?.attempt) !== attempt ||
      value.recipe?.id !== recipe.id || Number(value.recipe?.version) !== Number(recipe.version) ||
      digest(value.recipe?.digest, 'final evidence recipe') !== recipeDigest ||
      digest(value.plan?.sha256, 'final evidence plan') !== planSha256 ||
      !samePath(value.paths?.finalPackage || '', finalPackage) || value.finalVerification?.passed !== true ||
      Number(value.finalVerification?.openErrors) !== 0 || Number(value.finalVerification?.openWarnings) !== 32 ||
      value.relocatedVerification?.passed !== true || Number(value.relocatedVerification?.openErrors) !== 0 ||
      Number(value.relocatedVerification?.openWarnings) !== 32 || value.processes?.finalGate !== true ||
      !Array.isArray(value.processes?.finalSldworks) || value.processes.finalSldworks.length !== 0 ||
      !Array.isArray(value.processes?.finalSldprocmon) || value.processes.finalSldprocmon.length !== 0 ||
      !Array.isArray(value.processes?.cleanupErrors) || value.processes.cleanupErrors.length !== 0 ||
      value.transaction?.inputUnchanged !== true || value.transaction?.relocatedProbeRemoved !== true ||
      value.quality_boundary?.purpose !== PURPOSE || value.quality_boundary?.engineerReviewRequired !== true) {
    fail('NATIVE_PUBLICATION_FINAL_EVIDENCE_INVALID', 'final stage evidence did not preserve its required gates')
  }
  const post = value.postInventory
  if (post?.digest !== inventory.digest || Number(post?.fileCount) !== EXPECTED_FILE_COUNT ||
      Number(post?.assemblyFileCount) !== EXPECTED_ASSEMBLY_COUNT || Number(post?.partFileCount) !== EXPECTED_PART_COUNT ||
      !Array.isArray(post?.files) || post.files.length !== EXPECTED_FILE_COUNT) {
    fail('NATIVE_PUBLICATION_FINAL_EVIDENCE_INVALID', 'final evidence inventory summary differs from live CAD')
  }
  const evidenceRows = new Map(post.files.map((item) => [String(item?.name || ''), item]))
  for (const live of inventory.rows) {
    const recorded = evidenceRows.get(live.name)
    if (!recorded || digest(recorded.sha256, `recorded ${live.name}`) !== live.sha256 ||
        Number(recorded.sizeBytes) !== live.sizeBytes) {
      fail('NATIVE_PUBLICATION_FINAL_EVIDENCE_INVALID', `final evidence inventory differs for ${live.name}`)
    }
  }
  const commitment = digest(value.evidence_commitment_sha256, 'final evidence commitment')
  if (nativeEvidenceCommitmentSha256(value) !== commitment) {
    fail('NATIVE_PUBLICATION_FINAL_EVIDENCE_INVALID', 'final evidence semantic commitment does not match')
  }
  return commitment
}

function validateFinalReceipt(row, { task, recipe, recipeDigest, attempt, plan, evidence, inventory, commitment }) {
  const value = row.value
  if (!exactKeys(value, NATIVE_RUNTIME_RECEIPT_KEYS) || value.schema !== RECEIPT_SCHEMA ||
      value.phase !== FINAL_PHASE || value.success !== true || value.taskId !== task.id ||
      Number(value.taskRevision) !== Number(plan.task?.revisionAtPlanning) || value.taskDigest !== plan.task?.digest ||
      value.requestDigest !== plan.request?.digest || Number(value.attempt) !== attempt ||
      digest(value.planSha256, 'final receipt plan') !== sha256File(resolve(plan.__path)) ||
      value.recipeId !== recipe.id || digest(value.recipeDigest, 'final receipt recipe') !== recipeDigest ||
      value.toolId !== 'native_final_pack_888x14_v1' || value.evidencePath !== FINAL_EVIDENCE_RELATIVE ||
      digest(value.evidenceSha256, 'final receipt evidence') !== evidence.sha256 ||
      digest(value.evidenceCommitmentSha256, 'final receipt commitment') !== commitment ||
      digest(value.postInventoryDigest, 'final receipt inventory') !== inventory.digest) {
    fail('NATIVE_PUBLICATION_FINAL_RECEIPT_INVALID', 'final receipt does not bind the live final stage')
  }
}

let crcTable = null
function crc32(buffer) {
  if (!crcTable) {
    crcTable = Array.from({ length: 256 }, (_, index) => {
      let value = index
      for (let bit = 0; bit < 8; bit += 1) value = (value & 1) ? (0xEDB88320 ^ (value >>> 1)) : (value >>> 1)
      return value >>> 0
    })
  }
  let value = 0xFFFFFFFF
  for (const byte of buffer) value = crcTable[(value ^ byte) & 0xFF] ^ (value >>> 8)
  return (value ^ 0xFFFFFFFF) >>> 0
}

function localHeader(nameBytes, size, checksum) {
  const value = Buffer.alloc(30)
  value.writeUInt32LE(0x04034B50, 0)
  value.writeUInt16LE(20, 4)
  value.writeUInt16LE(0x0800, 6)
  value.writeUInt16LE(0, 8)
  value.writeUInt16LE(0, 10)
  value.writeUInt16LE(33, 12)
  value.writeUInt32LE(checksum, 14)
  value.writeUInt32LE(size, 18)
  value.writeUInt32LE(size, 22)
  value.writeUInt16LE(nameBytes.length, 26)
  return value
}

function centralHeader(nameBytes, size, checksum, offset) {
  const value = Buffer.alloc(46)
  value.writeUInt32LE(0x02014B50, 0)
  value.writeUInt16LE(20, 4)
  value.writeUInt16LE(20, 6)
  value.writeUInt16LE(0x0800, 8)
  value.writeUInt16LE(0, 10)
  value.writeUInt16LE(0, 12)
  value.writeUInt16LE(33, 14)
  value.writeUInt32LE(checksum, 16)
  value.writeUInt32LE(size, 20)
  value.writeUInt32LE(size, 24)
  value.writeUInt16LE(nameBytes.length, 28)
  value.writeUInt32LE(offset, 42)
  return value
}

function endRecord(entryCount, centralSize, centralOffset) {
  const value = Buffer.alloc(22)
  value.writeUInt32LE(0x06054B50, 0)
  value.writeUInt16LE(entryCount, 8)
  value.writeUInt16LE(entryCount, 10)
  value.writeUInt32LE(centralSize, 12)
  value.writeUInt32LE(centralOffset, 16)
  return value
}

function createArchive(descriptor, inventory, rootDirectory) {
  let offset = 0
  const central = []
  const write = (buffer) => { writeSync(descriptor, buffer); offset += buffer.length }
  for (const row of inventory.rows) {
    const data = readFileSync(row.path)
    if (sha256(data) !== row.sha256) fail('NATIVE_PUBLICATION_INVENTORY_CHANGED', `${row.name} changed during archive creation`)
    const nameBytes = Buffer.from(`${rootDirectory}/${row.name}`, 'utf8')
    const checksum = crc32(data)
    const localOffset = offset
    write(localHeader(nameBytes, data.length, checksum))
    write(nameBytes)
    write(data)
    central.push(Buffer.concat([centralHeader(nameBytes, data.length, checksum, localOffset), nameBytes]))
  }
  const centralOffset = offset
  for (const row of central) write(row)
  write(endRecord(central.length, offset - centralOffset, centralOffset))
}

function atomicCreate(path, writer) {
  if (existsSync(path)) fail('NATIVE_PUBLICATION_IMMUTABLE_CONFLICT', `${basename(path)} already exists`)
  mkdirSync(dirname(path), { recursive: true })
  const temp = resolve(dirname(path), `.${basename(path)}.tmp-${process.pid}-${randomBytes(8).toString('hex')}`)
  let descriptor = null
  let ownedIdentity = null
  try {
    descriptor = openSync(temp, 'wx')
    writer(descriptor)
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    const temporaryInfo = statSync(temp)
    linkSync(temp, path)
    ownedIdentity = { dev: Number(temporaryInfo.dev), ino: Number(temporaryInfo.ino) }
    unlinkSync(temp)
    assertSafeFile(path, dirname(path), `published ${basename(path)}`)
    return ownedIdentity
  } catch (error) {
    if (descriptor !== null) closeSync(descriptor)
    if (existsSync(temp)) unlinkSync(temp)
    if (ownedIdentity && existsSync(path)) {
      const current = statSync(path)
      if (Number(current.dev) === ownedIdentity.dev && Number(current.ino) === ownedIdentity.ino) unlinkSync(path)
    }
    throw error
  }
}

function removeOwnedPublishedFile(path, identity) {
  if (!identity || !existsSync(path)) return false
  const current = statSync(path)
  if (Number(current.dev) !== identity.dev || Number(current.ino) !== identity.ino) return false
  unlinkSync(path)
  return true
}

function writeImmutableJson(path, value) {
  const bytes = Buffer.from(`${JSON.stringify(value, null, 2)}\n`, 'utf8')
  atomicCreate(path, (descriptor) => writeFileSync(descriptor, bytes))
  if (!readFileSync(path).equals(bytes)) fail('NATIVE_PUBLICATION_COMMIT_INVALID', 'package manifest bytes differ after commit')
}

function publicationPaths(dataDir, taskId, attempt) {
  const dataRoot = assertSafeDirectory(dataDir, 'portal data root')
  const publicationRoot = resolve(dataRoot, 'native_model_publications')
  mkdirSync(publicationRoot, { recursive: true })
  assertSafeDirectory(publicationRoot, 'native publication root')
  const taskRoot = resolve(publicationRoot, safeIdentifier(taskId, 'taskId'))
  const attemptRoot = resolve(taskRoot, `attempt-${String(attempt).padStart(4, '0')}`)
  if (!underRoot(attemptRoot, publicationRoot)) fail('NATIVE_PUBLICATION_PATH_INVALID', 'publication path escaped its root')
  mkdirSync(attemptRoot, { recursive: true })
  assertSafeDirectory(attemptRoot, 'native publication attempt')
  return {
    publicationRoot,
    attemptRoot,
    archivePath: resolve(attemptRoot, '16029_888W_14door_structure_assistance.zip'),
    manifestPath: resolve(attemptRoot, 'package_manifest.json'),
  }
}

export function publishNativeStructureAssistance({
  dataDir,
  blueprint,
  task,
  recipe,
  stageResults,
  finalValidationEvidence,
  clock = () => new Date(),
} = {}) {
  if (!blueprint || blueprint.schema !== 'winnsen.native_888x14_execution_blueprint.v1' ||
      !task || task.nativeBuild?.state !== 'validating' || task.nativeBuild?.modelReady === true ||
      task.legacyFallbackUsed === true || task.nativeBuild?.legacyFallbackUsed === true) {
    fail('NATIVE_PUBLICATION_TASK_INVALID', 'only a validating native assistance task can be published')
  }
  const attemptRoot = assertSafeDirectory(blueprint.attemptDir, 'native attempt root')
  const attempt = Number(task.nativeBuild?.attempt)
  if (!Number.isInteger(attempt) || attempt < 1) fail('NATIVE_PUBLICATION_TASK_INVALID', 'task attempt is invalid')
  const results = Array.isArray(stageResults) ? stageResults : []
  if (JSON.stringify(results.map((row) => row?.phase)) !== JSON.stringify(NATIVE_STAGE_RECEIPT_ORDER)) {
    fail('NATIVE_PUBLICATION_STAGE_CHAIN_INVALID', 'all nine stage results are required in order')
  }
  const planRow = readJsonFile(resolve(attemptRoot, 'native_build_plan.json'), attemptRoot, 'native build plan')
  const plan = planRow.value
  plan.__path = planRow.path
  let expectedRecipeDigest
  try { expectedRecipeDigest = digest(nativeRecipeDigest(recipe), 'recipe') } catch {
    fail('NATIVE_PUBLICATION_PLAN_INVALID', 'trusted recipe is invalid')
  }
  if (plan?.schema !== 'winnsen.native_build_plan.v1' || plan?.purpose !== PURPOSE || plan?.task?.id !== task.id ||
      plan?.request?.fingerprint !== task.requestFingerprint || plan?.recipe?.id !== recipe?.id ||
      Number(plan?.recipe?.version) !== Number(recipe?.version) ||
      digest(plan?.recipe?.digest, 'plan recipe') !== expectedRecipeDigest) {
    fail('NATIVE_PUBLICATION_PLAN_INVALID', 'immutable plan does not match the task and recipe')
  }
  const finalPackage = assertSafeDirectory(resolve(attemptRoot, PACKAGE_RELATIVE), 'final native package')
  const inventory = captureInventory(finalPackage)
  const validation = readJsonFile(resolve(attemptRoot, VALIDATION_RELATIVE), attemptRoot, 'lock validation evidence')
  validateValidationEvidence(validation, finalValidationEvidence)
  const evidence = readJsonFile(resolve(attemptRoot, FINAL_EVIDENCE_RELATIVE), attemptRoot, 'final stage evidence')
  const commitment = validateFinalEvidence(evidence, {
    task, recipe, recipeDigest: expectedRecipeDigest, attempt,
    planSha256: planRow.sha256, inventory, finalPackage,
  })
  const receipt = readJsonFile(resolve(attemptRoot, FINAL_RECEIPT_RELATIVE), attemptRoot, 'final stage receipt')
  validateFinalReceipt(receipt, {
    task, recipe, recipeDigest: expectedRecipeDigest, attempt, plan, evidence, inventory, commitment,
  })
  const finalResult = results.at(-1)
  if (resolve(finalResult?.receipt?.path || '') !== receipt.path || digest(finalResult?.receipt?.sha256, 'stage result receipt') !== receipt.sha256 ||
      resolve(finalResult?.evidence?.path || '') !== evidence.path || digest(finalResult?.evidence?.sha256, 'stage result evidence') !== evidence.sha256 ||
      digest(finalResult?.postInventoryDigest, 'stage result inventory') !== inventory.digest) {
    fail('NATIVE_PUBLICATION_STAGE_CHAIN_INVALID', 'worker final stage references differ from live artifacts')
  }

  const paths = publicationPaths(dataDir, task.id, attempt)
  const rootDirectory = `16029_888W_14door_${safeIdentifier(task.id, 'taskId')}`
  let archiveOwned = false
  let archiveIdentity = null
  try {
    archiveIdentity = atomicCreate(paths.archivePath,
      (descriptor) => createArchive(descriptor, inventory, rootDirectory))
    archiveOwned = true
    const archiveInfo = statSync(paths.archivePath)
    const archive = Object.freeze({
      fileName: basename(paths.archivePath),
      sizeBytes: archiveInfo.size,
      sha256: sha256File(paths.archivePath),
      entryCount: EXPECTED_FILE_COUNT,
      rootDirectory,
    })
    const nowValue = clock()
    const manifest = {
      schema: MANIFEST_SCHEMA,
      purpose: PURPOSE,
      createdAt: (nowValue instanceof Date ? nowValue : new Date(nowValue)).toISOString(),
      task: { id: task.id, revision: Number(task.revision) },
      request: { fingerprint: String(task.requestFingerprint || '') },
      recipe: { id: recipe.id, version: Number(recipe.version), digest: expectedRecipeDigest },
      attempt,
      validationEvidence: manifestReference(validation, VALIDATION_SCHEMA),
      finalStageEvidence: manifestReference(evidence, RESULT_SCHEMA),
      finalStageReceipt: manifestReference(receipt, RECEIPT_SCHEMA),
      inventory: {
        digest: inventory.digest,
        fileCount: EXPECTED_FILE_COUNT,
        assemblyFileCount: EXPECTED_ASSEMBLY_COUNT,
        partFileCount: EXPECTED_PART_COUNT,
        files: inventory.rows.map(({ name, sizeBytes, sha256: fileSha256 }) => ({ name, sizeBytes, sha256: fileSha256 })),
      },
      archive,
      qualityBoundary: {
        purpose: PURPOSE,
        structureAssistanceReady: true,
        engineerReviewRequired: true,
        allRequiredChecksPassed: true,
      },
    }
    if (!exactKeys(manifest, MANIFEST_KEYS)) fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'package manifest shape is invalid')
    writeImmutableJson(paths.manifestPath, manifest)
    return Object.freeze({
      manifest: Object.freeze({ path: paths.manifestPath, sha256: sha256File(paths.manifestPath) }),
      archive: Object.freeze({ path: paths.archivePath, ...archive }),
    })
  } catch (error) {
    if (archiveOwned) removeOwnedPublishedFile(paths.archivePath, archiveIdentity)
    throw error
  }
}

export const NATIVE_STRUCTURE_ASSISTANCE_MANIFEST_SCHEMA = MANIFEST_SCHEMA

function exactReference(value, expectedPath, label) {
  if (!exactKeys(value, ['path', 'sha256']) || resolve(value.path || '') !== resolve(expectedPath) ||
      !/^[A-F0-9]{64}$/i.test(String(value.sha256 || ''))) {
    fail('NATIVE_PUBLICATION_RESULT_INVALID', `${label} reference is invalid`)
  }
  return { path: resolve(value.path), sha256: String(value.sha256).toUpperCase() }
}

function readPublicationBinding({ dataDir, task, verifyArchiveHash = false, keepArchiveOpen = false }) {
  if (!task || task.status !== 'native_assistance_model_ready' || task.nativeBuild?.state !== 'ready' ||
      task.nativeBuild?.modelReady !== true || task.legacyFallbackUsed === true ||
      task.nativeBuild?.legacyFallbackUsed === true) {
    fail('NATIVE_PUBLICATION_TASK_NOT_READY', 'native task is not in its completed assistance state')
  }
  const attempt = Number(task.nativeBuild?.attempt)
  if (!Number.isInteger(attempt) || attempt < 1) fail('NATIVE_PUBLICATION_RESULT_INVALID', 'native task attempt is invalid')
  const result = task.nativeBuild?.result
  if (!exactKeys(result, ['schema', 'validationEvidence', 'finalReceipt', 'packageManifest', 'archive']) ||
      result.schema !== 'winnsen.native_structure_engineering_assistance_result.v1') {
    fail('NATIVE_PUBLICATION_RESULT_INVALID', 'native task result shape is invalid')
  }
  const paths = publicationPathsForRead(dataDir, task.id, attempt)
  const packageManifest = exactReference(result.packageManifest, paths.manifestPath, 'package manifest')
  if (!exactKeys(result.archive, ['path', 'fileName', 'sha256', 'sizeBytes']) ||
      resolve(result.archive.path || '') !== paths.archivePath ||
      basename(String(result.archive.fileName || '')) !== basename(paths.archivePath) ||
      !/^[A-F0-9]{64}$/i.test(String(result.archive.sha256 || '')) ||
      !Number.isSafeInteger(Number(result.archive.sizeBytes)) || Number(result.archive.sizeBytes) <= 0) {
    fail('NATIVE_PUBLICATION_RESULT_INVALID', 'native task archive reference is invalid')
  }
  const manifestRow = readJsonFile(packageManifest.path, paths.attemptRoot, 'published package manifest')
  if (manifestRow.sha256 !== packageManifest.sha256) fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest hash changed')
  const manifest = manifestRow.value
  const plannedRecipe = task.nativeBuild?.planArtifact
  if (!exactKeys(manifest, MANIFEST_KEYS) || manifest.schema !== MANIFEST_SCHEMA || manifest.purpose !== PURPOSE ||
      manifest.task?.id !== task.id || manifest.request?.fingerprint !== task.requestFingerprint ||
      Number(manifest.attempt) !== attempt || manifest.recipe?.id !== plannedRecipe?.recipeId ||
      Number(manifest.recipe?.version) !== Number(plannedRecipe?.recipeVersion) ||
      String(manifest.recipe?.digest || '').toUpperCase() !== String(plannedRecipe?.recipeDigest || '').toUpperCase() ||
      !/^[A-F0-9]{64}$/i.test(String(manifest.recipe?.digest || '')) ||
      !exactKeys(manifest.archive, ['fileName', 'sizeBytes', 'sha256', 'entryCount', 'rootDirectory']) ||
      manifest.archive.fileName !== result.archive.fileName || Number(manifest.archive.sizeBytes) !== Number(result.archive.sizeBytes) ||
      String(manifest.archive.sha256).toUpperCase() !== String(result.archive.sha256).toUpperCase() ||
      Number(manifest.archive.entryCount) !== EXPECTED_FILE_COUNT ||
      !/^[A-Za-z0-9_.-]{1,200}$/.test(String(manifest.archive.rootDirectory || '')) ||
      !/^[A-F0-9]{64}$/i.test(String(manifest.inventory?.digest || '')) ||
      manifest.inventory?.fileCount !== EXPECTED_FILE_COUNT || manifest.inventory?.assemblyFileCount !== EXPECTED_ASSEMBLY_COUNT ||
      manifest.inventory?.partFileCount !== EXPECTED_PART_COUNT || !Array.isArray(manifest.inventory?.files) ||
      manifest.inventory.files.length !== EXPECTED_FILE_COUNT ||
      manifest.qualityBoundary?.purpose !== PURPOSE || manifest.qualityBoundary?.structureAssistanceReady !== true ||
      manifest.qualityBoundary?.engineerReviewRequired !== true || manifest.qualityBoundary?.allRequiredChecksPassed !== true) {
    fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest semantics are invalid')
  }
  const manifestInventoryNames = new Set()
  for (const row of manifest.inventory.files) {
    if (!exactKeys(row, ['name', 'sizeBytes', 'sha256']) || !/^[^\\/]{1,240}\.(?:SLDASM|SLDPRT)$/i.test(String(row.name || '')) ||
        !Number.isSafeInteger(Number(row.sizeBytes)) || Number(row.sizeBytes) <= 0 ||
        !/^[A-F0-9]{64}$/i.test(String(row.sha256 || ''))) {
      fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest inventory row is invalid')
    }
    const key = ordinalIgnoreCaseKey(row.name)
    if (manifestInventoryNames.has(key)) fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest inventory names collide')
    manifestInventoryNames.add(key)
  }
  if (manifest.validationEvidence?.schema !== VALIDATION_SCHEMA ||
      resolve(manifest.validationEvidence?.path || '') !== paths.validationPath ||
      manifest.finalStageEvidence?.schema !== RESULT_SCHEMA ||
      resolve(manifest.finalStageEvidence?.path || '') !== paths.finalEvidencePath ||
      manifest.finalStageReceipt?.schema !== RECEIPT_SCHEMA ||
      resolve(manifest.finalStageReceipt?.path || '') !== paths.finalReceiptPath) {
    fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest artifact paths are not canonical')
  }
  const validation = exactReference(result.validationEvidence, paths.validationPath, 'validation evidence')
  const finalReceipt = exactReference(result.finalReceipt, paths.finalReceiptPath, 'final receipt')
  if (validation.sha256 !== String(manifest.validationEvidence?.sha256 || '').toUpperCase() ||
      finalReceipt.sha256 !== String(manifest.finalStageReceipt?.sha256 || '').toUpperCase()) {
    fail('NATIVE_PUBLICATION_MANIFEST_INVALID', 'published manifest evidence references differ from the task result')
  }
  for (const [path, expectedSha256, label] of [
    [paths.validationPath, validation.sha256, 'validation evidence'],
    [paths.finalEvidencePath, String(manifest.finalStageEvidence.sha256).toUpperCase(), 'final stage evidence'],
    [paths.finalReceiptPath, finalReceipt.sha256, 'final stage receipt'],
  ]) {
    assertSafeFile(path, paths.buildAttemptRoot, label)
    if (sha256File(path) !== expectedSha256) fail('NATIVE_PUBLICATION_MANIFEST_INVALID', `${label} hash changed`)
  }
  assertSafeFile(paths.archivePath, paths.attemptRoot, 'published assistance archive')
  const beforeOpen = statSync(paths.archivePath)
  let descriptor = null
  try {
    descriptor = openSync(paths.archivePath, 'r')
    const info = fstatSync(descriptor)
    if (!info.isFile() || Number(info.nlink) !== 1 || Number(info.size) !== Number(result.archive.sizeBytes) ||
        Number(info.dev) !== Number(beforeOpen.dev) || Number(info.ino) !== Number(beforeOpen.ino)) {
      fail('NATIVE_PUBLICATION_ARCHIVE_INVALID', 'published archive identity or size changed')
    }
    const signature = Buffer.alloc(4)
    if (readSync(descriptor, signature, 0, 4, 0) !== 4 || signature.readUInt32LE(0) !== 0x04034B50) {
      fail('NATIVE_PUBLICATION_ARCHIVE_INVALID', 'published archive is not a ZIP file')
    }
    let liveSha256 = String(result.archive.sha256).toUpperCase()
    if (verifyArchiveHash) {
      const hasher = createHash('sha256')
      const buffer = Buffer.allocUnsafe(1024 * 1024)
      let position = 0
      while (position < info.size) {
        const count = readSync(descriptor, buffer, 0, Math.min(buffer.length, info.size - position), position)
        if (count <= 0) fail('NATIVE_PUBLICATION_ARCHIVE_INVALID', 'published archive ended during hash verification')
        hasher.update(buffer.subarray(0, count))
        position += count
      }
      liveSha256 = hasher.digest('hex').toUpperCase()
      if (liveSha256 !== String(result.archive.sha256).toUpperCase()) {
        fail('NATIVE_PUBLICATION_ARCHIVE_INVALID', 'published archive SHA-256 changed')
      }
    }
    const output = {
      manifest,
      manifestPath: paths.manifestPath,
      manifestSha256: manifestRow.sha256,
      archivePath: paths.archivePath,
      archiveFileName: result.archive.fileName,
      archiveSha256: liveSha256,
      stat: info,
      fd: keepArchiveOpen ? descriptor : null,
    }
    if (!keepArchiveOpen) {
      closeSync(descriptor)
      descriptor = null
    }
    return output
  } catch (error) {
    if (descriptor !== null) closeSync(descriptor)
    throw error
  }
}

function publicationPathsForRead(dataDir, taskId, attempt) {
  const dataRoot = assertSafeDirectory(dataDir, 'portal data root')
  const publicationRoot = assertSafeDirectory(resolve(dataRoot, 'native_model_publications'), 'native publication root')
  const taskRoot = assertSafeDirectory(resolve(publicationRoot, safeIdentifier(taskId, 'taskId')), 'native publication task')
  const attemptRoot = assertSafeDirectory(resolve(taskRoot, `attempt-${String(attempt).padStart(4, '0')}`), 'native publication attempt')
  if (!underRoot(attemptRoot, publicationRoot)) fail('NATIVE_PUBLICATION_PATH_INVALID', 'publication path escaped its root')
  const buildAttemptRoot = assertSafeDirectory(resolve(dataRoot, 'native_model_requests', 'attempts',
    safeIdentifier(taskId, 'taskId'), `attempt-${String(attempt).padStart(4, '0')}`), 'native build attempt')
  return {
    attemptRoot,
    buildAttemptRoot,
    archivePath: resolve(attemptRoot, '16029_888W_14door_structure_assistance.zip'),
    manifestPath: resolve(attemptRoot, 'package_manifest.json'),
    validationPath: resolve(buildAttemptRoot, VALIDATION_RELATIVE),
    finalEvidencePath: resolve(buildAttemptRoot, FINAL_EVIDENCE_RELATIVE),
    finalReceiptPath: resolve(buildAttemptRoot, FINAL_RECEIPT_RELATIVE),
  }
}

export function inspectNativeStructureAssistancePublication({ dataDir, task } = {}) {
  try {
    const binding = readPublicationBinding({ dataDir, task, verifyArchiveHash: false, keepArchiveOpen: false })
    return Object.freeze({
      ok: true,
      manifestSha256: binding.manifestSha256,
      archiveSha256: binding.archiveSha256,
      sizeBytes: binding.stat.size,
      fileName: binding.archiveFileName,
    })
  } catch (error) {
    return Object.freeze({ ok: false, reason: String(error?.code || 'NATIVE_PUBLICATION_INVALID') })
  }
}

export function openVerifiedNativeStructureAssistanceArchive({ dataDir, task } = {}) {
  return readPublicationBinding({ dataDir, task, verifyArchiveHash: true, keepArchiveOpen: true })
}
