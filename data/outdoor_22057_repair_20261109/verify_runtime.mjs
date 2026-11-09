import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs'
import { resolve } from 'node:path'
import { acquireNativeWorkerInstanceLock as oldLock } from '../../tools/run_16029_native_worker.mjs'
import { acquireNativeWorkerInstanceLock as newLock } from './runtime_v17/scripts/native_worker_lock.mjs'
const work = import.meta.dirname
const stage = resolve(work, 'runtime_v17')
const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''))
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex')
const q = read(resolve(stage, 'qualification.json'))
const checks = []
for (const [p, h] of Object.entries(q.support_sha256)) assert.equal(hash(resolve(stage, p)), h, p)
for (const [p, h] of Object.entries(q.external_support_sha256)) assert.equal(hash(p), h, p)
checks.push('all runtime and external hashes match')
const worker = readFileSync(resolve(work, '../../tools/run_16029_native_worker.mjs'), 'utf8')
const isolated = readFileSync(resolve(stage, 'scripts/native_worker_lock.mjs'), 'utf8')
const helpers = worker.slice(worker.indexOf('function isoNow('), worker.indexOf('function requestDigestForPlan('))
assert.ok(isolated.replace(/\r\n/g, '\n').includes(helpers.replace(/\r\n/g, '\n'))); checks.push('lock helpers identical apart from line endings')
assert.ok(!/from\s+['"](?!node:)/.test(isolated)); checks.push('lock has no indoor imports')
const dataDir = resolve(work, 'lock-test')
assert.ok(!existsSync(dataDir)); mkdirSync(dataDir)
let a = oldLock({ dataDir, workerId: 'existing-indoor' })
assert.throws(() => newLock({ dataDir, workerId: 'outdoor' }), { code: 'native_worker_already_active' })
a.heartbeat(); assert.equal(a.release(), true)
let b = newLock({ dataDir, workerId: 'outdoor' })
assert.throws(() => oldLock({ dataDir, workerId: 'existing-indoor' }), { code: 'native_worker_already_active' })
b.heartbeat()
assert.throws(() => oldLock({ dataDir, workerId: 'clock-advanced', clock: () => new Date(Date.now() + 3600000) }), { code: 'native_worker_already_active' })
const ownerPath = resolve(b.lockDir, 'owner.json'); const owner = read(ownerPath)
writeFileSync(ownerPath, JSON.stringify({ ...owner, token: 'other-owner' }))
assert.equal(b.release(), false)
assert.throws(() => b.heartbeat(), { code: 'native_worker_lock_lost' })
writeFileSync(ownerPath, JSON.stringify(owner)); assert.equal(b.release(), true)
checks.push('cross-runtime mutual exclusion, heartbeat, live stale owner and token checks')
assert.throws(() => newLock({ dataDir, workerId: '../escape' }), { code: 'unsafe_identifier' })
assert.ok(!/Stop-Process|taskkill|\.Kill\(/i.test(readFileSync(resolve(stage, 'scripts/cleanup_owned_residual_poll_r4.ps1'), 'utf8')))
checks.push('invalid worker rejected; shutdown has no forced termination')
const result = { status: 'PASS', checks, qualification_sha256: hash(resolve(stage,'qualification.json')), support_files: Object.keys(q.support_sha256).length }
writeFileSync(resolve(work, 'runtime_preflight.json'), JSON.stringify(result, null, 2))
console.log(JSON.stringify(result))
