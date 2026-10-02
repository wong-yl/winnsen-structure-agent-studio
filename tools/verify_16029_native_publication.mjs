import assert from 'node:assert/strict'
import { createHash, randomBytes } from 'node:crypto'
import {
  closeSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_STRUCTURE_ASSISTANCE_MANIFEST_SCHEMA,
  inspectNativeStructureAssistancePublication,
  openVerifiedNativeStructureAssistanceArchive,
  publishNativeStructureAssistance,
} from './lib/locker_16029_native_publication.mjs'
import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  NATIVE_STAGE_RECEIPT_ORDER,
  nativeEvidenceCommitmentSha256,
} from './lib/locker_16029_native_stage_contract.mjs'
import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from './locker_16029_native_recipe_registry.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const TEMP_ROOT = resolve(ROOT, `tmp/verify-native-publication-${process.pid}-${randomBytes(4).toString('hex')}`)
const RECIPE_DIGEST = 'F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8'
const DIGEST_A = 'A'.repeat(64)
const DIGEST_B = 'B'.repeat(64)
const checks = []

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256(readFileSync(path))
}

async function check(name, action) {
  try {
    await action()
    checks.push({ name, ok: true })
  } catch (error) {
    checks.push({ name, ok: false, error: error instanceof Error ? error.stack || error.message : String(error) })
  }
}

function inventoryDigest(files) {
  return sha256(Buffer.from(files.map((row) => `${row.name}|${row.sha256}\n`).join(''), 'utf8'))
}

