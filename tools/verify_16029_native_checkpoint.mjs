import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import {
  copyFileSync, cpSync, existsSync, linkSync, mkdirSync, mkdtempSync, readFileSync,
  renameSync, rmSync, rmdirSync, symlinkSync, unlinkSync, writeFileSync,
} from 'node:fs'
import { basename, resolve } from 'node:path'
import { canonicalFlatCadInventoryDigest } from './lib/locker_16029_native_stage_executor.mjs'
import { loadNativeCheckpoint } from './lib/locker_16029_native_checkpoint.mjs'

const ROOT = resolve(import.meta.dirname, '..')
const TASK_ID = 'NATIVE-20260831T170546-C97F93'
const REAL_DATA = resolve(ROOT, 'data')
const REAL_TASK_ROOT = resolve(REAL_DATA, 'native_model_requests')
const REAL_TASK = resolve(REAL_TASK_ROOT, `${TASK_ID}.json`)
const REAL_ATTEMPT = resolve(REAL_TASK_ROOT, 'attempts', TASK_ID, 'attempt-0001')
const checks = []
const check = async (name, action) => {
  try {
    await action()
    checks.push({ name, ok: true })
    process.stdout.write(`PASS ${name}\n`)
  } catch (error) {
    checks.push({ name, ok: false, error: error?.stack || String(error) })
    process.stderr.write(`FAIL ${name}\n${error?.stack || error}\n`)
  }
}
const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const rejectsCode = async (action, codes) => {
  const expected = new Set(Array.isArray(codes) ? codes : [codes])
  await assert.rejects(action, (error) => {
    assert.equal(expected.has(error?.code), true, `unexpected error code ${error?.code}`)
    return true
  })
}

const realBefore = {
  task: sha256(readFileSync(REAL_TASK)),
  plan: sha256(readFileSync(resolve(REAL_ATTEMPT, 'native_build_plan.json'))),
  assembliesReceipt: sha256(readFileSync(resolve(REAL_ATTEMPT, 'evidence', 'width-assemblies.receipt.json'))),
  audit: sha256(readFileSync(resolve(REAL_ATTEMPT, 'diagnostics', 'assemblies-r22-runtime-audit.json'))),
  inventory: canonicalFlatCadInventoryDigest(resolve(REAL_ATTEMPT, 'native_cad', 'working_pack'),
    { expectedFileCount: 75 }),
}

await check('real_r22_assemblies_checkpoint_is_readonly_and_fully_bound', async () => {
  const snapshot = loadNativeCheckpoint({
    repositoryRoot: ROOT,
    dataDir: REAL_DATA,
    taskId: TASK_ID,
    completedPhase: 'assemblies',
  })
  assert.equal(snapshot.descriptor.completedPhase, 'assemblies')
  assert.equal(snapshot.prefixStageResults.length, 5)
  assert.deepEqual(snapshot.prefixStageResults.map((row) => row.phase),
    ['clone_native_seed', 'dimensions', 'derived', 'base-hole', 'assemblies'])
  assert.equal(snapshot.currentInventoryDigest, realBefore.inventory)
  assert.equal(snapshot.nextPhase, 'door_module_888x14')
  assert.equal(snapshot.descriptor.completedReceiptSha256, realBefore.assembliesReceipt)
  assert.equal(snapshot.descriptor.checkpointAuditSha256, realBefore.audit)
  assert.match(snapshot.checkpointDigest, /^[A-F0-9]{64}$/)
})

const tempRoot = mkdtempSync(resolve(ROOT, 'data', '.native-checkpoint-test-'))
const fixtureData = resolve(tempRoot, 'data')
const fixtureTaskRoot = resolve(fixtureData, 'native_model_requests')
const fixtureTask = resolve(fixtureTaskRoot, `${TASK_ID}.json`)
const fixtureAttempt = resolve(fixtureTaskRoot, 'attempts', TASK_ID, 'attempt-0001')
mkdirSync(resolve(fixtureTaskRoot, 'attempts', TASK_ID), { recursive: true })
copyFileSync(REAL_TASK, fixtureTask)
cpSync(REAL_ATTEMPT, fixtureAttempt, { recursive: true, force: false, errorOnExist: true })
const loadFixture = (overrides = {}) => loadNativeCheckpoint({
  repositoryRoot: ROOT,
  dataDir: fixtureData,
  taskId: TASK_ID,
  completedPhase: 'assemblies',
  ...overrides,
})
const mutateFile = async (path, mutation, action) => {
  const original = readFileSync(path)
  try {
    mutation(path, original)
    await action()
  } finally {
    writeFileSync(path, original)
  }
}

