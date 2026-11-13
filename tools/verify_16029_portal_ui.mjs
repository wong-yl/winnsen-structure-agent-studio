// Real-browser UI acceptance in isolated storage. Reuses range QA publications; never starts CAD.
// Usage: node tools/verify_16029_portal_ui.mjs <range-qa-output-directory>
import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { randomBytes, createHash } from 'node:crypto'
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import { createServer } from 'node:net'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
assert.ok(process.argv[2], 'Supply the successful range QA output directory')
const seedDir = resolve(ROOT, process.argv[2])
const rangeEvidence = JSON.parse(readFileSync(resolve(seedDir, 'result.json'), 'utf8'))
assert.equal(rangeEvidence.pass, true)
assert.ok(rangeEvidence.workerEvidence?.taskId, 'Range QA must include a real completed publication')
const require = createRequire('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json')
const { chromium } = require('playwright')
const output = resolve(ROOT, 'output/playwright/16029-ui-' + Date.now())
const dataDir = resolve(output, 'data')
mkdirSync(dataDir, { recursive:true })
const staticFileName = '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'
mkdirSync(resolve(output, 'storage/模型下载'), { recursive:true })
copyFileSync(resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1', staticFileName), resolve(output, 'storage/模型下载', staticFileName))
const username = 'ui-review-' + randomBytes(4).toString('hex')
const password = randomBytes(24).toString('base64url')
const invite = randomBytes(18).toString('hex')
writeFileSync(resolve(dataDir, 'review_download_invite_code.txt'), invite)
const redact = text => String(text).replaceAll(password, '[redacted]').replaceAll(invite, '[redacted]')
const sleep = ms => new Promise(done => setTimeout(done, ms))
const checks = [], pageErrors = [], layouts = [], captures = []
let server, browser, page, baseUrl, stage = 'startup', serverError = '', generationPostCount = 0

async function availablePort() {
  const probe = createServer()
  await new Promise((done, reject) => { probe.once('error', reject); probe.listen(0, '127.0.0.1', done) })
  const port = probe.address().port
  await new Promise(done => probe.close(done))
  return port
}

async function capture(label, fullPage = true) {
  await page.evaluate(() => window.scrollTo({ top:0, behavior:'instant' }))
  await page.screenshot({ path:resolve(output, label + '.png'), fullPage })
  captures.push(label + '.png')
}

async function checkLayout(label) {
  const layout = await page.evaluate(() => {
    const visible = element => element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden'
    const overflow = [...document.querySelectorAll('body *')].filter(visible).filter(element => {
      const rect = element.getBoundingClientRect()
      return rect.left < -1 || rect.right > innerWidth + 1
    }).map(element => element.id || element.className?.baseVal || element.className || element.tagName).slice(0, 12)
    const clippedInputs = [...document.querySelectorAll('input,select,textarea')].filter(visible).filter(element => {
      const rect = element.getBoundingClientRect()
      return rect.width < 70 || rect.right > innerWidth + 1
    }).map(element => element.id || element.name)
    const brokenImages = [...document.images].filter(visible).filter(img => !img.complete || !img.naturalWidth).map(img => img.getAttribute('src'))
    const smallButtons = [...document.querySelectorAll('button,.button,[data-workspace-nav]')].filter(visible)
      .filter(element => element.getBoundingClientRect().height < 43.5).map(element => element.id || element.textContent.trim())
    return { viewport:innerWidth, documentWidth:document.documentElement.scrollWidth, overflow, clippedInputs, brokenImages, smallButtons }
  })
  layouts.push({ label, ...layout })
  assert.ok(layout.documentWidth <= layout.viewport + 1, label + ': page overflows')
  assert.deepEqual(layout.overflow, [], label + ': elements overflow')
  assert.deepEqual(layout.clippedInputs, [], label + ': inputs clipped')
  assert.deepEqual(layout.brokenImages, [], label + ': images missing')
  if (layout.viewport <= 820) assert.deepEqual(layout.smallButtons, [], label + ': touch targets too small')
}

async function openWorkspace(name) {
  await page.locator('[data-workspace-nav="' + name + '"]').click()
  await page.locator('[data-workspace-view="' + name + '"].active').waitFor()
  assert.equal(await page.locator('[data-workspace-nav="' + name + '"]').getAttribute('aria-current'), 'page')
}

async function seedTask(taskId, reference) {
  const item = JSON.parse(readFileSync(resolve(seedDir, 'data/native_model_requests', taskId + '.json'), 'utf8'))
  item.username = username
  item.customerRequirementReference = reference
  return taskStore.createOrImport(item)
}

const taskStore = createNativeTaskStore({ dataDir:resolve(dataDir, 'native_model_requests') })
try {
  const port = await availablePort()
  baseUrl = 'http://127.0.0.1:' + port
  server = spawn(process.execPath, ['tools/serve_16029_review_downloads.mjs'], { cwd:ROOT, windowsHide:true,
    env:{ ...process.env, STUDIO_REVIEW_HOST:'127.0.0.1', STUDIO_REVIEW_PORT:String(port), STUDIO_REVIEW_DATA_DIR:dataDir,
      STUDIO_REVIEW_STORAGE_ROOT:resolve(output, 'storage'), STUDIO_REVIEW_SKIP_ASSET_SYNC:'1' }, stdio:['ignore','pipe','pipe'] })
  server.stdout.on('data', () => {})
  server.stderr.on('data', bytes => { serverError = (serverError + redact(bytes.toString())).slice(-4000) })
  let available = false
  for (let i = 0; i < 80; i++) {
    assert.equal(server.exitCode, null, serverError)
    try { if ((await fetch(baseUrl + '/status.json', { signal:AbortSignal.timeout(1000) })).ok) { available = true; break } } catch {}
    await sleep(200)
  }
  assert.equal(available, true, serverError)
  browser = await chromium.launch({ executablePath:process.env.STUDIO_BROWSER_EXECUTABLE || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless:true })
  page = await browser.newPage({ viewport:{ width:1440, height:1000 }, acceptDownloads:true, reducedMotion:'reduce' })
  page.setDefaultTimeout(20000)
  page.on('pageerror', error => pageErrors.push(redact(error.message)))
  page.on('request', request => { if (new URL(request.url()).pathname === '/generation-request' && request.method() === 'POST') generationPostCount++ })

  stage = 'auth'
  await page.goto(baseUrl + '/login')
  assert.equal(await page.title(), '16029 参数化工程工作台 · Winnsen')
  await checkLayout('desktop-login')
  await capture('01-desktop-login')
  await page.locator('[name=username]').fill('missing-account')
  await page.locator('[name=password]').fill('invalid-password')
  await page.locator('button[type=submit]').click()
  await page.locator('.alert').waitFor()
  assert.match(await page.locator('.alert').innerText(), /账号或密码不正确/)
  await capture('02-login-error')
  await page.goto(baseUrl + '/register')
  await checkLayout('desktop-register')
  await capture('03-desktop-register')
  await page.locator('[name=username]').fill(username)
  await page.locator('[name=password]').fill(password)
  await page.locator('[name=inviteCode]').fill(invite)
  await page.locator('button[type=submit]').click()
  await page.locator('#generationForm').waitFor()
  checks.push('registration-and-login-error')

  stage = 'empty-tasks'
  await openWorkspace('my-models')
  await page.waitForFunction(() => document.getElementById('taskRefreshStatus').textContent.includes('状态已更新'))
  assert.equal(await page.locator('#generationRequestList .task-empty').count(), 1)
  assert.equal(await page.locator('#generationRequestEmptyFilter').isVisible(), false)
  await capture('04-empty-tasks')
  await page.locator('.task-empty a').click()
  await page.locator('[data-workspace-view="new-model"].active').waitFor()
  checks.push('empty-state-persists-after-poll-and-links-to-specification')

  stage = 'layout-preview'
  await page.locator('[name=customerRequirementReference]').fill('界面验收 / 850 柜体')
  await page.locator('#requestedWidthMm').fill('850')
  await page.locator('#cabinetHeight').fill('1950')
  await page.locator('#cabinetDepth').fill('380')
  await page.locator('#doorCount').press('Tab')
  assert.equal(await page.locator('#generationSubmitButton').isEnabled(), true)
  await capture('05-desktop-specification')
  const viewHashes = new Set()
  for (const view of ['front','right','top','iso']) {
    await page.locator('[data-view="' + view + '"]').click()
    assert.equal(await page.locator('[data-view="' + view + '"]').evaluate(button => button.classList.contains('view-active')), true)
    viewHashes.add(await page.locator('#cabinetPreview').evaluate(canvas => canvas.toDataURL()))
  }
  assert.equal(viewHashes.size, 4)
  await page.locator('#cabinetPreview').screenshot({ path:resolve(output, 'feedback-evidence.png') })
  const before = generationPostCount
  await page.locator('#generationSubmitButton').click()
  await page.locator('#generationConfirm').waitFor()
  assert.equal(generationPostCount, before)
  await capture('06-desktop-confirmation')
  await page.locator('#returnToSpecButton').click()
  assert.equal(await page.locator('#generationConfirm').isVisible(), false)
  await page.locator('#widthInputMode').selectOption('installed_door_panel_width')
  assert.equal(await page.locator('#requestedWidthMm').inputValue(), '362')
  assert.equal(await page.locator('#cabinetWidth').inputValue(), '850')
  await page.locator('#widthInputMode').selectOption('cabinet_outer_width')
  checks.push('four-distinct-preview-views-width-basis-and-confirmation')

  stage = 'task-states'
  const ready = await seedTask(rangeEvidence.workerEvidence.taskId, '界面验收 / 已校核模型')
  const building = await seedTask(rangeEvidence.pendingRequests[0].requestId, '界面验收 / 700 柜体构建')
  const queued = await seedTask(rangeEvidence.pendingRequests[1].requestId, '界面验收 / 1200 柜体排队')
  await page.locator('[name=customerRequirementReference]').fill('界面验收 / 待处理需求')
  await page.locator('#requestedWidthMm').fill('888')
  await page.locator('#generationSubmitButton').click()
  await page.locator('#generationConfirm').waitFor()
  const createResponse = page.waitForResponse(response => response.url().endsWith('/generation-request') && response.request().method() === 'POST')
  await page.locator('#confirmGenerationSubmit').click()
  const created = await createResponse
  assert.equal(created.status(), 201)
  const createdTask = await created.json()
  assert.equal(createdTask.modelReady, false)
  const failed = JSON.parse(readFileSync(resolve(dataDir, 'native_model_requests', createdTask.requestId + '.json'), 'utf8'))
  for (const [record, shouldFail] of [[building, false],[failed, true]]) {
    let task = await taskStore.claimTask({ taskId:record.id, workerId:'ui-acceptance', leaseMs:120000 })
    const args = () => ({ taskId:task.id, workerId:'ui-acceptance', leaseId:task.nativeBuild.lease.id, expectedRevision:task.revision })
    task = await taskStore.transition({ ...args(), transitionId:'ui-planning', toState:'planning' })
    task = await taskStore.transition({ ...args(), transitionId:'ui-building', toState:'building' })
    if (shouldFail) await taskStore.fail({ ...args(), transitionId:'ui-failed', code:'isolated_ui_fixture', message:'隔离界面验收状态，不代表生产任务。', retryable:false })
  }
  await page.reload()
  await openWorkspace('my-models')
  for (const state of ['ready','building','queued','failed']) await page.locator('[data-task-state="' + state + '"]').waitFor()
  await page.waitForFunction(() => document.getElementById('taskRefreshStatus').textContent.includes('状态已更新'))
  for (const state of ['ready','building','queued','failed']) {
    await page.locator('[data-task-filter="' + state + '"]').click()
    assert.equal(await page.locator('[data-task-row]:visible').count(), 1)
    await page.locator('[data-task-row]:visible .task-status.' + state).waitFor()
    assert.equal(await page.locator('[data-task-row]:visible .task-status').innerText(), { ready:'可下载',building:'构建',queued:'排队',failed:'未通过' }[state])
    assert.equal(await page.locator('[data-task-row]:visible time').count(), 1)
  }
  await page.locator('[data-task-filter=all]').click()
  await page.locator('#taskFilterInput').fill('no-matching-task')
  assert.equal(await page.locator('[data-task-row]:visible').count(), 0)
  assert.equal(await page.locator('#generationRequestEmptyFilter').isVisible(), true)
  await capture('07-empty-filter')
  await page.locator('#taskFilterInput').fill('')
  await capture('08-desktop-tasks')
  checks.push('real-publication-and-isolated-state-transitions-filters-timestamps')

  stage = 'feedback-upload'
  await page.locator('[data-task-feedback-id="' + ready.id + '"]').click()
  assert.equal(await page.locator('#feedbackGenerationRequestId').inputValue(), ready.id)
  await page.locator('#feedbackForm [name=componentName]').fill('界面验收 / 左列门板锁孔')
  await page.locator('#feedbackForm [name=currentProblem]').fill('隔离浏览器验收记录：检查任务关联、截图上传和档案回显。')
  await page.locator('#feedbackForm [name=attachments]').setInputFiles(resolve(output, 'feedback-evidence.png'))
  await capture('09-desktop-feedback')
  const feedbackResponse = page.waitForResponse(response => response.url().endsWith('/feedback') && response.request().method() === 'POST')
  const feedbackReload = page.waitForEvent('load').catch(() => null)
  await page.locator('#feedbackSubmitButton').click()
  const issueResponse = await feedbackResponse
  assert.equal(issueResponse.status(), 200)
  const issue = await issueResponse.json()
  assert.ok(issue.feedbackId)
  await feedbackReload
  checks.push('task-bound-feedback-upload')

  stage = 'efficiency-record'
  await openWorkspace('model-feedback')
  await page.locator('[data-feedback-tab=effect]').click()
  assert.equal(await page.locator('#efficiencySubmitButton').isEnabled(), false)
  assert.equal(await page.locator('#efficiencyForm [name=generationRequestId] option[value="' + ready.id + '"]').count(), 0)
  const staticResponse = await page.request.post(baseUrl + '/generation-request', { data:{
    customerParameterFlow:'v1', customerRequirementReference:'界面验收 / V37 精确规格', widthInputMode:'cabinet_outer_width', requestedWidthMm:760, cabinetDepth:550, doorCount:6,
  } })
  const staticTask = await staticResponse.json()
  assert.equal(staticResponse.status(), 200, redact(JSON.stringify(staticTask)))
  assert.equal(staticTask.engineeringContinuationEligible, true)
  await page.reload()
  await openWorkspace('model-feedback')
  await page.locator('[data-feedback-tab=issue]').focus()
  await page.keyboard.press('ArrowRight')
  assert.equal(await page.locator('[data-feedback-tab=effect]').getAttribute('aria-selected'), 'true')
  assert.ok(page.url().endsWith('#efficiency-trial'))
  const form = page.locator('#efficiencyForm')
  await form.locator('[name=generationRequestId]').selectOption(staticTask.requestId)
  await form.locator('[name=taskKind]').selectOption('typical_change')
  await form.locator('[name=taskReference]').fill('隔离界面验收 / 使用效果')
  await form.locator('[name=taskScope]').fill('仅测试门户交互；以下数字为隔离验收输入，不代表真实工程效率。')
  await form.locator('[name=traditionalEstimatedMinutes]').fill('60')
  await form.locator('[name=generatedModelActualMinutes]').fill('20')
  await form.locator('[name=directReusePercent]').fill('100')
  await form.locator('[name=engineeringDrawing]').selectOption('not_required')
  await form.locator('[name=bom]').selectOption('not_required')
  await capture('10-desktop-efficiency')
  const efficiencyResponse = page.waitForResponse(response => response.url().endsWith('/feedback') && response.request().method() === 'POST')
  const efficiencyReload = page.waitForEvent('load').catch(() => null)
  await page.locator('#efficiencySubmitButton').click()
  const effectResponse = await efficiencyResponse
  const effect = await effectResponse.json()
  assert.equal(effectResponse.status(), 200, redact(JSON.stringify(effect)))
  await efficiencyReload
  assert.equal(await page.locator('#efficiency-trial').isVisible(), true)
  assert.match(await page.locator('#efficiencyList').innerText(), /节省 40 分钟/)
  checks.push('eligible-model-selection-keyboard-tabs-efficiency-submission-and-pane-preserved-on-reload')

  stage = 'responsive-pages'
  for (const viewport of [{ width:1440,height:1000 },{ width:1024,height:900 },{ width:820,height:1000 },{ width:390,height:844 },{ width:360,height:800 }]) {
    await page.setViewportSize(viewport)
    for (const name of ['new-model','my-models','model-feedback','archive']) {
      await openWorkspace(name)
      if (name === 'model-feedback') await page.locator('[data-feedback-tab=issue]').click()
      const label = viewport.width + '-' + name
      await checkLayout(label)
      if ([1440,390].includes(viewport.width)) await capture('11-' + label)
      if (name === 'model-feedback') {
        await page.locator('[data-feedback-tab=effect]').click()
        await checkLayout(viewport.width + '-efficiency')
        if ([1440,390].includes(viewport.width)) await capture('12-' + viewport.width + '-efficiency')
      }
      if (name === 'archive') assert.ok((await page.locator('#feedbackList').innerText()).includes(issue.feedbackId))
    }
  }
  checks.push('all-workspaces-at-five-viewport-widths')

  stage = 'mobile-auth-and-login'
  await page.goto(baseUrl + '/logout')
  for (const route of ['login','register']) {
    await page.goto(baseUrl + '/' + route)
    await checkLayout('360-' + route)
    await capture('13-mobile-' + route)
  }
  await page.goto(baseUrl + '/login')
  await page.locator('[name=username]').fill(username)
  await page.locator('[name=password]').fill(password)
  await page.locator('button[type=submit]').click()
  await page.locator('#generationForm').waitFor()
  checks.push('mobile-auth-logout-and-successful-login')
  assert.deepEqual(pageErrors, [])
  const sourceHash = createHash('sha256').update(readFileSync(resolve(ROOT, 'tools/serve_16029_review_downloads.mjs'))).digest('hex')
  writeFileSync(resolve(output, 'result.json'), JSON.stringify({ pass:true, checks, layouts, captures, pageErrors, sourceHash,
    publicationSource:rangeEvidence.workerEvidence, generationPostCount, fixtureTaskIds:[ready.id,building.id,queued.id,failed.id],
    cadExecuted:false, productionDataModified:false, browser:'Microsoft Edge via bundled Playwright' }, null, 2) + '\n')
  console.log(JSON.stringify({ pass:true, output, checks:checks.length, layouts:layouts.length, pageErrorCount:pageErrors.length, sourceHash }))
} catch (error) {
  if (page) await capture('failure').catch(() => {})
  writeFileSync(resolve(output, 'result.json'), JSON.stringify({ pass:false, stage, error:redact(error.stack), checks, layouts, pageErrors }, null, 2) + '\n')
  console.error(JSON.stringify({ pass:false, stage, output, error:redact(error.message) }))
  process.exitCode = 1
} finally {
  if (browser) await browser.close()
  if (server && server.exitCode === null && server.signalCode === null) server.kill()
}
