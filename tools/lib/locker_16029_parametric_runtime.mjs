import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, readdirSync } from 'node:fs'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { run16029ParametricModel } from '../run_16029_parametric_model.mjs'
import { build16029ParametricContract } from './locker_16029_parametric_contract.mjs'
import { read16029ParametricRuntimeRepair, verify16029ParametricBuild } from './locker_16029_parametric_build.mjs'
import { inspect16029CadProcesses } from './locker_16029_parametric_process.mjs'
import { production16029Profile } from './locker_16029_production_evidence.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const BASELINE = 'engineer-feedback-20261008-r4'
const DOOR_PRECISION_REPAIRS = ['readonly-open-20261110-r4-doorprecision1', 'readonly-open-20261110-r4-doorprecision2', 'readonly-open-20261110-r4-doorprecision3']
const REPAIRS = ['readonly-open-20261105-r4', 'readonly-open-20261109-r4-rearwidth1', 'readonly-open-20261110-r4-reargeometry1', ...DOOR_PRECISION_REPAIRS]
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8'))
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)
const requireValue = (value, message) => { if (!value) throw new Error(message) }

function regular(path) {
  let current = resolve(path)
  const stat = lstatSync(current)
  requireValue(stat.isFile() && stat.nlink === 1, 'runtime bridge requires a regular file')
  while (true) {
    requireValue(!lstatSync(current).isSymbolicLink(), 'runtime bridge refuses linked paths')
    const parent = dirname(current)
    if (parent === current) break
    current = parent
  }
  return path
}

function verifySegment(result, phases, contract, build, attemptsRoot, existing, source = null, doorModules = null) {
  requireValue(result?.state === 'candidate_requires_validation' && same(result.stages, phases), 'runtime bridge stage sequence differs from the required segment')
  const attempt = resolve(result.attempt || '')
  requireValue(dirname(attempt) === attemptsRoot && basename(attempt) === result.taskId && !existing.has(basename(attempt)), 'runtime bridge requires a newly created isolated attempt')
  const planPath = regular(resolve(attempt, 'parametric_execution.json')), plan = read(planPath)
  requireValue(plan.taskId === result.taskId && same(plan.contract, contract), 'runtime bridge request or task identity changed')
  requireValue(!doorModules || same(plan.doorModules, doorModules), 'runtime bridge door module map differs from the request contract')
  if (source) {
    requireValue(plan.resumedEvidence?.sourceAttempt === source.attempt && plan.resumedEvidence.completedPhase === 'repair-frame-slots' &&
      plan.resumedEvidence.receiptSha256 === hash(regular(resolve(source.attempt, 'receipts/repair-frame-slots.json'))) &&
      !plan.resumedEvidence.backupPhase && !plan.resumedEvidence.snapshotSource, 'runtime bridge checkpoint differs from this request')
  } else requireValue(plan.resumedEvidence === null, 'runtime bridge first segment cannot have an ancestor')
  const executableSha256 = hash(regular(resolve(attempt, 'tools/NativeParametricAssembly.exe')))
  requireValue(executableSha256 === build.executableSha256 && hash(regular(resolve(attempt, 'tools/build.json'))) === build.manifestSha256, 'runtime bridge pinned build identity changed')
  const names = readdirSync(resolve(attempt, 'receipts')).sort()
  requireValue(same(names, phases.map(phase => phase + '.json').sort()), 'runtime bridge receipt coverage differs from the required segment')
  const planSha256 = hash(planPath)
  let predecessor = ''
  for (const phase of phases) {
    const receiptPath = regular(resolve(attempt, 'receipts', phase + '.json'))
    const evidencePath = regular(resolve(attempt, 'evidence', phase + '.json'))
    const authorizationPath = regular(resolve(attempt, 'authorizations', phase + '.json'))
    const receipt = read(receiptPath), evidence = read(evidencePath), authorization = read(authorizationPath)
    const evidenceMatchesPhase = phase.startsWith('door-')
      ? Number.isFinite(evidence.heightMm) && 'door-' + Number(evidence.heightMm.toFixed(6)).toString().replace('.', 'p') === phase &&
        (!doorModules || evidence.heightMm === doorModules.find(module => module.token === phase.slice(5))?.heightMm)
      : evidence.phase === phase
    requireValue(receipt.phase === phase && receipt.taskId === plan.taskId && receipt.planSha256 === planSha256 &&
      receipt.evidenceSha256 === hash(evidencePath) && receipt.authorizationSha256 === hash(authorizationPath) &&
      receipt.protectedSourcesUnchanged === true && receipt.predecessor === predecessor &&
      evidenceMatchesPhase && evidence.success === true && authorization.phase === phase &&
      authorization.taskId === plan.taskId && authorization.planSha256 === planSha256 &&
      authorization.executableSha256 === executableSha256, 'runtime bridge receipt binding failed: ' + phase)
    predecessor = hash(receiptPath)
  }
  return { attempt, taskId: plan.taskId }
}

