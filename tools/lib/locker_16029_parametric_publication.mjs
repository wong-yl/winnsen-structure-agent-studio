import { createHash } from 'node:crypto'
import { readFileSync, readdirSync, lstatSync, existsSync, openSync, closeSync, fstatSync } from 'node:fs'
import { resolve, dirname, relative, isAbsolute, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from './locker_16029_parametric_contract.mjs'
import { compare16029PhysicalAudit, inspect16029LegacyPhysicalHealth, PHYSICAL_QUALITY_SCHEMA } from '../verify_16029_parametric_physical_quality.mjs'
import { production16029Profile, read16029ParametricResumeSnapshot, verify16029ProductionEvidence } from './locker_16029_production_evidence.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const BASE = resolve(ROOT, 'data/parametric_attempts')
const V35_AUDIT = resolve(BASE, 'PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex').toUpperCase()
const read = path => JSON.parse(readFileSync(path, 'utf8'))
const cad = path => !basename(path).startsWith('~$') && /\.SLD(PRT|ASM)$/i.test(path)
// Only these already delivered archives retain the historical download boundary.
const LEGACY_PUBLISHED_ARCHIVES = new Map([
  ['NATIVE-20260927-850-12-REPAIR', ['2026-09-27T14:21:19.749Z', 'PARAM-20260927135715156-85BF93', '1DD4EBCB69C5326C6CE3E880D1E21F85922C4773CDCD6F5CD2F6B22C4E490922']],
  ['NATIVE-20261001T070110-5BB416', ['2026-10-01T07:29:34.885Z', 'PARAM-20261001070112525-21A287', '577FB639CD47C6D289058CCC7AEBD78A5DB5C6978974156B73B708614994F459']],
  ['NATIVE-20261003T090446-8F7DE2', ['2026-10-03T09:37:05.261Z', 'PARAM-20261003090451084-BB5B30', 'D6961B92B9BFD4DD14F4B249C6F89DB2B6BE96625E8C4B12D643203621D7621E']],
  ['PARAMETRIC-AUTO-889-1967-575-20260915', ['2026-09-15T07:43:01.619Z', 'PARAM-20260915071831594-70961E', 'A5495F2E6F2BF98A46D499D8CDFA404F1183DC5501ADB3DCFF507D9492B55205']],
])

function scoped(path, root) {
  const full = resolve(path), rel = relative(root, full)
  requireValue(rel && !rel.startsWith('..') && !isAbsolute(rel), 'publication path outside its bound directory')
  let current = full
  while (current !== root) {
    requireValue(!lstatSync(current).isSymbolicLink(), 'publication refuses linked paths')
    current = dirname(current)
  }
  return full
}

function validatePublication(attemptPath, { expectedRequest, legacyPublishedArchiveSha256 = '' } = {}) {
  try {
    const attempt = scoped(attemptPath, BASE)
    requireValue(dirname(attempt) === BASE, 'publication requires a direct isolated attempt')
    const chain = [], seen = new Set()
    let current = attempt
    while (current) {
      requireValue(chain.length < 20 && !seen.has(current), 'invalid parametric resume chain')
      seen.add(current)
      const planPath = scoped(resolve(current, 'parametric_execution.json'), current), plan = read(planPath)
      requireValue(plan.schema === 'winnsen.locker16029.parametric_execution.v1', 'invalid execution plan schema')
      const contract = build16029ParametricContract(plan.contract.input)
      requireValue(!contract.input.envelopeProbe, 'diagnostic envelope probes are not deliverable')
      requireValue(JSON.stringify(contract.geometry) === JSON.stringify(plan.contract.geometry), 'stored geometry differs from parameter calculation')
      requireValue(JSON.stringify(contract.rowsByColumn) === JSON.stringify(plan.contract.rowsByColumn), 'stored door rows differ from parameter calculation')
      requireValue(plan.contractSha256 === undefined || plan.contractSha256 === digest(plan.contract), 'stored contract hash differs from execution plan')
      if (chain.length) requireValue(JSON.stringify(plan.contract.input) === JSON.stringify(chain[0].plan.contract.input), 'resume chain parameter mismatch')
      const receiptDir = resolve(current, 'receipts'), stages = new Map()
      for (const name of readdirSync(receiptDir).filter(name => name.endsWith('.json'))) {
        const receiptPath = scoped(resolve(receiptDir, name), current), receipt = read(receiptPath)
        requireValue(name === receipt.phase + '.json' && receipt.taskId === plan.taskId && receipt.planSha256 === hash(planPath), 'receipt plan binding failed')
        const evidencePath = scoped(resolve(current, 'evidence', receipt.phase + '.json'), current)
        const authorizationPath = scoped(resolve(current, 'authorizations', receipt.phase + '.json'), current)
        requireValue(hash(evidencePath) === receipt.evidenceSha256 && hash(authorizationPath) === receipt.authorizationSha256, 'receipt evidence or authorization hash mismatch')
        const evidence = read(evidencePath), authorization = read(authorizationPath)
        requireValue(evidence.success === true && receipt.protectedSourcesUnchanged === true, 'native stage did not pass')
        requireValue(authorization.planSha256 === receipt.planSha256 && authorization.taskId === plan.taskId, 'authorization identity mismatch')
        requireValue(hash(scoped(resolve(current, 'tools/NativeParametricAssembly.exe'), current)) === authorization.executableSha256, 'pinned native executable changed')
        stages.set(receipt.phase, { receipt, evidence, evidencePath, receiptPath })
      }
      const receiptHashes = new Set([...stages.values()].map(row => hash(row.receiptPath)))
      for (const row of stages.values()) requireValue(!row.receipt.predecessor || receiptHashes.has(row.receipt.predecessor), 'stage predecessor receipt missing')
      chain.push({ path: current, plan, stages })
      const resumed = plan.resumedEvidence
      if (!resumed) break
      current = scoped(resumed.sourceAttempt, BASE)
      requireValue(dirname(current) === BASE, 'resume ancestor outside attempt store')
      const receiptPath = scoped(resolve(current, 'receipts', resumed.completedPhase + '.json'), current)
      requireValue(hash(receiptPath) === resumed.receiptSha256, 'resume predecessor hash mismatch')
      requireValue(JSON.stringify(read(receiptPath).after) === JSON.stringify(resumed.inventory), 'resume inventory binding mismatch')
      if (resumed.snapshotSource) {
        const snapshot = read16029ParametricResumeSnapshot(current, resumed.backupPhase, read(resolve(current, 'parametric_execution.json')), read(receiptPath), receiptPath)
        requireValue(snapshot.snapshotSource === resumed.snapshotSource && snapshot.snapshotProof === resumed.snapshotProof && snapshot.snapshotAuthorizationPath === resumed.snapshotAuthorizationPath &&
          snapshot.snapshotAuthorizationSha256 === resumed.snapshotAuthorizationSha256 && snapshot.snapshotPriorReceiptSha256 === resumed.snapshotPriorReceiptSha256, 'resume authorized snapshot binding mismatch')
      }
    }
    const contract = chain[0].plan.contract
    const productionProfile = production16029Profile(contract.input)
    if (expectedRequest) requireValue(JSON.stringify(build16029ParametricContract(expectedRequest).geometry) === JSON.stringify(contract.geometry) && JSON.stringify(build16029ParametricContract(expectedRequest).rowsByColumn) === JSON.stringify(contract.rowsByColumn) && production16029Profile(expectedRequest) === productionProfile, 'requested parameters differ from candidate')
    const productionEvidence = productionProfile ? verify16029ProductionEvidence(attempt) : null
    const find = phase => chain.map(row => row.stages.get(phase)).find(Boolean)
    const heights = [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => Number(row.doorHeightMm.toFixed(6)).toString().replace('.', 'p')))]
    for (const phase of ['dimensions', 'derived', 'base-hole', 'assemblies', ...heights.map(h => 'door-' + h), 'locks-verify', 'root']) requireValue(find(phase), 'missing completed stage: ' + phase)
    if(contract.geometry.cabinet.heightMm!==1917||contract.geometry.cabinet.depthMm!==550)requireValue(find('envelope'),'missing completed envelope stage')
    const leaf = chain[0].stages
    const feedbackRequired = chain[0].plan.feedbackRepairRequired === true || leaf.has('repair-engineer-feedback')
    if (feedbackRequired) requireValue(leaf.has('repair-engineer-feedback'), 'missing engineer feedback repair stage')
    for (const phase of ['package', 'verify-package', 'verify-relocated', 'physical-audit']) requireValue(leaf.has(phase), 'missing final acceptance stage: ' + phase)
    const packagePath = resolve(attempt, 'package'), extractedPath = resolve(attempt, 'unzipped_native_model')
    const inventory = leaf.get('package').evidence.rows
    requireValue(inventory.length > 0 && inventory.every(row => cad(row.path)), 'invalid package inventory')
    requireValue(readdirSync(packagePath).filter(cad).length === inventory.length && readdirSync(extractedPath).filter(cad).length === inventory.length, 'package inventory count changed')
    for (const row of inventory) {
      const path = scoped(row.path, packagePath), extracted = scoped(resolve(extractedPath, basename(path)), extractedPath)
      requireValue(hash(path) === row.sha256 && hash(extracted) === row.sha256, 'package or extracted CAD hash mismatch')
    }
    for (const phase of ['verify-package', 'verify-relocated']) {
      if (feedbackRequired) {
        const rows = leaf.get(phase).evidence.rows
        const one = inspection => { const matches = rows.filter(row => row.inspection === inspection); requireValue(matches.length === 1 && matches[0].pass === true, 'missing or failed feedback measurement: ' + inspection); return matches[0] }
        const slots = one('feedback_base_slots'), rear = one('feedback_rear_holes'), maintenance = one('feedback_maintenance_weldment')
        requireValue(Number.isFinite(slots.straightLengthMm) && slots.straightLengthMm >= 3 && slots.straightLengthMm <= 30.001 &&
          Math.abs(slots.totalSlotLengthMm - slots.straightLengthMm - 3) < .001 && Number.isFinite(slots.minimumWebMm) && slots.minimumWebMm >= 2.999 && slots.slotCount === 15, 'feedback slots intersect knockout or changed count')
        requireValue(rear.part === '上盖壳体后侧板.sldprt' && rear.remainingTargetHoleCount === 0, 'feedback rear holes remain')
        const t = maintenance.bracketTransform, panel = maintenance.panelTransform
        const identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
        requireValue(maintenance.componentCount === 2 && maintenance.bracketFile === '维护门锁支架_原生.SLDPRT' &&
          Array.isArray(t) && t.length === 16 && t.every(Number.isFinite) && Array.isArray(panel) && panel.length === 16 && panel.every((v, i) => Number.isFinite(v) && Math.abs(v - identity[i]) < .000001) &&
          t.slice(0, 9).every((v, i) => Math.abs(v - identity[i]) < .000001) && Math.abs(t[9]) < .000001 &&
          Math.abs(t[10] * 1000 - ((contract.geometry.cabinet.heightMm - 92) / 2 - 104)) < .001 && Math.abs(t[11] * 1000 - 15.2) < .001, 'feedback maintenance bracket missing or misaligned')
      }
      const result = leaf.get(phase).evidence.rows.find(row => row.actualDoors !== undefined)
      requireValue(result && result.actualDoors === contract.geometry.doorCount && result.actualTongues === contract.geometry.doorCount && result.featureIssueCount === 0 && Math.abs(result.measuredWidthMm - contract.geometry.cabinet.widthMm) < .2, 'package measured geometry failed')
      if(contract.geometry.cabinet.heightMm!==1917||contract.geometry.cabinet.depthMm!==550)requireValue(Math.abs(result.measuredTopHeightMm-contract.geometry.cabinet.heightMm)<.2&&Math.abs(result.measuredDepthMm-contract.geometry.cabinet.depthMm)<.2,'package height or depth measurement failed')
      for (const side of ['L', 'R']) for (const row of contract.rowsByColumn[side]) requireValue(result.tonguePositionsMm?.filter(position => Math.abs(position[0] - (side === 'L' ? -55 : 55)) < .01 && Math.abs(position[1] - row.centerYmm) < .01 && Math.abs(position[2] + 11.3) < .01).length === 1, 'package tongue alignment failed')
    }
    const relocation = leaf.get('verify-relocated').evidence.rows.find(row => row.archive)
    requireValue(relocation?.extractedFilesByteIdentical === true && relocation.cadFileCount === inventory.length, 'relocation inventory failed')
    const archivePath = scoped(relocation.archive, attempt)
    requireValue(hash(archivePath) === relocation.sha256, 'archive hash mismatch')
    const quality = read(scoped(resolve(attempt, 'evidence/physical-quality.json'), attempt))
    const baselinePath = scoped(quality.baselinePath, BASE), physical = leaf.get('physical-audit')
    requireValue(baselinePath === V35_AUDIT && quality.baselineSha256 === '0C94B7F93014C2FAEC11151A63D2331D49B1983EB3E6C7A86281B56B2842B89F', 'unrecognized V35 physical baseline')
    requireValue(hash(baselinePath) === quality.baselineSha256 && hash(physical.evidencePath) === quality.candidateEvidenceSha256, 'physical comparison evidence changed')
    const measured = compare16029PhysicalAudit(read(baselinePath), physical.evidence, { doorCount: contract.geometry.doorCount, packageInventory: inventory, packageVerification: leaf.get('verify-package').evidence, ...(productionEvidence ? { productionEvidence } : {}) })
    let acceptance = measured
    if (legacyPublishedArchiveSha256 && !physical.evidence.rows.some(row => row.inspection === 'native_physical_coverage')) {
      requireValue(legacyPublishedArchiveSha256 === relocation.sha256, 'historical published archive binding mismatch')
      acceptance = inspect16029LegacyPhysicalHealth(read(baselinePath), physical.evidence, { doorCount: contract.geometry.doorCount })
      requireValue(acceptance.legacyHealthPass && quality.pass === true, 'historical physical health failed')
    } else {
      requireValue(quality.schema === PHYSICAL_QUALITY_SCHEMA && quality.coverageVerified === true && quality.packageEvidenceSha256 === hash(leaf.get('package').evidencePath) && quality.packageVerificationEvidenceSha256 === hash(leaf.get('verify-package').evidencePath), 'physical quality schema or package coverage binding missing')
      requireValue(measured.pass && quality.pass === true, 'physical coverage or geometry failed: ' + measured.coverageErrors.map(row => row.reason).join('; '))
      const relocated = compare16029PhysicalAudit(read(baselinePath), physical.evidence, { doorCount: contract.geometry.doorCount, packageInventory: inventory, packageVerification: leaf.get('verify-relocated').evidence, ...(productionEvidence ? { productionEvidence } : {}) })
      requireValue(relocated.pass, 'relocated physical coverage differs from package')
    }
    for (const row of read(resolve(attempt, 'protected_sources.before.json'))) requireValue(existsSync(row.path) && hash(row.path) === row.sha256, 'protected source changed')
    const root = inventory.filter(row => /^16029_.*\.SLDASM$/i.test(basename(row.path)))
    requireValue(root.length === 1, 'package root is not unique')
    const candidateSource = {
      taskId: chain[0].plan.taskId, planSha256: hash(resolve(attempt, 'parametric_execution.json')), contractSha256: digest(contract),
      acceptanceSha256: digest({ chain: chain.map(row => ({ planSha256: hash(resolve(row.path, 'parametric_execution.json')),
        receipts: [...row.stages.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([phase, stage]) => ({ phase, sha256: hash(stage.receiptPath) })) })),
      qualitySha256: hash(resolve(attempt, 'evidence/physical-quality.json')), protectedSourcesSha256: hash(resolve(attempt, 'protected_sources.before.json')) }),
      ...(productionEvidence ? { productionEvidence: Object.fromEntries(['executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256', 'packageEvidenceSha256'].map(field => [field, productionEvidence[field]])) } : {}),
    }
    return { ok: true, modelReady: true, attempt, request: contract.input, productionProfile, candidateSource, rootPath: root[0].path, archivePath, archiveSha256: relocation.sha256, cadFileCount: inventory.length, quality: acceptance, coverageVerified: acceptance.coverageVerified, ancestorCount: chain.length, boundary: acceptance.coverageVerified ? 'static engineering assistance validated against V35; production release and hinge sweep are not approved' : 'historically delivered engineering-assistance archive; physical coverage is unverified; production release and hinge sweep are not approved' }
  } catch (error) {
    return { ok: false, modelReady: false, reason: error.message }
  }
}

