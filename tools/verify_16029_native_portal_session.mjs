import assert from 'node:assert/strict'
import { randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:net'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
mkdirSync(resolve(root, 'tmp'), { recursive: true })
const fixture = mkdtempSync(resolve(root, 'tmp/verify-native-portal-session-'))
const dataDir = resolve(fixture, 'data')
mkdirSync(dataDir)
writeFileSync(resolve(dataDir, 'review_download_invite_code.txt'), 'session-fixture', 'utf8')
const portProbe = createServer()
await new Promise((yes, no) => { portProbe.once('error', no); portProbe.listen(0, '127.0.0.1', yes) })
const port = portProbe.address().port
await new Promise((yes, no) => portProbe.close(error => error ? no(error) : yes()))
const base = `http://127.0.0.1:${port}`
const child = spawn(process.execPath, [resolve(root, 'tools/serve_16029_review_downloads.mjs')], {
  cwd: root, windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'],
  env: { ...process.env, STUDIO_REVIEW_ROOT: fixture, STUDIO_REVIEW_CONFIG: resolve(fixture, 'missing-config.json'),
    STUDIO_REVIEW_HOST: '127.0.0.1', STUDIO_REVIEW_PORT: String(port), STUDIO_REVIEW_DATA_DIR: dataDir,
    STUDIO_REVIEW_STORAGE_ROOT: resolve(fixture, 'storage'), STUDIO_REVIEW_SKIP_ASSET_SYNC: '1' },
})
let stderr = ''
child.stderr.on('data', bytes => { stderr += bytes.toString() })
const delay = ms => new Promise(yes => setTimeout(yes, ms))
const checks = []
async function check(name, run) {
  try { await run(); checks.push({ name, ok: true }) }
  catch (error) { checks.push({ name, ok: false, error: error.message }) }
}

try {
  let ready = false
  for (let attempt = 0; attempt < 80; attempt++) {
    if (child.exitCode !== null) throw new Error('isolated portal exited: ' + stderr)
    try { ready = (await fetch(base + '/status.json', { signal: AbortSignal.timeout(500) })).ok } catch {}
    if (ready) break
    await delay(75)
  }
  assert.ok(ready, 'isolated portal did not start')
  const password = randomBytes(24).toString('base64url')
  const register = async username => {
    const response = await fetch(base + '/register', { method: 'POST', redirect: 'manual',
      body: new URLSearchParams({ username, password, inviteCode: 'session-fixture' }) })
    assert.equal(response.status, 302)
    const cookie = (response.headers.get('set-cookie') || '').split(';', 1)[0]
    assert.ok(cookie.startsWith('review_session='), 'session cookie missing')
    return cookie
  }
  const owner = await register('session-owner'), other = await register('session-other')
  const payload = { customerParameterFlow: 'native_v1', customerRequirementReference: 'isolated session fixture',
    widthInputMode: 'cabinet_outer_width', requestedWidthMm: 888, cabinetDepth: 550, doorCount: 14 }
  const submitted = await fetch(base + '/generation-request', { method: 'POST',
    headers: { Cookie: owner, 'Content-Type': 'application/json' }, body: JSON.stringify(payload) })
  assert.equal(submitted.status, 201)
  const taskId = (await submitted.json()).requestId
  await check('task_owner_and_account_isolation', async () => {
    assert.equal((await fetch(base + '/generation-request/' + taskId, { headers: { Cookie: owner } })).status, 200)
    for (const path of ['/generation-request/' + taskId, '/native-assistance-download/' + taskId]) {
      assert.equal((await fetch(base + path, { headers: { Cookie: other } })).status, 404)
    }
    assert.equal((await fetch(base + '/generation-request/' + taskId + '/delete', { method: 'POST', headers: { Cookie: other } })).status, 404)
    const listing = await (await fetch(base + '/generation-requests.json', { headers: { Cookie: other } })).json()
    assert.equal(listing.requests.length, 0)
  })
  await check('malformed_cookie_does_not_break_authentication', async () => {
    assert.equal((await fetch(base + '/generation-requests.json', { headers: { Cookie: owner + '; unrelated=%' } })).status, 200)
    assert.equal((await fetch(base + '/generation-requests.json', { redirect: 'manual', headers: { Cookie: 'review_session=%' } })).status, 302)
  })
  await check('parametric_overflow_rejected_by_http', async () => {
    const parametricRequest = { cabinet: { widthMm: 888, heightMm: 1917, depthMm: 550 },
      columns: ['L', 'R'].map(side => ({ side, doors: [1e308, 1e308, 1e308].map(heightUnits => ({ heightUnits })) })) }
    assert.equal((await fetch(base + '/generation-request', { method: 'POST', headers: { Cookie: owner, 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...payload, parametricRequest }) })).status, 422)
  })
  await check('logout_revokes_server_session', async () => {
    assert.equal((await fetch(base + '/logout', { redirect: 'manual', headers: { Cookie: owner } })).status, 302)
    const staleGet = await fetch(base + '/generation-requests.json', { redirect: 'manual', headers: { Cookie: owner } })
    assert.equal(staleGet.status, 302)
    assert.equal(staleGet.headers.get('location'), '/login')
    assert.equal((await fetch(base + '/generation-request', { method: 'POST', headers: { Cookie: owner, 'Content-Type': 'application/json' },
      body: JSON.stringify(payload) })).status, 401)
    assert.equal((await fetch(base + '/generation-requests.json', { headers: { Cookie: other } })).status, 200)
  })
} finally {
  if (child.exitCode === null && child.signalCode === null) {
    const closed = new Promise(yes => child.once('close', yes))
    child.kill()
    await Promise.race([closed, delay(3000)])
    if (child.exitCode === null && child.signalCode === null) { child.kill('SIGKILL'); await closed }
  }
  rmSync(fixture, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 })
}

const failed = checks.filter(row => !row.ok)
console.log(JSON.stringify({ status: failed.length ? 'FAIL' : 'PASS', checksTotal: checks.length,
  checksFailed: failed.length, solidWorksStarted: false, checks }, null, 2))
if (failed.length) process.exitCode = 1
