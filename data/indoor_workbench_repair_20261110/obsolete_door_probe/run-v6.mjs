// Throwaway continuation after exact closed-file clone relinking; no production activation.
import { readFileSync, writeFileSync, readdirSync, lstatSync, mkdirSync, existsSync } from 'node:fs'
import { resolve, dirname, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawn } from 'node:child_process'
import { acquireNativeWorkerInstanceLock } from '../../../tools/run_16029_native_worker.mjs'

const folder = dirname(fileURLToPath(import.meta.url)), root = resolve(folder, '../../..')
const sourcePack = resolve(root, 'data/parametric_attempts/PARAM-20261110035409405-0C0A4D/native_cad/working_pack')
const pack = resolve(folder, 'candidate_pack_v6'), runs = resolve(folder, 'runs')
const rootName = '标准寄存柜1917×1000×550(总装配).SLDASM'
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase()
const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''))
const put = (p, value) => writeFileSync(p, JSON.stringify(value, null, 2) + '\n', { flag: 'wx' })
const [mode, operation, relinkPathArg, baselinePathArg, editPathArg, ...extra] = process.argv.slice(2)
if (!['baseline', 'edit', 'diagnostic33', 'verify'].includes(mode) || !['--dry-run', '--run-native'].includes(operation) || !relinkPathArg || extra.length || (mode === 'baseline' ? baselinePathArg || editPathArg : !baselinePathArg || (mode === 'edit' ? editPathArg : !editPathArg))) throw Error('Usage: <baseline|edit|diagnostic33|verify> <--dry-run|--run-native> <relinkProbe> [baselineProbe] [editProbe]')

