import { createHash } from 'node:crypto'
import { readFileSync, readdirSync, lstatSync, existsSync, openSync, closeSync, fstatSync } from 'node:fs'
import { resolve, dirname, relative, isAbsolute, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from './locker_16029_parametric_contract.mjs'
import { compare16029PhysicalAudit } from '../verify_16029_parametric_physical_quality.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const BASE = resolve(ROOT, 'data/parametric_attempts')
const V35_AUDIT = resolve(BASE, 'PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const read = path => JSON.parse(readFileSync(path, 'utf8'))
const cad = path => !basename(path).startsWith('~$') && /\.SLD(PRT|ASM)$/i.test(path)

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

export function validate16029ParametricPublication(attemptPath, { expectedRequest } = {}) {
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
    }
    const contract = chain[0].plan.contract
    if (expectedRequest) requireValue(JSON.stringify(build16029ParametricContract(expectedRequest).geometry) === JSON.stringify(contract.geometry) && JSON.stringify(build16029ParametricContract(expectedRequest).rowsByColumn) === JSON.stringify(contract.rowsByColumn), 'requested parameters differ from candidate')
    const find = phase => chain.map(row => row.stages.get(phase)).find(Boolean)
    const heights = [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => Number(row.doorHeightMm.toFixed(6)).toString().replace('.', 'p')))]
    for (const phase of ['dimensions', 'derived', 'base-hole', 'assemblies', ...heights.map(h => 'door-' + h), 'locks-verify', 'root']) requireValue(find(phase), 'missing completed stage: ' + phase)
    if(contract.geometry.cabinet.heightMm!==1917||contract.geometry.cabinet.depthMm!==550)requireValue(find('envelope'),'missing completed envelope stage')
    const leaf = chain[0].stages
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
    const measured = compare16029PhysicalAudit(read(baselinePath), physical.evidence, { doorCount: contract.geometry.doorCount })
    requireValue(measured.pass && quality.pass === true, 'physical geometry exceeds V35 baseline')
    for (const row of read(resolve(attempt, 'protected_sources.before.json'))) requireValue(existsSync(row.path) && hash(row.path) === row.sha256, 'protected source changed')
    const root = inventory.filter(row => /^16029_.*\.SLDASM$/i.test(basename(row.path)))
    requireValue(root.length === 1, 'package root is not unique')
    return { ok: true, modelReady: true, attempt, request: contract.input, rootPath: root[0].path, archivePath, archiveSha256: relocation.sha256, cadFileCount: inventory.length, quality: measured, ancestorCount: chain.length, boundary: 'static engineering assistance validated against V35; production release and hinge sweep are not approved' }
  } catch (error) {
    return { ok: false, modelReady: false, reason: error.message }
  }
}

export function inspect16029ParametricTaskPublication(task) {
  if (!task?.parametricRequest || task.nativeBuild?.state !== 'ready' || task.nativeBuild.modelReady !== true || task.status !== 'native_assistance_model_ready') return { ok: false, reason: 'parametric task is not ready' }
  const result = task.nativeBuild.result
  if (result?.schema !== 'winnsen.locker16029.parametric_publication.v1' || !result.attempt) return { ok: false, reason: 'parametric publication result missing' }
  const verified = validate16029ParametricPublication(result.attempt, { expectedRequest: task.parametricRequest })
  if (!verified.ok || verified.archiveSha256 !== result.archiveSha256 || verified.rootPath !== result.rootPath || verified.archivePath !== result.archivePath) return { ok: false, reason: verified.reason || 'parametric result binding mismatch' }
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
