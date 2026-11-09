import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, readdirSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { INDOOR_16029_PRODUCTION_PROFILE } from './locker_16029_production_profile.mjs'
import { build16029ParametricContract } from './locker_16029_parametric_contract.mjs'
import { physical16029PartRole } from '../verify_16029_parametric_physical_quality.mjs'

const SHA256 = /^[A-F0-9]{64}$/
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8'))
const cad = path => !basename(path).startsWith('~$') && /\.SLD(PRT|ASM)$/i.test(path)
const key = path => resolve(path).toLowerCase()
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)

function scoped(path, root, label) {
  requireValue(typeof path === 'string' && isAbsolute(path), label + ' path is not absolute')
  const full = resolve(path), rel = relative(resolve(root), full)
  requireValue(rel && !rel.startsWith('..') && !isAbsolute(rel), label + ' path leaves its bound directory')
  let current = full
  while (true) {
    const stat = lstatSync(current)
    requireValue(!stat.isSymbolicLink() && (!stat.isFile() || stat.nlink === 1), label + ' refuses linked paths')
    const parent = dirname(current)
    if (current === parent) break
    current = parent
  }
  return full
}

export function inventory16029ProductionFiles(folder, { cadOnly = false } = {}) {
  const rows = []
  const visit = directory => {
    requireValue(!lstatSync(directory).isSymbolicLink(), 'production inventory refuses linked directory')
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = resolve(directory, entry.name), stat = lstatSync(path)
      requireValue(!stat.isSymbolicLink() && (!stat.isFile() || stat.nlink === 1), 'production inventory refuses linked entry')
      if (stat.isDirectory()) visit(path)
      else if (stat.isFile() && (!cadOnly || cad(path))) rows.push({ path, relativePath: relative(folder, path), sha256: hash(path) })
      else requireValue(stat.isFile(), 'production inventory refuses special entry')
    }
  }
  visit(folder)
  return rows.sort((a, b) => a.relativePath.localeCompare(b.relativePath))
}

export function production16029Profile(request) {
  if (request?.productionProfile === undefined) return ''
  requireValue(request.productionProfile === INDOOR_16029_PRODUCTION_PROFILE, 'unrecognized 16029 production profile')
  return request.productionProfile
}

function inventoryMap(rows, root, label, { flat = true, allowEmpty = false } = {}) {
  requireValue(Array.isArray(rows) && (allowEmpty || rows.length > 0), label + ' inventory missing')
  const result = new Map()
  for (const row of rows) {
    requireValue(typeof row?.path === 'string' && isAbsolute(row.path) && row.relativePath === relative(root, resolve(row.path)) &&
      row.relativePath && !isAbsolute(row.relativePath) && !row.relativePath.startsWith('..') && cad(row.path) &&
      (!flat || dirname(resolve(row.path)) === root) && SHA256.test(row.sha256), label + ' inventory row invalid')
    requireValue(!result.has(key(row.path)), label + ' inventory duplicate')
    result.set(key(row.path), row)
  }
  return result
}

function assertCurrentInventory(folder, rows, label, options) {
  const expected = inventoryMap(rows, folder, label, options)
  const actual = existsSync(folder) ? inventory16029ProductionFiles(folder, { cadOnly: true }) : []
  requireValue(actual.length === expected.size && actual.every(row => expected.get(key(row.path))?.sha256 === row.sha256), label + ' inventory changed, missing or extra')
}

function readAttemptIdentity(attempt) {
  scoped(attempt, dirname(attempt), 'attempt')
  const planPath = scoped(resolve(attempt, 'parametric_execution.json'), attempt, 'plan'), plan = read(planPath)
  requireValue(plan.schema === 'winnsen.locker16029.parametric_execution.v1' && /^[A-Za-z0-9_-]+$/.test(plan.taskId) &&
    typeof plan.workerId === 'string' && plan.workerId.length > 0, 'production plan identity invalid')
  const calculated = build16029ParametricContract(plan.contract?.input)
  requireValue(same(calculated, plan.contract), 'production contract differs from parameter calculation')
  requireValue(plan.contractSha256 === undefined || plan.contractSha256 === sha(JSON.stringify(plan.contract)), 'production contract hash mismatch')
  const taskPath = scoped(plan.taskPath, attempt, 'task')
  requireValue(taskPath === resolve(attempt, 'task_store', plan.taskId + '.json'), 'production task path mismatch')
  const task = read(taskPath), cabinet = plan.contract.geometry.cabinet
  requireValue(task.id === plan.taskId && task.taskType === 'native_solidworks_build_task' && task.requestFingerprint === sha(JSON.stringify(plan.contract.input)) &&
    task.cabinetWidth === cabinet.widthMm && task.cabinetHeight === cabinet.heightMm && task.cabinetDepth === cabinet.depthMm &&
    task.columns === 2 && task.doorCount === plan.contract.geometry.doorCount && task.nativeBuild && Number.isInteger(task.revision), 'production task identity or parameters mismatch')
  const executablePath = scoped(resolve(attempt, 'tools/NativeParametricAssembly.exe'), attempt, 'executable')
  const executableSha256 = hash(executablePath), buildPath = scoped(resolve(attempt, 'tools/build.json'), attempt, 'build manifest'), build = read(buildPath)
  requireValue(build.executableSha256 === executableSha256 && Array.isArray(build.sources) && build.sources.length > 0, 'production pinned build executable mismatch')
  const sourceNames = new Set(), repository = dirname(dirname(dirname(attempt)))
  for (const row of build.sources) {
    const sourcePath = scoped(row.path, repository, 'build source'), name = basename(sourcePath).toLowerCase()
    requireValue(!sourceNames.has(name) && SHA256.test(row.sha256) && hash(sourcePath) === row.sha256, 'production pinned build source mismatch')
    sourceNames.add(name)
  }
  return { attempt, plan, planPath, task, planSha256: hash(planPath), contractSha256: sha(JSON.stringify(plan.contract)), executableSha256, build }
}

function readAuthorization(context, phase) {
  const path = scoped(resolve(context.attempt, 'authorizations', phase + '.json'), context.attempt, 'authorization'), authorization = read(path)
  requireValue(authorization.phase === phase && authorization.taskId === context.plan.taskId && authorization.workerId === context.plan.workerId &&
    authorization.planSha256 === context.planSha256 && authorization.executableSha256 === context.executableSha256 &&
    typeof authorization.leaseId === 'string' && authorization.leaseId.length > 0 && Number.isFinite(Date.parse(authorization.expiresAt)), 'production authorization identity mismatch: ' + phase)
  return { authorization, authorizationPath: path, authorizationSha256: hash(path) }
}

