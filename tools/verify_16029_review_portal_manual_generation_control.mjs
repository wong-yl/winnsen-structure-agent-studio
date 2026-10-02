import assert from 'node:assert/strict'
import { Script } from 'node:vm'
import { createHash, randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import { createServer as createNetServer } from 'node:net'
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_16029_SEEDS,
  normalize16029NativeModelRequest,
} from './locker_16029_native_generator.mjs'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from './locker_16029_native_recipe_registry.mjs'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { validate16029ParametricPublication } from './lib/locker_16029_parametric_publication.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const TEMP_ROOT = resolve(ROOT, 'tmp/verify_16029_native_portal')
const DATA_DIR = resolve(TEMP_ROOT, 'data')
const STORAGE_ROOT = resolve(TEMP_ROOT, 'storage')
const MODEL_DIR = resolve(STORAGE_ROOT, '模型下载')
const NATIVE_REQUEST_DIR = resolve(DATA_DIR, 'native_model_requests')
const NATIVE_INDEX_PATH = resolve(NATIVE_REQUEST_DIR, 'native_model_request_index.json')

const ASSETS = [
  {
    id: '16029-v43-v37-760w-six-door-engineering-assistance-zip',
    fileName: '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip',
    source: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'),
  },
  {
    id: '16029-v43-v36-four-door-engineering-assistance-zip',
    fileName: '16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip',
    source: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v36-four-door-l66-r66-r2/16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip'),
  },
  {
    id: '16029-v43-v35-one-door-one-lock-hole-rereview-zip',
    fileName: '16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip',
    source: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip'),
  },
]

function sha256(buffer) {
  return createHash('sha256').update(buffer).digest('hex').toUpperCase()
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds))
}

async function freePort() {
  const server = createNetServer()
  await new Promise((resolveListen, reject) => {
    server.once('error', reject)
    server.listen(0, '127.0.0.1', resolveListen)
  })
  const address = server.address()
  const port = typeof address === 'object' && address ? address.port : 0
  await new Promise((resolveClose, reject) => server.close((error) => error ? reject(error) : resolveClose()))
  if (!port) throw new Error('failed to reserve loopback port')
  return port
}

async function stopChild(child) {
  if (!child || child.exitCode !== null || child.signalCode !== null) return true
  child.kill()
  await Promise.race([
    new Promise((resolveExit) => child.once('close', resolveExit)),
    delay(3000),
  ])
  if (child.exitCode === null && child.signalCode === null) {
    spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true })
    await Promise.race([
      new Promise((resolveExit) => child.once('close', resolveExit)),
      delay(3000),
    ])
  }
  return child.exitCode !== null || child.signalCode !== null
}

async function waitForPortal(baseUrl, child, stderr) {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (child.exitCode !== null) throw new Error(`portal exited early: ${stderr()}`)
    try {
      const response = await fetch(`${baseUrl}/status.json`, { signal: AbortSignal.timeout(500) })
      if (response.ok) return
    } catch {
      // Waiting for the isolated test server.
    }
    await delay(75)
  }
  throw new Error(`portal did not become ready: ${stderr()}`)
}

function nativePayload(reference, width, doorCount, overrides = {}) {
  return {
    customerParameterFlow: 'native_v1',
    customerRequirementReference: reference,
    widthInputMode: 'cabinet_outer_width',
    requestedWidthMm: width,
    cabinetDepth: 550,
    doorCount,
    ...overrides,
  }
}

function nativeTaskFiles() {
  if (!existsSync(NATIVE_REQUEST_DIR)) return []
  return readdirSync(NATIVE_REQUEST_DIR)
    .filter((name) => name.endsWith('.json') && name !== basename(NATIVE_INDEX_PATH))
    .map((name) => JSON.parse(readFileSync(resolve(NATIVE_REQUEST_DIR, name), 'utf8')))
}

function nativeIndex() {
  return JSON.parse(readFileSync(NATIVE_INDEX_PATH, 'utf8'))
}

const checks = []
function check(name, action) {
  try {
    action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error instanceof Error ? error.message : String(error) })
  }
}

check('native_generator_resolves_v35_v36_v37', () => {
  assert.equal(normalize16029NativeModelRequest(nativePayload('V35', 740, 6)).seed.version, 'V35')
  assert.equal(normalize16029NativeModelRequest(nativePayload('V36', 740, 4)).seed.version, 'V36')
  assert.equal(normalize16029NativeModelRequest(nativePayload('V37', 760, 6)).seed.version, 'V37')
})

check('native_generator_creates_tasks_for_valid_unmatched_specs', () => {
  for (const payload of [
    nativePayload('V38', 760, 4),
    nativePayload('750规格', 750, 6),
    nativePayload('888宽14门', 888, 14),
  ]) {
    const result = normalize16029NativeModelRequest(payload)
    assert.equal(result.ok, true)
    assert.equal(result.status, 'native_task_created')
    assert.equal(result.matchType, 'native_build_task')
    assert.equal(result.normalizedRequest.taskType, 'native_solidworks_build_task')
    assert.equal(result.normalizedRequest.deliveryMode, 'native_task_pending')
    assert.equal(result.normalizedRequest.legacyFallbackUsed, false)
    assert.equal(result.normalizedRequest.previewDimensionsValidated, false)
  }

  const task = normalize16029NativeModelRequest(nativePayload('888宽14门', 888, 14)).normalizedRequest
  assert.equal(task.cabinetWidth, 888)
  assert.equal(task.cabinetHeight, 1917)
  assert.equal(task.cabinetDepth, 550)
  assert.equal(task.columns, 2)
  assert.deepEqual(task.columnDoorCounts, [7, 7])
  assert.equal(task.doorWidth, 381)
  assert.equal(task.rowSequence, 'L1111111-R1111111')
})

