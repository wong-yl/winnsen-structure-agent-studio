import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

function parseArgs(argv) {
  const options = { placements: '', outStep: '', outJson: '' }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--placements') options.placements = argv[++index] || ''
    else if (arg === '--out-step') options.outStep = argv[++index] || ''
    else if (arg === '--out-json') options.outJson = argv[++index] || ''
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

function fmt(value) {
  const rounded = Math.abs(value) < 1e-10 ? 0 : value
  return Number(rounded.toFixed(12)).toString()
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
  const upper = componentPath.replace(/\.SLDPRT$/i, '.STEP').replace(/\.SLDASM$/i, '.STEP')
  if (existsSync(upper)) return upper
  return exact
}

function dataSectionFromStep(path) {
  const text = readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
  const match = text.match(/DATA;\s*([\s\S]*?)\s*ENDSEC;/i)
  if (!match) throw new Error(`STEP DATA section not found: ${path}`)
  return match[1].trim()
}

function transformPoint(point, row) {
  const r = row.rotation
  return [
    r[0] * point[0] + r[1] * point[1] + r[2] * point[2] + row.tx_mm,
    r[3] * point[0] + r[4] * point[1] + r[5] * point[2] + row.ty_mm,
    r[6] * point[0] + r[7] * point[1] + r[8] * point[2] + row.tz_mm,
  ]
}

function updateBox(box, point) {
  if (!box) {
    return {
      xmin_mm: point[0],
      ymin_mm: point[1],
      zmin_mm: point[2],
      xmax_mm: point[0],
      ymax_mm: point[1],
      zmax_mm: point[2],
    }
  }
  box.xmin_mm = Math.min(box.xmin_mm, point[0])
  box.ymin_mm = Math.min(box.ymin_mm, point[1])
  box.zmin_mm = Math.min(box.zmin_mm, point[2])
  box.xmax_mm = Math.max(box.xmax_mm, point[0])
  box.ymax_mm = Math.max(box.ymax_mm, point[1])
  box.zmax_mm = Math.max(box.zmax_mm, point[2])
  return box
}

function finishBox(box) {
  if (!box) return null
  return {
    xmin_mm: Number(box.xmin_mm.toFixed(3)),
    ymin_mm: Number(box.ymin_mm.toFixed(3)),
    zmin_mm: Number(box.zmin_mm.toFixed(3)),
    xmax_mm: Number(box.xmax_mm.toFixed(3)),
    ymax_mm: Number(box.ymax_mm.toFixed(3)),
    zmax_mm: Number(box.zmax_mm.toFixed(3)),
    x_len_mm: Number((box.xmax_mm - box.xmin_mm).toFixed(3)),
    y_len_mm: Number((box.ymax_mm - box.ymin_mm).toFixed(3)),
    z_len_mm: Number((box.zmax_mm - box.zmin_mm).toFixed(3)),
  }
}

function mergeComponent(row, idOffset) {
  const stepPath = stepPathFor(row.path)
  if (!existsSync(stepPath)) {
    throw new Error(`component STEP missing for ${row.role}: ${stepPath}`)
  }

  const data = dataSectionFromStep(stepPath)
  let maxId = 0
  let entityCount = 0
  data.replace(/#(\d+)\s*=/g, (_match, rawId) => {
    maxId = Math.max(maxId, Number(rawId))
    entityCount += 1
    return _match
  })

  let bbox = null
  let pointCount = 0
  const pointPattern = /CARTESIAN_POINT\s*\(\s*'([^']*)'\s*,\s*\(\s*([-+0-9.Ee]+)\s*,\s*([-+0-9.Ee]+)\s*,\s*([-+0-9.Ee]+)\s*\)\s*\)/g
  const transformedPoints = data.replace(pointPattern, (_match, label, x, y, z) => {
    const transformed = transformPoint([Number(x), Number(y), Number(z)], row)
    bbox = updateBox(bbox, transformed)
    pointCount += 1
    return `CARTESIAN_POINT ( '${label}',  ( ${fmt(transformed[0])}, ${fmt(transformed[1])}, ${fmt(transformed[2])} ) )`
  })
  const renumbered = transformedPoints.replace(/#(\d+)/g, (_match, rawId) => `#${Number(rawId) + idOffset}`)

  return {
    data: renumbered,
    nextOffset: idOffset + maxId,
    summary: {
      role: row.role,
      source_model: rel(row.path),
      source_step: rel(stepPath),
      source_step_bytes: statSync(stepPath).size,
      entity_count: entityCount,
      point_count: pointCount,
      tx_mm: row.tx_mm,
      ty_mm: row.ty_mm,
      tz_mm: row.tz_mm,
      rotation: row.rotation,
      bbox_mm: finishBox(bbox),
    },
  }
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  if (!options.placements) throw new Error('--placements is required')
  if (!options.outStep) throw new Error('--out-step is required')
  if (!options.outJson) throw new Error('--out-json is required')

  const placementsPath = abs(options.placements)
  const outStep = abs(options.outStep)
  const outJson = abs(options.outJson)
  const placements = readPlacements(placementsPath)
  if (!placements.length) throw new Error('placement TSV has no component rows')

  let idOffset = 0
  const dataBlocks = []
  const components = []
  for (const row of placements) {
    const merged = mergeComponent(row, idOffset)
    idOffset = merged.nextOffset
    dataBlocks.push(merged.data)
    components.push(merged.summary)
  }

  const generatedAt = new Date().toISOString()
  const step = [
    'ISO-10303-21;',
    'HEADER;',
    "FILE_DESCRIPTION (( 'AP203 fallback merged geometry for 16029 part-first pilot; not a native SolidWorks assembly export' ), '1' );",
    `FILE_NAME ('${options.outStep.replaceAll('\\', '/')}', '${generatedAt}', ( 'Winnsen Structure Agent Studio' ), ( 'pilot_only_not_released' ), 'STEP merge fallback', 'Node.js', 'not for production release' );`,
    "FILE_SCHEMA (( 'CONFIG_CONTROL_DESIGN' ));",
    'ENDSEC;',
    '',
    'DATA;',
    ...dataBlocks,
    'ENDSEC;',
    'END-ISO-10303-21;',
    '',
  ].join('\n')
  writeFileSync(outStep, step, 'utf8')

  const report = {
    schema: 'winnsen.locker16029.minimum_interface_step_fallback_merge.v1',
    status: 'FALLBACK_STEP_MERGED_NOT_SW_NATIVE',
    release_blocker: true,
    generated_at: generatedAt,
    placements: rel(placementsPath),
    out_step: rel(outStep),
    out_step_exists: existsSync(outStep),
    out_step_bytes: existsSync(outStep) ? statSync(outStep).size : 0,
    engineering_read: 'This file is a merged STEP fallback from the three existing component STEP files and the placement TSV. It is useful as an exchange/visualization artifact, but it is not a SolidWorks-native SLDASM export and does not prove interference or clearance.',
    component_count: components.length,
    components,
    next_required_action: 'Open the native minimum-interface SLDASM in a controlled SolidWorks 2020 session and run true interference/clearance checks before treating the minimum interface as passed.',
  }
  writeFileSync(outJson, `${JSON.stringify(report, null, 2)}\n`, 'utf8')
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) main()
