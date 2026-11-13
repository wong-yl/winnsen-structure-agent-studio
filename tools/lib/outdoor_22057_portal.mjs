import { createReadStream, existsSync, readFileSync, statSync, writeFileSync, mkdirSync } from 'node:fs'
import { resolve } from 'node:path'
import { createHash, randomUUID } from 'node:crypto'
import { listOutdoorTasks, readOutdoorTask, updateOutdoorTask, outdoorSpecification } from './outdoor_22057_tasks.mjs'
import { outdoorLayoutMarkup, outdoorLayoutScript, outdoorLayoutStyles } from './outdoor_22057_layout.mjs'

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
      state: task.state || 'legacy_fixed', error: task.state === 'failed' ? '本次任务已停止，请联系维护人核对任务记录。' : null,
      downloadUrl: ready ? `/outdoor-22057/task/${task.id}/download` : null }
  }
  const page = username => {
    const g = gate()
    const ranges = g.generation?.ranges_mm || { height: [1939,1939], width: [1000,1000], depth: [550,550] }
    const defaults = g.generation?.default_specification || { height: 1939, width: 1000, depth: 550 }
    const editableLayout = g.generation?.layout?.enabled === true
    const fields = ['height', 'width', 'depth'].map((key, i) => `<label for="${key}">${['柜高','柜宽','柜深'][i]} / mm（${ranges[key][0]}–${ranges[key][1]}）</label><input id="${key}" name="${key}" type="number" min="${ranges[key][0]}" max="${ranges[key][1]}" step="${editableLayout ? "0.001" : "0.1"}" value="${defaults[key]}" required>`).join('')
    return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>22057 户外柜 · 工程辅助模型</title><style>
*{box-sizing:border-box}body{margin:0;background:#f3f5f7;color:#172336;font:15px/1.6 "Microsoft YaHei",sans-serif}header{background:#122b48;color:white;padding:18px 5vw;display:flex;justify-content:space-between;align-items:center}header a{color:white}main{max-width:1120px;margin:32px auto;padding:0 22px}.badge{display:inline-block;background:#e7eff7;border-radius:20px;padding:4px 12px;font-size:12px;color:#1a4f80}h1{font-size:28px;margin:10px 0 6px}h2{font-size:18px;margin-top:0}.intro{color:#526176}.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin-top:24px}.card{background:white;border:1px solid #dde3ea;border-radius:12px;padding:24px}.spec{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;padding:16px 0}.spec b{display:block;font-size:23px;color:#174d7a}.spec span{font-size:12px;color:#6a788a}input,textarea,button{font:inherit}input,textarea{width:100%;padding:10px;border:1px solid #c3cedc;border-radius:6px;margin:5px 0 14px}button,.download{background:#1e5c91;color:white;border:0;border-radius:6px;padding:10px 18px;cursor:pointer;display:inline-block;text-decoration:none}button:disabled{background:#8a96a5;cursor:default}.notice{background:#fff7e8;border:1px solid #edce95;padding:12px;border-radius:7px;color:#6b511d;font-size:13px}.task{padding:18px 0;border-bottom:1px solid #e1e7ee}.task:last-child{border-bottom:0}.task small{display:block;color:#647386;overflow-wrap:anywhere}.feedback{margin-top:12px}.preview{width:100%;height:280px;object-fit:contain;background:#edf1f5;border-radius:8px}#message{min-height:26px;color:#174d7a;margin-top:12px}@media(max-width:700px){.grid{grid-template-columns:1fr}h1{font-size:23px}header{padding:16px 22px}.card{padding:18px}}
${editableLayout ? outdoorLayoutStyles : ""}</style></head><body><header><strong>WINNSEN · 结构工程模型</strong><span><a href="/">室内柜</a> · ${htmlEscape(username)} · <a href="/logout">退出</a></span></header><main><span class="badge">${staging ? '发布前本机验证' : '工程辅助模型'}</span><h1>22057 户外快递副柜</h1><p class="intro">${editableLayout ? "填写柜体尺寸，分别编辑左右列的门数与门高，核对布局后生成整机模型。" : "双列14门，填写柜高、柜宽、柜深，系统生成整机并自动核对，通过后提供原生模型下载。"}</p><div class="grid"><section class="card"><h2>标准规格</h2><img class="preview" src="/outdoor-22057/preview" alt="已审核短销方案的原生总装参考图"><div class="spec"><div><b>${defaults.height}</b><span>柜高 / mm</span></div><div><b>${defaults.width}</b><span>柜宽 / mm</span></div><div><b>${defaults.depth}</b><span>柜深 / mm</span></div></div><p>${editableLayout ? "左右列独立门序" : "双列14门"} · SOLIDWORKS 2020 · 底部短销</p>${editableLayout ? '<svg id="layout-preview" class="layout-drawing" viewBox="0 0 420 350" role="img" aria-label="当前左右列门板布局预览"></svg>' : ""}<div class="notice">用于结构工程师生成和修改基础模型。交付包包含原生文件、STEP预览及本次核对记录；实际规格以任务为准。</div></section><section class="card"><h2>创建模型任务</h2><form id="request-form"><label for="reference">项目或订单备注</label><input id="reference" name="reference" maxlength="100" required placeholder="填写便于识别的项目备注">${fields}${editableLayout ? outdoorLayoutMarkup() : '<input type="hidden" name="doors" value="14">'}<button id="submit" type="submit" ${g.generation?.enabled ? '' : 'disabled'}>生成本次规格模型</button></form><p id="message" role="status"></p><div class="notice" id="gate-note">${htmlEscape(g.generation?.enabled ? (editableLayout ? '任务按顺序生成，完成后在下方下载。门序从下向上，每列须闭合；当前范围见输入框。' : '任务按顺序生成，完成后在下方下载。请保持双列14门布局；当前开放范围见输入框。') : '变尺寸任务暂未开放，历史固定规格包仍可下载。')}</div></section></div><section class="card" style="margin-top:24px"><h2>我的任务与反馈</h2><div id="tasks">正在读取任务…</div></section></main><script>
const esc=value=>String(value??'').replace(/[&<>"']/g,char=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
async function refresh(){const response=await fetch('/outdoor-22057/tasks.json');if(!response.ok)throw Error('任务读取失败');const data=await response.json();if(document.activeElement?.closest('.feedback'))return;const drafts=new Map(Array.from(document.querySelectorAll('.feedback')).map(form=>[form.dataset.id,form.querySelector('textarea').value]));document.getElementById('tasks').innerHTML=data.tasks.length?data.tasks.map(task=>'<div class="task"><strong>'+esc(task.reference)+'</strong><small>'+esc(task.id)+' · '+esc(task.status)+'</small><p>'+esc(task.specification.height)+'高 × '+esc(task.specification.width)+'宽 × '+esc(task.specification.depth)+'深 mm · '+esc(task.specification.doors || 14)+'门</p>'+(task.downloadUrl?'<a class="download" href="'+esc(task.downloadUrl)+'">下载本次模型</a>':'<p>'+esc(task.error || '生成及核对完成后提供下载。')+'</p>')+'<form class="feedback" data-id="'+esc(task.id)+'"><label>模型问题或应用反馈</label><textarea name="message" maxlength="2000" required rows="2"></textarea><button>保存反馈</button><span role="status"></span></form></div>').join(''):'暂无任务。';document.querySelectorAll('.feedback').forEach(form=>{form.querySelector('textarea').value=drafts.get(form.dataset.id)||'';});}
document.getElementById('request-form').addEventListener('submit',async event=>{event.preventDefault();if(event.target.dataset.layoutValid==='false'){document.getElementById('message').textContent='请先检查左右列门高与门缝闭合。';return;}const button=document.getElementById('submit');button.disabled=true;try{const response=await fetch('/outdoor-22057/task',{method:'POST',body:new URLSearchParams(new FormData(event.target))});const result=await response.json();if(!response.ok)throw Error(result.error);document.getElementById('message').textContent='任务已进入队列，可在下方查看进度：'+result.task.id;await refresh();}catch(error){document.getElementById('message').textContent=error.message;}finally{button.disabled=event.target.dataset.layoutValid==='false';}});
document.getElementById('tasks').addEventListener('submit',async event=>{event.preventDefault();const form=event.target;const button=form.querySelector('button');button.disabled=true;try{const response=await fetch('/outdoor-22057/task/'+form.dataset.id+'/feedback',{method:'POST',body:new URLSearchParams(new FormData(form))});const result=await response.json();if(!response.ok)throw Error(result.error);form.querySelector('span').textContent=' 反馈已保存';}catch(error){form.querySelector('span').textContent=error.message;}finally{button.disabled=false;}});refresh().catch(error=>document.getElementById('tasks').textContent=error.message);setInterval(()=>refresh().catch(()=>{}),10000);
${editableLayout ? outdoorLayoutScript(g.generation?.enabled) : ""}</script></body></html>`
  }
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
