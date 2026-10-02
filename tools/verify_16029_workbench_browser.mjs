import assert from 'node:assert/strict'

import { createRequire } from 'node:module'

import { randomBytes, createHash } from 'node:crypto'

import { existsSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs'

import { dirname, resolve } from 'node:path'

import { fileURLToPath } from 'node:url'

import { createServer } from 'node:net'

import { spawn } from 'node:child_process'

import { createNativeTaskStore } from '../tools/lib/locker_16029_native_task_store.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')

const requirePaths = [resolve(root, 'apps/web/package.json'), resolve(dirname(process.execPath), '../package.json'),

  ...(process.env.USERPROFILE ? [resolve(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json')] : [])]

let chromium

for (const requirePath of requirePaths) {

  try { ({ chromium } = createRequire(requirePath)('playwright')); break } catch {}

}

assert.ok(chromium, 'Playwright required: install the apps/web dev dependency or use the bundled Node runtime')

const readyTaskPath = resolve(root, process.env.STUDIO_REVIEW_READY_TASK || 'data/native_model_requests/PARAMETRIC-AUTO-889-1967-575-20260915.json')

assert.ok(existsSync(readyTaskPath), 'Local runtime prerequisite: provide STUDIO_REVIEW_READY_TASK pointing to an existing validated ready task and its evidence; CAD generation is not run')

const output = resolve(root, process.env.STUDIO_REVIEW_BROWSER_OUTPUT || 'output/playwright/workbench-' + Date.now()), data = resolve(output, 'data')

const portProbe = createServer()

await new Promise((yes, no) => { portProbe.once('error', no); portProbe.listen(0, '127.0.0.1', yes) })

const port = portProbe.address().port

await new Promise((yes, no) => portProbe.close(error => error ? no(error) : yes()))

const base = `http://127.0.0.1:${port}`

const edgePaths = [process.env.STUDIO_REVIEW_BROWSER_EXECUTABLE, ...[process.env['ProgramFiles(x86)'], process.env.ProgramFiles]

  .filter(Boolean).map(folder => resolve(folder, 'Microsoft/Edge/Application/msedge.exe'))]

const browserPath = edgePaths.find(path => path && existsSync(path))

mkdirSync(data, { recursive: true })

const invite = randomBytes(18).toString('hex'), password = randomBytes(24).toString('base64url')

writeFileSync(resolve(data, 'review_download_invite_code.txt'), invite)

const server = spawn(process.execPath, [resolve(root, 'tools/serve_16029_review_downloads.mjs')], { cwd: root, windowsHide: true,

  env: { ...process.env, STUDIO_REVIEW_HOST: '127.0.0.1', STUDIO_REVIEW_PORT: String(port), STUDIO_REVIEW_DATA_DIR: data,

    STUDIO_REVIEW_STORAGE_ROOT: resolve(output, 'storage'), STUDIO_REVIEW_SKIP_ASSET_SYNC: '1' }, stdio: ['ignore', 'pipe', 'pipe'] })

let browser, serverError = ''; server.stderr.on('data', b => { serverError += b.toString() })

try {

  let available = false

  for (let i = 0; i < 60; i++) { try { if ((await fetch(base + '/status.json', { signal: AbortSignal.timeout(1000) })).ok) { available = true; break } } catch {} await new Promise(r => setTimeout(r, 500)) }

  assert.ok(available, serverError)

  browser = await chromium.launch({ ...(browserPath ? { executablePath: browserPath } : {}), headless: true })

  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true })

  const errors = []; page.on('pageerror', e => errors.push(e.message))

  await page.goto(base + '/register')

  writeFileSync(resolve(output, 'register-snapshot.txt'), await page.locator('body').innerText())

  await page.locator('[name=username]').fill('parametric-browser-qa')

  await page.locator('[name=password]').fill(password)

  await page.locator('[name=inviteCode]').fill(invite)

  await page.locator('button[type=submit]').click()

  await page.waitForSelector('#generationForm')

  writeFileSync(resolve(output, 'form-before.txt'), await page.locator('#generationForm').innerText())

  await page.locator('[name=customerRequirementReference]').fill('组合参数浏览器验收')

  await page.locator('#requestedWidthMm').fill('877')

  await page.locator('#cabinetHeight').fill('1967')

  await page.locator('#cabinetDepth').fill('575')

  await page.locator('#leftDoorCount').fill('2')

  await page.locator('#rightDoorCount').fill('2')

  await page.locator('#rightDoorCount').press('Tab')

  assert.equal(await page.locator('#doorCount').inputValue(), '4')

  assert.equal(await page.locator('#generationSubmitButton').isEnabled(), true)

  assert.match(await page.locator('#generationCapabilityStatus').innerText(), /原生参数化生成队列/)

  await page.screenshot({ path: resolve(output, 'desktop.png'), fullPage: true })

  await page.locator('#generationSubmitButton').click()

  assert.equal(await page.locator('#generationConfirm').isVisible(), true)

  await page.locator('#returnToSpecButton').click()

  assert.equal(await page.locator('#generationConfirm').isVisible(), false)

  const responseWait = page.waitForResponse(r => r.url().endsWith('/generation-request') && r.request().method() === 'POST')

  await page.locator('#generationSubmitButton').click()

  await page.locator('#confirmGenerationSubmit').click()

  const response = await responseWait, result = await response.json()

  assert.equal(response.status(), 201)

  assert.equal(result.modelReady, false)

  const stored = JSON.parse(readFileSync(resolve(data, 'native_model_requests', result.requestId + '.json'), 'utf8'))

  assert.deepEqual(stored.parametricRequest.cabinet, { widthMm: 877, heightMm: 1967, depthMm: 575 })

  assert.deepEqual(stored.parametricRequest.columns.map(c => c.doors.length), [2, 2])

  await page.locator('[data-task-feedback-id="' + result.requestId + '"]').click()

  assert.equal(await page.locator('#feedbackGenerationRequestId').inputValue(), result.requestId)

  await page.locator('[data-workspace-nav=my-models]').click()

  const pollingStore = createNativeTaskStore({ dataDir: resolve(data, 'native_model_requests') })

  let pollingTask = await pollingStore.claimTask({ taskId: result.requestId, workerId: 'ui-polling-test', leaseMs: 120000 })

  const leaseArgs = () => ({ taskId: result.requestId, workerId: 'ui-polling-test', leaseId: pollingTask.nativeBuild.lease.id, expectedRevision: pollingTask.revision })

  pollingTask = await pollingStore.transition({ ...leaseArgs(), transitionId: 'poll-planning', toState: 'planning' })

  pollingTask = await pollingStore.transition({ ...leaseArgs(), transitionId: 'poll-building', toState: 'building' })

  await page.locator('[data-task-row][data-task-state=building]').waitFor({ timeout: 12000 })

  pollingTask = await pollingStore.fail({ ...leaseArgs(), transitionId: 'poll-failure', code: 'test_native_failure', message: 'Isolated test failure', retryable: false })

  await page.locator('[data-task-row][data-task-state=failed]').waitFor({ timeout: 12000 })

  await page.screenshot({ path: resolve(output, 'submitted-state.png'), fullPage: true })

  const completed = JSON.parse(readFileSync(readyTaskPath, 'utf8'))

  assert.equal(completed.nativeBuild.state, 'ready')

  completed.username = 'parametric-browser-qa'

  await createNativeTaskStore({ dataDir: resolve(data, 'native_model_requests') }).createOrImport(completed)

  const detail = await page.request.get(base + '/generation-request/' + completed.id)

  assert.equal(detail.status(), 200)

  const ready = (await detail.json()).request

  assert.equal(ready.modelReady, true)

  const download = await page.request.get(base + ready.downloadUrl)

  assert.equal(download.status(), 200)

  const archiveHash = createHash('sha256').update(await download.body()).digest('hex').toUpperCase()

  assert.equal(archiveHash, completed.nativeBuild.result.archiveSha256)

  await page.reload()

  await page.locator('[data-workspace-nav=my-models]').click()

  await page.locator('[data-task-filter=ready]').click()

  assert.equal(await page.locator('[data-task-row]:visible').count(), 1)

  await page.screenshot({ path: resolve(output, 'tasks.png'), fullPage: true })

  await page.locator('#taskFilterInput').fill('不存在的任务')

  assert.equal(await page.locator('[data-task-row]:visible').count(), 0)

  await page.setViewportSize({ width: 390, height: 844 })

  await page.locator('[data-workspace-nav=new-model]').click()

  await page.screenshot({ path: resolve(output, 'mobile.png'), fullPage: true })

  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true)

  assert.deepEqual(errors, [])

  writeFileSync(resolve(output, 'result.json'), JSON.stringify({ pass: true, requestId: result.requestId, realWorkerTaskId: completed.id,

    actualInput: stored.parametricRequest, archiveHash, pageErrors: errors, browser: browserPath || 'installed Playwright Chromium', solidWorksStarted: false }, null, 2))

  console.log(JSON.stringify({ pass: true, output, archiveHash }))

} finally {

  if (browser) await browser.close()

  if (server.exitCode === null && server.signalCode === null) {

    const stopped = new Promise(done => server.once('close', done))

    server.kill()

    await stopped

  }

}

