export function outdoorStudioClient({ generationReady }) {
  const $ = id => document.getElementById(id), form = $('request-form')
  const esc = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[char]))
  const date = value => { const parsed = new Date(value); return Number.isFinite(parsed.getTime()) ? parsed.toLocaleString('zh-CN', { hour12: false }) : '时间未知' }
  let tasks = [], filter = 'all', confirmed = null, submitting = false, refreshing = false, savingFeedback = false
  const dimensionsValid = () => ['width','height','depth'].every(id => $(id).checkValidity())
  const payload = () => new URLSearchParams(new FormData(form)).toString()
  function navigate(name, scroll = true) {
    if (!['new-model','my-models','model-feedback'].includes(name)) name = 'new-model'
    document.querySelectorAll('[data-workspace-view]').forEach(view => { const active = view.dataset.workspaceView === name; view.classList.toggle('active', active); view.hidden = !active })
    document.querySelectorAll('[data-workspace-nav]').forEach(link => { const active = link.dataset.workspaceNav === name; link.classList.toggle('active', active); active ? link.setAttribute('aria-current','page') : link.removeAttribute('aria-current') })
    if (location.hash !== '#' + name) history.replaceState(null, '', '#' + name)
    if (scroll) window.scrollTo({ top: 0, behavior: 'instant' })
  }
  document.querySelectorAll('[data-workspace-nav]').forEach(link => link.addEventListener('click', event => { event.preventDefault(); navigate(link.dataset.workspaceNav) }))
  window.addEventListener('hashchange', () => navigate(location.hash.slice(1)))
  function invalidate() { confirmed = null; $('generationConfirm').hidden = true; sync() }
  function sync() {
    const valid = form.dataset.layoutValid === 'true' && dimensionsValid()
    $('submit').disabled = !valid || submitting
    $('confirmGenerationSubmit').disabled = !generationReady || !valid || submitting || !confirmed
    const invalidDimension = ['width','height','depth'].find(id => !$(id).checkValidity())
    $('dimension-status').hidden = !invalidDimension
    if (invalidDimension) $('dimension-status').textContent = ({width:'柜宽',height:'柜高',depth:'柜深'})[invalidDimension] + '须在' + $(invalidDimension).min + '–' + $(invalidDimension).max + ' mm内，最多保留三位小数。'
    const columns = JSON.parse($('doorColumnsPayload').value || '[]')
    $('totalDoorCount').value = columns.reduce((sum, column) => sum + column.doors.length, 0)
    $('parameter-summary').textContent = '22057 · ' + $('width').value + 'W × ' + $('height').value + 'H × ' + $('depth').value + 'D mm\n左 ' + (columns[0]?.doors.length || 0) + ' 门 / 右 ' + (columns[1]?.doors.length || 0) + ' 门 · 门序从下向上 · 门缝 6 mm'
  }
  form.addEventListener('input', invalidate)
  document.addEventListener('outdoor-layout-change', invalidate)
  form.addEventListener('submit', event => {
    event.preventDefault()
    if (!form.reportValidity() || form.dataset.layoutValid !== 'true') return
    confirmed = payload()
    const columns = JSON.parse($('doorColumnsPayload').value)
    const rows = [ ['任务名称', $('reference').value, true], ['柜体尺寸 / W × H × D', $('width').value + ' × ' + $('height').value + ' × ' + $('depth').value + ' mm', true], ['左列门高 / 从下向上', columns[0].doors.map(door => door.heightMm).join(' / ') + ' mm', true], ['右列门高 / 从下向上', columns[1].doors.map(door => door.heightMm).join(' / ') + ' mm', true] ]
    $('generationConfirmSummary').innerHTML = rows.map(([label,value,wide]) => '<div' + (wide ? ' class="wide"' : '') + '><dt>' + esc(label) + '</dt><dd>' + esc(value) + '</dd></div>').join('')
    $('generationConfirm').hidden = false; sync(); $('generationConfirm').scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  })
  $('returnToSpecButton').addEventListener('click', () => { invalidate(); $('reference').focus() })
  $('confirmGenerationSubmit').addEventListener('click', async () => {
    if (!generationReady || submitting || !confirmed) return
    if (payload() !== confirmed || !form.reportValidity() || form.dataset.layoutValid !== 'true') { invalidate(); $('message').textContent = '参数已变化，请重新核对。'; return }
    submitting = true; sync()
    try {
      const response = await fetch('/outdoor-22057/task', { method:'POST', body: new URLSearchParams(confirmed) }), result = await response.json()
      if (!response.ok) throw Error(result.error || '任务提交失败')
      $('message').textContent = '任务已进入队列：' + result.task.id; confirmed = null; $('generationConfirm').hidden = true
      await refresh(); navigate('my-models')
    } catch (error) { $('message').textContent = error.message }
    finally { submitting = false; sync() }
  })
  function renderTasks() {
    const query = $('taskFilterInput').value.trim().toLowerCase()
    const visible = tasks.filter(task => {
      const state = task.state === 'legacy_fixed' && task.downloadUrl ? 'ready' : task.state
      return (filter === 'all' || state === filter) && [task.reference,task.id,task.specification.width,task.specification.height,task.specification.depth,task.specification.doors].join(' ').toLowerCase().includes(query)
    })
    $('tasks').innerHTML = visible.length ? visible.map(task => {
      const spec = task.specification, state = task.downloadUrl ? 'ready' : task.state
      const columns = spec.columns?.map(column => (column.side === 'L' ? '左 ' : '右 ') + column.doors.length + ' 门').join(' / ')
      return '<article class="task-row" data-state="' + esc(state) + '"><div class="task-row-main"><div class="task-row-title"><strong>' + esc(task.reference) + '</strong><span class="task-status ' + esc(state) + '">' + esc(task.status) + '</span></div><p class="task-row-meta">' + esc(task.id) + ' · ' + esc(date(task.createdAt)) + '</p><div class="task-dimensions"><span>' + esc(spec.width) + 'W × ' + esc(spec.height) + 'H × ' + esc(spec.depth) + 'D mm</span><span>' + esc(columns || '双列') + ' · 共 ' + esc(spec.doors || 14) + ' 门</span></div><p class="task-row-note" data-error="' + Boolean(task.error) + '">' + esc(task.error || (task.downloadUrl ? '本次模型已通过检查，可下载继续深化。' : '生成和检查完成后开放下载。')) + '</p><div class="task-row-actions">' + (task.downloadUrl ? '<a class="button" href="' + esc(task.downloadUrl) + '">下载本次模型</a>' : '') + '<button type="button" class="secondary" data-feedback-task="' + esc(task.id) + '">反馈 / ' + (task.feedback?.length || 0) + '</button></div></div></article>'
    }).join('') : '<div class="task-empty"><strong>' + (tasks.length ? '没有匹配的任务' : '还没有模型任务') + '</strong><span>' + (tasks.length ? '试试其他关键词或状态。' : '完成规格定义后，生成记录会出现在这里。') + '</span></div>'
  }
  function renderFeedbackContext() {
    const task = tasks.find(item => item.id === $('feedbackTask').value)
    $('saveFeedback').disabled = !task || savingFeedback
    $('feedbackTaskContext').textContent = task ? task.specification.width + 'W × ' + task.specification.height + 'H × ' + task.specification.depth + 'D mm · ' + task.id : '提交模型任务后，可在这里绑定反馈。'
    $('feedbackHistory').innerHTML = task?.feedback?.length ? '<h3>已保存反馈</h3>' + [...task.feedback].reverse().map(item => '<article class="feedback-row"><small>' + esc(date(item.createdAt)) + '</small><p>' + esc(item.message) + '</p></article>').join('') : ''
  }
  function renderFeedbackTasks() {
    const selected = $('feedbackTask').value
    $('feedbackTask').innerHTML = tasks.length ? tasks.map(task => '<option value="' + esc(task.id) + '">' + esc(task.reference) + ' · ' + esc(task.specification.width) + 'W / ' + esc(task.specification.doors || 14) + '门</option>').join('') : '<option value="">暂无可反馈的任务</option>'
    if (tasks.some(task => task.id === selected)) $('feedbackTask').value = selected
    renderFeedbackContext()
  }
  async function refresh() {
    if (refreshing) return
    refreshing = true; $('refreshTasks').disabled = true
    try {
      const response = await fetch('/outdoor-22057/tasks.json', { cache: 'no-store' })
      if (!response.ok) throw Error('任务读取失败，请稍后重试')
      const result = await response.json(); tasks = result.tasks; renderTasks(); renderFeedbackTasks()
      $('taskRefreshStatus').textContent = '共 ' + tasks.length + ' 个任务 · 每10秒更新 · 最近 ' + new Date().toLocaleTimeString('zh-CN', { hour12: false })
    } catch (error) { $('taskRefreshStatus').textContent = error.message }
    finally { refreshing = false; $('refreshTasks').disabled = false }
  }
  $('tasks').addEventListener('click', event => { const button = event.target.closest('[data-feedback-task]'); if (!button) return; $('feedbackTask').value = button.dataset.feedbackTask; renderFeedbackContext(); navigate('model-feedback'); $('feedbackMessage').focus() })
  $('feedbackTask').addEventListener('change', () => { $('feedbackStatus').textContent = ''; renderFeedbackContext() })
  $('taskFilterInput').addEventListener('input', renderTasks)
  document.querySelectorAll('[data-task-filter]').forEach(button => button.addEventListener('click', () => { filter = button.dataset.taskFilter; document.querySelectorAll('[data-task-filter]').forEach(item => { item.classList.toggle('active',item === button); item.setAttribute('aria-pressed',String(item === button)) }); renderTasks() }))
  $('refreshTasks').addEventListener('click', refresh)
  $('feedback-form').addEventListener('submit', async event => {
    event.preventDefault(); const id = $('feedbackTask').value; if (savingFeedback || !tasks.some(task => task.id === id)) return
    savingFeedback = true; $('saveFeedback').disabled = true
    try {
      const response = await fetch('/outdoor-22057/task/' + id + '/feedback', { method:'POST', body:new URLSearchParams({ message:$('feedbackMessage').value }) }), result = await response.json()
      if (!response.ok) throw Error(result.error || '反馈保存失败')
      $('feedbackMessage').value = ''; $('feedbackStatus').textContent = '反馈已保存，并绑定本次模型任务。'; await refresh()
    } catch (error) { $('feedbackStatus').textContent = error.message }
    finally { savingFeedback = false; $('saveFeedback').disabled = !tasks.some(task => task.id === $('feedbackTask').value) }
  })
  sync(); navigate(location.hash.slice(1), false); refresh(); setInterval(refresh, 10000)
}
