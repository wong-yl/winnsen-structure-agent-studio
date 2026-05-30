import { createReadStream, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:http'
import { networkInterfaces } from 'node:os'
import { resolve } from 'node:path'
import { pbkdf2Sync, randomBytes, timingSafeEqual } from 'node:crypto'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const PORT = Number(process.env.STUDIO_REVIEW_PORT || 5180)
const HOST = process.env.STUDIO_REVIEW_HOST || '0.0.0.0'
const DATA_DIR = resolve(ROOT, 'data')
const FEEDBACK_DIR = resolve(DATA_DIR, 'review_feedback')
const USER_DB_PATH = resolve(DATA_DIR, 'review_download_users.json')
const FEEDBACK_INDEX_PATH = resolve(FEEDBACK_DIR, 'feedback_index.json')
const INVITE_CODE_PATH = resolve(DATA_DIR, 'review_download_invite_code.txt')
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

function renderAuthPage(error = '', mode = 'login') {
  const isRegister = mode === 'register'
  return renderShell(`
    <main class="auth-wrap">
      <section class="auth-card">
        <p class="eyebrow">WINNSEN REVIEW PORTAL</p>
        <h1>${isRegister ? '注册审核账号' : '结构审核登录'}</h1>
        <p class="muted">${htmlEscape(reviewRound.title)}，只开放 LMS / SML / DUAL 三个候选审核包。</p>
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
  aside h2 { margin: 0 0 8px; font-size: 18px; }
  aside p { margin: 0 0 20px; color: #d9e1ee; font-size: 13px; line-height: 1.55; }
  .side-card { margin-top: 18px; padding: 14px; border: 1px solid rgba(255,255,255,.18); border-radius: 8px; background: rgba(255,255,255,.07); }
  .side-card strong, .side-card span { display:block; }
  .side-card span { color:#c6d2e4; font-size:12px; }
  main { padding: 28px; }
  .auth-wrap { min-height: 100vh; display:grid; place-items:center; padding:24px; }
  .auth-card, .panel, .asset-card, .feedback-row { border: 1px solid var(--line); border-radius: 8px; background: #fff; box-shadow: 0 12px 32px rgba(20,35,70,.08); }
  .auth-card { width: min(460px, 100%); padding: 24px; }
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
  .chips { display:flex; flex-wrap:wrap; gap:8px; margin-top:14px; }
  .chip { padding:6px 10px; border-radius:999px; background:#eef3ff; border:1px solid #c8d5ee; color:#28476d; font-size:12px; font-weight:700; }
  .chip.good { color:var(--good); border-color:#bfe5cf; background:#edf9f1; }
  .chip.warn { color:var(--warn); border-color:#f5d08d; background:#fff8e8; }
  .gate-warning { margin-top:14px; padding:12px 14px; border:1px solid #f5d08d; border-radius:8px; background:#fff8e8; color:#8a4f00; line-height:1.55; }
  .panel { padding:18px; margin-bottom:16px; }
  .asset-grid { display:grid; grid-template-columns: repeat(3, minmax(0,1fr)); gap:12px; }
  .asset-card { padding:16px; display:grid; gap:12px; }
  .asset-card strong { font-size:17px; }
  .asset-card p { margin:0; color:var(--muted); line-height:1.5; font-size:13px; }
  .meta { display:grid; gap:6px; font-size:12px; color:var(--muted); }
  .meta b { color:var(--ink); word-break:break-all; }
  .feedback-grid { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:16px; }
  .feedback-row { padding:12px; margin-top:10px; }
  .feedback-row p { margin:5px 0 0; color:var(--muted); }
  .alert { padding:10px 12px; margin-top:14px; border-radius:8px; background:#fff5f5; color:var(--risk); border:1px solid #f4c2c2; }
  .ok { padding:10px 12px; margin-top:12px; border-radius:8px; background:#edf9f1; color:var(--good); border:1px solid #bfe5cf; }
  @media (max-width: 900px) { .shell { grid-template-columns:1fr; } aside { position:static; } main { padding:16px; } .asset-grid, .feedback-grid { grid-template-columns:1fr; } .top { flex-direction:column; } }
</style>
</head>
<body>${content}</body>
</html>`
}

function renderAssetCards() {
  return assets.map(assetInfo).map((asset) => `
    <article class="asset-card">
      <div>
        <span class="chip">${htmlEscape(asset.category)}</span>
        <h2>${htmlEscape(asset.title)}</h2>
      </div>
      <p>${htmlEscape(asset.description)}</p>
      <div class="meta">
        <span>文件：<b>${htmlEscape(asset.fileName)}</b></span>
        <span>大小：<b>${asset.available ? formatBytes(asset.sizeBytes) : 'missing'}</b></span>
        <span>更新时间：<b>${asset.modifiedAt ? new Date(asset.modifiedAt).toLocaleString('zh-CN', { hour12: false }) : 'missing'}</b></span>
      </div>
      ${asset.available ? `<a class="button" href="/download/${asset.id}">下载 ${htmlEscape(asset.title)}</a>` : '<button disabled>文件缺失</button>'}
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

function renderPage(request) {
  const username = currentUser(request)
  const host = request.headers.host || `127.0.0.1:${PORT}`
  const assetCards = renderAssetCards()
  const mine = feedbackRows(currentRoundFeedback(username))
  const team = feedbackRows(currentRoundFeedback())
  return renderShell(`
    <div class="shell">
      <aside>
        <h2>16029 审核系统</h2>
        <p>${htmlEscape(reviewRound.instruction)}</p>
        <div class="side-card">
          <span>当前轮次</span>
          <strong>${htmlEscape(reviewRound.title)}</strong>
        </div>
        <div class="side-card">
          <span>访问地址</span>
          <strong>http://${htmlEscape(host)}/</strong>
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
              <h1>${htmlEscape(reviewRound.title)}</h1>
              <p class="muted">请下载 LMS、SML、DUAL 三个 ZIP，重点核对门序、门宽 W337、内部间隙 2+3+2、五金计数、STEP 在 SolidWorks 2020 中的可打开性和 gate 证据。</p>
              <div class="chips">
                <span class="chip good">${htmlEscape(reviewRound.cadMainline)}</span>
                <span class="chip warn">${htmlEscape(reviewRound.gateStatus)}</span>
                <span class="chip">${htmlEscape(reviewRound.boundary)}</span>
                <span class="chip">3 ZIP</span>
              </div>
              <div class="gate-warning">${htmlEscape(reviewRound.gateBlocker)} 当前包可继续用于结构审核反馈，但不要标记为最终验证通过。</div>
            </div>
            <a class="button" href="#feedback">提交审核意见</a>
          </div>
        </section>

        <section class="asset-grid">${assetCards}</section>

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

        <section class="feedback-grid">
          <div class="panel">
            <h2>当前轮次团队反馈</h2>
            ${team}
          </div>
          <div class="panel">
            <h2>我的反馈</h2>
            <div id="feedbackList">${mine}</div>
          </div>
        </section>
      </main>
    </div>
    <script>
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
    if (request.method === 'GET' && url.pathname === '/team-feedback.json') {
      sendJson(response, 200, { feedback: currentRoundFeedback() })
      return
    }
    if (request.method === 'POST' && url.pathname === '/feedback') {
      if (!String(request.headers['content-type'] || '').includes('application/json')) {
        sendJson(response, 415, { error: 'feedback expects application/json' })
        return
      }
      const payload = JSON.parse((await readBody(request)).toString('utf8'))
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
    sendJson(response, 500, { error: error.message || 'server error' })
  }
})

server.listen(PORT, HOST, () => {
  console.log(`16029 review login server listening on ${HOST}:${PORT}`)
  for (const address of localAddresses()) console.log(`- http://${address}:${PORT}/`)
})
