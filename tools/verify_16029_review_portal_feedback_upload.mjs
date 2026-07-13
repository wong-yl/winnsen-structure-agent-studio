import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createServer as createNetServer } from 'node:net'
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

const ROOT = resolve(process.cwd())
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const TEMP_ROOT = resolve(ROOT, 'tmp/verify_16029_review_portal_feedback_upload')
const DATA_DIR = resolve(TEMP_ROOT, 'data')
const STORAGE_ROOT = resolve(TEMP_ROOT, 'storage')
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

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(DATA_DIR, { recursive: true })
writeFileSync(resolve(DATA_DIR, 'review_download_invite_code.txt'), 'test-invite-code', 'utf8')

const port = await freePort()
const baseUrl = `http://127.0.0.1:${port}`
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
  assert.equal(status.reviewRound, '16029-v43-v23-engineering-feedback-20260713')
  assert.equal(status.sharedStorageReady, true)
  assert.equal(JSON.stringify(status).includes('sourcePath'), false)
  assert.equal(JSON.stringify(status).includes(STORAGE_ROOT), false)

  const unauthenticated = await fetch(`${baseUrl}/feedback.json`, { redirect: 'manual' })
  assert.equal(unauthenticated.status, 302)
  assert.equal(unauthenticated.headers.get('location'), '/login')

  const registration = await fetch(`${baseUrl}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      username: 'portal-feedback-test',
      password: 'portal-test-password',
      inviteCode: 'test-invite-code',
    }),
    redirect: 'manual',
  })
  assert.equal(registration.status, 302)
  const cookie = String(registration.headers.get('set-cookie') || '').split(';', 1)[0]
  assert.match(cookie, /^review_session=/)

  const payload = {
    reviewerName: '结构测试工程师',
    discipline: '结构',
    reviewTarget: '16029-v43-internal-sheetmetal-v23-controlled-candidate-zip',
    issueCategory: 'lock_common_datum_and_engagement',
    severity: 'P1',
    decision: 'needs_changes',
    componentName: '储物柜门装配_L6 / 锁孔基准',
    modelLocation: '左列 6/12 门后锁侧中部',
    currentProblem: '锁舌与锁孔中心存在可见偏移。',
    expectedResult: '锁舌、锁孔和定位孔使用共同基准。',
    keyDimensionTolerance: '待结构工程师测量确认',
    referenceBasis: '1000W gold/source reference',
    acceptanceCriteria: '六个门位逐一检查，无干涉且啮合深度符合正式图纸。',
    attachments: [{ name: 'Q01.png', type: 'image/png', dataUrl: `data:image/png;base64,${PNG_BASE64}` }],
  }
  const submission = await fetch(`${baseUrl}/feedback`, {
    method: 'POST',
    headers: { Cookie: cookie, 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  assert.equal(submission.status, 200)
  const submissionResult = await submission.json()
  assert.match(submissionResult.feedbackId, /^V23-Q-[A-Z0-9-]+$/)
  assert.equal(submissionResult.savedFiles, 1)

  const mineResponse = await fetch(`${baseUrl}/feedback.json`, { headers: { Cookie: cookie } })
  assert.equal(mineResponse.status, 200)
  const mine = await mineResponse.json()
  assert.equal(mine.feedback.length, 1)
  assert.equal(mine.feedback[0].id, submissionResult.feedbackId)
  assert.equal(mine.feedback[0].severity, 'P1')
  assert.equal(mine.feedback[0].issueCategory, 'lock_common_datum_and_engagement')
  assert.equal(mine.feedback[0].feedbackIsEngineeringSignoff, false)
  assert.equal(mine.feedback[0].productionReleaseEligible, false)

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

  const pageResponse = await fetch(`${baseUrl}/`, { headers: { Cookie: cookie } })
  assert.equal(pageResponse.status, 200)
  const page = await pageResponse.text()
  assert.match(page, /上传结构问题反馈/)
  assert.match(page, /v43-int-v23-all-sources-isolated/)
  assert.match(page, /参数化模型下载及反馈\/工程反馈/)
  assert.ok(page.includes(submissionResult.feedbackId))
  assert.match(page, /查看附件 1/)

  const indexPath = resolve(STORAGE_ROOT, '工程反馈/feedback_index.json')
  assert.equal(existsSync(indexPath), true)
  const storedIndex = JSON.parse(readFileSync(indexPath, 'utf8'))
  assert.equal(storedIndex.feedback.length, 1)
  const folder = resolve(STORAGE_ROOT, '工程反馈', storedIndex.feedback[0].folderName)
  assert.equal(existsSync(resolve(folder, 'feedback.json')), true)
  assert.equal(existsSync(resolve(folder, 'attachment-01.png')), true)

  console.log('16029 review portal structured feedback upload: PASS')
} finally {
  await stopChild(child)
  rmSync(TEMP_ROOT, { recursive: true, force: true })
}
