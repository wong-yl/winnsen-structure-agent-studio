import assert from 'node:assert/strict'
import { spawn, spawnSync } from 'node:child_process'
import { createHash, randomBytes } from 'node:crypto'
import { createRequire } from 'node:module'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:net'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  PORTAL_PRESETS,
  auditFlatDxfDirectory,
  normalizeVariableTrialPortalRequest,
  packageVariableTrialDeliverable,
  runVariableTrialPackage,
} from '../workers/master_v2/variable_trial/portal_pipeline.mjs'
import { createVariableTrialTaskStore } from './lib/locker_16029_variable_trial_tasks.mjs'
import { runNextVariableTrialPortalTask } from './run_16029_variable_trial_portal_worker.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const require = createRequire('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json')
const { chromium } = require('playwright')

const delay = (ms) => new Promise((done) => setTimeout(done, ms))

async function freePort() {
  const probe = createServer()
  await new Promise((done, reject) => {
    probe.once('error', reject)
    probe.listen(0, '127.0.0.1', done)
  })
  const port = probe.address().port
  await new Promise((done) => probe.close(done))
  return port
}

function makeMinimalValidFlatDxf(width = 100, height = 50, includeFastenerOverlay = false) {
  const lines = [
    '0', 'SECTION', '2', 'HEADER',
    '9', '$INSUNITS', '70', '4',
    '0', 'ENDSEC',
    '0', 'SECTION', '2', 'TABLES',
    '0', 'TABLE', '2', 'LAYER', '70', '3',
    '0', 'LAYER', '2', 'CUT', '70', '0', '62', '7', '6', 'CONTINUOUS',
    '0', 'LAYER', '2', 'BEND', '70', '0', '62', '3', '6', 'CONTINUOUS',
    '0', 'LAYER', '2', 'MARK', '70', '0', '62', '5', '6', 'CONTINUOUS',
    '0', 'ENDTAB',
    '0', 'ENDSEC',
    '0', 'SECTION', '2', 'ENTITIES',
    '0', 'LINE', '8', 'CUT', '10', '0', '20', '0', '11', String(width), '21', '0',
    '0', 'LINE', '8', 'CUT', '10', String(width), '20', '0', '11', String(width), '21', String(height),
    '0', 'LINE', '8', 'CUT', '10', String(width), '20', String(height), '11', '0', '21', String(height),
    '0', 'LINE', '8', 'CUT', '10', '0', '20', String(height), '11', '0', '21', '0',
    '0', 'CIRCLE', '8', 'CUT', '10', String(width / 2), '20', String(height / 2), '40', '4.4',
    '0', 'LINE', '8', 'BEND', '10', '10', '20', '0', '11', '10', '21', String(height),
    '0', 'LINE', '8', 'MARK', '10', '20', '20', '15', '11', '30', '21', '15',
  ]
  if (includeFastenerOverlay) {
    lines.push('0', 'CIRCLE', '8', 'CUT', '10', String(width / 2), '20', String(height / 2), '40', '4.4')
  }
  lines.push('0', 'ENDSEC', '0', 'EOF')
  return lines.join('\r\n') + '\r\n'
}

const tempRoot = mkdtempSync(resolve(ROOT, 'tmp-vt-portal-test-'))
let server = null
let browser = null

