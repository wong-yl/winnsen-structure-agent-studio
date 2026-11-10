// Throwaway closed-dependency collection. No documents are opened and no model edits are authorized.
import { readFileSync, writeFileSync, readdirSync, lstatSync, mkdirSync } from 'node:fs'
import { resolve, dirname, basename } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawn } from 'node:child_process'
import { acquireNativeWorkerInstanceLock } from '../../../tools/run_16029_native_worker.mjs'

const folder = dirname(fileURLToPath(import.meta.url)), root = resolve(folder, '../../..')
const sourcePack = resolve(root, 'data/parametric_attempts/PARAM-20261110035409405-0C0A4D/native_cad/working_pack')
const candidatePack = resolve(folder, 'candidate_pack_v5')
const rootName = '标准寄存柜1917×1000×550(总装配).SLDASM'
const rootHash = '1A14C86D381267E95ACDA7BDD825E8BA028261E1F19BB3C6A8D88BA77E5A5DB1'
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex').toUpperCase()
const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''))
const put = (p, value) => writeFileSync(p, JSON.stringify(value, null, 2) + '\n', { flag: 'wx' })
const [mode, operation, ...extra] = process.argv.slice(2)
if (!['diagnostic35'].includes(mode) || !['--dry-run', '--run-native'].includes(operation) || extra.length) throw Error('Usage: diagnostic35|diagnostic33 --dry-run|--run-native')

const receiptPath = resolve(sourcePack, '../../receipts/assemblies.json'), receipt = read(receiptPath)
const evidencePath = resolve(sourcePack, '../../evidence/assemblies.json')
if (hash(receiptPath) !== '1F89B12B1F63D8134ACFB04BA53B24571A0CB57035C6B16FA44F8F4107EDB4BD' || receipt.phase !== 'assemblies' || receipt.evidenceSha256 !== '2417CD3A7578A85EA4047D10E7EB6C932C89AD7976C46B8951EBB49D497C6AC2' || hash(evidencePath) !== receipt.evidenceSha256) throw Error('Source checkpoint changed')
const sourceRows = receipt.after.filter(row => dirname(row.path) === sourcePack)
if (sourceRows.length !== 75) throw Error('Source must contain 75 bound files')
const editPath = resolve(folder, 'runs/20261110065558794-edit-33604/probe.json')
const editSessionPath = resolve(dirname(editPath), 'session.json')
const editSha = 'FD3965620A9DE4B8F35C9DA3FA9741C556079B044039EB5D32EAE961DFAEF2D9'
if (hash(editPath) !== editSha || read(editSessionPath).pass !== true || read(editSessionPath).modelAccepted !== false) throw Error('Successful edit evidence changed')
const completion = read(editPath).events.filter(row => row.inspection === 'completion')
if (completion.length !== 1 || completion[0].operationComplete !== true || completion[0].rootAfterSha256 !== rootHash) throw Error('Edit completion differs')
const editEvidence = { path: editPath, sha256: editSha }
const buildPath = resolve(folder, 'closed_dependency/build.json'), build = read(buildPath), executable = build.executable
if (build.buildPassed !== true || hash(executable) !== build.executableSha256 || hash(build.source) !== build.sourceSha256) throw Error('Diagnostic build changed')
const frozenBindings = read(resolve(folder, '../brace_forming_readonly_probe/bin/bindings.json'))
const immutable = [...read(resolve(sourcePack, '../../protected_sources.before.json')), ...sourceRows,
  { path: receiptPath, sha256: hash(receiptPath) }, { path: evidencePath, sha256: hash(evidencePath) },
  { path: editPath, sha256: editSha }, { path: editSessionPath, sha256: hash(editSessionPath) },
  { path: frozenBindings.observer.path, sha256: frozenBindings.observer.sha256 },
  { path: resolve(folder, '../identity_probe/config35/runs/20261110052310959-32984/probe.json'), sha256: '86661305C2ACD047D431DB12CE800D2449472B6AFC0786EE6C395C3D6F10F7BE' }]
const files = sourceRows.map(row => ({ fileName: basename(row.path), sourcePath: row.path, path: resolve(candidatePack, basename(row.path)), sha256: row.sha256, size: lstatSync(row.path).size }))
if (readdirSync(candidatePack).length !== 75) throw Error('Unexpected candidate entries')
for (const row of files) immutable.push({ path: row.path, sha256: row.fileName === rootName ? rootHash : row.sha256 })
for (const p of [executable, buildPath, build.source, resolve(folder, 'observe-cold-diagnostic.py'), fileURLToPath(import.meta.url), ...readdirSync(dirname(executable)).filter(name => /\.dll$/i.test(name)).map(name => resolve(dirname(executable), name))]) immutable.push({ path: p, sha256: hash(p) })
const protectedFiles = [...new Map(immutable.map(row => [row.path.toLowerCase(), row])).values()]
for (const row of protectedFiles) {
  const info = lstatSync(row.path)
  if (!info.isFile() || info.isSymbolicLink() || info.nlink !== 1 || hash(row.path) !== row.sha256) throw Error('Protected input differs: ' + row.path)
}
if (operation === '--dry-run') {
  console.log(JSON.stringify({ dryRun: true, mode, sourcePack, candidatePack, immutableCount: protectedFiles.length, executable, editEvidence, rootHash, nativeCadExecuted: false, modelAccepted: false }))
  process.exit(0)
}
const lock = acquireNativeWorkerInstanceLock({ dataDir: resolve(root, 'data'), workerId: 'isolated-cold-' + mode + '-' + process.pid })
const timer = setInterval(() => lock.heartbeat(), 10000)
try {
  const owner = lock.heartbeat()
  const out = resolve(folder, 'runs', new Date().toISOString().replace(/[^0-9]/g, '') + '-' + mode + '-' + process.pid)
  mkdirSync(out, { recursive: true })
  const manifestPath = resolve(out, 'manifest.json')
  put(manifestPath, { sourcePack, candidatePack, files, sourceReceipt: { path: receiptPath, sha256: hash(receiptPath) }, expectedRootSha256: rootHash, editEvidence })
  protectedFiles.push({ path: manifestPath, sha256: hash(manifestPath) })
  const config = { root, mode, pack: candidatePack, target: rootName, executable, output: resolve(out, 'probe.json'), out, immutable: protectedFiles, observer: frozenBindings.observer, lockOwner: resolve(lock.lockDir, 'owner.json'), lockToken: owner.token, lockWorker: owner.workerId, lockPid: owner.pid }
  config.argv = [executable, mode, candidatePack, config.output, manifestPath]
  const configPath = resolve(out, 'runner.json'); put(configPath, config)
  console.log(JSON.stringify({ state: 'isolated_closed_dependency_started', mode, output: out, modelAccepted: false }))
  const child = spawn('python', [resolve(folder, 'observe-cold-diagnostic.py'), configPath, hash(configPath)], { cwd: candidatePack, windowsHide: true, stdio: 'inherit' })
  process.exitCode = await new Promise((res, rej) => { child.once('error', rej); child.once('close', res) })
} finally { clearInterval(timer); lock.release() }
