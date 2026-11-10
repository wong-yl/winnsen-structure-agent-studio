import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { basename, resolve } from 'node:path'
import { ARMS, activePortal } from './run-right-mirror.mjs'
const checks = []
const check = (name, action) => { action(); checks.push({ name, pass: true }) }
check('three exact ordered diagnostic arms', () => assert.deepEqual(ARMS, ['mirror-create', 'temporary-cold', 'final-cold']))
check('all active portal states are rejected by selector', () => { for (const state of ['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested']) assert.equal(activePortal([{ id: 'task', nativeBuild: { state, lease: null } }]).length, 1) })
check('nonnull lease on blocked or needs_input tasks remains active', () => { for (const state of ['blocked', 'needs_input']) assert.equal(activePortal([{ id: 'task', nativeBuild: { state, lease: {} } }]).length, 1) })
check('historical blocked tasks without leases remain untouched', () => assert.equal(activePortal([{ id: 'task', nativeBuild: { state: 'blocked', lease: null } }]).length, 0))
check('actual child guard retains lock for missing incomplete unreadable or live observations', () => {
  const source = readFileSync(new URL('./run-right-mirror.mjs', import.meta.url), 'utf8')
  const start = source.indexOf('    const childLive = () => {'), end = source.indexOf('\n    while (observerRunning', start)
  assert.ok(start >= 0 && end > start)
  const factory = new Function('fixture', 'let { nativeChildIdentity, nativeChildPid, nativeChildUnconfirmed, currentArmOut } = fixture; const { existsSync, read, resolve, spawnSync, need, context, console } = fixture;\n' + source.slice(start, end) + '\nreturn childLive;')
  const identity = { pid: 77, startTicks: '123456', executable: 'D:\\probe\\RightIdentityProbe.exe' }
  const observe = (values = {}) => {
    const files = values.files ?? {}
    return factory({ nativeChildIdentity: values.identity ?? null, nativeChildPid: values.pid ?? null, nativeChildUnconfirmed: values.unconfirmed ?? true, currentArmOut: 'D:\\probe\\arm', existsSync: path => Object.hasOwn(files, basename(path)), read: path => { const value = files[basename(path)]; if (value instanceof Error) throw value; return value }, resolve, spawnSync: () => ({ status: values.status ?? 0, stdout: JSON.stringify(values.current ?? null) }), need: (value, message) => assert.ok(value, message), context: { build: { executable: identity.executable } }, console: { error() {} } })()
  }
  assert.equal(observe(), true)
  assert.equal(observe({ pid: 77, current: identity }), true)
  assert.equal(observe({ pid: 77 }), false)
  assert.equal(observe({ files: { 'native-child-started.json': { pid: 77 } }, current: identity }), true)
  assert.equal(observe({ files: { 'native-child-started.json': { pid: 77 } } }), false)
  assert.equal(observe({ files: { 'session.json': { nativeChildAlive: false } } }), false)
  assert.equal(observe({ identity, unconfirmed: false, current: identity }), true)
  assert.equal(observe({ identity, unconfirmed: false, current: { ...identity, startTicks: '999999' } }), false)
  assert.equal(observe({ identity: { pid: 77 }, unconfirmed: false, current: identity }), true)
  assert.equal(observe({ identity: { ...identity, executable: 'D:\\other.exe' }, unconfirmed: false, current: identity }), true)
  assert.equal(observe({ pid: 77, status: 1 }), true)
  assert.equal(observe({ files: { 'session.json': new Error('partial journal') } }), true)
})
console.log(JSON.stringify({ checks, count: checks.length, failed: 0, nativeCadExecuted: false, taskCreated: false, modelAccepted: false }))
