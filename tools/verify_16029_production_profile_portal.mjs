import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createHash, randomBytes } from 'node:crypto'
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { Script, runInNewContext } from 'node:vm'
import { native16029RequestFingerprint, normalize16029NativeModelRequest } from './locker_16029_native_generator.mjs'
import { INDOOR_16029_PRODUCTION_PROFILE as PROFILE, INDOOR_16029_PRODUCTION_PROFILE_LABEL as LABEL, read16029ProductionSelection } from './lib/locker_16029_production_profile.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const fixtureRoot = mkdtempSync(resolve(tmpdir(), '16029-production-profile-'))
const dataDir = resolve(fixtureRoot, 'data')
const storageRoot = resolve(fixtureRoot, 'storage')
const selectionPath = resolve(fixtureRoot, 'workers/native_model_requests/parametric_v1/build-selection.json')
const seedName = '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'
const seedSource = resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1', seedName)
const checks = []
const hash = value => createHash('sha256').update(value).digest('hex')
const legacyPayload = { customerRequirementReference: 'profile-isolation-test', widthInputMode: 'cabinet_outer_width', requestedWidthMm: 760, cabinetDepth: 550, doorCount: 6 }
const orderInput = { cabinet: { widthMm: 1200, heightMm: 1917, depthMm: 485 }, columns: [
  { side: 'L', doors: [316, ...Array(11).fill(130)].map(heightMm => ({ heightMm })) },
  { side: 'R', doors: [179, ...Array(12).fill(130)].map(heightMm => ({ heightMm })) },
] }
let child, cookie = '', baseUrl = '', stderr = ''

function selection(value) { writeFileSync(selectionPath, JSON.stringify(value)) }
function stored(id) { return JSON.parse(readFileSync(resolve(dataDir, 'native_model_requests', id + '.json'), 'utf8')) }
async function request(path, payload) {
  return fetch(baseUrl + path, { method: payload ? 'POST' : 'GET', headers: { Cookie: cookie, ...(payload ? { 'Content-Type': 'application/json' } : {}) }, ...(payload ? { body: JSON.stringify(payload) } : {}) })
}

function pagePayload(html, layout) {
  const scripts = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(match => match[1])
  for (const source of scripts) new Script(source)
  const source = scripts.find(value => value.includes('function buildGenerationPayload()'))
  assert.ok(source)
  const body = source.slice(source.indexOf('function buildGenerationPayload()'), source.indexOf('function clearGenerationConfirmation('))
  const profile = html.match(/id="generationForm"[^>]*data-production-profile="([^"]*)"/)[1]
  const payloadEntries = Object.entries({ ...legacyPayload, cabinetWidth: 760, cabinetHeight: 1917, doorWidth: 317 })
  const form = { dataset: { productionProfile: profile, deliveryMode: 'verified_native_model', parametricEligible: 'true' } }
  const result = runInNewContext(body + '\nbuildGenerationPayload()', {
    generationForm: form, productionProfile: profile, currentDoorLayout: () => layout,
    FormData: class { entries() { return payloadEntries[Symbol.iterator]() } },
  })
  return JSON.parse(JSON.stringify(result))
}

function publicTask(item, publicationReady) {
  const source = readFileSync(resolve(ROOT, 'tools/serve_16029_review_downloads.mjs'), 'utf8')
  const body = source.slice(source.indexOf('function publicGenerationRequest(item)'), source.indexOf('function isEngineeringContinuationEligible(item)'))
  return runInNewContext(body + '\npublicGenerationRequest(item)', {
    item, structuredClone, is16029StructureAssistanceReady: () => false,
    inspect16029ParametricTaskPublication: () => ({ ok: publicationReady }),
    inspectNativeStructureAssistancePublication: () => ({ ok: publicationReady }),
    publicNativeBuild: build => build ? { state: build.state, modelReady: build.modelReady } : null,
    DATA_DIR: dataDir,
  })
}

