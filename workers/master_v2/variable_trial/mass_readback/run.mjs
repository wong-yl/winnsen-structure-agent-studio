import { existsSync, copyFileSync, constants } from 'node:fs'
import { join, resolve, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'
import { DATA, directory, inside, read, write, hash } from '../common.mjs'
import { runNativeJob, runProtectedPhase } from '../run.mjs'
import { buildWeightToolchain } from './build.mjs'

const RUNS = {
  T3: '2026-11-13T04-20-56-558Z-resume-T3-338f953c',
  T4: '2026-11-13T05-08-41-897Z-recovery-T4-f58333e9',
}
const TARGETS = [
  { case: 'case1', identity: 'crossbar_R_3', name: '门框 横隔板R.SLDPRT', dependencies: ['门框 横隔板.sldprt', '标准寄存柜 模型.SLDPRT'] },
  { case: 'case1', identity: 'shelf_R_2', name: '箱体横层板R.SLDPRT', dependencies: ['箱体横层板L.sldprt', '标准寄存柜 模型.SLDPRT'] },
  { case: 'case1', identity: 'structure_1000_2', name: '门框 右.sldprt', dependencies: ['门框 左.sldprt', '标准寄存柜 模型.SLDPRT'] },
  { case: 'case3', identity: 'crossbar_R_2', name: '门框 横隔板R.SLDPRT', dependencies: ['门框 横隔板.sldprt', '标准寄存柜 模型.SLDPRT'] },
]
const SOURCE_TARGET_HASHES = {
  'T3/case1/crossbar_R_3': '52F5F1AEE426CDC601B46AA38C0B02EBB6DA07930FF15A1931FF71CFF75F5341',
  'T4/case1/crossbar_R_3': '8C57BBC7E1546F95946ED6667B217021350595E4B3647FE434016053EEEC34DE',
  'T3/case1/shelf_R_2': '5FA990E4801A0D481AD456DC3C10CD63E4C3968F1859ACE6B8F9C4F8E26DC345',
  'T4/case1/shelf_R_2': '6A5B9801E7EE31FA4013A57D17FFABF0DF08B3F1B3EBC00B68436BA843581B3C',
  'T3/case1/structure_1000_2': 'D106C5E8DCF08B5E5B5F2CF84DE3FEA78A61B5B4DE7090FD056E8F9643486B9D',
  'T4/case1/structure_1000_2': 'B97587AB503061BCB0669A3ACCDF563E4D90D343C872216E5F1C21D86B9FE7B4',
  'T3/case3/crossbar_R_2': '980AC1DAC1748B5BA4747F3B5F0EA59A55BC58C2050B8EB682FA9C153443C108',
  'T4/case3/crossbar_R_2': '0F1EA6F285F555AFC8FBB92E5A1ED3F2BECA9581FB717EACF1766BB92C025986',
}
const need = (value, message) => { if (!value) throw new Error(message) }

export function prepareWeightSeed() {
  const inputs = [], weightTargets = [], weightNamespaces = []
  for (const [origin, run] of Object.entries(RUNS)) {
    for (const caseName of ['case1', 'case3']) {
      const originalDirectory = inside(join(DATA, 'runs', run, 'cad/assemblies', caseName, 'structure'), DATA)
      const selected = TARGETS.filter(target => target.case === caseName)
      const names = [...new Set(selected.flatMap(target => [target.name, ...target.dependencies]))]
      for (const name of names) {
        const source = inside(join(originalDirectory, name), originalDirectory)
        need(existsSync(source), 'WEIGHT_SOURCE_DEPENDENCY_MISSING ' + source)
        inputs.push({ source, relativePath: join(origin, caseName, name), sha256: hash(source) })
      }
      weightNamespaces.push({ relativeDirectory: join(origin, caseName), originalDirectory })
      for (const target of selected) {
        const identity = `${origin}/${caseName}/${target.identity}`, relativePath = join(origin, caseName, target.name)
        const record = inputs.find(item => item.relativePath === relativePath)
        need(record.sha256 === SOURCE_TARGET_HASHES[identity], 'WEIGHT_SOURCE_VERSION_CHANGED ' + identity)
        weightTargets.push({ identity: target.identity, origin, relativePath, sourceSha256: record.sha256 })
      }
    }
  }
  need(inputs.length === 20 && weightTargets.length === 8 && weightNamespaces.length === 4, 'WEIGHT_SEED_SCOPE_MISMATCH')
  const seedRun = directory(join(DATA, 'runs', new Date().toISOString().replace(/[:.]/g, '-') + '-weight-seed-' + randomBytes(4).toString('hex')), DATA, true)
  const cad = directory(join(seedRun, 'cad'), DATA, true)
  for (const input of inputs) {
    const path = inside(join(cad, input.relativePath), cad)
    directory(resolve(path, '..'), DATA)
    need(hash(input.source) === input.sha256, 'WEIGHT_SOURCE_CHANGED_BEFORE_COPY')
    copyFileSync(input.source, path, constants.COPYFILE_EXCL)
    need(hash(path) === input.sha256 && hash(input.source) === input.sha256, 'WEIGHT_COPY_CHANGED')
  }
  const record = { schema: '16029-weight-readback-seed/v1', cad, inputs, weightTargets, weightNamespaces,
    dependencyScope: 'historical expected closure; actual native closure must be checked', production_release: false, fullPlanPass: false }
  write(join(seedRun, 'weight-seed.json'), record)
  return { seedRun, record }
}

const reference = path => ({ path: inside(resolve(path), DATA), sha256: hash(inside(resolve(path), DATA)) })

export function validateFirstRoundReceipt(receiptFile) {
  const path = inside(resolve(receiptFile), DATA), receipt = read(path)
  need(receipt.schema === '16029-weight-round/v1' && receipt.round === 'first' && receipt.pass === true
    && receipt.production_release === false && receipt.acceptancePass === false && receipt.fullPlanPass === false, 'WEIGHT_SUCCESSFUL_FIRST_ROUND_REQUIRED')
  const run = inside(receipt.run, join(DATA, 'runs'))
  need(resolve(run, '..') === join(DATA, 'runs') && path === join(run, 'weight-round.json'), 'WEIGHT_FIRST_ROUND_PATH_REQUIRED')
  need(!existsSync(join(run, 'cancel.json')), 'WEIGHT_CANCELLED_FIRST_ROUND')
  for (const item of [receipt.seed, ...receipt.evidence]) need(hash(inside(item.path, DATA)) === item.sha256, 'WEIGHT_FIRST_ROUND_EVIDENCE_CHANGED')
  const evidence = new Set(receipt.evidence.map(item => item.path))
  for (const name of ['job.json', 'native-result.json', 'result.json', 'supervision.json', 'protection-result.json', 'toolchain.json'])
    need(evidence.has(join(run, name)), 'WEIGHT_FIRST_ROUND_EVIDENCE_MISSING ' + name)
  const job = read(join(run, 'job.json')), native = read(join(run, 'native-result.json')), completed = read(join(run, 'result.json'))
  const supervision = read(join(run, 'supervision.json')), protection = read(join(run, 'protection-result.json'))
  const claims = native.events.filter(event => event.inspection === 'cad_owned'), diagnostics = native.events.filter(event => event.inspection === 'weight_readback_diagnostic')
  need(job.action === 'weight-readback-diagnostic' && job.weightRound === 'first' && job.production_release === false
    && JSON.stringify(job.weightSeed) === JSON.stringify(receipt.seed), 'WEIGHT_FIRST_ROUND_JOB_BINDING_CHANGED')
  need(native.events.at(-1)?.inspection === 'finished' && native.events.at(-1)?.success === true && native.events.at(-1)?.cadExited === true
    && completed.code === 0 && completed.cadExited === true && supervision.state === 'cad_exited' && supervision.nativeEnded === true
    && supervision.cad.length === 0 && protection.pass === true && protection.changed.length === 0
    && protection.nativeTaskIndex?.accepted !== false, 'WEIGHT_FIRST_ROUND_NATIVE_FAILED')
  need(claims.length === 1 && JSON.stringify(claims[0]) === JSON.stringify(receipt.cad) && diagnostics.length === 1
    && diagnostics[0].result.success === true && diagnostics[0].result.readbackComplete === true
    && diagnostics[0].result.sourceFilesUnchanged === true && diagnostics[0].result.readOnlyCopyFilesUnchanged === true,
  'WEIGHT_FIRST_ROUND_DIAGNOSTIC_INCOMPLETE')
  return { receipt, reference: reference(path) }
}

export async function runWeightDiagnostic(mode, firstReceiptFile = null) {
  need(['first', 'second'].includes(mode), 'USAGE: node mass_readback/run.mjs first | second <first-round-receipt.json>')
  need(mode === 'first' ? firstReceiptFile === null : typeof firstReceiptFile === 'string', 'WEIGHT_ROUND_ARGUMENT_REQUIRED')
  const prior = mode === 'second' ? validateFirstRoundReceipt(firstReceiptFile) : null
  const seed = mode === 'first' ? prepareWeightSeed() : { record: read(prior.receipt.seed.path) }
  const record = seed.record, seedFile = mode === 'first' ? join(seed.seedRun, 'weight-seed.json') : prior.receipt.seed.path
  need(record.schema === '16029-weight-readback-seed/v1' && record.production_release === false && record.fullPlanPass === false, 'WEIGHT_SEED_RECORD_REQUIRED')
  const source = inside(record.cad, join(DATA, 'runs'))
  need(record.inputs.length === 20 && record.weightTargets.length === 8 && record.weightNamespaces.length === 4, 'WEIGHT_SEED_SCOPE_MISMATCH')
  for (const input of record.inputs) need(hash(inside(join(source, input.relativePath), source)) === input.sha256 && hash(input.source) === input.sha256, 'WEIGHT_SEED_OR_ORIGINAL_CHANGED')
  const built = buildWeightToolchain(), toolSources = built.manifest.sources.map(item => ({ name: basename(item.path), sha256: item.sha256 }))
  if (prior) need(JSON.stringify(prior.receipt.toolSources) === JSON.stringify(toolSources), 'WEIGHT_CROSS_ROUND_SOURCE_VERSION_CHANGED')
  let result, failure, runDirectory = null
  try {
    result = await runProtectedPhase({ tag: 'weight-diagnostic-' + mode, sourceDirectory: source, currentFile: null,
      job: { action: 'weight-readback-diagnostic', weightTargets: mode === 'first' ? record.weightTargets : [...record.weightTargets].reverse(),
        weightNamespaces: record.weightNamespaces, weightInputCopies: record.inputs, weightRound: mode,
        weightSeed: reference(seedFile), weightPriorRound: prior?.reference || null, weightToolchainProvenance: reference(built.provenancePath),
        production_release: false, fullPlanPass: false, drawingStatus: 'deferred_by_user_20261113' } },
      { executeNative: async (job, dir, options) => {
        runDirectory = dir
        return await runNativeJob(job, dir, { ...options, toolchainManifest: built.manifest, sourceRoot: built.sourceRoot })
      } })
  } catch (error) { failure = error }
  const originals = record.inputs.map(input => ({ path: input.source, expectedSha256: input.sha256, actualSha256: hash(input.source) }))
  const originalCheck = { schema: '16029-weight-original-protection/v1', originals, pass: originals.every(item => item.actualSha256 === item.expectedSha256), production_release: false }
  const checkName = 'weight-originals-' + mode + '-' + randomBytes(4).toString('hex') + '.json'
  const checkPath = join(result?.dir || runDirectory || resolve(source, '..'), checkName)
  write(checkPath, originalCheck)
  if (!originalCheck.pass) throw new Error('WEIGHT_ORIGINAL_FILES_CHANGED')
  if (failure) throw failure
  const native = read(join(result.dir, 'native-result.json')), claims = native.events.filter(event => event.inspection === 'cad_owned')
  const diagnostics = native.events.filter(event => event.inspection === 'weight_readback_diagnostic')
  need(claims.length === 1 && diagnostics.length === 1 && diagnostics[0].result.success === true && diagnostics[0].result.readbackComplete === true, 'WEIGHT_NATIVE_READBACK_INCOMPLETE')
  const claim = claims[0]
  if (prior) need(prior.receipt.run !== result.dir && prior.receipt.cad.startedAtUtc !== claim.startedAtUtc
    && prior.receipt.cad.startTicks !== claim.startTicks, 'WEIGHT_SECOND_NEW_CAD_PROCESS_REQUIRED')
  const evidence = ['job.json', 'native-result.json', 'result.json', 'supervision.json', 'protection-result.json', 'toolchain.json']
    .map(name => reference(join(result.dir, name)))
  evidence.push(reference(checkPath), reference(built.provenancePath))
  const receiptPath = join(result.dir, 'weight-round.json')
  const receipt = { schema: '16029-weight-round/v1', round: mode, run: result.dir, pass: true, seed: reference(seedFile),
    priorRound: prior?.reference || null, cad: claim, toolSources, evidence, acceptancePass: false, fullPlanPass: false, production_release: false }
  write(receiptPath, receipt)
  return { ...result, seedFile, roundReceipt: receiptPath }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  console.log(JSON.stringify(await runWeightDiagnostic(...process.argv.slice(2))))
}
