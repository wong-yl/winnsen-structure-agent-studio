import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

function parseArgs(argv) {
  const options = { placements: '', out: '' }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--placements') options.placements = argv[++index] || ''
    else if (arg === '--out') options.out = argv[++index] || ''
  }
  return options
}

function abs(path) {
  return resolve(ROOT, path)
}

function rel(path) {
  return path.replaceAll('\\', '/').replace(ROOT.replaceAll('\\', '/') + '/', '')
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

function round(value) {
  return Number.isFinite(value) ? Math.round(value * 1000) / 1000 : null
}

function splitTsv(line) {
  return line.split('\t').map((value) => value.trim())
}

function parseRotation(value) {
  const parts = String(value || '').split(/[,\s;]+/).filter(Boolean).map(Number)
  if (parts.length === 9 && parts.every(Number.isFinite)) return parts
  return [1, 0, 0, 0, 1, 0, 0, 0, 1]
}

function readPlacements(path) {
  const text = readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
  const lines = text.split(/\r?\n/).filter((line) => line.trim())
  const headers = splitTsv(lines.shift() || '')
  const index = new Map(headers.map((name, i) => [name, i]))
  return lines.map((line) => {
    const values = splitTsv(line)
    const get = (name) => values[index.get(name)] || ''
    return {
      role: get('role'),
      path: get('path'),
      tx_mm: asNumber(get('tx_mm')),
      ty_mm: asNumber(get('ty_mm')),
      tz_mm: asNumber(get('tz_mm')),
      rotation: parseRotation(get('rotation')),
    }
  }).filter((row) => row.role && row.path)
}

function stepPathFor(componentPath) {
  const exact = componentPath.replace(/\.SLDPRT$/i, '.step').replace(/\.SLDASM$/i, '.step')
  if (existsSync(exact)) return exact
  const lower = componentPath.replace(/\.SLDPRT$/i, '.STEP').replace(/\.SLDASM$/i, '.STEP')
  if (existsSync(lower)) return lower
  return exact
}

function pointsFromStep(path) {
  const text = readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
  const points = []
  const number = '([-+0-9.Ee]+)'
  const re = new RegExp(
    `CARTESIAN_POINT\\s*\\(\\s*'[^']*'\\s*,\\s*\\(\\s*${number}\\s*,\\s*${number}\\s*,\\s*${number}\\s*\\)\\s*\\)`,
    'g',
  )
  let match = null
  while ((match = re.exec(text))) {
    const point = [Number(match[1]), Number(match[2]), Number(match[3])]
    if (point.every(Number.isFinite)) points.push(point)
  }
  return points
}

function transformPoint(point, row) {
  const r = row.rotation
  return [
    r[0] * point[0] + r[1] * point[1] + r[2] * point[2] + row.tx_mm,
    r[3] * point[0] + r[4] * point[1] + r[5] * point[2] + row.ty_mm,
    r[6] * point[0] + r[7] * point[1] + r[8] * point[2] + row.tz_mm,
  ]
}

function bbox(points) {
  if (!points.length) return null
  const min = [...points[0]]
  const max = [...points[0]]
  for (const point of points) {
    for (let i = 0; i < 3; i += 1) {
      min[i] = Math.min(min[i], point[i])
      max[i] = Math.max(max[i], point[i])
    }
  }
  return {
    xmin_mm: round(min[0]),
    ymin_mm: round(min[1]),
    zmin_mm: round(min[2]),
    xmax_mm: round(max[0]),
    ymax_mm: round(max[1]),
    zmax_mm: round(max[2]),
    x_len_mm: round(max[0] - min[0]),
    y_len_mm: round(max[1] - min[1]),
    z_len_mm: round(max[2] - min[2]),
  }
}

function axisLengths(box) {
  if (!box) return []
  return [box.x_len_mm, box.y_len_mm, box.z_len_mm].filter((value) => Number.isFinite(value))
}

function bboxSanity(role, box) {
  if (!box) {
    return {
      status: 'NO_BBOX',
      clearance_usable: false,
      reason: 'No placed STEP point bbox could be computed.',
    }
  }

  const lengths = axisLengths(box)
  const sorted = [...lengths].sort((a, b) => b - a)
  if (role === 'door_panel_2_12_right_v43') {
    const pass = sorted[0] <= 400 && sorted[1] <= 400 && sorted[2] <= 80
    return {
      status: pass ? 'PASS' : 'FAIL',
      clearance_usable: pass,
      reference: '2/12 door nominal formed panel should stay near 307 x 298 mm plus shallow return flanges.',
      measured_axis_lengths_mm: sorted,
      reason: pass ? '' : 'STEP CARTESIAN_POINT envelope is far larger than the nominal door panel, so it is not trusted for clearance.',
    }
  }

  if (role === 'front_frame_vertical_left_v43') {
    const pass = sorted[0] >= 1700 && sorted[0] <= 1950 && sorted[1] <= 80 && sorted[2] <= 80
    return {
      status: pass ? 'PASS' : 'FAIL',
      clearance_usable: pass,
      reference: 'Front-frame vertical candidate is expected near 1834 mm tall with a small formed section.',
      measured_axis_lengths_mm: sorted,
      reason: pass ? '' : 'STEP envelope does not match the expected slender front-frame vertical body.',
    }
  }

  if (role === 'shelf_cross_panel_left_v43') {
    const pass = sorted[0] <= 700 && sorted[1] <= 700 && sorted[2] <= 120
    return {
      status: pass ? 'PASS' : 'FAIL',
      clearance_usable: pass,
      reference: 'Shelf/cross-panel flat evidence is 574.4 x 480.4 mm; formed thickness/depth should not create cabinet-height spans.',
      measured_axis_lengths_mm: sorted,
      reason: pass ? '' : 'STEP CARTESIAN_POINT envelope is far larger than the shelf flat evidence, so it is not trusted for clearance.',
    }
  }

  return {
    status: 'UNKNOWN_REFERENCE',
    clearance_usable: false,
    measured_axis_lengths_mm: sorted,
    reason: 'No role-specific sanity rule is available for this component.',
  }
}

function axisRelation(aMin, aMax, bMin, bMax) {
  if (aMax < bMin) return { separated: true, gap_mm: round(bMin - aMax), overlap_mm: 0 }
  if (bMax < aMin) return { separated: true, gap_mm: round(aMin - bMax), overlap_mm: 0 }
  return { separated: false, gap_mm: 0, overlap_mm: round(Math.min(aMax, bMax) - Math.max(aMin, bMin)) }
}

function pairRelation(a, b) {
  const x = axisRelation(a.bbox_mm.xmin_mm, a.bbox_mm.xmax_mm, b.bbox_mm.xmin_mm, b.bbox_mm.xmax_mm)
  const y = axisRelation(a.bbox_mm.ymin_mm, a.bbox_mm.ymax_mm, b.bbox_mm.ymin_mm, b.bbox_mm.ymax_mm)
  const z = axisRelation(a.bbox_mm.zmin_mm, a.bbox_mm.zmax_mm, b.bbox_mm.zmin_mm, b.bbox_mm.zmax_mm)
  const clearanceUsable = a.bbox_sanity?.clearance_usable === true && b.bbox_sanity?.clearance_usable === true
  return {
    roles: [a.role, b.role],
    aabb_intersects: !x.separated && !y.separated && !z.separated,
    clearance_conclusion: clearanceUsable
      ? (!x.separated && !y.separated && !z.separated ? 'AABB_INTERSECTS_REQUIRES_TRUE_INTERFERENCE_CHECK' : 'AABB_SEPARATED')
      : 'UNUSABLE_BBOX_SANITY_FAILED',
    axes: { x, y, z },
  }
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  if (!options.placements) throw new Error('--placements is required')
  const placementsPath = abs(options.placements)
  const placements = readPlacements(placementsPath)

  const components = placements.map((row) => {
    const sourcePath = row.path
    const sourceExists = existsSync(sourcePath)
    const stepPath = stepPathFor(sourcePath)
    const stepExists = existsSync(stepPath)
    const rawPoints = stepExists ? pointsFromStep(stepPath) : []
    const placedPoints = rawPoints.map((point) => transformPoint(point, row))
    const placedBox = bbox(placedPoints)
    return {
      role: row.role,
      source_path: rel(sourcePath),
      source_exists: sourceExists,
      step_path: rel(stepPath),
      step_exists: stepExists,
      point_count: rawPoints.length,
      tx_mm: row.tx_mm,
      ty_mm: row.ty_mm,
      tz_mm: row.tz_mm,
      bbox_mm: placedBox,
      bbox_sanity: bboxSanity(row.role, placedBox),
    }
  })

  const usable = components.filter((component) => component.bbox_mm)
  const clearanceUsable = usable.filter((component) => component.bbox_sanity?.clearance_usable === true)
  const pairwise = []
  for (let i = 0; i < usable.length; i += 1) {
    for (let j = i + 1; j < usable.length; j += 1) {
      pairwise.push(pairRelation(usable[i], usable[j]))
    }
  }

  const report = {
    schema: 'winnsen.locker16029.minimum_interface_step_bbox_probe.v1',
    status: usable.length !== placements.length
      ? 'FALLBACK_AABB_INCOMPLETE'
      : clearanceUsable.length === placements.length
        ? 'FALLBACK_AABB_PROBED'
        : 'FALLBACK_AABB_DEGRADED',
    release_blocker: true,
    placements: rel(placementsPath),
    note: 'This is an AABB fallback probe from STEP geometry and placement TSV. It does not replace the required SolidWorks SLDASM or true interference check.',
    component_count: components.length,
    usable_component_count: usable.length,
    clearance_usable_component_count: clearanceUsable.length,
    components,
    pairwise,
  }

  const text = `${JSON.stringify(report, null, 2)}\n`
  if (options.out) writeFileSync(abs(options.out), text, 'utf8')
  process.stdout.write(text)
  if (report.status === 'FALLBACK_AABB_INCOMPLETE') process.exit(1)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) main()