function readStage(context, phase) {
  requireValue(/^[a-zA-Z0-9_-]+$/.test(phase), 'production phase identity invalid')
  const receiptPath = scoped(resolve(context.attempt, 'receipts', phase + '.json'), context.attempt, 'receipt'), receipt = read(receiptPath)
  const evidencePath = scoped(resolve(context.attempt, 'evidence', phase + '.json'), context.attempt, 'evidence'), evidence = read(evidencePath)
  const auth = readAuthorization(context, phase)
  const doorPhase = /^door-(\d+(?:p\d+)?)$/.exec(phase)
  const evidencePhaseMatches = doorPhase
    ? (evidence.phase === undefined || evidence.phase === phase) && Number.isFinite(evidence.heightMm) && evidence.heightMm > 0 && evidence.heightMm === Number(doorPhase[1].replace('p', '.'))
    : evidence.phase === phase
  requireValue(receipt.phase === phase && receipt.taskId === context.plan.taskId && receipt.planSha256 === context.planSha256 &&
    receipt.authorizationSha256 === auth.authorizationSha256 && receipt.evidenceSha256 === hash(evidencePath) &&
    receipt.protectedSourcesUnchanged === true && evidence.success === true && evidencePhaseMatches, 'production receipt/evidence binding mismatch: ' + phase)
  const working = resolve(context.attempt, 'native_cad/working_pack'), doors = resolve(context.attempt, 'doors')
  inventoryMap(receipt.before, working, phase + ' before'); inventoryMap(receipt.after, working, phase + ' after')
  inventoryMap(receipt.doorInventory, doors, phase + ' doors', { flat: false, allowEmpty: true })
  requireValue(same(auth.authorization.preInventory, receipt.before) && auth.authorization.predecessor === receipt.predecessor, 'production authorization inventory or predecessor mismatch: ' + phase)
  if (receipt.beforeDoorInventory !== undefined || auth.authorization.preDoorInventory !== undefined) {
    inventoryMap(receipt.beforeDoorInventory, doors, phase + ' before doors', { flat: false, allowEmpty: true })
    requireValue(same(receipt.beforeDoorInventory, auth.authorization.preDoorInventory), 'production door authorization inventory mismatch: ' + phase)
  }
  return { ...auth, phase, receipt, receiptPath, receiptSha256: hash(receiptPath), evidence, evidencePath, evidenceSha256: hash(evidencePath) }
}

function readStageChain(context) {
  const folder = scoped(resolve(context.attempt, 'receipts'), context.attempt, 'receipts'), stages = new Map(), hashes = new Map()
  for (const entry of readdirSync(folder, { withFileTypes: true })) {
    requireValue(entry.isFile() && /^[a-zA-Z0-9_-]+\.json$/.test(entry.name), 'production receipt directory has an unexpected entry')
    const stage = readStage(context, entry.name.slice(0, -5)); stages.set(stage.phase, stage); hashes.set(stage.receiptSha256, stage)
  }
  requireValue(stages.size > 0 && hashes.size === stages.size, 'production receipt chain empty or duplicated')
  const children = new Map(), roots = []
  for (const stage of stages.values()) {
    const predecessor = stage.receipt.predecessor
    if (!predecessor) { roots.push(stage); continue }
    const prior = hashes.get(predecessor)
    requireValue(prior && prior !== stage && !children.has(predecessor) && same(prior.receipt.after, stage.receipt.before), 'production predecessor chain or inventory mismatch: ' + stage.phase)
    if (stage.receipt.beforeDoorInventory !== undefined) requireValue(same(prior.receipt.doorInventory, stage.receipt.beforeDoorInventory), 'production predecessor door inventory mismatch: ' + stage.phase)
    children.set(predecessor, stage)
  }
  requireValue(roots.length === 1, 'production receipt chain root missing or duplicated')
  const ordered = [], visited = new Set()
  for (let stage = roots[0]; stage; stage = children.get(stage.receiptSha256)) {
    requireValue(!visited.has(stage.phase), 'production receipt chain cycle'); visited.add(stage.phase); ordered.push(stage)
  }
  requireValue(visited.size === stages.size, 'production receipt chain disconnected')
  return { stages, ordered }
}

function readDoorProofs(context) {
  const proofs = new Map(), seen = new Set(), store = dirname(context.attempt)
  let current = context.attempt
  while (current) {
    requireValue(seen.size < 20 && !seen.has(key(current)) && dirname(current) === store, 'production door resume chain invalid')
    seen.add(key(current))
    const ancestor = readAttemptIdentity(current)
    requireValue(same(ancestor.plan.contract, context.plan.contract), 'production door proof contract mismatch')
    const { stages } = readStageChain(ancestor)
    for (const stage of stages.values()) {
      if (!/^door-\d+(?:p\d+)?$/.test(stage.phase) || proofs.has(stage.phase)) continue
      const result = stage.evidence.result, edits = result?.part_edits, token = stage.phase.slice(5)
      requireValue(Array.isArray(edits) && edits.length === 3 && new Set(edits.map(edit => edit.role)).size === 3 &&
        ['left_panel', 'right_panel_mirror_part', 'stiffener'].every(role => edits.some(edit => edit.role === role)), 'production door part edits missing or duplicated')
      const module = scoped(resolve(current, 'doors', 'H' + token), current, 'door module')
      requireValue(typeof result.transaction_root === 'string' && isAbsolute(result.transaction_root), 'production door transaction identity missing')
      const transaction = scoped(result.transaction_root, resolve(current, 'transactions'), 'door transaction')
      const prefix = ancestor.plan.taskId + '-H' + token + '-'
      requireValue(dirname(transaction) === resolve(current, 'transactions') && basename(transaction).startsWith(prefix) &&
        /^\d{8}T\d{9}Z$/.test(basename(transaction).slice(prefix.length)) && sameAbsolutePath(result.staging_root, resolve(transaction, 'candidate')) &&
        sameAbsolutePath(result.relocation_probe_root, resolve(transaction, 'relocation_probe')) && sameAbsolutePath(result.committed_output_dir, module) &&
        result.relocation_reopen_passed === true && result.committed_reopen_passed === true, 'production door transaction or committed reopen binding mismatch')
      const outputs = new Map()
      requireValue(Array.isArray(result.output_files) && result.output_files.length === 14, 'production door output inventory missing')
      for (const row of result.output_files) {
        requireValue(typeof row?.relative_path === 'string' && /^(?:(?:parts|assemblies)\/[^/\\]+\.SLD(?:PRT|ASM)|door_module_flat_import_manifest\.json)$/i.test(row.relative_path) &&
          SHA256.test(row.sha256) && Number.isInteger(row.size_bytes) && row.size_bytes > 0, 'production door output identity invalid')
        const path = scoped(resolve(module, row.relative_path), module, 'door output'), id = key(path)
        requireValue(!outputs.has(id), 'production door output file uniqueness mismatch')
        outputs.set(id, row)
      }
      const manifest = resolve(module, 'door_module_flat_import_manifest.json')
      requireValue(sameAbsolutePath(result.flat_import_manifest_path, manifest) && outputs.get(key(manifest))?.sha256 === result.flat_import_manifest_sha256, 'production door manifest binding mismatch')
      const moduleCad = stage.receipt.doorInventory.filter(row => {
        const rel = relative(module, resolve(row.path)); return rel && !rel.startsWith('..') && !isAbsolute(rel)
      })
      requireValue(moduleCad.length === 13 && moduleCad.every(row => outputs.get(key(row.path))?.sha256 === row.sha256) &&
        [...outputs.keys()].filter(cad).length === moduleCad.length, 'production door output receipt binding mismatch')
      requireValue(Array.isArray(result.reopen_checks), 'production door native reopen evidence missing')
      const files = new Map()
      for (const edit of edits) {
        requireValue(typeof edit?.path === 'string' && isAbsolute(edit.path) && SHA256.test(edit.sha256_after_save), 'production door part edit identity invalid')
        const stagedPath = resolve(edit.path), name = basename(stagedPath)
        requireValue(dirname(stagedPath) === resolve(result.staging_root, 'parts') && /\.SLDPRT$/i.test(name), 'production door part edit leaves its bound staging parts')
        const path = scoped(resolve(module, 'parts', name), module, 'door part edit')
        const output = outputs.get(key(path)), health = edit.after, panel = edit.role !== 'stiffener'
        requireValue(output?.sha256 === edit.sha256_after_save && hash(path) === edit.sha256_after_save && lstatSync(path).size === output.size_bytes &&
          stage.receipt.doorInventory.some(row => key(row.path) === key(path) && row.sha256 === edit.sha256_after_save) &&
          health?.has_sheet_metal === true && health.has_flat_pattern === true && health.body_count === 1 && health.traversal_complete === true &&
          ['foreign_feature_count', 'error_feature_count', 'warning_feature_count'].every(field => health[field] === 0) &&
          edit.target_width_mm === (panel ? ancestor.plan.contract.geometry.doorLeafWidthMm : 0) &&
          edit.target_height_mm === stage.evidence.heightMm - (panel ? 0 : 10.5) &&
          (edit.role !== 'right_panel_mirror_part' || health.mirror_part_feature_count >= 1 || health.detached_mirror_folder_count >= 1), 'production door part edit inventory, dimensions or health mismatch')
        const role = edit.role === 'right_panel_mirror_part' ? 'right_panel' : edit.role
        for (const [phase, root] of [['read_only_reopen', result.staging_root], ['relocation_reopen', result.relocation_probe_root], ['committed_reopen', module]]) {
          const reopened = result.reopen_checks.filter(row => row.phase === phase && sameAbsolutePath(row.path, resolve(root, 'parts', name)))
          requireValue(reopened.length === 1 && reopened[0].role === role && reopened[0].sha256 === edit.sha256_after_save && reopened[0].gate === true, 'production door part edit native reopen binding mismatch')
        }
        const file = basename(edit.path).toLowerCase()
        requireValue(!files.has(file), 'production duplicate door part edit')
        files.set(file, edit)
      }
      proofs.set(stage.phase, { ...stage, files })
    }
    const resumed = ancestor.plan.resumedEvidence
    if (!resumed) break
    const parent = scoped(resumed.sourceAttempt, store, 'production resume source')
    requireValue(dirname(parent) === store && /^[a-zA-Z0-9_-]+$/.test(resumed.completedPhase), 'production resume phase invalid')
    const receiptPath = scoped(resolve(parent, 'receipts', resumed.completedPhase + '.json'), parent, 'production resume receipt')
    requireValue(hash(receiptPath) === resumed.receiptSha256 && same(read(receiptPath).after, resumed.inventory), 'production resume receipt or inventory changed')
    current = parent
  }
  const heights = new Set(['L', 'R'].flatMap(side => context.plan.contract.rowsByColumn[side].map(row => Number(row.doorHeightMm.toFixed(6)).toString().replace('.', 'p'))))
  requireValue(proofs.size === heights.size && [...heights].every(height => proofs.has('door-' + height)), 'production door proof height coverage mismatch')
  return proofs
}