try {
  mkdirSync(dataDir, { recursive: true })
  mkdirSync(dirname(selectionPath), { recursive: true })
  mkdirSync(resolve(storageRoot, '模型下载'), { recursive: true })
  copyFileSync(seedSource, resolve(storageRoot, '模型下载', seedName))
  const invite = randomBytes(16).toString('hex')
  const password = randomBytes(24).toString('base64url')
  writeFileSync(resolve(dataDir, 'review_download_invite_code.txt'), invite)
  writeFileSync(resolve(dataDir, 'review_portal.local.json'), '{}')
  const currentSelection = JSON.parse(readFileSync(resolve(ROOT, 'workers/native_model_requests/parametric_v1/build-selection.json'), 'utf8'))
  selection(currentSelection)
  const selectedProfile = read16029ProductionSelection(fixtureRoot)
  assert.equal(selectedProfile.enabled, currentSelection.verified === true && currentSelection.productionProfile === PROFILE)
  checks.push('current-build-selection-read-only')
  for (const value of [{ verified: true }, { verified: false, productionProfile: PROFILE }, { verified: true, productionProfile: 'unknown' }]) {
    selection(value)
    assert.equal(read16029ProductionSelection(fixtureRoot).enabled, false)
  }
  writeFileSync(selectionPath, '{')
  assert.equal(read16029ProductionSelection(fixtureRoot).enabled, false)
  selection({ verified: true })
  checks.push('selection-requires-verified-and-exact-profile')

  const probe = createServer()
  await new Promise((done, reject) => { probe.once('error', reject); probe.listen(0, '127.0.0.1', done) })
  const port = probe.address().port
  await new Promise(done => probe.close(done))
  baseUrl = 'http://127.0.0.1:' + port
  child = spawn(process.execPath, [resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')], { cwd: ROOT, windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'],
    env: { ...process.env, STUDIO_REVIEW_ROOT: fixtureRoot, STUDIO_REVIEW_CONFIG: resolve(dataDir, 'review_portal.local.json'), STUDIO_REVIEW_DATA_DIR: dataDir, STUDIO_REVIEW_STORAGE_ROOT: storageRoot, STUDIO_REVIEW_PORT: String(port), STUDIO_REVIEW_HOST: '127.0.0.1', STUDIO_REVIEW_SKIP_ASSET_SYNC: '1' } })
  child.stderr.on('data', bytes => { stderr = (stderr + bytes.toString()).slice(-4000) })
  let available = false
  for (let attempt = 0; attempt < 60; attempt++) {
    assert.equal(child.exitCode, null, stderr)
    try { if ((await fetch(baseUrl + '/status.json', { signal: AbortSignal.timeout(1000) })).ok) { available = true; break } } catch {}
    await new Promise(done => setTimeout(done, 100))
  }
  assert.equal(available, true, stderr)
  const registration = await fetch(baseUrl + '/register', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ username: 'profile-test', password, inviteCode: invite }), redirect: 'manual' })
  assert.equal(registration.status, 302)
  cookie = registration.headers.get('set-cookie').split(';', 1)[0]

  const legacyResponse = await request('/generation-request', legacyPayload)
  assert.equal(legacyResponse.status, 200)
  const legacy = await legacyResponse.json()
  assert.equal(legacy.modelReady, true)
  assert.equal(legacy.deliveryMode, 'verified_native_model')
  const download = await request(legacy.downloadUrl)
  assert.equal(download.status, 200)
  assert.equal(hash(Buffer.from(await download.arrayBuffer())), hash(readFileSync(seedSource)))
  const legacyTask = stored(legacy.requestId)
  assert.equal(legacyTask.productionProfile, undefined)
  const layout = { valid: true, columns: [
    { side: 'L', doors: [908, 603, 298].map(heightMm => ({ heightMm })) },
    { side: 'R', doors: [298, 603, 908].map(heightMm => ({ heightMm })) },
  ] }
  const legacyHtml = await (await request('/')).text()
  assert.equal(pagePayload(legacyHtml, layout).payload.parametricRequest, undefined)
  assert.ok(legacyHtml.includes('已生成工艺版本：历史版本（未记录版本身份）'))
  checks.push('legacy-seed-download-and-page-payload-compatible')

  const newPayload = { ...legacyPayload, productionProfile: PROFILE }
  const newResponse = await request('/generation-request', newPayload)
  assert.equal(newResponse.status, 201)
  const next = await newResponse.json()
  assert.equal(next.modelReady, false)
  assert.equal(next.taskType, 'native_solidworks_build_task')
  assert.equal(next.downloadUrl, '')
  assert.equal(next.productionProfile, PROFILE)
  assert.equal(next.generatedProductionProfile, '')
  const nextTask = stored(next.requestId)
  assert.equal(nextTask.productionProfile, PROFILE)
  assert.equal(nextTask.parametricRequest.productionProfile, PROFILE)
  assert.notEqual(nextTask.requestFingerprint, legacyTask.requestFingerprint)
  assert.equal(nextTask.requestFingerprint, native16029RequestFingerprint(nextTask))
  assert.equal(normalize16029NativeModelRequest(newPayload).matchType, 'native_build_task')
  assert.equal((await (await request('/generation-request', newPayload)).json()).requestId, next.requestId)
  checks.push('same-size-new-profile-creates-native-task-and-distinct-fingerprint')

  const orderResponse = await request('/generation-request', { customerRequirementReference: 'order-left12-right13', parametricRequest: { ...orderInput, productionProfile: PROFILE } })
  assert.equal(orderResponse.status, 201)
  const order = await orderResponse.json()
  const orderTask = stored(order.requestId)
  assert.equal(orderTask.productionProfile, PROFILE)
  assert.deepEqual(orderTask.columnDoorCounts, [12, 13])
  assert.equal(Number(orderTask.doorCount), 25)
  assert.deepEqual(orderTask.parametricRequest, { ...orderInput, productionProfile: PROFILE })
  assert.equal(orderTask.generatedProductionProfile, undefined)
  assert.equal((await (await request('/generation-request/' + order.requestId)).json()).request.generatedProductionProfile, '')
  const profileAtTop = normalize16029NativeModelRequest({ customerRequirementReference: 'order-left12-right13', parametricRequest: orderInput, productionProfile: PROFILE })
  assert.deepEqual(profileAtTop.normalizedRequest.parametricRequest, orderTask.parametricRequest)
  assert.equal(profileAtTop.normalizedRequest.requestFingerprint, orderTask.requestFingerprint)
  const oldOrder = normalize16029NativeModelRequest({ customerRequirementReference: 'order-left12-right13', parametricRequest: orderInput })
  assert.notEqual(oldOrder.normalizedRequest.requestFingerprint, orderTask.requestFingerprint)
  checks.push('left12-right13-dimensions-and-bottom-up-order-preserved')

  for (const payload of [
    { ...legacyPayload, productionProfile: 'unknown' },
    { ...legacyPayload, productionProfile: PROFILE, parametricRequest: { ...orderInput, productionProfile: 'unknown' } },
    { ...legacyPayload, parametricRequest: { ...orderInput, productionProfile: 'unknown' } },
  ]) {
    assert.equal((await request('/generation-request', payload)).status, 422)
    assert.equal(native16029RequestFingerprint(payload), '')
  }
  checks.push('unknown-and-conflicting-profiles-rejected')

  const generatedFixture = { ...orderTask, nativeBuild: { state: 'ready', modelReady: true, result: { productionProfile: PROFILE } } }
  assert.equal(publicTask(generatedFixture, true).generatedProductionProfile, PROFILE)
  assert.equal(publicTask(generatedFixture, false).generatedProductionProfile, '')
  assert.equal(publicTask({ ...generatedFixture, nativeBuild: { ...generatedFixture.nativeBuild, result: {} } }, true).generatedProductionProfile, '')
  assert.equal(publicTask({ ...generatedFixture, nativeBuild: { ...generatedFixture.nativeBuild, result: { productionProfile: 'historical-generated-version' } } }, true).generatedProductionProfile, 'historical-generated-version')
  checks.push('generated-version-comes-from-checked-task-result')

  selection({ verified: true, productionProfile: PROFILE })
  const enabledHtml = await (await request('/')).text()
  assert.ok(enabledHtml.includes('工艺版本：' + LABEL))
  assert.ok(enabledHtml.includes('任务工艺版本：' + LABEL + '；已生成工艺版本：等待生成与检查'))
  const enabled = pagePayload(enabledHtml, layout)
  assert.equal(enabled.expectsVerifiedNativeModel, false)
  assert.equal(enabled.payload.productionProfile, PROFILE)
  assert.equal(enabled.payload.parametricRequest.productionProfile, PROFILE)
  assert.deepEqual(enabled.payload.parametricRequest.columns, layout.columns)
  assert.ok(enabledHtml.includes('const exactVerifiedRecipe = !productionProfile &&'))
  checks.push('verified-selection-enables-new-profile-and-bypasses-legacy-seed')
  selection({ verified: false, productionProfile: PROFILE })
  const disabledHtml = await (await request('/')).text()
  assert.ok(!disabledHtml.includes('<p class="muted">工艺版本：' + LABEL))
  assert.equal(pagePayload(disabledHtml, layout).payload.productionProfile, undefined)
  checks.push('unverified-selection-restores-legacy-mode-without-restarting')

  console.log(JSON.stringify({ status: 'PASS', checksTotal: checks.length, checks, solidWorksStarted: false, livePortalRestarted: false }, null, 2))
} finally {
  if (child && child.exitCode === null) {
    const exited = new Promise(done => child.once('exit', done))
    child.kill()
    await exited
  }
  rmSync(fixtureRoot, { recursive: true, force: true })
}
