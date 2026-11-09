import { createReadStream, existsSync, readFileSync, statSync, writeFileSync, mkdirSync } from 'node:fs'
import { resolve } from 'node:path'
import { createHash, randomUUID } from 'node:crypto'
import { listOutdoorTasks, readOutdoorTask, updateOutdoorTask, outdoorSpecification } from './outdoor_22057_tasks.mjs'
import { renderOutdoorStudio } from './outdoor_22057_studio.mjs'

export function createOutdoor22057Routes({ dataDir, stage, staging = false, sendHtml, sendJson, parseForm, htmlEscape, readJson, writeJson }) {
  const tasksDir = resolve(dataDir, 'outdoor_22057_tasks')
  const gatePath = resolve(stage, 'release_gate.json')
  const gate = () => readJson(gatePath, { packageReady: false, reason: '独立迁移验收尚未完成' })
  const owned = (id, username) => {
    if (!/^OUT22057-[a-f0-9-]{36}$/.test(id)) return null
    const item = readOutdoorTask(dataDir, id)
    return item?.username === username ? item : null
  }
  const publicTask = task => {
    const ready = task.state ? task.state === 'ready' && Boolean(task.artifact) : gate().packageReady
    return { id: task.id, reference: task.reference, createdAt: task.createdAt, specification: task.specification,
      status: task.state ? ({ queued: '排队等待生成', running: '正在生成并核对模型', ready: '核对通过，可下载模型', failed: '生成或验收未通过，下载已停止' }[task.state] || '等待处理') : ready ? '固定规格内测包可下载' : '等待迁移验收',
      feedback: task.feedback || [], state: task.state || 'legacy_fixed', error: task.state === 'failed' ? '本次任务已停止，请联系维护人核对任务记录。' : null,
      downloadUrl: ready ? `/outdoor-22057/task/${task.id}/download` : null }
  }
  const page = username => renderOutdoorStudio({ gate: gate(), username, staging, htmlEscape })
  return async function handle(request, response, url, username) {
    if (request.method === 'GET' && (url.pathname === '/' || url.pathname === '/outdoor-22057')) {
      sendHtml(response, 200, page(username)); return true
    }
    if (!url.pathname.startsWith('/outdoor-22057/')) return false
    if (request.method === 'GET' && url.pathname === '/outdoor-22057/preview') {
      const path = resolve(stage, 'portal/preview.png')
      if (!existsSync(path)) sendJson(response, 404, { error: '预览图尚未准备' })
      else { response.writeHead(200, { 'Content-Type': 'image/png', 'Cache-Control': 'no-store' }); createReadStream(path).pipe(response) }
      return true
    }
    if (request.method === 'GET' && url.pathname === '/outdoor-22057/tasks.json') {
      sendJson(response, 200, { tasks: listOutdoorTasks(dataDir).filter(task => task.username === username).map(publicTask), ranges_mm: gate().generation?.ranges_mm || null }); return true
    }
    if (request.method === 'POST' && url.pathname === '/outdoor-22057/task') {
      const params = await parseForm(request)
      const reference = String(params.get('reference') || '').trim()
      if (!reference || reference.length > 100) { sendJson(response, 400, { error: '请输入100字以内的项目备注' }); return true }
      let specification
      try { specification = outdoorSpecification(params, gate()) }
      catch (error) { sendJson(response, 422, { error: error.message }); return true }
      const task = { id: `OUT22057-${randomUUID()}`, username, reference, createdAt: new Date().toISOString(),
        specification, feedback: [], state: 'queued', artifact: null,
        generator: { ...gate().generation }, production_release: false }
      await updateOutdoorTask(dataDir, task.id, () => task)
      sendJson(response, 201, { task: publicTask(task) }); return true
    }
    const match = url.pathname.match(/^\/outdoor-22057\/task\/(OUT22057-[a-f0-9-]{36})\/(download|feedback)$/)
    if (!match) { sendJson(response, 404, { error: '户外柜入口不存在' }); return true }
    const task = owned(match[1], username)
    if (!task) { sendJson(response, 404, { error: '任务不存在' }); return true }
    if (request.method === 'POST' && match[2] === 'feedback') {
      const params = await parseForm(request); const message = String(params.get('message') || '').trim()
      if (!message || message.length > 2000) { sendJson(response, 400, { error: '反馈内容需要在1至2000字之间' }); return true }
      await updateOutdoorTask(dataDir, task.id, current => ({ ...current, feedback: [...current.feedback, { message, createdAt: new Date().toISOString(), username, artifactSha256: current.artifact?.sha256 || null }] }))
      sendJson(response, 201, { saved: true, taskId: task.id }); return true
    }
    if (request.method === 'GET' && match[2] === 'download') {
      const g = gate()
      if (task.state && (task.state !== 'ready' || !task.artifact)) { sendJson(response, 409, { error: '本次模型尚未通过生成和验收，不能下载' }); return true }
      if (!g.packageReady) { sendJson(response, 409, { error: g.reason || '模型仍在验收中' }); return true }
      const artifact = task.artifact || { archive: g.archive, sha256: g.sha256, bytes: g.bytes }
      const path = resolve(stage, artifact.archive || '')
      if (!path.startsWith(stage + '\\') || !existsSync(path)) { sendJson(response, 409, { error: '交付包尚未就绪' }); return true }
      const data = readFileSync(path)
      if (data.length !== artifact.bytes || createHash('sha256').update(data).digest('hex') !== artifact.sha256) {
        sendJson(response, 409, { error: '交付包完整性校验失败，下载已暂停' }); return true
      }
      if (!task.artifact) await updateOutdoorTask(dataDir, task.id, current => ({ ...current, artifact }))
      writeFileSync(resolve(tasksDir, `${task.id}.download.json`), JSON.stringify({ taskId: task.id, username, sha256: artifact.sha256, bytes: data.length, at: new Date().toISOString() }))
      response.writeHead(200, { 'Content-Type': 'application/zip', 'Content-Length': data.length, 'Content-Disposition': `attachment; filename="WINNSEN-22057-${task.state ? `${task.specification.height}H-${task.specification.width}W-${task.specification.depth}D` : 'fixed-size'}-trial.zip"`, 'Cache-Control': 'no-store' })
      response.end(data); return true
    }
    sendJson(response, 405, { error: '操作方式不支持' }); return true
  }
}