function snapshotInventory(context, snapshotSource, priorReceipt) {
  const expected = new Map()
  for (const [boundRoot, targetRoot, rows, flat] of [
    [resolve(context.attempt, 'native_cad/working_pack'), snapshotSource, priorReceipt.after, true],
    [resolve(context.attempt, 'doors'), resolve(snapshotSource, 'doors'), priorReceipt.doorInventory, false],
  ]) for (const row of inventoryMap(rows, boundRoot, 'snapshot', { flat, allowEmpty: !flat }).values()) {
    const name = relative(snapshotSource, resolve(targetRoot, row.relativePath)).toLowerCase()
    requireValue(!expected.has(name), 'snapshot inventory duplicate'); expected.set(name, row.sha256)
  }
  const rows = inventory16029ProductionFiles(snapshotSource)
  requireValue(rows.length === expected.size && rows.every(row => expected.get(row.relativePath.toLowerCase()) === row.sha256), 'authorized input snapshot inventory mismatch')
  return rows
}

function validMarkSolid(value) {
  return value && SHA256.test(value.geometrySha256) && Number.isFinite(value.volumeM3) && value.volumeM3 > 0 &&
    ['bodies', 'vertices', 'edges', 'faces'].every(field => Number.isInteger(value[field]) && value[field] >= 0) && value.bodies > 0 &&
    Array.isArray(value.exactBoundsM) && value.exactBoundsM.length === 6 && value.exactBoundsM.every(Number.isFinite) && [0, 1, 2].every(axis => value.exactBoundsM[axis] <= value.exactBoundsM[axis + 3])
}

function sameMarkSolid(a, b) {
  return validMarkSolid(a) && validMarkSolid(b) && ['geometrySha256', 'bodies', 'vertices', 'edges', 'faces'].every(field => a[field] === b[field]) &&
    Math.abs(a.volumeM3 - b.volumeM3) <= Math.max(1e-12, a.volumeM3 * 1e-9) && a.exactBoundsM.every((value, index) => Math.abs(value - b.exactBoundsM[index]) <= 1e-8)
}

function productionMarkRows(evidence, inspection, revisions, label) {
  const rows = evidence.rows?.filter(row => row.inspection === inspection), result = new Map(), files = new Set()
  requireValue(Array.isArray(rows) && rows.length > 0, label + ' marking coverage missing')
  const revised = new Set(revisions.map(row => key(row.path)))
  for (const row of rows) {
    requireValue(typeof row.path === 'string' && isAbsolute(row.path) && revised.has(key(row.path)) && typeof row.configuration === 'string' && row.configuration.length > 0 && SHA256.test(row.protectedFeatureStateSha256), label + ' marking identity invalid')
    const id = JSON.stringify([key(row.path), row.configuration])
    requireValue(!result.has(id), label + ' marking identity duplicated'); result.set(id, row); files.add(key(row.path))
  }
  requireValue(files.size === revised.size, label + ' marking revision coverage mismatch')
  return result
}

const locatorVolumeMm3 = 8 * Math.PI * .8
const locatorHealFeature = '生产_取消已验证定位孔'
const locatorReplacement = 'actual_weld_contact_short_lines'
const sameAbsolutePath = (a, b) => typeof a === 'string' && isAbsolute(a) && typeof b === 'string' && isAbsolute(b) && key(a) === key(b)
const sameLocatorVolume = (a, b) => Number.isFinite(a) && a > 0 && Number.isFinite(b) && b > 0 && Math.abs(a - b) <= Math.max(1e-12, Math.abs(a) * 1e-9)

function validLocatorCenters(centers) {
  return Array.isArray(centers) && centers.length === 8 && centers.every((point, index) => Array.isArray(point) && point.length === 3 && point.every(Number.isFinite) &&
    centers.slice(0, index).every(other => point.some((value, axis) => Math.abs(value - other[axis]) > 1e-8)))
}

function sameLocatorCenters(a, b, mirror = false) {
  return validLocatorCenters(a) && validLocatorCenters(b) && a.every(point => b.filter(other =>
    point.every((value, axis) => Math.abs((mirror && axis === 0 ? -value : value) - other[axis]) <= 1e-8)).length === 1)
}

function productionLocatorMirror(link, source, label) {
  requireValue(link && sameAbsolutePath(link.path, source.rightPath) && sameAbsolutePath(link.sourcePath, source.leftPath) &&
    link.configuration === source.rightConfiguration && link.sourceConfiguration === source.leftConfiguration &&
    link.type === 'MirrorStock' && typeof link.feature === 'string' && link.feature.length > 0 &&
    Array.isArray(link.references) && link.references.length > 0 && link.references.every(reference =>
      sameAbsolutePath(reference?.modelPath, source.leftPath) && reference.status === 3), label + ' locator mirror source, configuration or active reference invalid')
  return JSON.stringify([key(link.path), key(link.sourcePath), link.configuration, link.sourceConfiguration, link.feature, link.type])
}