export function validate16029ParametricPublication(attemptPath, { expectedRequest } = {}) {
  return validatePublication(attemptPath, { expectedRequest })
}

function requireReuseRequest(request) {
  const keys = (value, allowed, label) => requireValue(value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value).every(key => allowed.includes(key)), 'candidate reuse does not cover ' + label)
  keys(request, ['cabinet', 'columns', 'productionProfile', 'orderReference'], 'custom request fields')
  if (Object.hasOwn(request, 'orderReference')) requireValue(typeof request.orderReference === 'string' && request.orderReference.length > 0 && request.orderReference.length <= 200 &&
    request.orderReference.trim() === request.orderReference && !/[\u0000-\u001F\u007F]/.test(request.orderReference), 'candidate reuse orderReference must be a trimmed string of 1-200 characters without control characters')
  keys(request.cabinet, ['widthMm', 'heightMm', 'depthMm'], 'custom cabinet fields')
  requireValue(Array.isArray(request.columns), 'candidate reuse requires ordered columns')
  for (const column of request.columns) {
    keys(column, ['side', 'doors'], 'custom column fields')
    requireValue(Array.isArray(column.doors), 'candidate reuse requires ordered doors')
    for (const door of column.doors) keys(door, ['heightUnits', 'heightMm'], 'custom door fields')
  }
}