function makeFixture(label) {
  const root = resolve(TEMP_ROOT, label)
  const dataDir = resolve(root, 'data')
  const attemptDir = resolve(dataDir, 'native_model_requests/attempts/NATIVE-888-14/attempt-0001')
  const packageDir = resolve(attemptDir, 'final_native_package')
  mkdirSync(packageDir, { recursive: true })
  const files = []
  for (let index = 1; index <= 14; index += 1) {
    const name = `Assembly-${String(index).padStart(2, '0')}.SLDASM`
    const bytes = Buffer.from(`assembly-${index}\n`, 'utf8')
    writeFileSync(resolve(packageDir, name), bytes)
    files.push({ name, sizeBytes: bytes.length, sha256: sha256(bytes), bytes })
  }
  for (let index = 1; index <= 41; index += 1) {
    const name = `Part-${String(index).padStart(2, '0')}.SLDPRT`
    const bytes = Buffer.from(`part-${index}\n`, 'utf8')
    writeFileSync(resolve(packageDir, name), bytes)
    files.push({ name, sizeBytes: bytes.length, sha256: sha256(bytes), bytes })
  }
  files.sort((left, right) => left.name.localeCompare(right.name, 'en'))
  const postDigest = inventoryDigest(files)
  const task = {
    id: 'NATIVE-888-14', revision: 12, requestFingerprint: DIGEST_A,
    legacyFallbackUsed: false,
    nativeBuild: { state: 'validating', attempt: 1, modelReady: false, legacyFallbackUsed: false },
  }
  const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
  const recipeDigest = nativeRecipeDigest(recipe)
  assert.equal(recipeDigest.toUpperCase(), RECIPE_DIGEST)
  const recipeIdentity = { id: recipe.id, version: recipe.version, digest: recipeDigest }
  const plan = {
    schema: 'winnsen.native_build_plan.v1', purpose: 'structure_engineering_assistance',
    workerId: 'fixture-worker',
    task: { id: task.id, revisionAtPlanning: 3, digest: DIGEST_B },
    request: { fingerprint: task.requestFingerprint, digest: 'C'.repeat(64) },
    recipe: recipeIdentity,
  }
  const planPath = resolve(attemptDir, 'native_build_plan.json')
  writeFileSync(planPath, `${JSON.stringify(plan, null, 2)}\n`, 'utf8')
  const evidence = {
    schema: 'winnsen.16029.native_final_pack_result.v1',
    purpose: 'structure_engineering_assistance',
    status: 'STRUCTURE_ENGINEERING_ASSISTANCE_FINAL_PACK_PASS',
    success: true,
    engineerReviewRequired: true,
    completed_at_utc: '2026-08-13T02:00:00.000Z',
    task: { id: task.id, revision: 3, digest: DIGEST_B, attempt: 1, requestDigest: plan.request.digest },
    recipe: recipeIdentity,
    plan: { sha256: sha256File(planPath), workerId: plan.workerId },
    tool: { id: 'native_final_pack_888x14_v1', sourceNormalizedSha256: DIGEST_A, executableSha256: DIGEST_B },
    authorization: { id: 'native-auth-fixture', sha256: DIGEST_A, issuedAt: '2026-08-13T01:50:00.000Z', expiresAt: '2026-08-13T02:05:00.000Z', leaseId: 'lease-fixture', leaseExpiresAt: '2026-08-13T02:10:00.000Z' },
    predecessorRootReceiptSha256: DIGEST_A,
    paths: { workingPack: resolve(attemptDir, 'native_cad/working_pack'), finalPackage: packageDir, finalRoot: resolve(packageDir, 'Assembly-01.SLDASM') },
    preInventory: { digest: DIGEST_A, fileCount: 88, assemblyFileCount: 26, partFileCount: 62, files: [] },
    postInventory: { digest: postDigest, fileCount: 55, assemblyFileCount: 14, partFileCount: 41, files: files.map(({ bytes, ...row }) => row) },
    packaging: {},
    finalVerification: { openErrors: 0, openWarnings: 32, passed: true, inventoryDigest: postDigest, snapshot: {} },
    relocatedVerification: { openErrors: 0, openWarnings: 32, passed: true, inventoryDigest: postDigest, snapshot: {} },
    sessions: [],
    processes: { finalGate: true, finalSldworks: [], finalSldprocmon: [], cleanupErrors: [] },
    transaction: { inputUnchanged: true, relocatedProbeRemoved: true },
    quality_boundary: { purpose: 'structure_engineering_assistance', engineerReviewRequired: true },
    authorization_checkpoints: [],
    backup_deleted: true,
    backup_delete_error: '',
    evidence_commitment_sha256: '',
  }
  evidence.evidence_commitment_sha256 = nativeEvidenceCommitmentSha256(evidence)
  const evidencePath = resolve(attemptDir, 'evidence/final_pack_and_relocated_reopen.result.v1.json')
  mkdirSync(dirname(evidencePath), { recursive: true })
  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8')
  const receipt = {
    schema: 'winnsen.16029.native_final_pack_receipt.v1', phase: 'final_pack_and_relocated_reopen', success: true,
    completedAt: evidence.completed_at_utc, taskId: task.id, taskRevision: 3, taskDigest: DIGEST_B,
    requestDigest: plan.request.digest, leaseId: 'lease-fixture', leaseExpiresAt: '2026-08-13T02:10:00.000Z',
    attempt: 1, planSha256: sha256File(planPath), authorizationId: 'native-auth-fixture', authorizationSha256: DIGEST_A,
    authorizationJsonBase64: Buffer.from('{}\n').toString('base64'), authorizationIssuedAt: '2026-08-13T01:50:00.000Z',
    authorizationExpiresAt: '2026-08-13T02:05:00.000Z', recipeId: recipe.id, recipeDigest,
    toolId: 'native_final_pack_888x14_v1', toolSourceNormalizedSha256: DIGEST_A, toolExecutableSha256: DIGEST_B,
    evidencePath: 'evidence/final_pack_and_relocated_reopen.result.v1.json', evidenceSha256: sha256File(evidencePath),
    evidenceCommitmentSha256: evidence.evidence_commitment_sha256, preInventoryDigest: DIGEST_A,
    postInventoryDigest: postDigest, predecessorReceiptSha256: DIGEST_A,
  }
  assert.deepEqual(Object.keys(receipt).sort(), [...NATIVE_RUNTIME_RECEIPT_KEYS].sort())
  const receiptPath = resolve(attemptDir, 'receipts/final_pack_and_relocated_reopen.json')
  mkdirSync(dirname(receiptPath), { recursive: true })
  writeFileSync(receiptPath, `${JSON.stringify(receipt, null, 2)}\n`, 'utf8')
  const validation = {
    schema: 'winnsen.locker16029.native_888x14_lock_topology_validation.v2',
    purpose: 'structure_engineering_assistance', status: 'PASS', structureEngineeringEvidencePass: true,
    failedCheckCount: 0, failedChecks: [],
  }
  const validationPath = resolve(attemptDir, 'evidence/lock_topology_888x14.validation.v2.json')
  writeFileSync(validationPath, `${JSON.stringify(validation, null, 2)}\n`, 'utf8')
  const stageResults = NATIVE_STAGE_RECEIPT_ORDER.map((phase, index) => ({
    phase,
    receipt: phase === 'final_pack_and_relocated_reopen'
      ? { phase, path: receiptPath, sha256: sha256File(receiptPath) }
      : { phase, path: resolve(attemptDir, `receipts/fake-${index}.json`), sha256: DIGEST_A },
    evidence: phase === 'final_pack_and_relocated_reopen'
      ? { path: evidencePath, sha256: sha256File(evidencePath) }
      : { path: resolve(attemptDir, `evidence/fake-${index}.json`), sha256: DIGEST_B },
    postInventoryDigest: phase === 'final_pack_and_relocated_reopen' ? postDigest : DIGEST_A,
  }))
  return {
    root, dataDir, attemptDir, packageDir, files, postDigest, task, recipe,
    blueprint: { schema: 'winnsen.native_888x14_execution_blueprint.v1', attemptDir },
    evidence, evidencePath, receipt, receiptPath, validationPath, stageResults,
    args: {
      dataDir, task, recipe, stageResults,
      blueprint: { schema: 'winnsen.native_888x14_execution_blueprint.v1', attemptDir },
      finalValidationEvidence: { path: validationPath, sha256: sha256File(validationPath) },
      clock: () => new Date('2026-08-13T02:01:00.000Z'),
    },
  }
}