try {
  await check('temp_fixture_matches_real_checkpoint_without_external_paths', async () => {
    const snapshot = loadFixture()
    assert.equal(snapshot.currentInventoryDigest, realBefore.inventory)
    assert.equal(snapshot.attemptDir.startsWith(tempRoot), true)
  })

  await check('tampered_receipt_is_rejected', async () => {
    const path = resolve(fixtureAttempt, 'evidence', 'width-assemblies.receipt.json')
    await mutateFile(path, (target, bytes) => writeFileSync(target, Buffer.concat([bytes, Buffer.from(' ')])),
      () => rejectsCode(async () => loadFixture(),
        ['NATIVE_CHECKPOINT_JSON_INVALID', 'NATIVE_CHECKPOINT_RECEIPT_INVALID',
          'NATIVE_CHECKPOINT_BOOTSTRAP_ANCHOR_MISMATCH']))
  })

  await check('tampered_consumed_authorization_is_rejected', async () => {
    const consumed = resolve(fixtureAttempt, 'execution_authorizations', 'consumed')
    const name = (await import('node:fs')).readdirSync(consumed)
      .find((entry) => entry.startsWith('assemblies-'))
    const path = resolve(consumed, name)
    await mutateFile(path, (target, bytes) => writeFileSync(target, Buffer.concat([bytes, Buffer.from(' ')])),
      () => rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_AUTHORIZATION_INVALID'))
  })

  await check('changed_cad_hash_is_rejected', async () => {
    const workingPack = resolve(fixtureAttempt, 'native_cad', 'working_pack')
    const target = resolve(workingPack, (await import('node:fs')).readdirSync(workingPack)
      .find((name) => /\.sldprt$/i.test(name)))
    await mutateFile(target, (path, bytes) => writeFileSync(path, Buffer.concat([bytes, Buffer.from([0])])),
      () => rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_INVENTORY_DRIFT'))
  })

  await check('hardlinked_cad_file_is_rejected', async () => {
    const workingPack = resolve(fixtureAttempt, 'native_cad', 'working_pack')
    const name = (await import('node:fs')).readdirSync(workingPack).find((row) => /\.sldprt$/i.test(row))
    const target = resolve(workingPack, name)
    const bytes = readFileSync(target)
    const external = resolve(tempRoot, 'hardlink-source.SLDPRT')
    writeFileSync(external, bytes)
    unlinkSync(target)
    linkSync(external, target)
    try {
      await rejectsCode(async () => loadFixture(), 'NATIVE_EXECUTOR_INVENTORY_INVALID')
    } finally {
      unlinkSync(target)
      unlinkSync(external)
      writeFileSync(target, bytes)
    }
  })

  await check('working_pack_junction_is_rejected', async () => {
    const workingPack = resolve(fixtureAttempt, 'native_cad', 'working_pack')
    const realPack = resolve(fixtureAttempt, 'native_cad', 'working_pack-junction-target')
    renameSync(workingPack, realPack)
    symlinkSync(realPack, workingPack, 'junction')
    try {
      await rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_PATH_UNSAFE')
    } finally {
      rmdirSync(workingPack)
      renameSync(realPack, workingPack)
    }
  })

  await check('future_door_receipt_is_rejected_at_assemblies_checkpoint', async () => {
    const path = resolve(fixtureAttempt, 'receipts', 'door_module_888x14.json')
    writeFileSync(path, '{}\n', 'utf8')
    try {
      await rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_LATER_ARTIFACT_PRESENT')
    } finally { unlinkSync(path) }
  })

  await check('tampered_runtime_audit_is_rejected', async () => {
    const path = resolve(fixtureAttempt, 'diagnostics', 'assemblies-r22-runtime-audit.json')
    await mutateFile(path, (target, bytes) => {
      const value = JSON.parse(bytes.toString('utf8'))
      value.checksFailed = 1
      writeFileSync(target, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
    }, () => rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_AUDIT_INVALID'))
  })

  await check('forged_stored_checkpoint_is_rejected', async () => {
    const snapshot = loadFixture()
    const original = readFileSync(fixtureTask)
    try {
      const task = JSON.parse(original.toString('utf8'))
      task.nativeBuild.checkpoint = { ...snapshot.descriptor, currentInventoryDigest: '0'.repeat(64) }
      task.nativeBuild.checkpointDigest = snapshot.checkpointDigest
      writeFileSync(fixtureTask, `${JSON.stringify(task, null, 2)}\n`, 'utf8')
      await rejectsCode(async () => loadFixture(), 'NATIVE_CHECKPOINT_STORED_BINDING_INVALID')
    } finally { writeFileSync(fixtureTask, original) }
  })

  await check('path_escape_and_cross_task_requests_are_rejected', async () => {
    await rejectsCode(async () => loadFixture({ taskId: '../NATIVE-ESCAPE' }),
      'NATIVE_CHECKPOINT_TASK_INVALID')
    await rejectsCode(async () => loadFixture({ taskId: 'NATIVE-DIFFERENT-TASK' }),
      'NATIVE_CHECKPOINT_PATH_INVALID')
  })

  await check('door_checkpoint_cannot_be_forged_without_door_receipt', async () => {
    await rejectsCode(async () => loadFixture({ completedPhase: 'door_module_888x14' }),
      'NATIVE_CHECKPOINT_PATH_INVALID')
  })
} finally {
  rmSync(tempRoot, { recursive: true, force: true })
}

await check('real_r22_files_remain_byte_identical_after_all_checks', async () => {
  const after = {
    task: sha256(readFileSync(REAL_TASK)),
    plan: sha256(readFileSync(resolve(REAL_ATTEMPT, 'native_build_plan.json'))),
    assembliesReceipt: sha256(readFileSync(resolve(REAL_ATTEMPT, 'evidence', 'width-assemblies.receipt.json'))),
    audit: sha256(readFileSync(resolve(REAL_ATTEMPT, 'diagnostics', 'assemblies-r22-runtime-audit.json'))),
    inventory: canonicalFlatCadInventoryDigest(resolve(REAL_ATTEMPT, 'native_cad', 'working_pack'),
      { expectedFileCount: 75 }),
  }
  assert.deepEqual(after, realBefore)
})

const failures = checks.filter((row) => !row.ok)
console.log(JSON.stringify({
  status: failures.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failures.length,
  solidWorksStarted: false,
  r22Mutated: false,
  checks,
}, null, 2))
process.exitCode = failures.length ? 1 : 0
