import { createReadStream, existsSync, mkdirSync, openSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:http'
import { networkInterfaces } from 'node:os'
import { isAbsolute, relative, resolve } from 'node:path'
import { spawn } from 'node:child_process'
import { pbkdf2Sync, randomBytes, timingSafeEqual } from 'node:crypto'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const PORT = Number(process.env.STUDIO_REVIEW_PORT || 5180)
const HOST = process.env.STUDIO_REVIEW_HOST || '0.0.0.0'
const DATA_DIR = resolve(ROOT, 'data')
const FEEDBACK_DIR = resolve(DATA_DIR, 'review_feedback')
const GENERATION_REQUEST_DIR = resolve(DATA_DIR, 'review_generation_requests')
const USER_DB_PATH = resolve(DATA_DIR, 'review_download_users.json')
const FEEDBACK_INDEX_PATH = resolve(FEEDBACK_DIR, 'feedback_index.json')
const GENERATION_INDEX_PATH = resolve(GENERATION_REQUEST_DIR, 'generation_request_index.json')
const INVITE_CODE_PATH = resolve(DATA_DIR, 'review_download_invite_code.txt')
const BRAND_LOGO_PATH = resolve(ROOT, 'apps/web/public/brand/winnsen-logo.jpg')
const BRAND_MARK_PATH = resolve(ROOT, 'apps/web/public/brand/winnsen-mark.png')
const GENERATION_QUEUE_WORKER_PATH = resolve(ROOT, 'tools/process_16029_review_generation_queue.mjs')
const GENERATION_OUTPUT_ROOT = resolve(ROOT, 'workers/generated_models')
const GENERATION_LOG_ROOT = resolve(ROOT, 'workers/generation_logs')
const SESSION_COOKIE = 'review_session'
const SESSION_TTL_MS = 8 * 60 * 60 * 1000
const PASSWORD_ITERATIONS = 120000
const MAX_BODY_BYTES = 32 * 1024 * 1024
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024
const sessions = new Map()

const reviewRound = {
  id: '16029-800w-gold-variable-review-20260528',
  title: '16029 800W gold-variable 三组柜子审核',
  project: '16029 800W gold-variable',
  cadMainline: 'SolidWorks 2020',
  boundary: '800W x 1917H x 550D / W337 / gap 2+3+2=7',
  gateStatus: 'Scope gate PASS / 工程审核中',
  gateBlocker: 'LMS/SML 已补齐 SolidWorks 2020 打开截图证据；名义 1917H 与 raw STEP bbox 1983H 的底脚/顶部/前侧外伸仍需结构工程师签核。',
  instruction: '本轮只审核 LMS、SML、DUAL 三个候选审核包；历史候选包不作为本轮输入，生产图纸释放前需确认名义外形和安装外形口径。',
}

const assets = [
  {
    id: 'lms-gold-variable',
    title: 'LMS 审核包',
    category: '单方案审核',
    description: 'LMS: 大 6/12，中 4/12，小 2/12；工程师主审 STEP、verify CSV、model gate、bbox gate、SolidWorks 2020 打开截图，以及名义外形/raw bbox 外伸口径。',
    fileName: '16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip',
    path: resolve(ROOT, 'workers/handoffs/16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
  {
    id: 'sml-gold-variable',
    title: 'SML 审核包',
    category: '单方案审核',
    description: 'SML: 小 2/12，中 4/12，大 6/12；工程师主审 STEP、verify CSV、model gate、bbox gate、SolidWorks 2020 打开截图，以及名义外形/raw bbox 外伸口径。',
    fileName: '16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip',
    path: resolve(ROOT, 'workers/handoffs/16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
  {
    id: 'dual-gold-variable',
    title: 'LMS/SML 总审核包',
    category: '双方案候选汇总',
    description: '把 LMS 与 SML 两个候选审核包合并在一个 ZIP 中，适合统一转发、归档和外形口径签核；不是第三个结构方案。',
    fileName: '16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip',
    path: resolve(ROOT, 'workers/handoffs/16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
]

const byId = new Map(assets.map((asset) => [asset.id, asset]))

function ensureDirs() {
  mkdirSync(DATA_DIR, { recursive: true })
  mkdirSync(FEEDBACK_DIR, { recursive: true })
  mkdirSync(GENERATION_REQUEST_DIR, { recursive: true })
}

function readJson(path, fallback) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'))
  } catch {
    return fallback
  }
}

function writeJson(path, value) {
  writeFileSync(path, JSON.stringify(value, null, 2), 'utf8')
}

function htmlEscape(value) {
  return String(value ?? '').replace(/[&<>"']/g, (char) => (
    { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]
  ))
}

function normalizeUsername(value) {
  return String(value || '').trim().toLowerCase().replace(/[^a-z0-9._@-]/g, '')
}

function readUsers() {
  const db = readJson(USER_DB_PATH, { users: [] })
  return Array.isArray(db.users) ? db : { users: [] }
}

function saveUsers(db) {
  writeJson(USER_DB_PATH, db)
}

function hashPassword(password, salt, iterations = PASSWORD_ITERATIONS) {
  return pbkdf2Sync(String(password), Buffer.from(salt, 'hex'), iterations, 32, 'sha256').toString('hex')
}

function verifyPassword(user, password) {
  const expected = Buffer.from(user.hash, 'hex')
  const actual = Buffer.from(hashPassword(password, user.salt, user.iterations || PASSWORD_ITERATIONS), 'hex')
  return expected.length === actual.length && timingSafeEqual(expected, actual)
}

function createUser(username, password) {
  const salt = randomBytes(16).toString('hex')
  return {
    username,
    salt,
    hash: hashPassword(password, salt),
    iterations: PASSWORD_ITERATIONS,
    createdAt: new Date().toISOString(),
  }
}

function parseCookies(request) {
  return Object.fromEntries(
    String(request.headers.cookie || '')
      .split(';')
      .map((part) => part.trim())
      .filter(Boolean)
      .map((part) => {
        const index = part.indexOf('=')
        return index < 0 ? [part, ''] : [part.slice(0, index), decodeURIComponent(part.slice(index + 1))]
      }),
  )
}

function createSession(response, username) {
  const token = randomBytes(24).toString('hex')
  sessions.set(token, { username, expiresAt: Date.now() + SESSION_TTL_MS })
  response.setHeader('Set-Cookie', `${SESSION_COOKIE}=${encodeURIComponent(token)}; HttpOnly; Path=/; SameSite=Lax; Max-Age=${Math.floor(SESSION_TTL_MS / 1000)}`)
}

function currentUser(request) {
  const token = parseCookies(request)[SESSION_COOKIE]
  if (!token) return null
  const session = sessions.get(token)
  if (!session || session.expiresAt < Date.now()) {
    sessions.delete(token)
    return null
  }
  session.expiresAt = Date.now() + SESSION_TTL_MS
  return session.username
}

function sendHtml(response, status, html) {
  response.writeHead(status, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' })
  response.end(html)
}

function sendJson(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' })
  response.end(JSON.stringify(body, null, 2))
}

function redirect(response, location) {
  response.writeHead(302, { Location: location })
  response.end()
}

function readBody(request, maxBytes = MAX_BODY_BYTES) {
  return new Promise((resolveBody, reject) => {
    const chunks = []
    let total = 0
    request.on('data', (chunk) => {
      total += chunk.length
      if (total > maxBytes) {
        reject(new Error('request body too large'))
        request.destroy()
        return
      }
      chunks.push(chunk)
    })
    request.on('end', () => resolveBody(Buffer.concat(chunks)))
    request.on('error', reject)
  })
}

async function readJsonBody(request, maxBytes = MAX_BODY_BYTES) {
  const body = (await readBody(request, maxBytes)).toString('utf8')
  try {
    const payload = JSON.parse(body)
    if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
      const error = new Error('JSON body must be an object')
      error.statusCode = 400
      throw error
    }
    return payload
  } catch (error) {
    if (error.statusCode) throw error
    const parseError = new Error('invalid JSON body')
    parseError.statusCode = 400
    throw parseError
  }
}

function formatBytes(value) {
  if (value < 1024) return `${value} B`
  const kb = value / 1024
  if (kb < 1024) return `${kb.toFixed(kb >= 100 ? 0 : 1)} KB`
  const mb = kb / 1024
  if (mb < 1024) return `${mb.toFixed(mb >= 100 ? 0 : 1)} MB`
  return `${(mb / 1024).toFixed(1)} GB`
}

function assetInfo(asset) {
  if (!existsSync(asset.path)) return { ...asset, available: false, sizeBytes: null, modifiedAt: null }
  const stat = statSync(asset.path)
  return { ...asset, available: true, sizeBytes: stat.size, modifiedAt: stat.mtime.toISOString() }
}

function readFeedbackIndex() {
  const index = readJson(FEEDBACK_INDEX_PATH, { feedback: [] })
  return Array.isArray(index.feedback) ? index : { feedback: [] }
}

function currentRoundFeedback(username = null) {
  return readFeedbackIndex().feedback
    .filter((item) => item.reviewRoundId === reviewRound.id)
    .filter((item) => !username || item.username === username)
    .slice(0, 40)
}

function readGenerationIndex() {
  const index = readJson(GENERATION_INDEX_PATH, { requests: [] })
  return Array.isArray(index.requests) ? index : { requests: [] }
}

function currentGenerationRequests(username = null) {
  return readGenerationIndex().requests
    .filter((item) => !username || item.username === username)
    .slice(0, 40)
}

function isUnderRoot(path, root) {
  const rel = relative(root, path)
  return rel === '' || (rel && !rel.startsWith('..') && !isAbsolute(rel))
}

function generationStatusText(item) {
  if (item.downloadUrl) return item.resultKind === 'cad_worker_payload_package' ? '任务包已生成' : '已生成'
  if (String(item.status || '').includes('failed')) return '生成失败'
  if (String(item.status || '').includes('running')) return '后台生成中'
  return '等待后台生成'
}

function generationActionHtml(item) {
  if (item.downloadUrl) {
    const label = item.resultKind === 'cad_worker_payload_package' ? '下载任务包' : '下载结果'
    return `<a class="button" href="${htmlEscape(item.downloadUrl)}">${label}</a>`
  }
  if (String(item.status || '').includes('failed')) return '<button type="button" disabled>生成失败</button>'
  if (String(item.status || '').includes('running')) return '<button type="button" disabled>生成中</button>'
  return '<button type="button" disabled>等待生成</button>'
}

function startGenerationQueueWorker(requestId) {
  if (!existsSync(GENERATION_QUEUE_WORKER_PATH)) return
  mkdirSync(GENERATION_LOG_ROOT, { recursive: true })
  const stdout = openSync(resolve(GENERATION_LOG_ROOT, `review_generation_${requestId}_queue.stdout.txt`), 'a')
  const stderr = openSync(resolve(GENERATION_LOG_ROOT, `review_generation_${requestId}_queue.stderr.txt`), 'a')
  const child = spawn(process.execPath, [GENERATION_QUEUE_WORKER_PATH, '--id', requestId, '--limit', '1'], {
    cwd: ROOT,
    detached: true,
    stdio: ['ignore', stdout, stderr],
    windowsHide: true,
    env: {
      ...process.env,
      STUDIO_REVIEW_QUEUE_STARTED_BY: 'review_portal',
    },
  })
  child.unref()
}

function clampText(value, maxLength = 4000) {
  return String(value || '').trim().slice(0, maxLength)
}

function submitGenerationRequest(username, payload) {
  mkdirSync(GENERATION_REQUEST_DIR, { recursive: true })
  const createdAt = new Date().toISOString()
  const requestId = `${createdAt.replace(/[-:.]/g, '').slice(0, 15)}-${randomBytes(3).toString('hex')}`
  const normalized = {
    id: requestId,
    createdAt,
    username,
    status: 'draft_ready_for_internal_cad_worker',
    taskMode: clampText(payload.taskMode || 'full_assembly', 40),
    taskType: payload.taskMode === 'single_model' ? 'single_model_generation_draft' : 'full_assembly_generation_draft',
    prompt: clampText(payload.prompt),
    cabinetWidth: clampText(payload.cabinetWidth, 40),
    cabinetHeight: clampText(payload.cabinetHeight, 40),
    cabinetDepth: clampText(payload.cabinetDepth, 40),
    columns: clampText(payload.columns, 40),
    doorCount: clampText(payload.doorCount, 40),
    doorWidth: clampText(payload.doorWidth, 40),
    doorHeight: clampText(payload.doorHeight, 40),
    rowSequence: clampText(payload.rowSequence, 200),
    doorType: clampText(payload.doorType, 100),
    lockType: clampText(payload.lockType, 100),
    hingeType: clampText(payload.hingeType, 100),
    latchType: clampText(payload.latchType, 100),
    reinforcement: clampText(payload.reinforcement, 100),
    openings: clampText(payload.openings, 200),
    material: clampText(payload.material, 100),
    thickness: clampText(payload.thickness, 60),
    previewType: 'browser_parametric_3d_viewport_reference',
    workerRecommendation: 'route_to_internal_solidworks_or_freecad_after_evidence_gate',
    requestedOutputs: [
      'browser_parametric_3d_preview',
      'cad_worker_task_payload',
      'step_or_solidworks_after_engineering_gate',
    ],
    validationRequired: [
      'door_gap_and_clearance',
      'hinge_hole_pattern',
      'lock_and_latch_interface',
      'reinforcement_rib_clearance',
      'material_thickness_and_bend_rules',
      'model_gate_before_production_release',
    ],
    boundary:
      '登录台只给工程师提交生成意图和查看预览；正式CAD文件由内部worker产出后再开放下载，不在本页展示内部规则日志。',
  }

  const outPath = resolve(GENERATION_REQUEST_DIR, `${requestId}.json`)
  writeJson(outPath, normalized)
  const index = readGenerationIndex()
  index.requests = [normalized, ...index.requests.filter((item) => item.id !== requestId)].slice(0, 300)
  writeJson(GENERATION_INDEX_PATH, index)
  return normalized
}

function renderAuthPage(error = '', mode = 'login') {
  const isRegister = mode === 'register'
  return renderShell(`
    <main class="auth-wrap">
      <section class="auth-card">
        <div class="auth-logo">
          <img src="/brand/winnsen-logo.jpg" alt="Winnsen" />
        </div>
        <p class="eyebrow">WINNSEN REVIEW PORTAL</p>
        <h1>${isRegister ? '注册审核账号' : '结构审核登录'}</h1>
        <p class="muted">${htmlEscape(reviewRound.title)}，登录后只看到两条生成入口和一个预览窗口；内部 gate、日志和多余数据默认收起。</p>
        ${error ? `<div class="alert">${htmlEscape(error)}</div>` : ''}
        <form method="post" action="${isRegister ? '/register' : '/login'}" class="auth-form">
          <label>账号<input name="username" autocomplete="username" required /></label>
          <label>密码<input name="password" type="password" autocomplete="${isRegister ? 'new-password' : 'current-password'}" required /></label>
          ${isRegister ? '<label>邀请码<input name="inviteCode" required /></label>' : ''}
          <button type="submit">${isRegister ? '注册并进入审核' : '登录审核系统'}</button>
        </form>
        <p class="muted">${isRegister ? '已有账号？' : '第一次访问？'} <a href="${isRegister ? '/login' : '/register'}">${isRegister ? '返回登录' : '注册账号'}</a></p>
      </section>
    </main>
  `)
}

function renderShell(content, username = '') {
  return `<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>${htmlEscape(reviewRound.title)}</title>
<style>
  :root { color-scheme: light; --ink:#11214a; --muted:#607089; --line:#d9e2ef; --soft:#f5f8fc; --brand:#1f3585; --hot:#f04a12; --good:#14884f; --warn:#a76500; --risk:#b42318; }
  * { box-sizing: border-box; }
  body { margin: 0; font-family: "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; color: var(--ink); background: #eef3f9; }
  a { color: inherit; }
  .shell { min-height: 100vh; display: grid; grid-template-columns: 280px minmax(0, 1fr); }
  aside { padding: 24px 18px; background: linear-gradient(180deg, #1f3585, #102055); color: #fff; }
  .portal-logo { width: 156px; height: 56px; margin-bottom: 18px; display:flex; align-items:center; justify-content:center; border-radius:8px; background:#fff; overflow:hidden; }
  .portal-logo img { max-width: 138px; max-height: 42px; object-fit:contain; }
  aside h2 { margin: 0 0 8px; font-size: 18px; }
  aside p { margin: 0 0 20px; color: #d9e1ee; font-size: 13px; line-height: 1.55; }
  .side-card { margin-top: 18px; padding: 14px; border: 1px solid rgba(255,255,255,.18); border-radius: 8px; background: rgba(255,255,255,.07); }
  .side-card strong, .side-card span { display:block; }
  .side-card span { color:#c6d2e4; font-size:12px; }
  main { padding: 28px; }
  .auth-wrap { min-height: 100vh; display:grid; place-items:center; padding:24px; }
  .auth-card, .panel, .asset-card, .feedback-row { border: 1px solid var(--line); border-radius: 8px; background: #fff; box-shadow: 0 12px 32px rgba(20,35,70,.08); }
  .auth-card { width: min(460px, 100%); padding: 24px; }
  .auth-logo { width: 172px; height: 62px; margin-bottom: 16px; display:flex; align-items:center; justify-content:center; border:1px solid var(--line); border-radius:8px; background:#fff; overflow:hidden; }
  .auth-logo img { max-width: 148px; max-height: 46px; object-fit:contain; }
  .eyebrow { margin:0 0 6px; color:var(--hot); font-size:12px; font-weight:700; letter-spacing:0; }
  h1 { margin: 0; font-size: 30px; line-height: 1.15; }
  h2 { margin: 0; font-size: 20px; }
  .muted { color: var(--muted); line-height: 1.55; }
  .auth-form { display:grid; gap:12px; margin-top:18px; }
  label { display:grid; gap:6px; color:var(--muted); font-size:13px; }
  input, select, textarea { width:100%; min-width:0; border:1px solid var(--line); border-radius:8px; padding:10px 11px; font:inherit; color:var(--ink); background:#fff; }
  textarea { min-height:92px; resize:vertical; }
  button, .button { display:inline-flex; align-items:center; justify-content:center; min-height:40px; padding:0 14px; border:0; border-radius:8px; background:var(--hot); color:#fff; font:inherit; font-weight:700; text-decoration:none; cursor:pointer; }
  .button.secondary, button.secondary { background:#eef3ff; color:var(--brand); border:1px solid #c8d5ee; }
  .top { display:flex; justify-content:space-between; gap:18px; align-items:flex-start; margin-bottom:18px; }
  .top p { margin:8px 0 0; }
  .nav-links { display:flex; flex-wrap:wrap; gap:8px; margin-top:16px; }
  .chips { display:flex; flex-wrap:wrap; gap:8px; margin-top:14px; }
  .chip { padding:6px 10px; border-radius:999px; background:#eef3ff; border:1px solid #c8d5ee; color:#28476d; font-size:12px; font-weight:700; }
  .chip.good { color:var(--good); border-color:#bfe5cf; background:#edf9f1; }
  .chip.warn { color:var(--warn); border-color:#f5d08d; background:#fff8e8; }
  .gate-warning { margin-top:14px; padding:12px 14px; border:1px solid #f5d08d; border-radius:8px; background:#fff8e8; color:#8a4f00; line-height:1.55; }
  .panel { padding:18px; margin-bottom:16px; }
  .mode-grid { display:grid; grid-template-columns: repeat(2, minmax(0,1fr)); gap:12px; }
  .mode-card { min-height:96px; padding:14px; border:1px solid var(--line); border-radius:8px; background:#f8fbff; color:var(--ink); text-align:left; display:grid; gap:6px; align-content:start; cursor:pointer; }
  .mode-card strong, .mode-card span { display:block; }
  .mode-card span { color:var(--muted); font-size:12px; line-height:1.45; }
  .mode-card.active { border-color:var(--hot); background:#fff7f3; box-shadow:0 0 0 2px rgba(240,74,18,.12); }
  .mode-card:focus-visible { outline:3px solid rgba(31,53,133,.2); outline-offset:2px; }
  .quick-status { display:grid; grid-template-columns: repeat(3, minmax(0,1fr)); gap:10px; margin-top:14px; }
  .quick-status div { padding:12px; border:1px solid var(--line); border-radius:8px; background:#f8fbff; }
  .quick-status span, .quick-status strong { display:block; }
  .quick-status span { color:var(--muted); font-size:12px; }
  .quick-status strong { font-size:15px; }
  .advanced-fields { border:1px solid var(--line); border-radius:8px; background:#fbfdff; }
  .advanced-fields summary { padding:12px; cursor:pointer; font-weight:700; color:var(--brand); }
  .advanced-fields .field-grid { padding:0 12px 12px; }
  .progress-wrap { margin-top:10px; display:grid; gap:6px; }
  .progress-track { height:8px; overflow:hidden; border-radius:999px; background:#e3eaf5; }
  .progress-track span { display:block; height:100%; width:0; background:linear-gradient(90deg, var(--hot), #ff8a4b); transition:width .35s ease; }
  .progress-label { color:var(--muted); font-size:12px; }
  .asset-grid { display:grid; grid-template-columns: repeat(3, minmax(0,1fr)); gap:12px; }
  .asset-card { padding:16px; display:grid; gap:12px; }
  .asset-card strong { font-size:17px; }
  .asset-card p { margin:0; color:var(--muted); line-height:1.5; font-size:13px; }
  .meta { display:grid; gap:6px; font-size:12px; color:var(--muted); }
  .meta b { color:var(--ink); word-break:break-all; }
  .feedback-grid { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:16px; }
  .studio-grid { display:grid; grid-template-columns:minmax(0, 1fr) minmax(360px, .9fr); gap:16px; align-items:start; }
  .generator-form { display:grid; gap:12px; }
  .field-grid { display:grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap:10px; }
  .field-grid.two { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .prompt-actions { display:flex; flex-wrap:wrap; gap:8px; }
  .preview-panel { min-width:0; padding:14px; border:1px solid var(--line); border-radius:8px; background:#f8fbff; }
  .preview-head { display:flex; align-items:flex-start; justify-content:space-between; gap:12px; margin-bottom:10px; }
  .preview-head strong, .preview-head span { display:block; }
  .preview-head span { color:var(--muted); font-size:12px; }
  .viewport-toolbar { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:10px; margin:0 0 10px; }
  .view-button-row { display:flex; flex-wrap:wrap; gap:6px; }
  .view-button-row .button { padding:7px 9px; min-height:32px; font-size:12px; }
  .view-button-row .view-active { background:var(--brand); color:#fff; border-color:var(--brand); }
  .viewport-hud { position:absolute; left:10px; bottom:10px; z-index:2; display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:6px; max-width:360px; pointer-events:none; }
  .viewport-hud div { padding:7px 9px; border:1px solid rgba(31,53,133,.18); border-radius:6px; background:rgba(255,255,255,.88); box-shadow:0 6px 18px rgba(17,33,74,.08); }
  .viewport-hud span, .viewport-hud strong { display:block; }
  .viewport-hud span { color:var(--muted); font-size:10px; }
  .viewport-hud strong { color:var(--ink); font-size:12px; white-space:nowrap; }
  .preview-canvas-wrap { position:relative; overflow:hidden; border:1px solid #cdd9ec; border-radius:8px; background:#edf3fa; }
  #cabinetPreview { width:100%; height:520px; display:block; cursor:grab; touch-action:none; }
  #cabinetPreview:active { cursor:grabbing; }
  .preview-note { margin:10px 0 0; color:var(--muted); font-size:12px; line-height:1.55; }
  .preview-controls { display:grid; grid-template-columns:44px minmax(0,1fr) 64px 44px minmax(0,1fr) 64px; gap:10px; align-items:center; margin-top:10px; }
  .preview-controls label { color:var(--muted); font-size:12px; }
  .preview-controls input { width:100%; }
  .generated-prompt { display:none !important; padding:10px 12px; border:1px solid #f5d08d; border-radius:8px; background:#fff8e8; color:#8a4f00; font-size:12px; line-height:1.55; white-space:pre-wrap; }
  .request-list { display:grid; gap:10px; }
  .request-row { padding:11px; border:1px solid var(--line); border-radius:8px; background:#fff; }
  .request-row strong, .request-row span { display:block; }
  .request-row span { color:var(--muted); font-size:12px; line-height:1.45; }
  .request-row-actions { display:flex; flex-wrap:wrap; gap:8px; margin-top:10px; }
  .request-row-actions .button,
  .request-row-actions button { min-height:34px; padding:0 12px; font-size:12px; }
  .request-row-actions button[disabled] { cursor:not-allowed; opacity:.68; background:#eef3ff; color:var(--muted); border:1px solid var(--line); }
  .feedback-row { padding:12px; margin-top:10px; }
  .feedback-row p { margin:5px 0 0; color:var(--muted); }
  .alert { padding:10px 12px; margin-top:14px; border-radius:8px; background:#fff5f5; color:var(--risk); border:1px solid #f4c2c2; }
  .ok { padding:10px 12px; margin-top:12px; border-radius:8px; background:#edf9f1; color:var(--good); border:1px solid #bfe5cf; }
  @media (max-width: 1100px) { .studio-grid { grid-template-columns:1fr; } }
  @media (max-width: 900px) { .shell { grid-template-columns:1fr; } aside { position:static; } main { padding:16px; } .asset-grid, .feedback-grid, .field-grid, .field-grid.two, .mode-grid, .quick-status { grid-template-columns:1fr; } .top { flex-direction:column; } #cabinetPreview { height:360px; } .preview-controls { grid-template-columns:44px minmax(0,1fr) 58px; } .viewport-hud { position:static; margin:8px; max-width:none; } }
</style>
</head>
<body>${content}</body>
</html>`
}

function renderAssetCards() {
  return assets.map(assetInfo).map((asset) => `
    <article class="asset-card">
      <div>
        <h2>${htmlEscape(asset.title)}</h2>
        <span class="chip">${asset.available ? '可下载' : '文件缺失'}</span>
      </div>
      <p>${asset.id === 'dual-gold-variable' ? '统一下载 LMS/SML 总包。' : '下载后用 SolidWorks 2020 打开审核。'}</p>
      ${asset.available ? `<a class="button" href="/download/${asset.id}">下载</a>` : '<button disabled>文件缺失</button>'}
    </article>
  `).join('')
}

function feedbackRows(rows) {
  if (!rows.length) return '<p class="muted">当前轮次还没有提交记录。</p>'
  return rows.map((item) => `
    <div class="feedback-row">
      <strong>${htmlEscape(item.reviewerName || item.username)} / ${htmlEscape(item.decisionLabel || item.decision)}</strong>
      <p>${htmlEscape(item.summary || '已提交反馈')}</p>
      <p>${htmlEscape(item.reviewTargetLabel || item.reviewTarget)} / ${new Date(item.submittedAt).toLocaleString('zh-CN', { hour12: false })}</p>
    </div>
  `).join('')
}

function generationRows(rows) {
  if (!rows.length) return '<p class="muted">还没有3D建模任务草稿。提交后会保存在本机 data/review_generation_requests 目录，后续可接 CAD worker 队列。</p>'
  return rows.map((item) => `
    <div class="request-row">
      <strong>${item.taskMode === 'single_model' ? '单个模型' : '完整装配体'} / ${htmlEscape(item.cabinetWidth || '-')}W x ${htmlEscape(item.cabinetHeight || '-')}H x ${htmlEscape(item.cabinetDepth || '-')}D</strong>
      <span>${htmlEscape(item.doorCount || '-')} 门 / 门板 ${htmlEscape(item.doorWidth || '-')}W x ${htmlEscape(item.doorHeight || '-')}H</span>
      <span>${new Date(item.createdAt).toLocaleString('zh-CN', { hour12: false })} / ${htmlEscape(generationStatusText(item))}</span>
      <div class="request-row-actions">
        ${generationActionHtml(item)}
      </div>
    </div>
  `).join('')
}

function renderPage(request) {
  const username = currentUser(request)
  const assetCards = renderAssetCards()
  const myGenerationRequests = generationRows(currentGenerationRequests(username))
  const mine = feedbackRows(currentRoundFeedback(username))
  return renderShell(`
    <div class="shell">
      <aside>
        <div class="portal-logo">
          <img src="/brand/winnsen-logo.jpg" alt="Winnsen" />
        </div>
        <h2>16029 审核系统</h2>
        <p>登录后只看两条生成入口：完整装配体和单个模型。内部数据默认收起。</p>
        <div class="nav-links">
          <a class="button secondary" href="#generate">模型任务</a>
          <a class="button secondary" href="#downloads">下载</a>
          <a class="button secondary" href="#feedback">反馈</a>
        </div>
        <div class="side-card">
          <span>当前轮次</span>
          <strong>16029 800W gold-variable</strong>
        </div>
        <div class="side-card">
          <span>登录账号</span>
          <strong>${htmlEscape(username)}</strong>
          <a class="button secondary" href="/logout" style="margin-top:10px">退出</a>
        </div>
      </aside>
      <main>
        <section class="panel">
          <div class="top">
            <div>
              <p class="eyebrow">ENGINEER REVIEW</p>
              <h1>16029 800W 审核入口</h1>
              <p class="muted">自动识别完整装配体或单个模型；提交后进入后台生成，完成后才显示下载。右侧只保留实时预览，不展示内部日志。</p>
              <div class="chips">
                <span class="chip good">${htmlEscape(reviewRound.cadMainline)}</span>
                <span class="chip warn">工程审核</span>
                <span class="chip">2 条入口</span>
              </div>
            </div>
            <a class="button" href="#generate">开始生成</a>
          </div>
          <div class="quick-status">
            <div>
              <span>入口</span>
              <strong>完整装配体 / 单个模型</strong>
            </div>
            <div>
              <span>生成状态</span>
              <strong>提交后显示进度条</strong>
            </div>
            <div>
              <span>下载</span>
              <strong>完成后出现</strong>
            </div>
          </div>
        </section>

        <section id="generate" class="panel">
          <div class="top">
            <div>
              <h2>模型任务</h2>
              <p class="muted">默认自动判断生成完整装配体还是单个模型。需要改时，直接点下面两张卡。</p>
            </div>
            <span class="chip warn">审核页</span>
          </div>
          <div class="studio-grid">
            <form id="generationForm" class="generator-form">
              <label>自然语言/参数化提示词
                <textarea name="prompt" id="doorPrompt">800W x 1917H x 550D, two columns, LMS row units 6/12, 4/12, 2/12, W337, ordinary locker doors, electric lock, concealed hinge, latch, reinforcement rib, lock holes and hinge holes.</textarea>
              </label>
              <input type="hidden" name="taskMode" id="taskMode" value="full_assembly" />
              <div class="mode-grid" role="tablist" aria-label="生成模式选择">
                <button class="mode-card active" type="button" data-task-mode="full_assembly">
                  <strong>生成完整装配体</strong>
                  <span>用于整柜结构审核。提示词里写门数、列数、W/H/D 即可。</span>
                </button>
                <button class="mode-card" type="button" data-task-mode="single_model">
                  <strong>生成单个模型</strong>
                  <span>用于单门板 / 单柜门件审核。适合先把一个门做对。</span>
                </button>
              </div>
              <div class="prompt-actions">
                <button class="button secondary" type="button" data-example="800W x 1917H x 550D, two columns, LMS row units 6/12, 4/12, 2/12, W337, electric lock, concealed hinge, reinforcement rib.">整柜样例</button>
                <button class="button secondary" type="button" data-example="300W x 1917H x 550D, single locker door panel, ordinary_door_panel, electric lock, concealed hinge, lock holes.">单门样例</button>
              </div>
              <details class="advanced-fields">
                <summary>高级参数</summary>
                <div class="field-grid">
                  <label>柜宽 W(mm)<input name="cabinetWidth" id="cabinetWidth" value="800" /></label>
                  <label>柜高 H(mm)<input name="cabinetHeight" id="cabinetHeight" value="1917" /></label>
                  <label>柜深 D(mm)<input name="cabinetDepth" id="cabinetDepth" value="550" /></label>
                  <label>列数<input name="columns" id="columns" value="2" /></label>
                  <label>门数<input name="doorCount" id="doorCount" value="6" /></label>
                  <label>门宽(mm)<input name="doorWidth" id="doorWidth" value="337" /></label>
                  <label>门高(mm)<input name="doorHeight" id="doorHeight" value="908" /></label>
                  <label>门高序列<input name="rowSequence" id="rowSequence" value="6/12, 4/12, 2/12" /></label>
                  <label>板厚<input name="thickness" id="thickness" value="待确认" /></label>
                </div>
                <div class="field-grid two">
                  <label>门型<input name="doorType" id="doorType" value="ordinary_door_panel" /></label>
                  <label>锁具<input name="lockType" id="lockType" value="电控锁" /></label>
                  <label>铰链<input name="hingeType" id="hingeType" value="暗铰链" /></label>
                  <label>插销/锁扣<input name="latchType" id="latchType" value="插销/锁扣需确认" /></label>
                  <label>加强筋<input name="reinforcement" id="reinforcement" value="加强筋" /></label>
                  <label>开孔<input name="openings" id="openings" value="锁孔/铰链孔需确认" /></label>
                  <label>材料<input name="material" id="material" value="待确认" /></label>
                </div>
                <div id="generatedPrompt" class="generated-prompt" hidden></div>
              </details>
              <button type="submit">生成模型任务</button>
              <div class="progress-wrap" aria-live="polite">
                <div class="progress-track"><span id="generationProgressBar"></span></div>
                <div id="generationProgressLabel" class="progress-label">等待提交</div>
              </div>
              <div id="generationStatus"></div>
            </form>

            <div>
              <div class="preview-panel">
                <div class="preview-head">
                  <div>
                    <span>3D 实时预览窗口</span>
                    <strong>柜门布局预览</strong>
                  </div>
                  <span class="chip">CAD 视口</span>
                </div>
                <div class="viewport-toolbar">
                  <div class="view-button-row" aria-label="3D viewport view controls">
                    <button class="button secondary view-active" type="button" data-view="iso">ISO</button>
                    <button class="button secondary" type="button" data-view="front">前视</button>
                    <button class="button secondary" type="button" data-view="right">右视</button>
                    <button class="button secondary" type="button" data-view="top">俯视</button>
                  </div>
                  <span class="chip good">Orbit / Zoom / Grid</span>
                </div>
                <div class="preview-canvas-wrap">
                  <canvas id="cabinetPreview" width="760" height="560"></canvas>
                  <div class="viewport-hud">
                    <div><span>外形</span><strong id="hudCabinetSize">-</strong></div>
                    <div><span>门件</span><strong id="hudDoorSize">-</strong></div>
                    <div><span>布局</span><strong id="hudDoorLayout">-</strong></div>
                    <div><span>五金</span><strong id="hudHardware">-</strong></div>
                  </div>
                </div>
                <div class="preview-controls">
                  <label>旋转</label>
                  <input id="previewRotation" type="range" min="-70" max="70" value="-28" />
                  <span id="rotationLabel" class="muted">-28°</span>
                  <label>缩放</label>
                  <input id="previewZoom" type="range" min="70" max="150" value="100" />
                  <span id="zoomLabel" class="muted">100%</span>
                </div>
                <p class="preview-note">这是浏览器内的参数化预览：可切 ISO/前视/右视/俯视、拖动旋转、缩放。正式 CAD 结果仍由后台生成后提供下载。</p>
              </div>
              <div class="panel" style="margin-top:12px">
                <h2>我的生成任务</h2>
                <div class="request-list" id="generationRequestList">${myGenerationRequests}</div>
              </div>
            </div>
          </div>
        </section>

        <section id="downloads" class="panel">
          <div class="top">
            <div>
              <h2>审核包下载</h2>
              <p class="muted">只保留当前三包下载入口，不展开内部 gate 细节。</p>
            </div>
            <span class="chip good">受保护下载</span>
          </div>
          <div class="asset-grid">${assetCards}</div>
        </section>

        <section id="feedback" class="panel" style="margin-top:16px">
          <h2>提交审核意见</h2>
          <p class="muted">通过、需修改、无法判断都可以提交。截图会随反馈归档在本机 review_feedback 目录。</p>
          <form id="feedbackForm" class="feedback-grid">
            <div>
              <label>审核人姓名<input name="reviewerName" value="${htmlEscape(username)}" required /></label>
              <label>专业/角色<input name="discipline" placeholder="结构 / 工艺 / 项目" /></label>
              <label>审核对象
                <select name="reviewTarget">
                  ${assets.map((asset) => `<option value="${asset.id}">${htmlEscape(asset.title)}</option>`).join('')}
                </select>
              </label>
              <label>结论
                <select name="decision">
                  <option value="pass">通过</option>
                  <option value="needs_changes">需修改</option>
                  <option value="blocked">阻塞</option>
                  <option value="cannot_judge">无法判断</option>
                </select>
              </label>
            </div>
            <div>
              <label>问题摘要<textarea name="summary" required placeholder="例如：SML 中门锁孔偏移 / DUAL 包可正常打开"></textarea></label>
              <label>截图/附件<input name="attachments" type="file" multiple /></label>
              <button type="submit">提交反馈</button>
              <div id="feedbackStatus"></div>
            </div>
          </form>
        </section>
        <section class="panel">
          <h2>我的反馈</h2>
          <div id="feedbackList">${mine}</div>
        </section>
      </main>
    </div>
    <script>
      const promptInput = document.getElementById('doorPrompt')
      const generationForm = document.getElementById('generationForm')
      const generatedPromptBox = document.getElementById('generatedPrompt')
      const generationStatus = document.getElementById('generationStatus')
      const taskModeInput = document.getElementById('taskMode')
      const taskModeCards = Array.from(document.querySelectorAll('[data-task-mode]'))
      const generationProgressBar = document.getElementById('generationProgressBar')
      const generationProgressLabel = document.getElementById('generationProgressLabel')
      const generationRequestList = document.getElementById('generationRequestList')
      const rotationInput = document.getElementById('previewRotation')
      const rotationLabel = document.getElementById('rotationLabel')
      const zoomInput = document.getElementById('previewZoom')
      const zoomLabel = document.getElementById('zoomLabel')
      const previewCanvas = document.getElementById('cabinetPreview')
      const previewContext = previewCanvas.getContext('2d')
      const hudCabinetSize = document.getElementById('hudCabinetSize')
      const hudDoorSize = document.getElementById('hudDoorSize')
      const hudDoorLayout = document.getElementById('hudDoorLayout')
      const hudHardware = document.getElementById('hudHardware')
      const fieldIds = ['cabinetWidth', 'cabinetHeight', 'cabinetDepth', 'columns', 'doorCount', 'doorWidth', 'doorHeight', 'rowSequence', 'doorType', 'lockType', 'hingeType', 'latchType', 'reinforcement', 'openings', 'material', 'thickness']
      const fields = Object.fromEntries(fieldIds.map((id) => [id, document.getElementById(id)]))
      let previewTiltDeg = -12
      let isDraggingPreview = false
      let dragStartX = 0
      let dragStartRotation = Number(rotationInput.value)
      let isSyncingFields = false
      let manualTaskMode = false
      let lastAutoDoorWidth = fields.doorWidth.value
      let lastAutoDoorHeight = fields.doorHeight.value
      setGenerationProgress(0, '等待提交')

      function setGenerationProgress(percent, label) {
        generationProgressBar.style.width = Math.max(0, Math.min(100, percent)) + '%'
        generationProgressLabel.textContent = label
      }

      function generationStatusTextForClient(item) {
        const status = String(item.status || '')
        if (item.downloadUrl) return item.resultKind === 'cad_worker_payload_package' ? '任务包已生成' : '已生成'
        if (status.includes('failed')) return '生成失败'
        if (status.includes('running')) return '后台生成中'
        return '等待后台生成'
      }

      function generationActionForClient(item) {
        const wrapper = document.createElement('div')
        wrapper.className = 'request-row-actions'
        if (item.downloadUrl) {
          const link = document.createElement('a')
          link.className = 'button'
          link.href = item.downloadUrl
          link.textContent = item.resultKind === 'cad_worker_payload_package' ? '下载任务包' : '下载结果'
          wrapper.append(link)
          return wrapper
        }
        const button = document.createElement('button')
        button.type = 'button'
        button.disabled = true
        const status = String(item.status || '')
        button.textContent = status.includes('failed') ? '生成失败' : status.includes('running') ? '生成中' : '等待生成'
        wrapper.append(button)
        return wrapper
      }

      function generationRequestRow(item) {
        const modeLabel = item.taskMode === 'single_model' ? '单个模型' : '完整装配体'
        const row = document.createElement('div')
        row.className = 'request-row'
        const title = document.createElement('strong')
        title.textContent = modeLabel + ' / ' + (item.cabinetWidth || '-') + 'W x ' + (item.cabinetHeight || '-') + 'H x ' + (item.cabinetDepth || '-') + 'D'
        const dims = document.createElement('span')
        dims.textContent = (item.doorCount || '-') + ' 门 / 门板 ' + (item.doorWidth || '-') + 'W x ' + (item.doorHeight || '-') + 'H'
        const status = document.createElement('span')
        const createdAt = item.createdAt ? new Date(item.createdAt).toLocaleString('zh-CN', { hour12: false }) : '刚刚提交'
        status.textContent = createdAt + ' / ' + generationStatusTextForClient(item)
        row.append(title, dims, status, generationActionForClient(item))
        return row
      }

      async function refreshGenerationRequestList() {
        if (!generationRequestList) return []
        const response = await fetch('/generation-requests.json')
        if (!response.ok) return []
        const data = await response.json()
        const requests = Array.isArray(data.requests) ? data.requests : []
        generationRequestList.replaceChildren(...requests.map(generationRequestRow))
        return requests
      }

      let generationPollTimer = null
      function startGenerationPolling() {
        if (!generationRequestList || generationPollTimer) return
        generationPollTimer = setInterval(async () => {
          const requests = await refreshGenerationRequestList()
          const hasPending = requests.some((item) => !item.downloadUrl && !String(item.status || '').includes('failed'))
          if (!hasPending) {
            clearInterval(generationPollTimer)
            generationPollTimer = null
          }
        }, 2000)
      }

      function inferTaskMode(text) {
        const value = String(text || '').toLowerCase()
        if (/单个|单件|单门|门板|柜门|single|panel|door\\s*panel|one\\s*door/.test(value)) return 'single_model'
        if (/完整|整柜|装配体|assembly|cabinet|locker/.test(value)) return 'full_assembly'
        return 'full_assembly'
      }

      function setTaskMode(mode, isManual = false) {
        const normalized = mode === 'single_model' ? 'single_model' : 'full_assembly'
        if (isManual) manualTaskMode = true
        taskModeInput.value = normalized
        taskModeCards.forEach((card) => card.classList.toggle('active', card.getAttribute('data-task-mode') === normalized))
        if (normalized === 'single_model') {
          fields.columns.value = '1'
          fields.doorCount.value = '1'
          fields.rowSequence.value = 'single panel'
          lastAutoDoorWidth = String(Math.round(numberValue('cabinetWidth', 800)))
          lastAutoDoorHeight = String(Math.round(numberValue('cabinetHeight', 1917)))
          fields.doorWidth.value = lastAutoDoorWidth
          fields.doorHeight.value = lastAutoDoorHeight
        } else if (fields.columns.value === '1') {
          fields.columns.value = '2'
          const rowUnits = explicitRowUnits(fields.rowSequence.value)
          fields.doorCount.value = String(rowUnits.length ? rowUnits.length * 2 : 6)
          if (fields.rowSequence.value === 'single panel') fields.rowSequence.value = '6/12, 4/12, 2/12'
        }
      }

      function numberValue(id, fallback) {
        const value = Number(fields[id].value)
        return Number.isFinite(value) && value > 0 ? value : fallback
      }

      function firstMatch(text, patterns) {
        for (const pattern of patterns) {
          const match = pattern.exec(text)
          if (match && match[1]) return Number(match[1])
        }
        return null
      }

      function parseDimensions(text) {
        const match = text.match(/(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*[x×*]\\s*(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*[x×*]\\s*(\\d+(?:\\.\\d+)?)/i)
        return match ? { width: Number(match[1]), height: Number(match[2]), depth: Number(match[3]) } : null
      }

      function parseRowSequence(text) {
        if (/LMS/i.test(text)) return '6/12, 4/12, 2/12'
        if (/SML/i.test(text)) return '2/12, 4/12, 6/12'
        const explicit = text.match(/(?:门高序列|高度序列|row\\s*units?)\\s*[:：=]\\s*([^。；;]+)/i)
        if (explicit && explicit[1]) return explicit[1].trim()
        const fractions = Array.from(text.matchAll(/([1-6])\\s*\\/\\s*12/g), (match) => match[1] + '/12')
        return fractions.length ? Array.from(new Set(fractions)).join(', ') : 'equal rows'
      }

      function explicitRowUnits(sequence) {
        const units = Array.from(String(sequence || '').matchAll(/([1-6])\\s*\\/\\s*12/g), (match) => Number(match[1]))
        return units
      }

      function rowUnitsFromSequence(sequence, doorCount, columns) {
        const units = explicitRowUnits(sequence)
        if (units.length) return units
        const rows = Math.max(1, Math.ceil(doorCount / columns))
        return Array.from({ length: rows }, () => 1)
      }

      function estimateDoorWidthFor(cabinetWidth, columns) {
        if (Math.abs(cabinetWidth - 800) < 1 && columns === 2) return 337
        if (Math.abs(cabinetWidth - 1000) < 1 && columns === 2) return 437
        return Math.max(80, Math.round((cabinetWidth - 126) / columns))
      }

      function estimateDoorHeightFor(cabinetHeight, doorCount, columns, rowSequence) {
        const explicitUnits = explicitRowUnits(rowSequence)
        const units = rowUnitsFromSequence(rowSequence, doorCount, columns)
        const rows = Math.max(1, units.length || Math.ceil(doorCount / columns))
        if (explicitUnits.length) return Math.max(80, Math.round(explicitUnits[0] * 152.5 - 7))
        const bodyHeight = Math.min(cabinetHeight - 90, 1827)
        return Math.max(80, Math.round((bodyHeight - (rows - 1) * 7) / rows))
      }

      function syncDependentFields(sourceId) {
        if (isSyncingFields) return
        isSyncingFields = true
        const W = numberValue('cabinetWidth', 800)
        const H = numberValue('cabinetHeight', 1917)
        const columns = Math.max(1, Math.min(6, Math.round(numberValue('columns', 2))))
        const rowUnits = explicitRowUnits(fields.rowSequence.value)
        const sequenceDefinesRows = rowUnits.length > 0
        if (sourceId === 'columns' && fields.columns.value !== String(columns)) fields.columns.value = String(columns)
        if (sourceId === 'rowSequence' && sequenceDefinesRows) fields.doorCount.value = String(rowUnits.length * columns)
        const doorCount = Math.max(columns, Math.round(numberValue('doorCount', rowUnits.length * columns || 6)))
        const shouldUpdateDoorWidth = ['cabinetWidth', 'columns'].includes(sourceId) || fields.doorWidth.value === lastAutoDoorWidth
        const shouldUpdateDoorHeight = ['cabinetHeight', 'doorCount', 'columns', 'rowSequence'].includes(sourceId) || fields.doorHeight.value === lastAutoDoorHeight
        if (shouldUpdateDoorWidth) {
          lastAutoDoorWidth = String(estimateDoorWidthFor(W, columns))
          fields.doorWidth.value = lastAutoDoorWidth
        }
        if (shouldUpdateDoorHeight) {
          lastAutoDoorHeight = String(estimateDoorHeightFor(H, doorCount, columns, fields.rowSequence.value))
          fields.doorHeight.value = lastAutoDoorHeight
        }
        updateGeneratedPrompt()
        drawPreview()
        isSyncingFields = false
      }

      function applyPromptToFields() {
        const text = promptInput.value || ''
        const lower = text.toLowerCase()
        const taskMode = manualTaskMode ? taskModeInput.value : inferTaskMode(text)
        if (!manualTaskMode) setTaskMode(taskMode)
        const dimensions = parseDimensions(text)
        const cabinetWidth = firstMatch(text, [/外形宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:w|W|宽)/]) || (dimensions && dimensions.width) || 800
        const cabinetHeight = firstMatch(text, [/外形高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:h|H|高)/]) || (dimensions && dimensions.height) || 1917
        const cabinetDepth = firstMatch(text, [/外形深\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜深\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:d|D|深)/]) || (dimensions && dimensions.depth) || 550
        const isSingleModel = taskMode === 'single_model'
        const autoColumns = isSingleModel ? 1 : 2
        const columns = Math.max(1, Math.min(6, Math.round(firstMatch(text, [/(\\d+)\\s*(?:列|columns?|cols?)/i]) || autoColumns)))
        const rowSequence = isSingleModel ? 'single panel' : parseRowSequence(text)
        const sequenceRows = isSingleModel ? [1] : rowUnitsFromSequence(rowSequence, 12, columns)
        const explicitDoorCount = firstMatch(text, [/门数\\s*[:：=]?\\s*(\\d+)/i, /door\\s*count\\s*[:=]?\\s*(\\d+)/i, /(\\d+)\\s*门(?:柜|整柜|布局|方案)/])
        const doorCount = isSingleModel ? 1 : Math.max(columns, Math.round(explicitDoorCount || sequenceRows.length * columns || 12))
        const explicitDoorWidth = firstMatch(text, [/门宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /door\\s*width\\s*[:=]?\\s*(\\d+(?:\\.\\d+)?)/i, /W\\s*(\\d+(?:\\.\\d+)?)/])
        const explicitDoorHeight = firstMatch(text, [/门高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /door\\s*height\\s*[:=]?\\s*(\\d+(?:\\.\\d+)?)/i, /H\\s*(\\d+(?:\\.\\d+)?)/])
        const estimatedDoorWidth = isSingleModel ? cabinetWidth : estimateDoorWidthFor(cabinetWidth, columns)
        const estimatedDoorHeight = isSingleModel ? cabinetHeight : estimateDoorHeightFor(cabinetHeight, doorCount, columns, rowSequence)
        fields.cabinetWidth.value = String(Math.round(cabinetWidth))
        fields.cabinetHeight.value = String(Math.round(cabinetHeight))
        fields.cabinetDepth.value = String(Math.round(cabinetDepth))
        fields.columns.value = String(columns)
        fields.doorCount.value = String(doorCount)
        fields.doorWidth.value = String(Math.round(explicitDoorWidth || estimatedDoorWidth))
        fields.doorHeight.value = String(Math.round(explicitDoorHeight || estimatedDoorHeight))
        lastAutoDoorWidth = String(Math.round(explicitDoorWidth || estimatedDoorWidth))
        lastAutoDoorHeight = String(Math.round(explicitDoorHeight || estimatedDoorHeight))
        fields.rowSequence.value = rowSequence
        fields.doorType.value = lower.includes('control') || text.includes('中控') ? 'control_door' : 'ordinary_door_panel'
        fields.lockType.value = lower.includes('electric') || text.includes('电控') ? '电控锁' : text.includes('机械') ? '机械锁' : '待确认'
        fields.hingeType.value = lower.includes('concealed') || text.includes('暗铰') ? '暗铰链' : lower.includes('piano') || text.includes('长铰') ? '长铰链' : '待确认'
        fields.latchType.value = lower.includes('latch') || text.includes('插销') ? '插销/锁扣需确认' : '待确认'
        fields.reinforcement.value = lower.includes('rib') || text.includes('加强') ? '加强筋' : '待确认'
        fields.openings.value = lower.includes('hole') || text.includes('孔') ? '锁孔/铰链孔需确认' : '待确认'
        fields.material.value = lower.includes('galvanized') || text.includes('镀锌') ? '镀锌板' : text.includes('不锈钢') ? '不锈钢' : '待确认'
        const thickness = firstMatch(text, [/(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:厚|thickness|板厚)/i])
        fields.thickness.value = thickness ? thickness + ' mm' : '待确认'
        updateGeneratedPrompt()
        drawPreview()
      }

      function updateGeneratedPrompt() {
        generatedPromptBox.textContent = [
          '柜体外形 ' + fields.cabinetWidth.value + 'W x ' + fields.cabinetHeight.value + 'H x ' + fields.cabinetDepth.value + 'D',
          fields.columns.value + ' 列 / ' + fields.doorCount.value + ' 门',
          '单门板 ' + fields.doorWidth.value + 'W x ' + fields.doorHeight.value + 'H',
          '门高序列 ' + fields.rowSequence.value,
          fields.doorType.value + ', ' + fields.lockType.value + ', ' + fields.hingeType.value + ', ' + fields.latchType.value,
          fields.reinforcement.value + ', ' + fields.openings.value + ', ' + fields.material.value + ', ' + fields.thickness.value,
        ].join('；')
      }

      function projectPoint(point, angleDeg) {
        const angle = angleDeg * Math.PI / 180
        const tilt = previewTiltDeg * Math.PI / 180
        const cos = Math.cos(angle)
        const sin = Math.sin(angle)
        let x = point.x * cos - point.z * sin
        let z = point.x * sin + point.z * cos
        let y = point.y * Math.cos(tilt) - z * Math.sin(tilt)
        z = point.y * Math.sin(tilt) + z * Math.cos(tilt)
        const perspective = 760 / (760 + z)
        return { x: previewCanvas.width / 2 + x * perspective, y: previewCanvas.height / 2 - y * perspective, depth: z }
      }

      function facePath(face, angleDeg) {
        const projected = face.points.map((point) => projectPoint(point, angleDeg))
        return { projected, depth: projected.reduce((sum, point) => sum + point.depth, 0) / projected.length, color: face.color, stroke: face.stroke }
      }

      function addBox(faces, cx, cy, cz, w, h, d, colors, stroke) {
        const x0 = cx - w / 2, x1 = cx + w / 2
        const y0 = cy - h / 2, y1 = cy + h / 2
        const z0 = cz - d / 2, z1 = cz + d / 2
        faces.push({ color: colors.front, stroke, points: [{ x:x0, y:y0, z:z1 }, { x:x1, y:y0, z:z1 }, { x:x1, y:y1, z:z1 }, { x:x0, y:y1, z:z1 }] })
        faces.push({ color: colors.right, stroke, points: [{ x:x1, y:y0, z:z1 }, { x:x1, y:y0, z:z0 }, { x:x1, y:y1, z:z0 }, { x:x1, y:y1, z:z1 }] })
        faces.push({ color: colors.left, stroke, points: [{ x:x0, y:y0, z:z0 }, { x:x0, y:y0, z:z1 }, { x:x0, y:y1, z:z1 }, { x:x0, y:y1, z:z0 }] })
        faces.push({ color: colors.top, stroke, points: [{ x:x0, y:y1, z:z1 }, { x:x1, y:y1, z:z1 }, { x:x1, y:y1, z:z0 }, { x:x0, y:y1, z:z0 }] })
        faces.push({ color: colors.bottom, stroke, points: [{ x:x0, y:y0, z:z0 }, { x:x1, y:y0, z:z0 }, { x:x1, y:y0, z:z1 }, { x:x0, y:y0, z:z1 }] })
      }

      function drawMarker(x, y, z, radius, color, angleDeg) {
        const point = projectPoint({ x, y, z }, angleDeg)
        previewContext.beginPath()
        previewContext.arc(point.x, point.y, radius, 0, Math.PI * 2)
        previewContext.fillStyle = color
        previewContext.fill()
      }

      function drawLine3d(points, angleDeg, color, width, dash) {
        const projected = points.map((point) => projectPoint(point, angleDeg))
        previewContext.save()
        previewContext.beginPath()
        projected.forEach((point, index) => index ? previewContext.lineTo(point.x, point.y) : previewContext.moveTo(point.x, point.y))
        previewContext.strokeStyle = color
        previewContext.lineWidth = width || 1
        previewContext.setLineDash(dash || [])
        previewContext.stroke()
        previewContext.restore()
      }

      function drawTextAt(point, text, angleDeg, color, align) {
        const projected = projectPoint(point, angleDeg)
        previewContext.save()
        previewContext.fillStyle = color || '#28476d'
        previewContext.font = '12px Arial, sans-serif'
        previewContext.textAlign = align || 'center'
        previewContext.textBaseline = 'middle'
        previewContext.fillText(text, projected.x, projected.y)
        previewContext.restore()
      }

      function drawViewportGrid(angleDeg, modelW, modelH, modelD, scale) {
        const y = -modelH / 2 - 20 * scale
        const extentX = modelW * .85
        const extentZ = modelD * 1.15
        const step = Math.max(28, Math.min(modelW, modelD) / 7)
        for (let x = -extentX; x <= extentX + 1; x += step) {
          drawLine3d([{ x, y, z:-extentZ }, { x, y, z:extentZ }], angleDeg, 'rgba(132,154,184,.32)', 1)
        }
        for (let z = -extentZ; z <= extentZ + 1; z += step) {
          drawLine3d([{ x:-extentX, y, z }, { x:extentX, y, z }], angleDeg, 'rgba(132,154,184,.32)', 1)
        }
        const origin = { x:-modelW / 2 - 42 * scale, y, z:modelD / 2 + 52 * scale }
        drawLine3d([origin, { x:origin.x + 90 * scale, y:origin.y, z:origin.z }], angleDeg, '#c8322a', 2)
        drawLine3d([origin, { x:origin.x, y:origin.y + 90 * scale, z:origin.z }], angleDeg, '#17884f', 2)
        drawLine3d([origin, { x:origin.x, y:origin.y, z:origin.z - 90 * scale }], angleDeg, '#1f64b5', 2)
        drawTextAt({ x:origin.x + 105 * scale, y:origin.y, z:origin.z }, 'X', angleDeg, '#c8322a')
        drawTextAt({ x:origin.x, y:origin.y + 105 * scale, z:origin.z }, 'Y', angleDeg, '#17884f')
        drawTextAt({ x:origin.x, y:origin.y, z:origin.z - 105 * scale }, 'Z', angleDeg, '#1f64b5')
      }

      function drawBoxWireframe(angleDeg, w, h, d, color) {
        const x0 = -w / 2, x1 = w / 2
        const y0 = -h / 2, y1 = h / 2
        const z0 = -d / 2, z1 = d / 2
        const edges = [
          [{ x:x0, y:y0, z:z0 }, { x:x1, y:y0, z:z0 }],
          [{ x:x1, y:y0, z:z0 }, { x:x1, y:y1, z:z0 }],
          [{ x:x1, y:y1, z:z0 }, { x:x0, y:y1, z:z0 }],
          [{ x:x0, y:y1, z:z0 }, { x:x0, y:y0, z:z0 }],
          [{ x:x0, y:y0, z:z1 }, { x:x1, y:y0, z:z1 }],
          [{ x:x1, y:y0, z:z1 }, { x:x1, y:y1, z:z1 }],
          [{ x:x1, y:y1, z:z1 }, { x:x0, y:y1, z:z1 }],
          [{ x:x0, y:y1, z:z1 }, { x:x0, y:y0, z:z1 }],
          [{ x:x0, y:y0, z:z0 }, { x:x0, y:y0, z:z1 }],
          [{ x:x1, y:y0, z:z0 }, { x:x1, y:y0, z:z1 }],
          [{ x:x1, y:y1, z:z0 }, { x:x1, y:y1, z:z1 }],
          [{ x:x0, y:y1, z:z0 }, { x:x0, y:y1, z:z1 }],
        ]
        edges.forEach((edge) => drawLine3d(edge, angleDeg, color || 'rgba(31,53,133,.55)', 1.3))
      }

      function drawPreview() {
        const ctx = previewContext
        const angle = Number(rotationInput.value)
        const zoom = Math.max(.7, Math.min(1.5, Number(zoomInput.value) / 100 || 1))
        rotationLabel.textContent = Math.round(angle) + '°'
        zoomLabel.textContent = Math.round(zoom * 100) + '%'
        ctx.clearRect(0, 0, previewCanvas.width, previewCanvas.height)
        const gradient = ctx.createLinearGradient(0, 0, 0, previewCanvas.height)
        gradient.addColorStop(0, '#eaf1f8')
        gradient.addColorStop(.55, '#f7fafd')
        gradient.addColorStop(1, '#ffffff')
        ctx.fillStyle = gradient
        ctx.fillRect(0, 0, previewCanvas.width, previewCanvas.height)
        const W = numberValue('cabinetWidth', 800)
        const H = numberValue('cabinetHeight', 1917)
        const D = numberValue('cabinetDepth', 550)
        const columns = Math.max(1, Math.round(numberValue('columns', 2)))
        const doorCount = Math.max(columns, Math.round(numberValue('doorCount', 6)))
        const units = rowUnitsFromSequence(fields.rowSequence.value, doorCount, columns)
        const rows = units.length || Math.ceil(doorCount / columns)
        const scale = Math.min(360 / W, 430 / H, 260 / D) * zoom
        const modelW = W * scale
        const modelH = H * scale
        const modelD = D * scale
        const faces = []
        drawViewportGrid(angle, modelW, modelH, modelD, scale)
        addBox(faces, 0, 0, 0, modelW, modelH, modelD, { front:'#dfe8f4', right:'#cbd9ea', left:'#e8eef6', top:'#f4f7fb', bottom:'#b8c8db' }, '#8ba0bc')
        const frontZ = modelD / 2 + 7
        const gap = 7 * scale
        const doorW = Math.min(modelW / columns - gap * 2, numberValue('doorWidth', W / columns) * scale)
        const totalUnits = units.reduce((sum, unit) => sum + unit, 0)
        let yTop = modelH / 2 - 44 * scale
        for (let row = 0; row < rows; row += 1) {
          const unit = units[row] || 1
          const rowH = units.length ? (modelH - 88 * scale - gap * (rows - 1)) * unit / totalUnits : (modelH - 88 * scale - gap * (rows - 1)) / rows
          const cy = yTop - rowH / 2
          for (let col = 0; col < columns; col += 1) {
            const cx = -modelW / 2 + (col + .5) * (modelW / columns)
            addBox(faces, cx, cy, frontZ, doorW, rowH, 8, { front:'#ffffff', right:'#dbe4ef', left:'#f6f8fb', top:'#ffffff', bottom:'#d6e1ee' }, '#1f3585')
          }
          yTop -= rowH + gap
        }
        faces.map((face) => facePath(face, angle)).sort((a, b) => a.depth - b.depth).forEach((face) => {
          ctx.beginPath()
          face.projected.forEach((point, index) => index ? ctx.lineTo(point.x, point.y) : ctx.moveTo(point.x, point.y))
          ctx.closePath()
          ctx.fillStyle = face.color
          ctx.fill()
          ctx.strokeStyle = face.stroke
          ctx.lineWidth = 1
          ctx.stroke()
        })
        drawBoxWireframe(angle, modelW, modelH, modelD, 'rgba(31,53,133,.58)')
        yTop = modelH / 2 - 44 * scale
        for (let row = 0; row < rows; row += 1) {
          const unit = units[row] || 1
          const rowH = units.length ? (modelH - 88 * scale - gap * (rows - 1)) * unit / totalUnits : (modelH - 88 * scale - gap * (rows - 1)) / rows
          const cy = yTop - rowH / 2
          for (let col = 0; col < columns; col += 1) {
            const cx = -modelW / 2 + (col + .5) * (modelW / columns)
            drawMarker(cx + doorW / 2 - 12, cy, frontZ + 8, 4, '#f04a12', angle)
            drawMarker(cx - doorW / 2 + 10, cy + rowH / 2 - 18, frontZ + 8, 3, '#67758a', angle)
            drawMarker(cx - doorW / 2 + 10, cy - rowH / 2 + 18, frontZ + 8, 3, '#67758a', angle)
          }
          yTop -= rowH + gap
        }
        drawTextAt({ x:0, y:-modelH / 2 - 42 * scale, z:modelD / 2 + 30 * scale }, W + 'W', angle, '#28476d')
        drawTextAt({ x:modelW / 2 + 45 * scale, y:0, z:modelD / 2 + 10 * scale }, H + 'H', angle, '#28476d')
        drawTextAt({ x:modelW / 2 + 20 * scale, y:-modelH / 2 - 24 * scale, z:0 }, D + 'D', angle, '#28476d')
        hudCabinetSize.textContent = W + 'W x ' + H + 'H x ' + D + 'D'
        hudDoorSize.textContent = fields.doorWidth.value + 'W x ' + fields.doorHeight.value + 'H'
        hudDoorLayout.textContent = columns + '列 / ' + doorCount + '门 / ' + fields.rowSequence.value
        hudHardware.textContent = fields.lockType.value + ' / ' + fields.hingeType.value
      }

      document.querySelectorAll('[data-example]').forEach((button) => {
        button.addEventListener('click', () => {
          promptInput.value = button.getAttribute('data-example') || ''
          applyPromptToFields()
        })
      })
      promptInput.addEventListener('input', applyPromptToFields)
      taskModeCards.forEach((card) => {
        card.addEventListener('click', () => {
          setTaskMode(card.getAttribute('data-task-mode'), true)
          updateGeneratedPrompt()
          drawPreview()
        })
      })
      Object.values(fields).forEach((input) => input.addEventListener('input', () => syncDependentFields(input.id)))
      rotationInput.addEventListener('input', drawPreview)
      zoomInput.addEventListener('input', drawPreview)
      document.querySelectorAll('[data-view]').forEach((button) => {
        button.addEventListener('click', () => {
          document.querySelectorAll('[data-view]').forEach((item) => item.classList.remove('view-active'))
          button.classList.add('view-active')
          const view = button.getAttribute('data-view')
          const viewState = {
            iso: { rotation:-28, tilt:-12 },
            front: { rotation:0, tilt:0 },
            right: { rotation:70, tilt:0 },
            top: { rotation:-35, tilt:-65 },
          }[view] || { rotation:-28, tilt:-12 }
          previewTiltDeg = viewState.tilt
          rotationInput.value = String(viewState.rotation)
          drawPreview()
        })
      })
      previewCanvas.addEventListener('pointerdown', (event) => {
        isDraggingPreview = true
        dragStartX = event.clientX
        dragStartRotation = Number(rotationInput.value)
        previewCanvas.setPointerCapture(event.pointerId)
      })
      previewCanvas.addEventListener('pointermove', (event) => {
        if (!isDraggingPreview) return
        rotationInput.value = String(Math.max(-70, Math.min(70, dragStartRotation + (event.clientX - dragStartX) / 4)))
        drawPreview()
      })
      previewCanvas.addEventListener('pointerup', () => { isDraggingPreview = false })
      previewCanvas.addEventListener('pointercancel', () => { isDraggingPreview = false })
      previewCanvas.addEventListener('wheel', (event) => {
        event.preventDefault()
        const nextZoom = Math.max(70, Math.min(150, Number(zoomInput.value) + (event.deltaY > 0 ? -5 : 5)))
        zoomInput.value = String(nextZoom)
        drawPreview()
      }, { passive:false })
      generationForm.addEventListener('submit', async (event) => {
        event.preventDefault()
        generationStatus.className = ''
        generationStatus.textContent = '正在提交生成任务...'
        setGenerationProgress(18, '已接收任务草稿')
        const payload = Object.fromEntries(new FormData(generationForm).entries())
        setGenerationProgress(42, payload.taskMode === 'single_model' ? '单个模型排队中' : '完整装配体排队中')
        const response = await fetch('/generation-request', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        })
        const result = await response.json()
        if (!response.ok) {
          generationStatus.className = 'alert'
          generationStatus.textContent = result.error || '保存失败'
          setGenerationProgress(0, '提交失败')
          return
        }
        generationStatus.className = 'ok'
        setGenerationProgress(100, '任务已提交，等待后台生成')
        generationStatus.textContent = '任务已提交：' + result.requestId + '。后台生成完成后，任务卡上的下载按钮会启用。'
        if (generationRequestList) {
          await refreshGenerationRequestList()
          startGenerationPolling()
        }
      })
      applyPromptToFields()
      startGenerationPolling()

      const form = document.getElementById('feedbackForm')
      const statusBox = document.getElementById('feedbackStatus')
      function fileToEntry(file) {
        return new Promise((resolve, reject) => {
          const reader = new FileReader()
          reader.onload = () => resolve({ name: file.name, type: file.type || 'application/octet-stream', dataUrl: reader.result })
          reader.onerror = reject
          reader.readAsDataURL(file)
        })
      }
      form.addEventListener('submit', async (event) => {
        event.preventDefault()
        statusBox.className = ''
        statusBox.textContent = '提交中...'
        const data = new FormData(form)
        const files = Array.from(form.elements.attachments.files || [])
        const attachments = await Promise.all(files.map(fileToEntry))
        const response = await fetch('/feedback', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            reviewerName: data.get('reviewerName'),
            discipline: data.get('discipline'),
            reviewTarget: data.get('reviewTarget'),
            decision: data.get('decision'),
            summary: data.get('summary'),
            attachments,
          }),
        })
        const result = await response.json()
        if (!response.ok) {
          statusBox.className = 'alert'
          statusBox.textContent = result.error || '提交失败'
          return
        }
        statusBox.className = 'ok'
        statusBox.textContent = '已提交：' + result.feedbackId
        setTimeout(() => location.reload(), 800)
      })
    </script>
  `, username)
}

async function parseForm(request) {
  const body = await readBody(request, 128 * 1024)
  return new URLSearchParams(body.toString('utf8'))
}

function readInviteCode() {
  try {
    return readFileSync(INVITE_CODE_PATH, 'utf8').trim()
  } catch {
    return ''
  }
}

function safeSlug(value) {
  return String(value || 'user').replace(/[^a-zA-Z0-9_-]/g, '_').slice(0, 48)
}

function dataUrlToBuffer(dataUrl) {
  const match = String(dataUrl || '').match(/^data:([^;,]+)?;base64,(.*)$/)
  if (!match) throw new Error('invalid attachment data')
  return { mime: match[1] || 'application/octet-stream', buffer: Buffer.from(match[2], 'base64') }
}

function submitFeedback(username, payload) {
  const reviewTarget = String(payload.reviewTarget || '').trim()
  const asset = byId.get(reviewTarget)
  if (!asset) throw new Error('invalid review target')
  const decision = String(payload.decision || '').trim()
  const decisionLabels = { pass: '通过', needs_changes: '需修改', blocked: '阻塞', cannot_judge: '无法判断' }
  if (!decisionLabels[decision]) throw new Error('invalid decision')

  const submittedAt = new Date().toISOString()
  const feedbackId = `${submittedAt.replace(/[-:.]/g, '').slice(0, 15)}-${randomBytes(3).toString('hex')}`
  const folderName = `${submittedAt.replace(/[-:.]/g, '').slice(0, 15)}-${safeSlug(username)}-${feedbackId.slice(-6)}`
  const folderPath = resolve(FEEDBACK_DIR, folderName)
  mkdirSync(folderPath, { recursive: true })

  const attachments = []
  for (const [index, attachment] of (payload.attachments || []).entries()) {
    const { mime, buffer } = dataUrlToBuffer(attachment.dataUrl)
    if (buffer.length > MAX_ATTACHMENT_BYTES) throw new Error('attachment too large')
    const safeName = safeSlug(attachment.name || `attachment-${index + 1}`)
    const outName = `${index + 1}-${safeName}`
    writeFileSync(resolve(folderPath, outName), buffer)
    attachments.push({ name: attachment.name || outName, savedAs: outName, mime, bytes: buffer.length })
  }

  const feedback = {
    id: feedbackId,
    submittedAt,
    username,
    reviewerName: String(payload.reviewerName || username).trim(),
    discipline: String(payload.discipline || '').trim(),
    decision,
    decisionLabel: decisionLabels[decision],
    summary: String(payload.summary || '').trim(),
    reviewRound,
    reviewRoundId: reviewRound.id,
    reviewTarget,
    reviewTargetLabel: asset.title,
    attachments,
  }
  writeJson(resolve(folderPath, 'feedback.json'), feedback)

  const index = readFeedbackIndex()
  const summary = {
    id: feedback.id,
    submittedAt: feedback.submittedAt,
    username: feedback.username,
    reviewerName: feedback.reviewerName,
    discipline: feedback.discipline,
    decision: feedback.decision,
    decisionLabel: feedback.decisionLabel,
    summary: feedback.summary,
    reviewRoundId: feedback.reviewRoundId,
    reviewTarget: feedback.reviewTarget,
    reviewTargetLabel: feedback.reviewTargetLabel,
    folderName,
    attachmentCount: attachments.length,
  }
  index.feedback = [summary, ...index.feedback.filter((item) => item.id !== summary.id)].slice(0, 300)
  writeJson(FEEDBACK_INDEX_PATH, index)
  return feedback
}

function localAddresses() {
  const addresses = new Set(['127.0.0.1'])
  for (const infos of Object.values(networkInterfaces())) {
    for (const info of infos || []) {
      if (info.family === 'IPv4' && !info.internal) addresses.add(info.address)
    }
  }
  return [...addresses]
}

ensureDirs()

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url || '/', `http://${request.headers.host || '127.0.0.1'}`)
    if ((request.method === 'GET' || request.method === 'HEAD') && url.pathname === '/favicon.ico') {
      response.writeHead(204, { 'Cache-Control': 'public, max-age=86400' })
      response.end()
      return
    }
    if (request.method === 'GET' && url.pathname === '/brand/winnsen-logo.jpg') {
      if (!existsSync(BRAND_LOGO_PATH)) {
        response.writeHead(404)
        response.end()
        return
      }
      response.writeHead(200, { 'Content-Type': 'image/jpeg', 'Cache-Control': 'public, max-age=3600' })
      createReadStream(BRAND_LOGO_PATH).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/brand/winnsen-mark.png') {
      if (!existsSync(BRAND_MARK_PATH)) {
        response.writeHead(404)
        response.end()
        return
      }
      response.writeHead(200, { 'Content-Type': 'image/png', 'Cache-Control': 'public, max-age=3600' })
      createReadStream(BRAND_MARK_PATH).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/status.json') {
      sendJson(response, 200, { status: 'ok', auth: 'enabled', reviewRound: reviewRound.id, assets: assets.map(assetInfo) })
      return
    }
    if (request.method === 'GET' && url.pathname === '/login') {
      sendHtml(response, 200, renderAuthPage('', 'login'))
      return
    }
    if (request.method === 'GET' && url.pathname === '/register') {
      sendHtml(response, 200, renderAuthPage('', 'register'))
      return
    }
    if (request.method === 'POST' && url.pathname === '/login') {
      const params = await parseForm(request)
      const username = normalizeUsername(params.get('username'))
      const password = params.get('password') || ''
      const user = readUsers().users.find((item) => item.username === username)
      if (!user || !verifyPassword(user, password)) {
        sendHtml(response, 401, renderAuthPage('账号或密码不正确。', 'login'))
        return
      }
      createSession(response, username)
      redirect(response, '/')
      return
    }
    if (request.method === 'POST' && url.pathname === '/register') {
      const params = await parseForm(request)
      const username = normalizeUsername(params.get('username'))
      const password = String(params.get('password') || '')
      const inviteCode = String(params.get('inviteCode') || '').trim()
      if (!username || password.length < 6) {
        sendHtml(response, 400, renderAuthPage('账号不能为空，密码至少 6 位。', 'register'))
        return
      }
      if (readInviteCode() && inviteCode !== readInviteCode()) {
        sendHtml(response, 403, renderAuthPage('邀请码不正确。', 'register'))
        return
      }
      const db = readUsers()
      if (db.users.some((item) => item.username === username)) {
        sendHtml(response, 409, renderAuthPage('账号已存在，请直接登录。', 'register'))
        return
      }
      db.users.push(createUser(username, password))
      saveUsers(db)
      createSession(response, username)
      redirect(response, '/')
      return
    }
    if (request.method === 'GET' && url.pathname === '/logout') {
      response.setHeader('Set-Cookie', `${SESSION_COOKIE}=; HttpOnly; Path=/; Max-Age=0; SameSite=Lax`)
      redirect(response, '/login')
      return
    }

    const username = currentUser(request)
    if (!username) {
      if (request.method === 'GET') redirect(response, '/login')
      else sendJson(response, 401, { error: 'login required' })
      return
    }

    if (request.method === 'GET' && url.pathname === '/') {
      sendHtml(response, 200, renderPage(request))
      return
    }
    if (request.method === 'GET' && url.pathname === '/assets.json') {
      sendJson(response, 200, { reviewRound, assets: assets.map(assetInfo).map(({ path, ...asset }) => ({ ...asset, downloadUrl: `/download/${asset.id}` })) })
      return
    }
    if (request.method === 'GET' && url.pathname === '/feedback.json') {
      sendJson(response, 200, { feedback: currentRoundFeedback(username) })
      return
    }
    if (request.method === 'GET' && url.pathname === '/generation-requests.json') {
      sendJson(response, 200, { requests: currentGenerationRequests(username) })
      return
    }
    const generationDownloadMatch = url.pathname.match(/^\/generation-download\/([a-zA-Z0-9_.-]+)$/)
    if (request.method === 'GET' && generationDownloadMatch) {
      const requestId = generationDownloadMatch[1]
      const item = readGenerationIndex().requests.find((entry) => entry.id === requestId && entry.username === username)
      if (!item || !item.zipPath || !item.downloadUrl) {
        sendJson(response, 404, { error: 'generation output not found' })
        return
      }
      const zipPath = resolve(String(item.zipPath))
      const allowed = [GENERATION_OUTPUT_ROOT, GENERATION_LOG_ROOT].some((root) => isUnderRoot(zipPath, root))
      if (!allowed || !existsSync(zipPath)) {
        sendJson(response, 404, { error: 'generation output zip is missing' })
        return
      }
      const stat = statSync(zipPath)
      const downloadKind = item.resultKind === 'cad_worker_payload_package'
        ? 'cad-worker-package'
        : item.resultKind === 'solidworks2020_full_assembly_model'
          ? 'solidworks2020-full-assembly'
          : 'solidworks2020-sheetmetal-model'
      response.writeHead(200, {
        'Content-Type': 'application/zip',
        'Content-Length': stat.size,
        'Content-Disposition': `attachment; filename="${requestId}-${downloadKind}.zip"`,
        'Cache-Control': 'no-store',
      })
      createReadStream(zipPath).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/team-feedback.json') {
      sendJson(response, 200, { feedback: currentRoundFeedback() })
      return
    }
    if (request.method === 'POST' && url.pathname === '/generation-request') {
      if (!String(request.headers['content-type'] || '').includes('application/json')) {
        sendJson(response, 415, { error: 'generation request expects application/json' })
        return
      }
      const payload = await readJsonBody(request, 512 * 1024)
      const requestRecord = submitGenerationRequest(username, payload)
      startGenerationQueueWorker(requestRecord.id)
      sendJson(response, 200, { ok: true, requestId: requestRecord.id, status: requestRecord.status, taskType: requestRecord.taskType })
      return
    }
    if (request.method === 'POST' && url.pathname === '/feedback') {
      if (!String(request.headers['content-type'] || '').includes('application/json')) {
        sendJson(response, 415, { error: 'feedback expects application/json' })
        return
      }
      const payload = await readJsonBody(request)
      const feedback = submitFeedback(username, payload)
      sendJson(response, 200, { ok: true, feedbackId: feedback.id, savedFiles: feedback.attachments.length })
      return
    }
    const downloadMatch = url.pathname.match(/^\/download\/([a-z0-9-]+)$/)
    if (request.method === 'GET' && downloadMatch) {
      const asset = byId.get(downloadMatch[1])
      if (!asset || !existsSync(asset.path)) {
        sendJson(response, 404, { error: 'asset not found' })
        return
      }
      const stat = statSync(asset.path)
      response.writeHead(200, {
        'Content-Type': 'application/zip',
        'Content-Length': stat.size,
        'Content-Disposition': `attachment; filename="${asset.fileName}"`,
        'Cache-Control': 'no-store',
      })
      createReadStream(asset.path).pipe(response)
      return
    }
    sendJson(response, 404, { error: 'not found' })
  } catch (error) {
    const statusCode = Number.isInteger(error.statusCode) ? error.statusCode : 500
    sendJson(response, statusCode, { error: error.message || 'server error' })
  }
})

server.listen(PORT, HOST, () => {
  console.log(`16029 review login server listening on ${HOST}:${PORT}`)
  for (const address of localAddresses()) console.log(`- http://${address}:${PORT}/`)
})