check('native_generator_rejects_invalid_or_electrical_requests', () => {
  assert.equal(normalize16029NativeModelRequest(nativePayload('无效宽度', 0, 14)).ok, false)
  assert.equal(normalize16029NativeModelRequest(nativePayload('奇数门', 888, 13)).reason, 'uneven_two_column_door_count')
  assert.equal(normalize16029NativeModelRequest(nativePayload('电控硬件', 888, 14, { prompt: '增加电控锁实体' })).reason, 'unsupported_electrical_hardware_request')
})

check('old_generator_files_are_deleted', () => {
  const oldFiles = [
    'tools/process_16029_review_generation_queue.mjs',
    'tools/generate_review_solidworks_full_assembly.ps1',
    'tools/generate_review_solidworks_single_door.ps1',
    'tools/locker_16029_controlled_generation_policy.mjs',
    'tools/locker_16029_generation_cache.mjs',
  ]
  assert.deepEqual(oldFiles.filter((path) => existsSync(resolve(ROOT, path))), [])
})

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(DATA_DIR, { recursive: true })
mkdirSync(MODEL_DIR, { recursive: true })
writeFileSync(resolve(DATA_DIR, 'review_download_invite_code.txt'), 'native-test-invite', 'utf8')
for (const asset of ASSETS) {
  assert.equal(existsSync(asset.source), true, `${asset.fileName} must exist`)
  copyFileSync(asset.source, resolve(MODEL_DIR, asset.fileName))
}

const readyNativeId = 'NATIVE-READY-888-14'
const readyNativeFingerprint = 'A'.repeat(64)
const readyRecipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
const readyRecipeDigest = nativeRecipeDigest(readyRecipe).toUpperCase()
const readyPublicationDir = resolve(DATA_DIR, 'native_model_publications', readyNativeId, 'attempt-0001')
mkdirSync(readyPublicationDir, { recursive: true })
const readyArchivePath = resolve(readyPublicationDir, '16029_888W_14door_structure_assistance.zip')
const readyArchiveBytes = Buffer.from('504B03041400000000000000210000000000000000000000000000', 'hex')
writeFileSync(readyArchivePath, readyArchiveBytes)
const readyArchiveSha256 = sha256(readyArchiveBytes)
const readyBuildAttemptDir = resolve(NATIVE_REQUEST_DIR, 'attempts', readyNativeId, 'attempt-0001')
const readyEvidenceDir = resolve(readyBuildAttemptDir, 'evidence')
const readyReceiptDir = resolve(readyBuildAttemptDir, 'receipts')
mkdirSync(readyEvidenceDir, { recursive: true })
mkdirSync(readyReceiptDir, { recursive: true })
const readyValidationPath = resolve(readyEvidenceDir, 'lock_topology_888x14.validation.v2.json')
const readyFinalEvidencePath = resolve(readyEvidenceDir, 'final_pack_and_relocated_reopen.result.v1.json')
const readyFinalReceiptPath = resolve(readyReceiptDir, 'final_pack_and_relocated_reopen.json')
const readyValidationBytes = Buffer.from('{"fixture":"validated"}\n', 'utf8')
const readyFinalEvidenceBytes = Buffer.from('{"fixture":"final-evidence"}\n', 'utf8')
const readyFinalReceiptBytes = Buffer.from('{"fixture":"final-receipt"}\n', 'utf8')
writeFileSync(readyValidationPath, readyValidationBytes)
writeFileSync(readyFinalEvidencePath, readyFinalEvidenceBytes)
writeFileSync(readyFinalReceiptPath, readyFinalReceiptBytes)
const readyValidationReference = { path: readyValidationPath, sha256: sha256(readyValidationBytes) }
const readyReceiptReference = { path: readyFinalReceiptPath, sha256: sha256(readyFinalReceiptBytes) }
const readyManifestPath = resolve(readyPublicationDir, 'package_manifest.json')
const readyManifest = {
  schema: 'winnsen.native_structure_assistance_package_manifest.v1',
  purpose: 'structure_engineering_assistance',
  createdAt: '2026-08-13T02:01:00.000Z',
  task: { id: readyNativeId, revision: 7 },
  request: { fingerprint: readyNativeFingerprint },
  recipe: { id: readyRecipe.id, version: readyRecipe.version, digest: readyRecipeDigest },
  attempt: 1,
  validationEvidence: { ...readyValidationReference, schema: 'winnsen.locker16029.native_888x14_lock_topology_validation.v2' },
  finalStageEvidence: { path: readyFinalEvidencePath, sha256: sha256(readyFinalEvidenceBytes), schema: 'winnsen.16029.native_final_pack_result.v1' },
  finalStageReceipt: { ...readyReceiptReference, schema: 'winnsen.16029.native_final_pack_receipt.v1' },
  inventory: {
    digest: 'E'.repeat(64), fileCount: 55, assemblyFileCount: 14, partFileCount: 41,
    files: Array.from({ length: 55 }, (_, index) => ({
      name: `${index < 14 ? 'Assembly' : 'Part'}-${String(index + 1).padStart(2, '0')}.${index < 14 ? 'SLDASM' : 'SLDPRT'}`,
      sizeBytes: index + 1,
      sha256: 'F'.repeat(64),
    })),
  },
  archive: {
    fileName: basename(readyArchivePath), sizeBytes: readyArchiveBytes.length,
    sha256: readyArchiveSha256, entryCount: 55, rootDirectory: `16029_888W_14door_${readyNativeId}`,
  },
  qualityBoundary: {
    purpose: 'structure_engineering_assistance', structureAssistanceReady: true,
    engineerReviewRequired: true, allRequiredChecksPassed: true,
  },
}
writeFileSync(readyManifestPath, `${JSON.stringify(readyManifest, null, 2)}\n`, 'utf8')
const readyStore = createNativeTaskStore({ dataDir: NATIVE_REQUEST_DIR })
await readyStore.createOrImport({
  schema: 'winnsen.native_16029_request.v1', id: readyNativeId,
  username: 'native-portal-test', createdAt: '2026-08-13T02:00:00.000Z', revision: 8,
  requestFingerprint: readyNativeFingerprint, status: 'native_assistance_model_ready',
  taskType: 'native_solidworks_build_task', deliveryMode: 'native_task_pending', storageMode: 'task_file_source',
  modelReady: false, legacyFallbackUsed: false, purpose: 'structure_engineering_assistance',
  cabinetWidth: 888, cabinetHeight: 1917, cabinetDepth: 550, columns: 2, doorCount: 14,
  doorWidth: 381, columnDoorCounts: [7, 7], rowSequence: 'L1111111-R1111111',
  nativeBuild: {
    state: 'ready', attempt: 1, modelReady: true, legacyFallbackUsed: false,
    planArtifact: { recipeId: readyRecipe.id, recipeVersion: readyRecipe.version, recipeDigest: readyRecipeDigest },
    result: {
      schema: 'winnsen.native_structure_engineering_assistance_result.v1',
      validationEvidence: readyValidationReference, finalReceipt: readyReceiptReference,
      packageManifest: { path: readyManifestPath, sha256: sha256(readFileSync(readyManifestPath)) },
      archive: { path: readyArchivePath, fileName: basename(readyArchivePath), sha256: readyArchiveSha256, sizeBytes: readyArchiveBytes.length },
    },
    statusHistory: [{ state: 'ready', at: '2026-08-13T02:01:00.000Z' }],
  },
})

