// Throwaway clone-alignment experiment. Preserves every earlier candidate and production selection.
import { readFileSync, writeFileSync, readdirSync, lstatSync, mkdirSync, copyFileSync, constants, existsSync } from 'node:fs'
import { resolve, dirname, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawn } from 'node:child_process'
import { acquireNativeWorkerInstanceLock } from '../../../tools/run_16029_native_worker.mjs'

const folder = dirname(fileURLToPath(import.meta.url)), root = resolve(folder, '../../..')
const sourcePack = resolve(root, 'data/parametric_attempts/PARAM-20261110035409405-0C0A4D/native_cad/working_pack')
const candidatePack = resolve(folder, 'candidate_pack_v6')
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase()
const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''))
const put = (p, v) => writeFileSync(p, JSON.stringify(v, null, 2) + '\n', { flag: 'wx' })
const [operation, ...extra] = process.argv.slice(2)
if (!['--dry-run', '--run-native'].includes(operation) || extra.length) throw Error('Usage: node run-relink-v6.mjs --dry-run|--run-native')

const receiptPath = resolve(sourcePack, '../../receipts/assemblies.json'), receipt = read(receiptPath)
const evidencePath = resolve(sourcePack, '../../evidence/assemblies.json')
const receiptSha = '1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD'
const evidenceSha = '2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2'
if (hash(receiptPath) !== receiptSha || receipt.phase !== 'assemblies' || receipt.evidenceSha256 !== evidenceSha || hash(evidencePath) !== evidenceSha) throw Error('Source checkpoint changed')
const sourceRows = receipt.after.filter(row => dirname(row.path) === sourcePack)
if (sourceRows.length !== 75 || new Set(sourceRows.map(row => basename(row.path).toLowerCase())).size !== 75) throw Error('Source requires 75 unique bound files')
const buildPath = resolve(folder, 'relink_v6/build.json'), build = read(buildPath), executable = build.executable
if (build.buildPassed !== true || hash(executable) !== build.executableSha256 || hash(build.source) !== build.sourceSha256) throw Error('Relink build changed')
const staticPath = resolve(folder, 'relink_v6/static-validation.json'), staticEvidence = read(staticPath)
if (staticEvidence.failed !== 0 || staticEvidence.sourceSha256 !== build.sourceSha256 || staticEvidence.executableSha256 !== build.executableSha256 || staticEvidence.buildManifestSha256 !== hash(buildPath)) throw Error('Relink static evidence differs')
const oldBindings = read(resolve(folder, '../brace_forming_readonly_probe/bin/bindings.json'))
const immutable = [...read(resolve(sourcePack, '../../protected_sources.before.json')), ...sourceRows,
  { path: receiptPath, sha256: receiptSha }, { path: evidencePath, sha256: evidenceSha },
  { path: oldBindings.observer.path, sha256: oldBindings.observer.sha256 }]
for (const name of ['candidate_pack', 'candidate_pack_v2', 'candidate_pack_v3', 'candidate_pack_v4', 'candidate_pack_v5']) {
  const pack = resolve(folder, name)
  if (!existsSync(pack)) throw Error('Frozen candidate missing: ' + pack)
  for (const name of readdirSync(pack)) { const path = resolve(pack, name); immutable.push({ path, sha256: hash(path) }) }
}
for (const name of readdirSync(resolve(folder, 'runs'))) {
  const run = resolve(folder, 'runs', name)
  for (const file of ['probe.json', 'session.json', 'manifest.json', 'runner.json']) {
    const path = resolve(run, file)
    if (existsSync(path)) immutable.push({ path, sha256: hash(path) })
  }
}
for (const path of [executable, buildPath, build.source, staticPath, resolve(folder, 'observe-cold-diagnostic.py'), fileURLToPath(import.meta.url), ...readdirSync(dirname(executable)).filter(name => /\.dll$/i.test(name)).map(name => resolve(dirname(executable), name))]) immutable.push({ path, sha256: hash(path) })
const protectedFiles = [...new Map(immutable.map(row => [row.path.toLowerCase(), row])).values()]
for (const row of protectedFiles) {
  const s = lstatSync(row.path)
  if (!s.isFile() || s.isSymbolicLink() || s.nlink !== 1 || hash(row.path) !== row.sha256) throw Error('Protected input differs: ' + row.path)
}
if (existsSync(candidatePack)) throw Error('V6 candidate already exists; preserve it')
if (operation === '--dry-run') {
  console.log(JSON.stringify({ dryRun: true, mode: 'relink', sourcePack, candidatePack, immutableCount: protectedFiles.length, executable, nativeCadExecuted: false, modelAccepted: false }))
  process.exit(0)
}
const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(root, 'data'), workerId: 'isolated-relink-v6-' + process.pid })
const timer = setInterval(() => lock.heartbeat(), 10000)
try {
  const owner = lock.heartbeat()
  const out = resolve(folder, 'runs', new Date().toISOString().replace(/[^0-9]/g, '') + '-relink-v6-' + process.pid)
  mkdirSync(out, { recursive: true }); mkdirSync(candidatePack)
  for (const row of sourceRows) {
    const target = resolve(candidatePack, basename(row.path))
    if (hash(row.path) !== row.sha256) throw Error('Source changed before clone')
    copyFileSync(row.path, target, constants.COPYFILE_EXCL)
    if (hash(target) !== row.sha256) throw Error('Clone differs')
  }
  const files = sourceRows.map(row => ({ fileName: basename(row.path), sourcePath: row.path, path: resolve(candidatePack, basename(row.path)), sha256: row.sha256, size: lstatSync(row.path).size }))
  if (readdirSync(candidatePack).length !== 75) throw Error('Unexpected candidate entries')
  const manifestPath = resolve(out, 'manifest.json')
  put(manifestPath, { sourcePack, candidatePack, files, sourceReceipt: { path: receiptPath, sha256: receiptSha }, sourceEvidence: { path: evidencePath, sha256: evidenceSha } })
  protectedFiles.push({ path: manifestPath, sha256: hash(manifestPath) })
  const config = { root, mode: 'relink', pack: candidatePack, executable, output: resolve(out, 'probe.json'), out, immutable: protectedFiles, observer: oldBindings.observer, lockOwner: resolve(lock.lockDir, 'owner.json'), lockToken: owner.token, lockWorker: owner.workerId, lockPid: owner.pid }
  config.argv = [executable, 'relink', candidatePack, config.output, manifestPath]
  const configPath = resolve(out, 'runner.json'); put(configPath, config)
  console.log(JSON.stringify({ state: 'isolated_relink_v6_started', output: out, modelAccepted: false }))
  const child = spawn('python', [resolve(folder, 'observe-cold-diagnostic.py'), configPath, hash(configPath)], { cwd: candidatePack, windowsHide: true, stdio: 'inherit' })
  process.exitCode = await new Promise((res, rej) => { child.once('error', rej); child.once('close', res) })
} finally { clearInterval(timer); lock.release() }
