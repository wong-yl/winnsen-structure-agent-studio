import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

function parseArgs(argv) {
  const options = { components: '', out: '', assemblyId: 'door_frame_lock_shelf_minimum_interface_v43' }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--components') options.components = argv[++index] || ''
    else if (arg === '--out') options.out = argv[++index] || ''
    else if (arg === '--assembly-id') options.assemblyId = argv[++index] || options.assemblyId
  }
  return options
}

function abs(path) {
  return resolve(ROOT, path)
}

function rel(path) {
  return path.replaceAll('\\', '/').replace(ROOT.replaceAll('\\', '/') + '/', '')
}

function round(value) {
  return Number.isFinite(value) ? Math.round(value * 1000) / 1000 : null
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function boxFromComponent(component) {
  const box = component.box || {}
  const required = ['xmin_mm', 'ymin_mm', 'zmin_mm', 'xmax_mm', 'ymax_mm', 'zmax_mm']
  if (!required.every((key) => Number.isFinite(Number(box[key])))) return null
  return {
    xmin_mm: Number(box.xmin_mm),
    ymin_mm: Number(box.ymin_mm),
    zmin_mm: Number(box.zmin_mm),
    xmax_mm: Number(box.xmax_mm),
    ymax_mm: Number(box.ymax_mm),
    zmax_mm: Number(box.zmax_mm),
    x_len_mm: round(Number(box.xmax_mm) - Number(box.xmin_mm)),
    y_len_mm: round(Number(box.ymax_mm) - Number(box.ymin_mm)),
    z_len_mm: round(Number(box.zmax_mm) - Number(box.zmin_mm)),
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
  const separatedAxes = Object.entries({ x, y, z }).filter(([, value]) => value.separated)
  const minPositiveGap = separatedAxes
    .map(([, value]) => value.gap_mm)
    .filter((value) => Number.isFinite(value) && value > 0)
    .sort((left, right) => left - right)[0] || 0
  return {
    roles: [a.role, b.role],
    component_names: [a.name, b.name],
    aabb_intersects: separatedAxes.length === 0,
    clearance_conclusion: separatedAxes.length === 0
      ? 'AABB_OVERLAPS_REQUIRES_EXACT_SOLID_INTERFERENCE_CHECK'
      : 'SOLIDWORKS_NATIVE_AABB_SEPARATED_NO_SOLID_INTERSECTION_POSSIBLE',
    minimum_separating_gap_mm: round(minPositiveGap),
    separating_axes: separatedAxes.map(([axis]) => axis),
    axes: { x, y, z },
  }
}

function roleForName(name) {
  const text = String(name || '').toLowerCase()
  if (text.includes('front_frame_vertical_left')) return 'front_frame_vertical_left_v43'
  if (text.includes('shelf_cross_panel_left') || text.includes('shelf_cross_panel')) return 'shelf_cross_panel_left_v43'
  if (text.includes('door_panel') || text.includes('smoke_door_panel')) return 'door_panel_2_12_right_v43'
  return String(name || '').replace(/-\d+$/, '')
}

function scopeLimitations(components) {
  const paths = components.map((component) => String(component.path || '').toLowerCase())
  const usesEnvelopeDoor = paths.some((path) => path.includes('smoke_rebuild_v7_folded_envelope'))
  const usesEnvelopeShelf = paths.some((path) => path.includes('clean_envelope') || path.includes('interface_envelope'))
  const limitations = []
  if (usesEnvelopeDoor) {
    limitations.push('The door component is the v7 folded-envelope interface placeholder, not the current formal door-panel SLDPRT.')
  }
  if (usesEnvelopeShelf) {
    limitations.push('The shelf/cross-panel component is an interface envelope, not the current formal shelf sheet-metal SLDPRT.')
  }
  if (!usesEnvelopeDoor && !usesEnvelopeShelf) {
    limitations.push('The report uses the current formal pilot SLDPRTs from the placement TSV, but it is still a bbox clearance screen, not a production release sign-off.')
  }
  limitations.push('AABB-separated pairs cannot intersect, but AABB-overlapping pairs require exact SolidWorks interference or engineering gap review.')
  limitations.push('This bbox clearance screen does not validate hinge-hole datum, lock-tongue datum, coating allowance, door swing sweep, or production tolerance stack-up.')
  return limitations
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  if (!options.components) throw new Error('--components is required')
  if (!options.out) throw new Error('--out is required')

  const componentsPath = abs(options.components)
  const outPath = abs(options.out)
  const source = readJson(componentsPath)
  const components = (source.components || []).map((component) => ({
    role: roleForName(component.name),
    name: component.name || '',
    path: component.path ? rel(component.path) : '',
    is_suppressed: component.is_suppressed === true,
    is_hidden: component.is_hidden === true,
    tx_mm: round(component.transform?.tx_mm),
    ty_mm: round(component.transform?.ty_mm),
    tz_mm: round(component.transform?.tz_mm),
    bbox_mm: boxFromComponent(component),
  }))

  const usable = components.filter((component) => component.bbox_mm && !component.is_suppressed)
  const pairwise = []
  for (let i = 0; i < usable.length; i += 1) {
    for (let j = i + 1; j < usable.length; j += 1) {
      pairwise.push(pairRelation(usable[i], usable[j]))
    }
  }

  const aabbOverlaps = pairwise.filter((pair) => pair.aabb_intersects)
  const incomplete = source.opened !== true || source.rebuilt !== true || usable.length !== components.length || usable.length < 2
  const status = incomplete
    ? 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_INCOMPLETE'
    : aabbOverlaps.length === 0
      ? 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_SEPARATED'
      : 'SOLIDWORKS_NATIVE_BBOX_CLEARANCE_HAS_AABB_OVERLAPS'

  const blockingReasons = []
  if (incomplete) {
    blockingReasons.push({
      id: 'solidworks_component_bbox_incomplete',
      severity: 'release_blocking',
      evidence: { opened: source.opened, rebuilt: source.rebuilt, component_count: source.component_count, usable_component_count: usable.length },
      engineering_read: 'The SolidWorks-native assembly could not be fully opened/rebuilt/enumerated for bbox clearance.',
    })
  }
  for (const pair of aabbOverlaps) {
    blockingReasons.push({
      id: `aabb_overlap_${pair.roles.join('_vs_')}`,
      severity: 'release_blocking',
      evidence: pair,
      engineering_read: 'The SolidWorks-native component bounding boxes overlap, so exact interference must be checked before this pair can pass.',
    })
  }

  const report = {
    schema: 'winnsen.locker16029.minimum_interface_solidworks_bbox_clearance.v1',
    assembly_id: options.assemblyId,
    status,
    created_at: new Date().toISOString(),
    release_blocker: true,
    source_components_json: rel(componentsPath),
    assembly_path: source.assembly_path ? rel(source.assembly_path) : '',
    check_method: 'SolidWorks 2020 opened/rebuilt SLDASM, enumerated components, and compared native component bounding boxes.',
    engineering_read: 'If two component bounding boxes are separated on any axis, the corresponding solid bodies cannot intersect. This is a SolidWorks-native clearance screen for the narrow pilot interface, not a production geometry release or precise datum/gap sign-off.',
    opened: source.opened === true,
    rebuilt: source.rebuilt === true,
    component_count: components.length,
    usable_component_count: usable.length,
    pair_count: pairwise.length,
    separated_pair_count: pairwise.filter((pair) => !pair.aabb_intersects).length,
    overlapping_pair_count: aabbOverlaps.length,
    components,
    pairwise,
    checks_not_performed: [],
    blocking_reasons: blockingReasons,
    scope_limitations: scopeLimitations(components),
    next_required_action: blockingReasons.length === 0
      ? 'Use this as narrow pilot clearance evidence only. Production release still requires door folded-state closure, front-frame production FlatPattern/DXF, and engineering sign-off on datum/tolerance gaps.'
      : 'Run exact SolidWorks interference checks or repair the overlapping/incomplete component evidence before treating the minimum interface as clear.',
  }

  writeFileSync(outPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8')
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
  if (incomplete) process.exit(1)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) main()