const port = await freePort()
const baseUrl = `http://127.0.0.1:${port}`
const password = randomBytes(24).toString('base64url')
let stderrText = ''
const child = spawn(process.execPath, [PORTAL_PATH], {
  cwd: ROOT,
  env: {
    ...process.env,
    STUDIO_REVIEW_HOST: '127.0.0.1',
    STUDIO_REVIEW_PORT: String(port),
    STUDIO_REVIEW_DATA_DIR: DATA_DIR,
    STUDIO_REVIEW_STORAGE_ROOT: STORAGE_ROOT,
    STUDIO_REVIEW_SKIP_ASSET_SYNC: '1',
  },
  stdio: ['ignore', 'pipe', 'pipe'],
  windowsHide: true,
})
child.stderr.on('data', (chunk) => { stderrText += chunk.toString('utf8') })

let cookie = ''
try {
  await waitForPortal(baseUrl, child, () => stderrText)
  const registration = await fetch(`${baseUrl}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      username: 'native-portal-test',
      password,
      inviteCode: 'native-test-invite',
    }),
    redirect: 'manual',
  })
  assert.equal(registration.status, 302)
  cookie = String(registration.headers.get('set-cookie') || '').split(';', 1)[0]
  assert.match(cookie, /^review_session=/)

  const nativeReadyDetailResponse = await fetch(`${baseUrl}/generation-request/${readyNativeId}`, {
    headers: { Cookie: cookie },
  })
  assert.equal(nativeReadyDetailResponse.status, 200)
  const nativeReadyDetail = (await nativeReadyDetailResponse.json()).request
  assert.equal(nativeReadyDetail.status, 'native_assistance_model_ready')
  assert.equal(nativeReadyDetail.deliveryMode, 'verified_native_structure_assistance_model')
  assert.equal(nativeReadyDetail.modelReady, true)
  assert.equal(nativeReadyDetail.engineeringAssistanceReady, true)
  assert.equal(nativeReadyDetail.downloadUrl, `/native-assistance-download/${readyNativeId}`)
  assert.equal(nativeReadyDetail.sourceBaselineAssetId, '')

  const nativeReadyDownload = await fetch(`${baseUrl}${nativeReadyDetail.downloadUrl}`, {
    headers: { Cookie: cookie },
  })
  assert.equal(nativeReadyDownload.status, 200)
  const nativeReadyDownloadedBytes = Buffer.from(await nativeReadyDownload.arrayBuffer())
  assert.deepEqual(nativeReadyDownloadedBytes, readyArchiveBytes)
  assert.equal(sha256(nativeReadyDownloadedBytes), readyArchiveSha256)

  const tamperedNativeArchive = Buffer.from(readyArchiveBytes)
  tamperedNativeArchive[tamperedNativeArchive.length - 1] ^= 0x01
  writeFileSync(readyArchivePath, tamperedNativeArchive)
  const tamperedNativeDownload = await fetch(`${baseUrl}${nativeReadyDetail.downloadUrl}`, {
    headers: { Cookie: cookie },
  })
  assert.equal(tamperedNativeDownload.status, 409)
  writeFileSync(readyArchivePath, readyArchiveBytes)

  const exactRequestIds = new Map()
  for (const [reference, width, doorCount, expectedVersion, asset] of [
    ['V35任务', 740, 6, 'V35', ASSETS[2]],
    ['V36任务', 740, 4, 'V36', ASSETS[1]],
    ['V37任务', 760, 6, 'V37', ASSETS[0]],
  ]) {
    const response = await fetch(`${baseUrl}/generation-request`, {
      method: 'POST',
      headers: { Cookie: cookie, 'Content-Type': 'application/json' },
      body: JSON.stringify(nativePayload(reference, width, doorCount)),
    })
    assert.equal(response.status, 200)
    const result = await response.json()
    assert.equal(result.status, 'native_assistance_model_ready')
    assert.equal(result.deliveryMode, 'verified_native_model')
    assert.equal(result.engineeringAssistanceReady, true)
    assert.equal(result.sourceBaselineAssetId, asset.id)
    assert.equal(result.generationTemplateLabel.includes(expectedVersion), true)
    assert.match(result.downloadUrl, new RegExp(`^/download/${asset.id}$`))
    assert.equal(result.legacyFallbackUsed, false)
    exactRequestIds.set(expectedVersion, result.requestId)

    const download = await fetch(`${baseUrl}${result.downloadUrl}`, { headers: { Cookie: cookie } })
    assert.equal(download.status, 200)
    const downloadBytes = Buffer.from(await download.arrayBuffer())
    const expectedBytes = readFileSync(asset.source)
    assert.equal(downloadBytes.length, expectedBytes.length)
    assert.equal(sha256(downloadBytes), sha256(expectedBytes))
  }

  const exactIntegrityPayload = nativePayload('V37-integrity-recheck', 760, 6)
  const exactIntegrityInitial = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(exactIntegrityPayload),
  })
  assert.equal(exactIntegrityInitial.status, 200)
  assert.equal((await exactIntegrityInitial.json()).deliveryMode, 'verified_native_model')

  const v37TargetPath = resolve(MODEL_DIR, ASSETS[0].fileName)
  const v37TargetBytes = readFileSync(v37TargetPath)
  assert.equal(v37TargetBytes.length > 1024, true)
  const tamperedV37Bytes = Buffer.from(v37TargetBytes)
  tamperedV37Bytes[Math.floor(tamperedV37Bytes.length / 2)] ^= 0xff
  writeFileSync(v37TargetPath, tamperedV37Bytes)

  const staleExactReuse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(exactIntegrityPayload),
  })
  assert.equal(staleExactReuse.status, 409)

  copyFileSync(ASSETS[0].source, v37TargetPath)
  const restoredExactReuse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(exactIntegrityPayload),
  })
  assert.equal(restoredExactReuse.status, 200)
  const restoredExactResult = await restoredExactReuse.json()
  assert.equal(restoredExactResult.deliveryMode, 'verified_native_model')
  assert.equal(restoredExactResult.deduplicated, true)

  for (const payload of [
    nativePayload('V38组合', 760, 4),
    nativePayload('750未命中', 750, 6),
  ]) {
    const response = await fetch(`${baseUrl}/generation-request`, {
      method: 'POST',
      headers: { Cookie: cookie, 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
    assert.equal(response.status, 201)
    assert.match(String(response.headers.get('location') || ''), /^\/generation-request\/NATIVE-/)
    const result = await response.json()
    assert.equal(result.status, 'native_task_created')
    assert.equal(result.taskType, 'native_solidworks_build_task')
    assert.equal(result.deliveryMode, 'native_task_pending')
    assert.equal(result.modelReady, false)
    assert.equal(result.engineeringAssistanceReady, false)
    assert.equal(result.legacyFallbackUsed, false)
    assert.equal(Boolean(result.downloadUrl), false)
    assert.equal(result.nativeBuild?.state, 'created')
  }

  const concurrentPayload = nativePayload('客户888宽14门', 888, 14)
  const concurrentResponses = await Promise.all([1, 2, 3].map(() => fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(concurrentPayload),
  })))
  assert.deepEqual(concurrentResponses.map((response) => response.status).sort(), [200, 200, 201])
  assert.equal(concurrentResponses.filter((response) => response.status === 201).length, 1)
  assert.equal(concurrentResponses.filter((response) => response.status === 201)[0].headers.get('location')?.startsWith('/generation-request/NATIVE-'), true)
  const concurrentResults = await Promise.all(concurrentResponses.map((response) => response.json()))
  assert.equal(new Set(concurrentResults.map((result) => result.requestId)).size, 1)
  assert.equal(concurrentResults.filter((result) => result.created === true).length, 1)
  assert.equal(concurrentResults.filter((result) => result.deduplicated === true).length, 2)
  const requestId888 = concurrentResults[0].requestId
  for (const result of concurrentResults) {
    assert.equal(result.status, 'native_task_created')
    assert.equal(result.taskType, 'native_solidworks_build_task')
    assert.equal(result.deliveryMode, 'native_task_pending')
    assert.equal(result.modelReady, false)
    assert.equal(result.legacyFallbackUsed, false)
    assert.equal(Boolean(result.downloadUrl), false)
  }

  const parametricRequest = { cabinet: { widthMm: 850, heightMm: 1950, depthMm: 380 }, columns: [
    { side: 'L', doors: [{ heightUnits: 6 }, { heightUnits: 4 }, { heightUnits: 2 }] },
    { side: 'R', doors: [{ heightUnits: 2 }, { heightUnits: 4 }, { heightUnits: 6 }] },
  ] }
  const parametricResponse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST', headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...nativePayload('参数化门序验收', 850, 6), parametricRequest }),
  })
  assert.equal(parametricResponse.status, 201)
  const parametricResult = await parametricResponse.json()
  const parametricStored = JSON.parse(readFileSync(resolve(NATIVE_REQUEST_DIR, parametricResult.requestId + '.json'), 'utf8'))
  assert.deepEqual(parametricStored.parametricRequest, parametricRequest)
  assert.equal(parametricStored.status, 'native_task_created')
  assert.equal(parametricStored.deliveryMode, 'native_task_pending')
  assert.equal(parametricStored.modelReady, false)
  assert.equal(parametricStored.engineeringAssistanceReady, false)
  const verifiedParametric = validate16029ParametricPublication(resolve(ROOT, 'data/parametric_attempts/PARAM-20260906115210111-B86FBD'))
  assert.equal(verifiedParametric.ok, true, verifiedParametric.reason)
  const publishedResponse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST', headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...nativePayload('参数化下载验收', 913, 4), parametricRequest: verifiedParametric.request }),
  })
  assert.equal(publishedResponse.status, 201)
  const publishedResult = await publishedResponse.json()
  const publishedPath = resolve(NATIVE_REQUEST_DIR, publishedResult.requestId + '.json')
  const published = JSON.parse(readFileSync(publishedPath, 'utf8'))
  published.status = 'native_assistance_model_ready'
  published.nativeBuild.state = 'ready'
  published.nativeBuild.modelReady = true
  published.nativeBuild.result = { schema: 'winnsen.locker16029.parametric_publication.v1', ...verifiedParametric }
  writeFileSync(publishedPath, JSON.stringify(published), 'utf8')
  const dynamicDownload = await fetch(`${baseUrl}/native-assistance-download/${published.id}`, { headers: { Cookie: cookie } })
  assert.equal(dynamicDownload.status, 200)
  assert.equal(createHash('sha256').update(Buffer.from(await dynamicDownload.arrayBuffer())).digest('hex').toUpperCase(), verifiedParametric.archiveSha256)
  published.nativeBuild.result.archiveSha256 = '0'.repeat(64)
  writeFileSync(publishedPath, JSON.stringify(published), 'utf8')
  assert.equal((await fetch(`${baseUrl}/native-assistance-download/${published.id}`, { headers: { Cookie: cookie } })).status, 409)

  const changedEngineeringSpecResponse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      ...concurrentPayload,
      doorType: 'changed-door-type',
      lockType: 'mechanical-lock-b',
      hingeType: 'changed-hinge',
      latchType: 'changed-latch',
      reinforcement: 'changed-reinforcement',
      openings: 'changed-structural-openings',
      material: 'SPCC-B',
      thickness: '1.2',
    }),
  })
  assert.equal(changedEngineeringSpecResponse.status, 409)

  const detailResponse = await fetch(`${baseUrl}/generation-request/${requestId888}`, { headers: { Cookie: cookie } })
  assert.equal(detailResponse.status, 200)
  const detail = (await detailResponse.json()).request
  assert.equal(detail.id, requestId888)
  assert.equal(detail.status, 'native_task_created')
  assert.equal(detail.taskType, 'native_solidworks_build_task')
  assert.equal(detail.deliveryMode, 'native_task_pending')
  assert.equal(Number(detail.cabinetWidth), 888)
  assert.equal(Number(detail.cabinetHeight), 1917)
  assert.equal(Number(detail.cabinetDepth), 550)
  assert.equal(Number(detail.columns), 2)
  assert.deepEqual(detail.columnDoorCounts, [7, 7])
  assert.equal(Number(detail.doorWidth), 381)
  assert.equal(detail.rowSequence, 'L1111111-R1111111')
  assert.equal(detail.previewDimensionsValidated, false)
  assert.equal(detail.legacyFallbackUsed, false)
  assert.equal(Boolean(detail.downloadUrl), false)
  assert.equal(detail.nativeBuild?.state, 'created')
  assert.equal(detail.nativeBuild?.requiredChecks?.includes('one_door_one_lock_opening'), true)

  const filesAfterValidRequests = nativeTaskFiles()
  const indexAfterValidRequests = nativeIndex()
  assert.equal(filesAfterValidRequests.filter((item) => item.id === requestId888).length, 1)
  assert.equal(indexAfterValidRequests.requests.filter((item) => item.id === requestId888).length, 1)
  assert.equal(indexAfterValidRequests.requests.filter((item) => item.customerRequirementReference === '客户888宽14门').length, 1)
  const v38Task = indexAfterValidRequests.requests.find((item) => item.customerRequirementReference === 'V38组合')
  const pending750Task = indexAfterValidRequests.requests.find((item) => item.customerRequirementReference === '750未命中')
  assert.equal(v38Task?.candidateRecipeId, 'v38-760w-four-door-l66-r66')
  assert.deepEqual(v38Task?.candidateSourceSeedIds, [
    'v37-760w-six-door-l642-r246',
    'v36-740w-four-door-l66-r66',
  ])
  assert.equal(v38Task?.nativeBuild?.requiredChecks?.includes('shelf_and_crossbar_topology'), true)
  assert.equal(v38Task?.nativeBuild?.requiredChecks?.includes('relocated_copy_reopen'), true)

  const idsBeforeInvalidRequests = indexAfterValidRequests.requests.map((item) => item.id).sort()
  const detailFileCountBeforeInvalidRequests = filesAfterValidRequests.length
  for (const payload of [
    nativePayload('无效宽度HTTP', 0, 14),
    nativePayload('奇数门HTTP', 888, 13),
    nativePayload('电控硬件HTTP', 888, 14, { prompt: '增加电控锁实体' }),
    nativePayload('精确规格结构化电控硬件HTTP', 760, 6, { lockType: '电控锁' }),
    nativePayload('跨字段电控硬件HTTP', 760, 6, { lockType: '电控锁', openings: '不含电器板' }),
  ]) {
    const response = await fetch(`${baseUrl}/generation-request`, {
      method: 'POST',
      headers: { Cookie: cookie, 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
    assert.equal(response.status, 422)
  }
  assert.equal(nativeTaskFiles().length, detailFileCountBeforeInvalidRequests)
  assert.deepEqual(nativeIndex().requests.map((item) => item.id).sort(), idsBeforeInvalidRequests)

  const corruptIndexText = '{"schema":"deliberately-corrupted"'
  writeFileSync(NATIVE_INDEX_PATH, corruptIndexText, 'utf8')
  const recoveredDetailResponse = await fetch(`${baseUrl}/generation-request/${requestId888}`, { headers: { Cookie: cookie } })
  assert.equal(recoveredDetailResponse.status, 200)
  const recoveredDetail = (await recoveredDetailResponse.json()).request
  assert.equal(recoveredDetail.id, requestId888)
  assert.equal(readFileSync(NATIVE_INDEX_PATH, 'utf8'), corruptIndexText)
  assert.equal(readdirSync(NATIVE_REQUEST_DIR).some((name) => name.startsWith(`${basename(NATIVE_INDEX_PATH)}.corrupt-`)), false)

  const requestsResponse = await fetch(`${baseUrl}/generation-requests.json`, { headers: { Cookie: cookie } })
  assert.equal(requestsResponse.status, 200)
  const requests = await requestsResponse.json()
  assert.equal(requests.requests.length, detailFileCountBeforeInvalidRequests)
  assert.equal(requests.requests.filter((item) => item.id === requestId888).length, 1)
  assert.equal(existsSync(NATIVE_INDEX_PATH), true)
  assert.equal(readFileSync(NATIVE_INDEX_PATH, 'utf8'), corruptIndexText)
  assert.equal(existsSync(resolve(DATA_DIR, 'review_generation_requests')), false)

  const startResponse = await fetch(`${baseUrl}/generation-request/${requestId888}/start`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
  })
  assert.equal(startResponse.status, 404)
  const oldDownload = await fetch(`${baseUrl}/generation-download/${requestId888}`, { headers: { Cookie: cookie } })
  assert.equal(oldDownload.status, 404)

  const pageResponse = await fetch(baseUrl, { headers: { Cookie: cookie } })
  assert.equal(pageResponse.status, 200)
  const page = await pageResponse.text()
  for (const script of page.matchAll(/<script\b[^>]*>([\s\S]*?)<\/script>/g)) new Script(script[1])
  assert.match(page, /name="cabinetHeight" id="cabinetHeight" type="number" min="1700" max="2200"/)
  assert.match(page, /name="cabinetDepth" id="cabinetDepth" type="number" min="250" max="800"/)
  assert.equal(page.includes('宽 700–1200 mm · 高 1700–2200 mm · 深 250–650 mm · 两列独立门序（共 2–34 门）'), true)
  assert.match(page, /<section id="new-model"[^>]*>[\s\S]*?<h1>[^<]+<\/h1>/)
  assert.equal(page.includes('id="generationConfirm"'), true)
  assert.equal(page.includes('id="taskFilterInput"'), true)
  assert.equal(page.includes('核对参数与布局'), true)
  assert.equal(page.includes('只有实际模型通过尺寸、装配、钣金展开、干涉和迁移重开检查后才提供下载'), true)
  assert.equal(page.includes('尚不能自动生成下载'), true)
  assert.match(page, /<input name="doorCount" id="doorCount" type="number" min="2" max="34" step="1" value="6" readonly \/>/)
  assert.equal(page.includes('需结构工程师确认后方可生产'), false)
  assert.equal(page.includes('productionReleaseEligible'), false)
  assert.equal(page.includes('generation-start-button'), false)

  const deleteResponse = await fetch(`${baseUrl}/generation-request/${v38Task.id}/delete`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
  })
  assert.equal(deleteResponse.status, 200)
  const deleteResult = await deleteResponse.json()
  assert.equal(deleteResult.action, 'cancelled')
  assert.equal(deleteResult.cancelled, true)
  assert.equal(deleteResult.preservedTaskRecord, true)
  const deletedDetailResponse = await fetch(`${baseUrl}/generation-request/${v38Task.id}`, { headers: { Cookie: cookie } })
  assert.equal(deletedDetailResponse.status, 404)
  assert.equal(existsSync(resolve(NATIVE_REQUEST_DIR, `${v38Task.id}.json`)), true)
  const cancelledTask = JSON.parse(readFileSync(resolve(NATIVE_REQUEST_DIR, `${v38Task.id}.json`), 'utf8'))
  assert.equal(cancelledTask.nativeBuild.state, 'cancelled')
  assert.equal(nativeIndex().requests.some((item) => item.id === v38Task.id), true)
  assert.equal(readdirSync(NATIVE_REQUEST_DIR).some((name) => name.startsWith(`${basename(NATIVE_INDEX_PATH)}.corrupt-`)), true)

  const activeTaskPath = resolve(NATIVE_REQUEST_DIR, `${pending750Task.id}.json`)
  const activeTask = JSON.parse(readFileSync(activeTaskPath, 'utf8'))
  activeTask.status = 'native_build_in_progress'
  activeTask.nativeBuild.state = 'building'
  activeTask.nativeBuild.lease = {
    id: 'isolated-worker-lease',
    workerId: 'isolated-worker',
    workerPid: process.pid,
    claimedAt: new Date().toISOString(),
    heartbeatAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    durationMs: 60_000,
  }
  writeFileSync(activeTaskPath, `${JSON.stringify(activeTask, null, 2)}\n`, 'utf8')
  const activeCancelResponse = await fetch(`${baseUrl}/generation-request/${pending750Task.id}/delete`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
  })
  assert.equal(activeCancelResponse.status, 200)
  const activeCancelResult = await activeCancelResponse.json()
  assert.equal(activeCancelResult.action, 'cancel_requested')
  assert.equal(activeCancelResult.hidden, false)
  assert.equal(existsSync(activeTaskPath), true)
  const activeDetailResponse = await fetch(`${baseUrl}/generation-request/${pending750Task.id}`, { headers: { Cookie: cookie } })
  assert.equal(activeDetailResponse.status, 200)
  const activeDetailText = await activeDetailResponse.text()
  assert.equal(activeDetailText.includes('isolated-worker-lease'), false)
  assert.equal(JSON.parse(activeDetailText).request.status, 'native_task_cancel_requested')

  const readyRequestId = exactRequestIds.get('V35')
  const readyTaskPath = resolve(NATIVE_REQUEST_DIR, `${readyRequestId}.json`)
  const readyArchiveResponse = await fetch(`${baseUrl}/generation-request/${readyRequestId}/delete`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
  })
  assert.equal(readyArchiveResponse.status, 200)
  const readyArchiveResult = await readyArchiveResponse.json()
  assert.equal(readyArchiveResult.action, 'archived')
  assert.equal(readyArchiveResult.archived, true)
  assert.equal(existsSync(readyTaskPath), true)
  assert.equal(JSON.parse(readFileSync(readyTaskPath, 'utf8')).nativeBuild.archived, true)
  assert.equal((await fetch(`${baseUrl}/generation-request/${readyRequestId}`, { headers: { Cookie: cookie } })).status, 404)
  const postRemovalList = await (await fetch(`${baseUrl}/generation-requests.json`, { headers: { Cookie: cookie } })).json()
  assert.equal(postRemovalList.requests.some((item) => item.id === v38Task.id), false)
  assert.equal(postRemovalList.requests.some((item) => item.id === readyRequestId), false)
  assert.equal(postRemovalList.requests.some((item) => item.id === pending750Task.id && item.status === 'native_task_cancel_requested'), true)

  const forgedTaskPath = resolve(NATIVE_REQUEST_DIR, `${requestId888}.json`)
  const forgedReadyTask = JSON.parse(readFileSync(forgedTaskPath, 'utf8'))
  Object.assign(forgedReadyTask, {
    status: 'native_assistance_model_ready',
    taskType: 'verified_native_structure_assistance_model',
    modelReady: true,
    verifiedRecipeMatched: true,
    engineeringAssistanceReady: true,
    engineeringContinuationEligible: true,
    engineeringUseEligible: true,
    deliveryMode: 'verified_native_model',
    generationTemplateId: 'v37-760w-six-door-l642-r246',
    sourceBaselineAssetId: ASSETS[0].id,
    downloadUrl: `/download/${ASSETS[0].id}`,
    message: 'C:\\private-native-build\\top-level-secret.SLDASM command=powershell',
    path: 'C:\\private-native-build\\secret-model.SLDASM',
    command: 'forged-local-command --dangerous',
    script: 'C:\\private-native-build\\local-worker.ps1',
    trustedSources: ['trusted-seed-secret'],
  })
  forgedReadyTask.nativeBuild.execution = {
    path: 'C:\\private-native-build\\execution',
    command: 'forged-execution-command',
    script: 'forged-execution-script.ps1',
    trustedSources: ['nested-trusted-source-secret'],
  }
  forgedReadyTask.nativeBuild.validation = { reportPath: 'C:\\private-native-build\\validation.json' }
  forgedReadyTask.nativeBuild.result = { outputPath: 'C:\\private-native-build\\output.zip' }
  forgedReadyTask.nativeBuild.progress = {
    stage: 'building',
    percent: 42,
    message: 'C:\\private-native-build\\progress-secret.SLDASM command=powershell',
    path: 'C:\\private-native-build\\progress.json',
  }
  forgedReadyTask.nativeBuild.message = 'C:\\private-native-build\\native-secret.SLDASM command=powershell -File secret.ps1'
  forgedReadyTask.nativeBuild.planArtifact = {
    attempt: 2,
    fileName: 'native_build_plan.json',
    fileSha256: 'SAFE_PLAN_SHA256',
    immutable: true,
    recipeId: 'safe-recipe-id',
    path: 'C:\\private-native-build\\native_build_plan.json',
    command: 'forged-plan-command',
    trustedSources: ['nested-plan-secret'],
  }
  writeFileSync(forgedTaskPath, `${JSON.stringify(forgedReadyTask, null, 2)}\n`, 'utf8')

  const forgedListResponse = await fetch(`${baseUrl}/generation-requests.json`, { headers: { Cookie: cookie } })
  assert.equal(forgedListResponse.status, 200)
  const forgedListText = await forgedListResponse.text()
  for (const forbidden of ['private-native-build', 'powershell', 'secret.ps1', 'forged-local-command', 'local-worker.ps1', 'trusted-seed-secret', 'forged-plan-command', 'nested-plan-secret']) {
    assert.equal(forgedListText.includes(forbidden), false, `list leaked ${forbidden}`)
  }
  const forgedListItem = JSON.parse(forgedListText).requests.find((item) => item.id === requestId888)
  assert.equal(forgedListItem.nativeBuild.progress.stage, 'building')
  assert.equal(forgedListItem.nativeBuild.progress.percent, 42)
  assert.equal(forgedListItem.nativeBuild.planArtifact.fileName, 'native_build_plan.json')
  assert.equal(forgedListItem.nativeBuild.planArtifact.fileSha256, 'SAFE_PLAN_SHA256')
  assert.equal(forgedListItem.nativeBuild.planArtifact.attempt, 2)
  assert.equal(forgedListItem.nativeBuild.planArtifact.immutable, true)
  assert.equal(forgedListItem.engineeringAssistanceReady, false)
  assert.equal(forgedListItem.modelReady, false)
  assert.equal(Boolean(forgedListItem.downloadUrl), false)
  const forgedDetailResponse = await fetch(`${baseUrl}/generation-request/${requestId888}`, { headers: { Cookie: cookie } })
  assert.equal(forgedDetailResponse.status, 200)
  const forgedDetailText = await forgedDetailResponse.text()
  for (const forbidden of ['private-native-build', 'powershell', 'secret.ps1', 'forged-local-command', 'local-worker.ps1', 'trusted-seed-secret', 'forged-plan-command', 'nested-plan-secret']) {
    assert.equal(forgedDetailText.includes(forbidden), false, `detail leaked ${forbidden}`)
  }
  const forgedDetail = JSON.parse(forgedDetailText).request
  assert.equal(forgedDetail.engineeringAssistanceReady, false)
  assert.equal(Boolean(forgedDetail.downloadUrl), false)
  const forgedReuseResponse = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(concurrentPayload),
  })
  assert.equal(forgedReuseResponse.status, 409)
  const forgedReuseText = await forgedReuseResponse.text()
  for (const forbidden of ['private-native-build', 'powershell', 'secret.ps1', 'forged-local-command', 'local-worker.ps1', 'trusted-seed-secret', 'forged-plan-command', 'nested-plan-secret']) {
    assert.equal(forgedReuseText.includes(forbidden), false, `POST leaked ${forbidden}`)
  }

  checks.push({ name: 'isolated_native_portal_http_flow', ok: true })
} catch (error) {
  checks.push({ name: 'isolated_native_portal_http_flow', ok: false, error: error instanceof Error ? error.message : String(error) })
} finally {
  const childStopped = await stopChild(child)
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  checks.push({ name: 'isolated_portal_cleanup', ok: childStopped && !existsSync(TEMP_ROOT) })
}

check('portal_source_has_no_old_runtime_entry', () => {
  const source = readFileSync(PORTAL_PATH, 'utf8')
  for (const forbidden of [
    'process_16029_review_generation_queue.mjs',
    'locker_16029_generation_cache.mjs',
    '/generation-download/',
    'generation-start-button',
    'manual_cad_worker',
    'verified_asset_cache',
  ]) {
    assert.equal(source.includes(forbidden), false, forbidden)
  }
  assert.equal(source.includes("from './locker_16029_native_generator.mjs'"), true)
  assert.equal(source.includes("from './lib/locker_16029_native_task_store.mjs'"), true)
  assert.equal(source.includes('createOrReuseByFingerprint'), true)
  assert.equal(source.includes('listSnapshot'), true)
  assert.equal(source.includes('readGenerationIndex'), false)
  assert.equal(NATIVE_16029_SEEDS.length, 3)
})

const failed = checks.filter((item) => !item.ok)
const result = {
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  checks,
}
process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exit(1)