function productionLocatorApply(evidence, revisions, marks, working) {
  const sources = evidence.rows?.filter(row => row.inspection === 'production_locator_pair_source')
  const changes = evidence.rows?.filter(row => row.inspection === 'production_locator_change')
  requireValue(Array.isArray(sources) && sources.length === 1 && Array.isArray(changes) && changes.length === 2, 'production locator pair or change coverage missing or duplicated')
  const source = sources[0], leftPath = resolve(working, '箱体横层板L.sldprt'), rightPath = resolve(working, '箱体横层板R.SLDPRT')
  requireValue(sameAbsolutePath(source.leftPath, leftPath) && sameAbsolutePath(source.rightPath, rightPath) &&
    [source.leftConfiguration, source.rightConfiguration].every(configuration => typeof configuration === 'string' && configuration.length > 0) &&
    sameLocatorCenters(source.leftCentersModelM, source.rightCentersModelM, true), 'production locator pair identity, configuration or mirrored centers invalid')
  const mirror = productionLocatorMirror(source.mirrorLink, source, 'production source'), result = new Map()
  for (const change of changes) {
    const left = sameAbsolutePath(change.path, leftPath), right = sameAbsolutePath(change.path, rightPath)
    requireValue(left || right, 'production locator change path invalid')
    const id = key(change.path), configuration = left ? source.leftConfiguration : source.rightConfiguration
    const revision = revisions.find(row => key(row.path) === id), mark = marks.get(JSON.stringify([id, configuration]))
    requireValue(!result.has(id) && revision && mark && change.configuration === configuration && change.axis === 1 && change.removedHoleCount === 8 &&
      change.diameterMm === 2 && change.holeCountBefore === 8 && change.holeCountAfter === 0 && change.replacement === locatorReplacement &&
      change.strategy === (left ? 'direct_heal' : 'mirrored_source_heal') &&
      (left ? change.sourcePath === '' && change.sourceFeature === '' && change.feature === locatorHealFeature :
        sameAbsolutePath(change.sourcePath, leftPath) && change.sourceFeature === locatorHealFeature && change.feature === source.mirrorLink.feature), 'production locator change identity, configuration, strategy or coverage invalid')
    requireValue(sameLocatorCenters(change.centersModelM, left ? source.leftCentersModelM : source.rightCentersModelM) &&
      sameLocatorVolume(change.volumeBeforeM3, left ? source.leftVolumeBeforeM3 : source.rightVolumeBeforeM3) &&
      sameLocatorVolume(change.volumeAfterM3, mark.markSolidAfter.volumeM3) && Number.isFinite(change.addedVolumeMm3) &&
      Math.abs(change.addedVolumeMm3 - locatorVolumeMm3) <= .01 &&
      Math.abs((change.volumeAfterM3 - change.volumeBeforeM3) * 1e9 - locatorVolumeMm3) <= .01 &&
      Math.abs((change.volumeAfterM3 - change.volumeBeforeM3) * 1e9 - change.addedVolumeMm3) <= .01, 'production locator hole geometry or healed volume invalid')
    if (right) requireValue(productionLocatorMirror(change.mirrorLink, source, 'production apply') === mirror, 'production locator mirror identity changed during apply')
    result.set(id, change)
  }
  requireValue(result.has(key(leftPath)) && result.has(key(rightPath)), 'production locator left/right coverage incomplete')
  return { source, mirror, changes: result }
}

function verifyProductionLocators(evidence, applied, revisions, marks) {
  const rows = evidence.rows?.filter(row => row.inspection === 'production_locator_verification'), seen = new Set()
  requireValue(Array.isArray(rows) && rows.length === 2, 'production locator verification coverage missing or duplicated')
  for (const row of rows) {
    requireValue(typeof row.path === 'string' && isAbsolute(row.path), 'production locator verification path invalid')
    const id = key(row.path), change = applied.changes.get(id), revision = revisions.find(item => key(item.path) === id)
    const mark = marks.get(JSON.stringify([id, row.configuration]))
    requireValue(!seen.has(id) && change && revision && mark && row.sha256 === revision.newProductionSha256 && row.pass === true && row.holeCount === 0 &&
      row.removedHoleCount === 8 && row.axis === 1 && row.diameterMm === 2 && row.replacement === locatorReplacement &&
      ['configuration', 'strategy', 'sourceFeature', 'feature'].every(field => row[field] === change[field]) &&
      (change.sourcePath === '' ? row.sourcePath === '' : sameAbsolutePath(row.sourcePath, change.sourcePath)), 'production locator verification identity, hash or persisted change invalid')
    requireValue(sameLocatorVolume(row.volumeM3, change.volumeAfterM3) && sameLocatorVolume(row.volumeM3, mark.markSolidCurrent.volumeM3), 'production locator persisted volume invalid')
    if (change.strategy === 'mirrored_source_heal') requireValue(productionLocatorMirror(row.mirrorLink, applied.source, 'production verify') === applied.mirror, 'production locator mirror identity changed after reopen')
    seen.add(id)
  }
  requireValue([...applied.changes.keys()].every(id => seen.has(id)), 'production locator verification left/right coverage incomplete')
}

function packageInventorySha256(rows) {
  return sha(rows.map(row => ({ path: resolve(row.path), sha256: row.sha256 })).sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0)
    .map(row => row.path + '\t' + row.sha256 + '\n').join(''))
}

function packageDocumentState(rows, path, label) {
  requireValue(Array.isArray(rows) && rows.length > 0, label + ' document state missing')
  const documentType = /\.SLDPRT$/i.test(path) ? 'part' : 'assembly', result = new Map()
  for (const row of rows) {
    requireValue(typeof row?.configuration === 'string' && row.configuration.trim().length > 0 && !result.has(row.configuration) && row.documentType === documentType &&
      SHA256.test(row.featureStateSha256) && (documentType === 'part' ? SHA256.test(row.geometrySha256) && row.assemblyStateSha256 === '' :
        row.geometrySha256 === '' && SHA256.test(row.assemblyStateSha256)), label + ' document state identity invalid or duplicated')
    result.set(row.configuration, [row.documentType, row.geometrySha256, row.featureStateSha256, row.assemblyStateSha256])
  }
  return result
}

function packageDependencyPaths(paths, inventory, label) {
  requireValue(Array.isArray(paths), label + ' dependencies missing')
  const result = new Set()
  for (const path of paths) {
    requireValue(typeof path === 'string' && isAbsolute(path) && inventory.has(key(path)) && !result.has(key(path)), label + ' dependency missing, external or duplicated')
    result.add(key(path))
  }
  return result
}

