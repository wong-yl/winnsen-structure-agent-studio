import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'

const requireFromWeb = createRequire('file:///D:/Winnsen_Structure_Agent_Studio/apps/web/package.json')
const { chromium } = requireFromWeb('playwright')

const desktop = 'C:\\Users\\Administrator\\Desktop'
const outPdf = path.join(desktop, '16029_800W_gold_variable_project_flow_20260528.pdf')
const outHtml = path.join(desktop, '16029_800W_gold_variable_project_flow_20260528.html')

const nodes = [
  { id: 'A', text: '唯一当前路线\n800W x 1917H x 550D\nW337 / gap 2+3+2', x: 44, y: 92, w: 290, h: 92, tone: 'source' },
  { id: 'B', text: '只从 gold/source 出发\n源 STEP、层板焊件、前框、五金模板', x: 415, y: 92, w: 330, h: 92, tone: 'source' },
  { id: 'C', text: '统一生成入口\nvariant_token + row_units', x: 826, y: 92, w: 270, h: 92, tone: 'source' },

  { id: 'D', text: 'LMS\n大 6/12\n中 4/12\n小 2/12', x: 82, y: 275, w: 240, h: 122, tone: 'variant' },
  { id: 'E', text: 'SML\n小 2/12\n中 4/12\n大 6/12', x: 450, y: 275, w: 240, h: 122, tone: 'variant' },
  { id: 'F', text: 'DUAL\n合并 LMS + SML\n统一转发和归档', x: 815, y: 275, w: 260, h: 122, tone: 'variant' },

  { id: 'G', text: '每组必须产出\nSTEP / 自审图\nverify CSV / model gate / bbox gate', x: 74, y: 492, w: 332, h: 118, tone: 'gate' },
  { id: 'H', text: '内部闸门\n模型完整性\n门序、五金计数、层板关系', x: 454, y: 492, w: 290, h: 118, tone: 'gate' },
  { id: 'I', text: 'STEP bbox 闸门\n只允许当前外形边界\n不放行历史试错数据', x: 806, y: 492, w: 290, h: 118, tone: 'gate' },

  { id: 'J', text: '审核登录台\n只显示 LMS / SML / DUAL\n账号登录后下载和反馈', x: 86, y: 718, w: 322, h: 112, tone: 'review' },
  { id: 'K', text: '工程师看到的是审核包\n不是截图猜结构\n不是轻量占位包', x: 452, y: 718, w: 300, h: 112, tone: 'review' },
  { id: 'L', text: '当前 CAD 主线\nSolidWorks 2020 打开核实\n其他软件不作为主线', x: 820, y: 718, w: 286, h: 112, tone: 'review' },
]

const byId = Object.fromEntries(nodes.map((node) => [node.id, node]))
const edges = [
  ['A', 'B'],
  ['B', 'C'],
  ['C', 'D'],
  ['C', 'E'],
  ['C', 'F'],
  ['D', 'G'],
  ['E', 'G'],
  ['F', 'G'],
  ['G', 'H'],
  ['H', 'I'],
  ['I', 'J'],
  ['J', 'K'],
  ['K', 'L'],
]

const palette = {
  source: ['#eef5ff', '#1d4f91'],
  variant: ['#eef9f3', '#1c7a4d'],
  gate: ['#fff7e6', '#9a6800'],
  review: ['#f2f0ff', '#5742a2'],
}

function center(node) {
  return { x: node.x + node.w / 2, y: node.y + node.h / 2 }
}

function edgePoints(a, b) {
  const source = byId[a]
  const target = byId[b]
  const sourceCenter = center(source)
  const targetCenter = center(target)
  const dx = targetCenter.x - sourceCenter.x
  const dy = targetCenter.y - sourceCenter.y

  if (Math.abs(dx) > Math.abs(dy)) {
    return {
      x1: dx > 0 ? source.x + source.w : source.x,
      y1: sourceCenter.y,
      x2: dx > 0 ? target.x : target.x + target.w,
      y2: targetCenter.y,
    }
  }

  return {
    x1: sourceCenter.x,
    y1: dy > 0 ? source.y + source.h : source.y,
    x2: targetCenter.x,
    y2: dy > 0 ? target.y : target.y + target.h,
  }
}

function escapeXml(value) {
  return value.replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;' })[char])
}

function textLines(text) {
  return text
    .split('\n')
    .map((line, index) => `<tspan x="0" dy="${index === 0 ? 0 : 22}">${escapeXml(line)}</tspan>`)
    .join('')
}

