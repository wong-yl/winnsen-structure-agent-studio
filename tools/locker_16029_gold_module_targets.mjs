import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { buildGoldSourceManifest, GOLD_SOURCE_MODULES } from './locker_16029_gold_source_manifest.mjs'

export const GOLD_MODULE_TARGET_SCHEMA = 'winnsen.locker16029.gold_module_targets.v1'

const FIXED_CABINET_MODULE_KEYS = Object.freeze([
  'cabinet_left_side_weldment',
  'cabinet_right_side_weldment',
  'cabinet_left_partition_weldment',
  'cabinet_right_partition_weldment',
  'front_frame_weldment',
  'base_weldment',
  'top_cover_weldment',
])

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function writeText(path, text) {
  mkdirSync(dirname(resolve(path)), { recursive: true })
  writeFileSync(path, text, 'utf8')
}

function textOf(value) {
  return String(value ?? '')
}

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function identityRotation() {
  return [1, 0, 0, 0, 1, 0, 0, 0, 1]
}

function rotationOf(transform) {
  const array = Array.isArray(transform?.array) ? transform.array : []
  if (array.length >= 9) return array.slice(0, 9).map((value) => roundMm(value))
  return identityRotation()
}

function compactNumber(value) {
  return Number(value).toFixed(3).replace(/\.?0+$/, '')
}

function median(values) {
  const numbers = values
    .map((value) => Number(value))
    .filter((value) => Number.isFinite(value))
    .sort((a, b) => a - b)
  if (!numbers.length) return null
  const mid = Math.floor(numbers.length / 2)
  return numbers.length % 2 ? numbers[mid] : (numbers[mid - 1] + numbers[mid]) / 2
}

function normalizePathLeaf(path) {
  return basename(textOf(path)).toLocaleLowerCase('zh-CN')
}

function moduleByKey(manifest, key) {
  const found = manifest.modules.find((module) => module.key === key)
  if (!found) throw new Error(`gold source module is not in manifest: ${key}`)
  return found
}

function sourceInstancesByFile(structureRecord) {
  const map = new Map()
  for (const component of Array.isArray(structureRecord?.components) ? structureRecord.components : []) {
    if (Number(component.depth) !== 0 || component.is_hidden || component.is_suppressed) continue
    const leaf = normalizePathLeaf(component.path || component.name)
    if (!leaf) continue
    if (!map.has(leaf)) map.set(leaf, [])
    map.get(leaf).push({
      name: component.name,
      path: component.path,
      transform: component.transform || null,
      totalTransform: component.total_transform || null,
    })
  }
  return map
}

function sourceReferenceFor(module, sourceInstances) {
  const instances = sourceInstances.get(normalizePathLeaf(module.file)) || []
  const transformsMm = instances.map((item) => ({
    txMm: roundMm(item.transform?.tx_mm || 0),
    tyMm: roundMm(item.transform?.ty_mm || 0),
    tzMm: roundMm(item.transform?.tz_mm || 0),
    rotation: rotationOf(item.transform),
  }))
  return {
    instanceCount: instances.length,
    topLevelNames: instances.map((item) => item.name),
    transformsMm,
    firstTransformMm: transformsMm[0] || null,
  }
}

function targetForModule(module, sourceInstances, binding) {
  const sourceReference = sourceReferenceFor(module, sourceInstances)
  return {
    key: module.key,
    role: module.role,
    file: module.file,
    sourcePath: module.sourcePath || '',
    exists: Boolean(module.exists),
    binding,
    status: 'source_reference_only_until_parametric_geometry_binding',
    sourceReference,
    placement: placementForBinding(binding, sourceReference),
  }
}

