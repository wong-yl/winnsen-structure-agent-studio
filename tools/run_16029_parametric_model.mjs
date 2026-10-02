import { createHash, randomBytes } from 'node:crypto'
import { spawn } from 'node:child_process'
import { mkdirSync, readFileSync, writeFileSync, readdirSync, copyFileSync, existsSync, lstatSync } from 'node:fs'
import { resolve, dirname, relative, isAbsolute, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { acquireNativeWorkerInstanceLock } from './run_16029_native_worker.mjs'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
import { compare16029PhysicalAudit } from './verify_16029_parametric_physical_quality.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const V35 = resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/native_cad/candidate_native_hierarchical_pack_and_go')
const GOLD = resolve(ROOT, 'workers/analysis/desktop_reference/参数化模板素材_U盘原始_20260526/16029 寄存柜(标准组合式 1917×1000×550)/1.工程图')
const ORIGINAL_DOORS = resolve(ROOT, 'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图')
const sha = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const hash = (path) => sha(readFileSync(path))
const json = (path) => JSON.parse(readFileSync(path, 'utf8'))
const write = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n', { encoding: 'utf8', flag: 'wx' })
const cad = (name) => !name.startsWith('~$') && /\.SLD(PRT|ASM)$/i.test(name)

function inventory(folder) {
  const rows = []
  const visit = (directory) => {
    if (lstatSync(directory).isSymbolicLink()) throw new Error('CAD inventory refuses linked directory')
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = resolve(directory, entry.name)
      if (entry.isSymbolicLink()) throw new Error('CAD inventory refuses linked entry')
      if (entry.isDirectory()) visit(path)
      else if (cad(entry.name)) {
        if (lstatSync(path).nlink !== 1) throw new Error('CAD inventory refuses hardlink')
        rows.push({ path, relativePath: relative(folder, path), sha256: hash(path) })
      }
    }
  }
  visit(folder)
  return rows.sort((a, b) => a.relativePath.localeCompare(b.relativePath))
}

function assertInventory(rows) {
  const changed = rows.filter(row => !existsSync(row.path) || hash(row.path) !== row.sha256)
  if (changed.length) throw new Error(`PROTECTED_SOURCE_CHANGED: ${changed.map(row => row.path).join(', ')}`)
}

function scopedResumePath(path, root) {
  const full = resolve(path), rel = relative(root, full)
  if (!rel || rel.startsWith('..') || isAbsolute(rel)) throw new Error('resume snapshot path leaves its bound directory')
  let current = full
  while (true) {
    const stat = lstatSync(current)
    if (stat.isSymbolicLink() || (stat.isFile() && stat.nlink !== 1)) throw new Error('resume snapshot refuses linked paths')
    const parent = dirname(current)
    if (parent === current) break
    current = parent
  }
  return full
}

function verifyResumeCheckpoint(sourceAttempt, phase, plan, receipt) {
  const planPath = scopedResumePath(resolve(sourceAttempt, 'parametric_execution.json'), sourceAttempt)
  const evidencePath = scopedResumePath(resolve(sourceAttempt, 'evidence', phase + '.json'), sourceAttempt)
  const authPath = scopedResumePath(resolve(sourceAttempt, 'authorizations', phase + '.json'), sourceAttempt)
  const executable = scopedResumePath(resolve(sourceAttempt, 'tools/NativeParametricAssembly.exe'), sourceAttempt)
  const evidence = json(evidencePath), authorization = json(authPath)
  if (receipt.phase !== phase || receipt.taskId !== plan.taskId || receipt.planSha256 !== hash(planPath) ||
      receipt.authorizationSha256 !== hash(authPath) || receipt.evidenceSha256 !== hash(evidencePath) ||
      evidence.phase !== phase || evidence.success !== true || receipt.protectedSourcesUnchanged !== true ||
      authorization.phase !== phase || authorization.taskId !== plan.taskId || authorization.planSha256 !== receipt.planSha256 ||
      authorization.executableSha256 !== hash(executable)) throw new Error('resume checkpoint receipt binding mismatch')
}