const svgEdges = edges
  .map(([a, b]) => {
    const point = edgePoints(a, b)
    return `<line x1="${point.x1}" y1="${point.y1}" x2="${point.x2}" y2="${point.y2}" class="edge" marker-end="url(#arrow)"/>`
  })
  .join('\n')

const svgNodes = nodes
  .map((node) => {
    const [fill, stroke] = palette[node.tone]
    return `<g class="node"><rect x="${node.x}" y="${node.y}" width="${node.w}" height="${node.h}" rx="8" fill="${fill}" stroke="${stroke}"/><text transform="translate(${node.x + node.w / 2},${node.y + 30})" class="node-text" text-anchor="middle">${textLines(node.text)}</text></g>`
  })
  .join('\n')

const html = `<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8" />
<title>16029 800W gold-variable 项目路线图</title>
<style>
  @page { size: A3 landscape; margin: 12mm; }
  body { margin: 0; font-family: "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; color: #14213d; background: #ffffff; }
  .page { width: 100%; box-sizing: border-box; }
  .header { display: flex; justify-content: space-between; align-items: flex-end; border-bottom: 2px solid #dbe5f2; padding-bottom: 12px; margin-bottom: 18px; gap: 24px; }
  h1 { font-size: 28px; margin: 0; letter-spacing: 0; }
  .sub { font-size: 14px; color: #53657c; margin-top: 7px; }
  .badge { font-size: 13px; border: 1px solid #b7c6da; border-radius: 8px; padding: 8px 12px; color: #28476d; background: #f7fbff; white-space: nowrap; }
  svg { width: 100%; height: auto; display: block; }
  .edge { stroke: #7b8aa0; stroke-width: 2.2; fill: none; }
  .node rect { stroke-width: 2.2; filter: drop-shadow(0 2px 2px rgba(30, 50, 80, 0.12)); }
  .node-text { font-size: 15px; font-weight: 700; fill: #162033; }
  .legend { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; margin-top: 16px; font-size: 12px; color: #4d5f75; }
  .legend span { display: inline-block; width: 12px; height: 12px; border-radius: 3px; margin-right: 6px; vertical-align: -2px; border: 1px solid #8aa; }
  .foot { margin-top: 12px; font-size: 12px; color: #64748b; display: flex; justify-content: space-between; border-top: 1px solid #e3eaf3; padding-top: 8px; }
</style>
</head>
<body>
<div class="page">
  <div class="header">
    <div>
      <h1>16029 800W gold-variable 项目路线图</h1>
      <div class="sub">目标：把 LMS、SML 和 DUAL 三组完整审核包交给结构工程师继续审核。</div>
    </div>
    <div class="badge">当前阶段：800W gold-variable 双方案审核</div>
  </div>
  <svg viewBox="0 0 1140 870" role="img" aria-label="16029 800W gold-variable 项目路线图">
    <defs>
      <marker id="arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth">
        <path d="M0,0 L0,6 L9,3 z" fill="#7b8aa0" />
      </marker>
    </defs>
    ${svgEdges}
    ${svgNodes}
  </svg>
  <div class="legend">
    <div><span style="background:#eef5ff;border-color:#1d4f91"></span>源头：当前唯一尺寸、源文件和生成入口</div>
    <div><span style="background:#eef9f3;border-color:#1c7a4d"></span>变体：LMS / SML / DUAL 三组审核对象</div>
    <div><span style="background:#fff7e6;border-color:#9a6800"></span>闸门：model gate 和 STEP bbox gate 先通过</div>
    <div><span style="background:#f2f0ff;border-color:#5742a2"></span>交付：登录台只给工程师完整审核包</div>
  </div>
  <div class="foot">
    <div>防呆原则：每次只动 variant_token 和 row_units。</div>
    <div>生成日期：2026-05-28</div>
  </div>
</div>
</body>
</html>`

fs.writeFileSync(outHtml, html, 'utf8')

const browserExecutable = [
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
].find((candidate) => fs.existsSync(candidate))

const browser = await chromium.launch({
  headless: true,
  ...(browserExecutable ? { executablePath: browserExecutable } : {}),
})
const page = await browser.newPage({ viewport: { width: 1600, height: 1050 }, deviceScaleFactor: 1 })
await page.goto(`file:///${outHtml.replace(/\\/g, '/')}`, { waitUntil: 'networkidle' })
await page.pdf({ path: outPdf, printBackground: true, preferCSSPageSize: true })
await browser.close()

const stat = fs.statSync(outPdf)
console.log(JSON.stringify({ outPdf, outHtml, bytes: stat.size }, null, 2))