function placementForBinding(binding, sourceReference) {
  const source = sourceReference.firstTransformMm || { txMm: 0, tyMm: 0, tzMm: 0, rotation: identityRotation() }
  if (binding?.type === 'row_boundary_shelf') {
    const hasGoldSourceInstance = sourceReference.instanceCount > 0
    const calibration = shelfYCalibration(sourceReference)
    const targetTyMm = calibration
      ? roundMm(binding.boundaryYmm - calibration.sourceOriginYFromBottomMm)
      : roundMm(binding.boundaryYmm)
    return {
      coordinateFrame: 'current_template_rule_mm',
      targetTxMm: 0,
      targetTyMm,
      targetTzMm: 0,
      rotation: identityRotation(),
      sourceReferenceTxMm: source.txMm,
      sourceReferenceTyMm: source.tyMm,
      sourceReferenceTzMm: source.tzMm,
      buildReady: false,
      buildStatus: 'requires_parametric_y_geometry_binding',
      candidateBuildReady: hasGoldSourceInstance,
      candidateBuildStatus: hasGoldSourceInstance
        ? calibration ? 'calibrated_visual_binding_candidate_only' : 'uncalibrated_visual_binding_candidate_only'
        : 'missing_gold_source_instance_for_candidate',
      candidateValidation: 'needs_solidworks_visual_check_and_structural_engineer_validation',
      calibration,
      note: calibration
        ? 'Shelf candidate Y is mapped from requested row boundary through the gold-source shelf pitch; final geometry still needs SolidWorks visual check and engineer validation.'
        : 'The source gold shelf weldment proves module semantics; final geometry must bind Y to the requested row boundary before engineer handoff.',
    }
  }
  return {
    coordinateFrame: 'gold_source_total_assembly_mm',
    targetTxMm: source.txMm,
    targetTyMm: source.tyMm,
    targetTzMm: source.tzMm,
    rotation: source.rotation || identityRotation(),
    sourceReferenceTxMm: source.txMm,
    sourceReferenceTyMm: source.tyMm,
    sourceReferenceTzMm: source.tzMm,
    buildReady: Boolean(sourceReference.instanceCount),
    buildStatus: sourceReference.instanceCount ? 'source_reference_transform_available' : 'missing_source_reference_transform',
    note: 'Fixed cabinet module keeps the gold-source total-assembly transform until width/height geometry parameters are bound.',
  }
}

function shelfYCalibration(sourceReference) {
  const sourceTyMm = (Array.isArray(sourceReference?.transformsMm) ? sourceReference.transformsMm : [])
    .map((item) => Number(item.tyMm))
    .filter((value) => Number.isFinite(value))
    .sort((a, b) => a - b)
  if (sourceTyMm.length < 2) return null
  const deltas = []
  for (let index = 1; index < sourceTyMm.length; index += 1) {
    const delta = Math.abs(sourceTyMm[index] - sourceTyMm[index - 1])
    if (delta > 0.001) deltas.push(delta)
  }
  const sourcePitchMm = median(deltas)
  if (!sourcePitchMm || sourcePitchMm <= 0) return null
  const origins = sourceTyMm.map((tyMm, index) => ((index + 1) * sourcePitchMm) - tyMm)
  return {
    sourceTyMm: sourceTyMm.map((value) => roundMm(value)),
    sourcePitchMm: roundMm(sourcePitchMm),
    sourceOriginYFromBottomMm: roundMm(median(origins)),
    mapping: 'solidworks_ty_mm = requested_boundary_y_mm - source_origin_y_from_bottom_mm',
  }
}

function shelfTargets(plan, manifest, sourceInstances) {
  const targets = []
  for (const column of Array.isArray(plan?.columns) ? plan.columns : []) {
    const side = textOf(column.side).toUpperCase() === 'R' ? 'R' : 'L'
    const key = side === 'L' ? 'cabinet_left_shelf_weldment' : 'cabinet_right_shelf_weldment'
    const module = moduleByKey(manifest, key)
    const rows = Array.isArray(column.rows) ? column.rows : []
    for (const row of rows.slice(0, -1)) {
      const boundaryYmm = roundMm(row.topYmm)
      targets.push(targetForModule(module, sourceInstances, {
        type: 'row_boundary_shelf',
        side,
        afterRowIndex: Number(row.index),
        afterRowRatio: `${compactNumber(row.unit)}/12`,
        boundaryYmm,
        rule: 'place only at requested row boundary; suppress unmatched historical shelf bands',
      }))
    }
  }
  return targets
}

