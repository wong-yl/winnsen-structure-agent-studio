// Throwaway research runner. No production receipts, resume source, publication, or CAD cleanup.
import { createHash, randomBytes } from 'node:crypto'
import { constants, copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { spawnSync } from 'node:child_process'

import { STAGES, DOOR_MODULES, RIGHT_NAME, LEFT_NAME, RIGHT_SOURCE_NAME, V6_RIGHT_SHA, SOURCE_SHA, sourceLeaf, checkPrecision, checkSourceManifest, checkAuthorization, checkDeferredRight, checkDoor, checkRepair, checkLocks, equalPath, checkContinuationClone } from './research-gates.mjs'

export const ROOT = 'D:\\Winnsen_Structure_Agent_Studio'
const HERE = dirname(fileURLToPath(import.meta.url))
const PROBE = resolve(ROOT, 'data/indoor_workbench_repair_20261110/obsolete_door_probe')
const ATTEMPTS = resolve(ROOT, 'data/parametric_attempts')
const ORIGINAL = resolve(ATTEMPTS, 'PARAM-20261110035409405-0C0A4D')
const PACK = resolve(PROBE, 'candidate_pack_v6')
const SOURCE_ATTEMPT = resolve(ATTEMPTS, 'PARAM-20261110104244594-C292D9')
const SOURCE_BINDING = resolve(HERE, 'source-binding.json')
const SOURCE_BINDING_SHA = 'E2F59AE0B0DAE5665C249D63F9A7FD7612E70F976895604813C0B0129AFE4D7B'
const V1_ATTEMPT = resolve(ATTEMPTS, 'PARAM-20261110095819500-614465')
const ORIGINAL_SOURCE_MANIFEST = resolve(ORIGINAL, 'door_sources.json')
const V1_BASELINE = resolve(V1_ATTEMPT, 'research/protected-before.json')
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
PINS.push([ORIGINAL_SOURCE_MANIFEST, 'B7E325E05A87E5161D12D63C3F14B086CBBD71A90FBC5E8CE543C31AD63C95E9'], [V1_BASELINE, '37C85E703B6B89E2797D4E4C0F779D9530AB8A77F413BB82B205E3DAACA246B3'], [resolve(V1_ATTEMPT, 'evidence/envelope.json'), '83A687E4D7F657BB146B8FD427DDB408C8C4715A4A74B44630703355F177A54B'], [resolve(PROBE, 'stage_continuation_v1/run-stage-continuation.mjs'), '913FD65DA9B2EAD39E8B468F3280B6E56AFB6357636E1F6C28BEFD53E986EE25'], [resolve(PROBE, 'stage_continuation_v1/static-check.mjs'), '290A17580F6D3C62A62E104D72CCC2394FC7118B50FF48A2CF7D3292A0B5EEE0'])
const STATUS_EVIDENCE = resolve(ATTEMPTS, 'PARAM-20261110005000585-0301BA/evidence/locks.json')
PINS.push([STATUS_EVIDENCE, 'C67D5353BC996BECE6FEBBA7670765EE3850E842A42646459D404DF082E554D5'], [resolve(ORIGINAL, 'tools/SolidWorks.Interop.swconst.dll'), '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359'])
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
// Ancestor-only compatibility helpers; v3 execution never reuses this 140-edge expectation.
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
  need(HERE === resolve(PROBE, 'stage_continuation_v3') && hash(SOURCE_BINDING) === SOURCE_BINDING_SHA, 'V3 source binding changed')
  const source = read(SOURCE_BINDING)
  need(source.sourceAttempt === SOURCE_ATTEMPT && source.working.length === 75 && source.doors.length === 30 && source.sources.length === 13 && source.currentDependencyGraphComplete === false, 'V3 fixed source scope differs')
  for (const [path, expected] of PINS) need(hash(path) === expected, 'Ancestor pin changed: ' + path)
  unchanged([...source.allAncestorFiles, ...source.protectedV2Files])
  const historicalProtection = read(source.protectedBefore.path)
  need(historicalProtection.rows.length === 19052, 'V2 protection baseline count differs')
  unchanged(historicalProtection.rows)
  const failedProtection = read(source.protectedAfterFailure.path), failure = read(source.failure.path)
  need(failedProtection.unchanged === true && failedProtection.rows.length === 19052 && failedProtection.rows.every(row => row.beforeSha256 === row.afterSha256 && !row.error), 'V2 protected failure snapshot differs')
  need(failure.phase === 'repair-interfaces' && failure.error === 'Expected unique interface part' && failure.protectionError === '' && failure.rollbackPerformed === false && failure.cleanupPerformed === false, 'Unexpected ancestor failure')
  const prior = read(resolve(SOURCE_ATTEMPT, 'parametric_execution.json'))
  checkPrecision(prior)
  need(prior.contractSha256 === sha(JSON.stringify(prior.contract)) && prior.resumedEvidence === null, 'Ancestor full contract or resume identity changed')
  let predecessor = ''
  for (const node of source.stageNodes) {
    const auth = read(node.authorization.path), proof = read(node.nativeEvidence.path), processProof = read(node.processEvidence.path), before = read(node.beforeResearch.path)
    need(proof.success === true && auth.phase === node.phase && auth.planSha256 === hash(node.plan.path) && auth.taskId === prior.taskId && auth.workerId === prior.workerId && auth.executableSha256 === EXPECTED_EXE, 'Ancestor plan/authorization/native binding failed: ' + node.phase)
    need(processProof.code === 0 && processProof.preserveCadOnFailure === true && processProof.cleanup?.verified === true && !processProof.stopReason && equalPath(processProof.executable, resolve(SOURCE_ATTEMPT, 'tools/NativeParametricAssembly.exe')), 'Ancestor process evidence failed: ' + node.phase)
    need(before.previousResearchSha256 === predecessor && auth.previousResearchSha256 === predecessor && same(auth.preInventory, before.inventory) && same(auth.preDoorInventory, before.doorInventory), 'Ancestor chain before-inventory mismatch: ' + node.phase)
    if (node.observation) {
      const observation = read(node.observation.path)
      need(observation.phase === node.phase && observation.researchOnly === true && observation.modelAccepted === false && observation.productionReceiptCreated === false && observation.protectedSourcesUnchanged === true && observation.previousResearchSha256 === predecessor && observation.planSha256 === hash(node.plan.path) && observation.authorizationSha256 === hash(node.authorization.path) && observation.originalNativeEvidence.sha256 === hash(node.nativeEvidence.path) && observation.processEvidence.sha256 === hash(node.processEvidence.path), 'Ancestor observation hash chain failed: ' + node.phase)
      predecessor = hash(node.observation.path)
    }
  }
  const interfaceNode = source.stageNodes.at(-1), interfaceProof = read(interfaceNode.nativeEvidence.path), interfaceBefore = read(interfaceNode.beforeResearch.path)
  checkRepair(interfaceProof, 'repair-interfaces', resolve(SOURCE_ATTEMPT, 'native_cad/working_pack'), resolve(SOURCE_ATTEMPT, 'doors'), DOOR_MODULES)
  const rows = inventory(resolve(SOURCE_ATTEMPT, 'native_cad/working_pack')), doorRows = inventory(resolve(SOURCE_ATTEMPT, 'doors'))
  need(same(rows, source.working) && same(doorRows, source.doors), 'Ancestor current 75/30 bytes changed')
  const placements = read(source.stageNodes.find(node => node.phase === 'repair-door-placements').observation.path)
  need(same(doorRows, placements.afterDoorInventory) && same(doorRows, interfaceBefore.doorInventory), 'Doors changed after accepted placements or during interfaces')
  const allowedInterfaceNames = ['箱体右侧板.sldprt', '箱体右侧板焊接.SLDASM', ROOT_NAME].map(name => name.toLowerCase())
  unchanged(interfaceBefore.inventory.filter(row => !allowedInterfaceNames.includes(basename(row.path).toLowerCase())))
  const envelope = read(source.stageNodes.find(node => node.phase === 'envelope').observation.path)
  const pendingRight = envelope.observation.pendingRight
  need(same(pendingRight, source.inheritedPending) && source.sourceRightAfterEnvelopeSha256 === source.sourceRightAfterInterfacesSha256 && source.sourceLeftAfterEnvelopeSha256 === source.sourceLeftAfterInterfacesSha256 && pendingRight.rightAfterEnvelopeSha256 === source.sourceRightAfterInterfacesSha256 && pendingRight.envelopeLeftSha256 === source.sourceLeftAfterInterfacesSha256, 'Ancestor envelope/interface left/right provenance differs')
  const sourceManifest = read(source.sourceManifest.path), sourceRows = walk(resolve(SOURCE_ATTEMPT, 'door_sources'))
  checkSourceManifest(sourceManifest, resolve(SOURCE_ATTEMPT, 'door_sources'), sourceRows)
  const { verify16029ParametricBuild } = await import(pathToFileURL(resolve(ROOT, 'tools/lib/locker_16029_parametric_build.mjs')).href)
  const build = verify16029ParametricBuild(PROFILE, { root: ROOT, repairProfile: PROFILE })
  need(build.manifest.executableSha256 === EXPECTED_EXE && regular(build.executable).sha256 === EXPECTED_EXE && !build.manifest.productionProfile, 'Frozen research build differs')
  const protectedRows = [...historicalProtection.rows, ...walk(SOURCE_ATTEMPT), ...source.protectedV2Files, ...walk(PROBE, true), ...walk(HERE), ...walk(build.folder), ...walk(build.repairSource.sourceDirectory)]
  const protectionMap = new Map()
  for (const row of protectedRows) { const old = protectionMap.get(row.path.toLowerCase()); need(!old || old.sha256 === row.sha256, 'Conflicting protection snapshots: ' + row.path); protectionMap.set(row.path.toLowerCase(), row) }
  const deduplicated = [...protectionMap.values()]; unchanged(deduplicated)
  return { source, prior, rows, doorRows, build, sourceManifest, sourceRows, pendingRight, historicalProtectionCount: 19052, protectedRows: deduplicated, process: processSnapshot(), axis: { rawEqual: false, cylinderAxisRepresentationEquivalent: true, finiteBoundariesFullyCompared: false, negativeControlsPassed: 15 } }
}
// Ancestor-only evidence gate; the v3 native path exclusively calls checkContinuationClone.
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
// Ancestor-only evidence gate; v3 does not execute envelope again.
export function checkEnvelope(proof, contract, binding) {
  need(proof.phase === 'envelope' && proof.success === true && Array.isArray(proof.rows), 'Envelope evidence is not successful')
  const rows = proof.rows, cabinet = contract.geometry.cabinet
  const summaries = rows.filter(row => row.diagnosticOnly === true && Object.hasOwn(row, 'saved'))
  need(summaries.length === DERIVED_NAMES.length, 'Missing/extra derived summaries')
  need(rows.filter(row => row.inspection === 'envelope_target_boundary').length === 26, 'Envelope target boundary count differs')
  let pendingRight = null
  for (const name of DERIVED_NAMES) {
    const summary = unique(summaries, row => row.file === name, 'derived summary ' + name)
    need(summary.rebuilt === true && summary.healthy === true && summary.saved === true && Array.isArray(summary.issues) && summary.issues.length === 0 && Array.isArray(summary.links) && (name === RIGHT_NAME || summary.links.every(link => link.blockedByBrokenReference !== true)), 'Derived rebuild/health/save failed: ' + name)
    if (name === RIGHT_NAME) pendingRight = checkDeferredRight(summary, binding)
    if (name !== RIGHT_NAME) {
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
  return { pendingRight, derivedSummaryCount: summaries.length, targetBoundaryCount: DERIVED_NAMES.length - 1, masterHeightReadbackMm: master.readbackMm, targetDepthMm: terminal.targetDepthMm,
    combinedHeightDepthGateReachedInFrozenNativeBuild: true, explicitDepthReadbackInJson: false, masterAndSidesSavedReopenObserved: true, completeColdReopenVerified: false }
}
function phaseSources() { return STAGES.map(phase => ({ phase, nativeProfile: PROFILE, executableSha256: EXPECTED_EXE, sourceAttempt: SOURCE_ATTEMPT, newPlanAndLeaseAuthorizationRequired: true, ancestorDoorGenerationIsNotThisRun: true, input: phase === 'clone' ? 'fixed after-interface working75 and placement-after doors30; exact explicit source aliases; observed replacement count only, complete input dependency graph unavailable' : phase === 'repair-base-foot' ? 'new clone of ancestor current base stiffener and root' : phase === 'locks' ? 'ancestor envelope/interface L/R hashes then fresh post-clone L/R hashes; native local parameter source, patterns, MirrorPart2 and replacement' : 'same newly saved lock files; original native read-only geometry reuse', productionReceiptRequired: false, productionReceiptCreated: false })) }
function checkWorkingNames(working, originalRows, withLockSource) {
  const names = originalRows.map(row => basename(row.path))
  if (withLockSource) names.push(RIGHT_SOURCE_NAME, LEFT_NAME + '.before-parametric-lock.bak', RIGHT_NAME + '.before-parametric-lock.bak')
  need(same(readdirSync(working).sort(), names.sort()), 'Exact working files differ from phase contract')
}
function checkMutationScope(phase, before, after, doorBefore, doorAfter) {
  const allowed = phase === 'clone' || phase === 'envelope' ? before.map(row => basename(row.path)) : phase === 'repair-door-placements' ? [ROOT_NAME] : phase === 'repair-interfaces' ? ['箱体右侧板.sldprt', '箱体右侧板焊接.SLDASM', ROOT_NAME] : phase === 'repair-base-foot' ? ['底座加强筋.sldprt', '底座焊接.SLDASM', ROOT_NAME] : phase === 'locks' ? [LEFT_NAME, RIGHT_NAME] : []
  unchanged(before.filter(row => !allowed.some(name => name.toLowerCase() === basename(row.path).toLowerCase())))
  need(after.length === (phase === 'locks' || phase === 'locks-verify' ? 76 : 75), 'Phase CAD inventory count differs')
  if (phase.startsWith('door-')) {
    unchanged(doorBefore)
    need(doorAfter.length - doorBefore.length === 13, 'Door phase must add exactly thirteen CAD files')
  } else if (phase === 'repair-door-placements') {
    unchanged(doorBefore.filter(row => !/\.SLDASM$/i.test(row.path)))
    need(doorBefore.length === 26 && doorAfter.length === 30, 'Door placements require exactly four top-latch part additions')
    for (const module of DOOR_MODULES) for (const side of ['left', 'right']) unique(doorAfter, row => row.path.endsWith('H' + module.token + '\\parts\\top_latch_' + side + '_W387_H' + module.token + '.SLDPRT') && row.sha256 === SOURCE_SHA['top_latch_' + side], 'copied top latch')
  } else if (phase === 'clone') { need(doorBefore.length === 30 && doorAfter.length === 30, 'Clone must preserve 30 door CAD names') } else { unchanged(doorBefore); need(doorAfter.length === doorBefore.length, 'Unexpected door file addition/removal') }
}
async function dryRun() {
  const beforeAttempts = readdirSync(ATTEMPTS).sort(), beforePortal = portalFiles()
  const context = await preflight(), proposedId = 'PARAM-' + stamp() + '-' + randomBytes(3).toString('hex').toUpperCase()
  const proposedAttempt = resolve(ATTEMPTS, proposedId), workerId = 'research-through-locks-' + process.pid
  const report = { schema: 'obsolete-door-stage-continuation-dryrun-v3', generatedAtUtc: new Date().toISOString(), mode: 'dry-run', nativeCadExecuted: false, cadCopiesCreated: 0, taskCreated: false,
    stageOrder: STAGES, sourceManifest: { path: context.source.sourceManifest.path, sha256: hash(context.source.sourceManifest.path), roles: context.sourceManifest, inventory: context.sourceRows }, historicalProtectionCount: context.historicalProtectionCount, phaseSources: phaseSources(), runner: regular(fileURLToPath(import.meta.url)), pins: PINS.map(([path, sha256]) => ({ path, sha256 })), build: { tag: PROFILE, executable: context.build.executable, executableSha256: EXPECTED_EXE },
    sourceBinding: regular(SOURCE_BINDING), sourceAttempt: SOURCE_ATTEMPT, sourceAfterInterfacesRootSha256: hash(resolve(SOURCE_ATTEMPT, 'native_cad/working_pack', ROOT_NAME)), candidateCount: context.rows.length, doorCadCount: context.doorRows.length, candidateInventorySha256: sha(JSON.stringify(context.rows)), referenceAliases: context.source.referenceAliases, frozenCurrentDependencyGraphAvailable: false, allFrozenCurrentDependencyEdgesCovered: false, process: context.process, axis: context.axis,
    proposedAttemptOnlyNotCreated: proposedAttempt, proposedPlanOnlyNotWritten: makePlan(context.prior, proposedId, workerId, resolve(proposedAttempt, 'task_store')),
    proposedSourceManifestOnlyNotWritten: Object.fromEntries(Object.entries(context.sourceManifest).map(([role, spec]) => [role, { path: resolve(proposedAttempt, 'door_sources', sourceLeaf(role)), sha256: spec.sha256 }])),
    exactCopyMappingPreview: [...context.rows.map(row => ({ source: row.path, destination: resolve(proposedAttempt, 'native_cad/working_pack', row.relativePath), kind: 'working', sha256: row.sha256, size: row.size })), ...context.doorRows.map(row => ({ source: row.path, destination: resolve(proposedAttempt, 'doors', row.relativePath), kind: 'door', sha256: row.sha256, size: row.size }))],
    protectedRows: context.protectedRows, protectedBeforeAfterUnchanged: true, warning32Unresolved: true, finiteBoundariesFullyCompared: false, strictColdAcceptancePassed: false, modelAccepted: false, releaseEligible: false, downloadProduced: false,
    portalFileHashesCapturedThisV3DryRunOnly: beforePortal, historicalV1PortalWholeFileHashesCaptured: false,
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
  const workerId = 'research-through-locks-' + process.pid + '-' + randomBytes(3).toString('hex')
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
    for (const folder of ['native_cad/working_pack', 'evidence', 'authorizations', 'research', 'tools', 'door_sources', 'doors']) mkdirSync(resolve(attempt, folder), { recursive: true })
    const working = resolve(attempt, 'native_cad/working_pack'), doors = resolve(attempt, 'doors'), sourceDirectory = resolve(attempt, 'door_sources'), storeDir = resolve(attempt, 'task_store')
    const mapping = [...context.rows.map(row => ({ source: row.path, destination: resolve(working, row.relativePath), kind: 'working', sha256: row.sha256, size: row.size })), ...context.doorRows.map(row => ({ source: row.path, destination: resolve(doors, row.relativePath), kind: 'door', sha256: row.sha256, size: row.size }))]
    put(resolve(attempt, 'research/source-and-copy-mapping.json'), { schema: 'obsolete-door-stage-continuation-mapping-v3', taskId: id, sourceIsResearchCandidate: true, productionResumeClaimed: false, pins: PINS.map(([path, sha256]) => ({ path, sha256 })), files: mapping, sourceBinding: regular(SOURCE_BINDING), referenceAliases: context.source.referenceAliases, completeDependencyGraphAvailable: false })
    put(resolve(attempt, 'research/protected-before.json'), { schema: 'obsolete-door-stage-continuation-protection-v3', rows: context.protectedRows, process: context.process })
    put(resolve(attempt, 'research/portal-before.json'), { schema: 'obsolete-door-stage-continuation-portal-observation-v3', records: portalSnapshot.map(row => ({ id: row.id, state: row.nativeBuild.state, lease: row.nativeBuild.lease, updatedAt: row.updatedAt, blocker: row.nativeBuild.blocker || null })), existingBlockedTasksUnresolved: true, existingTasksModified: false })
    for (const row of mapping) { mkdirSync(dirname(row.destination), { recursive: true }); copyFileSync(row.source, row.destination, constants.COPYFILE_EXCL); need(hash(row.destination) === row.sha256, 'Copy hash mismatch: ' + row.destination) }
    const newSourceManifest = {}
    for (const [role, spec] of Object.entries(context.sourceManifest)) {
      const destination = resolve(sourceDirectory, sourceLeaf(role))
      copyFileSync(spec.path, destination, constants.COPYFILE_EXCL)
      need(hash(destination) === spec.sha256, 'Door source copy mismatch: ' + role)
      newSourceManifest[role] = { path: destination, sha256: spec.sha256 }
    }
    checkSourceManifest(newSourceManifest, sourceDirectory, walk(sourceDirectory))
    const newSourceManifestPath = resolve(attempt, 'door_sources.json')
    put(newSourceManifestPath, newSourceManifest)
    const copiedSourceRows = [regular(newSourceManifestPath), ...walk(sourceDirectory)]
    put(resolve(attempt, 'research/source-preparation.json'), { schema: 'obsolete-door-source-preparation-v3', researchOnly: true, originalManifest: regular(context.source.sourceManifest.path), originalRows: context.sourceRows, copiedManifest: regular(newSourceManifestPath), copiedRows: copiedSourceRows, exactRolePathShaBinding: true, phases: phaseSources(), standaloneSourceRelinkPerformed: false, ancestorNativeGenerationOnly: true, nativeGenerateExecutedThisRun: false, ancestorSourceBinding: regular(SOURCE_BINDING) })
    store = createNativeTaskStore({ dataDir: storeDir })
    const cabinet = context.prior.contract.geometry.cabinet
    await store.createOrImport({ id, username: 'authorized-isolated-through-locks-research', createdAt: new Date().toISOString(), status: 'native_task_created', taskType: 'native_solidworks_build_task',
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
    let previousResearchSha256 = '', cloneObservation = null, pendingRight = structuredClone(context.pendingRight), locksObservation = null
    const stageObservations = []
    for (phase of STAGES) {
      await pulse(); if (heartbeatError) throw heartbeatError
      need(!controller.signal.aborted, 'Research cancelled before phase')
      need((await inspect16029CadProcesses()).verified === true, 'CAD_OR_MONITOR_NOT_EMPTY_BEFORE_' + phase)
      unchanged(context.protectedRows)
      const before = inventory(working), doorBefore = existsSync(doors) ? inventory(doors) : []
      checkWorkingNames(working, context.rows, phase === 'locks-verify')
      unchanged(copiedSourceRows)
      checkPrecision(plan)
      if (phase === 'repair-base-foot') need(hash(resolve(working, RIGHT_NAME)) === pendingRight.postCloneRightSha256 && hash(resolve(working, LEFT_NAME)) === pendingRight.postCloneLeftSha256, 'Post-clone left/right changed before locks')
      const authorizationPath = resolve(attempt, 'authorizations', phase + '.json')
      const issuedAtUtc = new Date().toISOString()
      const authorization = { issuedAtUtc, schema: 'obsolete-door-stage-continuation-authorization-v3', researchOnly: true, phase, planSha256: hash(planPath), executableSha256: hash(executable), taskId: id, workerId, leaseId: task.nativeBuild.lease.id,
        rightPartitionSha256: hash(resolve(working, '箱体竖隔板R.SLDPRT')), expiresAt: new Date(Date.now() + 22 * 60 * 1000).toISOString(), preInventory: before, preDoorInventory: doorBefore, predecessor: '', previousResearchSha256 }
      put(authorizationPath, authorization)
      checkAuthorization(read(authorizationPath), { ...authorization, attempt, authorizationPath, taskPath: plan.taskPath, state: task.nativeBuild.state, leaseExpiresAt: task.nativeBuild.lease.expiresAt })
      put(resolve(attempt, 'research', phase + '-before.json'), { schema: 'obsolete-door-stage-continuation-stage-before-v3', phase, inventory: before, doorInventory: doorBefore, exactSourceDestinationMapping: mapping, sourceManifest: regular(newSourceManifestPath), previousResearchSha256 })
      nativeStarted = true
      await run16029ParametricPhaseProcess(executable, [planPath, phase], { phase, attempt, cwd: working, timeoutMs: 20 * 60 * 1000, preserveCadOnFailure: true, signal: controller.signal,
        env: { ...process.env, WINNSEN_PARAMETRIC_PLAN_SHA256: hash(planPath), WINNSEN_PARAMETRIC_AUTH_PATH: authorizationPath, WINNSEN_PARAMETRIC_AUTH_SHA256: hash(authorizationPath) },
        isCancelled: async () => { if (heartbeatError) throw heartbeatError; return (await store.read(id)).nativeBuild.state === 'cancel_requested' },
        onProgress: message => console.log(message) })
      await chain; if (heartbeatError) throw heartbeatError
      unchanged(context.protectedRows)
      const evidencePath = resolve(attempt, 'evidence', phase + '.json'), proof = read(evidencePath)
      const processEvidencePath = resolve(attempt, 'evidence', phase + '.process.json'), processProof = read(processEvidencePath)
      need(processProof.code === 0 && !processProof.stopReason && equalPath(processProof.executable, executable) && processProof.preserveCadOnFailure === true && processProof.cleanup?.verified === true, 'Process execution binding/cleanup evidence failed')
      need(processProof.startedAtMs >= Date.parse(issuedAtUtc) && processProof.finishedAtMs >= processProof.startedAtMs, 'Native process evidence is older than the fresh authorization')
      const after = inventory(working), doorAfter = existsSync(doors) ? inventory(doors) : []
      checkWorkingNames(working, context.rows, ['locks', 'locks-verify'].includes(phase))
      unchanged(copiedSourceRows)
      checkMutationScope(phase, before, after, doorBefore, doorAfter)
      let observation
      if (phase === 'clone') {
        observation = checkContinuationClone(proof, mapping, context.source.referenceAliases); cloneObservation = observation
        put(resolve(attempt, 'research/clone-observed-replacements.json'), { schema: 'obsolete-door-v3-observed-clone-replacements-v1', researchOnly: true, sourceBinding: regular(SOURCE_BINDING), rows: proof.rows, replacementCount: observation.replacementCount, replacements: observation.replacements, frozenCurrentDependencyGraphAvailable: false, allFrozenCurrentDependencyEdgesCovered: false, modelAccepted: false })
        pendingRight = { ...context.pendingRight, ancestorEnvelopeObservation: context.source.stageNodes.find(node => node.phase === 'envelope').observation,
          sourceRightAfterEnvelopeSha256: context.source.sourceRightAfterEnvelopeSha256, sourceRightAfterInterfacesSha256: context.source.sourceRightAfterInterfacesSha256,
          sourceLeftAfterEnvelopeSha256: context.source.sourceLeftAfterEnvelopeSha256, sourceLeftAfterInterfacesSha256: context.source.sourceLeftAfterInterfacesSha256,
          postCloneRightSha256: hash(resolve(working, RIGHT_NAME)), postCloneLeftSha256: hash(resolve(working, LEFT_NAME)),
          currentHashBindingStage: 'v3-native-clone', ancestorEnvelopeGeometryNotReexecuted: true, completeDependencyGraphAvailable: false }
        observation.inheritedRightProvenance = pendingRight
      } else if (['locks', 'locks-verify'].includes(phase)) {
        observation = checkLocks(proof, phase, plan.contract, { working, before, after, pending: pendingRight, locksObservation, processProof,
          rightBackupSha256: hash(resolve(working, RIGHT_NAME + '.before-parametric-lock.bak')),
          leftBackupSha256: hash(resolve(working, LEFT_NAME + '.before-parametric-lock.bak')), leftSeedSha256: hash(resolve(attempt, 'lock_seed_left.SLDPRT')) })
        if (phase === 'locks') locksObservation = observation
      } else observation = checkRepair(proof, phase, working, doors, DOOR_MODULES)
      need(!existsSync(resolve(attempt, 'receipts')), 'Production or stage receipts are forbidden')
      const researchPath = resolve(attempt, 'research', phase + '-observation.json')
      put(researchPath, { schema: 'obsolete-door-stage-continuation-stage-observation-v3', phase, taskId: id, researchOnly: true, planSha256: hash(planPath), authorizationSha256: hash(authorizationPath), originalNativeEvidence: { path: evidencePath, sha256: hash(evidencePath) }, processEvidence: { path: processEvidencePath, sha256: hash(processEvidencePath) }, sourceManifest: regular(newSourceManifestPath), phaseSource: phaseSources().find(row => row.phase === phase),
        beforeInventory: before, afterInventory: after, beforeDoorInventory: doorBefore, afterDoorInventory: doorAfter, observation, previousResearchSha256, protectedSourcesUnchanged: true, modelAccepted: false, productionReceiptCreated: false })
      previousResearchSha256 = hash(researchPath); stageObservations.push({ phase, path: researchPath, sha256: previousResearchSha256 })
    }
    clearInterval(timer); timer = null; await chain
    await move('validating', { progress: { stage: 'research_observation_only', percent: 90 } }, 'validating')
    await move('blocked', { blocker: { code: 'RESEARCH_CANDIDATE_REQUIRES_VALIDATION', message: 'Continuation clone/base-foot/locks/locks-verify were observed; door/envelope/placements/interfaces are research ancestors only; root/assembly integration, warning32, full cold reopen and finite boundaries remain unverified.' },
      execution: { started: true, cadStarted: true, modelReady: false, modelAccepted: false }, validation: { researchOnly: true, modelAccepted: false, warning32Unresolved: true, finiteBoundariesFullyCompared: false } }, 'blocked-research-candidate')
    unchanged(context.protectedRows)
    put(resolve(attempt, 'research/protected-after.json'), { schema: 'obsolete-door-stage-continuation-protection-v3', rows: context.protectedRows.map(row => regular(row.path)), unchanged: true, process: processSnapshot() })
    const resultPath = resolve(attempt, 'research/result.json')
    put(resultPath, { schema: 'obsolete-door-stage-continuation-result-v3', state: 'candidate_requires_validation', taskState: task.nativeBuild.state, attempt, taskId: id, stages: stageObservations, stoppedAfter: 'locks-verify', deferredRightResearchState: 'discharged_by_native_locks_and_saved_verify', rootPhaseExecuted: false, sourceBinding: regular(SOURCE_BINDING), ancestorDoorGenerationOnly: true, completeDependencyGraphAvailable: false, sourceManifest: regular(newSourceManifestPath),
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
      put(resolve(attempt, 'research/protected-after-failure.json'), { schema: 'obsolete-door-stage-continuation-protection-v3', rows: context.protectedRows.map(row => { try { return { path: row.path, beforeSha256: row.sha256, afterSha256: regular(row.path).sha256 } } catch (failure) { return { path: row.path, beforeSha256: row.sha256, error: failure.message } } }), unchanged: !protectionError })
      put(resolve(attempt, 'research/failure.json'), { schema: 'obsolete-door-stage-continuation-failure-v3', phase, error: error.message, protectionError, modelAccepted: false, preservedFailedCandidateArtifacts: true, rollbackPerformed: false, cleanupPerformed: false })
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