function remapPackageEvidence(value, mapping, sourceHashes = false) {
  if (typeof value === 'string' && isAbsolute(value)) {
    requireValue(mapping.has(key(value)), 'production package evidence references an unmapped CAD path')
    return mapping.get(key(value)).path
  }
  if (Array.isArray(value)) return value.map(item => remapPackageEvidence(item, mapping, sourceHashes))
  if (value && typeof value === 'object') {
    let source = null
    if (Object.hasOwn(value, 'sourceSha256')) {
      requireValue(typeof value.sourcePath === 'string' && isAbsolute(value.sourcePath), 'production package contact source path missing')
      source = mapping.get(key(value.sourcePath))
      requireValue(source && value.sourceSha256 === (sourceHashes ? source.sourceSha256 : source.sha256), 'production package contact source hash mismatch')
    }
    return Object.fromEntries(Object.keys(value).sort().map(name => [name, name === 'sourceSha256' ? source.sha256 : remapPackageEvidence(value[name], mapping, sourceHashes)]))
  }
  return value
}

function samePackageMarkEvidence(a, b, field = '') {
  if (typeof a === 'number' || typeof b === 'number') {
    if (!Number.isFinite(a) || !Number.isFinite(b)) return false
    if (/^(?:.*(?:count|axis|sign|index|status|code|order|ordinal)|kind|type|contactFaces|bodies|vertices|edges|faces)$/i.test(field)) return Number.isInteger(a) && Number.isInteger(b) && a === b
    return Math.abs(a - b) <= (/(?:sourceToRoot|rootToTarget|transform)/i.test(field) ? 1e-9 : 1e-8)
  }
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false
    if (field === 'sourceContactsAndTransforms') {
      const contacts = new Map()
      for (const row of b) {
        const id = packageMarkContactIdentity(row)
        if (!id || contacts.has(id)) return false
        contacts.set(id, row)
      }
      const seen = new Set()
      return a.every(row => {
        const id = packageMarkContactIdentity(row)
        if (!id || seen.has(id) || !contacts.has(id) || !samePackageMarkEvidence(row, contacts.get(id))) return false
        seen.add(id)
        return true
      })
    }
    if (['expectedSegments', 'actualSegments', 'originalContactOuterLoop', 'projectedContactOuterLoop', 'materialClippedContactOuterLoop'].includes(field)) {
      const matched = Array(b.length).fill(-1)
      const match = (index, seen) => {
        for (let candidate = 0; candidate < b.length; candidate++) {
          if (seen[candidate] || !samePackageMarkEvidence(a[index], b[candidate])) continue
          seen[candidate] = true
          if (matched[candidate] === -1 || match(matched[candidate], seen)) {
            matched[candidate] = index
            return true
          }
        }
        return false
      }
      return a.every((_, index) => match(index, Array(b.length).fill(false)))
    }
    return a.every((value, index) => samePackageMarkEvidence(value, b[index], field))
  }
  if (a && typeof a === 'object' || b && typeof b === 'object') {
    if (!a || !b || typeof a !== 'object' || typeof b !== 'object') return false
    const names = Object.keys(a).sort()
    return same(names, Object.keys(b).sort()) && names.every(name => samePackageMarkEvidence(a[name], b[name], name))
  }
  return a === b
}

function packageMarkContactIdentity(row) {
  const contact = row?.sourceContact || row
  if (!contact || typeof contact !== 'object' || !['sourceComponent', 'sourcePath', 'sourceConfiguration', 'targetComponent', 'targetPath', 'targetConfiguration'].every(field =>
    typeof contact[field] === 'string' && contact[field].length > 0) || !isAbsolute(contact.sourcePath) || !isAbsolute(contact.targetPath)) return ''
  return JSON.stringify([contact.sourceComponent, key(contact.sourcePath), contact.sourceConfiguration,
    contact.targetComponent, key(contact.targetPath), contact.targetConfiguration])
}

function validPackageMarkRepresentative(mark) {
  return typeof mark?.component === 'string' && mark.component.length > 0 && typeof mark.path === 'string' && isAbsolute(mark.path) &&
    typeof mark.configuration === 'string' && mark.configuration.length > 0 && Array.isArray(mark.sourceContactsAndTransforms) &&
    mark.sourceContactsAndTransforms.some(row => {
      const contact = row?.sourceContact || row
      return contact && (mark.family === '侧加强筋朝上箭头与上'
        ? mark.component === contact.sourceComponent && sameAbsolutePath(mark.path, contact.sourcePath) && mark.configuration === contact.sourceConfiguration
        : mark.component === contact.targetComponent && sameAbsolutePath(mark.path, contact.targetPath) && mark.configuration === contact.targetConfiguration)
    })
}

export function same16029PackageMarksEvidence(a, b) {
  if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false
  const marks = new Map()
  for (const mark of b) {
    if (typeof mark?.family !== 'string' || !mark.family || marks.has(mark.family) || !validPackageMarkRepresentative(mark)) return false
    marks.set(mark.family, mark)
  }
  const seen = new Set()
  return a.every(mark => {
    if (typeof mark?.family !== 'string' || seen.has(mark.family) || !validPackageMarkRepresentative(mark)) return false
    seen.add(mark.family)
    const packaged = marks.get(mark.family)
    if (!packaged) return false
    const names = Object.keys(mark).sort()
    return same(names, Object.keys(packaged).sort()) && names.every(name => name === 'component' || samePackageMarkEvidence(mark[name], packaged[name], name))
  })
}