function readResumeSnapshot(sourceAttempt, backupPhase, priorPlan, priorReceipt, priorReceiptPath) {
  const snapshotSource = scopedResumePath(resolve(sourceAttempt, 'backups', backupPhase), sourceAttempt)
  if (!lstatSync(snapshotSource).isDirectory()) throw new Error('resume snapshot is not a directory')
  const nextReceiptPath = scopedResumePath(resolve(sourceAttempt, 'receipts', backupPhase + '.json'), sourceAttempt)
  const nextReceipt = json(nextReceiptPath)
  verifyResumeCheckpoint(sourceAttempt, backupPhase, priorPlan, nextReceipt)
  const nextAuthorization = json(resolve(sourceAttempt, 'authorizations', backupPhase + '.json'))
  if (nextReceipt.predecessor !== hash(priorReceiptPath) || nextAuthorization.predecessor !== nextReceipt.predecessor ||
      JSON.stringify(nextReceipt.before) !== JSON.stringify(priorReceipt.after) ||
      JSON.stringify(nextAuthorization.preInventory) !== JSON.stringify(priorReceipt.after)) throw new Error('resume snapshot is not the checkpoint next-phase input')
  if (!Array.isArray(priorReceipt.after) || !priorReceipt.after.length || !Array.isArray(priorReceipt.doorInventory)) throw new Error('resume snapshot checkpoint inventories are missing')
  const expected = new Map()
  for (const [boundRoot, snapshotRoot, rows, flat] of [
    [resolve(sourceAttempt, 'native_cad/working_pack'), snapshotSource, priorReceipt.after, true],
    [resolve(sourceAttempt, 'doors'), resolve(snapshotSource, 'doors'), priorReceipt.doorInventory, false],
  ]) {
    for (const row of rows) {
      if (typeof row.path !== 'string' || !isAbsolute(row.path) || typeof row.relativePath !== 'string' ||
          !row.relativePath || isAbsolute(row.relativePath) || !/^[A-F0-9]{64}$/.test(row.sha256)) throw new Error('resume snapshot inventory row is invalid')
      const rel = relative(boundRoot, resolve(row.path))
      if (!rel || rel.startsWith('..') || isAbsolute(rel) || rel !== row.relativePath || !cad(row.path) ||
          (flat && dirname(resolve(row.path)) !== boundRoot)) throw new Error('resume snapshot inventory leaves its bound directory')
      const source = resolve(snapshotRoot, rel), key = relative(snapshotSource, source).toLowerCase()
      if (expected.has(key)) throw new Error('resume snapshot inventory contains duplicate paths')
      expected.set(key, row.sha256)
    }
  }
  const rows = inventory(snapshotSource)
  if (rows.length !== expected.size || rows.some(row => expected.get(row.relativePath.toLowerCase()) !== row.sha256)) throw new Error('resume snapshot inventory mismatch')
  return { snapshotSource, backupPhase, rows, cadRows: rows.filter(row => dirname(row.path) === snapshotSource) }
}

function sourceInputs(attempt, working) {
  const sourceFolder = resolve(attempt, 'door_sources')
  mkdirSync(sourceFolder)
  const nativeSources = {
    left_panel_seed: [ORIGINAL_DOORS, '储物柜门板1╱12.SLDPRT'],
    stiffener_seed: [ORIGINAL_DOORS, '柜门加强筋2╱12.SLDPRT'],
    latch_plate: [V35, '插销固定板.SLDPRT'], hook_pad: [V35, 'U型锁钩垫板.SLDPRT'],
    bushing: [V35, '塑料轴套(云绅模具).SLDPRT'], hinge_pin: [V35, '门轴销.SLDPRT'],
    circlip: [V35, '开口挡圈5.SLDPRT'], mechanical_lock_tongue: [V35, '锁舌.SLDPRT'],
    top_latch_left: [V35, '插销固定板2╱12_左.SLDPRT'], top_latch_right: [V35, '插销固定板2╱12_右.SLDPRT'],
    base_stiffener_source: [V35, '底座加强筋.sldprt'],
  }
  const result = {}
  for (const [role, [folder, name]] of Object.entries(nativeSources)) {
    const source = resolve(folder, name)
    const target = resolve(sourceFolder, role + '.SLDPRT')
    copyFileSync(source, target, 1)
    result[role] = { path: target, sha256: hash(source) }
    if (hash(target) !== result[role].sha256) throw new Error('source copy hash mismatch')
  }
  write(resolve(attempt, 'door_sources.json'), result)
}

