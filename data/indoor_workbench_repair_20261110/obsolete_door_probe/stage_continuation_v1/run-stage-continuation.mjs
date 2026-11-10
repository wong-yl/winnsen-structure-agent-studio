// Throwaway research runner. No production receipts, resume source, publication, or CAD cleanup.
import { createHash, randomBytes } from 'node:crypto'
import { constants, copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { spawnSync } from 'node:child_process'

export const ROOT = 'D:\\Winnsen_Structure_Agent_Studio'
const HERE = dirname(fileURLToPath(import.meta.url))
const PROBE = resolve(ROOT, 'data/indoor_workbench_repair_20261110/obsolete_door_probe')
const ATTEMPTS = resolve(ROOT, 'data/parametric_attempts')
const ORIGINAL = resolve(ATTEMPTS, 'PARAM-20261110035409405-0C0A4D')
const PACK = resolve(PROBE, 'candidate_pack_v6')
const ROOT_NAME = '标准寄存柜1917×1000×550(总装配).SLDASM'
const PROFILE = 'readonly-open-20261110-r4-doorprecision3'
const RUNNING_PORTAL_STATES = ['created', 'claimed', 'planning', 'building', 'validating', 'cancel_requested']
const EXPECTED_EXE = 'AD7E642311109DAE8ADD7463B7A8AEB60877301F59B6DDF08BBC3B97F7EB56FA'
const EXPECTED_ROOT = '84ADA16033D0AC42FCA7461348D9E692DDE3DE154E755995AC097081F27B4F3A'
const PINS = [
  [resolve(ORIGINAL, 'parametric_execution.json'), 'ABE2F394BB5474243882174E3D068F52D84DD50F4F8F0B45928FA22FEB8F6F54'],
  [resolve(ORIGINAL, 'receipts/assemblies.json'), '1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD'],
  [resolve(ORIGINAL, 'evidence/assemblies.json'), '2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2'],
  [resolve(PROBE, 'runs/20261110084549485-v6-edit-31788/probe.json'), '87D9334E7BC9992962021F810FF230B401DC7F1DFFB2BC8FF07E87AD6BE88F5F'],
  [resolve(PROBE, 'runs/20261110091308144-v6-diagnostic33-18608/probe.json'), '4F79819F58A4FC5B8CBE0833E887201DE7D4FEE5F98B448884624C578978BD20'],
  [resolve(PROBE, 'axis_equivalence_v1/report-20261110T093042673218Z-33460.json'), 'C6325B4F9B7FE08FAA0CB84A1E9EAA2A6FE378044C6216A77BFB4FF30A4EE323'],
  [resolve(PROBE, 'runs/20261110083234105-relink-v6-2128/probe.json'), '73209BAFB0674E7B834F9878699406F3E3AC05E5C2A9D1A8F70052E221EFE43F'],
]
export const DERIVED_NAMES = [
  '底座 模型.sldprt', '底座底板.sldprt', '底座加强筋.sldprt', '底座外框.sldprt',
  '上盖 模型.sldprt', '上盖壳体底板.sldprt', '上盖壳体后侧板.sldprt', '上盖壳体前侧板.sldprt', '上盖壳体左侧板.sldprt', '上盖壳体右侧板.SLDPRT',
  '箱体横层板L.sldprt', '箱体横层板R.SLDPRT', '门框 横隔板.sldprt', '门框 横隔板R.SLDPRT', '门框 上.sldprt', '门框 左.sldprt', '门框 右.sldprt', '门框 下.sldprt',
  '箱体侧板加强筋1.sldprt', '箱体右侧板.sldprt', '箱体左侧板.sldprt',
  '箱体竖隔板L.sldprt', '箱体竖隔板R.SLDPRT', '门框 竖隔板L.sldprt', '门框 竖隔板R.SLDPRT', '箱体竖隔板加强件.sldprt', '箱体侧板加强筋2.sldprt',
]
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
export const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)
const need = (condition, message) => { if (!condition) throw Error(message) }
const put = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n', { flag: 'wx' })
const isCad = name => !name.startsWith('~$') && /\.SLD(?:PRT|ASM)$/i.test(name)
const stamp = () => new Date().toISOString().replace(/[^0-9]/g, '')
const under = (root, path) => { const rel = relative(root, resolve(path)); return rel && !rel.startsWith('..') && !isAbsolute(rel) }
function regular(path) {
  const stat = lstatSync(path)
  need(stat.isFile() && stat.nlink === 1, 'Regular single-link file required: ' + path)
  for (let current = resolve(path); ; current = dirname(current)) {
    need(!lstatSync(current).isSymbolicLink(), 'Linked path forbidden: ' + current)
    if (dirname(current) === current) break
  }
  return { path: resolve(path), sha256: hash(path), size: stat.size }
}
function walk(directory, skipOwn = false) {
  need(lstatSync(directory).isDirectory() && !lstatSync(directory).isSymbolicLink(), 'Linked directory forbidden: ' + directory)
  const rows = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name)
    if (skipOwn && path === HERE) continue
    need(!entry.isSymbolicLink(), 'Linked entry forbidden: ' + path)
    if (entry.isDirectory()) rows.push(...walk(path, skipOwn))
    else rows.push(regular(path))
  }
  return rows
}
function inventory(folder) {
  return walk(folder).filter(row => isCad(basename(row.path))).map(row => ({ ...row, relativePath: relative(folder, row.path) })).sort((a, b) => a.relativePath.localeCompare(b.relativePath))
}
function unchanged(rows) {
  const changes = rows.filter(row => !existsSync(row.path) || regular(row.path).sha256 !== row.sha256)
  need(!changes.length, 'PROTECTED_SOURCE_CHANGED: ' + changes.map(row => row.path).join(', '))
}
function unique(rows, predicate, label) {
  const matches = rows.filter(predicate)
  need(matches.length === 1, 'Expected unique evidence: ' + label)
  return matches[0]
}
function event(proof, inspection, stage) {
  return unique(proof.events || [], row => row.inspection === inspection && (stage === undefined || row.stage === stage), inspection + '/' + stage)
}
function frozenDependencies(relink, edit, currentRows) {
  const owners = relink.events.filter(row => row.inspection === 'owner_dependency_verification')
  const oldInventory = event(relink, 'cad_inventory', 'candidate-after').files
  need(owners.length === 75 && oldInventory.length === 75, 'Frozen dependency graph must cover all 75 owners')
  const removedNames = ['L2', 'L4', 'L6', 'R2', 'R4', 'R6'].map(token => '储物柜门装配_' + token + '.SLDASM')
  const direct = event(edit, 'snapshot', 'after-save').state.components.filter(row => row.depth === 1)
  const directPaths = [...new Set(direct.map(row => row.path.toLowerCase()))].sort()
  need(direct.length === 19 && directPaths.length === 13, 'Edited root direct-component inventory differs')
  const result = currentRows.map(file => {
    const owner = unique(owners, row => row.owner.toLowerCase() === file.path.toLowerCase(), 'frozen owner')
    need(owner.matchesFrozenMappedPlan === true && owner.observed.owner.toLowerCase() === file.path.toLowerCase() && Array.isArray(owner.observed.edges), 'Frozen owner binding differs')
    const old = unique(oldInventory, row => row.path.toLowerCase() === file.path.toLowerCase(), 'relink inventory')
    let sources = owner.observed.edges.map(edge => {
      unique(currentRows, row => row.path.toLowerCase() === String(edge.candidatePath).toLowerCase(), 'frozen edge candidate')
      return edge.candidatePath
    })
    if (basename(file.path) === ROOT_NAME) {
      need(sources.length === 19 && removedNames.every(name => sources.filter(path => basename(path) === name).length === 1), 'Root obsolete dependency set differs')
      sources = sources.filter(path => !removedNames.includes(basename(path)))
      need(sources.length === 13 && same(sources.map(path => path.toLowerCase()).sort(), directPaths), 'Edited root direct paths do not prove the remaining frozen dependency graph')
    } else need(old.sha256 === file.sha256 && old.size === file.size, 'Nonroot owner changed after frozen graph: ' + file.path)
    return { owner: file.path, dependencySources: sources }
  })
  need(result.reduce((count, row) => count + row.dependencySources.length, 0) === 140, 'Frozen post-delete edge count differs')
  return result
}
export function makePlan(prior, id, workerId, storeDir) {
  return { schema: prior.schema, purpose: prior.purpose, feedbackRepairRequired: prior.feedbackRepairRequired,
    taskId: id, taskPath: resolve(storeDir, id + '.json'), workerId,
    contract: structuredClone(prior.contract), contractSha256: prior.contractSha256,
    resumedEvidence: null, sourceRightPartitionSha256: prior.sourceRightPartitionSha256,
    doorModules: structuredClone(prior.doorModules) }
}
function processSnapshot() {
  need(process.platform === 'win32', 'This bound research runner requires Windows')
  const script = "$ErrorActionPreference='Stop'; $rows=@(Get-Process -Name SLDWORKS,sldProcMon,sldworks_fs -ErrorAction SilentlyContinue | ForEach-Object { @{pid=$_.Id;name=$_.ProcessName;startTicks=$_.StartTime.ToUniversalTime().Ticks.ToString();executable=$_.Path} }); @{processes=$rows} | ConvertTo-Json -Depth 4 -Compress"
  const result = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { windowsHide: true, encoding: 'utf8', timeout: 10000 })
  need(result.status === 0, result.stderr || result.error?.message || 'Process inspection failed')
  const rows = JSON.parse(result.stdout).processes
  need(Array.isArray(rows), 'Invalid process inspection')
  const protectedFs = rows.find(row => row.pid === 13080)
  if (protectedFs) need(protectedFs.name.toLowerCase() === 'sldworks_fs' && protectedFs.startTicks === '639298656825633090', 'Protected filesystem process identity changed')
  return { processes: rows, cadAndMonitorEmpty: !rows.some(row => ['sldworks', 'sldprocmon'].includes(row.name.toLowerCase())), protectedFs: protectedFs || null }
}
function portalFiles() {
  const directory = resolve(ROOT, 'data/native_model_requests')
  return readdirSync(directory, { withFileTypes: true }).filter(entry => entry.isFile() && entry.name.endsWith('.json')).map(entry => regular(resolve(directory, entry.name))).filter(row => { const value = read(row.path); return value.id && value.nativeBuild })
}
async function preflight() {
  need(resolve(process.cwd()).toLowerCase() === ROOT.toLowerCase(), 'Run from the actual project root, not a worktree')
  need(HERE === resolve(PROBE, 'stage_continuation_v1'), 'Runner is outside its assigned research directory')
  for (const [path, expected] of PINS) need(regular(path).sha256 === expected, 'Pinned input changed: ' + path)
  const prior = read(PINS[0][0]), sourceReceipt = read(PINS[1][0]), edit = read(PINS[3][0]), diagnostic = read(PINS[4][0]), axis = read(PINS[5][0])
  need(prior.schema === 'winnsen.locker16029.parametric_execution.v1' && prior.purpose === 'structure_engineering_assistance' && prior.feedbackRepairRequired === true, 'Original plan contract invalid')
  need(prior.contractSha256 === sha(JSON.stringify(prior.contract)) && same(prior.contract.geometry.cabinet, { widthMm: 900, heightMm: 2000, depthMm: 500 }), 'Original dimensions changed')
  need(same(prior.doorModules, [{ token: '471p25', heightMm: 471.25 }, { token: '205p555556', heightMm: 205.55555555555554 }]), 'Original door map changed')
  need(sourceReceipt.phase === 'assemblies' && sourceReceipt.evidenceSha256 === PINS[2][1] && read(PINS[2][0]).success === true, 'Original assemblies checkpoint invalid')
  const editEnd = event(edit, 'completion'), diagnosticEnd = event(diagnostic, 'completion')
  need(editEnd.mode === 'edit' && editEnd.operationComplete === true && editEnd.modelAccepted === false && editEnd.rootAfterSha256 === EXPECTED_ROOT && editEnd.candidatePack === PACK && editEnd.other74CandidateUnchanged === true, 'v6 edit binding invalid')
  need(diagnosticEnd.mode === 'diagnostic33' && diagnosticEnd.diagnosticCompleted === false && diagnosticEnd.operationComplete === false && diagnosticEnd.noSave === true && diagnosticEnd.all75CandidateUnchanged === true && diagnosticEnd.modelAccepted === false && diagnosticEnd.strictColdAcceptancePassed === false && diagnosticEnd.candidatePack === PACK && diagnosticEnd.error.startsWith('System.InvalidOperationException: NUMERIC_STATE_DRIFT '), 'v6 diagnostic binding invalid')
  const rawOpen = event(diagnostic, 'raw_open')
  need(rawOpen.errors === 0 && rawOpen.warnings === 32 && rawOpen.strictOpenPassed === false && rawOpen.actualPath === resolve(PACK, ROOT_NAME), 'Diagnostic input is not exact errors=0/warnings=32')
  need(axis.rawEqual === false && axis.cylinderAxisRepresentationEquivalent === true && axis.warning32Unresolved === true && axis.modelAccepted === false && axis.offlineChecksPassed === true && axis.negativeControls?.length === 15 && axis.negativeControls.every(row => row.pass === true), 'Axis analysis is not the approved limited research evidence')
  const rows = inventory(PACK)
  need(readdirSync(PACK).length === 75 && rows.length === 75 && rows.every(row => dirname(row.path) === PACK), 'Exact flat 75-file candidate required')
  const expectedRows = event(edit, 'cad_inventory', 'candidate-after').files
  const diagnosticRows = event(diagnostic, 'cad_inventory', 'candidate-after').files
  need(expectedRows.length === 75 && diagnosticRows.length === 75, 'Probe inventory count differs')
  for (const row of rows) for (const [label, bound] of [['edit', expectedRows], ['diagnostic', diagnosticRows]]) {
    const expected = unique(bound, expected => expected.path.toLowerCase() === row.path.toLowerCase(), label + ' inventory path')
    need(expected.sha256 === row.sha256 && expected.size === row.size, label + ' exact inventory differs: ' + row.path)
  }
  const dependencies = frozenDependencies(read(PINS[6][0]), edit, rows)
  need(hash(resolve(PACK, ROOT_NAME)) === EXPECTED_ROOT, 'Candidate root differs')
  const { verify16029ParametricBuild } = await import(pathToFileURL(resolve(ROOT, 'tools/lib/locker_16029_parametric_build.mjs')).href)
  const build = verify16029ParametricBuild(PROFILE, { root: ROOT, repairProfile: PROFILE })
  need(build.manifest.executableSha256 === EXPECTED_EXE && regular(build.executable).sha256 === EXPECTED_EXE && !build.manifest.productionProfile, 'Frozen research build differs')
  const originals = read(resolve(ORIGINAL, 'protected_sources.before.json'))
  unchanged([...originals, ...sourceReceipt.after])
  const protectedRows = [...originals, ...sourceReceipt.after, ...walk(PROBE, true), ...walk(resolve(ORIGINAL, 'tools')),
    ...walk(build.folder), ...walk(resolve(ROOT, 'workers/native_model_requests/parametric_v1/build-' + PROFILE)), ...walk(build.repairSource.sourceDirectory),
    ...['tools/run_16029_native_worker.mjs', 'tools/run_16029_parametric_model.mjs', 'tools/lib/locker_16029_native_task_store.mjs', 'tools/lib/locker_16029_parametric_process.mjs', 'tools/lib/locker_16029_parametric_build.mjs', 'tools/lib/locker_16029_parametric_runtime.mjs',
      'workers/native_model_requests/parametric_v1/runtime-repair-selection.json', 'workers/native_model_requests/parametric_v1/build-selection.json'].map(path => regular(resolve(ROOT, path))),
    ...PINS.map(([path]) => regular(path)), regular(fileURLToPath(import.meta.url))]
  const deduplicated = [...new Map(protectedRows.map(row => [row.path.toLowerCase(), row])).values()]
  unchanged(deduplicated)
  return { prior, rows, dependencies, build, protectedRows: deduplicated, process: processSnapshot(), axis: { rawEqual: false, cylinderAxisRepresentationEquivalent: true, finiteBoundariesFullyCompared: false, negativeControlsPassed: 15 } }
}
export function checkClone(proof, mapping, dependencies) {
  need(proof.phase === 'clone' && proof.success === true && Array.isArray(proof.rows) && proof.rows.length > 0, 'Clone evidence is incomplete')
  need(Array.isArray(dependencies) && dependencies.length === mapping.length, 'Complete frozen owner dependency graph required')
  const seen = new Set(), replacements = []
  for (const row of proof.rows) {
    const file = unique(mapping, item => item.destination.toLowerCase() === String(row.file).toLowerCase(), 'clone file')
    need(!seen.has(file.destination.toLowerCase()) && Array.isArray(row.replacements), 'Duplicate/invalid clone row')
    seen.add(file.destination.toLowerCase())
    const expected = unique(dependencies, owner => owner.owner.toLowerCase() === file.source.toLowerCase(), 'clone frozen owner').dependencySources
    for (const item of row.replacements) {
      const bound = unique(mapping, target => target.source.toLowerCase() === String(item.source).toLowerCase(), 'clone old source')
      need(item.replaced === true && bound.destination.toLowerCase() === String(item.target).toLowerCase(), 'Clone replacement is outside exact 75 source/destination mapping')
      replacements.push(item)
    }
    need(same(row.replacements.map(item => item.source.toLowerCase()).sort(), expected.map(path => path.toLowerCase()).sort()), 'Clone replacement multiset omits or adds frozen dependency edges: ' + file.destination)
  }
  for (const owner of dependencies) if (owner.dependencySources.length) {
    const file = unique(mapping, item => item.source.toLowerCase() === owner.owner.toLowerCase(), 'required dependency owner')
    need(seen.has(file.destination.toLowerCase()), 'Clone omitted a nonempty dependency owner: ' + file.destination)
  }
  need(replacements.length > 0, 'Fresh clone has no observed reference replacements')
  return { filesWithDependencyRows: seen.size, replacementCount: replacements.length, exactSourceDestinationMapping: true, allFrozenDependencyEdgesReplaced: true, fullPostCloneDependencyReenumerationVerified: false }
}
function boxClose(a, b, tolerance) { return Array.isArray(a) && Array.isArray(b) && a.length === 6 && b.length === 6 && a.every((value, i) => Number.isFinite(value) && Number.isFinite(b[i]) && Math.abs(value - b[i]) < tolerance) }
export function checkEnvelope(proof, contract) {
  need(proof.phase === 'envelope' && proof.success === true && Array.isArray(proof.rows), 'Envelope evidence is not successful')
  const rows = proof.rows, cabinet = contract.geometry.cabinet
  const summaries = rows.filter(row => row.diagnosticOnly === true && Object.hasOwn(row, 'saved'))
  need(summaries.length === DERIVED_NAMES.length, 'Missing/extra derived summaries')
  for (const name of DERIVED_NAMES) {
    const summary = unique(summaries, row => row.file === name, 'derived summary ' + name)
    need(summary.rebuilt === true && summary.healthy === true && summary.saved === true && Array.isArray(summary.issues) && summary.issues.length === 0 && Array.isArray(summary.links) && summary.links.every(link => link.blockedByBrokenReference !== true), 'Derived rebuild/health/save failed: ' + name)
    if (name !== '箱体竖隔板R.SLDPRT') {
      const boundary = unique(rows, row => row.inspection === 'envelope_target_boundary' && row.file === name, 'target boundary ' + name)
      need(boundary.healthy === true && boxClose(boundary.afterGeometry?.part_box_m, boundary.expectedBox, 0.0002), 'Native target boundary failed: ' + name)
    }
  }
  const master = unique(rows, row => row.file === '标准寄存柜 模型.SLDPRT' && row.dimension === 'D2@草图1', 'master dimension')
  need(master.rebuilt === true && master.healthy === true && master.targetMm === cabinet.heightMm && Math.abs(master.readbackMm - cabinet.heightMm) < .001, 'Master height readback differs')
  const reopen = unique(rows, row => row.inspection === 'master_frame_pattern_saved_reopened', 'master reopen')
  need(reopen.rebuilt === true && boxClose(reopen.snapshot?.part_box_m, master.afterGeometry?.part_box_m, .000001) && reopen.snapshot?.body_count === master.afterGeometry?.body_count, 'Master reopen differs')
  for (const name of ['箱体右侧板.sldprt', '箱体左侧板.sldprt']) {
    const reopened = unique(rows, row => row.inspection === 'rear_hole_saved_reopened' && row.file === name, 'side reopen ' + name)
    const summary = summaries.find(row => row.file === name)
    need(boxClose(reopened.snapshot?.part_box_m, summary.afterGeometry?.part_box_m, .000001) && reopened.snapshot?.body_count === summary.afterGeometry?.body_count, 'Side saved reopen differs: ' + name)
  }
  const terminal = unique(rows, row => row.targetHeightMm === cabinet.heightMm && row.targetDepthMm === cabinet.depthMm && row.deliveryEligible === false && row.diagnosticOnly === false, 'envelope target conclusion')
  return { derivedSummaryCount: summaries.length, targetBoundaryCount: DERIVED_NAMES.length - 1, masterHeightReadbackMm: master.readbackMm, targetDepthMm: terminal.targetDepthMm,
    combinedHeightDepthGateReachedInFrozenNativeBuild: true, explicitDepthReadbackInJson: false, masterAndSidesSavedReopenObserved: true, completeColdReopenVerified: false }
}
async function dryRun() {
  const beforeAttempts = readdirSync(ATTEMPTS).sort(), beforePortal = portalFiles()
  const context = await preflight(), proposedId = 'PARAM-' + stamp() + '-' + randomBytes(3).toString('hex').toUpperCase()
  const proposedAttempt = resolve(ATTEMPTS, proposedId), workerId = 'research-envelope-' + process.pid
  const report = { schema: 'obsolete-door-stage-continuation-dryrun-v1', generatedAtUtc: new Date().toISOString(), mode: 'dry-run', nativeCadExecuted: false, cadCopiesCreated: 0, taskCreated: false,
    runner: regular(fileURLToPath(import.meta.url)), pins: PINS.map(([path, sha256]) => ({ path, sha256 })), build: { tag: PROFILE, executable: context.build.executable, executableSha256: EXPECTED_EXE },
    candidateRootSha256: EXPECTED_ROOT, candidateCount: context.rows.length, candidateInventorySha256: sha(JSON.stringify(context.rows)), frozenExpectedDependencies: context.dependencies, frozenExpectedEdgeCount: 140, process: context.process, axis: context.axis,
    proposedAttemptOnlyNotCreated: proposedAttempt, proposedPlanOnlyNotWritten: makePlan(context.prior, proposedId, workerId, resolve(proposedAttempt, 'task_store')),
    exactCopyMappingPreview: context.rows.map(row => ({ source: row.path, destination: resolve(proposedAttempt, 'native_cad/working_pack', row.relativePath), sha256: row.sha256, size: row.size })),
    protectedRows: context.protectedRows, protectedBeforeAfterUnchanged: true, warning32Unresolved: true, finiteBoundariesFullyCompared: false, strictColdAcceptancePassed: false, modelAccepted: false, releaseEligible: false, downloadProduced: false,
    portalSnapshotReadOnly: beforePortal.map(row => read(row.path)).filter(row => row.nativeBuild && row.id).map(row => ({ id: row.id, state: row.nativeBuild.state, lease: row.nativeBuild.lease, updatedAt: row.updatedAt, blocker: row.nativeBuild.blocker || null })),
    runGate: 'Recheck global lock, real portal created/claimed/planning/building/validating/cancel_requested tasks or any nonnull lease, and empty SLDWORKS/sldProcMon immediately before creating an attempt. Old blocked/needs_input records without leases remain unchanged and unresolved. Dry-run creates no lock or store.' }
  unchanged(context.protectedRows); unchanged(beforePortal)
  need(same(beforeAttempts, readdirSync(ATTEMPTS).sort()), 'Attempt inventory changed during dry-run')
  const reportPath = resolve(HERE, 'dry-run-' + stamp() + '-' + process.pid + '.json')
  put(reportPath, report)
  console.log(JSON.stringify({ state: 'dry_run_only', reportPath, sha256: hash(reportPath), candidateCount: 75, protectedCount: context.protectedRows.length, cadCopiesCreated: 0, taskCreated: false, modelAccepted: false }, null, 2))
}
async function runNative() {
  const context = await preflight()
  const { acquireNativeWorkerInstanceLock } = await import(pathToFileURL(resolve(ROOT, 'tools/run_16029_native_worker.mjs')).href)
  const { createNativeTaskStore } = await import(pathToFileURL(resolve(ROOT, 'tools/lib/locker_16029_native_task_store.mjs')).href)
  const { inspect16029CadProcesses, run16029ParametricPhaseProcess } = await import(pathToFileURL(resolve(ROOT, 'tools/lib/locker_16029_parametric_process.mjs')).href)
  const workerId = 'research-envelope-' + process.pid + '-' + randomBytes(3).toString('hex')
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, 'data'), workerId })
  let attempt = '', store, task, timer, heartbeatError, chain = Promise.resolve(), phase = '', nativeStarted = false
  const controller = new AbortController(), abort = () => controller.abort()
  const serial = operation => { const next = chain.then(operation); chain = next.catch(error => { heartbeatError ||= error }); return next }
  const leaseArgs = () => ({ taskId: task.id, workerId, leaseId: task.nativeBuild.lease.id, expectedRevision: task.revision })
  const pulse = () => serial(async () => { lock.heartbeat(); if (task?.nativeBuild?.lease) { task = await store.read(task.id); task = await store.heartbeat(leaseArgs()) } })
  const move = (toState, patch, suffix) => serial(async () => { task = await store.read(task.id); task = await store.transition({ ...leaseArgs(), transitionId: task.id + '-' + suffix, toState, patch }) })
  process.on('SIGINT', abort); process.on('SIGTERM', abort)
  try {
    const portalBefore = portalFiles()
    const portal = createNativeTaskStore({ dataDir: resolve(ROOT, 'data/native_model_requests') })
    const portalSnapshot = await portal.listSnapshot()
    const active = portalSnapshot.filter(row => RUNNING_PORTAL_STATES.includes(row.nativeBuild.state) || row.nativeBuild.lease != null)
    unchanged(portalBefore)
    need(!active.length, 'PORTAL_ACTIVE_TASKS_PRESENT: ' + active.map(row => row.id + ':' + row.nativeBuild.state).join(', '))
    need((await inspect16029CadProcesses()).verified === true && processSnapshot().cadAndMonitorEmpty, 'CAD_OR_MONITOR_NOT_EMPTY')
    unchanged(context.protectedRows)
    const id = 'PARAM-' + stamp() + '-' + randomBytes(3).toString('hex').toUpperCase()
    attempt = resolve(ATTEMPTS, id)
    mkdirSync(attempt)
    for (const folder of ['native_cad/working_pack', 'evidence', 'authorizations', 'research', 'tools']) mkdirSync(resolve(attempt, folder), { recursive: true })
    const working = resolve(attempt, 'native_cad/working_pack'), storeDir = resolve(attempt, 'task_store')
    const mapping = context.rows.map(row => ({ source: row.path, destination: resolve(working, row.relativePath), sha256: row.sha256, size: row.size }))
    put(resolve(attempt, 'research/source-and-copy-mapping.json'), { schema: 'obsolete-door-stage-continuation-mapping-v1', taskId: id, sourceIsResearchCandidate: true, productionResumeClaimed: false, pins: PINS.map(([path, sha256]) => ({ path, sha256 })), files: mapping, frozenExpectedDependencies: context.dependencies })
    put(resolve(attempt, 'research/protected-before.json'), { schema: 'obsolete-door-stage-continuation-protection-v1', rows: context.protectedRows, process: context.process })
    put(resolve(attempt, 'research/portal-before.json'), { schema: 'obsolete-door-stage-continuation-portal-observation-v1', records: portalSnapshot.map(row => ({ id: row.id, state: row.nativeBuild.state, lease: row.nativeBuild.lease, updatedAt: row.updatedAt, blocker: row.nativeBuild.blocker || null })), existingBlockedTasksUnresolved: true, existingTasksModified: false })
    for (const row of mapping) { copyFileSync(row.source, row.destination, constants.COPYFILE_EXCL); need(hash(row.destination) === row.sha256, 'Copy hash mismatch: ' + row.destination) }
    store = createNativeTaskStore({ dataDir: storeDir })
    const cabinet = context.prior.contract.geometry.cabinet
    await store.createOrImport({ id, username: 'authorized-isolated-envelope-research', createdAt: new Date().toISOString(), status: 'native_task_created', taskType: 'native_solidworks_build_task',
      requestFingerprint: sha(JSON.stringify({ researchOnly: true, id, input: context.prior.contract.input })), cabinetWidth: cabinet.widthMm, cabinetHeight: cabinet.heightMm, cabinetDepth: cabinet.depthMm, columns: 2, doorCount: context.prior.contract.geometry.doorCount,
      modelReady: false, engineeringAssistanceReady: false, modelAccepted: false, nativeBuild: { state: 'created', attempt: 0, modelReady: false, executionStarted: false } })
    task = await store.claimNext({ workerId, leaseMs: 30 * 60 * 1000 })
    need(task?.id === id, 'Independent research task claim mismatch')
    await move('planning', undefined, 'planning')
    const plan = makePlan(context.prior, id, workerId, storeDir), planPath = resolve(attempt, 'parametric_execution.json')
    put(planPath, plan)
    need(same(plan.contract, context.prior.contract) && same(plan.doorModules, context.prior.doorModules) && plan.resumedEvidence === null, 'Research plan changed the original contract')
    for (const name of ['NativeParametricAssembly.exe', 'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll', 'build.json']) copyFileSync(resolve(context.build.folder, name), resolve(attempt, 'tools', name), constants.COPYFILE_EXCL)
    const executable = resolve(attempt, 'tools/NativeParametricAssembly.exe')
    need(hash(executable) === EXPECTED_EXE, 'Pinned executable differs')
    await move('building', { execution: { started: true, cadStarted: false, modelReady: false, modelAccepted: false }, progress: { stage: 'clone', percent: 0 } }, 'building')
    timer = setInterval(() => { void pulse().catch(error => { heartbeatError ||= error }) }, 20000)
    let previousResearchSha256 = '', nonEnvelope = []
    const stageObservations = []
    for (phase of ['clone', 'envelope']) {
      await pulse(); if (heartbeatError) throw heartbeatError
      need(!controller.signal.aborted, 'Research cancelled before phase')
      need((await inspect16029CadProcesses()).verified === true, 'CAD_OR_MONITOR_NOT_EMPTY_BEFORE_' + phase)
      unchanged(context.protectedRows)
      const before = inventory(working)
      need(before.length === 75, 'Working inventory count changed')
      const authorizationPath = resolve(attempt, 'authorizations', phase + '.json')
      put(authorizationPath, { schema: 'obsolete-door-stage-continuation-authorization-v1', researchOnly: true, phase, planSha256: hash(planPath), executableSha256: hash(executable), taskId: id, workerId, leaseId: task.nativeBuild.lease.id,
        rightPartitionSha256: hash(resolve(working, '箱体竖隔板R.SLDPRT')), expiresAt: new Date(Date.now() + 22 * 60 * 1000).toISOString(), preInventory: before, preDoorInventory: [], predecessor: '', previousResearchSha256 })
      put(resolve(attempt, 'research', phase + '-before.json'), { schema: 'obsolete-door-stage-continuation-stage-before-v1', phase, inventory: before, exactSourceDestinationMapping: mapping })
      nativeStarted = true
      await run16029ParametricPhaseProcess(executable, [planPath, phase], { phase, attempt, cwd: working, timeoutMs: 20 * 60 * 1000, preserveCadOnFailure: true, signal: controller.signal,
        env: { ...process.env, WINNSEN_PARAMETRIC_PLAN_SHA256: hash(planPath), WINNSEN_PARAMETRIC_AUTH_PATH: authorizationPath, WINNSEN_PARAMETRIC_AUTH_SHA256: hash(authorizationPath) },
        isCancelled: async () => { if (heartbeatError) throw heartbeatError; return (await store.read(id)).nativeBuild.state === 'cancel_requested' },
        onProgress: message => console.log(message) })
      await chain; if (heartbeatError) throw heartbeatError
      unchanged(context.protectedRows)
      const evidencePath = resolve(attempt, 'evidence', phase + '.json'), proof = read(evidencePath)
      const observation = phase === 'clone' ? checkClone(proof, mapping, context.dependencies) : checkEnvelope(proof, context.prior.contract)
      const after = inventory(working)
      need(after.length === 75 && after.every(row => mapping.some(item => item.destination === row.path)), 'Phase changed exact inventory names')
      if (phase === 'clone') {
        const allowed = new Set(['标准寄存柜 模型.SLDPRT', ...DERIVED_NAMES, '箱体横层板L焊接.SLDASM', '箱体横层板R焊接.SLDASM'].map(name => name.toLowerCase()))
        nonEnvelope = after.filter(row => !allowed.has(basename(row.path).toLowerCase()))
      } else unchanged(nonEnvelope)
      const researchPath = resolve(attempt, 'research', phase + '-observation.json')
      put(researchPath, { schema: 'obsolete-door-stage-continuation-stage-observation-v1', phase, taskId: id, researchOnly: true, planSha256: hash(planPath), authorizationSha256: hash(authorizationPath), originalNativeEvidence: { path: evidencePath, sha256: hash(evidencePath) },
        beforeInventory: before, afterInventory: after, observation, previousResearchSha256, protectedSourcesUnchanged: true, modelAccepted: false, productionReceiptCreated: false })
      previousResearchSha256 = hash(researchPath); stageObservations.push({ phase, path: researchPath, sha256: previousResearchSha256 })
    }
    clearInterval(timer); timer = null; await chain
    await move('validating', { progress: { stage: 'research_observation_only', percent: 90 } }, 'validating')
    await move('blocked', { blocker: { code: 'RESEARCH_CANDIDATE_REQUIRES_VALIDATION', message: 'Only original clone/envelope research observations exist; warning32, full cold reopen and finite boundaries remain unverified.' },
      execution: { started: true, cadStarted: true, modelReady: false, modelAccepted: false }, validation: { researchOnly: true, modelAccepted: false, warning32Unresolved: true, finiteBoundariesFullyCompared: false } }, 'blocked-research-candidate')
    unchanged(context.protectedRows)
    put(resolve(attempt, 'research/protected-after.json'), { schema: 'obsolete-door-stage-continuation-protection-v1', rows: context.protectedRows.map(row => regular(row.path)), unchanged: true, process: processSnapshot() })
    const resultPath = resolve(attempt, 'research/result.json')
    put(resultPath, { schema: 'obsolete-door-stage-continuation-result-v1', state: 'candidate_requires_validation', taskState: task.nativeBuild.state, attempt, taskId: id, stages: stageObservations,
      protectedSourcesUnchanged: true, productionReceiptCreated: false, warning32Unresolved: true, strictColdAcceptancePassed: false, finiteBoundariesFullyCompared: false, completeColdReopenVerified: false, modelAccepted: false, modelReady: false, releaseEligible: false, downloadProduced: false })
    console.log(JSON.stringify({ state: 'candidate_requires_validation', attempt, resultPath, sha256: hash(resultPath), modelAccepted: false }, null, 2))
  } catch (error) {
    clearInterval(timer); timer = null; await chain
    let protectionError = ''
    try { unchanged(context.protectedRows) } catch (failure) { protectionError = failure.message }
    if (task?.nativeBuild?.lease) {
      try { await move('blocked', { blocker: { code: 'RESEARCH_STAGE_FAILED_PRESERVED', message: error.message }, execution: { started: nativeStarted, cadStarted: nativeStarted, modelReady: false, modelAccepted: false } }, 'blocked-failure-' + (phase || 'setup')) } catch (failure) { protectionError += '\nTask transition: ' + failure.message }
    }
    if (attempt) {
      put(resolve(attempt, 'research/protected-after-failure.json'), { schema: 'obsolete-door-stage-continuation-protection-v1', rows: context.protectedRows.map(row => { try { return { path: row.path, beforeSha256: row.sha256, afterSha256: regular(row.path).sha256 } } catch (failure) { return { path: row.path, beforeSha256: row.sha256, error: failure.message } } }), unchanged: !protectionError })
      put(resolve(attempt, 'research/failure.json'), { schema: 'obsolete-door-stage-continuation-failure-v1', phase, error: error.message, protectionError, modelAccepted: false, preservedFailedCandidateArtifacts: true, rollbackPerformed: false, cleanupPerformed: false })
    }
    throw Error(error.message + (protectionError ? '\n' + protectionError : '') + (attempt ? '\nPreserved attempt: ' + attempt : ''))
  } finally {
    clearInterval(timer); await chain
    // The original process helper also waits without killing; retain the lock if any CAD/monitor remains.
    let observed = nativeStarted ? await inspect16029CadProcesses() : { verified: true }
    while (!observed.verified) {
      console.error('manual_disposition_required: CAD/monitor remains; global lock retained; no process is terminated.')
      lock.heartbeat(); await new Promise(done => setTimeout(done, 20000)); observed = await inspect16029CadProcesses()
    }
    try {
      const afterProcess = processSnapshot()
      if (context.process.protectedFs) need(same(afterProcess.protectedFs, context.process.protectedFs), 'Protected sldworks_fs process changed')
    } finally { process.off('SIGINT', abort); process.off('SIGTERM', abort); lock.release() }
  }
}

async function main() {
  need(process.argv.length === 3 && ['--dry-run', '--run-native'].includes(process.argv[2]), 'Usage: node run-stage-continuation.mjs <--dry-run|--run-native>')
  if (process.argv[2] === '--dry-run') await dryRun()
  else await runNative()
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error.stack || error.message); process.exitCode = 1 })
