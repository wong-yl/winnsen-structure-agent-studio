import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createServer as createNetServer } from 'node:net'
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { randomBytes } from 'node:crypto'

const ROOT = resolve(process.cwd())
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const TEMP_ROOT = resolve(ROOT, 'tmp/verify_16029_review_portal_feedback_upload')
const DATA_DIR = resolve(TEMP_ROOT, 'data')
const STORAGE_ROOT = resolve(TEMP_ROOT, 'storage')
const CURRENT_ASSET_SOURCE = resolve(
  ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip',
)
const V36_ASSET_SOURCE = resolve(
  ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v36-four-door-l66-r66-r2/16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip',
)
const V35_ASSET_SOURCE = resolve(
  ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip',
)
const PNG_BASE64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII='

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds))
}

async function freePort() {
  const probe = createNetServer()
  await new Promise((resolveListen, reject) => {
    probe.once('error', reject)
    probe.listen(0, '127.0.0.1', resolveListen)
  })
  const address = probe.address()
  const port = typeof address === 'object' && address ? address.port : 0
  await new Promise((resolveClose) => probe.close(resolveClose))
  return port
}

async function waitForServer(baseUrl, child, stderr) {
  for (let attempt = 0; attempt < 80; attempt += 1) {
    if (child.exitCode !== null) throw new Error(`portal exited during startup: ${stderr()}`)
    try {
      const response = await fetch(`${baseUrl}/status.json`)
      if (response.ok) return response.json()
    } catch {
      // The test server is still starting.
    }
    await delay(100)
  }
  throw new Error(`portal did not start: ${stderr()}`)
}

async function stopChild(child) {
  if (!child || child.exitCode !== null) return
  child.kill()
  await Promise.race([
    new Promise((resolveExit) => child.once('exit', resolveExit)),
    delay(3000),
  ])
}

function requestByReference(requests, customerRequirementReference) {
  const matches = requests.filter((item) => item.customerRequirementReference === customerRequirementReference)
  assert.equal(matches.length, 1, `expected one generation request for ${customerRequirementReference}`)
  return matches[0]
}

function assertNoLegacyGenerationTokens(value, label) {
  const serialized = JSON.stringify(value).toLowerCase()
  for (const token of [
    'production',
    'review_generation',
    'freecad',
    'manual_cad_worker',
    'verified_asset_cache',
    'controlledgenerationpolicy',
    'controlled_generation',
    'process_16029_review_generation_queue',
  ]) {
    assert.equal(serialized.includes(token), false, `${label} must not contain legacy token: ${token}`)
  }
}

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(DATA_DIR, { recursive: true })
writeFileSync(resolve(DATA_DIR, 'review_download_invite_code.txt'), 'test-invite-code', 'utf8')
const currentAssetName = '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'
const v36AssetName = '16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip'
const v35AssetName = '16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip'
mkdirSync(resolve(STORAGE_ROOT, '模型下载'), { recursive: true })
assert.equal(existsSync(CURRENT_ASSET_SOURCE), true, 'current V37 source asset is required for integrity testing')
assert.equal(existsSync(V36_ASSET_SOURCE), true, 'historical V36 source asset is required for recipe testing')
assert.equal(existsSync(V35_ASSET_SOURCE), true, 'historical V35 source asset is required for baseline testing')
const currentAssetPath = resolve(STORAGE_ROOT, '模型下载', currentAssetName)
copyFileSync(CURRENT_ASSET_SOURCE, currentAssetPath)
copyFileSync(V36_ASSET_SOURCE, resolve(STORAGE_ROOT, '模型下载', v36AssetName))
copyFileSync(V35_ASSET_SOURCE, resolve(STORAGE_ROOT, '模型下载', v35AssetName))
const currentAssetBytes = readFileSync(CURRENT_ASSET_SOURCE)

const port = await freePort()
const baseUrl = `http://127.0.0.1:${port}`
const testPassword = randomBytes(24).toString('base64url')
let stderrText = ''
const child = spawn(process.execPath, [PORTAL_PATH], {
  cwd: ROOT,
  env: {
    ...process.env,
    STUDIO_REVIEW_HOST: '127.0.0.1',
    STUDIO_REVIEW_PORT: String(port),
    STUDIO_REVIEW_DATA_DIR: DATA_DIR,
      STUDIO_REVIEW_STORAGE_ROOT: STORAGE_ROOT,
      STUDIO_REVIEW_SKIP_ASSET_SYNC: '1',
  },
  stdio: ['ignore', 'pipe', 'pipe'],
  windowsHide: true,
})
child.stderr.on('data', (chunk) => { stderrText += chunk.toString('utf8') })

