import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

function parseArgs(argv) {
  const options = {
    input: '',
    out: '',
    expectedWidthMm: null,
    expectedHeightMm: null,
    toleranceMm: 0.25,
  }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    const next = () => argv[++index] || ''
    if (arg === '--input') options.input = next()
    else if (arg === '--out') options.out = next()
    else if (arg === '--expected-width-mm') options.expectedWidthMm = Number(next())
    else if (arg === '--expected-height-mm') options.expectedHeightMm = Number(next())
    else if (arg === '--tolerance-mm') options.toleranceMm = Number(next())
  }
  return options
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function round(value) {
  return Number.isFinite(value) ? Math.round(value * 1000) / 1000 : null
}

function addPoint(points, x, y) {
  if (Number.isFinite(x) && Number.isFinite(y)) points.push([x, y])
}

function normalizeDegrees(value) {
  if (!Number.isFinite(value)) return null
  const normalized = value % 360
  return normalized < 0 ? normalized + 360 : normalized
}

function degreesToRadians(value) {
  return (value * Math.PI) / 180
}

function angleWithinArc(angle, start, end) {
  const a = normalizeDegrees(angle)
  const s = normalizeDegrees(start)
  const e = normalizeDegrees(end)
  if (a == null || s == null || e == null) return false
  if (s <= e) return a >= s - 1e-9 && a <= e + 1e-9
  return a >= s - 1e-9 || a <= e + 1e-9
}

function addArcPoint(points, cx, cy, r, angle) {
  const radians = degreesToRadians(angle)
  addPoint(points, cx + r * Math.cos(radians), cy + r * Math.sin(radians))
}

function addArcBounds(points, cx, cy, r, start, end) {
  addArcPoint(points, cx, cy, r, start)
  addArcPoint(points, cx, cy, r, end)
  for (const angle of [0, 90, 180, 270]) {
    if (angleWithinArc(angle, start, end)) addArcPoint(points, cx, cy, r, angle)
  }
}

function bboxFromPoints(points) {
  if (!points.length) return null
  let xmin = points[0][0]
  let ymin = points[0][1]
  let xmax = points[0][0]
  let ymax = points[0][1]
  for (const [x, y] of points) {
    xmin = Math.min(xmin, x)
    ymin = Math.min(ymin, y)
    xmax = Math.max(xmax, x)
    ymax = Math.max(ymax, y)
  }
  return {
    xmin_mm: round(xmin),
    ymin_mm: round(ymin),
    xmax_mm: round(xmax),
    ymax_mm: round(ymax),
    width_mm: round(xmax - xmin),
    height_mm: round(ymax - ymin),
  }
}

function entityPairs(lines) {
  const pairs = []
  for (let index = 0; index + 1 < lines.length; index += 2) {
    pairs.push([String(lines[index]).trim(), String(lines[index + 1]).trim()])
  }
  return pairs
}

function parseEntity(startIndex, pairs) {
  const type = pairs[startIndex][1]
  const values = {}
  let index = startIndex + 1
  while (index < pairs.length) {
    const [code, value] = pairs[index]
    if (code === '0') break
    if (!values[code]) values[code] = []
    values[code].push(value)
    index += 1
  }
  return { type, values, nextIndex: index }
}

function firstNumber(values, code) {
  return asNumber(values?.[code]?.[0])
}

function parseDxf(path) {
  const text = readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
  const lines = text.split(/\r?\n/)
  const pairs = entityPairs(lines)
  const counts = {}
  const points = []
  let inEntities = false

  for (let index = 0; index < pairs.length;) {
    const [code, value] = pairs[index]
    if (code === '0' && value === 'SECTION' && pairs[index + 1]?.[0] === '2' && pairs[index + 1]?.[1] === 'ENTITIES') {
      inEntities = true
      index += 2
      continue
    }
    if (inEntities && code === '0' && value === 'ENDSEC') {
      inEntities = false
      index += 1
      continue
    }
    if (!inEntities || code !== '0') {
      index += 1
      continue
    }

    const entity = parseEntity(index, pairs)
    counts[entity.type] = (counts[entity.type] || 0) + 1
    const values = entity.values
    if (entity.type === 'LINE') {
      addPoint(points, firstNumber(values, '10'), firstNumber(values, '20'))
      addPoint(points, firstNumber(values, '11'), firstNumber(values, '21'))
    } else if (entity.type === 'CIRCLE') {
      const cx = firstNumber(values, '10')
      const cy = firstNumber(values, '20')
      const r = firstNumber(values, '40')
      if (Number.isFinite(cx) && Number.isFinite(cy) && Number.isFinite(r)) {
        addPoint(points, cx - r, cy - r)
        addPoint(points, cx + r, cy + r)
      }
    } else if (entity.type === 'ARC') {
      const cx = firstNumber(values, '10')
      const cy = firstNumber(values, '20')
      const r = firstNumber(values, '40')
      const start = firstNumber(values, '50')
      const end = firstNumber(values, '51')
      if (Number.isFinite(cx) && Number.isFinite(cy) && Number.isFinite(r) && Number.isFinite(start) && Number.isFinite(end)) {
        addArcBounds(points, cx, cy, r, start, end)
      } else if (Number.isFinite(cx) && Number.isFinite(cy) && Number.isFinite(r)) {
        addPoint(points, cx - r, cy - r)
        addPoint(points, cx + r, cy + r)
      }
    } else if (entity.type === 'LWPOLYLINE' || entity.type === 'POLYLINE') {
      const xs = values['10'] || []
      const ys = values['20'] || []
      for (let i = 0; i < Math.min(xs.length, ys.length); i += 1) addPoint(points, asNumber(xs[i]), asNumber(ys[i]))
    }
    index = entity.nextIndex
  }

  return {
    entity_counts: counts,
    entity_count_total: Object.values(counts).reduce((total, count) => total + count, 0),
    bbox_mm: bboxFromPoints(points),
  }
}

function almostEqual(a, b, tolerance) {
  return Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) <= tolerance
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  if (!options.input) throw new Error('--input is required')
  const inputPath = resolve(ROOT, options.input)
  const exists = existsSync(inputPath)
  const parsed = exists ? parseDxf(inputPath) : { entity_counts: {}, entity_count_total: 0, bbox_mm: null }
  const bbox = parsed.bbox_mm
  const widthOk = options.expectedWidthMm == null || almostEqual(bbox?.width_mm, options.expectedWidthMm, options.toleranceMm)
  const heightOk = options.expectedHeightMm == null || almostEqual(bbox?.height_mm, options.expectedHeightMm, options.toleranceMm)
  const report = {
    schema: 'winnsen.locker16029.flat_dxf_inspection.v1',
    input: options.input,
    exists,
    status: exists && parsed.entity_count_total > 0 && widthOk && heightOk ? 'PASS' : 'FAIL',
    expected_width_mm: options.expectedWidthMm,
    expected_height_mm: options.expectedHeightMm,
    tolerance_mm: options.toleranceMm,
    width_ok: widthOk,
    height_ok: heightOk,
    ...parsed,
  }
  const text = `${JSON.stringify(report, null, 2)}\n`
  if (options.out) writeFileSync(resolve(ROOT, options.out), text, 'utf8')
  process.stdout.write(text)
  if (report.status !== 'PASS') process.exit(1)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) {
  main()
}