export async function run16029ParametricModel(request, { phases = null, resumeSource = '', resumeStage = '', resumeBackupPhase = '', buildTag = 'unique-doors', onProgress = console.log } = {}) {
  const resumePhases = ['verify-package','package','verify','repair-frame-slots','root','repair-partition-mates','locks-verify','locks','repair-base-foot','repair-interfaces','repair-door-placements','envelope','assemblies','base-hole']
  if (typeof resumeStage !== 'string' || typeof resumeBackupPhase !== 'string' ||
      (resumeStage && (!resumeSource || !resumePhases.includes(resumeStage))) ||
      (resumeBackupPhase && (!resumeStage || !/^[a-zA-Z0-9_-]+$/.test(resumeBackupPhase)))) throw new Error('invalid explicit resume checkpoint options')
  if (request.envelopeProbe && (resumeSource || !phases || !phases.includes('probe-envelope') || phases.some(phase => !['clone', 'probe-envelope'].includes(phase)))) throw new Error('Envelope diagnostics must run in a fresh isolated probe attempt and cannot enter packaging or delivery stages.')
  const contract = build16029ParametricContract(request)
  const cabinet = contract.geometry.cabinet
  if (cabinet.heightMm < 1700 || cabinet.heightMm > 2200 || cabinet.depthMm < 250 || cabinet.depthMm > 650 || cabinet.widthMm < 700 || cabinet.widthMm > 1200) throw new Error('当前原生试验范围为700–1200W、1700–2200H、250–650D；每个结果须通过完整原生验收。')
  const id = `PARAM-${new Date().toISOString().replace(/[^0-9]/g, '')}-${randomBytes(3).toString('hex').toUpperCase()}`
  const workerId = `parametric-${process.pid}-${randomBytes(3).toString('hex')}`
  const attempt = resolve(ROOT, 'data/parametric_attempts', id)
  mkdirSync(attempt, { recursive: true })
  for (const name of ['evidence', 'authorizations', 'receipts', 'backups', 'native_cad/working_pack']) mkdirSync(resolve(attempt, name), { recursive: true })
  const working = resolve(attempt, 'native_cad/working_pack')
  const doors = resolve(attempt, 'doors')
  const protectedInventory = [...inventory(V35), ...inventory(GOLD), ...inventory(ORIGINAL_DOORS)]
  if (!resumeBackupPhase) write(resolve(attempt, 'protected_sources.before.json'), protectedInventory)
  let cloneSource = V35
  let cloneInventory = null
  let resumedEvidence = null
  let resumedPhase = ''
  let envelopeVerified = false
  const resumedDoorHeights = new Set()
  if (resumeSource) {
    const sourceAttempt = resolve(resumeSource)
    if (dirname(sourceAttempt) !== resolve(ROOT, 'data/parametric_attempts')) throw new Error('resume source is not an isolated parametric attempt')
    if (resumeStage) {
      scopedResumePath(sourceAttempt, resolve(ROOT, 'data/parametric_attempts'))
      scopedResumePath(resolve(sourceAttempt, 'parametric_execution.json'), sourceAttempt)
    }
    const priorPlan = json(resolve(sourceAttempt, 'parametric_execution.json'))
    if (JSON.stringify(priorPlan.contract.input) !== JSON.stringify(contract.input) || JSON.stringify(priorPlan.contract.geometry) !== JSON.stringify(contract.geometry)) throw new Error('resume parameters differ from prior measured dimensions')
    resumedPhase = resumeStage || resumePhases.find(phase => existsSync(resolve(sourceAttempt, 'receipts', phase + '.json')))
    if (!resumedPhase) throw new Error('no verified native stage exists to resume')
    const priorReceiptPath = resolve(sourceAttempt, 'receipts', resumedPhase + '.json')
    if (resumeStage) scopedResumePath(priorReceiptPath, sourceAttempt)
    const priorReceipt = json(priorReceiptPath)
    const priorEvidencePath = resolve(sourceAttempt, 'evidence', resumedPhase + '.json')
    if (priorReceipt.evidenceSha256 !== hash(priorEvidencePath) || json(priorEvidencePath).success !== true) throw new Error(resumedPhase + ' receipt no longer binds successful evidence')
    if (resumeStage) verifyResumeCheckpoint(sourceAttempt, resumedPhase, priorPlan, priorReceipt)
    let snapshot = null
    if (resumeBackupPhase) {
      snapshot = readResumeSnapshot(sourceAttempt, resumeBackupPhase, priorPlan, priorReceipt, priorReceiptPath)
      cloneSource = snapshot.snapshotSource
      cloneInventory = snapshot.cadRows
      protectedInventory.push(...snapshot.rows, ...inventory(resolve(sourceAttempt, 'native_cad/working_pack')),
        ...(existsSync(resolve(sourceAttempt, 'doors')) ? inventory(resolve(sourceAttempt, 'doors')) : []))
      write(resolve(attempt, 'protected_sources.before.json'), protectedInventory)
    } else {
      assertInventory(priorReceipt.after)
      cloneSource = resolve(sourceAttempt, 'native_cad/working_pack')
    }
    resumedEvidence = { sourceAttempt, completedPhase: resumedPhase, receiptSha256: hash(priorReceiptPath), inventory: priorReceipt.after }
    if (snapshot) Object.assign(resumedEvidence, { snapshotSource: snapshot.snapshotSource, backupPhase: snapshot.backupPhase })
    write(resolve(attempt, 'resumed_source.json'), resumedEvidence)
    if(cabinet.heightMm!==1917||cabinet.depthMm!==550){
      let ancestor=sourceAttempt
      for(let count=0;count<20;count++){
        const receiptPath=resolve(ancestor,'receipts/envelope.json')
        if(existsSync(receiptPath)){
          const receipt=json(receiptPath),evidencePath=resolve(ancestor,'evidence/envelope.json')
          if(receipt.evidenceSha256!==hash(evidencePath)||json(evidencePath).success!==true)throw new Error('resumed envelope evidence mismatch')
          envelopeVerified=true;break
        }
        const prior=json(resolve(ancestor,'parametric_execution.json')).resumedEvidence?.sourceAttempt
        if(!prior)break
        ancestor=resolve(prior)
        if(dirname(ancestor)!==resolve(ROOT,'data/parametric_attempts'))throw new Error('envelope proof leaves isolated attempts')
      }
    }
    const boundDoorRoot = resolve(sourceAttempt, 'doors')
    const sourceDoorRoot = snapshot ? resolve(snapshot.snapshotSource, 'doors') : boundDoorRoot
    if (existsSync(sourceDoorRoot)) {
      for (const doorFolder of readdirSync(sourceDoorRoot, { withFileTypes: true })) {
        if (!doorFolder.isDirectory() || !/^H\d+(?:p\d+)?$/.test(doorFolder.name)) throw new Error('invalid resumed door folder')
        const heightToken = doorFolder.name.slice(1)
        let proofAttempt = sourceAttempt
        for (let count = 0; !existsSync(resolve(proofAttempt, 'receipts', 'door-' + heightToken + '.json')) && count < 16; count++) {
          proofAttempt = resolve(json(resolve(proofAttempt, 'parametric_execution.json')).resumedEvidence?.sourceAttempt || '')
          if (dirname(proofAttempt) !== resolve(ROOT, 'data/parametric_attempts')) throw new Error('resumed door proof leaves isolated attempts')
        }
        const receiptPath = resolve(proofAttempt, 'receipts', 'door-' + heightToken + '.json')
        const resultPath = resolve(proofAttempt, 'evidence', 'door-' + heightToken + '.json')
        const receipt = json(receiptPath), result = json(resultPath)
        if (!result.success || receipt.evidenceSha256 !== hash(resultPath)) throw new Error('resumed door evidence mismatch')
        const sourceFolder = resolve(sourceDoorRoot, doorFolder.name)
        const boundFolder = resolve(boundDoorRoot, doorFolder.name)
        for (const file of result.result.output_files) {
          const source = resolve(sourceFolder, file.relative_path)
          const moduleRelative = relative(sourceFolder, source)
          if (snapshot && (!moduleRelative || isAbsolute(file.relative_path) || moduleRelative.startsWith('..') || isAbsolute(moduleRelative))) throw new Error('resumed door inventory mismatch')
          if (snapshot && !cad(basename(source))) continue
          const boundCurrent = priorReceipt.doorInventory?.find(row => row.path === resolve(boundFolder, file.relative_path))
          if (relative(sourceFolder, source).startsWith('..') || (snapshot && !boundCurrent) || hash(source) !== (boundCurrent?.sha256 || file.sha256)) throw new Error('resumed door inventory mismatch')
          if (snapshot) scopedResumePath(source, snapshot.snapshotSource)
          const destination = resolve(attempt, 'doors', doorFolder.name, file.relative_path)
          mkdirSync(dirname(destination), { recursive: true }); copyFileSync(source, destination, 1)
          if (snapshot && hash(destination) !== boundCurrent.sha256) throw new Error('resumed door snapshot copy hash mismatch')
        }
        for (const boundFile of priorReceipt.doorInventory || []) {
          const moduleRelative = relative(boundFolder, boundFile.path)
          if (moduleRelative.startsWith('..') || isAbsolute(moduleRelative)) continue
          const destination = resolve(attempt, 'doors', doorFolder.name, moduleRelative)
          if (existsSync(destination)) continue
          const source = resolve(sourceFolder, moduleRelative)
          if (snapshot) scopedResumePath(source, snapshot.snapshotSource)
          if (hash(source) !== boundFile.sha256) throw new Error('resumed auxiliary door part differs from its receipt')
          mkdirSync(dirname(destination), { recursive: true }); copyFileSync(source, destination, 1)
          if (snapshot && hash(destination) !== boundFile.sha256) throw new Error('resumed auxiliary door snapshot copy hash mismatch')
        }
        if (!snapshot) protectedInventory.push(...inventory(sourceFolder))
        resumedDoorHeights.add(heightToken)
      }
    }
  }
  for (const row of cloneInventory || inventory(cloneSource)) {
    if (dirname(row.path) !== cloneSource) throw new Error('native source must be flat')
    if (resumeBackupPhase) scopedResumePath(row.path, cloneSource)
    copyFileSync(row.path, resolve(working, basename(row.path)), 1)
    if (resumeBackupPhase && hash(resolve(working, basename(row.path))) !== row.sha256) throw new Error('native snapshot copy hash mismatch')
  }
  if (resumeBackupPhase) assertInventory(protectedInventory)
  sourceInputs(attempt, working)
  const storeDir = resolve(attempt, 'task_store')
  const store = createNativeTaskStore({ dataDir: storeDir })
  const now = new Date().toISOString()
  await store.createOrImport({ id, username: 'authorized-16029-parametric-development', createdAt: now, status: 'native_task_created', taskType: 'native_solidworks_build_task', requestFingerprint: sha(JSON.stringify(request)), cabinetWidth: cabinet.widthMm, cabinetHeight: cabinet.heightMm, cabinetDepth: cabinet.depthMm, columns: 2, doorCount: contract.geometry.doorCount, modelReady: false, engineeringAssistanceReady: false, nativeBuild: { state: 'created', attempt: 0, modelReady: false, executionStarted: false } })
  const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(ROOT, 'data'), workerId })
  let task = null
  let timer = null
  let heartbeatChain = Promise.resolve()
  let heartbeatError = null
  let activePhase = ''
  let activeBefore = []
  let activeBackup = ''
  let activeDoorBefore = []
  let activeDoorBackup = ''
  const leaseArgs = () => ({ taskId: task.id, workerId, leaseId: task.nativeBuild.lease.id, expectedRevision: task.revision })
  try {
    task = await store.claimNext({ workerId, leaseMs: 30 * 60 * 1000 })
    if (task.id !== id) throw new Error('unexpected parametric task claim')
    task = await store.transition({ ...leaseArgs(), transitionId: id + '-planning', toState: 'planning' })
    const plan = { schema: 'winnsen.locker16029.parametric_execution.v1', purpose: 'structure_engineering_assistance', taskId: id, taskPath: resolve(storeDir, id + '.json'), workerId, contract, resumedEvidence, sourceRightPartitionSha256: hash(resolve(working, '箱体竖隔板R.SLDPRT')) }
    const planPath = resolve(attempt, 'parametric_execution.json')
    write(planPath, plan)
    task = await store.transition({ ...leaseArgs(), transitionId: id + '-building', toState: 'building', patch: { execution: { started: true, cadStarted: false, modelReady: false }, progress: { stage: 'clone', percent: 0 } } })
    timer = setInterval(() => { heartbeatChain = heartbeatChain.then(async () => { if (!task.nativeBuild.lease) return; task = await store.heartbeat(leaseArgs()); lock.heartbeat() }).catch(error => { heartbeatError = error }) }, 20000)
    if (!/^[a-zA-Z0-9_-]+$/.test(buildTag)) throw new Error('invalid native build tag')
    const buildFolder = resolve(ROOT, 'workers/native_model_requests/parametric_v1', 'bin-' + buildTag)
    const buildManifest = json(resolve(buildFolder, 'build.json'))
    if (hash(buildManifest.executable) !== buildManifest.executableSha256) throw new Error('native executable no longer matches the built manifest')
    const pinnedTools = resolve(attempt, 'tools')
    mkdirSync(pinnedTools)
    for (const file of ['NativeParametricAssembly.exe', 'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']) copyFileSync(resolve(buildFolder, file), resolve(pinnedTools, file), 1)
    write(resolve(pinnedTools, 'build.json'), buildManifest)
    const executable = resolve(pinnedTools, 'NativeParametricAssembly.exe')
    const heights = [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => row.doorHeightMm))]
    const order = phases || ['clone', ...(resumeSource ? [] : ['probe', 'dimensions', 'derived', 'base-hole']), ...(resumedPhase && resumedPhase !== 'base-hole' ? [] : ['assemblies']), ...((cabinet.heightMm !== 1917 || cabinet.depthMm !== 550) && !envelopeVerified ? ['envelope'] : []), ...heights.map(height => Number(height.toFixed(6)).toString().replace('.', 'p')).filter(token => !resumedDoorHeights.has(token)).map(token => 'door-' + token), 'repair-door-placements', 'repair-interfaces', 'repair-base-foot', 'locks', 'locks-verify', 'repair-partition-mates', ...(['root','verify','package','verify-package'].includes(resumedPhase) ? [] : ['root']), 'repair-frame-slots', ...(cabinet.heightMm !== 1917 ? ['repair-height-braces'] : []), ...(cabinet.depthMm !== 550 ? ['repair-depth-placements'] : []), 'repair-base-mates', 'verify', 'package', 'verify-package', 'verify-relocated', 'physical-audit']
    let predecessor = ''
    for (const phase of order) {
      activePhase = phase
      await heartbeatChain
      if (heartbeatError) throw heartbeatError
      task = await store.read(id)
      if (task.nativeBuild.state === 'cancel_requested') {
        task = await store.transition({ ...leaseArgs(), transitionId: id + '-cancelled-between-phases', toState: 'cancelled' })
        return { attempt, taskId: id, state: 'cancelled_after_atomic_stage', modelReady: false }
      }
      task = await store.heartbeat(leaseArgs())
      const before = inventory(working)
      const doorBefore = existsSync(doors) ? inventory(doors) : []
      const backup = resolve(attempt, 'backups', phase)
      const doorBackup = resolve(backup, 'doors')
      mkdirSync(backup)
      for (const row of before) copyFileSync(row.path, resolve(backup, row.relativePath), 1)
      for (const row of doorBefore) {
        const destination = resolve(doorBackup, row.relativePath)
        mkdirSync(dirname(destination), { recursive: true })
        copyFileSync(row.path, destination, 1)
        if (hash(destination) !== row.sha256) throw new Error('isolated door backup hash mismatch')
      }
      activeBefore = before
      activeBackup = backup
      activeDoorBefore = doorBefore
      activeDoorBackup = doorBackup
      const authPath = resolve(attempt, 'authorizations', phase + '.json')
      write(authPath, { phase, planSha256: hash(planPath), executableSha256: hash(executable), taskId: id, workerId, leaseId: task.nativeBuild.lease.id, rightPartitionSha256: hash(resolve(working, '箱体竖隔板R.SLDPRT')), expiresAt: new Date(Date.now() + 29 * 60 * 1000).toISOString(), preInventory: before, predecessor })
      onProgress(JSON.stringify({ taskId: id, phase, state: 'started', attempt }))
      await new Promise((resolveRun, rejectRun) => {
        const child = spawn(executable, [planPath, phase], { shell: false, windowsHide: true, cwd: working, env: { ...process.env, WINNSEN_PARAMETRIC_PLAN_SHA256: hash(planPath), WINNSEN_PARAMETRIC_AUTH_PATH: authPath, WINNSEN_PARAMETRIC_AUTH_SHA256: hash(authPath) } })
        let stdout = '', stderr = ''
        child.stdout.on('data', data => { stdout += data.toString(); onProgress(data.toString().trim()) })
        child.stderr.on('data', data => { stderr += data.toString() })
        child.on('error', rejectRun)
        child.on('close', code => { write(resolve(attempt, 'evidence', phase + '.process.json'), { code, stdout, stderr }); if (code === 0) resolveRun(); else rejectRun(new Error(`PARAMETRIC_PHASE_FAILED ${phase}: ${stderr || stdout}`)) })
      })
      assertInventory(protectedInventory)
      const receiptPath = resolve(attempt, 'receipts', phase + '.json')
      const resultPath = resolve(attempt, 'evidence', phase + '.json')
      const result = json(resultPath)
      if (result.success !== true) throw new Error('stage evidence is not successful: ' + phase)
      if (phase === 'physical-audit') {
        const baselinePath = resolve(ROOT, 'data/parametric_attempts/PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
        const quality = compare16029PhysicalAudit(json(baselinePath), result, { doorCount: contract.geometry.doorCount })
        write(resolve(attempt, 'evidence/physical-quality.json'), { ...quality, baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(resultPath) })
        if (!quality.pass) throw new Error('generated physical geometry does not meet the measured V35 baseline; see physical-quality.json')
      }
      write(receiptPath, { phase, taskId: id, planSha256: hash(planPath), authorizationSha256: hash(authPath), evidenceSha256: hash(resultPath), before, after: inventory(working), doorInventory: existsSync(resolve(attempt, 'doors')) ? inventory(resolve(attempt, 'doors')) : [], protectedSourcesUnchanged: true, predecessor })
      predecessor = hash(receiptPath)
      activeBefore = []
      activeBackup = ''
      activeDoorBefore = []
      activeDoorBackup = ''
      onProgress(JSON.stringify({ taskId: id, phase, state: 'verified' }))
    }
    clearInterval(timer); timer = null
    await heartbeatChain
    task = await store.transition({ ...leaseArgs(), transitionId: id + '-validating', toState: 'validating', patch: { progress: { stage: 'independent_validation_pending', percent: 90 } } })
    task = await store.transition({ ...leaseArgs(), transitionId: id + '-candidate', toState: 'blocked', patch: { message: '原生阶段已执行；完整几何、干涉、展开与迁移验证尚待独立验收。', execution: { started: true, cadStarted: true, modelReady: false } } })
    return { attempt, taskId: id, stages: order, state: 'candidate_requires_validation', modelReady: false }
  } catch (error) {
    clearInterval(timer); timer = null
    await heartbeatChain
    assertInventory(protectedInventory)
    if (activeBackup && (activeBefore.length || activeDoorBefore.length)) {
      const rollback = []
      for (const row of activeBefore) {
        const source = resolve(activeBackup, row.relativePath)
        if (relative(working, row.path).startsWith('..') || relative(activeBackup, source).startsWith('..') || hash(source) !== row.sha256) throw new Error('isolated rollback source binding is invalid')
        if (!existsSync(row.path) || hash(row.path) !== row.sha256) {
          copyFileSync(source, row.path)
          if (hash(row.path) !== row.sha256) throw new Error('isolated rollback verification failed')
          rollback.push(row.relativePath)
        }
      }
      const restoredDoorFiles = []
      for (const row of activeDoorBefore) {
        const source = resolve(activeDoorBackup, row.relativePath)
        const targetRelative = relative(doors, row.path), sourceRelative = relative(activeDoorBackup, source)
        if (targetRelative.startsWith('..') || isAbsolute(targetRelative) || sourceRelative.startsWith('..') || isAbsolute(sourceRelative) || hash(source) !== row.sha256) throw new Error('isolated door rollback source binding is invalid')
        if (!existsSync(row.path) || hash(row.path) !== row.sha256) {
          mkdirSync(dirname(row.path), { recursive: true })
          copyFileSync(source, row.path)
          if (hash(row.path) !== row.sha256) throw new Error('isolated door rollback verification failed')
          restoredDoorFiles.push(row.relativePath)
        }
      }
      write(resolve(attempt, 'evidence', activePhase + '.rollback.json'), { success: true, restoredFiles: rollback, restoredDoorFiles, preservedFailedCandidateArtifacts: true })
    }
    if (task?.nativeBuild?.lease) {
      const latest = await store.read(id)
      if (latest.nativeBuild.lease?.id === task.nativeBuild.lease.id && latest.nativeBuild.lease?.workerId === workerId) {
        task = latest
        if (task.nativeBuild.state === 'cancel_requested') task = await store.transition({ ...leaseArgs(), transitionId: id + '-cancelled-on-error', toState: 'cancelled' })
        else task = await store.fail({ ...leaseArgs(), transitionId: id + '-failed-' + activePhase, code: 'parametric_native_execution_failed', message: error.message, retryable: false })
      }
    }
    throw Object.assign(error, { attempt })
  } finally {
    if (timer) clearInterval(timer)
    lock.release()
    assertInventory(protectedInventory)
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const requestPath = process.argv[2]
  if (!requestPath || !isAbsolute(resolve(requestPath))) throw new Error('Usage: node tools/run_16029_parametric_model.mjs <request.json> [phase,phase]')
  try { console.log(JSON.stringify(await run16029ParametricModel(json(resolve(requestPath)), { phases: process.argv[3] && process.argv[3] !== '--resume-source' ? process.argv[3].split(',') : null, resumeSource: process.argv[3] === '--resume-source' ? process.argv[4] : '' }), null, 2)) }
  catch (error) { console.error(JSON.stringify({ error: error.message, attempt: error.attempt })); process.exitCode = 1 }
}
