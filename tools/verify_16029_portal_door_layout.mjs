// Isolated browser acceptance: editable door layout -> confirmation -> saved native task. No CAD or worker.
import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { createHash, randomBytes } from 'node:crypto'
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const require = createRequire('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json')
const { chromium } = require('playwright')
const output = resolve(ROOT, 'output/door-layout-editor-20261001/browser-' + Date.now())
const dataDir = resolve(output, 'data')
mkdirSync(dataDir, { recursive:true })
const staticFile = '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'
mkdirSync(resolve(output, 'storage/模型下载'), { recursive:true })
copyFileSync(resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1', staticFile), resolve(output, 'storage/模型下载', staticFile))
const username = 'door-layout-' + randomBytes(4).toString('hex')
const password = randomBytes(24).toString('base64url')
const invite = randomBytes(18).toString('hex')
writeFileSync(resolve(dataDir, 'review_download_invite_code.txt'), invite)
const redact = value => String(value).replaceAll(password, '[redacted]').replaceAll(invite, '[redacted]')
const hash = value => createHash('sha256').update(value).digest('hex')
const checks = [], layouts = [], pageErrors = [], posts = [], tasks = []
let server, browser, page, baseUrl, stage = 'startup', serverError = ''

async function availablePort() {
  const probe = createServer()
  await new Promise((done, reject) => { probe.once('error', reject); probe.listen(0, '127.0.0.1', done) })
  const port = probe.address().port
  await new Promise(done => probe.close(done))
  return port
}

async function openSpecification() {
  await page.locator('[data-workspace-nav=new-model]').click()
  await page.locator('[data-workspace-view=new-model].active').waitFor()
}

async function resetMixed() {
  await openSpecification()
  await page.locator('#cabinetHeight').fill('1917')
  await page.locator('#requestedWidthMm').fill('760')
  await page.locator('#cabinetDepth').fill('550')
  await page.locator('[data-layout-preset=mixed-six]').click()
}

async function heights(side) {
  return page.locator('[data-door-height="' + side + '"]').evaluateAll(inputs => inputs.map(input => input.value))
}

async function fillHeights(side, values) {
  for (let index = 0; index < values.length; index++) {
    const input = page.locator('#doorHeight' + side + (index + 1))
    if (await input.getAttribute('readonly') !== null) await page.locator('[data-door-auto="' + side + '"][data-row="' + (index + 1) + '"]').click()
    await input.fill(String(values[index]))
  }
}

async function expectEnabled(enabled) {
  assert.equal(await page.locator('#generationSubmitButton').isEnabled(), enabled, await page.locator('#generationCapabilityStatus').innerText())
}

async function capture(label) {
  await page.evaluate(() => window.scrollTo({ top:0, behavior:'instant' }))
  await page.screenshot({ path:resolve(output, label + '.png'), fullPage:true })
}

async function confirm() {
  await expectEnabled(true)
  const before = posts.length
  await page.locator('#generationSubmitButton').click()
  await page.locator('#generationConfirm').waitFor()
  assert.equal(posts.length, before, 'Review must not submit a task')
}

async function submit() {
  await confirm()
  const responsePending = page.waitForResponse(response => response.url().endsWith('/generation-request') && response.request().method() === 'POST')
  await page.locator('#confirmGenerationSubmit').click()
  const response = await responsePending
  const result = await response.json()
  assert.ok([200,201].includes(response.status()), redact(JSON.stringify(result)))
  await page.locator('[data-workspace-view=my-models].active').waitFor()
  const task = JSON.parse(readFileSync(resolve(dataDir, 'native_model_requests', result.requestId + '.json'), 'utf8'))
  tasks.push({ requestId:result.requestId, status:result.status, doorCount:task.doorCount })
  return { result, task, payload:response.request().postDataJSON() }
}

async function checkLayout(label) {
  const layout = await page.evaluate(() => {
    const visible = element => element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden'
    const overflow = [...document.querySelectorAll('body *')].filter(visible).filter(element => {
      const rect = element.getBoundingClientRect()
      return rect.left < -1 || rect.right > innerWidth + 1
    }).map(element => element.id || element.tagName).slice(0, 12)
    const clippedInputs = [...document.querySelectorAll('input,select,textarea')].filter(visible).filter(element => element.getBoundingClientRect().width < 70).map(element => element.id || element.name)
    const smallButtons = [...document.querySelectorAll('button,.button,[data-workspace-nav]')].filter(visible).filter(element => element.getBoundingClientRect().height < 43.5).map(element => element.id || element.textContent.trim())
    return { viewport:innerWidth, documentWidth:document.documentElement.scrollWidth, overflow, clippedInputs, smallButtons }
  })
  layouts.push({ label, ...layout })
  assert.ok(layout.documentWidth <= layout.viewport + 1, label + ': page overflows')
  assert.deepEqual(layout.overflow, [], label + ': elements overflow')
  assert.deepEqual(layout.clippedInputs, [], label + ': inputs clipped')
  if (layout.viewport <= 820) assert.deepEqual(layout.smallButtons, [], label + ': touch targets too small')
}

try {
  baseUrl = 'http://127.0.0.1:' + await availablePort()
  server = spawn(process.execPath, ['tools/serve_16029_review_downloads.mjs'], { cwd:ROOT, windowsHide:true,
    env:{ ...process.env, STUDIO_REVIEW_HOST:'127.0.0.1', STUDIO_REVIEW_PORT:baseUrl.split(':').at(-1),
      STUDIO_REVIEW_DATA_DIR:dataDir, STUDIO_REVIEW_STORAGE_ROOT:resolve(output, 'storage'), STUDIO_REVIEW_SKIP_ASSET_SYNC:'1' },
    stdio:['ignore','ignore','pipe'] })
  server.stderr.on('data', bytes => { serverError = (serverError + redact(bytes.toString())).slice(-4000) })
  let available = false
  for (let attempt = 0; attempt < 80; attempt++) {
    assert.equal(server.exitCode, null, serverError)
    try { if ((await fetch(baseUrl + '/status.json', { signal:AbortSignal.timeout(1000) })).ok) { available = true; break } } catch {}
    await new Promise(done => setTimeout(done, 200))
  }
  assert.equal(available, true, serverError)
  browser = await chromium.launch({ executablePath:process.env.STUDIO_BROWSER_EXECUTABLE || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless:true })
  page = await browser.newPage({ viewport:{ width:1440,height:1000 }, reducedMotion:'reduce' })
  page.setDefaultTimeout(20000)
  page.on('pageerror', error => pageErrors.push(redact(error.message)))
  page.on('request', request => { if (new URL(request.url()).pathname === '/generation-request' && request.method() === 'POST') posts.push(request.postDataJSON()) })
  await page.goto(baseUrl + '/register')
  await page.locator('[name=username]').fill(username)
  await page.locator('[name=password]').fill(password)
  await page.locator('[name=inviteCode]').fill(invite)
  await page.locator('button[type=submit]').click()
  await page.locator('#generationForm').waitFor()
  await page.locator('[name=customerRequirementReference]').fill('独立门序浏览器验收')

  stage = 'default-and-independent-counts'
  assert.equal(await page.locator('#doorCount').getAttribute('readonly'), '')
  assert.deepEqual(await heights('L'), ['908','603','298'])
  assert.deepEqual(await heights('R'), ['298','603','908'])
  assert.equal(await page.locator('#generationForm').getAttribute('data-delivery-mode'), 'verified_native_model')
  await page.locator('[data-view=front]').click()
  const defaultCanvasHash = hash(await page.locator('#cabinetPreview').screenshot())
  const originalRight = await heights('R')
  await page.locator('#leftDoorCount').fill('2')
  assert.deepEqual(await heights('L'), ['908','908'])
  assert.deepEqual(await heights('R'), originalRight)
  assert.equal(await page.locator('#doorCount').inputValue(), '5')
  assert.notEqual(hash(await page.locator('#cabinetPreview').screenshot()), defaultCanvasHash)
  checks.push('independent-column-counts-readonly-total-and-preview')

  stage = 'asymmetric-manual-order'
  await resetMixed()
  await page.locator('#rightDoorCount').fill('4')
  await fillHeights('L', [900,600,309])
  await fillHeights('R', [400,450,500,452])
  await expectEnabled(true)
  assert.equal(await page.locator('#doorCount').inputValue(), '7')
  assert.match(await page.locator('#hudDoorLayout').innerText(), /左 3 \/ 右 4 \/ 共 7 门/)
  assert.match(await page.locator('#generatedPrompt').innerText(), /900 \/ 600 \/ 309 mm（自下向上）/)
  const customCanvasHash = hash(await page.locator('#cabinetPreview').screenshot({ path:resolve(output, 'asymmetric-front-canvas.png') }))
  assert.notEqual(customCanvasHash, defaultCanvasHash)
  await capture('01-asymmetric-desktop')
  await page.setViewportSize({ width:390,height:844 })
  await checkLayout('390-asymmetric')
  await capture('02-asymmetric-mobile')
  await page.setViewportSize({ width:1440,height:1000 })
  await confirm()
  assert.match(await page.locator('#generationConfirmSummary').innerText(), /左 3 门 \/ 右 4 门 \/ 共 7 门/)
  assert.match(await page.locator('#generationConfirmSummary').innerText(), /400 \/ 450 \/ 500 \/ 452 mm/)
  await page.locator('#returnToSpecButton').click()
  const asymmetric = await submit()
  assert.equal(asymmetric.result.modelReady, false)
  assert.equal(asymmetric.result.status, 'native_task_created')
  assert.equal(Boolean(asymmetric.result.downloadUrl || asymmetric.result.directUrl), false)
  assert.equal(Number(asymmetric.task.doorCount), 7)
  assert.deepEqual(asymmetric.task.columnDoorCounts, [3,4])
  assert.deepEqual(asymmetric.task.parametricRequest, asymmetric.payload.parametricRequest)
  const contract = build16029ParametricContract(asymmetric.task.parametricRequest)
  assert.deepEqual(contract.rowsByColumn.L.map(row => row.doorHeightMm), [900,600,309])
  assert.deepEqual(contract.rowsByColumn.R.map(row => row.doorHeightMm), [400,450,500,452])
  assert.notEqual(contract.boundaries.L[0].doorGapCenterYmm, contract.boundaries.R[0].doorGapCenterYmm)
  await page.locator('[data-task-row][data-task-state=queued]').waitFor()
  assert.equal(await page.locator('[data-task-row][data-task-state=queued] a[href*="/download/"]').count(), 0)
  checks.push('asymmetric-odd-total-manual-heights-confirmation-post-contract-pending-without-download')

  stage = 'invalid-heights-and-top-fill'
  await openSpecification()
  const savedRight = await heights('R')
  for (const value of ['99','0','','900.0001','899','1100']) {
    await page.locator('#doorHeightL1').fill(value)
    await expectEnabled(false)
    assert.equal(await page.locator('#doorLayoutStatus').getAttribute('data-valid'), 'false')
  }
  assert.match(await page.locator('#leftDoorSummary').innerText(), /超出 200/)
  await page.locator('#doorHeightL1').fill('1700')
  assert.equal(await page.locator('[data-column-fill-top=L]').isEnabled(), false)
  await page.locator('#doorHeightL1').fill('899')
  assert.match(await page.locator('#leftDoorSummary').innerText(), /还差 1/)
  await page.locator('[data-column-fill-top=L]').click()
  assert.deepEqual(await heights('L'), ['899','600','310'])
  assert.deepEqual(await heights('R'), savedRight)
  await expectEnabled(true)
  checks.push('invalid-blank-small-excess-precision-heights-and-fill-top-only-in-target-column')

  stage = 'invalid-counts-and-dimensions'
  for (const value of ['0','25','2.5','']) {
    await page.locator('#leftDoorCount').fill(value)
    await expectEnabled(false)
    assert.deepEqual(await heights('R'), savedRight)
  }
  await page.locator('#leftDoorCount').fill('3')
  await expectEnabled(true)
  await page.locator('#cabinetHeight').fill('')
  await expectEnabled(false)
  await page.locator('#cabinetHeight').fill('1917')
  await expectEnabled(true)
  await page.locator('#requestedWidthMm').fill('1300')
  await expectEnabled(false)
  await page.locator('#requestedWidthMm').fill('760')
  await page.locator('#cabinetDepth').fill('700')
  await expectEnabled(false)
  await page.locator('#cabinetDepth').fill('550')
  checks.push('invalid-column-counts-empty-height-and-custom-order-outside-parametric-range')

  stage = 'height-linkage-and-confirmation-invalidation'
  await resetMixed()
  await fillHeights('L', [900,600,309])
  const manualLeft = await heights('L')
  const autoRight = await heights('R')
  await page.locator('#cabinetHeight').fill('2000')
  assert.deepEqual(await heights('L'), manualLeft)
  assert.notDeepEqual(await heights('R'), autoRight)
  assert.match(await page.locator('#leftDoorSummary').innerText(), /还差 83/)
  await expectEnabled(false)
  await page.locator('[data-column-fill-top=L]').click()
  assert.deepEqual(await heights('L'), ['900','600','392'])
  await confirm()
  await page.locator('[data-column-equalize=L]').click()
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  assert.equal(await page.locator('#generationSubmitButton').isVisible(), true)
  assert.equal(new Set(await heights('L')).size, 1)
  await confirm()
  await page.locator('[data-layout-preset=equal-four]').click()
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  assert.equal(await page.locator('#doorCount').inputValue(), '4')
  await confirm()
  await page.locator('#doorHeightR1').fill('908')
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  await resetMixed()
  await confirm()
  await page.locator('[data-column-fill-top=L]').click()
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  await confirm()
  await page.locator('#leftDoorCount').fill('2')
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  checks.push('auto-height-linkage-manual-retention-equalize-presets-and-confirmation-invalidated')

  stage = 'recipe-matching'
  await resetMixed()
  await fillHeights('L', [908.001,603,297.999])
  assert.equal(await page.locator('#generationForm').getAttribute('data-delivery-mode'), 'native_task_pending')
  const customSix = await submit()
  assert.ok(customSix.payload.parametricRequest)
  assert.deepEqual(customSix.payload.parametricRequest.columns[0].doors.map(door => door.heightMm), [908.001,603,297.999])
  assert.equal(Number(customSix.task.doorCount), 6)
  assert.equal(customSix.result.status, 'native_task_created')
  assert.equal(Boolean(customSix.result.downloadUrl || customSix.result.directUrl), false)
  await resetMixed()
  const exact = await submit()
  assert.equal(exact.payload.parametricRequest, undefined)
  assert.equal(exact.result.engineeringAssistanceReady, true)
  assert.ok(exact.result.downloadUrl || exact.result.directUrl)
  checks.push('custom-six-door-layout-cannot-reuse-v37-default-preset-still-matches')

  stage = 'integer-cabinet-unrounded-automatic-layout'
  await resetMixed()
  await page.locator('#requestedWidthMm').fill('900')
  await page.locator('#cabinetHeight').fill('2000')
  await page.locator('#cabinetDepth').fill('500')
  await page.locator('#leftDoorCount').fill('4')
  await page.locator('#rightDoorCount').fill('9')
  const equalNine = await submit()
  assert.deepEqual(equalNine.task.parametricRequest.columns.map(column => column.doors),
    [Array.from({ length:4 }, () => ({ heightUnits:1 })), Array.from({ length:9 }, () => ({ heightUnits:1 }))])
  const equalContract = build16029ParametricContract(equalNine.task.parametricRequest)
  assert.equal(new Set(equalContract.rowsByColumn.R.map(row => row.doorHeightMm)).size, 1)
  assert.equal(equalContract.rowsByColumn.R[0].doorHeightMm, 1913 / 9 - 7)
  assert.ok(Math.abs(equalContract.boundaries.R[2].shelfCenterYmm - (27.5 + 1913 / 3)) < 1e-9)
  assert.ok(Math.abs(equalContract.boundaries.R[5].shelfCenterYmm - (27.5 + 1913 * 2 / 3)) < 1e-9)
  await openSpecification()
  assert.deepEqual(await heights('R'), Array(9).fill('205.556'))
  await page.locator('[data-column-mode=R][data-allocation-mode=ratio]').click()
  await page.locator('#doorRatioR1').fill('2')
  const ratioNine = await submit()
  assert.deepEqual(ratioNine.task.parametricRequest.columns[1].doors, [2,1,1,1,1,1,1,1,1].map(heightUnits => ({ heightUnits })))
  await openSpecification()
  await page.locator('[data-column-mode=R][data-allocation-mode=height]').click()
  await page.locator('#doorHeightR1').fill('380')
  const fixedNine = await submit()
  const fixedDoors = fixedNine.task.parametricRequest.columns[1].doors
  assert.equal(fixedDoors[0].heightMm, 380)
  assert.ok(fixedDoors.every(door => 'heightMm' in door && !('heightUnits' in door)))
  build16029ParametricContract(fixedNine.task.parametricRequest)
  await openSpecification()
  await page.locator('[data-column-equalize=R]').click()
  const equalAgain = await submit()
  assert.deepEqual(equalAgain.task.parametricRequest.columns[1].doors, Array.from({ length:9 }, () => ({ heightUnits:1 })))
  checks.push('integer-900-cabinet-equal-nine-full-precision-ratio-manual-transition-and-equalize')

  stage = 'single-door-and-high-count'
  await resetMixed()
  await page.locator('#leftDoorCount').fill('1')
  await page.locator('#rightDoorCount').fill('1')
  assert.equal(await page.locator('#doorCount').inputValue(), '2')
  assert.deepEqual(await heights('L'), ['1823'])
  await expectEnabled(true)
  await page.locator('#cabinetHeight').fill('1700')
  await page.locator('#leftDoorCount').fill('16')
  await expectEnabled(false)
  assert.match(await page.locator('#doorLayoutStatus').innerText(), /至少 100/)
  await page.locator('#cabinetHeight').fill('2200')
  await page.locator('#leftDoorCount').fill('18')
  await page.locator('#rightDoorCount').fill('17')
  assert.equal(await page.locator('#doorCount').inputValue(), '35')
  await expectEnabled(false)
  assert.match(await page.locator('#doorLayoutStatus').innerText(), /最多 34/)
  await page.locator('#leftDoorCount').fill('17')
  await expectEnabled(true)
  for (const viewport of [{ width:1440,height:1000 },{ width:1024,height:900 },{ width:820,height:1000 },{ width:390,height:844 },{ width:360,height:800 }]) {
    await page.setViewportSize(viewport)
    await checkLayout(viewport.width + '-34-doors')
    if ([1440,390].includes(viewport.width)) await capture('03-' + viewport.width + '-34-doors')
  }
  await page.setViewportSize({ width:1440,height:1000 })
  const manyDoors = await submit()
  assert.equal(Number(manyDoors.task.doorCount), 34)
  assert.deepEqual(manyDoors.task.columnDoorCounts, [17,17])
  const manyContract = build16029ParametricContract(manyDoors.task.parametricRequest)
  assert.equal(new Set(manyContract.rowsByColumn.L.map(row => row.doorHeightMm)).size, 1)
  assert.ok(manyContract.rowsByColumn.L.every(row => row.doorHeightMm >= 100))
  checks.push('one-door-per-column-minimum-height-total-cap-and-34-door-responsive-unrounded-ratios')

  assert.deepEqual(pageErrors, [])
  writeFileSync(resolve(output, 'result.json'), JSON.stringify({ pass:true, checks, layouts, tasks, posts, pageErrors,
    previewHashes:{ default:defaultCanvasHash, asymmetric:customCanvasHash },
    sourceHash:hash(readFileSync(resolve(ROOT, 'tools/serve_16029_review_downloads.mjs'))),
    cadExecuted:false, productionDataModified:false, workerStarted:false, browser:'Microsoft Edge via bundled Playwright' }, null, 2) + '\n')
  console.log(JSON.stringify({ pass:true, output, checks:checks.length, layouts:layouts.length, posts:posts.length, pageErrors:pageErrors.length }))
} catch (error) {
  if (page) await capture('failure').catch(() => {})
  writeFileSync(resolve(output, 'result.json'), JSON.stringify({ pass:false, stage, error:redact(error.stack), checks, layouts, pageErrors }, null, 2) + '\n')
  console.error(JSON.stringify({ pass:false, stage, output, error:redact(error.message) }))
  process.exitCode = 1
} finally {
  if (browser) await browser.close()
  if (server && server.exitCode === null && server.signalCode === null) server.kill()
}
