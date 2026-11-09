import { readFileSync } from 'node:fs'
import { outdoorLayoutMarkup, outdoorLayoutScript } from '../../outdoor_22057_width600_portal_20261018_v16/portal_studio/lib/outdoor_22057_layout.mjs'
import { outdoorStudioClient } from '../../outdoor_22057_width600_portal_20261018_v16/portal_studio/lib/outdoor_22057_studio_client.mjs'
import { outdoorStudioPreview } from './outdoor_22057_studio_preview.mjs'

const styles = readFileSync(new URL('../../outdoor_22057_width600_portal_20261018_v16/portal_studio/lib/indoor_workspace_snapshot.css', import.meta.url), 'utf8')
const refinements = readFileSync(new URL('./outdoor_22057_studio.css', import.meta.url), 'utf8')

const previewStyles = readFileSync(new URL('./preview.css', import.meta.url), 'utf8')

const displayGeometry = JSON.parse(readFileSync(new URL('./canopy_display.json', import.meta.url), 'utf8'))

export function renderOutdoorStudio({ gate, username, htmlEscape }) {
  const generation = gate.generation || {}
  const ranges = generation.ranges_mm || { height: [1939, 1939], width: [1000, 1000], depth: [550, 550] }
  const defaults = generation.default_specification || { height: 1939, width: 1000, depth: 550 }
  const ready = generation.enabled === true && /^[a-f0-9]{64}$/i.test(generation.qualification_sha256 || '')
  const dimension = (key, label, symbol) => `<label for="${key}"><span class="field-label">${label}<small>${symbol} / mm</small></span><input id="${key}" name="${key}" aria-describedby="${key}-range" type="number" min="${ranges[key][0]}" max="${ranges[key][1]}" step="0.001" value="${defaults[key]}" required><span id="${key}-range" class="width-basis-note">${ranges[key][0]}–${ranges[key][1]} mm</span></label>`
  const icon = path => `<svg class="portal-nav-icon" viewBox="0 0 18 18" aria-hidden="true"><path d="${path}" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>`
  return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>22057 户外柜工程工作台 · Winnsen</title><style>${styles}\n${refinements}\n${previewStyles}</style></head><body>
<div class="shell">
  <header class="portal-sidebar" aria-label="门户导航">
    <div class="workspace-brand"><a class="portal-logo" href="/outdoor-22057" aria-label="户外柜工作台"><img src="/brand/winnsen-logo.jpg" alt="Winnsen"></a><div class="workspace-brand-copy"><strong>结构设计工作台</strong><span>22057 / Outdoor workspace</span></div></div>
    <nav class="portal-nav" aria-label="模型助手功能">
      <a href="/">${icon('M3 4h12v12H3zM7 4v12M3 9h12')}<span class="portal-nav-copy"><strong>室内柜</strong></span></a>
      <a href="#new-model" data-workspace-nav="new-model" class="active" aria-current="page">${icon('M3 4h12v12H3zM7 4v12M3 9h12')}<span class="portal-nav-copy"><strong>定义规格</strong></span></a>
      <a href="#my-models" data-workspace-nav="my-models">${icon('M3 5h12v10H3zM6 2h6M6 8h6M6 12h4')}<span class="portal-nav-copy"><strong>任务工作台</strong></span></a>
      <a href="#model-feedback" data-workspace-nav="model-feedback">${icon('M3 3h12v10H8l-4 3v-3H3zM6 6h6M6 9h4')}<span class="portal-nav-copy"><strong>模型反馈</strong></span></a>
    </nav>
    <div class="account-card"><div class="account-avatar" aria-hidden="true">${htmlEscape(username.slice(0, 1).toUpperCase())}</div><div class="account-copy"><strong>${htmlEscape(username)}</strong></div><a href="/logout">退出</a></div>
  </header>
  <main class="workspace-main">
    <div class="workspace-topline"><span><i aria-hidden="true"></i><b>22057</b> / 户外快递副柜</span><span>${ready ? '原生建模 · 逐单校核 · 工程深化' : '新版界面预览 · 扩展范围验证中'}</span></div>
    <div class="workspace-view active" data-workspace-view="new-model">
      <section class="review-overview" aria-labelledby="studio-title">
        <div class="workspace-title-row"><div class="simple-intro"><p class="eyebrow">新建模型 / Specification</p><h1 id="studio-title">从规格，到户外柜模型。</h1><p class="muted">定义外形尺寸，独立编辑左右门列。核对布局后生成整机，通过检查即可下载基础模型继续深化。</p></div><div class="workspace-stamp"><span>22057 / OUTDOOR</span><strong>01 / 03</strong><span class="stamp-title">规格定义 / 布局预览</span></div></div>
        <ol class="workflow-rail" aria-label="参数化任务流程"><li class="active"><b>01</b><span>定义规格</span><small>外形与门数</small></li><li><b>02</b><span>核对布局</span><small>左右列预览</small></li><li><b>03</b><span>生成校核</span><small>原生模型检查</small></li><li><b>04</b><span>下载深化</span><small>通过后开放</small></li><li><b>05</b><span>反馈修订</span><small>绑定任务记录</small></li></ol>
        <div class="engineering-boundary"><strong>${ready ? '适用范围' : '编辑范围'}</strong><span>宽 ${ranges.width.join('–')} mm · 高 ${ranges.height.join('–')} mm · 深 ${ranges.depth.join('–')} mm · 两列独立门序 · 所有门高至少170 mm</span></div>
        ${ready ? '' : '<p class="feedback-boundary generation-capability-pending" role="status">600–1200 mm 扩展范围正在验证。可编辑并核对布局，整机检查完成后开放新版生成。</p>'}
      </section>
      <div class="studio-grid">
        <form id="request-form" class="generator-form">
          <h2 class="generator-title">规格与门板布局</h2>
          <div class="field-grid customer-parameter-grid">
            <label class="wide" for="reference"><span class="field-label">需求代号 / 任务名称<small>REFERENCE</small></span><input id="reference" name="reference" maxlength="100" required placeholder="例如：项目A-户外柜-01"></label>
            ${dimension('width', '柜体宽度', 'W')}
            <label for="totalDoorCount"><span class="field-label">总门数<small>自动汇总</small></span><input id="totalDoorCount" type="number" readonly value="14"><span class="width-basis-note">左右两列分别设置</span></label>
            ${dimension('height', '柜体高度', 'H')}${dimension('depth', '柜体深度', 'D')}
          </div>
          <p id="dimension-status" class="dimension-error" role="status" hidden></p>
          ${outdoorLayoutMarkup()}
          <div id="parameter-summary" class="generated-prompt" aria-label="本次参数摘要"></div>
          <button id="submit" type="submit">核对参数并进入确认<span aria-hidden="true">→</span></button>
          <section id="generationConfirm" class="generation-confirm" hidden aria-labelledby="confirm-title">
            <div class="generation-confirm-head"><div><span>02 / LAYOUT REVIEW</span><strong id="confirm-title">确认本次任务参数</strong></div><span class="chip">待提交</span></div>
            <dl id="generationConfirmSummary" class="generation-confirm-summary"></dl>
            <p class="generation-confirm-boundary">${ready ? '生成任务将保存本次尺寸和左右门序。原生模型通过检查后提供下载。' : '布局可供核对。扩展范围尚未通过整机验证，暂不能提交生成任务。'}</p>
            <div class="generation-confirm-actions"><button class="secondary" id="returnToSpecButton" type="button">返回编辑</button><button id="confirmGenerationSubmit" type="button" ${ready ? '' : 'disabled'}>${ready ? '确认提交生成' : '扩展范围验证中'}</button></div>
          </section>
          <p id="message" role="status" aria-live="polite"></p>
        </form>
        <aside class="layout-review-panel" aria-label="布局核对预览">
          <div class="preview-panel">
            <div class="preview-head"><div><span>模型预览 / 3D preview</span><strong>查看你的户外柜</strong></div><span class="chip">三维布局</span></div>
            <div class="viewport-toolbar"><div class="view-button-row" role="group" aria-label="预览视角"><button class="button secondary view-active" data-view="iso" type="button" aria-pressed="true">ISO</button><button class="button secondary" data-view="front" type="button" aria-pressed="false">前视</button><button class="button secondary" data-view="right" type="button" aria-pressed="false">右视</button><button class="button secondary" data-view="top" type="button" aria-pressed="false">俯视</button></div><span class="preview-legend">拖动旋转 · 滚轮缩放</span><button class="secondary preview-reset" id="previewReset" type="button">复位</button></div>
            <div class="preview-canvas-wrap"><div class="drawing-corner" aria-hidden="true"><b>22057 / OUTDOOR</b><span>DUAL COLUMN LAYOUT</span></div><span class="drawing-scale">参数联动 · mm</span><canvas id="cabinetPreview" width="900" height="720" tabindex="0" role="img" aria-label="当前参数的户外柜三维布局，拖动旋转，滚轮缩放，方向键调整，Home复位"></canvas><svg id="previewDimensions" aria-hidden="true"></svg><p id="previewFallback" hidden>兼容模式 · 三维布局</p></div>
            <div class="viewport-hud"><div><span>柜体尺寸 / W × H × D</span><strong id="hudCabinetSize">—</strong></div><div><span>左右门列</span><strong id="hudDoorLayout">—</strong></div><div><span>单扇门宽估算</span><strong id="hudDoorSize">—</strong></div><div><span>门高与闭合</span><strong id="hudLayoutState">—</strong></div></div>
            <div class="preview-controls"><label for="previewRotation">旋转</label><input id="previewRotation" type="range" min="-180" max="180" value="-28"><span id="rotationLabel">-28°</span><label for="previewZoom">缩放</label><input id="previewZoom" type="range" min="70" max="150" value="100"><span id="zoomLabel">100%</span></div>
            <p class="preview-note">预览随尺寸和门序联动。01为最下门；精确零件、孔位及装配以生成后的原生模型为准。</p>
          </div>
          <details class="reference-model"><summary>查看标准原生总装参考图</summary><img src="/outdoor-22057/preview" alt="标准规格SOLIDWORKS总装参考，非当前参数生成结果"><p class="muted">标准规格参考图，不随当前输入变化。</p></details>
        </aside>
      </div>
    </div>
    <div class="workspace-view" data-workspace-view="my-models" hidden>
      <section class="panel task-panel" aria-labelledby="tasks-title"><div class="workspace-view-heading"><div><p class="eyebrow">我的模型 / Build &amp; validate</p><h1 id="tasks-title">任务工作台</h1><p class="muted">查看本账号的生成进度，下载通过检查的模型，或提交对应任务的反馈。</p></div><div class="workspace-stamp"><span>22057 / OUTDOOR</span><strong>02 / 03</strong><span class="stamp-title">任务状态 / 模型下载</span></div></div>
      <div class="task-toolbar"><label class="task-search" for="taskFilterInput"><span>检索</span><input id="taskFilterInput" type="search" placeholder="任务名称、尺寸或编号"></label><div class="task-filter-buttons" role="group" aria-label="任务状态筛选"><button class="secondary active" data-task-filter="all" type="button" aria-pressed="true">全部</button><button class="secondary" data-task-filter="queued" type="button" aria-pressed="false">排队</button><button class="secondary" data-task-filter="running" type="button" aria-pressed="false">生成中</button><button class="secondary" data-task-filter="ready" type="button" aria-pressed="false">可下载</button><button class="secondary" data-task-filter="failed" type="button" aria-pressed="false">待处理</button></div></div>
      <div class="task-refresh-row"><p id="taskRefreshStatus" class="muted" role="status">正在读取任务…</p><button id="refreshTasks" class="secondary" type="button">刷新任务</button></div><div id="tasks" class="request-list"></div></section>
    </div>
    <div class="workspace-view" data-workspace-view="model-feedback" hidden>
      <section class="panel task-panel" aria-labelledby="feedback-title"><div class="workspace-view-heading"><div><p class="eyebrow">模型反馈 / Feedback</p><h1 id="feedback-title">让修订对应到每次模型。</h1><p class="muted">选择具体任务，描述位置、现象及期望调整。反馈会保存在该任务记录中。</p></div><div class="workspace-stamp"><span>22057 / OUTDOOR</span><strong>03 / 03</strong><span class="stamp-title">问题反馈 / 修订记录</span></div></div>
      <div class="feedback-layout"><form id="feedback-form" class="studio-feedback-form"><label for="feedbackTask">选择模型任务<select id="feedbackTask" required><option value="">暂无可反馈的任务</option></select></label><p id="feedbackTaskContext" class="feedback-task-context">提交模型任务后，可在这里绑定反馈。</p><label for="feedbackMessage">模型问题或应用反馈<textarea id="feedbackMessage" name="message" maxlength="2000" required rows="6" placeholder="例如：左列第03门的连接位置需要调整……"></textarea></label><button id="saveFeedback" type="submit" disabled>保存本次反馈</button><p id="feedbackStatus" role="status" aria-live="polite"></p><div id="feedbackHistory"></div></form><aside class="feedback-guide"><h3>便于工程师定位</h3><p>说明柜体尺寸和具体零件，门序从下向上编号。</p><p>可记录干涉、尺寸、安装方式，以及与已确认模型的差异。</p><p>本页保存文字反馈；配图可通过原沟通渠道发送，并附任务编号。</p></aside></div></section>
    </div>
    <footer class="workspace-footer"><span>WINNSEN / 22057 户外柜工程辅助模型</span><span>参数编辑 → 原生构建 → 自动检查 → 下载深化</span></footer>
  </main>
</div><script>${outdoorLayoutScript(true)}\n(${outdoorStudioPreview.toString()})(${JSON.stringify(displayGeometry)});\n(${outdoorStudioClient.toString()})(${JSON.stringify({ generationReady: ready })});</script></body></html>`
}