export function validate16029ParametricCandidateReuse(attemptPath, { task, expectedArchiveSha256 } = {}) {
  try {
    requireValue(typeof expectedArchiveSha256 === 'string' && /^[A-F0-9]{64}$/i.test(expectedArchiveSha256), 'candidate reuse requires the accepted archive hash')
    requireValue(task?.parametricRequest && typeof task.id === 'string' && typeof task.requestFingerprint === 'string' && task.requestFingerprint.trim(), 'candidate reuse requires a bound portal task')
    requireReuseRequest(task.parametricRequest)
    const standard = {
      doorType: ['ordinary_door_panel'], lockType: ['机械锁舌；排除电控锁实体'], hingeType: ['暗铰链'], latchType: ['锁舌接口'],
      reinforcement: ['按模板规则生成'], openings: ['一门一锁孔；保留已验证定位基准'],
      material: ['待工程深化确认', '由结构工程师在项目深化中确定'], thickness: ['待工程深化确认', '由结构工程师在项目深化中确定'],
    }
    for (const [field, values] of Object.entries(standard)) {
      const value = String(task[field] ?? '').trim()
      requireValue(!value || values.includes(value), 'candidate reuse does not cover custom ' + field)
    }
    const checked = validatePublication(attemptPath, { expectedRequest: task.parametricRequest })
    requireValue(checked.ok && checked.coverageVerified === true, checked.reason || 'candidate physical coverage is incomplete')
    requireReuseRequest(checked.request)
    requireValue(!task.productionProfile || task.productionProfile === checked.productionProfile, 'portal production profile differs from candidate')
    requireValue(checked.archiveSha256 === expectedArchiveSha256.toUpperCase(), 'accepted candidate archive hash changed')
    requireValue(typeof checked.candidateSource.taskId === 'string' && checked.candidateSource.taskId, 'candidate source task identity missing')
    return { ...checked, executionMode: 'accepted_candidate_reuse', cadStarted: false, portalTaskId: task.id, requestFingerprint: task.requestFingerprint }
  } catch (error) { return { ok: false, modelReady: false, reason: error.message } }
}