export function buildGoldModuleTargets({ plan, goldStructure = null, goldSourceRoot = undefined } = {}) {
  if (!plan) throw new Error('plan is required')
  const manifest = buildGoldSourceManifest(goldSourceRoot)
  const sourceInstances = sourceInstancesByFile(goldStructure)
  const fixedTargets = FIXED_CABINET_MODULE_KEYS.map((key) => {
    const module = moduleByKey(manifest, key)
    return targetForModule(module, sourceInstances, {
      type: 'fixed_cabinet_body_module',
      rule: 'preserve as a cabinet body subassembly, not as an imported multi-body part',
    })
  })
  const shelves = shelfTargets(plan, manifest, sourceInstances)
  const targets = [...fixedTargets, ...shelves]
  const requestedShelfBoundaryY = shelves.map((target) => target.binding.boundaryYmm)
  const requestedUniqueShelfBoundaryY = [...new Set(requestedShelfBoundaryY)].sort((a, b) => a - b)
  const buildReadyTargetCount = targets.filter((target) => target.placement?.buildReady).length
  const needsBindingTargetCount = targets.length - buildReadyTargetCount
  const shelfCandidateTargetCount = shelves.filter((target) => target.placement?.candidateBuildReady).length
  const cabinetCandidateTargetCount = buildReadyTargetCount + shelfCandidateTargetCount

  return {
    schema: GOLD_MODULE_TARGET_SCHEMA,
    cadMainline: 'SolidWorks 2020',
    sourceRoot: manifest.sourceRoot,
    sourceRootExists: manifest.sourceRootExists,
    engineeringDir: manifest.engineeringDir,
    engineeringDirExists: manifest.engineeringDirExists,
    requested: plan.requested,
    templateRuleVersion: plan.templateRuleVersion,
    derived: {
      targetCount: targets.length,
      fixedCabinetModuleCount: fixedTargets.length,
      shelfModuleCount: shelves.length,
      requestedShelfBoundaryY,
      requestedUniqueShelfBoundaryY,
      missingSourceFileCount: targets.filter((target) => !target.exists).length,
      buildReadyTargetCount,
      needsBindingTargetCount,
      shelfCandidateTargetCount,
      cabinetCandidateTargetCount,
      sourceStructureBound: Boolean(goldStructure),
    },
    policy: {
      cabinetBody: 'top-level cabinet body must become an assembly made from gold-source cabinet weldment modules',
      shelves: 'shelf weldments are generated from requested row boundaries; historical shelf bands outside the current row plan are unused',
      naming: 'engineer-facing modules use Chinese SolidWorks names from the gold source; automation ids stay ASCII',
    },
    targets,
  }
}

function tsvValue(value) {
  return textOf(value).replace(/\t/g, ' ').replace(/\r?\n/g, ' ')
}

function toTsv(targets) {
  const headers = [
    'key',
    'role',
    'file',
    'source_path',
    'binding_type',
    'side',
    'boundary_y_mm',
    'exists',
    'status',
    'source_instance_count',
    'placement_status',
    'build_ready',
    'target_tx_mm',
    'target_ty_mm',
    'target_tz_mm',
  ]
  const rows = [headers.join('\t')]
  for (const target of targets) {
    rows.push([
      target.key,
      target.role,
      target.file,
      target.sourcePath,
      target.binding?.type || '',
      target.binding?.side || '',
      target.binding?.boundaryYmm ?? '',
      target.exists ? 'true' : 'false',
      target.status,
      target.sourceReference?.instanceCount ?? 0,
      target.placement?.buildStatus || '',
      target.placement?.buildReady ? 'true' : 'false',
      target.placement?.targetTxMm ?? '',
      target.placement?.targetTyMm ?? '',
      target.placement?.targetTzMm ?? '',
    ].map(tsvValue).join('\t'))
  }
  return `${rows.join('\n')}\n`
}