function verifyProductionPackage(context, packageStage, verification, revisions, verifyMarks, appliedLocators, working, doors, productionProfile) {
  const folder = resolve(context.attempt, 'package'), input = resolve(context.attempt, 'package_input'), rows = packageStage.evidence.rows
  requireValue(Array.isArray(rows) && rows.length > 0, 'production final package inventory missing')
  requireValue(same(packageStage.receipt.before, packageStage.receipt.after) && same(packageStage.receipt.before, verification.receipt.after), 'production package changed working inventory')
  requireValue(same(packageStage.receipt.beforeDoorInventory, verification.receipt.doorInventory) && same(packageStage.receipt.beforeDoorInventory, packageStage.receipt.doorInventory) &&
    same(packageStage.receipt.protectedDoorFilesBefore, verification.receipt.protectedDoorFilesAfter) &&
    same(packageStage.receipt.protectedDoorFilesBefore, packageStage.authorization.protectedDoorFilesBefore) &&
    same(packageStage.receipt.protectedDoorFilesBefore, packageStage.receipt.protectedDoorFilesAfter) &&
    same(packageStage.receipt.protectedDoorFilesAfter, inventory16029ProductionFiles(doors)), 'production package changed protected door files')
  const sources = inventoryMap(verification.receipt.after, working, 'production package source'), mapped = new Map(), final = new Map(), isolated = new Set()
  for (const row of rows) {
    const path = scoped(row.path, folder, 'production package'), sourcePath = scoped(row.sourcePath, working, 'production package source'), isolatedPath = scoped(row.isolatedPath, input, 'production package isolated input')
    const id = key(path), sourceId = key(sourcePath), isolatedId = key(isolatedPath)
    requireValue(dirname(path) === folder && dirname(sourcePath) === working && dirname(isolatedPath) === input && cad(path) && cad(sourcePath) &&
      /\.SLDPRT$/i.test(path) === /\.SLDPRT$/i.test(sourcePath) && basename(isolatedPath) === basename(sourcePath) &&
      !final.has(id) && !mapped.has(sourceId) && !isolated.has(isolatedId) && SHA256.test(row.sha256) && hash(path) === row.sha256 &&
      SHA256.test(row.sourceSha256) && sources.get(sourceId)?.sha256 === row.sourceSha256 && hash(sourcePath) === row.sourceSha256 &&
      row.isolatedCopySha256 === row.sourceSha256 && SHA256.test(row.packInputSha256) && SHA256.test(row.isolatedAfterSha256) &&
      hash(isolatedPath) === row.isolatedAfterSha256, 'production package file provenance or hash invalid')
    const sourceState = packageDocumentState(row.sourceState, sourcePath, 'production package source'), targetState = packageDocumentState(row.targetState, path, 'production package target')
    const isolatedAfterState = packageDocumentState(row.isolatedAfterState, isolatedPath, 'production package isolated input after')
    requireValue(sourceState.size === targetState.size && sourceState.size === isolatedAfterState.size && [...sourceState].every(([configuration, state]) =>
      same(state, targetState.get(configuration)) && same(state, isolatedAfterState.get(configuration))), 'production package configuration, geometry or feature state changed')
    final.set(id, row); mapped.set(sourceId, row); isolated.add(isolatedId)
  }
  const actual = inventory16029ProductionFiles(folder, { cadOnly: true })
  requireValue(actual.length === final.size && actual.every(row => final.get(key(row.path))?.sha256 === row.sha256), 'production final package missing or extra CAD')
  const roots = rows.filter(row => row.provenance !== undefined)
  requireValue(roots.length === 1, 'production package root provenance missing or duplicated')
  const root = roots[0], provenance = root.provenance, cabinet = context.plan.contract.geometry.cabinet
  const targetRoot = resolve(folder, '16029_' + cabinet.widthMm + 'W_' + cabinet.heightMm + 'H_' + cabinet.depthMm + 'D.SLDASM')
  requireValue(provenance?.schema === 'winnsen.locker16029.package_provenance.v1' && provenance.taskId === context.plan.taskId &&
    provenance.planSha256 === context.planSha256 && provenance.executableSha256 === context.executableSha256 && provenance.includeSuppressed === false &&
    sameAbsolutePath(provenance.sourceRoot, root.sourcePath) && sameAbsolutePath(provenance.isolatedRoot, root.isolatedPath) &&
    sameAbsolutePath(provenance.targetRoot, root.path) && sameAbsolutePath(root.path, targetRoot) && /\.SLDASM$/i.test(root.sourcePath), 'production package root provenance identity invalid')
  requireValue(provenance.sourceInventorySha256 === packageInventorySha256(verification.receipt.after) &&
    provenance.sourceInventorySha256 === packageInventorySha256(packageStage.receipt.before) && provenance.sourceInventorySha256 === packageInventorySha256(packageStage.receipt.after) &&
    provenance.protectedDoorFilesBeforeSha256 === packageInventorySha256(packageStage.receipt.protectedDoorFilesBefore) &&
    provenance.protectedDoorFilesAfterSha256 === packageInventorySha256(packageStage.receipt.protectedDoorFilesAfter) &&
    provenance.protectedDoorFilesBeforeSha256 === provenance.protectedDoorFilesAfterSha256, 'production package inventory provenance digest mismatch')
  const dependencies = new Map(), isolatedTargets = new Map(rows.map(row => [key(row.isolatedPath), key(row.sourcePath)]))
  for (const row of rows) {
    const sourceId = key(row.sourcePath), sourceDependencies = packageDependencyPaths(row.sourceDependencies, sources, 'production package source'), targetDependencies = packageDependencyPaths(row.targetDependencies, final, 'production package target')
    requireValue(!sourceDependencies.has(sourceId) && !targetDependencies.has(key(row.path)), 'production package self dependency invalid')
    const expected = [...sourceDependencies].map(id => mapped.get(id))
    requireValue(expected.every(Boolean) && expected.length === targetDependencies.size && expected.every(row => targetDependencies.has(key(row.path))), 'production package dependency mapping mismatch')
    requireValue(Array.isArray(row.isolatedAfterDependencies), 'production package isolated after dependencies missing')
    const isolatedAfterDependencies = new Set()
    for (const path of row.isolatedAfterDependencies) {
      requireValue(typeof path === 'string' && isAbsolute(path), 'production package isolated after dependency path invalid')
      const id = isolatedTargets.get(key(path)) || (final.has(key(path)) ? key(final.get(key(path)).sourcePath) : '')
      requireValue(id && sourceDependencies.has(id) && !isolatedAfterDependencies.has(id), 'production package isolated after dependency external, rebound or duplicated')
      isolatedAfterDependencies.add(id)
    }
    requireValue(isolatedAfterDependencies.size === sourceDependencies.size, 'production package isolated after dependency coverage mismatch')
    requireValue(sourceDependencies.size > 0 || row.packInputSha256 === row.sourceSha256 && row.isolatedAfterSha256 === row.packInputSha256, 'production package isolated input changed without a direct dependency')
    dependencies.set(sourceId, sourceDependencies)
    const sameName = basename(row.path).toLowerCase() === basename(row.sourcePath).toLowerCase(), sameHash = row.sha256 === row.sourceSha256
    requireValue(row.allowedChangeReason === 'byte_identical' ? sameName && sameHash :
      row.allowedChangeReason === 'reference_rewrite' ? sameName && !sameHash && sourceDependencies.size > 0 :
        row.allowedChangeReason === 'root_rename_and_reference_rewrite' && row === root && !sameName && sourceDependencies.size > 0, 'production package file change reason invalid')
  }
  const closure = new Set(), pending = [key(root.sourcePath)]
  while (pending.length) {
    const id = pending.pop()
    if (closure.has(id)) continue
    requireValue(mapped.has(id) && dependencies.has(id), 'production package dependency closure is incomplete')
    closure.add(id); pending.push(...dependencies.get(id))
  }
  const declaredClosure = packageDependencyPaths(provenance.rootDependencyClosure, sources, 'production package root closure')
  requireValue(closure.size === mapped.size && closure.size === declaredClosure.size && [...closure].every(id => declaredClosure.has(id)), 'production package root dependency closure mismatch')
  requireValue(Array.isArray(provenance.excluded), 'production package excluded inventory missing')
  const excluded = new Set(), revisionIds = new Set(revisions.map(row => key(row.path)))
  for (const row of provenance.excluded) {
    requireValue(typeof row?.sourcePath === 'string' && isAbsolute(row.sourcePath), 'production package excluded source path invalid')
    const id = key(row.sourcePath)
    requireValue(sources.has(id) && !closure.has(id) && !excluded.has(id) && !revisionIds.has(id) && row.sourceSha256 === sources.get(id).sha256 &&
      row.reason === 'outside_root_dependency_closure', 'production package excluded source binding invalid')
    excluded.add(id)
  }
  requireValue(excluded.size + closure.size === sources.size && [...sources.keys()].every(id => closure.has(id) || excluded.has(id)) &&
    [...revisionIds].every(id => closure.has(id)), 'production package omitted source or production revision')
  const packagedRows = provenance.productionVerificationRows
  requireValue(Array.isArray(packagedRows), 'production package native verification missing')
  const checks = packagedRows.filter(row => row.inspection === 'production_packaged_part_verification')
  requireValue(checks.length === revisions.length && new Set(checks.map(row => key(row.path))).size === checks.length &&
    new Set(checks.map(row => key(row.sourcePath))).size === checks.length, 'production package native part verification coverage mismatch')
  const packagedRevisions = revisions.map(revision => {
    const mapping = mapped.get(key(revision.path)), check = checks.find(row => sameAbsolutePath(row.sourcePath, revision.path))
    requireValue(mapping && mapping.sourceSha256 === revision.newProductionSha256 && check && check.sourceSha256 === revision.newProductionSha256 &&
      sameAbsolutePath(check.path, mapping.path) && check.sha256 === mapping.sha256 && check.file === revision.file && check.file === basename(mapping.path) &&
      check.role === revision.role && check.productionProfile === productionProfile && check.pass === true, 'production package native part verification binding mismatch')
    return { ...revision, path: mapping.path, newProductionSha256: mapping.sha256 }
  })
  const packagedMarks = productionMarkRows({ rows: packagedRows }, 'production_marks_verify', packagedRevisions, 'production package')
  requireValue(packagedMarks.size === verifyMarks.size, 'production package marking configuration coverage mismatch')
  for (const baseline of verifyMarks.values()) {
    const mapping = mapped.get(key(baseline.path)), row = packagedMarks.get(JSON.stringify([key(mapping.path), baseline.configuration]))
    requireValue(row && row.sha256 === mapping.sha256 && row.readOnly === true && row.independentlyRecomputed === true && row.saved === false &&
      row.protectedFeatureStateSha256 === baseline.protectedFeatureStateSha256 && sameMarkSolid(baseline.markSolidCurrent, row.markSolidCurrent) &&
      sameMarkSolid(row.markSolidCurrent, row.verificationSolidBefore) && sameMarkSolid(row.markSolidCurrent, row.verificationSolidAfter), 'production package marking persisted geometry, configuration or native verification mismatch')
    requireValue(Object.hasOwn(row, 'marks') === Object.hasOwn(baseline, 'marks') && (!Object.hasOwn(baseline, 'marks') ||
      Array.isArray(baseline.marks) && baseline.marks.length > 0 && Array.isArray(row.marks) &&
      same16029PackageMarksEvidence(remapPackageEvidence(baseline.marks, mapped, true), remapPackageEvidence(row.marks, final))), 'production package marking segment or path mapping mismatch')
  }
  const source = remapPackageEvidence(appliedLocators.source, mapped), changes = [...appliedLocators.changes.values()].map(row => remapPackageEvidence(row, mapped))
  verifyProductionLocators({ rows: packagedRows }, { source, mirror: productionLocatorMirror(source.mirrorLink, source, 'production package'),
    changes: new Map(changes.map(row => [key(row.path), row])) }, packagedRevisions, packagedMarks)
  return revisions.map(revision => {
    const mapping = mapped.get(key(revision.path))
    return { sourcePath: revision.path, sourceSha256: revision.newProductionSha256, path: mapping.path, sha256: mapping.sha256, file: basename(mapping.path), allowedChangeReason: mapping.allowedChangeReason }
  })
}