function readStoredZip(path) {
  const bytes = readFileSync(path)
  const entries = []
  let offset = 0
  while (offset + 4 <= bytes.length && bytes.readUInt32LE(offset) === 0x04034B50) {
    const flags = bytes.readUInt16LE(offset + 6)
    const method = bytes.readUInt16LE(offset + 8)
    const size = bytes.readUInt32LE(offset + 18)
    const nameLength = bytes.readUInt16LE(offset + 26)
    const extraLength = bytes.readUInt16LE(offset + 28)
    const nameStart = offset + 30
    const dataStart = nameStart + nameLength + extraLength
    entries.push({
      name: bytes.subarray(nameStart, nameStart + nameLength).toString('utf8'),
      flags, method, bytes: bytes.subarray(dataStart, dataStart + size),
    })
    offset = dataStart + size
  }
  assert.equal(bytes.readUInt32LE(offset), 0x02014B50)
  return entries
}

await check('publishes_exact_55_file_single_root_zip_and_independent_manifest', () => {
  const fixture = makeFixture('success')
  const result = publishNativeStructureAssistance(fixture.args)
  const manifest = JSON.parse(readFileSync(result.manifest.path, 'utf8'))
  assert.equal(manifest.schema, NATIVE_STRUCTURE_ASSISTANCE_MANIFEST_SCHEMA)
  assert.equal(manifest.purpose, 'structure_engineering_assistance')
  assert.equal(manifest.inventory.digest, fixture.postDigest)
  assert.deepEqual([manifest.inventory.fileCount, manifest.inventory.assemblyFileCount, manifest.inventory.partFileCount], [55, 14, 41])
  assert.equal(manifest.qualityBoundary.structureAssistanceReady, true)
  assert.equal(manifest.qualityBoundary.engineerReviewRequired, true)
  assert.notEqual(resolve(result.manifest.path), resolve(fixture.evidencePath))
  assert.equal(result.manifest.sha256, sha256File(result.manifest.path))
  assert.equal(result.archive.sha256, sha256File(result.archive.path))
  const entries = readStoredZip(result.archive.path)
  assert.equal(entries.length, 55)
  const roots = new Set(entries.map((entry) => entry.name.split('/')[0]))
  assert.deepEqual([...roots], [manifest.archive.rootDirectory])
  assert.equal(entries.every((entry) => entry.flags === 0x0800 && entry.method === 0 && !entry.name.includes('..') && !entry.name.includes('\\')), true)
  const byName = new Map(fixture.files.map((row) => [row.name, row]))
  for (const entry of entries) {
    const source = byName.get(entry.name.slice(entry.name.indexOf('/') + 1))
    assert.ok(source)
    assert.equal(sha256(entry.bytes), source.sha256)
  }
})