const receiptPath = resolve(sourcePack, '../../receipts/assemblies.json'), evidencePath = resolve(sourcePack, '../../evidence/assemblies.json')
const receiptSha = '1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD'
const evidenceSha = '2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2'
const receipt = read(receiptPath)
if (hash(receiptPath) !== receiptSha || receipt.phase !== 'assemblies' || receipt.evidenceSha256 !== evidenceSha || hash(evidencePath) !== evidenceSha) throw Error('Source checkpoint changed')
const sourceRows = receipt.after.filter(row => dirname(row.path) === sourcePack)
if (sourceRows.length !== 75) throw Error('Exact 75-file checkpoint required')
const buildPath = resolve(folder, 'build-v6.json'), build = read(buildPath), executable = build.executable
const relinkBuildPath = resolve(folder, 'relink_v6/build.json'), relinkBuild = read(relinkBuildPath)
for (const b of [build, relinkBuild]) if (b.buildPassed !== true || hash(b.source) !== b.sourceSha256 || hash(b.executable) !== b.executableSha256) throw Error('Frozen build changed')
const staticPath = resolve(folder, 'v6/static-validation.json'), staticEvidence = read(staticPath)
if (staticEvidence.failed !== 0 || staticEvidence.sourceSha256 !== build.sourceSha256 || staticEvidence.executableSha256 !== build.executableSha256 || staticEvidence.buildReportSha256 !== hash(buildPath)) throw Error('V6 static evidence differs')
const oldBindings = read(resolve(folder, '../brace_forming_readonly_probe/bin/bindings.json'))
const immutable = [...read(resolve(sourcePack, '../../protected_sources.before.json')), ...sourceRows, { path: receiptPath, sha256: receiptSha }, { path: evidencePath, sha256: evidenceSha }, { path: oldBindings.observer.path, sha256: oldBindings.observer.sha256 }]
const event = (proof, inspection, stage) => {
  const rows = proof.events.filter(row => row.inspection === inspection && (stage === undefined || row.stage === stage))
  if (rows.length !== 1) throw Error('Evidence event not unique: ' + inspection + '/' + stage)
  return rows[0]
}
function proof(argument, expectedMode, diagnostic, expectedExe) {
  const path = resolve(argument)
  if (dirname(dirname(path)) !== runs || basename(path) !== 'probe.json') throw Error('Evidence must be a local frozen run probe')
  const document = read(path), sessionPath = resolve(dirname(path), 'session.json'), session = read(sessionPath)
  const commandPath = resolve(dirname(path), 'command.json'), command = read(commandPath)
  const complete = event(document, 'completion')
  if (session.pass !== true || session.modelAccepted !== false || session.naturalExit !== true || session.allProtectedUnchanged !== true || complete.mode !== expectedMode || complete.candidatePack !== pack || (diagnostic ? complete.diagnosticCompleted !== true || complete.operationComplete !== false : complete.operationComplete !== true)) throw Error('Prior run was not successful for this candidate: ' + expectedMode)
  const argv = command.argv
  if (!Array.isArray(argv) || argv.length !== 5 || argv[0] !== expectedExe || argv[1] !== expectedMode || argv[2] !== pack || argv[3] !== path || dirname(argv[4]) !== dirname(path)) throw Error('Prior command identity differs')
  for (const p of [path, sessionPath, commandPath, argv[4]]) immutable.push({ path: p, sha256: hash(p) })
  return { path, sha256: hash(path), document, complete, manifest: read(argv[4]) }
}
const relink = proof(relinkPathArg, 'relink', true, relinkBuild.executable)
const relinkRows = event(relink.document, 'cad_inventory', 'candidate-after').files
if (!Array.isArray(relinkRows) || relinkRows.length !== 75 || new Set(relinkRows.map(row => row.path.toLowerCase())).size !== 75) throw Error('Relink inventory differs')
const files = sourceRows.map(row => {
  const path = resolve(pack, basename(row.path)), matches = relinkRows.filter(file => file.path.toLowerCase() === path.toLowerCase())
  if (matches.length !== 1 || !/^[A-F0-9]{64}$/.test(matches[0].sha256)) throw Error('Missing exact relink binding')
  return { fileName: basename(row.path), sourcePath: row.path, path, sha256: row.sha256, size: lstatSync(row.path).size, candidateSha256: matches[0].sha256, candidateSize: matches[0].size }
})
const baseline = baselinePathArg ? proof(baselinePathArg, 'baseline', true, executable) : null
if (baseline && (baseline.manifest.relinkEvidence?.sha256 !== relink.sha256 || JSON.stringify(baseline.manifest.files) !== JSON.stringify(files))) throw Error('Baseline belongs to another relink inventory')
const edit = editPathArg ? proof(editPathArg, 'edit', false, executable) : null
if (edit && (edit.manifest.relinkEvidence?.sha256 !== relink.sha256 || edit.manifest.baselineEvidence?.sha256 !== baseline.sha256 || JSON.stringify(edit.manifest.files) !== JSON.stringify(files) || !/^[A-F0-9]{64}$/.test(edit.complete.rootAfterSha256))) throw Error('Edit belongs to another baseline or relink inventory')
if (readdirSync(pack).length !== 75) throw Error('Candidate must contain exact 75 entries')
for (const file of files) {
  const expected = edit && file.fileName === rootName ? edit.complete.rootAfterSha256 : file.candidateSha256
  if (hash(file.path) !== expected) throw Error('Candidate differs: ' + file.path)
  if (mode !== 'edit' || file.fileName !== rootName) immutable.push({ path: file.path, sha256: expected })
}
for (const name of ['candidate_pack', 'candidate_pack_v2', 'candidate_pack_v3', 'candidate_pack_v4', 'candidate_pack_v5']) {
  const directory = resolve(folder, name)
  for (const entry of readdirSync(directory)) { const path = resolve(directory, entry); immutable.push({ path, sha256: hash(path) }) }
}
for (const directory of readdirSync(runs)) for (const file of ['probe.json', 'session.json', 'manifest.json', 'runner.json']) {
  const path = resolve(runs, directory, file)
  if (existsSync(path)) immutable.push({ path, sha256: hash(path) })
}
const observerScript = resolve(folder, mode === 'baseline' || mode === 'diagnostic33' ? 'observe-cold-diagnostic.py' : 'observe.py')
for (const path of [fileURLToPath(import.meta.url), observerScript, staticPath, executable, build.source, buildPath, relinkBuildPath, relinkBuild.source, relinkBuild.executable, ...readdirSync(dirname(executable)).filter(name => /\.dll$/i.test(name)).map(name => resolve(dirname(executable), name))]) immutable.push({ path, sha256: hash(path) })
const protectedFiles = [...new Map(immutable.map(row => [row.path.toLowerCase(), row])).values()]
for (const row of protectedFiles) {
  const s = lstatSync(row.path)
  if (!s.isFile() || s.isSymbolicLink() || s.nlink !== 1 || hash(row.path) !== row.sha256) throw Error('Protected input differs: ' + row.path)
}
if (operation === '--dry-run') {
  console.log(JSON.stringify({ dryRun: true, mode, candidatePack: pack, immutableCount: protectedFiles.length, executable, nativeCadExecuted: false, modelAccepted: false, relinkEvidence: relink.sha256, baselineEvidence: baseline?.sha256, editEvidence: edit?.sha256 }))
  process.exit(0)
}
const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(root, 'data'), workerId: 'isolated-v6-' + mode + '-' + process.pid })
const timer = setInterval(() => lock.heartbeat(), 10000)
try {
  const owner = lock.heartbeat(), out = resolve(runs, new Date().toISOString().replace(/[^0-9]/g, '') + '-v6-' + mode + '-' + process.pid)
  mkdirSync(out, { recursive: true })
  const manifestPath = resolve(out, 'manifest.json')
  put(manifestPath, { sourcePack, candidatePack: pack, files, sourceReceipt: { path: receiptPath, sha256: receiptSha }, sourceEvidence: { path: evidencePath, sha256: evidenceSha }, relinkEvidence: { path: relink.path, sha256: relink.sha256 }, baselineEvidence: baseline ? { path: baseline.path, sha256: baseline.sha256 } : null, expectedRootSha256: edit?.complete.rootAfterSha256 ?? null, editEvidence: edit ? { path: edit.path, sha256: edit.sha256 } : null })
  protectedFiles.push({ path: manifestPath, sha256: hash(manifestPath) })
  const config = { root, mode, pack, target: rootName, executable, output: resolve(out, 'probe.json'), out, immutable: protectedFiles, observer: oldBindings.observer, lockOwner: resolve(lock.lockDir, 'owner.json'), lockToken: owner.token, lockWorker: owner.workerId, lockPid: owner.pid }
  config.argv = [executable, mode, pack, config.output, manifestPath]
  const configPath = resolve(out, 'runner.json'); put(configPath, config)
  console.log(JSON.stringify({ state: 'isolated_v6_started', mode, output: out, modelAccepted: false }))
  const child = spawn('python', [observerScript, configPath, hash(configPath)], { cwd: pack, windowsHide: true, stdio: 'inherit' })
  process.exitCode = await new Promise((res, rej) => { child.once('error', rej); child.once('close', res) })
} finally { clearInterval(timer); lock.release() }