export function inspect16029ParametricTaskPublication(task) {
  if (!task?.parametricRequest || task.nativeBuild?.state !== 'ready' || task.nativeBuild.modelReady !== true || task.status !== 'native_assistance_model_ready') return { ok: false, reason: 'parametric task is not ready' }
  const result = task.nativeBuild.result
  if (result?.schema !== 'winnsen.locker16029.parametric_publication.v1' || !result.attempt) return { ok: false, reason: 'parametric publication result missing' }
  const legacy = LEGACY_PUBLISHED_ARCHIVES.get(task.id)
  const legacyPublishedArchiveSha256 = legacy && task.updatedAt === legacy[0] && basename(result.attempt) === legacy[1] && result.archiveSha256 === legacy[2] && result.ok === true && result.modelReady === true ? legacy[2] : ''
  const lastReady = [...(task.nativeBuild.statusHistory || [])].reverse().find(row => row.to === 'ready')
  const reusing = result.executionMode === 'accepted_candidate_reuse' || lastReady?.operation === 'complete_parametric_candidate'
  const verified = reusing ? validate16029ParametricCandidateReuse(result.attempt, { task, expectedArchiveSha256: result.archiveSha256 }) :
    validatePublication(result.attempt, { expectedRequest: task.parametricRequest, legacyPublishedArchiveSha256 })
  if (!verified.ok || verified.archiveSha256 !== result.archiveSha256 || verified.rootPath !== result.rootPath || verified.archivePath !== result.archivePath) return { ok: false, reason: verified.reason || 'parametric result binding mismatch' }
  if (reusing && (result.executionMode !== verified.executionMode || result.cadStarted !== false || result.portalTaskId !== verified.portalTaskId ||
      result.requestFingerprint !== verified.requestFingerprint || result.productionProfile !== verified.productionProfile || JSON.stringify(result.candidateSource) !== JSON.stringify(verified.candidateSource))) {
    return { ok: false, reason: 'accepted candidate provenance binding mismatch' }
  }
  return verified
}

export function open16029ParametricTaskArchive(task) {
  const publication = inspect16029ParametricTaskPublication(task)
  requireValue(publication.ok, publication.reason)
  const fd = openSync(publication.archivePath, 'r')
  try {
    const stat = fstatSync(fd)
    requireValue(stat.isFile() && stat.nlink === 1 && stat.size > 0, 'invalid parametric archive file')
    requireValue(createHash('sha256').update(readFileSync(fd)).digest('hex').toUpperCase() === publication.archiveSha256, 'opened archive hash mismatch')
    return { fd, stat, archivePath: publication.archivePath, archiveFileName: basename(publication.rootPath, '.SLDASM') + '.zip' }
  } catch (error) { closeSync(fd); throw error }
}