try {
  const status = await waitForServer(baseUrl, child, () => stderrText)
  assert.equal(status.status, 'ok')
  assert.equal(status.auth, 'enabled')
  assert.equal(status.reviewRound, '16029-v43-v37-760w-six-door-engineering-assistance-20260811')
  assert.equal(status.sharedStorageReady, true)
  assert.equal(JSON.stringify(status).includes('sourcePath'), false)
  assert.equal(JSON.stringify(status).includes(STORAGE_ROOT), false)

  const emptyAuditResponse = await fetch(`${baseUrl}/local-audit/feedback-summary.json`)
  assert.equal(emptyAuditResponse.status, 200)
  const emptyAudit = await emptyAuditResponse.json()
  assert.equal(emptyAudit.currentReviewRoundId, '16029-v43-v37-760w-six-door-engineering-assistance-20260811')
  assert.equal(emptyAudit.totalSubmissionCount, 0)
  assert.equal(emptyAudit.currentRoundSubmissionCount, 0)
  assert.deepEqual(emptyAudit.indexRowsWithoutFolder, [])
  assert.deepEqual(emptyAudit.foldersWithoutIndexRow, [])

  const unauthenticated = await fetch(`${baseUrl}/feedback.json`, { redirect: 'manual' })
  assert.equal(unauthenticated.status, 302)
  assert.equal(unauthenticated.headers.get('location'), '/login')

  const registration = await fetch(`${baseUrl}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      username: 'portal-feedback-test',
      password: testPassword,
      inviteCode: 'test-invite-code',
    }),
    redirect: 'manual',
  })
  assert.equal(registration.status, 302)
  const cookie = String(registration.headers.get('set-cookie') || '').split(';', 1)[0]
  assert.match(cookie, /^review_session=/)

  const generationSubmission = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'v1',
      customerRequirementReference: '脱敏V37客户任务A',
      widthInputMode: 'cabinet_outer_width',
      requestedWidthMm: 760,
      cabinetDepth: 550,
      doorCount: 6,
    }),
  })
  assert.equal(generationSubmission.status, 200)
  const generationResult = await generationSubmission.json()
  assert.equal(generationResult.deliveryMode, 'verified_native_model')
  assert.equal(generationResult.engineeringAssistanceReady, true)
  assert.equal(generationResult.engineeringContinuationEligible, true)
  assert.equal(generationResult.engineeringUseEligible, true)
  assert.equal(Object.hasOwn(generationResult, 'productionReleaseEligible'), false)
  assert.equal(generationResult.sourceBaselineAssetId, '16029-v43-v37-760w-six-door-engineering-assistance-zip')
  assert.equal(generationResult.downloadUrl, '/download/16029-v43-v37-760w-six-door-engineering-assistance-zip')
  const generationRequestId = generationResult.requestId

  const v36GenerationSubmission = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'v1',
      customerRequirementReference: '脱敏四门历史配方任务',
      widthInputMode: 'cabinet_outer_width',
      requestedWidthMm: 740,
      cabinetDepth: 550,
      doorCount: 4,
    }),
  })
  assert.equal(v36GenerationSubmission.status, 200)
  const v36GenerationResult = await v36GenerationSubmission.json()
  assert.equal(v36GenerationResult.deliveryMode, 'verified_native_model')
  assert.equal(v36GenerationResult.engineeringAssistanceReady, true)
  assert.equal(v36GenerationResult.engineeringUseEligible, true)
  assert.equal(Object.hasOwn(v36GenerationResult, 'productionReleaseEligible'), false)
  assert.equal(v36GenerationResult.sourceBaselineAssetId, '16029-v43-v36-four-door-engineering-assistance-zip')
  assert.equal(v36GenerationResult.downloadUrl, '/download/16029-v43-v36-four-door-engineering-assistance-zip')

  const v35GenerationSubmission = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'v1',
      customerRequirementReference: '脱敏六门基线任务',
      widthInputMode: 'installed_door_panel_width',
      requestedWidthMm: 307,
      cabinetDepth: 550,
      doorCount: 6,
    }),
  })
  assert.equal(v35GenerationSubmission.status, 200)
  const v35GenerationResult = await v35GenerationSubmission.json()
  assert.equal(v35GenerationResult.deliveryMode, 'verified_native_model')
  assert.equal(v35GenerationResult.engineeringAssistanceReady, true)
  assert.equal(v35GenerationResult.engineeringUseEligible, true)
  assert.equal(Object.hasOwn(v35GenerationResult, 'productionReleaseEligible'), false)
  assert.equal(v35GenerationResult.sourceBaselineAssetId, '16029-v43-v35-one-door-one-lock-hole-rereview-zip')
  assert.equal(v35GenerationResult.downloadUrl, '/download/16029-v43-v35-one-door-one-lock-hole-rereview-zip')

  const pendingNativeReference = '脱敏888宽14门原生新任务'
  const pendingNativeSubmission = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'native_v1',
      customerRequirementReference: pendingNativeReference,
      widthInputMode: 'cabinet_outer_width',
      requestedWidthMm: 888,
      cabinetDepth: 550,
      doorCount: 14,
    }),
  })
  assert.equal(pendingNativeSubmission.status, 201)
  const pendingNativeResult = await pendingNativeSubmission.json()
  assert.equal(pendingNativeResult.status, 'native_task_created')
  assert.equal(pendingNativeResult.taskType, 'native_solidworks_build_task')
  assert.equal(pendingNativeResult.deliveryMode, 'native_task_pending')
  assert.equal(pendingNativeResult.modelReady, false)
  assert.equal(pendingNativeResult.engineeringAssistanceReady, false)
  assert.equal(pendingNativeResult.engineeringContinuationEligible, false)
  assert.equal(pendingNativeResult.engineeringUseEligible, false)
  assert.equal(pendingNativeResult.downloadUrl, '')
  assert.equal(pendingNativeResult.legacyFallbackUsed, false)
  assert.equal(pendingNativeResult.nativeBuild.state, 'created')
  assertNoLegacyGenerationTokens(pendingNativeResult, '888x14 native task response')

  const invalidGeneration = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'native_v1',
      customerRequirementReference: '脱敏无效宽度任务',
      widthInputMode: 'cabinet_outer_width',
      requestedWidthMm: 0,
      cabinetDepth: 550,
      doorCount: 14,
    }),
  })
  assert.equal(invalidGeneration.status, 422)

  const electricalHardwareGeneration = await fetch(`${baseUrl}/generation-request`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerParameterFlow: 'native_v1',
      customerRequirementReference: '脱敏电控硬件任务',
      widthInputMode: 'cabinet_outer_width',
      requestedWidthMm: 888,
      cabinetDepth: 550,
      doorCount: 14,
      prompt: '增加电控锁',
    }),
  })
  assert.equal(electricalHardwareGeneration.status, 422)

  const generationListResponse = await fetch(`${baseUrl}/generation-requests.json`, { headers: { Cookie: cookie } })
  assert.equal(generationListResponse.status, 200)
  const generationList = await generationListResponse.json()
  const v35Request = requestByReference(generationList.requests, '脱敏六门基线任务')
  assert.equal(v35Request.sourceBaselineAssetId, '16029-v43-v35-one-door-one-lock-hole-rereview-zip')
  assert.equal(v35Request.engineeringUseEligible, true)
  const v36Request = requestByReference(generationList.requests, '脱敏四门历史配方任务')
  assert.equal(v36Request.sourceBaselineAssetId, '16029-v43-v36-four-door-engineering-assistance-zip')
  assert.equal(v36Request.engineeringUseEligible, true)
  const v37Request = requestByReference(generationList.requests, '脱敏V37客户任务A')
  assert.equal(v37Request.engineeringContinuationEligible, true)
  assert.equal(v37Request.engineeringUseEligible, true)
  assert.equal(v37Request.doorCount, '6')
  assert.equal(v37Request.doorWidth, '317')
  const pendingNativeRequest = requestByReference(generationList.requests, pendingNativeReference)
  assert.equal(pendingNativeRequest.status, 'native_task_created')
  assert.equal(pendingNativeRequest.taskType, 'native_solidworks_build_task')
  assert.equal(pendingNativeRequest.deliveryMode, 'native_task_pending')
  assert.equal(pendingNativeRequest.modelReady, false)
  assert.equal(pendingNativeRequest.engineeringAssistanceReady, false)
  assert.equal(pendingNativeRequest.engineeringContinuationEligible, false)
  assert.equal(pendingNativeRequest.engineeringUseEligible, false)
  assert.equal(pendingNativeRequest.downloadUrl, '')
  assert.equal(pendingNativeRequest.legacyFallbackUsed, false)
  assert.equal(pendingNativeRequest.nativeBuild.state, 'created')
  assertNoLegacyGenerationTokens(pendingNativeRequest, 'stored 888x14 native task')

  const currentDownload = await fetch(
    `${baseUrl}/download/16029-v43-v37-760w-six-door-engineering-assistance-zip`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(currentDownload.status, 200)
  assert.match(String(currentDownload.headers.get('content-disposition') || ''), /filename\*=UTF-8''/)
  assert.deepEqual(Buffer.from(await currentDownload.arrayBuffer()), currentAssetBytes)

  const v36BaselineDownload = await fetch(
    `${baseUrl}/download/16029-v43-v36-four-door-engineering-assistance-zip`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(v36BaselineDownload.status, 200)
  assert.deepEqual(Buffer.from(await v36BaselineDownload.arrayBuffer()), readFileSync(V36_ASSET_SOURCE))

  const v35BaselineDownload = await fetch(
    `${baseUrl}/download/16029-v43-v35-one-door-one-lock-hole-rereview-zip`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(v35BaselineDownload.status, 200)
  assert.deepEqual(Buffer.from(await v35BaselineDownload.arrayBuffer()), readFileSync(V35_ASSET_SOURCE))

  writeFileSync(currentAssetPath, Buffer.concat([currentAssetBytes, Buffer.from('tampered')]))
  const tamperedDownload = await fetch(
    `${baseUrl}/download/16029-v43-v37-760w-six-door-engineering-assistance-zip`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(tamperedDownload.status, 409)
  assert.equal((await tamperedDownload.json()).error, 'asset integrity verification failed')
  copyFileSync(CURRENT_ASSET_SOURCE, currentAssetPath)

  const resolvedClarificationImage = await fetch(
    `${baseUrl}/clarification/V34-C1-UNUSED-STANDARD-LOCK-ROWS/image/1`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(resolvedClarificationImage.status, 404)

  const assetsResponse = await fetch(`${baseUrl}/assets.json`, { headers: { Cookie: cookie } })
  assert.equal(assetsResponse.status, 200)
  const assetCatalog = await assetsResponse.json()
  assert.equal(assetCatalog.assets.length, 21)
  assert.deepEqual(
    assetCatalog.assets.slice(0, 4).map((asset) => asset.id),
    [
      '16029-v43-v37-760w-six-door-engineering-assistance-zip',
      '16029-v43-v36-four-door-engineering-assistance-zip',
      '16029-v43-v35-one-door-one-lock-hole-rereview-zip',
      '16029-v43-v34-lock-hole-center-rereview-zip',
    ],
  )
  assert.equal(assetCatalog.assets[0].category, '当前已验证工程辅助模型｜V37')
  assert.equal(assetCatalog.assets[0].sha256, '6B9F62E4F697B96909131D81EAD0FA341E059530204DB76480148460883CD1C0')
  assert.equal(assetCatalog.assets[0].expectedSizeBytes, 24535798)
  assert.equal(assetCatalog.assets[0].engineeringUseEligible, true)
  assert.equal(Object.hasOwn(assetCatalog.assets[0], 'productionReleaseEligible'), false)
  assert.equal(Object.hasOwn(assetCatalog.assets[0], 'releaseReady'), false)
  assert.equal(assetCatalog.assets[0].openRebuildRequired, true)
  assert.equal(assetCatalog.assets[0].warningFree, false)
  assert.equal(assetCatalog.assets[0].fullAssemblyValidationAllGreen, false)
  assert.deepEqual(assetCatalog.assets[0].knownIssue, {
    component: '箱体右侧板焊接-1',
    type: 'Reference',
    code: 51,
    warning: true,
  })
  assert.equal(assetCatalog.assets[1].category, '历史已验证四门基线｜V36')
  assert.equal(assetCatalog.assets[2].category, '历史已验证六门基线｜V35')

  const payload = {
    reviewerName: '结构测试工程师',
    discipline: '结构审核',
    reviewTarget: '16029-v43-v37-760w-six-door-engineering-assistance-zip',
    generationRequestId,
    issueCategory: 'other_structure_issue',
    severity: 'P1',
    decision: 'needs_changes',
    componentName: '箱体竖隔板L/R',
    modelLocation: '左列下门锁孔附近',
    currentProblem: '测试提交：复核一门一锁孔工位。',
    expectedResult: '保持一扇门对应一个原生异形槽和一个配套圆孔。',
    keyDimensionTolerance: '待结构工程师测量确认',
    referenceBasis: 'V37 760宽六门 L642-R246 工程辅助模型',
    acceptanceCriteria: '六扇门各对应一个原生异形槽和一个配套圆孔，不出现多余同类孔行。',
    attachments: [{ name: 'Q01.png', type: 'image/png', dataUrl: `data:image/png;base64,${PNG_BASE64}` }],
  }
  const submission = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  assert.equal(submission.status, 200)
  const submissionResult = await submission.json()
  assert.match(submissionResult.feedbackId, /^V37-Q-[A-Z0-9-]+$/)
  assert.equal(submissionResult.savedFiles, 1)

  const mineResponse = await fetch(`${baseUrl}/feedback.json`, { headers: { Cookie: cookie } })
  assert.equal(mineResponse.status, 200)
  const mine = await mineResponse.json()
  assert.equal(mine.feedback.length, 1)
  assert.equal(mine.feedback[0].id, submissionResult.feedbackId)
  assert.equal(mine.feedback[0].recordType, 'structure_issue')
  assert.equal(mine.feedback[0].schema, 'winnsen.review.structure_issue.v1')
  assert.equal(mine.feedback[0].severity, 'P1')
  assert.equal(mine.feedback[0].issueCategory, 'other_structure_issue')
  assert.equal(mine.feedback[0].clarificationId, '')
  assert.equal(mine.feedback[0].clarificationOption, '')
  assert.equal(mine.feedback[0].feedbackIsEngineeringSignoff, false)
  assert.equal(Object.hasOwn(mine.feedback[0], 'productionReleaseEligible'), false)
  assert.equal(mine.feedback[0].reviewTargetType, 'generation_request')
  assert.equal(mine.feedback[0].generationRequestId, generationRequestId)
  assert.equal(mine.feedback[0].generationRequestSnapshot.customerRequirementReference, '脱敏V37客户任务A')
  assert.equal(mine.feedback[0].generationRequestSnapshot.doorCount, '6')
  assert.equal(mine.feedback[0].generationRequestSnapshot.doorWidth, '317')
  assert.equal(mine.feedback[0].generationRequestSnapshot.generationTemplateId, 'v37-760w-six-door-l642-r246')
  assert.equal(mine.feedback[0].generationRequestSnapshot.engineeringAssistanceReady, true)
  assert.equal(mine.feedback[0].generationRequestSnapshot.engineeringDeepeningRequired, true)
  assert.equal(Object.hasOwn(mine.feedback[0].generationRequestSnapshot, 'productionReleaseEligible'), false)

  const efficiencyPayload = {
    recordType: 'efficiency_validation',
    reviewerName: '结构测试工程师',
    discipline: '结构设计效率验证',
    reviewTarget: '16029-v43-v37-760w-six-door-engineering-assistance-zip',
    generationRequestId,
    taskKind: 'typical_change',
    taskReference: '脱敏典型变更A',
    taskScope: '使用V37 760宽六门基础模型继续深化一项真实结构任务，计时包含可重建模型与工程图。',
    traditionalEstimatedMinutes: 240,
    generatedModelActualMinutes: 80,
    directReusePercent: 75,
    manualModificationItems: ['调整门板高度', '更新隔板和工程图'],
    deliverables: {
      rebuildableModel: 'completed',
      engineeringDrawing: 'completed',
      bom: 'not_required',
    },
    primaryProblemCategory: 'parameter_adaptation',
    problemNotes: '参数适配仍需人工处理。',
    attachments: [],
  }
  const efficiencySubmission = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(efficiencyPayload),
  })
  assert.equal(efficiencySubmission.status, 200)
  const efficiencyResult = await efficiencySubmission.json()
  assert.match(efficiencyResult.feedbackId, /^V37-Q-[A-Z0-9-]+$/)
  assert.equal(efficiencyResult.savedFiles, 0)

  const combinedMineResponse = await fetch(`${baseUrl}/feedback.json`, { headers: { Cookie: cookie } })
  assert.equal(combinedMineResponse.status, 200)
  const combinedMine = await combinedMineResponse.json()
  assert.equal(combinedMine.feedback.length, 2)
  assert.equal(combinedMine.feedback[0].id, efficiencyResult.feedbackId)
  assert.equal(combinedMine.feedback[0].recordType, 'efficiency_validation')
  assert.equal(combinedMine.feedback[0].schema, 'winnsen.review.efficiency_validation.v1')
  assert.equal(combinedMine.feedback[0].attachmentCount, 0)
  assert.equal(combinedMine.feedback[0].feedbackIsEngineeringSignoff, false)
  assert.equal(Object.hasOwn(combinedMine.feedback[0], 'productionReleaseEligible'), false)
  assert.equal(combinedMine.feedback[0].efficiencyValidation.measurementComplete, true)
  assert.equal(combinedMine.feedback[0].efficiencyValidation.timeSavingsMinutes, 160)
  assert.equal(combinedMine.feedback[0].efficiencyValidation.timeSavingsPercent, 66.7)
  assert.equal(combinedMine.feedback[0].efficiencyValidation.generatedModelActualMinutes, 80)
  assert.equal(combinedMine.feedback[0].generationRequestId, generationRequestId)
  assert.equal(combinedMine.feedback[0].generationRequestSnapshot.engineeringAssistanceReady, true)
  assert.equal(combinedMine.feedback[0].generationRequestSnapshot.engineeringDeepeningRequired, true)
  assert.deepEqual(
    combinedMine.feedback[0].efficiencyValidation.manualModificationItems,
    ['调整门板高度', '更新隔板和工程图'],
  )

  const populatedAuditResponse = await fetch(`${baseUrl}/local-audit/feedback-summary.json`)
  assert.equal(populatedAuditResponse.status, 200)
  const populatedAudit = await populatedAuditResponse.json()
  assert.equal(populatedAudit.totalSubmissionCount, 2)
  assert.equal(populatedAudit.currentRoundSubmissionCount, 2)
  assert.equal(populatedAudit.feedback[0].id, efficiencyResult.feedbackId)
  assert.equal(populatedAudit.feedback[0].recordType, 'efficiency_validation')
  assert.equal(populatedAudit.feedback[0].attachmentCount, 0)
  assert.equal(populatedAudit.feedback[0].efficiencyValidation.directReusePercent, 75)
  assert.equal(populatedAudit.feedback[1].id, submissionResult.feedbackId)
  assert.equal(populatedAudit.feedback[1].recordType, 'structure_issue')
  assert.equal(populatedAudit.feedback[1].attachmentCount, 1)
  assert.equal(populatedAudit.feedback[1].clarificationId, '')
  assert.equal(populatedAudit.feedback[1].clarificationOption, '')
  assert.deepEqual(populatedAudit.indexRowsWithoutFolder, [])
  assert.deepEqual(populatedAudit.foldersWithoutIndexRow, [])

  const localAuditAttachmentResponse = await fetch(
    `${baseUrl}/local-audit/feedback/${encodeURIComponent(submissionResult.feedbackId)}/attachment/1`,
  )
  assert.equal(localAuditAttachmentResponse.status, 200)
  assert.deepEqual(
    Buffer.from(await localAuditAttachmentResponse.arrayBuffer()),
    Buffer.from(PNG_BASE64, 'base64'),
  )

  const attachmentResponse = await fetch(
    `${baseUrl}/feedback/${encodeURIComponent(submissionResult.feedbackId)}/attachment/1`,
    { headers: { Cookie: cookie } },
  )
  assert.equal(attachmentResponse.status, 200)
  assert.equal(attachmentResponse.headers.get('content-type'), 'image/png')
  assert.deepEqual(Buffer.from(await attachmentResponse.arrayBuffer()), Buffer.from(PNG_BASE64, 'base64'))

  const invalidSubmission = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...payload, attachments: [] }),
  })
  assert.equal(invalidSubmission.status, 400)
  const invalidRecordType = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...efficiencyPayload, recordType: 'unknown_type' }),
  })
  assert.equal(invalidRecordType.status, 400)
  const invalidEfficiencyTime = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...efficiencyPayload, traditionalEstimatedMinutes: 0 }),
  })
  assert.equal(invalidEfficiencyTime.status, 400)
  const invalidEfficiencyWithoutGeneration = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...efficiencyPayload, generationRequestId: '' }),
  })
  assert.equal(invalidEfficiencyWithoutGeneration.status, 400)
  const invalidEfficiencyManualItems = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...efficiencyPayload, directReusePercent: 80, manualModificationItems: [] }),
  })
  assert.equal(invalidEfficiencyManualItems.status, 400)
  const invalidEfficiencyOtherNotes = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...efficiencyPayload, primaryProblemCategory: 'other', problemNotes: '' }),
  })
  assert.equal(invalidEfficiencyOtherNotes.status, 400)
  const invalidClarification = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      ...payload,
      clarificationId: 'V34-C1-UNUSED-STANDARD-LOCK-ROWS',
      clarificationOption: 'remove_unused_rows',
    }),
  })
  assert.equal(invalidClarification.status, 400)

  const pageResponse = await fetch(`${baseUrl}/`, { headers: { Cookie: cookie } })
  assert.equal(pageResponse.status, 200)
  const page = await pageResponse.text()
  assert.match(page, /上传结构问题反馈/)
  assert.match(page, /参数化工程工作台/)
  assert.match(page, /<form\b[^>]*id="generationForm"/)
  assert.match(page, /<input\b[^>]*name="requestedWidthMm"[^>]*required/)
  assert.match(page, /<button\b[^>]*id="generationSubmitButton"[^>]*type="submit"/)
  assert.match(page, /<section\b[^>]*id="generationConfirm"[^>]*hidden/)
  assert.match(page, /data-workspace-nav="new-model"[^>]*class="active"/)
  assert.match(page, /data-workspace-nav="my-models"/)
  assert.match(page, /data-workspace-nav="model-feedback"/)
  assert.match(page, /data-workspace-nav="archive"/)
  assert.match(page, /任务工作台/)
  assert.match(page, /模型有问题/)
  assert.match(page, /使用效果/)
  assert.match(page, /资料档案/)
  assert.match(page, /记录使用效果/)
  assert.match(page, /传统方式预计耗时/)
  assert.match(page, /脱敏典型变更A/)
  assert.match(page, /脱敏V37客户任务A/)
  assert.match(page, /节省 160 分钟（66.7%）/)
  assert.equal(page.includes('id="clarifications"'), false)
  assert.equal(page.includes('待工程师确认（0 项）'), false)
  assert.match(page, /V37的760W×1917H×550D、2列6门、L642-R246/)
  assert.match(page, /V36的740W四门L66-R66和V35的740W六门L642-R246继续作为历史已验证精确配方/)
  assert.match(page, /V37-Q 编号/)
  assert.match(page, /参数化模型下载及反馈\/工程反馈/)
  assert.ok(page.includes(submissionResult.feedbackId))
  assert.ok(page.includes(efficiencyResult.feedbackId))
  assert.match(page, /查看附件 1/)
  assert.match(page, /结构工程辅助基础模型/)
  assert.equal(page.includes('productionReleaseEligible'), false)
  assert.equal(page.includes('generation-start-button'), false)
  for (const forbiddenPhrase of ['非生产', '生产可用', '生产发布', '生产放行', '生产级', '工程签核']) {
    assert.equal(page.includes(forbiddenPhrase), false, `page must not expose legacy phrase: ${forbiddenPhrase}`)
  }

  const indexPath = resolve(STORAGE_ROOT, '工程反馈/feedback_index.json')
  assert.equal(existsSync(indexPath), true)
  const storedIndex = JSON.parse(readFileSync(indexPath, 'utf8'))
  assert.equal(storedIndex.feedback.length, 2)
  const efficiencyIndexRow = storedIndex.feedback.find((item) => item.id === efficiencyResult.feedbackId)
  const issueIndexRow = storedIndex.feedback.find((item) => item.id === submissionResult.feedbackId)
  assert.equal(efficiencyIndexRow.recordType, 'efficiency_validation')
  assert.equal(efficiencyIndexRow.attachmentCount, 0)
  assert.equal(efficiencyIndexRow.feedbackIsEngineeringSignoff, false)
  assert.equal(Object.hasOwn(efficiencyIndexRow, 'productionReleaseEligible'), false)
  assert.equal(efficiencyIndexRow.efficiencyValidation.taskReference, '脱敏典型变更A')
  const efficiencyFolder = resolve(STORAGE_ROOT, '工程反馈', efficiencyIndexRow.folderName)
  const storedEfficiency = JSON.parse(readFileSync(resolve(efficiencyFolder, 'feedback.json'), 'utf8'))
  assert.equal(storedEfficiency.recordType, 'efficiency_validation')
  assert.equal(storedEfficiency.feedbackIsEngineeringSignoff, false)
  assert.equal(Object.hasOwn(storedEfficiency, 'productionReleaseEligible'), false)
  assert.equal(storedEfficiency.efficiencyValidation.timeSavingsMinutes, 160)
  assert.equal(existsSync(resolve(efficiencyFolder, 'attachment-01.png')), false)
  const issueFolder = resolve(STORAGE_ROOT, '工程反馈', issueIndexRow.folderName)
  assert.equal(existsSync(resolve(issueFolder, 'feedback.json')), true)
  assert.equal(existsSync(resolve(issueFolder, 'attachment-01.png')), true)

  const legacyId = 'V35-Q-20260801T000000-LEGACY'
  const legacyFolderName = legacyId
  const legacyRecord = {
    id: legacyId,
    submittedAt: '2026-08-01T00:00:00.000Z',
    username: 'legacy-reviewer',
    reviewerName: '历史结构工程师',
    discipline: '结构审核',
    reviewRoundId: '16029-v43-v35-one-door-one-lock-hole-rereview-20260809',
    reviewTarget: '16029-v43-v35-one-door-one-lock-hole-rereview-zip',
    reviewTargetLabel: '16029 740W v43 V35 一门一锁孔工程辅助模型',
    severity: 'P2',
    decision: 'comment_only',
    decisionLabel: '仅记录',
    issueCategory: 'other_structure_issue',
    issueCategoryLabel: '其他结构问题',
    componentName: '历史结构件',
    modelLocation: '历史位置',
    currentProblem: '历史记录缺少recordType，仍应按结构问题兼容显示。',
    summary: '历史记录缺少recordType，仍应按结构问题兼容显示。',
    expectedResult: '无需迁移旧JSON即可继续读取。',
    attachments: [],
    attachmentCount: 0,
    folderName: legacyFolderName,
    feedbackIsEngineeringSignoff: false,
    productionReleaseEligible: false,
  }
  const legacyFolder = resolve(STORAGE_ROOT, '工程反馈', legacyFolderName)
  mkdirSync(legacyFolder, { recursive: true })
  writeFileSync(resolve(legacyFolder, 'feedback.json'), `${JSON.stringify(legacyRecord, null, 2)}\n`, 'utf8')
  storedIndex.feedback.push(legacyRecord)
  writeFileSync(indexPath, `${JSON.stringify(storedIndex, null, 2)}\n`, 'utf8')

  const legacyPageResponse = await fetch(`${baseUrl}/`, { headers: { Cookie: cookie } })
  assert.equal(legacyPageResponse.status, 200)
  const legacyPage = await legacyPageResponse.text()
  assert.ok(legacyPage.includes(legacyId))
  assert.match(legacyPage, /历史记录缺少recordType/)
  assert.match(legacyPage, /历史反馈轮次（1 条/)

  const legacyAuditResponse = await fetch(`${baseUrl}/local-audit/feedback-summary.json`)
  assert.equal(legacyAuditResponse.status, 200)
  const legacyAudit = await legacyAuditResponse.json()
  assert.equal(legacyAudit.totalSubmissionCount, 3)
  assert.equal(legacyAudit.currentRoundSubmissionCount, 2)
  assert.equal(legacyAudit.byRound.find((item) => item.reviewRoundId === '16029-v43-v35-one-door-one-lock-hole-rereview-20260809').count, 1)
  const legacyAuditRow = legacyAudit.feedback.find((item) => item.id === legacyId)
  assert.equal(legacyAuditRow.recordType, 'structure_issue')
  assert.equal(legacyAuditRow.schema, 'winnsen.review.structure_issue.legacy')
  assert.deepEqual(legacyAudit.indexRowsWithoutFolder, [])
  assert.deepEqual(legacyAudit.foldersWithoutIndexRow, [])

  const legacyIndexRow = JSON.parse(readFileSync(indexPath, 'utf8')).feedback.find((item) => item.id === legacyId)
  const storedLegacy = JSON.parse(readFileSync(resolve(legacyFolder, 'feedback.json'), 'utf8'))
  assert.equal(Object.hasOwn(legacyIndexRow, 'recordType'), false)
  assert.equal(Object.hasOwn(storedLegacy, 'recordType'), false)

  console.log('16029 review portal structured feedback upload: PASS')
} finally {
  await stopChild(child)
  rmSync(TEMP_ROOT, { recursive: true, force: true })
}