export function read16029ParametricResumeSnapshot(sourceAttempt, backupPhase, priorPlan, priorReceipt, priorReceiptPath, { requireFailedNextPhase = false } = {}) {
  const attempt = resolve(sourceAttempt), context = readAttemptIdentity(attempt), chain = readStageChain(context)
  const prior = chain.stages.get(priorReceipt.phase)
  requireValue(prior && same(context.plan, priorPlan) && same(prior.receipt, priorReceipt) && resolve(priorReceiptPath) === prior.receiptPath, 'snapshot caller identity differs from saved evidence')
  const next = chain.stages.get(backupPhase)
  if (requireFailedNextPhase) requireValue(!existsSync(resolve(attempt, 'receipts', backupPhase + '.json')), 'authorized input snapshot cannot replace an existing receipt')
  const auth = readAuthorization(context, backupPhase)
  requireValue(auth.authorization.predecessor === prior.receiptSha256 && same(auth.authorization.preInventory, priorReceipt.after), 'snapshot authorization predecessor or inventory mismatch')
  if (next) {
    requireValue(next.receipt.predecessor === prior.receiptSha256 && same(next.receipt.before, priorReceipt.after), 'snapshot successful next-stage input mismatch')
  } else {
    requireValue(priorReceipt.phase === 'base-hole' && backupPhase === 'assemblies' && priorReceipt.doorInventory.length === 0, 'authorized input snapshot requires base-hole to assemblies and no doors')
    requireValue(chain.ordered.at(-1) === prior && same(chain.ordered.map(row => row.phase), ['clone', 'probe', 'dimensions', 'derived', 'base-hole']), 'snapshot predecessor receipt chain incomplete or has future receipts')
    requireValue(['blocked', 'cancelled', 'failed'].includes(context.task.nativeBuild.state) && !context.task.nativeBuild.lease, 'snapshot source task is still active')
    const authNames = readdirSync(resolve(attempt, 'authorizations')).sort()
    requireValue(same(authNames, [...chain.stages.keys(), backupPhase].map(phase => phase + '.json').sort()), 'snapshot authorization inventory has missing or extra entries')
    const nextEvidence = resolve(attempt, 'evidence', backupPhase + '.json')
    requireValue(!existsSync(nextEvidence) || read(scoped(nextEvidence, attempt, 'snapshot failed evidence')).success !== true, 'snapshot cannot bypass successful evidence without a receipt')
  }
  const snapshotSource = scoped(resolve(attempt, 'backups', backupPhase), attempt, 'snapshot source')
  requireValue(lstatSync(snapshotSource).isDirectory(), 'snapshot source is not a directory')
  const rows = snapshotInventory(context, snapshotSource, priorReceipt)
  return { snapshotSource, backupPhase, rows, cadRows: rows.filter(row => dirname(row.path) === snapshotSource), protectedRows: inventory16029ProductionFiles(attempt),
    snapshotProof: next ? 'successful-next-phase-input' : 'authorized-next-phase-input', snapshotAuthorizationPath: auth.authorizationPath,
    snapshotAuthorizationSha256: auth.authorizationSha256, snapshotPriorReceiptSha256: prior.receiptSha256 }
}

export function read16029AuthorizedNextPhaseInputSnapshot(sourceAttempt, backupPhase, priorPlan, priorReceipt, priorReceiptPath) {
  return read16029ParametricResumeSnapshot(sourceAttempt, backupPhase, priorPlan, priorReceipt, priorReceiptPath, { requireFailedNextPhase: true })
}