function toBuildPlanTsv(targets) {
  const headers = [
    'key',
    'role',
    'source_path',
    'binding_type',
    'side',
    'boundary_y_mm',
    'target_tx_mm',
    'target_ty_mm',
    'target_tz_mm',
    'rotation',
    'build_ready',
    'build_status',
    'note',
  ]
  const rows = [headers.join('\t')]
  for (const target of targets) {
    rows.push([
      target.key,
      target.role,
      target.sourcePath,
      target.binding?.type || '',
      target.binding?.side || '',
      target.binding?.boundaryYmm ?? '',
      target.placement?.targetTxMm ?? '',
      target.placement?.targetTyMm ?? '',
      target.placement?.targetTzMm ?? '',
      Array.isArray(target.placement?.rotation) ? target.placement.rotation.join(',') : identityRotation().join(','),
      target.placement?.buildReady ? 'true' : 'false',
      target.placement?.buildStatus || '',
      target.placement?.note || '',
    ].map(tsvValue).join('\t'))
  }
  return `${rows.join('\n')}\n`
}

function toFixedPlacementTsv(targets) {
  const headers = ['role', 'path', 'tx_mm', 'ty_mm', 'tz_mm', 'rotation']
  const rows = [headers.join('\t')]
  for (const target of targets.filter((item) => item.placement?.buildReady)) {
    rows.push([
      target.role,
      target.sourcePath,
      target.placement?.targetTxMm ?? 0,
      target.placement?.targetTyMm ?? 0,
      target.placement?.targetTzMm ?? 0,
      Array.isArray(target.placement?.rotation) ? target.placement.rotation.join(',') : identityRotation().join(','),
    ].map(tsvValue).join('\t'))
  }
  return `${rows.join('\n')}\n`
}

function candidateShelfRole(target) {
  const side = target.binding?.side || 'X'
  const index = String(target.binding?.afterRowIndex ?? '').padStart(2, '0')
  const boundary = compactNumber(target.binding?.boundaryYmm ?? 0)
  return `${target.role}_${side}_after_row${index}_Y${boundary}`
}

function toShelfCandidatePlacementTsv(targets) {
  const headers = ['role', 'path', 'tx_mm', 'ty_mm', 'tz_mm', 'rotation']
  const rows = [headers.join('\t')]
  for (const target of targets.filter((item) => item.binding?.type === 'row_boundary_shelf' && item.placement?.candidateBuildReady)) {
    rows.push([
      candidateShelfRole(target),
      target.sourcePath,
      target.placement?.targetTxMm ?? 0,
      target.placement?.targetTyMm ?? 0,
      target.placement?.targetTzMm ?? 0,
      Array.isArray(target.placement?.rotation) ? target.placement.rotation.join(',') : identityRotation().join(','),
    ].map(tsvValue).join('\t'))
  }
  return `${rows.join('\n')}\n`
}

function toCabinetCandidatePlacementTsv(targets) {
  const headers = ['role', 'path', 'tx_mm', 'ty_mm', 'tz_mm', 'rotation']
  const rows = [headers.join('\t')]
  for (const target of targets) {
    const isShelf = target.binding?.type === 'row_boundary_shelf'
    const buildReady = isShelf ? target.placement?.candidateBuildReady : target.placement?.buildReady
    if (!buildReady) continue
    rows.push([
      isShelf ? candidateShelfRole(target) : target.role,
      target.sourcePath,
      target.placement?.targetTxMm ?? 0,
      target.placement?.targetTyMm ?? 0,
      target.placement?.targetTzMm ?? 0,
      Array.isArray(target.placement?.rotation) ? target.placement.rotation.join(',') : identityRotation().join(','),
    ].map(tsvValue).join('\t'))
  }
  return `${rows.join('\n')}\n`
}

function parsePlacementTsv(text) {
  const lines = textOf(text).replace(/^\uFEFF/, '').split(/\r?\n/).filter((line) => line.trim())
  if (!lines.length) return []
  const headers = lines[0].split('\t').map((item) => item.trim())
  return lines.slice(1).map((line) => {
    const values = line.split('\t')
    const row = {}
    headers.forEach((header, index) => {
      row[header] = values[index] ?? ''
    })
    return row
  })
}

