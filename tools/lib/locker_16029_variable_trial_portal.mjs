import { createHash } from 'node:crypto'
import { createReadStream, existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import {
  DEFAULT_VARIABLE_TRIAL_TASKS_DIR,
  createVariableTrialTaskStore,
  isPathInsideRoot,
  isSafeTaskId,
} from './locker_16029_variable_trial_tasks.mjs'
import {
  PORTAL_DIMENSION_LIMITS,
  PORTAL_PRESETS,
  normalizeVariableTrialPortalRequest,
} from '../../workers/master_v2/variable_trial/portal_pipeline.mjs'

export const VARIABLE_TRIAL_PORTAL_LABEL = '16029 全参数·3D+DXF'
export const VARIABLE_TRIAL_PORTAL_ROUTE = '/indoor-v2'

function readFastCadQueueState(rootDir) {
  const lockOwnerPath = resolve(rootDir, 'data/native_model_requests/.native-worker-instance.lock/owner.json')
  const nativeDir = resolve(rootDir, 'data/native_model_requests')
  const outdoorDir = resolve(rootDir, 'data/outdoor_22057_tasks')
  const blockers = []

  if (existsSync(lockOwnerPath)) {
    try {
      const owner = JSON.parse(readFileSync(lockOwnerPath, 'utf8'))
      const pid = Number(owner?.pid)
      if (Number.isInteger(pid) && pid > 0) {
        try {
          process.kill(pid, 0)
          blockers.push('SolidWorks 独占锁活跃')
        } catch (err) {
          if (err?.code === 'EPERM') blockers.push('SolidWorks 独占锁活跃')
        }
      }
    } catch {
      blockers.push('CAD 锁状态读取中')
    }
  }

  if (existsSync(nativeDir)) {
    try {
      for (const name of readdirSync(nativeDir)) {
        if (!name.endsWith('.json') || name.startsWith('native_model_request_index')) continue
        const item = JSON.parse(readFileSync(join(nativeDir, name), 'utf8'))
        if (['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested'].includes(item?.nativeBuild?.state)) {
          blockers.push('基础版 16029 队列运行中')
          break
        }
      }
    } catch {}
  }

  if (existsSync(outdoorDir)) {
    try {
      for (const name of readdirSync(outdoorDir)) {
        if (!name.endsWith('.json')) continue
        const item = JSON.parse(readFileSync(join(outdoorDir, name), 'utf8'))
        if (['queued', 'running', 'cancelling'].includes(item?.status)) {
          blockers.push('22057 户外柜队列运行中')
          break
        }
      }
    } catch {}
  }

  return {
    busy: blockers.length > 0,
    blockers,
  }
}

function renderVariableTrialPage({
  username,
  htmlEscape,
  processReviewEnabled = false,
  processReviewLabel = '',
}) {
  const limitsJson = JSON.stringify(PORTAL_DIMENSION_LIMITS)
  const presetsJson = JSON.stringify(PORTAL_PRESETS)

  return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>16029 全参数工程工作台（3D + DXF + 生产工艺闭环）· Winnsen</title>
  <style>
    :root {
      --ink:#141b29; --muted:#5c667a; --line:#d8e0ec; --soft:#edf1f7; --paper:#ffffff;
      --brand:#2554e6; --brand-deep:#1a3db3; --brand-tint:#eef3ff;
      --good:#117a53; --good-bg:#e8f8f0; --good-line:#bce4d1;
      --warn:#9b6211; --warn-bg:#fff6e6; --warn-line:#edd4a6;
      --risk:#b42b3c; --risk-bg:#fff0f2; --risk-line:#efc5cb;
      --cad-bg:#0c1424; --cad-grid:#17253e; --cad-ink:#e6eefc; --cad-cyan:#49d1ff; --cad-amber:#ffbd4a; --cad-green:#4ee2a1; --cad-pink:#ff6b9d;
      --sans:"Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif;
      --mono:"Cascadia Mono", Consolas, "Courier New", monospace;
    }
    *, *::before, *::after { box-sizing:border-box; }
    body { margin:0; background:linear-gradient(180deg, #e7edf6 0%, #f3f5fa 260px, #eef2f8 100%); color:var(--ink); font:14px/1.55 var(--sans); }
    a { color:var(--brand); text-decoration:none; }
    a:hover { text-decoration:underline; }
    button, .button {
      display:inline-flex; align-items:center; justify-content:center; gap:6px;
      min-height:42px; padding:8px 15px; border-radius:9px; border:1px solid transparent;
      background:var(--brand); color:#fff; font:600 13px/1.25 var(--sans); cursor:pointer;
      text-decoration:none; white-space:nowrap; transition:background .14s, border-color .14s, transform .08s, box-shadow .14s;
    }
    button:hover, .button:hover { background:var(--brand-deep); text-decoration:none; box-shadow:0 4px 12px #2554e628; }
    button:active, .button:active { transform:translateY(1px); }
    button.secondary, .button.secondary { background:#fff; color:#3c4659; border-color:var(--line); box-shadow:none; }
    button.secondary:hover, .button.secondary:hover { background:#f3f6fc; border-color:#adc0e6; color:var(--brand); }
    button.danger { background:var(--risk-bg); color:var(--risk); border-color:var(--risk-line); }
    button.danger:hover { background:#ffe1e6; color:#8d1c2b; box-shadow:none; }
    button:disabled { background:#eef1f6; color:#8892a4; border-color:#dce2ec; cursor:not-allowed; box-shadow:none; transform:none; }
    input, select {
      width:100%; min-height:42px; padding:8px 11px; border:1px solid #cfd8e6;
      border-radius:9px; background:#fbfcff; color:var(--ink); font:14px var(--sans);
      transition:border-color .14s, box-shadow .14s, background .14s;
    }
    input:focus, select:focus { outline:none; background:#fff; border-color:var(--brand); box-shadow:0 0 0 3px #2554e61c; }
    :where(a,button,input,select,summary):focus-visible { outline:3px solid #2554e670; outline-offset:2px; }

    .portal-header {
      position:sticky; top:0; z-index:40; min-height:74px; padding:10px max(24px, calc((100vw - 1560px) / 2));
      background:rgba(255,255,255,.94); backdrop-filter:blur(10px);
      border-bottom:1px solid var(--line); display:flex; flex-wrap:wrap; align-items:center; gap:16px;
    }
    .workspace-brand { display:flex; align-items:center; gap:12px; }
    .portal-logo { width:88px; height:40px; display:grid; place-items:center; }
    .portal-logo img { width:84px; max-height:36px; object-fit:contain; }
    .workspace-brand-copy { border-left:1px solid var(--line); padding-left:14px; display:grid; gap:1px; }
    .workspace-brand-copy strong { font-size:13.5px; font-weight:700; letter-spacing:-.01em; }
    .workspace-brand-copy span { font-size:11px; color:var(--muted); font-family:var(--mono); }
    .portal-nav { display:flex; flex-wrap:wrap; gap:6px; margin:0 auto; padding:4px; background:#f2f5fa; border-radius:11px; border:1px solid #e2e8f2; }
    .portal-nav a {
      min-height:36px; padding:7px 14px; border-radius:8px; color:#556175;
      display:inline-flex; align-items:center; font:600 13px var(--sans); text-decoration:none;
    }
    .portal-nav a:hover { background:#fff; color:var(--ink); text-decoration:none; }
    .portal-nav a.active { color:#fff; background:var(--brand); box-shadow:0 2px 8px #2554e630; }
    .account-card { display:flex; align-items:center; gap:10px; }
    .account-avatar {
      width:32px; height:32px; border-radius:50%; background:var(--brand-tint); color:var(--brand);
      border:1px solid #c7d6ff; display:grid; place-items:center; font:700 12px var(--mono);
    }
    .account-card strong { font-size:12.5px; }
    .account-card a { min-height:36px; display:inline-flex; align-items:center; color:var(--muted); font-size:12px; padding:0 8px; border-radius:7px; }
    .account-card a:hover { background:#f2f5fa; color:var(--risk); text-decoration:none; }

    .workspace-main { max-width:1560px; margin:0 auto; padding:18px 26px 54px; }
    .workspace-topline {
      display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between;
      gap:12px; padding:8px 14px; border-radius:10px; background:rgba(255,255,255,.78); border:1px solid var(--line);
      color:#485468; font-size:12px;
    }
    .cad-pill {
      display:inline-flex; align-items:center; gap:8px; padding:5px 13px; border-radius:999px;
      border:1px solid var(--good-line); background:var(--good-bg); color:var(--good); font-size:12px; font-weight:650;
    }
    .cad-pill::before { content:""; width:8px; height:8px; border-radius:50%; background:currentColor; }
    .cad-pill.busy { border-color:var(--warn-line); background:var(--warn-bg); color:var(--warn); }

    .overview {
      margin-top:16px; padding:22px 24px; border-radius:18px;
      background:linear-gradient(135deg, #101b33 0%, #162646 62%, #1d3564 100%);
      color:#eef4ff; border:1px solid #263b66; box-shadow:0 16px 38px #0c14241c;
    }
    .overview-row { display:flex; flex-wrap:wrap; align-items:start; justify-content:space-between; gap:20px; }
    .eyebrow { color:#8eb4ff; font:650 11px var(--mono); letter-spacing:.08em; text-transform:uppercase; }
    .overview h1 { margin:6px 0 8px; font-size:28px; line-height:1.2; letter-spacing:-.02em; color:#fff; }
    .overview p { margin:0; max-width:78ch; color:#bfd0ee; font-size:13px; line-height:1.65; }
    .stamp {
      min-width:255px; padding:14px 16px; border-radius:12px;
      background:rgba(11,18,32,.65); border:1px solid #324a78; font-size:12px; color:#c8d8f5;
    }
    .stamp strong { display:block; margin:4px 0; font:700 13.5px var(--mono); color:#74f0b8; }
    .workflow-rail {
      margin:18px 0 0; padding:0; list-style:none; display:grid;
      grid-template-columns:repeat(5, minmax(0, 1fr)); gap:10px;
    }
    .workflow-rail li {
      padding:10px 12px; border-radius:10px; background:rgba(255,255,255,.07); border:1px solid rgba(255,255,255,.11);
      display:grid; grid-template-columns:26px minmax(0,1fr); column-gap:9px; align-items:center;
    }
    .workflow-rail b {
      width:26px; height:26px; border-radius:50%; background:rgba(255,255,255,.14); color:#dbe7ff;
      display:grid; place-items:center; font:700 11px var(--mono);
    }
    .workflow-rail li.active { background:rgba(73,209,255,.14); border-color:rgba(73,209,255,.45); }
    .workflow-rail li.active b { background:var(--cad-cyan); color:#091322; }
    .workflow-rail span { font-size:12px; font-weight:650; color:#fff; }
    .workflow-rail small { grid-column:2; font-size:10.5px; color:#a9bddf; }

    .studio-grid { display:grid; grid-template-columns:minmax(380px, .92fr) minmax(0, 1.08fr); gap:22px; align-items:start; margin-top:20px; }
    .card { padding:22px; border:1px solid var(--line); border-radius:16px; background:var(--paper); box-shadow:0 8px 24px #14223b0a; }
    .card-head { display:flex; align-items:center; justify-content:space-between; gap:10px; margin-bottom:4px; }
    .card h2 { margin:0; font-size:18px; letter-spacing:-.01em; }
    .tag-chip {
      padding:3px 9px; border-radius:6px; font:650 11px var(--mono);
      background:var(--brand-tint); color:var(--brand); border:1px solid #cad8ff;
    }
    .muted { color:var(--muted); }

    .preset-bar { display:flex; flex-wrap:wrap; gap:7px; margin:12px 0 18px; }
    .preset-btn { min-height:38px; padding:6px 12px; font-size:12px; border-radius:8px; }
    .preset-btn.selected { background:var(--brand-tint); border-color:var(--brand); color:var(--brand); }
    .field-grid { display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:13px; }
    .field-grid .wide { grid-column:1 / -1; }
    .field label { display:grid; gap:5px; font-size:12px; font-weight:650; color:#364154; }
    .field-head { display:flex; justify-content:space-between; gap:8px; align-items:baseline; }
    .field-head small { color:#7a8599; font:500 10.5px var(--mono); }

    .door-editor { margin-top:18px; padding-top:16px; border-top:1px solid var(--line); }
    .door-editor-top { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:10px; }
    .door-editor-top h3 { margin:0; font-size:15px; }
    .mode-switch { display:inline-flex; gap:4px; padding:3px; background:#e9eef7; border-radius:9px; border:1px solid #d9e1ef; }
    .mode-switch button { min-height:36px; padding:5px 12px; background:transparent; color:#5d697e; border:0; border-radius:6px; font-size:12px; box-shadow:none; }
    .mode-switch button[aria-pressed="true"] { background:#fff; color:var(--brand); box-shadow:0 1px 4px #18274218; }

    .door-columns { display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:12px; margin-top:12px; }
    .door-col { padding:12px; border:1px solid #dce3ee; border-radius:12px; background:#f8fafe; }
    .door-col-head { display:flex; align-items:center; justify-content:space-between; gap:8px; margin-bottom:10px; }
    .door-col-head strong { font-size:13px; display:flex; align-items:center; gap:6px; }
    .col-notch-pill { font:600 10px var(--mono); padding:2px 6px; border-radius:5px; background:#eaf0ff; color:var(--brand); border:1px solid #cad8ff; }
    .col-notch-pill.right { background:#fff4e2; color:#9b5c08; border-color:#f2d199; }
    .door-col-head input { width:76px; min-height:38px; text-align:right; font-family:var(--mono); }
    .door-rows { display:grid; gap:7px; max-height:310px; overflow:auto; padding-right:2px; }
    .door-row { display:grid; grid-template-columns:36px minmax(0, 1fr) 74px; gap:6px; align-items:center; }
    .door-row span.idx { font:700 12px var(--mono); color:#48566d; }
    .door-row input { min-height:38px; padding:5px 8px; text-align:right; font:13px var(--mono); background:#fff; }
    .door-row small.calc { text-align:right; font:11px var(--mono); color:var(--muted); }
    .door-col-actions { display:flex; gap:6px; margin-top:10px; }
    .door-col-actions button { flex:1; min-height:36px; padding:5px 8px; font-size:11.5px; }

    .layout-banner {
      margin-top:14px; padding:11px 14px; border-radius:10px; font-size:12.5px; line-height:1.6; font-weight:500;
      border:1px solid var(--good-line); background:var(--good-bg); color:var(--good);
    }
    .layout-banner[data-valid="false"] { border-color:var(--risk-line); background:var(--risk-bg); color:var(--risk); }

    .confirm-box {
      margin-top:14px; padding:16px; border:1px solid #adc2ff; border-radius:12px; background:#eff4ff; display:grid; gap:10px;
    }
    .confirm-grid { display:grid; grid-template-columns:repeat(2, minmax(0,1fr)); gap:8px; font-size:12px; }
    .confirm-grid div { padding:8px 10px; background:#fff; border-radius:8px; border:1px solid #d4e0fb; }
    .confirm-grid span { display:block; color:var(--muted); font-size:11px; }
    .confirm-grid strong { font:650 12px var(--mono); }

    .preview-card { padding:0; overflow:hidden; position:sticky; top:90px; }
    .preview-head {
      padding:16px 20px; display:flex; align-items:center; justify-content:space-between; gap:12px;
      background:#0e172a; color:#fff; border-bottom:1px solid #1e2d4d;
    }
    .preview-head .eyebrow { color:#7ab4ff; }
    .preview-head strong { font-size:16.5px; color:#fff; }
    .view-bar {
      padding:10px 16px; background:#121e36; display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between;
      gap:10px; border-bottom:1px solid #1f3052;
    }
    .view-buttons { display:inline-flex; flex-wrap:wrap; gap:4px; padding:3px; background:#091121; border-radius:9px; border:1px solid #22355a; }
    .view-buttons button {
      min-height:36px; padding:6px 12px; border:0; border-radius:6px; background:transparent; color:#96abcf; font-size:12px; box-shadow:none;
    }
    .view-buttons button:hover { color:#fff; background:rgba(255,255,255,.08); }
    .view-buttons button.active { background:#2554e6; color:#fff; box-shadow:0 2px 8px #2554e666; }
    .canvas-wrap { padding:12px; background:var(--cad-bg); }
    canvas#vtPreview { width:100%; height:auto; display:block; border-radius:10px; background:var(--cad-bg); border:1px solid #213457; }
    .hud-grid { display:grid; grid-template-columns:repeat(4, minmax(0, 1fr)); background:#0f192d; border-top:1px solid #1e2d4d; }
    .hud-grid div { padding:10px 14px; border-left:1px solid #1e2d4d; }
    .hud-grid div:first-child { border-left:0; }
    .hud-grid span { display:block; font-size:10.5px; color:#8ba1c7; }
    .hud-grid strong { font:700 12.5px var(--mono); color:var(--cad-cyan); }

    .matrix-wrap { display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:10px; padding:14px 16px; background:#f8fafe; border-top:1px solid var(--line); }
    .matrix-box { padding:11px 12px; border-radius:10px; background:#fff; border:1px solid #e1e8f3; display:grid; gap:6px; }
    .matrix-box h4 { margin:0; font-size:12px; font-weight:700; color:#23314d; display:flex; justify-content:space-between; align-items:center; }
    .matrix-box ul { margin:0; padding:0; list-style:none; display:grid; gap:4px; font-size:11.5px; color:#4f5e75; }
    .matrix-box li { display:flex; align-items:baseline; gap:6px; line-height:1.45; }
    .matrix-box li b { font:700 10.5px var(--mono); padding:1px 5px; border-radius:4px; background:#eef3ff; color:var(--brand); flex:none; }
    .matrix-box.craft li b { background:#e8f8f0; color:var(--good); }

    .tasks-section { margin-top:26px; }
    .task-toolbar {
      display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:12px;
      padding:16px 20px; border:1px solid var(--line); border-radius:14px; background:#fff; box-shadow:0 4px 16px #14223b07;
    }
    .filter-pills { display:flex; flex-wrap:wrap; gap:6px; }
    .filter-pills button { min-height:36px; padding:5px 12px; font-size:12px; }
    .filter-pills button.active { background:var(--brand); color:#fff; border-color:var(--brand); }
    .task-search { width:260px; max-width:100%; min-height:38px; }
    .task-list { display:grid; gap:14px; margin-top:14px; }
    .task-item {
      padding:18px 22px; border:1px solid var(--line); border-radius:14px; background:#fff;
      display:grid; gap:12px; box-shadow:0 4px 16px #14223b08;
    }
    .task-top { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:10px; }
    .task-title { display:flex; flex-wrap:wrap; align-items:center; gap:10px; }
    .task-title strong { font:700 15px var(--mono); color:#14213d; }
    .badge { padding:3px 9px; border-radius:6px; font:650 11px var(--sans); border:1px solid transparent; }
    .badge.ready { background:var(--good-bg); border-color:var(--good-line); color:var(--good); }
    .badge.running, .badge.cancel_requested { background:#eaf0ff; border-color:#c6d5ff; color:var(--brand); }
    .badge.queued { background:var(--warn-bg); border-color:var(--warn-line); color:var(--warn); }
    .badge.failed { background:var(--risk-bg); border-color:var(--risk-line); color:var(--risk); }
    .badge.cancelled { background:#f0f2f5; border-color:#dce1e8; color:#647082; }
    .task-specs { padding:10px 14px; border-radius:9px; background:#f5f8fc; border:1px solid #e4ebf5; font:12px/1.7 var(--mono); color:#3d4d66; }
    .metrics-grid { display:grid; grid-template-columns:repeat(5, minmax(0, 1fr)); gap:8px; }
    .metrics-grid div { padding:9px 11px; border-radius:9px; background:#f0faf5; border:1px solid #cce9da; font-size:11px; }
    .metrics-grid span { display:block; color:#3d6654; }
    .metrics-grid strong { font:700 12px var(--mono); color:var(--good); }
    .craft-badges { display:flex; flex-wrap:wrap; gap:6px; }
    .craft-pill {
      padding:3px 9px; border-radius:999px; font:600 11px var(--sans);
      background:#f2f6ff; color:#2b4ba8; border:1px solid #d2dfff;
    }
    .task-actions { display:flex; flex-wrap:wrap; gap:8px; align-items:center; }
    .empty-state { padding:36px 20px; text-align:center; border:1px dashed #cfd6e3; border-radius:14px; background:#fff; color:var(--muted); }

    dialog.report-modal {
      width:min(960px, 94vw); max-height:86vh; padding:0; border:1px solid var(--line);
      border-radius:14px; background:#fff; box-shadow:0 24px 60px #0f172a30;
    }
    dialog.report-modal::backdrop { background:#0f172a66; }
    .modal-head { padding:16px 20px; border-bottom:1px solid var(--line); display:flex; justify-content:space-between; align-items:center; }
    .modal-body { padding:18px 20px; overflow:auto; max-height:calc(86vh - 76px); font:12px/1.6 var(--mono); white-space:pre-wrap; }

    @media (max-width: 1120px) {
      .studio-grid { grid-template-columns:1fr; }
      .preview-card { position:static; }
      .workflow-rail { grid-template-columns:1fr; }
      .metrics-grid, .hud-grid, .matrix-wrap { grid-template-columns:1fr; }
    }
    @media (max-width: 640px) {
      .workspace-main { padding:12px 14px 36px; }
      .card { padding:16px; }
      .door-columns, .field-grid, .confirm-grid { grid-template-columns:1fr; }
      .portal-header { padding:10px 14px; }
    }
  </style>
</head>
<body>
  <header class="portal-header">
    <div class="workspace-brand">
      <div class="portal-logo"><img src="/brand/winnsen-logo.jpg" alt="Winnsen" /></div>
      <div class="workspace-brand-copy">
        <strong>Winnsen 智能柜结构工程工作台</strong>
        <span>Q1–Q4 + 4 项车间工艺 · SolidWorks 2020 原生参数化</span>
      </div>
    </div>
    <nav class="portal-nav" aria-label="模型助手功能">
      <a href="/outdoor-22057">22057 户外柜内测</a>
      <a href="/">室内柜·基础版</a>
      ${processReviewEnabled ? `<a href="/indoor-process-review">${htmlEscape(processReviewLabel || '室内柜·工艺版')}</a>` : ''}
      <a href="${VARIABLE_TRIAL_PORTAL_ROUTE}" class="active" aria-current="page">${VARIABLE_TRIAL_PORTAL_LABEL}</a>
    </nav>
    <div class="account-card">
      <div class="account-avatar">${htmlEscape(String(username || 'U').slice(0, 2).toUpperCase())}</div>
      <div><strong>${htmlEscape(username)}</strong></div>
      <a href="/logout">退出</a>
    </div>
  </header>

  <main class="workspace-main">
    <div class="workspace-topline">
      <span>当前产线：<strong>16029 全尺寸参数化（3D 装配 + 展开 DXF + 2D 工程图 SLDDRW/PDF + BOM）</strong></span>
      <span id="cadStatusPill" class="cad-pill">CAD 工作站状态：检查中…</span>
    </div>

    <section class="overview">
      <div class="overview-row">
        <div>
          <div class="eyebrow">WINNSEN CAD WORKSTATION · Q1–Q4 + WORKSHOP PRODUCTION CRAFTS + 2D DRAWINGS</div>
          <h1>16029 全参数工程工作台（3D + DXF + 2D 工程图）</h1>
          <p>支持 700–1200W × 1700–2200H × 250–650D mm 任意整柜外廓与左右异门格配置。隔离 SolidWorks 2020 自动生成全套装配并完整注入 4 项车间工艺（左半圆/右三角防呆缺口、上销孔内偏 0.5mm、层板取消 Ø2 定位孔、全套激光焊接打标），联动导出 CUT/BEND/MARK 三层展开 DXF、总装与各唯一门板 2D 工程图（SLDDRW + PDF）以及 Excel BOM 下料清单。</p>
        </div>
        <div class="stamp">
          <span>工程门禁标识</span>
          <strong>production_release: false</strong>
          <span>3D 零结构干涉 + 100% DXF + 2D 工程图通过后开放交付</span>
        </div>
      </div>
      <ol class="workflow-rail">
        <li class="active"><b>1</b><span>规格与门格设计</span><small>700–1200W / L1–24 / R1–24</small></li>
        <li><b>2</b><span>闭合校验与预览</span><small>0.001mm 闭合 + 工艺蓝图</small></li>
        <li><b>3</b><span>3D 全柜 + 4项工艺</span><small>防呆缺口 / 偏孔 / 打标</small></li>
        <li><b>4</b><span>DXF + 2D 工程图</span><small>全检 DXF + SLDDRW/PDF</small></li>
        <li><b>5</b><span>全套工程包交付</span><small>3D+DXF+2D+BOM ZIP</small></li>
      </ol>
    </section>

    <section class="studio-grid">
      <form id="vtForm" class="card" novalidate>
        <div class="card-head">
          <h2>1. 定制参数规格</h2>
          <span class="tag-chip">SOLIDWORKS 2020</span>
        </div>
        <p class="muted" style="margin:0; font-size:12px;">点击已验证工况预设快速载入，或自由调整整柜尺寸与左右异门格排布。</p>
        <div class="preset-bar" id="presetBar"></div>

        <div class="field-grid">
          <div class="field wide">
            <label>
              <span class="field-head">项目 / 客户需求备注 <small>可选，写入报告与清单</small></span>
              <input id="vtRef" name="customerRequirementReference" type="text" maxlength="120" placeholder="例如：某医院项目 860×1800×450 L6-R5 验证包" />
            </label>
          </div>
          <div class="field">
            <label>
              <span class="field-head">宽度输入口径 <small>支持门宽反算</small></span>
              <select id="vtWidthMode" name="widthInputMode">
                <option value="cabinet_outer_width">整柜外宽 (700–1200 mm)</option>
                <option value="installed_door_panel_width">装后门板单宽 (287–537 mm)</option>
              </select>
            </label>
          </div>
          <div class="field">
            <label>
              <span class="field-head"><span id="vtWidthLabel">柜体总宽 W (mm)</span> <small id="vtWidthRange">700–1200</small></span>
              <input id="vtWidthInput" type="number" step="1" value="1000" required />
            </label>
          </div>
          <div class="field">
            <label>
              <span class="field-head">柜体总高 H (mm) <small>1700–2200</small></span>
              <input id="vtHeightInput" type="number" step="1" min="1700" max="2200" value="2000" required />
            </label>
          </div>
          <div class="field">
            <label>
              <span class="field-head">柜体总深 D (mm) <small>250–650</small></span>
              <input id="vtDepthInput" type="number" step="1" min="250" max="650" value="500" required />
            </label>
          </div>
        </div>

        <div class="door-editor">
          <div class="door-editor-top">
            <h3>左右门格排布（自下向上）</h3>
            <div class="mode-switch" role="group" aria-label="门高分配模式">
              <button type="button" id="modeRatioBtn" aria-pressed="true">比例分配</button>
              <button type="button" id="modeMmBtn" aria-pressed="false">精确门高 (mm)</button>
            </div>
          </div>

          <div class="door-columns">
            <div class="door-col" data-col="L">
              <div class="door-col-head">
                <strong>左列 L 门数 <span class="col-notch-pill">◐ 半圆 R1.5</span></strong>
                <input id="leftDoorCount" type="number" min="1" max="24" step="1" value="4" />
              </div>
              <div class="door-rows" id="leftDoorRows"></div>
              <div class="door-col-actions">
                <button type="button" class="secondary" data-equalize="L">左列等分</button>
                <button type="button" class="secondary" data-balance="L">配平顶门</button>
              </div>
            </div>

            <div class="door-col" data-col="R">
              <div class="door-col-head">
                <strong>右列 R 门数 <span class="col-notch-pill right">◁ 三角 3×1.5</span></strong>
                <input id="rightDoorCount" type="number" min="1" max="24" step="1" value="4" />
              </div>
              <div class="door-rows" id="rightDoorRows"></div>
              <div class="door-col-actions">
                <button type="button" class="secondary" data-equalize="R">右列等分</button>
                <button type="button" class="secondary" data-balance="R">配平顶门</button>
              </div>
            </div>
          </div>

          <div id="layoutBanner" class="layout-banner" data-valid="true">正在校核门格…</div>
        </div>

        <div style="display:flex; flex-wrap:wrap; gap:10px; margin-top:16px;">
          <button type="button" id="prepareSubmitBtn">预览参数并准备提交</button>
          <span id="submitFeedback" class="muted" style="align-self:center; font-size:12px;"></span>
        </div>

        <div id="confirmBox" class="confirm-box" hidden>
          <div style="display:flex; justify-content:space-between; align-items:center; gap:8px;">
            <strong>确认提交全参数 3D + DXF + 2D 工程图 + 生产工艺生成任务</strong>
            <span class="badge queued">production_release = false</span>
          </div>
          <div class="confirm-grid" id="confirmGrid"></div>
          <div style="display:flex; flex-wrap:wrap; gap:10px;">
            <button type="button" id="confirmSubmitBtn">确认入队生成（含 3D + DXF + 2D工程图）</button>
            <button type="button" class="secondary" id="cancelConfirmBtn">返回修改</button>
          </div>
        </div>
      </form>

      <aside class="card preview-card">
        <div class="preview-head">
          <div>
            <span class="eyebrow">CAD DRAFTING BLUEPRINT & PROCESS VIEW</span><br />
            <strong id="previewHeading">1000 × 2000 × 500 mm · 8 门</strong>
          </div>
          <span class="badge ready" id="previewValidBadge">校验通过</span>
        </div>
        <div class="view-bar">
          <div class="view-buttons">
            <button type="button" data-vt-view="front" class="active">正视图</button>
            <button type="button" data-vt-view="right">右视图</button>
            <button type="button" data-vt-view="top">俯视图</button>
            <button type="button" data-vt-view="iso">等轴测</button>
            <button type="button" data-vt-view="craft">工艺详图</button>
          </div>
          <span style="font:500 11px var(--mono); color:#8ca2ca;">底脚 32mm · 门缝 7mm</span>
        </div>
        <div class="canvas-wrap">
          <canvas id="vtPreview" width="840" height="560"></canvas>
        </div>
        <div class="hud-grid">
          <div><span>整柜外宽 W</span><strong id="hudW">1000 mm</strong></div>
          <div><span>门叶单宽</span><strong id="hudLeaf">437.00 mm</strong></div>
          <div><span>总门数 / 竖隔</span><strong id="hudDoors">8 门 (L4/R4)</strong></div>
          <div><span>竖向净空间</span><strong id="hudBudget">1913.00 mm</strong></div>
        </div>
        <div class="matrix-wrap">
          <div class="matrix-box">
            <h4>Q1–Q4 结构与 DXF 审计门禁 <span style="font:600 10px var(--mono); color:var(--brand);">ACTIVE</span></h4>
            <ul>
              <li><b>Q1</b><span>门板锁扣内缩 0.2mm（零结构干涉），挡圈插接与上销预偏自动分类豁免</span></li>
              <li><b>Q2</b><span>整柜特征与关联孔距按外廓全参数联动缩放，独立 Pack-and-Go 闭环</span></li>
              <li><b>Q3</b><span>成形模具（开口卡/拱桥模具）抑制特征并自动分入 DXF MARK 层</span></li>
              <li><b>Q4</b><span>清理压铆螺母/柱内圈同心孔，仅保留外圈预孔并通过 dxf_check</span></li>
            </ul>
          </div>
          <div class="matrix-box craft">
            <h4>4 项生产工艺原生集成 <span style="font:600 10px var(--good); font-family:var(--mono);">INTEGRATED</span></h4>
            <ul>
              <li><b>C1</b><span>左门 5mm 铰链边中点切 R1.5 半圆缺口；右门切 3×1.5 三角缺口</span></li>
              <li><b>C2</b><span>左右门上铰链销孔 Ø10.2 向内侧预偏 0.5mm（距边 10.5 vs 10.0mm）</span></li>
              <li><b>C3</b><span>横层板 L/R 原 8 处 Ø2.0mm 定位孔通过 DeleteFace-Patch 修补移除</span></li>
              <li><b>C4</b><span>门板/层板/侧板/竖隔板/加强筋 3D 表面草图与展开 DXF MARK 打标同步</span></li>
            </ul>
          </div>
        </div>
      </aside>
    </section>

    <section class="tasks-section" id="vtTaskSection">
      <div class="task-toolbar">
        <div>
          <strong style="font-size:17px;">我的 3D + DXF 生成队列与交付包</strong>
          <span id="taskRefreshNote" class="muted" style="margin-left:10px; font-size:12px;">等待首次刷新…</span>
        </div>
        <div style="display:flex; flex-wrap:wrap; gap:10px; align-items:center;">
          <div class="filter-pills" id="taskFilterPills">
            <button type="button" class="secondary active" data-filter="all">全部</button>
            <button type="button" class="secondary" data-filter="ready">可下载</button>
            <button type="button" class="secondary" data-filter="active">进行中/排队</button>
            <button type="button" class="secondary" data-filter="failed">未通过</button>
            <button type="button" class="secondary" data-filter="cancelled">已取消</button>
          </div>
          <input id="taskSearch" class="task-search" type="search" placeholder="筛选任务编号 / 备注 / 尺寸…" />
        </div>
      </div>

      <div id="vtTaskList" class="task-list">
        <div class="empty-state">正在读取队列…</div>
      </div>
    </section>
  </main>

  <dialog id="reportModal" class="report-modal">
    <div class="modal-head">
      <strong id="reportModalTitle">工程验证详细报告</strong>
      <button type="button" class="secondary" id="closeReportModal">关闭</button>
    </div>
    <pre id="reportModalBody" class="modal-body"></pre>
  </dialog>

  <script>
    (() => {
      const LIMITS = ${limitsJson};
      const PRESETS = ${presetsJson};
      const STATUS_LABELS = {
        queued: '排队等待',
        running: '构建/审计中',
        cancel_requested: '取消中',
        ready: '已通过 · 可下载',
        failed: '未通过',
        cancelled: '已取消',
      };

      const state = {
        widthInputMode: 'cabinet_outer_width',
        widthInputValue: 1000,
        heightMm: 2000,
        depthMm: 500,
        allocationMode: 'ratio',
        view: 'front',
        filter: 'all',
        search: '',
        tasks: [],
        columns: {
          L: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }],
          R: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }],
        },
      };

      const el = (id) => document.getElementById(id);
      const escapeHtml = (value) => String(value == null ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

      function computeOuterWidthMm() {
        const val = Number(state.widthInputValue);
        if (!Number.isFinite(val)) return NaN;
        return state.widthInputMode === 'installed_door_panel_width' ? val * 2 + 126 : val;
      }

      function computeColumnDoors(side, cabinetHeightMm) {
        const items = state.columns[side];
        const budget = cabinetHeightMm - LIMITS.verticalAllowanceMm;
        if (!Array.isArray(items) || !items.length || !Number.isFinite(budget) || budget <= 0) {
          return { ok: false, error: '门数无效', heights: [], sumPitch: 0, budget };
        }
        if (state.allocationMode === 'height_mm') {
          const heights = items.map((d) => Number(d.heightMm ?? d.computedMm ?? 100));
          const invalid = heights.some((h) => !Number.isFinite(h) || h < LIMITS.minDoorHeightMm);
          const sumPitch = heights.reduce((s, h) => s + h + LIMITS.doorGapMm, 0);
          const delta = sumPitch - budget;
          const ok = !invalid && Math.abs(delta) <= LIMITS.closureToleranceMm;
          return {
            ok,
            heights,
            sumPitch,
            budget,
            delta,
            error: invalid
              ? \`\${side} 列存在小于 \${LIMITS.minDoorHeightMm} mm 的门\`
              : Math.abs(delta) > LIMITS.closureToleranceMm
                ? \`\${side} 列闭合差 \${delta > 0 ? '+' : ''}\${delta.toFixed(3)} mm（可点击“配平顶门”）\`
                : '',
          };
        }
        const units = items.map((d) => Number(d.heightUnits ?? 1));
        if (units.some((u) => !Number.isFinite(u) || u <= 0)) {
          return { ok: false, error: \`\${side} 列比例系数必须为正数\`, heights: [], sumPitch: 0, budget };
        }
        const unitSum = units.reduce((s, u) => s + u, 0);
        const heights = units.map((u) => budget * u / unitSum - LIMITS.doorGapMm);
        const tooSmall = heights.some((h) => !Number.isFinite(h) || h < LIMITS.minDoorHeightMm);
        return {
          ok: !tooSmall,
          heights,
          sumPitch: budget,
          budget,
          delta: 0,
          error: tooSmall ? \`\${side} 列门数过多或比例过小，单门高低于 100 mm\` : '',
        };
      }

      function validateAll() {
        const w = computeOuterWidthMm();
        const h = Number(state.heightMm);
        const d = Number(state.depthMm);
        const errors = [];
        if (!Number.isInteger(w) || w < LIMITS.widthMm[0] || w > LIMITS.widthMm[1]) {
          errors.push(\`整柜外宽 W 需为 \${LIMITS.widthMm[0]}–\${LIMITS.widthMm[1]} mm 整数\`);
        }
        if (!Number.isInteger(h) || h < LIMITS.heightMm[0] || h > LIMITS.heightMm[1]) {
          errors.push(\`整柜总高 H 需为 \${LIMITS.heightMm[0]}–\${LIMITS.heightMm[1]} mm 整数\`);
        }
        if (!Number.isInteger(d) || d < LIMITS.depthMm[0] || d > LIMITS.depthMm[1]) {
          errors.push(\`整柜总深 D 需为 \${LIMITS.depthMm[0]}–\${LIMITS.depthMm[1]} mm 整数\`);
        }
        const lCount = state.columns.L.length;
        const rCount = state.columns.R.length;
        if (lCount < 1 || lCount > LIMITS.maxDoorsPerColumn || rCount < 1 || rCount > LIMITS.maxDoorsPerColumn) {
          errors.push(\`单列门数须在 1–\${LIMITS.maxDoorsPerColumn}\`);
        }
        if (lCount + rCount > LIMITS.maxTotalDoors) {
          errors.push(\`总门数 (\${lCount + rCount}) 不得超过 \${LIMITS.maxTotalDoors}\`);
        }
        const left = computeColumnDoors('L', h);
        const right = computeColumnDoors('R', h);
        if (!left.ok && left.error) errors.push(left.error);
        if (!right.ok && right.error) errors.push(right.error);
        return {
          ok: errors.length === 0,
          errors,
          cabinet: { widthMm: w, heightMm: h, depthMm: d },
          doorLeafWidthMm: Number.isFinite(w) ? (w - 126) / 2 : NaN,
          left,
          right,
        };
      }

      function renderPresets() {
        const bar = el('presetBar');
        bar.innerHTML = '';
        PRESETS.forEach((preset) => {
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = 'secondary preset-btn';
          btn.textContent = preset.label;
          btn.title = preset.description;
          btn.addEventListener('click', () => {
            bar.querySelectorAll('button').forEach((b) => b.classList.toggle('selected', b === btn));
            state.widthInputMode = 'cabinet_outer_width';
            state.widthInputValue = preset.cabinet.widthMm;
            state.heightMm = preset.cabinet.heightMm;
            state.depthMm = preset.cabinet.depthMm;
            state.allocationMode = 'ratio';
            state.columns = {
              L: preset.columns[0].doors.map((x) => ({ heightUnits: x.heightUnits })),
              R: preset.columns[1].doors.map((x) => ({ heightUnits: x.heightUnits })),
            };
            syncInputsFromState();
            updateUI();
          });
          bar.appendChild(btn);
        });
      }

      function syncInputsFromState() {
        el('vtWidthMode').value = state.widthInputMode;
        el('vtWidthInput').value = String(state.widthInputValue);
        el('vtHeightInput').value = String(state.heightMm);
        el('vtDepthInput').value = String(state.depthMm);
        el('leftDoorCount').value = String(state.columns.L.length);
        el('rightDoorCount').value = String(state.columns.R.length);
        el('vtWidthLabel').textContent = state.widthInputMode === 'installed_door_panel_width' ? '装后门板单宽 (mm)' : '柜体总宽 W (mm)';
        el('vtWidthRange').textContent = state.widthInputMode === 'installed_door_panel_width' ? '287–537' : '700–1200';
        el('modeRatioBtn').setAttribute('aria-pressed', state.allocationMode === 'ratio' ? 'true' : 'false');
        el('modeMmBtn').setAttribute('aria-pressed', state.allocationMode === 'height_mm' ? 'true' : 'false');
      }

      function renderDoorRows(side) {
        const container = el(side === 'L' ? 'leftDoorRows' : 'rightDoorRows');
        const evalRes = computeColumnDoors(side, Number(state.heightMm));
        container.innerHTML = '';
        state.columns[side].forEach((door, idx) => {
          const row = document.createElement('div');
          row.className = 'door-row';
          const lbl = document.createElement('span');
          lbl.className = 'idx';
          lbl.textContent = \`\${side}\${idx + 1}\`;
          const input = document.createElement('input');
          input.type = 'number';
          if (state.allocationMode === 'ratio') {
            input.step = '0.5';
            input.min = '0.1';
            input.value = String(door.heightUnits ?? 1);
            input.addEventListener('input', () => {
              door.heightUnits = Number(input.value);
              refreshComputedLabels();
              updateStatusAndPreview();
            });
          } else {
            input.step = '0.25';
            input.min = '100';
            const mmVal = door.heightMm !== undefined ? door.heightMm : Number((evalRes.heights[idx] || 100).toFixed(3));
            door.heightMm = mmVal;
            input.value = String(mmVal);
            input.addEventListener('input', () => {
              door.heightMm = Number(input.value);
              refreshComputedLabels();
              updateStatusAndPreview();
            });
          }
          const calc = document.createElement('small');
          calc.className = 'calc';
          calc.dataset.side = side;
          calc.dataset.idx = String(idx);
          const hVal = evalRes.heights[idx];
          calc.textContent = Number.isFinite(hVal) ? \`\${hVal.toFixed(2)} mm\` : '--';
          row.appendChild(lbl);
          row.appendChild(input);
          row.appendChild(calc);
          container.appendChild(row);
        });
      }

      function refreshComputedLabels() {
        ['L', 'R'].forEach((side) => {
          const res = computeColumnDoors(side, Number(state.heightMm));
          document.querySelectorAll(\`.calc[data-side="\${side}"]\`).forEach((node) => {
            const idx = Number(node.dataset.idx);
            const hVal = res.heights[idx];
            node.textContent = Number.isFinite(hVal) ? \`\${hVal.toFixed(2)} mm\` : '--';
          });
        });
      }

      function setColumnDoorCount(side, count) {
        const clamped = Math.max(1, Math.min(24, Math.trunc(Number(count) || 1)));
        const current = state.columns[side];
        if (state.allocationMode === 'ratio') {
          state.columns[side] = Array.from({ length: clamped }, (_, i) => ({ heightUnits: current[i]?.heightUnits || 1 }));
        } else {
          const budget = (Number(state.heightMm) || 2000) - LIMITS.verticalAllowanceMm;
          const equalMm = Number((budget / clamped - LIMITS.doorGapMm).toFixed(3));
          state.columns[side] = Array.from({ length: clamped }, () => ({ heightMm: equalMm }));
        }
        el(side === 'L' ? 'leftDoorCount' : 'rightDoorCount').value = String(clamped);
        renderDoorRows(side);
        updateStatusAndPreview();
      }

      function drawCadGrid(ctx, W, H) {
        ctx.fillStyle = '#0c1424';
        ctx.fillRect(0, 0, W, H);
        ctx.strokeStyle = '#15233d';
        ctx.lineWidth = 1;
        for (let x = 28; x < W; x += 28) {
          ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke();
        }
        for (let y = 28; y < H; y += 28) {
          ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke();
        }
        ctx.strokeStyle = '#1f3358';
        ctx.lineWidth = 1.5;
        ctx.strokeRect(10, 10, W - 20, H - 20);
      }

      function drawCabinet() {
        const canvas = el('vtPreview');
        const ctx = canvas.getContext('2d');
        const W = canvas.width;
        const H = canvas.height;
        drawCadGrid(ctx, W, H);

        const check = validateAll();
        const cabW = Number.isFinite(check.cabinet.widthMm) ? check.cabinet.widthMm : 1000;
        const cabH = Number.isFinite(check.cabinet.heightMm) ? check.cabinet.heightMm : 2000;
        const cabD = Number.isFinite(check.cabinet.depthMm) ? check.cabinet.depthMm : 500;
        const leafW = Math.max(20, (cabW - 126) / 2);
        const view = state.view;

        ctx.font = '12px Consolas, monospace';

        if (view === 'front') {
          const padX = 120;
          const padY = 52;
          const scale = Math.min((W - padX * 2) / cabW, (H - padY * 2) / cabH);
          const drawW = cabW * scale;
          const drawH = cabH * scale;
          const x0 = (W - drawW) / 2;
          const y0 = (H - drawH) / 2;

          ctx.fillStyle = '#13213a';
          ctx.strokeStyle = '#7ba8e8';
          ctx.lineWidth = 2;
          ctx.fillRect(x0, y0, drawW, drawH);
          ctx.strokeRect(x0, y0, drawW, drawH);

          // Base plinth & feet (32 mm bottom)
          const footH = 32 * scale;
          ctx.fillStyle = '#233658';
          ctx.fillRect(x0 + 18 * scale, y0 + drawH - footH, 40 * scale, footH);
          ctx.fillRect(x0 + drawW - 58 * scale, y0 + drawH - footH, 40 * scale, footH);

          const leafDrawW = leafW * scale;
          const colLX = x0 + 35 * scale;
          const colRX = x0 + drawW - 35 * scale - leafDrawW;

          const drawColumn = (colX, side, heights) => {
            let curY = y0 + drawH - 32 * scale;
            heights.forEach((hMm, idx) => {
              const doorH = Math.max(4, hMm * scale);
              curY -= doorH;
              ctx.fillStyle = '#182b4c';
              ctx.strokeStyle = '#49d1ff';
              ctx.lineWidth = 1.5;
              ctx.fillRect(colX, curY, leafDrawW, doorH);
              ctx.strokeRect(colX, curY, leafDrawW, doorH);

              // Production craft hinge indicator (semicircle vs triangle + upper pin pre-bias dot)
              const hingeX = side === 'L' ? colX + 5 : colX + leafDrawW - 5;
              ctx.fillStyle = side === 'L' ? '#4ee2a1' : '#ffbd4a';
              ctx.beginPath();
              ctx.arc(hingeX, curY + 5, 2.2, 0, Math.PI * 2);
              ctx.fill();

              if (doorH >= 15) {
                ctx.fillStyle = '#e6eefc';
                ctx.textAlign = 'center';
                ctx.fillText(\`\${side}\${idx + 1}: \${hMm.toFixed(1)}mm\`, colX + leafDrawW / 2, curY + doorH / 2 + 4);
              }
              curY -= LIMITS.doorGapMm * scale;
            });
          };

          drawColumn(colLX, 'L', check.left.heights);
          drawColumn(colRX, 'R', check.right.heights);

          // Center mullion
          const midX = colLX + leafDrawW;
          const midW = colRX - midX;
          ctx.fillStyle = '#1e3152';
          ctx.fillRect(midX, y0 + 25 * scale, midW, drawH - 57 * scale);
          ctx.strokeStyle = '#6e90c8';
          ctx.strokeRect(midX, y0 + 25 * scale, midW, drawH - 57 * scale);

          // Labels & craft summary banner on canvas
          ctx.fillStyle = '#49d1ff';
          ctx.textAlign = 'center';
          ctx.fillText(\`FRONT · 整柜总宽 W=\${cabW}mm  |  门叶单宽=\${leafW.toFixed(1)}mm  |  左◐ R1.5 · 右◁ 3×1.5\`, W / 2, y0 - 16);
          ctx.save();
          ctx.translate(x0 - 22, y0 + drawH / 2);
          ctx.rotate(-Math.PI / 2);
          ctx.fillText(\`总高 H = \${cabH} mm\`, 0, 0);
          ctx.restore();
        } else if (view === 'right') {
          const pad = 74;
          const scale = Math.min((W - pad * 2) / cabD, (H - pad * 2) / cabH);
          const drawD = cabD * scale;
          const drawH = cabH * scale;
          const x0 = (W - drawD) / 2;
          const y0 = (H - drawH) / 2;
          ctx.fillStyle = '#14223d';
          ctx.strokeStyle = '#7ba8e8';
          ctx.lineWidth = 2;
          ctx.fillRect(x0, y0, drawD, drawH);
          ctx.strokeRect(x0, y0, drawD, drawH);
          // Front door plane & internal stiffeners
          ctx.fillStyle = '#49d1ff';
          ctx.fillRect(x0 - 4, y0 + 32 * scale, 5, drawH - 87 * scale);
          ctx.strokeStyle = '#4ee2a1';
          ctx.setLineDash([5, 4]);
          ctx.strokeRect(x0 + drawD * 0.25, y0 + 40 * scale, 12, drawH - 95 * scale);
          ctx.strokeRect(x0 + drawD * 0.72, y0 + 40 * scale, 12, drawH - 95 * scale);
          ctx.setLineDash([]);
          ctx.fillStyle = '#49d1ff';
          ctx.textAlign = 'center';
          ctx.fillText(\`SIDE · 外深 D = \${cabD} mm  |  虚线：侧板竖向加强筋 (首尾 50mm 激光打标)\`, W / 2, y0 - 18);
          ctx.fillText(\`H = \${cabH} mm\`, x0 + drawD / 2, y0 + drawH + 24);
        } else if (view === 'top') {
          const pad = 76;
          const scale = Math.min((W - pad * 2) / cabW, (H - pad * 2) / cabD);
          const drawW = cabW * scale;
          const drawD = cabD * scale;
          const x0 = (W - drawW) / 2;
          const y0 = (H - drawD) / 2;
          ctx.fillStyle = '#14223d';
          ctx.strokeStyle = '#7ba8e8';
          ctx.lineWidth = 2;
          ctx.fillRect(x0, y0, drawW, drawD);
          ctx.strokeRect(x0, y0, drawW, drawD);
          const leafDrawW = leafW * scale;
          ctx.fillStyle = '#49d1ff';
          ctx.fillRect(x0 + 35 * scale, y0 + drawD - 4, leafDrawW, 6);
          ctx.fillRect(x0 + drawW - 35 * scale - leafDrawW, y0 + drawD - 4, leafDrawW, 6);
          // Partition mullion zone
          ctx.fillStyle = '#1f365c';
          ctx.fillRect(x0 + 35 * scale + leafDrawW + 4, y0 + 18 * scale, 42 * scale, drawD - 36 * scale);
          ctx.strokeStyle = '#4ee2a1';
          ctx.strokeRect(x0 + 35 * scale + leafDrawW + 4, y0 + 18 * scale, 42 * scale, drawD - 36 * scale);
          ctx.fillStyle = '#49d1ff';
          ctx.textAlign = 'center';
          ctx.fillText(\`TOP · 包络 W \${cabW} × D \${cabD} mm（门朝下侧前方 · 中间双竖隔板带 3mm 角标）\`, W / 2, y0 - 18);
        } else if (view === 'iso') {
          const cx = W / 2 - 20;
          const cy = H - 72;
          const maxSpan = cabW + cabD + cabH * 0.7;
          const scale = Math.min(W, H) / Math.max(cabH * 1.15, maxSpan * 0.9);
          const iso = (x, y, z) => [
            cx + (x - z * 0.85) * 0.86 * scale,
            cy - (y + (x + z) * 0.32) * scale,
          ];
          const pts = {
            p000: iso(-cabW / 2, 0, 0),
            p100: iso(cabW / 2, 0, 0),
            p110: iso(cabW / 2, cabH, 0),
            p010: iso(-cabW / 2, cabH, 0),
            p001: iso(-cabW / 2, 0, cabD),
            p101: iso(cabW / 2, 0, cabD),
            p111: iso(cabW / 2, cabH, cabD),
            p011: iso(-cabW / 2, cabH, cabD),
          };
          const poly = (list, fill) => {
            ctx.beginPath();
            ctx.moveTo(list[0][0], list[0][1]);
            for (let i = 1; i < list.length; i += 1) ctx.lineTo(list[i][0], list[i][1]);
            ctx.closePath();
            ctx.fillStyle = fill;
            ctx.fill();
            ctx.strokeStyle = '#49d1ff';
            ctx.lineWidth = 1.6;
            ctx.stroke();
          };
          poly([pts.p010, pts.p110, pts.p111, pts.p011], '#1c2f52');
          poly([pts.p100, pts.p101, pts.p111, pts.p110], '#13233e');
          poly([pts.p000, pts.p100, pts.p110, pts.p010], '#192c4e');
          ctx.fillStyle = '#49d1ff';
          ctx.textAlign = 'center';
          ctx.fillText(\`ISO · 等轴测三维包络：\${cabW}W × \${cabH}H × \${cabD}D mm（L\${check.left.heights.length} / R\${check.right.heights.length}）\`, W / 2, 32);
        } else {
          // CRAFT DETAIL BLUEPRINT VIEW (4 Workshop Crafts Magnified)
          ctx.fillStyle = '#49d1ff';
          ctx.textAlign = 'left';
          ctx.font = 'bold 13px Consolas, monospace';
          ctx.fillText(\`WORKSHOP CRAFT BLUEPRINT · 4 项车间工艺详情（门宽=\${leafW.toFixed(1)}mm）\`, 28, 36);
          ctx.font = '11.5px Consolas, monospace';

          const drawCard = (x, y, w, h, title, accent) => {
            ctx.fillStyle = '#111d34';
            ctx.strokeStyle = '#253c66';
            ctx.lineWidth = 1.5;
            ctx.fillRect(x, y, w, h);
            ctx.strokeRect(x, y, w, h);
            ctx.fillStyle = accent;
            ctx.fillText(title, x + 14, y + 22);
          };

          // Panel 1: Craft 1 Left Semicircle vs Right Triangle Notches
          drawCard(28, 52, 384, 232, '[Craft 1] 左右门防呆识别缺口（5mm 铰链回折边中点）', '#4ee2a1');
          ctx.fillStyle = '#192b4c'; ctx.strokeStyle = '#7ba8e8'; ctx.lineWidth = 1.6;
          ctx.fillRect(62, 102, 118, 134); ctx.strokeRect(62, 102, 118, 134);
          // Semicircle notch on inner free edge
          ctx.fillStyle = '#0c1424'; ctx.strokeStyle = '#4ee2a1'; ctx.lineWidth = 2;
          ctx.beginPath(); ctx.arc(180, 169, 15, Math.PI * 0.5, Math.PI * 1.5); ctx.fill(); ctx.stroke();
          ctx.fillStyle = '#e6eefc'; ctx.fillText('L 左门 · R1.5 半圆', 66, 256);

          ctx.fillStyle = '#192b4c'; ctx.strokeStyle = '#7ba8e8'; ctx.lineWidth = 1.6;
          ctx.fillRect(256, 102, 118, 134); ctx.strokeRect(256, 102, 118, 134);
          // Triangle V notch on inner free edge
          ctx.fillStyle = '#0c1424'; ctx.strokeStyle = '#ffbd4a'; ctx.lineWidth = 2;
          ctx.beginPath(); ctx.moveTo(256, 154); ctx.lineTo(274, 169); ctx.lineTo(256, 184); ctx.closePath(); ctx.fill(); ctx.stroke();
          ctx.fillStyle = '#e6eefc'; ctx.fillText('R 右门 · 3×1.5 三角', 252, 256);

          // Panel 2: Craft 2 Upper Pin Hole 0.5 mm Inward Pre-bias
          drawCard(428, 52, 384, 232, '[Craft 2] 门上销孔内侧预偏 0.5mm（无需二次校形）', '#ffbd4a');
          ctx.strokeStyle = '#7ba8e8'; ctx.lineWidth = 1.5;
          ctx.strokeRect(468, 92, 304, 58); // Top flange
          ctx.strokeRect(468, 184, 304, 58); // Bottom flange
          ctx.beginPath(); ctx.arc(542, 121, 14, 0, Math.PI * 2); ctx.strokeStyle = '#ffbd4a'; ctx.stroke();
          ctx.beginPath(); ctx.arc(522, 213, 14, 0, Math.PI * 2); ctx.strokeStyle = '#4ee2a1'; ctx.stroke();
          ctx.setLineDash([4, 4]); ctx.strokeStyle = '#8aa2ca';
          ctx.beginPath(); ctx.moveTo(522, 82); ctx.lineTo(522, 252); ctx.stroke();
          ctx.setLineDash([]);
          ctx.fillStyle = '#ffbd4a'; ctx.fillText('上销孔 Ø10.2 · 距外边 10.5mm (内偏 0.5)', 568, 125);
          ctx.fillStyle = '#4ee2a1'; ctx.fillText('下销孔 Ø10.2 · 距外边 10.0mm (原轴)', 568, 217);

          // Panel 3: Craft 3 Shelf Locator Hole Removal
          drawCard(28, 300, 384, 232, '[Craft 3] 横层板取消 8 处 Ø2.0 定位孔', '#49d1ff');
          ctx.fillStyle = '#182b4c'; ctx.strokeStyle = '#7ba8e8';
          ctx.fillRect(58, 344, 324, 134); ctx.strokeRect(58, 344, 324, 134);
          ctx.strokeStyle = '#ff6b9d'; ctx.lineWidth = 1.5;
          [110, 180, 260, 330].forEach((hx) => {
            [374, 448].forEach((hy) => {
              ctx.beginPath(); ctx.arc(hx, hy, 5, 0, Math.PI * 2); ctx.stroke();
              ctx.beginPath(); ctx.moveTo(hx - 6, hy - 6); ctx.lineTo(hx + 6, hy + 6); ctx.stroke();
            });
          });
          // Yellow MARK lines replacing holes
          ctx.strokeStyle = '#ffbd4a'; ctx.lineWidth = 2.2;
          [[90, 366, 122, 366], [90, 384, 122, 384], [318, 366, 350, 366], [318, 384, 350, 384]].forEach(([x1, y1, x2, y2]) => {
            ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
          });
          ctx.fillStyle = '#e6eefc'; ctx.fillText('已修补 8×Ø2.0（红叉） · 改由两筋外端 3mm MARK 线定位', 56, 504);

          // Panel 4: Craft 4 Full Laser Welding MARK Layer
          drawCard(428, 300, 384, 232, '[Craft 4] 3D 打标草图 + 展开 DXF MARK 图层', '#ff6b9d');
          ctx.fillStyle = '#182b4c'; ctx.strokeStyle = '#7ba8e8'; ctx.lineWidth = 1.4;
          ctx.fillRect(460, 342, 186, 148); ctx.strokeRect(460, 342, 186, 148);
          // 3mm L corner marks + 4-R3 U hook + latches
          ctx.strokeStyle = '#ffbd4a'; ctx.lineWidth = 2;
          [[484, 362, 500, 362], [484, 362, 484, 378], [620, 362, 604, 362], [620, 362, 620, 378],
           [484, 470, 500, 470], [484, 470, 484, 454], [620, 470, 604, 470], [620, 470, 620, 454]].forEach(([x1, y1, x2, y2]) => {
            ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
          });
          ctx.strokeStyle = '#49d1ff';
          ctx.strokeRect(592, 396, 26, 40); // 4-R3 hook outline
          ctx.strokeRect(470, 352, 24, 20); // top latch
          ctx.strokeRect(470, 454, 24, 24); // bot latch
          // Stiffener arrow
          ctx.strokeStyle = '#4ee2a1'; ctx.fillStyle = '#4ee2a1';
          ctx.fillRect(676, 342, 108, 148);
          ctx.fillStyle = '#111d34'; ctx.fillRect(678, 344, 104, 144);
          ctx.beginPath(); ctx.moveTo(730, 430); ctx.lineTo(730, 372); ctx.lineTo(720, 384); ctx.moveTo(730, 372); ctx.lineTo(740, 384); ctx.stroke();
          ctx.fillStyle = '#4ee2a1'; ctx.font = 'bold 16px Microsoft YaHei'; ctx.fillText('上', 722, 456);
          ctx.font = '11px Consolas, monospace'; ctx.fillStyle = '#e6eefc';
          ctx.fillText('门板 3mm L标 / 4-R3 勾片 + 侧筋 ↑上', 458, 510);
        }
      }

      function updateStatusAndPreview() {
        const res = validateAll();
        const banner = el('layoutBanner');
        const badge = el('previewValidBadge');
        el('prepareSubmitBtn').disabled = !res.ok;

        if (res.ok) {
          banner.dataset.valid = 'true';
          banner.innerHTML = \`校验通过 · 左列 L\${state.columns.L.length}（合计 \${res.left.sumPitch.toFixed(2)} mm），右列 R\${state.columns.R.length}（合计 \${res.right.sumPitch.toFixed(2)} mm），门板单宽 \${res.doorLeafWidthMm.toFixed(2)} mm。\`;
          badge.className = 'badge ready';
          badge.textContent = '校验通过';
        } else {
          banner.dataset.valid = 'false';
          banner.innerHTML = '参数待修正：' + res.errors.map(escapeHtml).join('；');
          badge.className = 'badge failed';
          badge.textContent = '待修正';
        }

        const totalDoors = state.columns.L.length + state.columns.R.length;
        el('previewHeading').textContent = \`\${Number.isFinite(res.cabinet.widthMm) ? res.cabinet.widthMm : '--'} × \${Number.isFinite(res.cabinet.heightMm) ? res.cabinet.heightMm : '--'} × \${Number.isFinite(res.cabinet.depthMm) ? res.cabinet.depthMm : '--'} mm · \${totalDoors} 门\`;
        el('hudW').textContent = Number.isFinite(res.cabinet.widthMm) ? \`\${res.cabinet.widthMm} mm\` : '--';
        el('hudLeaf').textContent = Number.isFinite(res.doorLeafWidthMm) ? \`\${res.doorLeafWidthMm.toFixed(2)} mm\` : '--';
        el('hudDoors').textContent = \`\${totalDoors} 门 (L\${state.columns.L.length}/R\${state.columns.R.length})\`;
        el('hudBudget').textContent = Number.isFinite(res.cabinet.heightMm) ? \`\${(res.cabinet.heightMm - 87).toFixed(2)} mm\` : '--';
        el('confirmBox').hidden = true;
        drawCabinet();
      }

      function updateUI() {
        renderDoorRows('L');
        renderDoorRows('R');
        updateStatusAndPreview();
      }

      function buildPayload() {
        const res = validateAll();
        return {
          customerRequirementReference: el('vtRef').value.trim(),
          widthInputMode: state.widthInputMode,
          requestedWidthMm: Number(state.widthInputValue),
          cabinet: {
            widthMm: res.cabinet.widthMm,
            heightMm: res.cabinet.heightMm,
            depthMm: res.cabinet.depthMm,
          },
          allocationMode: state.allocationMode,
          columns: [
            {
              side: 'L',
              doors: state.columns.L.map((d, i) => state.allocationMode === 'height_mm'
                ? { heightMm: Number((res.left.heights[i] ?? d.heightMm).toFixed(6)) }
                : { heightUnits: Number(d.heightUnits ?? 1) }),
            },
            {
              side: 'R',
              doors: state.columns.R.map((d, i) => state.allocationMode === 'height_mm'
                ? { heightMm: Number((res.right.heights[i] ?? d.heightMm).toFixed(6)) }
                : { heightUnits: Number(d.heightUnits ?? 1) }),
            },
          ],
        };
      }

      function renderTasks() {
        const container = el('vtTaskList');
        const q = state.search.trim().toLowerCase();
        const filtered = state.tasks.filter((task) => {
          if (state.filter === 'ready' && task.status !== 'ready') return false;
          if (state.filter === 'active' && !['queued', 'running', 'cancel_requested'].includes(task.status)) return false;
          if (state.filter === 'failed' && task.status !== 'failed') return false;
          if (state.filter === 'cancelled' && task.status !== 'cancelled') return false;
          if (!q) return true;
          const hay = [
            task.id,
            task.request?.customerRequirementReference || '',
            \`\${task.request?.cabinet?.widthMm}x\${task.request?.cabinet?.heightMm}x\${task.request?.cabinet?.depthMm}\`,
            task.publication?.zipFileName || '',
          ].join(' ').toLowerCase();
          return hay.includes(q);
        });

        if (!filtered.length) {
          container.innerHTML = '<div class="empty-state">暂无匹配的任务记录，可在上方配置规格并提交到队列。</div>';
          return;
        }

        container.innerHTML = filtered.map((task) => {
          const cab = task.request?.cabinet || {};
          const counts = task.layoutSummary?.columnDoorCounts || [0, 0];
          const pub = task.publication;
          const canCancel = task.status === 'queued' || task.status === 'running';
          const canRetry = task.status === 'failed' || task.status === 'cancelled';
          const canDelete = !['queued', 'running', 'cancel_requested'].includes(task.status);
          return \`
            <article class="task-item" data-task-id="\${escapeHtml(task.id)}" data-task-status="\${escapeHtml(task.status)}">
              <div class="task-top">
                <div class="task-title">
                  <strong>\${escapeHtml(task.id)}</strong>
                  <span class="badge \${escapeHtml(task.status)}">\${escapeHtml(STATUS_LABELS[task.status] || task.status)}</span>
                  \${task.request?.customerRequirementReference ? \`<span class="muted">\${escapeHtml(task.request.customerRequirementReference)}</span>\` : ''}
                </div>
                <span class="muted" style="font-size:11px;">更新于 \${escapeHtml(new Date(task.updatedAt || task.createdAt).toLocaleString('zh-CN'))}</span>
              </div>
              <div class="task-specs">
                规格: \${cab.widthMm}W × \${cab.heightMm}H × \${cab.depthMm}D mm · 门叶宽 \${Number(task.layoutSummary?.doorLeafWidthMm || 0).toFixed(2)} mm · L\${counts[0]} / R\${counts[1]}<br />
                进度: \${escapeHtml(task.progressDetail || '')}
              </div>
              \${pub ? \`
                <div class="metrics-grid">
                  <div><span>3D 外廓与 BOM</span><strong>\${Number(pub.measuredEnvelopeMm?.widthMm || 0).toFixed(1)}×\${Number(pub.measuredEnvelopeMm?.heightMm || 0).toFixed(1)}×\${Number(pub.measuredEnvelopeMm?.depthMm || 0).toFixed(1)} · \${pub.bomUniquePartCount ?? '--'}种/\${pub.bomTotalInstanceCount ?? '--'}件</strong></div>
                  <div><span>干涉与上销预偏</span><strong>结构干涉=\${pub.structuralCollisionCount ?? 0} · 上偏豁免=\${pub.approvedUpperPinPrebiasCount ?? 0}</strong></div>
                  <div><span>DXF 装机全检</span><strong>\${pub.dxfAuditPassCount}/\${pub.dxfAuditTotalCount} 通过 (0断链/0样条)</strong></div>
                  <div><span>2D 工程图 (总装/门板)</span><strong>\${pub.drawingSlddrwCount ?? 0}×SLDDRW · \${pub.drawingPdfCount ?? 0}×PDF</strong></div>
                  <div><span>MARK 打标与预孔</span><strong>MARK=\${pub.totalMarkEntities ?? 0} (焊接\${pub.totalProductionMarkEntities ?? 0}) · 去内孔=\${pub.removedFastenerBores ?? 0}</strong></div>
                </div>
                <div class="craft-badges">
                  <span class="craft-pill">C1 左R1.5半圆 / 右3×1.5三角</span>
                  <span class="craft-pill">C2 上销孔内偏 0.5mm</span>
                  <span class="craft-pill">C3 横层板去 Ø2.0 定位孔</span>
                  <span class="craft-pill">C4 3D+DXF 焊接定位打标</span>
                  <span class="craft-pill">2D 总装多视图 + 门板展开图/孔表</span>
                </div>
              \` : ''}
              \${task.error ? \`<div style="color:var(--risk); font-size:12px;">错误：\${escapeHtml(task.error.message || '')}</div>\` : ''}
              <div class="task-actions">
                \${task.status === 'ready' && pub ? \`<a class="button" href="${VARIABLE_TRIAL_PORTAL_ROUTE}/task/\${encodeURIComponent(task.id)}/download">下载 3D+DXF+2D工程图+BOM (.zip)</a>\` : ''}
                \${task.status === 'ready' ? \`<button type="button" class="secondary" data-act="report" data-id="\${escapeHtml(task.id)}">查看审计详情</button>\` : ''}
                \${canCancel ? \`<button type="button" class="secondary" data-act="cancel" data-id="\${escapeHtml(task.id)}">取消任务</button>\` : ''}
                \${canRetry ? \`<button type="button" class="secondary" data-act="retry" data-id="\${escapeHtml(task.id)}">重新排队</button>\` : ''}
                \${canDelete ? \`<button type="button" class="danger" data-act="delete" data-id="\${escapeHtml(task.id)}">删除</button>\` : ''}
              </div>
            </article>
          \`;
        }).join('');
      }

      async function refreshTasks() {
        try {
          const resp = await fetch('${VARIABLE_TRIAL_PORTAL_ROUTE}/tasks.json', { cache: 'no-store' });
          if (!resp.ok) throw new Error('HTTP ' + resp.status);
          const data = await resp.json();
          state.tasks = Array.isArray(data.tasks) ? data.tasks : [];
          const pill = el('cadStatusPill');
          if (data.cadBusy) {
            pill.className = 'cad-pill busy';
            pill.textContent = \`CAD 占用中：\${(data.blockers || []).join(' / ') || '构建任务运行中'}\`;
          } else {
            pill.className = 'cad-pill';
            pill.textContent = 'CAD 工作站状态：空闲 · 可随时接单';
          }
          el('taskRefreshNote').textContent = \`上次刷新 \${new Date().toLocaleTimeString('zh-CN')}（共 \${state.tasks.length} 个任务）\`;
          renderTasks();
        } catch (err) {
          el('taskRefreshNote').textContent = '刷新失败：' + (err?.message || err);
        }
      }

      el('vtWidthMode').addEventListener('change', (e) => {
        const prevW = computeOuterWidthMm();
        state.widthInputMode = e.target.value;
        if (Number.isFinite(prevW)) {
          state.widthInputValue = state.widthInputMode === 'installed_door_panel_width'
            ? Number(((prevW - 126) / 2).toFixed(2))
            : Math.round(prevW);
        }
        syncInputsFromState();
        updateStatusAndPreview();
      });

      el('vtWidthInput').addEventListener('input', (e) => {
        state.widthInputValue = Number(e.target.value);
        updateStatusAndPreview();
      });
      el('vtHeightInput').addEventListener('input', (e) => {
        state.heightMm = Number(e.target.value);
        refreshComputedLabels();
        updateStatusAndPreview();
      });
      el('vtDepthInput').addEventListener('input', (e) => {
        state.depthMm = Number(e.target.value);
        updateStatusAndPreview();
      });
      el('leftDoorCount').addEventListener('change', (e) => setColumnDoorCount('L', e.target.value));
      el('rightDoorCount').addEventListener('change', (e) => setColumnDoorCount('R', e.target.value));

      el('modeRatioBtn').addEventListener('click', () => {
        if (state.allocationMode === 'ratio') return;
        const evalL = computeColumnDoors('L', Number(state.heightMm));
        const evalR = computeColumnDoors('R', Number(state.heightMm));
        state.columns.L.forEach((d, i) => { d.heightUnits = Number(((evalL.heights[i] || 100) + 7).toFixed(2)); });
        state.columns.R.forEach((d, i) => { d.heightUnits = Number(((evalR.heights[i] || 100) + 7).toFixed(2)); });
        state.allocationMode = 'ratio';
        syncInputsFromState();
        updateUI();
      });

      el('modeMmBtn').addEventListener('click', () => {
        if (state.allocationMode === 'height_mm') return;
        const evalL = computeColumnDoors('L', Number(state.heightMm));
        const evalR = computeColumnDoors('R', Number(state.heightMm));
        state.columns.L.forEach((d, i) => { d.heightMm = Number((evalL.heights[i] || 100).toFixed(3)); });
        state.columns.R.forEach((d, i) => { d.heightMm = Number((evalR.heights[i] || 100).toFixed(3)); });
        state.allocationMode = 'height_mm';
        syncInputsFromState();
        updateUI();
      });

      document.querySelectorAll('[data-equalize]').forEach((btn) => {
        btn.addEventListener('click', () => setColumnDoorCount(btn.dataset.equalize, state.columns[btn.dataset.equalize].length));
      });
      document.querySelectorAll('[data-balance]').forEach((btn) => {
        btn.addEventListener('click', () => {
          const side = btn.dataset.balance;
          if (state.allocationMode !== 'height_mm') return;
          const budget = Number(state.heightMm) - LIMITS.verticalAllowanceMm;
          const cols = state.columns[side];
          const lowerPitch = cols.slice(0, -1).reduce((s, d) => s + Number(d.heightMm || 100) + LIMITS.doorGapMm, 0);
          const topMm = Number((budget - lowerPitch - LIMITS.doorGapMm).toFixed(4));
          cols[cols.length - 1].heightMm = Math.max(100, topMm);
          renderDoorRows(side);
          updateStatusAndPreview();
        });
      });

      document.querySelectorAll('[data-vt-view]').forEach((btn) => {
        btn.addEventListener('click', () => {
          state.view = btn.dataset.vtView;
          document.querySelectorAll('[data-vt-view]').forEach((b) => b.classList.toggle('active', b === btn));
          drawCabinet();
        });
      });

      el('prepareSubmitBtn').addEventListener('click', () => {
        const res = validateAll();
        if (!res.ok) return;
        el('confirmGrid').innerHTML = \`
          <div><span>外廓规格</span><strong>\${res.cabinet.widthMm} × \${res.cabinet.heightMm} × \${res.cabinet.depthMm} mm</strong></div>
          <div><span>门格排布</span><strong>左 \${res.left.heights.length} 门 / 右 \${res.right.heights.length} 门（门叶宽 \${res.doorLeafWidthMm.toFixed(2)} mm）</strong></div>
          <div><span>生产工艺</span><strong>防呆缺口 + 上销内偏0.5 + 去Ø2孔 + 激光打标</strong></div>
          <div><span>交付内容</span><strong>3D 装配 + DXF + 2D 工程图(SLDDRW/PDF) + BOM</strong></div>
        \`;
        el('confirmBox').hidden = false;
      });

      el('cancelConfirmBtn').addEventListener('click', () => { el('confirmBox').hidden = true; });

      el('confirmSubmitBtn').addEventListener('click', async () => {
        const btn = el('confirmSubmitBtn');
        btn.disabled = true;
        el('submitFeedback').textContent = '正在提交任务…';
        try {
          const resp = await fetch('${VARIABLE_TRIAL_PORTAL_ROUTE}/task', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(buildPayload()),
          });
          const result = await resp.json();
          if (!resp.ok) throw new Error(result.message || result.error || '提交失败');
          el('confirmBox').hidden = true;
          el('submitFeedback').textContent = result.deduplicated
            ? \`命中已有同规格任务 \${result.taskId}（可直接复用）\`
            : \`已创建任务 \${result.taskId}，已加入构建队列\`;
          await refreshTasks();
        } catch (err) {
          el('submitFeedback').textContent = '提交失败：' + (err?.message || err);
        } finally {
          btn.disabled = false;
        }
      });

      el('taskFilterPills').addEventListener('click', (e) => {
        const btn = e.target.closest('button[data-filter]');
        if (!btn) return;
        state.filter = btn.dataset.filter;
        el('taskFilterPills').querySelectorAll('button').forEach((b) => b.classList.toggle('active', b === btn));
        renderTasks();
      });
      el('taskSearch').addEventListener('input', (e) => {
        state.search = e.target.value;
        renderTasks();
      });

      el('vtTaskList').addEventListener('click', async (e) => {
        const btn = e.target.closest('button[data-act]');
        if (!btn) return;
        const id = btn.dataset.id;
        const act = btn.dataset.act;
        if (act === 'report') {
          const resp = await fetch(\`${VARIABLE_TRIAL_PORTAL_ROUTE}/task/\${encodeURIComponent(id)}/report.json\`);
          const txt = await resp.text();
          el('reportModalTitle').textContent = \`工程审计报告 · \${id}\`;
          el('reportModalBody').textContent = txt;
          el('reportModal').showModal();
          return;
        }
        btn.disabled = true;
        try {
          const resp = await fetch(\`${VARIABLE_TRIAL_PORTAL_ROUTE}/task/\${encodeURIComponent(id)}/\${act}\`, { method: 'POST' });
          if (!resp.ok) {
            const payload = await resp.json().catch(() => ({}));
            alert(payload.error || '操作失败');
          }
          await refreshTasks();
        } finally {
          btn.disabled = false;
        }
      });

      el('closeReportModal').addEventListener('click', () => el('reportModal').close());

      renderPresets();
      syncInputsFromState();
      updateUI();
      refreshTasks();
      setInterval(refreshTasks, 3000);
    })();
  </script>
</body>
</html>`
}

export function createVariableTrialPortalRoutes({
  rootDir,
  dataDir = DEFAULT_VARIABLE_TRIAL_TASKS_DIR,
  sendHtml,
  sendJson,
  readJsonBody,
  htmlEscape,
  attachmentContentDisposition,
  processReviewEnabled = () => false,
  processReviewLabel = '',
}) {
  const store = createVariableTrialTaskStore({ dataDir })

  return async function handleVariableTrialRoutes(request, response, url, username) {
    if (request.method === 'GET' && url.pathname === VARIABLE_TRIAL_PORTAL_ROUTE) {
      sendHtml(response, 200, renderVariableTrialPage({
        username,
        htmlEscape,
        processReviewEnabled: Boolean(typeof processReviewEnabled === 'function' ? processReviewEnabled() : processReviewEnabled),
        processReviewLabel,
      }))
      return true
    }

    if (request.method === 'GET' && url.pathname === `${VARIABLE_TRIAL_PORTAL_ROUTE}/presets.json`) {
      sendJson(response, 200, { presets: PORTAL_PRESETS, limits: PORTAL_DIMENSION_LIMITS })
      return true
    }

    if (request.method === 'GET' && url.pathname === `${VARIABLE_TRIAL_PORTAL_ROUTE}/tasks.json`) {
      const queueState = readFastCadQueueState(rootDir)
      sendJson(response, 200, {
        tasks: store.list({ username }),
        cadBusy: queueState.busy,
        blockers: queueState.blockers,
      })
      return true
    }

    if (request.method === 'POST' && url.pathname === `${VARIABLE_TRIAL_PORTAL_ROUTE}/task`) {
      let body
      try {
        body = await readJsonBody(request)
      } catch (err) {
        sendJson(response, err.statusCode || 400, { error: 'invalid_json', message: err.message })
        return true
      }

      const normalized = normalizeVariableTrialPortalRequest(body)
      if (!normalized.ok) {
        sendJson(response, 400, { error: normalized.code, message: normalized.message })
        return true
      }

      if (!body.forceNew) {
        const reusable = store.findReusableMatch({ username, requestFingerprint: normalized.requestFingerprint })
        if (reusable) {
          sendJson(response, 200, {
            taskId: reusable.id,
            status: reusable.status,
            deduplicated: true,
            layoutSummary: reusable.layoutSummary,
            task: reusable,
          })
          return true
        }
      }

      const task = store.create({ username, ...normalized })
      sendJson(response, 201, {
        taskId: task.id,
        status: task.status,
        deduplicated: false,
        layoutSummary: task.layoutSummary,
        task,
      })
      return true
    }

    const actionMatch = url.pathname.match(/^\/indoor-v2\/task\/([A-Za-z0-9._-]+)\/(cancel|retry|delete|report\.json|download)$/)
    if (!actionMatch) {
      sendJson(response, 404, { error: 'not found' })
      return true
    }

    const [, taskId, endpoint] = actionMatch
    if (!isSafeTaskId(taskId)) {
      sendJson(response, 400, { error: 'invalid task id' })
      return true
    }
    const existing = store.read(taskId)
    if (!existing || existing.username !== username) {
      sendJson(response, 404, { error: '任务不存在或无权访问。' })
      return true
    }

    if (request.method === 'POST' && endpoint === 'cancel') {
      const outcome = store.requestCancel(taskId)
      sendJson(response, outcome.ok ? 200 : outcome.status, outcome.ok ? outcome : { error: outcome.error })
      return true
    }

    if (request.method === 'POST' && endpoint === 'retry') {
      const outcome = store.retryTask(taskId)
      sendJson(response, outcome.ok ? 200 : outcome.status, outcome.ok ? outcome : { error: outcome.error })
      return true
    }

    if (request.method === 'POST' && endpoint === 'delete') {
      const outcome = store.removeTask(taskId)
      sendJson(response, outcome.ok ? 200 : outcome.status, outcome.ok ? outcome : { error: outcome.error })
      return true
    }

    if (request.method === 'GET' && endpoint === 'report.json') {
      const reportPath = existing.publication?.reportPath
      if (existing.status !== 'ready' || !reportPath || !isPathInsideRoot(reportPath, store.taskDir(taskId)) || !existsSync(reportPath)) {
        sendJson(response, 404, { error: '报告尚未生成。' })
        return true
      }
      response.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' })
      createReadStream(reportPath).pipe(response)
      return true
    }

    if (request.method === 'GET' && endpoint === 'download') {
      const zipPath = existing.publication?.zipPath
      if (existing.status !== 'ready' || !zipPath || !isPathInsideRoot(zipPath, store.taskDir(taskId)) || !existsSync(zipPath)) {
        sendJson(response, 409, { error: '交付包尚未通过全部验证或文件不存在。' })
        return true
      }
      const actualSha = createHash('sha256').update(readFileSync(zipPath)).digest('hex').toUpperCase()
      if (actualSha !== String(existing.publication.sha256 || '').toUpperCase()) {
        sendJson(response, 500, { error: '交付包 SHA-256 校验不一致，已阻止下载。' })
        return true
      }
      const stat = statSync(zipPath)
      response.writeHead(200, {
        'Content-Type': 'application/zip',
        'Content-Length': stat.size,
        'Content-Disposition': attachmentContentDisposition(existing.publication.zipFileName, `${taskId}.zip`),
        'Cache-Control': 'private, no-store',
      })
      createReadStream(zipPath).pipe(response)
      return true
    }

    sendJson(response, 405, { error: 'method not allowed' })
    return true
  }
}