export function verify16029ProductionEvidence(attemptPath, { throughPhase = 'package' } = {}) {
  requireValue(['production-apply', 'production-verify', 'package'].includes(throughPhase), 'unsupported production evidence phase')
  const attempt = resolve(attemptPath), context = readAttemptIdentity(attempt)
  const productionProfile = production16029Profile(context.plan.contract.input)
  requireValue(productionProfile && context.build.productionProfile === productionProfile && context.plan.contractSha256 === context.contractSha256, 'production profile, contract hash or pinned build profile missing')
  const { stages } = readStageChain(context), feedback = stages.get('repair-engineer-feedback'), apply = stages.get('production-apply')
  requireValue(feedback && apply && apply.receipt.predecessor === feedback.receiptSha256 && same(feedback.receipt.after, apply.receipt.before), 'production apply must follow engineer feedback repair')
  const working = resolve(attempt, 'native_cad/working_pack'), doors = resolve(attempt, 'doors')
  requireValue(Array.isArray(apply.receipt.beforeDoorInventory) && same(apply.receipt.beforeDoorInventory, feedback.receipt.doorInventory) && same(apply.receipt.beforeDoorInventory, apply.receipt.doorInventory), 'production apply changed protected door inventory')
  requireValue(Array.isArray(apply.receipt.protectedDoorFilesBefore) && apply.receipt.protectedDoorFilesBefore.length > 0 &&
    same(apply.receipt.protectedDoorFilesBefore, apply.authorization.protectedDoorFilesBefore) && same(apply.receipt.protectedDoorFilesBefore, apply.receipt.protectedDoorFilesAfter) &&
    same(apply.receipt.protectedDoorFilesAfter, inventory16029ProductionFiles(doors)), 'production apply changed protected door files')
  assertCurrentInventory(doors, apply.receipt.doorInventory, 'protected production doors', { flat: false, allowEmpty: false })
  const before = inventoryMap(apply.receipt.before, working, 'production apply before'), after = inventoryMap(apply.receipt.after, working, 'production apply after')
  requireValue(before.size === after.size && [...before.keys()].every(path => after.has(path)), 'production apply added or removed CAD')
  const changed = new Map([...after].filter(([path, row]) => before.get(path).sha256 !== row.sha256))
  requireValue([...changed.values()].every(row => /\.SLDPRT$/i.test(row.path)), 'production apply changed an assembly')
  const revisions = apply.evidence.rows?.filter(row => row.inspection === 'production_part_revision')
  requireValue(Array.isArray(revisions) && revisions.length > 0 && revisions.length === changed.size, 'production part revision coverage mismatch')
  const proofFiles = new Map()
  for (const proof of readDoorProofs(context).values()) for (const [file, edit] of proof.files) {
    requireValue(!proofFiles.has(file), 'production door proof file duplicated'); proofFiles.set(file, { proof, edit })
  }
  const localized = new Map()
  for (const row of feedback.evidence.rows?.filter(row => row.inspection === 'production_localized_file') || []) {
    const path = scoped(row.path, working, 'localized production file'), sourcePath = scoped(row.sourcePath, doors, 'localized door source')
    requireValue(row.file === basename(path) && basename(sourcePath) === row.file && dirname(path) === working && !localized.has(key(path)) &&
      row.productionProfile === productionProfile && hash(sourcePath) === row.sourceSha256 && SHA256.test(row.sha256) && before.get(key(path))?.sha256 === row.sha256, 'localized production file binding mismatch')
    localized.set(key(path), row)
  }
  const revisionFiles = new Set()
  for (const row of revisions) {
    const path = scoped(row.path, working, 'production revision'), id = key(path), current = changed.get(id)
    requireValue(row.file === basename(path) && dirname(path) === working && /\.SLDPRT$/i.test(path) && !revisionFiles.has(id) && current &&
      row.oldSha256 === before.get(id).sha256 && row.newProductionSha256 === current.sha256 && row.productionProfile === productionProfile &&
      physical16029PartRole(row.file) !== 'unknown' && row.role === physical16029PartRole(row.file) && typeof row.hasSheetMetal === 'boolean' && typeof row.hasFlatPattern === 'boolean' &&
      (!row.hasSheetMetal || row.hasFlatPattern), 'production part revision identity, hashes or health mismatch')
    revisionFiles.add(id)
    const door = proofFiles.get(row.file.toLowerCase())
    if (door) {
      requireValue(localized.has(id) && row.oldDoorSha256 === door.edit.sha256_after_save && row.doorEvidenceSha256 === door.proof.evidenceSha256 &&
        localized.get(id).sourceSha256 === row.oldDoorSha256, 'production door revision provenance mismatch')
    } else requireValue(row.oldDoorSha256 === '' && row.doorEvidenceSha256 === '', 'non-door production revision has unbound door evidence')
  }
  const applyMarks = productionMarkRows(apply.evidence, 'production_marks_apply', revisions, 'production apply')
  for (const row of applyMarks.values()) requireValue(SHA256.test(row.sourceFileSha256BeforeCallerSave) && sameMarkSolid(row.markSolidBefore, row.markSolidAfter), 'production marking changed solid geometry')
  const appliedLocators = productionLocatorApply(apply.evidence, revisions, applyMarks, working)
  assertCurrentInventory(working, apply.receipt.after, 'production working package')
  let verification = null, packageStage = null, verifyMarks = null, packageRevisions = []
  if (throughPhase !== 'production-apply') {
    verification = stages.get('production-verify')
    requireValue(verification && verification.receipt.predecessor === apply.receiptSha256 && same(verification.receipt.before, apply.receipt.after) &&
      same(verification.receipt.before, verification.receipt.after) && same(verification.receipt.beforeDoorInventory, apply.receipt.doorInventory) &&
      same(verification.receipt.beforeDoorInventory, verification.receipt.doorInventory), 'production verification changed CAD or failed predecessor binding')
    requireValue(same(verification.receipt.protectedDoorFilesBefore, apply.receipt.protectedDoorFilesAfter) && same(verification.receipt.protectedDoorFilesBefore, verification.authorization.protectedDoorFilesBefore) &&
      same(verification.receipt.protectedDoorFilesBefore, verification.receipt.protectedDoorFilesAfter) && same(verification.receipt.protectedDoorFilesAfter, inventory16029ProductionFiles(doors)), 'production verification changed protected door files')
    const rows = verification.evidence.rows?.filter(row => row.inspection === 'production_part_verification')
    requireValue(Array.isArray(rows) && rows.length === revisions.length && new Set(rows.map(row => key(row.path))).size === rows.length, 'production verification coverage mismatch')
    for (const revision of revisions) {
      const row = rows.find(row => row.path === revision.path)
      requireValue(row && row.file === revision.file && row.role === revision.role && row.sha256 === revision.newProductionSha256 && row.productionProfile === productionProfile && row.pass === true, 'production part verification binding mismatch')
    }
    verifyMarks = productionMarkRows(verification.evidence, 'production_marks_verify', revisions, 'production verify')
    requireValue(verifyMarks.size === applyMarks.size && [...applyMarks.keys()].every(id => verifyMarks.has(id)), 'production marking configuration coverage mismatch')
    for (const [id, row] of verifyMarks) {
      const applied = applyMarks.get(id), revision = revisions.find(item => key(item.path) === key(row.path))
      requireValue(row.sha256 === revision.newProductionSha256 && row.protectedFeatureStateSha256 === applied.protectedFeatureStateSha256 && sameMarkSolid(applied.markSolidAfter, row.markSolidCurrent), 'production marking persisted geometry or hash mismatch')
    }
    verifyProductionLocators(verification.evidence, appliedLocators, revisions, verifyMarks)
  }
  if (throughPhase === 'package') {
    packageStage = stages.get('package')
    const verify = stages.get('verify')
    requireValue(packageStage && verify && verify.receipt.predecessor === verification.receiptSha256 && packageStage.receipt.predecessor === verify.receiptSha256 &&
      same(verify.receipt.before, verification.receipt.after) && same(verify.receipt.before, verify.receipt.after) && same(packageStage.receipt.before, verification.receipt.after), 'production package missing, changed working inventory or out of sequence')
    packageRevisions = verifyProductionPackage(context, packageStage, verification, revisions, verifyMarks, appliedLocators, working, doors, productionProfile)
  }
  return { ok: true, productionProfile, throughPhase, taskId: context.plan.taskId, planSha256: context.planSha256, contractSha256: context.contractSha256,
    executableSha256: context.executableSha256, applyReceiptSha256: apply.receiptSha256, applyEvidenceSha256: apply.evidenceSha256,
    verificationReceiptSha256: verification?.receiptSha256 || '', verificationEvidenceSha256: verification?.evidenceSha256 || '',
    packageEvidenceSha256: packageStage?.evidenceSha256 || '', revisions, packageRevisions }
}