function isTemplateShellBlackBoxPlacement(row) {
  const text = `${row.role || ''} ${row.path || ''}`.toLowerCase()
  return text.includes('gold_source_shell_frame_shelf') ||
    text.includes('candidate_16029_740w_gold_shell_frame_shelf_only')
}

function rightDoorSheetMetalCandidatePath(row) {
  const role = textOf(row.role)
  const sourcePath = textOf(row.path)
  if (!/^R_row/i.test(role) || !/v43_doors_variable_rib_restored_tongue/i.test(sourcePath)) return ''

  const roleMatch = role.match(/^R_row\d+_([0-9]+(?:p[0-9]+)?)_12/i)
  const pathMatch = sourcePath.match(/gold_ordinary_door_([0-9]+(?:p[0-9]+)?)_12_W307_right/i)
  const ratio = textOf(roleMatch?.[1] || pathMatch?.[1]).replace('p', '.')
  if (!ratio) return ''

  const root = sourcePath.split(/[\\/]v43_doors_variable_rib_restored_tongue[\\/]/i)[0]
  if (!root || root === sourcePath) return ''

  const candidate = join(root, 'v31_doors', `gold_ordinary_door_${ratio}_12_W307_right_v31.SLDASM`)
  return existsSync(candidate) ? candidate : ''
}

function toFullCandidatePlacementTsv(targets, templatePlacementText) {
  const headers = ['role', 'path', 'tx_mm', 'ty_mm', 'tz_mm', 'rotation']
  const rows = [headers.join('\t')]
  for (const row of parsePlacementTsv(templatePlacementText)) {
    if (isTemplateShellBlackBoxPlacement(row)) continue
    const replacementPath = rightDoorSheetMetalCandidatePath(row)
    const outRow = replacementPath
      ? { ...row, path: replacementPath, role: `${row.role}_sheetmetal_panel_candidate` }
      : row
    rows.push(headers.map((header) => tsvValue(outRow[header] ?? '')).join('\t'))
  }
  for (const line of toCabinetCandidatePlacementTsv(targets).trimEnd().split(/\r?\n/).slice(1)) {
    if (line.trim()) rows.push(line)
  }
  return `${rows.join('\n')}\n`
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    args[item.slice(2)] = argv[index + 1]
    index += 1
  }
  return args
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  if (!args.plan) {
    throw new Error('Usage: node tools/locker_16029_gold_module_targets.mjs --plan <template_rule_plan.json> [--gold-structure <original_structure.json>] [--out <targets.json>] [--tsv <targets.tsv>] [--build-plan <plan.tsv>] [--fixed-placements <placements.tsv>] [--shelf-candidate-placements <placements.tsv>] [--cabinet-candidate-placements <placements.tsv>] [--template-placements <template.tsv>] [--full-candidate-placements <placements.tsv>] [--root <gold-source-root>]')
  }
  const targets = buildGoldModuleTargets({
    plan: readJson(args.plan),
    goldStructure: args['gold-structure'] ? readJson(args['gold-structure']) : null,
    goldSourceRoot: args.root,
  })
  const jsonText = `${JSON.stringify(targets, null, 2)}\n`
  if (args.out) writeText(args.out, jsonText)
  else process.stdout.write(jsonText)
  if (args.tsv) writeText(args.tsv, toTsv(targets.targets))
  if (args['build-plan']) writeText(args['build-plan'], toBuildPlanTsv(targets.targets))
  if (args['fixed-placements']) writeText(args['fixed-placements'], toFixedPlacementTsv(targets.targets))
  if (args['shelf-candidate-placements']) writeText(args['shelf-candidate-placements'], toShelfCandidatePlacementTsv(targets.targets))
  if (args['cabinet-candidate-placements']) writeText(args['cabinet-candidate-placements'], toCabinetCandidatePlacementTsv(targets.targets))
  if (args['full-candidate-placements']) {
    if (!args['template-placements']) throw new Error('--full-candidate-placements requires --template-placements')
    writeText(args['full-candidate-placements'], toFullCandidatePlacementTsv(targets.targets, readFileSync(args['template-placements'], 'utf8')))
  }
  return targets
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
