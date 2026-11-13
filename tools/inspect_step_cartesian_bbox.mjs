import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

function parseArgs(argv) {
  const options = { step: '', out: '' }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--step') options.step = argv[++index] || ''
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

function finishBox(box) {
  if (!box) return null
  return {
    xmin_mm: Number(box.xmin.toFixed(3)),
    ymin_mm: Number(box.ymin.toFixed(3)),
    zmin_mm: Number(box.zmin.toFixed(3)),
    xmax_mm: Number(box.xmax.toFixed(3)),
    ymax_mm: Number(box.ymax.toFixed(3)),
    zmax_mm: Number(box.zmax.toFixed(3)),
    x_len_mm: Number((box.xmax - box.xmin).toFixed(3)),
    y_len_mm: Number((box.ymax - box.ymin).toFixed(3)),
    z_len_mm: Number((box.zmax - box.zmin).toFixed(3)),
  }
}

function updateBox(box, x, y, z) {
  if (!box) return { xmin: x, ymin: y, zmin: z, xmax: x, ymax: y, zmax: z }
  box.xmin = Math.min(box.xmin, x)
  box.ymin = Math.min(box.ymin, y)
  box.zmin = Math.min(box.zmin, z)
  box.xmax = Math.max(box.xmax, x)
  box.ymax = Math.max(box.ymax, y)
  box.zmax = Math.max(box.zmax, z)
  return box
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  if (!options.step) throw new Error('--step is required')
  const stepPath = abs(options.step)
  const stepExists = existsSync(stepPath)
  let text = ''
  if (stepExists) text = readFileSync(stepPath, 'utf8').replace(/^\uFEFF/, '')

  let entityCount = 0
  let pointCount = 0
  let box = null
  if (stepExists) {
    entityCount = [...text.matchAll(/#\d+\s*=/g)].length
    const re = /CARTESIAN_POINT\s*\(\s*'[^']*'\s*,\s*\(\s*([-+0-9.Ee]+)\s*,\s*([-+0-9.Ee]+)\s*,\s*([-+0-9.Ee]+)\s*\)\s*\)/g
    for (const match of text.matchAll(re)) {
      const x = Number(match[1])
      const y = Number(match[2])
      const z = Number(match[3])
      if (![x, y, z].every(Number.isFinite)) continue
      pointCount += 1
      box = updateBox(box, x, y, z)
    }
  }

  const report = {
    schema: 'winnsen.step_cartesian_bbox_check.v1',
    status: stepExists && pointCount > 0 ? 'PASS' : 'FAIL',
    step: rel(stepPath),
    exists: stepExists,
    bytes: stepExists ? statSync(stepPath).size : 0,
    entity_count: entityCount,
    cartesian_point_count: pointCount,
    bbox_mm: finishBox(box),
  }
  const output = `${JSON.stringify(report, null, 2)}\n`
  if (options.out) writeFileSync(abs(options.out), output, 'utf8')
  process.stdout.write(output)
  if (report.status !== 'PASS') process.exit(1)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) main()