await check('ready_inspection_binds_manifest_and_download_rehashes_the_same_open_fd', () => {
  const fixture = makeFixture('ready-download')
  const published = publishNativeStructureAssistance(fixture.args)
  const readyTask = {
    ...fixture.task,
    revision: fixture.task.revision + 1,
    status: 'native_assistance_model_ready',
    nativeBuild: {
      ...fixture.task.nativeBuild,
      state: 'ready',
      modelReady: true,
      planArtifact: {
        recipeId: fixture.recipe.id,
        recipeVersion: fixture.recipe.version,
        recipeDigest: nativeRecipeDigest(fixture.recipe),
      },
      result: {
        schema: 'winnsen.native_structure_engineering_assistance_result.v1',
        validationEvidence: fixture.args.finalValidationEvidence,
        finalReceipt: {
          path: fixture.stageResults.at(-1).receipt.path,
          sha256: fixture.stageResults.at(-1).receipt.sha256,
        },
        packageManifest: published.manifest,
        archive: {
          path: published.archive.path,
          fileName: published.archive.fileName,
          sha256: published.archive.sha256,
          sizeBytes: published.archive.sizeBytes,
        },
      },
    },
  }
  const info = inspectNativeStructureAssistancePublication({ dataDir: fixture.dataDir, task: readyTask })
  assert.equal(info.ok, true, JSON.stringify(info))
  const opened = openVerifiedNativeStructureAssistanceArchive({ dataDir: fixture.dataDir, task: readyTask })
  try {
    assert.equal(opened.fd >= 0, true)
    assert.equal(opened.archiveSha256, published.archive.sha256)
    assert.equal(opened.stat.size, published.archive.sizeBytes)
  } finally {
    closeSync(opened.fd)
  }
  const bytes = readFileSync(published.archive.path)
  bytes[bytes.length - 1] ^= 0x01
  writeFileSync(published.archive.path, bytes)
  assert.throws(
    () => openVerifiedNativeStructureAssistanceArchive({ dataDir: fixture.dataDir, task: readyTask }),
    (error) => error?.code === 'NATIVE_PUBLICATION_ARCHIVE_INVALID',
  )
})

await check('rejects_live_cad_tamper_before_creating_publication', () => {
  const fixture = makeFixture('cad-tamper')
  writeFileSync(resolve(fixture.packageDir, fixture.files[0].name), 'tampered\n', 'utf8')
  assert.throws(() => publishNativeStructureAssistance(fixture.args),
    (error) => /INVENTORY|EVIDENCE/.test(String(error?.code || '')))
  assert.equal(existsSync(resolve(fixture.dataDir, 'native_model_publications', fixture.task.id)), false)
})

await check('rejects_forged_receipt_or_validation_and_never_uses_stage_evidence_as_manifest', () => {
  const receiptFixture = makeFixture('receipt-tamper')
  const receipt = JSON.parse(readFileSync(receiptFixture.receiptPath, 'utf8'))
  receipt.postInventoryDigest = DIGEST_B
  writeFileSync(receiptFixture.receiptPath, `${JSON.stringify(receipt, null, 2)}\n`, 'utf8')
  receiptFixture.stageResults.at(-1).receipt.sha256 = sha256File(receiptFixture.receiptPath)
  assert.throws(() => publishNativeStructureAssistance(receiptFixture.args),
    (error) => error?.code === 'NATIVE_PUBLICATION_FINAL_RECEIPT_INVALID')

  const validationFixture = makeFixture('validation-fail')
  const validation = JSON.parse(readFileSync(validationFixture.validationPath, 'utf8'))
  validation.status = 'FAIL'
  writeFileSync(validationFixture.validationPath, `${JSON.stringify(validation, null, 2)}\n`, 'utf8')
  validationFixture.args.finalValidationEvidence.sha256 = sha256File(validationFixture.validationPath)
  assert.throws(() => publishNativeStructureAssistance(validationFixture.args),
    (error) => error?.code === 'NATIVE_PUBLICATION_VALIDATION_INVALID')
})

await check('foreign_manifest_collision_is_preserved_and_owned_archive_is_removed', () => {
  const fixture = makeFixture('manifest-collision')
  const output = resolve(fixture.dataDir, 'native_model_publications', fixture.task.id, 'attempt-0001')
  mkdirSync(output, { recursive: true })
  const manifestPath = resolve(output, 'package_manifest.json')
  const foreign = 'foreign manifest must remain\n'
  writeFileSync(manifestPath, foreign, 'utf8')
  assert.throws(() => publishNativeStructureAssistance(fixture.args),
    (error) => error?.code === 'NATIVE_PUBLICATION_IMMUTABLE_CONFLICT')
  assert.equal(readFileSync(manifestPath, 'utf8'), foreign)
  assert.equal(existsSync(resolve(output, '16029_888W_14door_structure_assistance.zip')), false)
})

await check('publication_source_contains_no_non_assistance_completion_vocabulary', () => {
  const source = readFileSync(resolve(ROOT, 'tools/lib/locker_16029_native_publication.mjs'), 'utf8')
  assert.equal(/\b(?:production|release)\b/i.test(source), false)
  assert.equal(source.includes('finalStage.packageManifest || finalStage.evidence'), false)
})

await check('temporary_publication_fixture_is_removed', () => {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  assert.equal(existsSync(TEMP_ROOT), false)
})

const failed = checks.filter((row) => !row.ok)
process.stdout.write(`${JSON.stringify({
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  solidWorksStarted: false,
  cadFilesOpened: false,
  checks,
}, null, 2)}\n`)
if (failed.length) process.exit(1)