export async function run16029ParametricRuntime(request, options = {}, {
  root = ROOT, runModel = run16029ParametricModel, readRepair = read16029ParametricRuntimeRepair,
  verifyBuild = verify16029ParametricBuild, inspectCad = inspect16029CadProcesses,
} = {}) {
  const manual = ['buildTag', 'repairProfile', 'resumeSource', 'resumeStage', 'resumeBackupPhase'].some(key => Boolean(options[key])) || options.phases != null
  if (manual || request.envelopeProbe || production16029Profile(request)) return runModel(request, options)
  const repair = readRepair({ root })
  if (!repair.buildTag) return runModel(request, options)
  requireValue(REPAIRS.includes(repair.buildTag) && repair.repairProfile === repair.buildTag, 'runtime bridge does not support the selected repair profile')
  const baseline = verifyBuild(undefined, { root })
  requireValue(baseline.tag === BASELINE && !baseline.manifest.productionProfile, 'runtime bridge baseline selection changed')
  const repaired = verifyBuild(repair.buildTag, { root, repairProfile: repair.repairProfile })
  const pin = build => ({ executableSha256: build.manifest.executableSha256, manifestSha256: hash(regular(resolve(build.folder, 'build.json'))) })
  const baselinePin = pin(baseline), repairPin = pin(repaired)
  const contract = build16029ParametricContract(request), { heightMm, depthMm } = contract.geometry.cabinet
  const doorHeights = new Map()
  for (const row of [...contract.rowsByColumn.L, ...contract.rowsByColumn.R]) {
    const height = row.doorHeightMm, token = Number(height.toFixed(6)).toString().replace('.', 'p')
    requireValue(!doorHeights.has(token) || doorHeights.get(token) === height, 'Distinct contract door heights map to the same door module identifier')
    doorHeights.set(token, height)
  }
  const prefix = ['clone', 'probe', 'dimensions', 'derived', 'base-hole', 'assemblies',
    ...(heightMm !== 1917 || depthMm !== 550 ? ['envelope'] : []),
    ...[...doorHeights.keys()].map(token => 'door-' + token),
    'repair-door-placements', 'repair-interfaces', 'repair-base-foot', 'locks', 'locks-verify', 'repair-partition-mates', 'root', 'repair-frame-slots']
  const suffix = ['clone', ...(heightMm !== 1917 ? ['repair-height-braces'] : []),
    ...(depthMm !== 550 ? ['repair-depth-placements'] : []), 'repair-base-mates', 'repair-engineer-feedback',
    'verify', 'package', 'verify-package', 'verify-relocated', 'physical-audit']
  const attemptsRoot = resolve(root, 'data/parametric_attempts')
  const existing = new Set(existsSync(attemptsRoot) ? readdirSync(attemptsRoot) : [])
  const preciseDoors = DOOR_PRECISION_REPAIRS.includes(repair.buildTag)
  const doorModules = preciseDoors ? [...doorHeights].map(([token, heightMm]) => ({ token, heightMm })) : null
  const first = await runModel(request, { ...options, ...(preciseDoors ? repair : { buildTag: BASELINE, repairProfile: '' }), phases: prefix })
  if (first.state === 'cancelled_after_atomic_stage') return first
  const source = verifySegment(first, prefix, contract, preciseDoors ? repairPin : baselinePin, attemptsRoot, existing, null, doorModules)
  await options.beforeResume?.()
  requireValue(!options.signal?.aborted, 'runtime bridge cancelled before resume')
  const cad = inspectCad()
  requireValue(cad.verified === true, 'runtime bridge cannot resume while CAD processes remain or their exit is unverified')
  requireValue(!options.signal?.aborted, 'runtime bridge cancelled before resume')
  const second = await runModel(request, { ...options, ...repair, resumeSource: source.attempt, resumeStage: 'repair-frame-slots' })
  if (second.state === 'cancelled_after_atomic_stage') return second
  existing.add(basename(source.attempt))
  verifySegment(second, suffix, contract, repairPin, attemptsRoot, existing, source, doorModules)
  return second
}