try {
  // 1. Validate request normalization across presets, width modes, heightMm mode, and invalid bounds.
  for (const preset of PORTAL_PRESETS) {
    const res = normalizeVariableTrialPortalRequest({ ...preset })
    assert.equal(res.ok, true, `${preset.id} must validate: ${res.message}`)
  }

  const byDoorWidth = normalizeVariableTrialPortalRequest({
    widthInputMode: 'installed_door_panel_width',
    requestedWidthMm: 437,
    cabinet: { heightMm: 2000, depthMm: 500 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
      { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
    ],
  })
  assert.equal(byDoorWidth.ok, true, byDoorWidth.message)
  assert.equal(byDoorWidth.normalizedRequest.cabinet.widthMm, 1000)

  const exactHeightMm = normalizeVariableTrialPortalRequest({
    cabinet: { widthMm: 850, heightMm: 1917, depthMm: 485 },
    allocationMode: 'height_mm',
    columns: [
      { side: 'L', doors: [{ heightMm: 400 }, { heightMm: 500 }, { heightMm: 909 }] },
      { side: 'R', doors: [{ heightMm: 600 }, { heightMm: 1216 }] },
    ],
  })
  assert.equal(exactHeightMm.ok, true, exactHeightMm.message)
  assert.ok(Math.abs(exactHeightMm.layoutSummary.rowsByColumn.L[2].doorHeightMm - 909) < 0.001)

  assert.equal(normalizeVariableTrialPortalRequest({
    cabinet: { widthMm: 699, heightMm: 2000, depthMm: 500 },
    columns: PORTAL_PRESETS[0].columns,
  }).ok, false)
  assert.equal(normalizeVariableTrialPortalRequest({
    cabinet: { widthMm: 1000, heightMm: 2000, depthMm: 500 },
    allocationMode: 'height_mm',
    columns: [
      { side: 'L', doors: [{ heightMm: 400 }, { heightMm: 400 }] },
      { side: 'R', doors: [{ heightMm: 949.5 }, { heightMm: 949.5 }] },
    ],
  }).ok, false)

  // 2. Verify Python dxf_check audit integration + packaging + worker transitions (without starting SolidWorks).
  const fakePipeRoot = join(tempRoot, 'fake-pipeline')
  const buildDir = join(fakePipeRoot, 'build-run')
  const buildCaseDir = join(buildDir, 'cad', 'assemblies', 'case1')
  const buildCadDir = join(buildCaseDir, 'structure')
  mkdirSync(join(buildDir, 'phases', 'case1-build'), { recursive: true })
  mkdirSync(buildCadDir, { recursive: true })
  writeFileSync(join(buildCadDir, '上盖壳体底板.sldprt'), 'mock-sldprt-bytes', 'utf8')
  writeFileSync(join(buildCaseDir, 'assembly-evidence.json'), '{"duplicate":true}', 'utf8')
  writeFileSync(join(buildDir, 'protection-result.json'), JSON.stringify({ pass: true }), 'utf8')
  const mockExpectedManifest = [
    { identity: 'top_plate_1', path: join(buildCadDir, '上盖壳体底板.sldprt') },
    { identity: 'top_plate_2', path: join(buildCadDir, '上盖壳体底板.sldprt') },
  ]
  writeFileSync(join(buildDir, 'phases', 'case1-build', 'native-result.json'), JSON.stringify({
    events: [{
      inspection: 'minimum_assembly',
      result: {
        success: true,
        assemblyBuilt: true,
        layout: byDoorWidth.layoutSummary,
        measuredEnvelopeMm: { widthMm: 1000.0, heightMm: 2000.0, depthMm: 500.0 },
        interferencePass: true,
        interferenceClassification: {
          structuralCollisionCount: 0,
          unapprovedInterferenceCount: 0,
          approvedCirclipHingePinCount: 18,
        },
        belowFloorNonFootPass: true,
        materialCheck: { uniquePartCount: 1, missingMaterialCount: 0 },
        expectedManifest: mockExpectedManifest,
        events: [{
          inspection: 'assembly_native_feature_health',
          phase: 'full_after_top_plate',
          path: join(buildCadDir, '上盖壳体底板.sldprt'),
          features: [{ type: 'MaterialFolder', name: '冷轧钢板' }],
        }],
        parts: [{
          role: 'top_plate',
          path: join(buildCadDir, '上盖壳体底板.sldprt'),
          after: { bodies: [{ boxMm: [0, 0, 0, 858, 41.8, 429.5] }] },
        }],
        inputFilesUnchanged: true,
        fullScopeSummary: { expectedComponentCount: 155 },
      },
    }],
  }), 'utf8')

  const dxfDir = join(fakePipeRoot, 'dxf-run')
  const dxfOutDir = join(dxfDir, 'dxf-outputs', 'case1', 'structure')
  mkdirSync(dxfOutDir, { recursive: true })
  const validDxfPath = join(dxfOutDir, '上盖壳体底板.flat.dxf')
  const extraUninstalledDxfPath = join(dxfOutDir, 'door_panel_left_W437_H471p25.flat.dxf')
  writeFileSync(validDxfPath, makeMinimalValidFlatDxf(100, 50, false), 'utf8')
  writeFileSync(extraUninstalledDxfPath, makeMinimalValidFlatDxf(473.4, 507.65, false), 'utf8')
  const validDxfSha = createHash('sha256').update(readFileSync(validDxfPath)).digest('hex').toUpperCase()
  const extraDxfSha = createHash('sha256').update(readFileSync(extraUninstalledDxfPath)).digest('hex').toUpperCase()
  writeFileSync(join(dxfDir, 'protection-result.json'), JSON.stringify({ pass: true }), 'utf8')
  writeFileSync(join(dxfDir, 'native-result.json'), JSON.stringify({
    events: [{
      inspection: 'assembly_flat_dxfs',
      result: {
        inputHashesUnchanged: true,
        sheetMetalCount: 2,
        nonSheetMetalCount: 0,
        dxfOutputDir: join(dxfDir, 'dxf-outputs', 'case1'),
        parts: [
          {
            relPath: 'structure\\上盖壳体底板.sldprt',
            classification: 'sheet_metal',
            flatPath: validDxfPath,
            sha256: validDxfSha,
            markEntityCount: 2,
            removedFastenerBoresCount: 1,
            formTools: ['拱桥成型模具1'],
          },
          {
            relPath: 'structure\\door_panel_left_W437_H471p25.SLDPRT',
            classification: 'sheet_metal',
            flatPath: extraUninstalledDxfPath,
            sha256: extraDxfSha,
            markEntityCount: 0,
            removedFastenerBoresCount: 2,
            formTools: [],
          },
        ],
      },
    }],
  }), 'utf8')

  const badDxfDir = join(fakePipeRoot, 'bad-dxf')
  mkdirSync(badDxfDir, { recursive: true })
  writeFileSync(join(badDxfDir, 'duplicate.flat.dxf'), makeMinimalValidFlatDxf(100, 50, true), 'utf8')
  const badAudit = auditFlatDxfDirectory(badDxfDir, [])
  assert.equal(badAudit.pass, false, 'duplicate concentric circles should fail dxf_check')

  const drawingRunDir = join(fakePipeRoot, 'drawing-run')
  const drawingOutDir = join(drawingRunDir, 'drawing-outputs', 'case1')
  mkdirSync(drawingOutDir, { recursive: true })
  writeFileSync(join(drawingOutDir, '00_总装图_16029_variable_full_cabinet.SLDDRW'), 'mock-asm-slddrw', 'utf8')
  writeFileSync(join(drawingOutDir, '00_总装图_16029_variable_full_cabinet.pdf'), '%PDF-1.4\n%mock\n%%EOF\n', 'utf8')
  writeFileSync(join(drawingOutDir, '01_左门板_L_mock.SLDDRW'), 'mock-door-slddrw', 'utf8')
  writeFileSync(join(drawingOutDir, '01_左门板_L_mock.pdf'), '%PDF-1.4\n%mock\n%%EOF\n', 'utf8')
  writeFileSync(join(drawingRunDir, 'protection-result.json'), JSON.stringify({ pass: true }), 'utf8')
  writeFileSync(join(drawingRunDir, 'native-result.json'), JSON.stringify({
    events: [{
      inspection: 'assembly_engineering_drawings',
      result: {
        inputHashesUnchanged: true,
        drawingOutputDir: drawingOutDir,
        drawingCount: 2,
        slddrwFileCount: 2,
        pdfFileCount: 2,
        drawings: [
          { kind: 'cabinet_assembly', fileStem: '00_总装图_16029_variable_full_cabinet' },
          { kind: 'door_panel', fileStem: '01_左门板_L_mock' },
        ],
      },
    }],
  }), 'utf8')

  const tasksDir = join(tempRoot, 'tasks-unit')
  const store = createVariableTrialTaskStore({ dataDir: tasksDir })
  const created = store.create({ username: 'alice', ...byDoorWidth })
  const workerRes = await runNextVariableTrialPortalTask({
    dataDir: tasksDir,
    cadAvailable: () => true,
    runPackage: (input, opts) => runVariableTrialPackage(input, {
      ...opts,
      hooks: {
        runBuild: async () => ({ pass: true, dir: buildDir }),
        runDxf: async () => ({ pass: true, dir: dxfDir }),
        runDrawings: async () => ({ pass: true, dir: drawingRunDir }),
        auditDxf: auditFlatDxfDirectory,
        packageZip: packageVariableTrialDeliverable,
      },
    }),
  })
  assert.equal(workerRes.state, 'ready', workerRes.error)
  assert.equal(workerRes.publication.cadFileCount, 1, 'duplicate assembly-evidence.json must be excluded from 3D_CAD')
  assert.equal(workerRes.publication.dxfFileCount, 1, 'uninstalled seed DXF must be filtered out')
  assert.equal(workerRes.publication.drawingSlddrwCount, 2)
  assert.equal(workerRes.publication.drawingPdfCount, 2)
  assert.equal(workerRes.publication.dxfAuditPassCount, 1)
  assert.equal(workerRes.publication.dxfAuditTotalCount, 1)
  assert.equal(workerRes.publication.totalMarkEntities, 1)
  assert.equal(workerRes.publication.removedFastenerBores, 1)
  assert.equal(workerRes.publication.bomUniquePartCount, 1)
  assert.equal(workerRes.publication.bomTotalInstanceCount, 2)

  // Verify ZIP entries contain BOM + CAD guide + 2D drawings and omit duplicate JSON and uninstalled DXF
  const zipExtractDir = join(tempRoot, 'zip-check')
  const unzipRes = spawnSync('powershell.exe', [
    '-NoProfile',
    '-Command',
    'Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory($env:VT_ZIP_TEST, $env:VT_OUT_TEST, [System.Text.Encoding]::UTF8)',
  ], {
    env: { ...process.env, VT_ZIP_TEST: workerRes.publication.zipPath, VT_OUT_TEST: zipExtractDir },
    windowsHide: true,
    encoding: 'utf8',
  })
  assert.equal(unzipRes.status, 0, unzipRes.stderr)
  assert.ok(readFileSync(join(zipExtractDir, 'BOM_零件与下料清单.csv'), 'utf8').includes('100 × 50'))
  assert.ok(readFileSync(join(zipExtractDir, 'BOM_零件与下料清单.md'), 'utf8').includes('上盖壳体底板'))
  assert.ok(readFileSync(join(zipExtractDir, '3D_CAD', '请打开此总装_说明.txt'), 'utf8').includes('16029_variable_full_cabinet.SLDASM'))
  const { existsSync } = await import('node:fs')
  assert.equal(existsSync(join(zipExtractDir, '3D_CAD', 'assembly-evidence.json')), false)
  assert.equal(existsSync(join(zipExtractDir, 'DXF_展开图', 'structure', 'door_panel_left_W437_H471p25.flat.dxf')), false)
  assert.equal(existsSync(join(zipExtractDir, 'DXF_展开图', 'structure', '上盖壳体底板.flat.dxf')), true)
  assert.equal(existsSync(join(zipExtractDir, '2D_工程图', '00_总装图_16029_variable_full_cabinet.SLDDRW')), true)
  assert.equal(existsSync(join(zipExtractDir, '2D_工程图', '00_总装图_16029_variable_full_cabinet.pdf')), true)
  assert.equal(existsSync(join(zipExtractDir, '2D_工程图', '01_左门板_L_mock.SLDDRW')), true)
  assert.equal(existsSync(join(zipExtractDir, '2D_工程图', '01_左门板_L_mock.pdf')), true)

  const queuedCancel = store.create({ username: 'alice', ...exactHeightMm })
  assert.equal(store.requestCancel(queuedCancel.id).task.status, 'cancelled')
  assert.equal(store.retryTask(queuedCancel.id).task.status, 'queued')

  // Verify waiting_for_cad & live in-flight AbortSignal cancellation propagation
  const busyRes = await runNextVariableTrialPortalTask({
    dataDir: tasksDir,
    cadAvailable: () => false,
    runPackage: async () => assert.fail('must not start runPackage while CAD is busy'),
  })
  assert.equal(busyRes.state, 'waiting_for_cad')

  const cancelRunningRes = await runNextVariableTrialPortalTask({
    dataDir: tasksDir,
    cadAvailable: () => true,
    runPackage: async (_input, { signal }) => {
      const cancelReq = store.requestCancel(queuedCancel.id)
      assert.equal(cancelReq.task.status, 'cancel_requested')
      await new Promise((resolveAbort) => {
        if (signal.aborted) resolveAbort()
        else signal.addEventListener('abort', resolveAbort, { once: true })
      })
      throw new Error('TRIAL_INTERRUPTED')
    },
  })
  assert.equal(cancelRunningRes.state, 'cancelled')
  assert.equal(store.read(queuedCancel.id).status, 'cancelled')

  // 3. Spin up real 5180 server in isolated data dir and test full UI + API + ZIP download via Playwright Edge.
  const portalDataDir = join(tempRoot, 'portal-data')
  const portalStorageDir = join(tempRoot, 'portal-storage')
  mkdirSync(portalDataDir, { recursive: true })
  mkdirSync(join(portalStorageDir, '模型下载'), { recursive: true })
  const staticAsset = 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'
  writeFileSync(join(portalDataDir, 'review_download_invite_code.txt'), 'test-invite-vt', 'utf8')
  const port = await freePort()
  const baseUrl = `http://127.0.0.1:${port}`
  const { copyFileSync } = await import('node:fs')
  copyFileSync(resolve(ROOT, staticAsset), join(portalStorageDir, '模型下载', '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'))

  let serverLogs = ''
  server = spawn(process.execPath, ['tools/serve_16029_review_downloads.mjs'], {
    cwd: ROOT,
    windowsHide: true,
    env: {
      ...process.env,
      STUDIO_REVIEW_HOST: '127.0.0.1',
      STUDIO_REVIEW_PORT: String(port),
      STUDIO_REVIEW_DATA_DIR: portalDataDir,
      STUDIO_REVIEW_STORAGE_ROOT: portalStorageDir,
      STUDIO_REVIEW_SKIP_ASSET_SYNC: '1',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  server.stderr.on('data', (chunk) => { serverLogs += chunk.toString() })

  let readyUp = false
  for (let i = 0; i < 60; i += 1) {
    assert.equal(server.exitCode, null, serverLogs)
    try {
      const r = await fetch(`${baseUrl}/status.json`, { signal: AbortSignal.timeout(500) })
      if (r.ok) { readyUp = true; break }
    } catch {}
    await delay(150)
  }
  assert.equal(readyUp, true, serverLogs)

  const portalTasksDir = join(portalDataDir, 'variable_trial_portal_tasks')
  browser = await chromium.launch({
    executablePath: process.env.STUDIO_BROWSER_EXECUTABLE || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    headless: true,
  })
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true })
  page.setDefaultTimeout(15000)
  const pageErrors = []
  page.on('pageerror', (err) => pageErrors.push(err.message))

  const username = 'vt-' + randomBytes(4).toString('hex')
  await page.goto(`${baseUrl}/register`)
  await page.locator('[name=username]').fill(username)
  await page.locator('[name=password]').fill('StrongPass123!')
  await page.locator('[name=inviteCode]').fill('test-invite-vt')
  await page.locator('button[type=submit]').click()
  await page.locator('#generationForm').waitFor()

  // Navigate to /indoor-v2 via sidebar link
  await page.locator('a[href="/indoor-v2"]').click()
  await page.locator('#vtForm').waitFor()

  // Test preset selection + 5 preview views + submit confirmation + live queue task creation
  await page.locator('#presetBar button').nth(2).click()
  assert.equal(await page.locator('#vtWidthInput').inputValue(), '850')
  assert.equal(await page.locator('#vtHeightInput').inputValue(), '1850')
  const viewData = new Set()
  for (const view of ['front', 'right', 'top', 'iso', 'craft']) {
    await page.locator(`[data-vt-view="${view}"]`).click()
    viewData.add(await page.locator('#vtPreview').evaluate((c) => c.toDataURL()))
  }
  assert.equal(viewData.size, 5, 'all 5 canvas preview projections must render distinctly')

  await page.locator('#vtRef').fill('浏览器全流程验证 850x1850')
  await page.locator('#prepareSubmitBtn').click()
  await page.locator('#confirmBox').waitFor({ state: 'visible' })
  const postResp = page.waitForResponse((res) => res.url().endsWith('/indoor-v2/task') && res.request().method() === 'POST')
  await page.locator('#confirmSubmitBtn').click()
  const createdResp = await postResp
  assert.equal(createdResp.status(), 201)
  const createdPayload = await createdResp.json()

  // Run worker on the real created task using mocked build/dxf outputs matching 850x1850x485 + real dxf_check + real ZIP packaging
  const browserBuildDir = join(fakePipeRoot, 'build-browser-850')
  mkdirSync(join(browserBuildDir, 'phases', 'case1-build'), { recursive: true })
  mkdirSync(join(browserBuildDir, 'cad', 'assemblies', 'case1', 'structure'), { recursive: true })
  writeFileSync(join(browserBuildDir, 'cad', 'assemblies', 'case1', 'structure', '上盖壳体底板.sldprt'), 'mock-850', 'utf8')
  writeFileSync(join(browserBuildDir, 'protection-result.json'), JSON.stringify({ pass: true }), 'utf8')
  writeFileSync(join(browserBuildDir, 'phases', 'case1-build', 'native-result.json'), JSON.stringify({
    events: [{
      inspection: 'minimum_assembly',
      result: {
        success: true,
        assemblyBuilt: true,
        layout: createdPayload.layoutSummary,
        measuredEnvelopeMm: { widthMm: 850.0, heightMm: 1850.0, depthMm: 485.0 },
        interferencePass: true,
        interferenceClassification: { structuralCollisionCount: 0, unapprovedInterferenceCount: 0 },
        belowFloorNonFootPass: true,
        materialCheck: { uniquePartCount: 42, missingMaterialCount: 0 },
        inputFilesUnchanged: true,
      },
    }],
  }), 'utf8')

  const browserWorker = await runNextVariableTrialPortalTask({
    dataDir: portalTasksDir,
    cadAvailable: () => true,
    runPackage: (input, opts) => runVariableTrialPackage(input, {
      ...opts,
      hooks: {
        runBuild: async () => ({ pass: true, dir: browserBuildDir }),
        runDxf: async () => ({ pass: true, dir: dxfDir }),
        runDrawings: async () => ({ pass: true, dir: drawingRunDir }),
        auditDxf: auditFlatDxfDirectory,
        packageZip: packageVariableTrialDeliverable,
      },
    }),
  })
  assert.equal(browserWorker.state, 'ready', browserWorker.error)

  await page.reload()
  await page.locator(`[data-task-id="${createdPayload.taskId}"][data-task-status="ready"]`).waitFor()
  const downloadPromise = page.waitForEvent('download')
  await page.locator(`[data-task-id="${createdPayload.taskId}"] a`).click()
  const download = await downloadPromise
  assert.match(download.suggestedFilename(), /^16029_全参数_850W_1850H_485D_L5-R2_3D与DXF_/)
  const dlPath = await download.path()
  assert.ok(dlPath && readFileSync(dlPath).length > 500)

  // Check responsive layout across desktop, tablet, and mobile viewports on both / and /indoor-v2
  for (const vp of [{ width: 1440, height: 1000 }, { width: 1024, height: 900 }, { width: 820, height: 1000 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(vp)
    const vtOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1)
    assert.equal(vtOverflow, false, `/indoor-v2 must not overflow at ${vp.width}px`)
  }

  // Test cancel / retry / delete / report modal interactions on /indoor-v2
  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.locator(`[data-task-id="${createdPayload.taskId}"] button[data-act="report"]`).click()
  await page.locator('#reportModal[open]').waitFor()
  const modalTxt = await page.locator('#reportModalBody').innerText()
  assert.match(modalTxt, /dxfAuditPassCount/)
  assert.match(modalTxt, /drawingSummary/)
  await page.locator('#closeReportModal').click()

  // Submit a second queued task (forceNew) and exercise UI cancel -> retry -> delete
  const postSecond = await page.request.post(`${baseUrl}/indoor-v2/task`, {
    data: { ...PORTAL_PRESETS[0], customerRequirementReference: '取消重试删除测试', forceNew: true },
  })
  assert.equal(postSecond.status(), 201)
  const secondTaskId = (await postSecond.json()).taskId
  await page.reload()
  await page.locator(`[data-task-id="${secondTaskId}"][data-task-status="queued"] button[data-act="cancel"]`).click()
  await page.locator(`[data-task-id="${secondTaskId}"][data-task-status="cancelled"] button[data-act="retry"]`).waitFor()
  await page.locator(`[data-task-id="${secondTaskId}"] button[data-act="retry"]`).click()
  await page.locator(`[data-task-id="${secondTaskId}"][data-task-status="queued"]`).waitFor()
  await page.locator(`[data-task-id="${secondTaskId}"] button[data-act="cancel"]`).click()
  await page.locator(`[data-task-id="${secondTaskId}"][data-task-status="cancelled"] button[data-act="delete"]`).waitFor()
  await page.locator(`[data-task-id="${secondTaskId}"] button[data-act="delete"]`).click()
  await page.waitForFunction((id) => !document.querySelector(`[data-task-id="${id}"]`), secondTaskId)

  // Verify cross-user isolation
  const otherStore = createVariableTrialTaskStore({ dataDir: portalTasksDir })
  const foreignTask = otherStore.create({ username: 'bob', ...byDoorWidth })
  const foreignCheck = await page.request.get(`${baseUrl}/indoor-v2/task/${foreignTask.id}/download`)
  assert.equal(foreignCheck.status(), 404)

  await page.goto(`${baseUrl}/`)
  await page.locator('#generationForm').waitFor()
  for (const vp of [{ width: 1440, height: 1000 }, { width: 1024, height: 900 }, { width: 820, height: 1000 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(vp)
    const rootOverflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1)
    assert.equal(rootOverflow, false, `/ must not overflow at ${vp.width}px`)
  }
  assert.deepEqual(pageErrors, [])

  console.log(JSON.stringify({
    pass: true,
    presetsVerified: PORTAL_PRESETS.length,
    workerTaskId: createdPayload.taskId,
    downloadedZipName: download.suggestedFilename(),
    cadExecuted: false,
  }))
} finally {
  if (browser) await browser.close()
  if (server && server.exitCode === null) server.kill()
  rmSync(tempRoot, { recursive: true, force: true })
}
