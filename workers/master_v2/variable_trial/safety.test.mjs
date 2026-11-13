import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, readdirSync, realpathSync, existsSync, rmSync, copyFileSync, symlinkSync } from 'node:fs'
import fs from 'node:fs'
import { syncBuiltinESMExports } from 'node:module'
import { resolve, join, dirname, basename, relative } from 'node:path'
import { tmpdir } from 'node:os'
import { EventEmitter } from 'node:events'
import { PassThrough } from 'node:stream'
import { inside, directory, write, read, hash, files, copyTree, protectionSnapshot, compareProtection, processState, assertIdle, acquireLock, validateToolchain, TOOLCHAIN_SCHEMA, INTEROP_DLLS } from './common.mjs'
import { archiveSources } from './build.mjs'
import { superviseCadSession, runNativeJob, runProtectedPhase, trialSource, protectedScopes } from './run.mjs'

function temporary(t) {
  const temporaryRoot = realpathSync.native(tmpdir())
  const root = mkdtempSync(join(temporaryRoot, 'winnsen-variable-trial-safety-'))
  t.after(() => {
    const tail = relative(temporaryRoot, root)
    assert.ok(tail.startsWith('winnsen-variable-trial-safety-') && !/[\\/]/.test(tail))
    rmSync(root, { recursive: true, force: true })
  })
  return root
}

function rawFile(path, content = 'fixture') {
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, content)
  return path
}

function mockRename(t, implementation) {
  const original = fs.renameSync
  t.mock.method(fs, 'renameSync', (from, to) => implementation(from, to, original))
  syncBuiltinESMExports()
  t.after(() => { t.mock.restoreAll(); syncBuiltinESMExports() })
}

function toolFixture(t) {
  const root = temporary(t), dataRoot = join(root, 'data'), sourceRoot = join(root, 'source'), bin = join(dataRoot, 'toolchain', 'fixture-v2')
  rawFile(join(sourceRoot, 'VariableTrial.cs'), Buffer.from('\ufefffixture source\r\n', 'utf8'))
  rawFile(join(sourceRoot, 'TrialOutput.cs'), 'output source\n')
  directory(bin, dataRoot, true)
  const sources = archiveSources(sourceRoot, bin, { dataRoot })
  const exe = rawFile(join(bin, 'VariableTrial.exe'), 'offline fake executable; never execute')
  const dlls = INTEROP_DLLS.map(name => { const path = rawFile(join(bin, name), 'offline fake ' + name); return { path, sha256: hash(path) } })
  const manifest = { schema: TOOLCHAIN_SCHEMA, exe, sha256: hash(exe), sources, dlls }
  write(join(dataRoot, 'toolchain-current.json'), manifest, { root: dataRoot })
  return { root, dataRoot, sourceRoot, bin, manifest }
}

function fakeLock() {
  return {
    owners: [], heartbeats: 0, assertions: 0, released: false, lockDir: 'fixture-only-lock', owner: { workerId: 'fixture-worker', token: 'fixture-token' },
    assert() { this.assertions++; if (this.failure) throw this.failure },
    heartbeat() { this.assert(); this.heartbeats++ },
    bindProcess(identity) { this.assert(); this.owners.push(identity) },
    release() { this.assert(); this.released = true },
  }
}

const nativeProcess = { name: 'VariableTrial.exe', pid: 1001, parentPid: 1, startedAtUtc: '2026-11-12T00:00:00.0000000Z', executable: process.execPath }
const cadProcess = { name: 'SLDWORKS.exe', pid: 1002, parentPid: 1001, startedAtUtc: '2026-11-12T00:00:01.1234567Z', executable: process.execPath }
const cadClaim = { inspection: 'cad_owned', pid: cadProcess.pid, startedAtUtc: cadProcess.startedAtUtc, executable: cadProcess.executable }

test('processState rejects error exit, stderr, malformed JSON, and incomplete identities', () => {
  const ok = processes => ({ status: 0, stdout: JSON.stringify({ schema: 'winnsen.variable_trial.processes.v1', processes }), stderr: '' })
  const invalid = [
    { status: 1, stdout: '', stderr: 'CIM failure' },
    { status: 0, stdout: '', stderr: 'nonterminating CIM failure' },
    { status: null, stdout: '', stderr: '', error: new Error('timeout') },
    { status: 0, stdout: '', stderr: '' },
    { status: 0, stdout: '[]', stderr: '' },
    ok([{ ...cadProcess, startedAtUtc: undefined }]),
    ok([{ ...cadProcess, executable: '' }]),
    ok([cadProcess, cadProcess]),
  ]
  for (const result of invalid) assert.throws(() => processState({ runner: () => result }), /PROCESS_/)
  assert.deepEqual(processState({ runner: (_exe, args) => {
    assert.match(args.at(-1), /ErrorActionPreference='Stop'/)
    assert.match(args.at(-1), /catch.*exit 1/)
    return ok([])
  } }), [])
  assert.deepEqual(processState({ pids: [nativeProcess.pid], runner: () => ok([nativeProcess, cadProcess]) }), [nativeProcess, cadProcess])
  assert.throws(() => processState({ runner: () => ok([nativeProcess]) }), /UNREQUESTED_PROCESS/)
})

test('assertIdle propagates query failures and rejects CAD or active formal tasks', () => {
  const failure = new Error('CIM unavailable')
  assert.throws(() => assertIdle({ query: () => { throw failure }, tasks: () => [] }), error => error === failure)
  assert.throws(() => assertIdle({ query: () => [cadProcess], tasks: () => [] }), /CAD_RESOURCE_BUSY/)
  assert.throws(() => assertIdle({ query: () => [], tasks: () => [{ id: 'NATIVE-test', state: 'running' }] }), /CAD_RESOURCE_BUSY/)
  assert.doesNotThrow(() => assertIdle({ query: () => [], tasks: () => [] }))
})

test('paths reject traversal, UNC, alternate streams, and directory links before writes', t => {
  const root = temporary(t), allowed = join(root, 'allowed'), outside = join(root, 'outside')
  mkdirSync(allowed); mkdirSync(outside)
  assert.equal(inside(join(allowed, 'new', 'file.json'), allowed), join(allowed, 'new', 'file.json'))
  for (const path of [join(outside, 'x.json'), '\\\\review-server\\share\\outside-output', join(allowed, 'x.json:stream'), join(allowed, 'trailing.')]) {
    assert.throws(() => inside(path, allowed), /PATH_ESCAPE|AMBIGUOUS_PATH/)
  }
  const junction = join(allowed, 'redirected')
  symlinkSync(outside, junction, 'junction')
  assert.throws(() => write(join(junction, 'escape.json'), {}, { root: allowed }), /REPARSE_PATH/)
  assert.throws(() => files(allowed), /REPARSE_PATH/)
  assert.equal(existsSync(join(outside, 'escape.json')), false)
  assert.throws(() => write(join(outside, 'outside.json'), {}, { root: allowed }), /PATH_ESCAPE/)
  assert.equal(existsSync(join(outside, 'outside.json')), false)
})

test('copyTree verifies every byte and preserves nested membership independently', t => {
  const root = temporary(t), source = join(root, 'source'), outputRoot = join(root, 'output')
  rawFile(join(source, 'panel.SLDPRT'), 'panel bytes')
  rawFile(join(source, 'nested', 'door.SLDASM'), 'nested assembly bytes')
  const target = join(outputRoot, 'cad')
  const report = copyTree(source, target, { outputRoot, sourceRoot: root })
  assert.equal(report.files.length, 2)
  assert.deepEqual(files(target).map(path => relative(target, path)), files(source).map(path => relative(source, path)))
  for (const item of report.files) assert.equal(hash(join(target, item.relativePath)), item.sha256)
  writeFileSync(join(source, 'panel.SLDPRT'), 'changed original')
  assert.equal(readFileSync(join(target, 'panel.SLDPRT'), 'utf8'), 'panel bytes')
  assert.throws(() => copyTree(source, target, { outputRoot, sourceRoot: root }), /EEXIST/)
})

test('copyTree rejects corruption and source-member changes during copying', t => {
  const root = temporary(t), source = join(root, 'source'), outputRoot = join(root, 'output')
  rawFile(join(source, 'panel.SLDPRT'), 'original')
  assert.throws(() => copyTree(source, join(outputRoot, 'corrupt'), { outputRoot, sourceRoot: root, copyFile: (_from, to) => rawFile(to, 'corrupt') }), /COPY_BYTES_MISMATCH/)
  assert.throws(() => copyTree(source, join(outputRoot, 'changed-source'), { outputRoot, sourceRoot: root, copyFile: (from, to, flag) => {
    copyFileSync(from, to, flag); rawFile(join(source, 'added.SLDPRT'), 'new member')
  } }), /COPY_SOURCE_CHANGED/)
  assert.throws(() => copyTree(source, join(source, 'nested-copy'), { outputRoot: root, sourceRoot: root }), /OVERLAP/)
})

test('protection detects additions, deletion, changed actual source bytes and optional file creation', t => {
  const root = temporary(t), queue = join(root, 'queue'), actualSource = join(root, 'actual-source'), optional = join(root, 'optional.json')
  rawFile(join(queue, 'NATIVE-existing.json'), '{}')
  rawFile(join(actualSource, 'nested', 'door.SLDPRT'), 'actual input')
  const scopes = [{ path: queue, required: true, extensions: ['.json', '.zip'], excludePrefixes: ['.native-worker-instance.lock'] }, { path: actualSource, required: true }, { path: optional }]
  const before = protectionSnapshot(scopes)
  rawFile(join(queue, '.native-worker-instance.lock', 'owner.json'), 'ignore coordination lock')
  assert.deepEqual(compareProtection(before), [])
  const added = rawFile(join(queue, 'PARAMETRIC-added.json'), '{}')
  const changedSource = rawFile(join(actualSource, 'nested', 'door.SLDPRT'), 'changed')
  rawFile(optional, '{}')
  rmSync(join(queue, 'NATIVE-existing.json'))
  const changed = compareProtection(before)
  for (const path of [added, changedSource, optional, join(queue, 'NATIVE-existing.json')]) assert.ok(changed.includes(path))
  const sourceScopes = protectedScopes(actualSource, { root, seed: actualSource })
  assert.ok(sourceScopes.some(scope => scope.path === actualSource && scope.required))
})

test('case3 copy exclusion preserves the original failed files and protects excluded bytes', t => {
  const root=temporary(t),source=join(root,'source'),outputRoot=join(root,'output')
  const original=rawFile(join(source,'assemblies','case3','failure.json'),'original failure')
  rawFile(join(source,'assemblies','case1','evidence.json'),'retained measurement')
  const manifest=copyTree(source,join(outputRoot,'copy'),{outputRoot,sourceRoot:root,excludeRelativeDirectories:['assemblies/case3']})
  assert.deepEqual(manifest.excludedRelativeDirectories,['assemblies/case3'])
  assert.equal(existsSync(join(outputRoot,'copy','assemblies','case3')),false)
  assert.equal(readFileSync(original,'utf8'),'original failure')
  assert.equal(readFileSync(join(outputRoot,'copy','assemblies','case1','evidence.json'),'utf8'),'retained measurement')
  assert.throws(()=>copyTree(source,join(outputRoot,'mutation'),{outputRoot,sourceRoot:root,excludeRelativeDirectories:['assemblies/case3'],copyFile:(from,to,flag)=>{copyFileSync(from,to,flag);rawFile(original,'unexpected mutation')}}),/COPY_SOURCE_CHANGED/)
  for(const invalid of ['../source','assemblies\\case3','assemblies//case3','/assemblies'])assert.throws(()=>copyTree(source,join(outputRoot,'invalid'),{outputRoot,sourceRoot:root,excludeRelativeDirectories:[invalid]}),/INVALID_COPY_EXCLUSION/)
})

test('protected phase only omits case3 for the exact two-case assembly resume contract', async t => {
  const fixture=phaseFixture(t),{dataRoot}=fixture,sourceRun=join(dataRoot,'runs','failed-assembly'),source=join(sourceRun,'cad')
  rawFile(join(source,'assemblies','case1','evidence.json'),'retained one')
  rawFile(join(source,'assemblies','case2','evidence.json'),'retained two')
  const failed=rawFile(join(source,'assemblies','case3','failure.json'),'retained old failure'),beforeHash=hash(failed)
  const job={action:'assembly',assemblyResume:{schema:'16029-variable-assembly-resume/v1',sourceRun,generateCaseIndices:[3],reuseCaseIndices:[1,2]}}
  let calls=0
  const options={...fixture.options,executeNative:async(payload)=>{calls++;assert.equal(existsSync(join(payload.cadDirectory,'assemblies','case3')),false);assert.equal(hash(failed),beforeHash);return {code:0,cadExited:true}}}
  const args={tag:'resume-T3',sourceDirectory:source,job,excludeRelativeDirectories:['assemblies/case3']}
  const result=await runProtectedPhase(args,options)
  assert.equal(result.pass,true);assert.equal(calls,1);assert.equal(hash(failed),beforeHash)
  for(const changed of [{job:{action:'assembly'}},{excludeRelativeDirectories:['assemblies/case1']},{job:{...job,assemblyResume:{...job.assemblyResume,generateCaseIndices:[1]}}}])await assert.rejects(runProtectedPhase({...args,...changed},options),/EXACT_CASE3_RESUME_EXCLUSION_REQUIRED/)
  assert.equal(calls,1)
})

function nativeIndexFixture(t) {
  const root = temporary(t), store = join(root, 'data', 'native_model_requests')
  const path = join(store, 'native_model_request_index.json'), task = join(store, 'NATIVE-fixture.json')
  const value = { schema: 'winnsen.native_model_request_index.v2', rebuiltAt: '2026-11-13T00:00:00.000Z',
    requests: [{ id: 'NATIVE-fixture', revision: 1, nativeBuild: { state: 'cancelled', rebuiltAt: 'task content' } },
      { id: 'NATIVE-second', revision: 2 }] }
  rawFile(task, JSON.stringify(value.requests[0]) + '\n')
  const put = data => rawFile(path, JSON.stringify(data, null, 2) + '\n')
  put(value)
  const scopes = [{ path: store, required: true, extensions: ['.json', '.zip'] }]
  return { root, store, path, task, value, put, snapshot: () => protectionSnapshot(scopes, { root }),
    compare: (before, callback = () => {}) => compareProtection(before, { root, onNativeTaskIndexComparison: callback }) }
}

test('exact native index snapshots preserve original bytes, SHA and semantics while accepting only rebuiltAt', t => {
  const fixture = nativeIndexFixture(t), { path, value, put } = fixture
  const original = readFileSync(path), before = fixture.snapshot(), evidence = join(fixture.root, 'before.json')
  write(evidence, before, { root: fixture.root })
  assert.equal(before.schema, 'winnsen.variable_trial.protection.v3')
  assert.deepEqual(Buffer.from(before.nativeTaskIndex.rawBytesBase64, 'base64'), original)
  assert.equal(before.nativeTaskIndex.sha256, hash(path))
  assert.deepEqual(before.nativeTaskIndex.semantic, value)
  let diagnostic
  assert.deepEqual(fixture.compare(read(evidence), row => { diagnostic = row }), [])
  assert.equal(diagnostic.accepted, true)
  assert.equal(diagnostic.rawHashChanged, false)
  assert.equal(diagnostic.hashChangeOnlyRebuiltAt, false)
  put({ ...value, rebuiltAt: '2026-11-13T00:01:00.001Z' })
  assert.deepEqual(fixture.compare(read(evidence), row => { diagnostic = row }), [])
  assert.equal(diagnostic.accepted, true)
  assert.equal(diagnostic.rawHashChanged, true)
  assert.equal(diagnostic.hashChangeOnlyRebuiltAt, true)
  assert.deepEqual(diagnostic.changedJsonFields, ['rebuiltAt'])
  assert.deepEqual(diagnostic.rejectionReasons, [])
  assert.deepEqual(diagnostic.taskFilesChanged, [])
  assert.equal(diagnostic.before.sha256, before.nativeTaskIndex.sha256)
  assert.equal(diagnostic.after.sha256, hash(path))
  assert.deepEqual(Buffer.from(diagnostic.after.rawBytesBase64, 'base64'), readFileSync(path))
})

test('native index rejects changed JSON content even alongside a valid rebuiltAt change', t => {
  for (const mutate of [
    value => { value.requests[0].revision++ },
    value => { value.requests[0].nativeBuild.state = 'ready' },
    value => { value.requests[0].nativeBuild.rebuiltAt = 'changed task content' },
    value => { value.requests.reverse() },
    value => { value.requests.pop() },
    value => { value.requests[0].extra = true },
  ]) {
    const fixture = nativeIndexFixture(t), before = fixture.snapshot(), changed = structuredClone(fixture.value)
    changed.rebuiltAt = '2026-11-13T00:01:00.000Z'; mutate(changed); fixture.put(changed)
    let diagnostic
    assert.deepEqual(fixture.compare(before, row => { diagnostic = row }), [fixture.path])
    assert.equal(diagnostic.accepted, false)
    assert.equal(diagnostic.hashChangeOnlyRebuiltAt, false)
    assert.ok(diagnostic.changedJsonFields.includes('requests'))
  }
})

test('native index rejects missing fields, invalid timestamps, malformed JSON and non-owner formatting', t => {
  const mutations = [
    value => { delete value.rebuiltAt }, value => { delete value.requests }, value => { delete value.schema },
    value => { value.rebuiltAt = null }, value => { value.rebuiltAt = 1 },
    value => { value.rebuiltAt = 'not a date' }, value => { value.rebuiltAt = '2026-02-30T00:00:00.000Z' },
    value => { value.rebuiltAt = '2026-11-13T00:01:00Z' },
    value => { value.rebuiltAt = '2026-11-13T08:01:00.000+08:00' },
    value => { value.schema = 'wrong' }, value => { value.requests = {} }, value => { value.extra = true },
  ]
  for (const mutate of mutations) {
    const fixture = nativeIndexFixture(t), before = fixture.snapshot(), changed = structuredClone(fixture.value)
    mutate(changed); fixture.put(changed)
    let diagnostic
    assert.deepEqual(fixture.compare(before, row => { diagnostic = row }), [fixture.path])
    assert.equal(diagnostic.accepted, false)
    assert.ok(diagnostic.rejectionReasons.some(value => value.startsWith('after_')))
  }
  for (const content of ['{invalid json', JSON.stringify(nativeIndexFixture(t).value),
    '{\n  "schema": "winnsen.native_model_request_index.v2",\n  "rebuiltAt": "invalid",\n  "rebuiltAt": "2026-11-13T00:01:00.000Z",\n  "requests": []\n}\n']) {
    const fixture = nativeIndexFixture(t), before = fixture.snapshot()
    rawFile(fixture.path, content)
    assert.deepEqual(fixture.compare(before), [fixture.path])
  }
})

test('native index additions, deletion and task-file changes remain protected', t => {
  for (const kind of ['modify', 'add', 'delete']) {
    const fixture = nativeIndexFixture(t), before = fixture.snapshot()
    fixture.put({ ...fixture.value, rebuiltAt: '2026-11-13T00:01:00.000Z' })
    const changedTask = kind === 'add' ? rawFile(join(fixture.store, 'NATIVE-added.json'), '{}') : fixture.task
    if (kind === 'modify') rawFile(fixture.task, 'changed real task bytes')
    if (kind === 'delete') rmSync(fixture.task)
    let diagnostic
    const changed = fixture.compare(before, row => { diagnostic = row })
    assert.ok(changed.includes(changedTask))
    assert.ok(changed.includes(fixture.path))
    assert.equal(diagnostic.accepted, false)
    assert.ok(diagnostic.rejectionReasons.includes('protected_task_files_changed'))
  }
  const fixture = nativeIndexFixture(t), before = fixture.snapshot()
  rmSync(fixture.path)
  assert.deepEqual(fixture.compare(before), [fixture.path])
  const absent = fixture.snapshot()
  fixture.put(fixture.value)
  assert.deepEqual(fixture.compare(absent), [fixture.path])
})

test('legacy snapshots stay strict and incomplete or tampered v3 captures cannot authorize an index change', t => {
  const fixture = nativeIndexFixture(t), before = fixture.snapshot()
  const legacy = structuredClone(before); legacy.schema = 'winnsen.variable_trial.protection.v2'; delete legacy.nativeTaskIndex
  assert.deepEqual(fixture.compare(legacy), [])
  fixture.put({ ...fixture.value, rebuiltAt: '2026-11-13T00:01:00.000Z' })
  let diagnostic
  assert.deepEqual(fixture.compare(legacy, row => { diagnostic = row }), [fixture.path])
  assert.equal(diagnostic.mode, 'legacy_strict_sha256')
  assert.equal(diagnostic.accepted, false)
  for (const mutate of [
    value => { delete value.nativeTaskIndex },
    value => { value.nativeTaskIndex.semantic.requests[0].revision++ },
    value => { value.nativeTaskIndex.sha256 = 'F'.repeat(64) },
    value => { value.nativeTaskIndex.rawBytesBase64 = Buffer.from('{}').toString('base64') },
  ]) {
    const invalid = structuredClone(before); mutate(invalid)
    assert.deepEqual(fixture.compare(invalid), [fixture.path])
  }
})

test('rebuiltAt never exempts another same-name file or a format-only index rewrite', t => {
  const fixture = nativeIndexFixture(t), other = rawFile(join(fixture.root, 'other', 'native_model_request_index.json'), JSON.stringify(fixture.value))
  const beforeOther = protectionSnapshot([{ path: other }], { root: fixture.root })
  rawFile(other, JSON.stringify({ ...fixture.value, rebuiltAt: '2026-11-13T00:01:00.000Z' }))
  assert.deepEqual(compareProtection(beforeOther, { root: fixture.root }), [other])
  const before = fixture.snapshot()
  rawFile(fixture.path, JSON.stringify(fixture.value))
  assert.deepEqual(fixture.compare(before), [fixture.path])
  fixture.put({ requests: fixture.value.requests, rebuiltAt: '2026-11-13T00:01:00.000Z', schema: fixture.value.schema })
  assert.deepEqual(fixture.compare(before), [fixture.path])
})

test('toolchain archives exact source bytes and validates exact source, executable and DLL sets', t => {
  const fixture = toolFixture(t), { manifest, dataRoot, sourceRoot } = fixture
  assert.equal(validateToolchain(manifest, { dataRoot, sourceRoot }), manifest)
  for (const item of manifest.sources) assert.deepEqual(readFileSync(item.archivePath), readFileSync(item.path))
  const archived = manifest.sources[0].archivePath
  const original = manifest.sources[0].path
  const bytes = readFileSync(archived)
  writeFileSync(original, 'new source')
  assert.deepEqual(readFileSync(archived), bytes)
  assert.throws(() => validateToolchain(manifest, { dataRoot, sourceRoot }), /BUILD_CHANGED/)
  writeFileSync(original, bytes)
  writeFileSync(manifest.dlls[0].path, 'changed DLL')
  assert.throws(() => validateToolchain(manifest, { dataRoot, sourceRoot }), /BUILD_CHANGED/)
})

test('toolchain rejects untrusted paths, archive drift, new C# sources and unmanifested DLLs', t => {
  const fixture = toolFixture(t), { manifest, dataRoot, sourceRoot, root, bin } = fixture
  assert.throws(() => validateToolchain({ ...manifest, schema: undefined }, { dataRoot, sourceRoot }), /V2_REQUIRED/)
  const outsideExe = rawFile(join(root, 'VariableTrial.exe'), 'outside')
  assert.throws(() => validateToolchain({ ...manifest, exe: outsideExe }, { dataRoot, sourceRoot }), /PATH_ESCAPE/)
  const escapedSource = structuredClone(manifest)
  escapedSource.sources[0].path = outsideExe
  assert.throws(() => validateToolchain(escapedSource, { dataRoot, sourceRoot }), /SOURCE_PATH_INVALID/)
  const missingDll = structuredClone(manifest); missingDll.dlls.pop()
  assert.throws(() => validateToolchain(missingDll, { dataRoot, sourceRoot }), /DLL_SET_INVALID/)
  const extraDll = rawFile(join(bin, 'Unmanifested.dll'), 'untrusted')
  assert.throws(() => validateToolchain(manifest, { dataRoot, sourceRoot }), /UNMANIFESTED/)
  rmSync(extraDll)
  const extraSource = rawFile(join(sourceRoot, 'Unexpected.cs'), 'new source')
  assert.throws(() => validateToolchain(manifest, { dataRoot, sourceRoot }), /SOURCE_SET_CHANGED/)
  rmSync(extraSource)
  writeFileSync(manifest.sources[0].archivePath, 'drift')
  assert.throws(() => validateToolchain(manifest, { dataRoot, sourceRoot }), /BUILD_CHANGED/)
})

test('shared lock keeps native/CAD identity and refuses token loss synchronously', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  assert.throws(() => acquireLock({ lockDirectory }), /SHARED_LOCK_BUSY/)
  lock.bindProcess({ kind: 'native', pid: nativeProcess.pid, executable: nativeProcess.executable })
  assert.equal(read(join(lockDirectory, 'owner.json')).pid, nativeProcess.pid)
  lock.bindProcess({ kind: 'cad', ...cadProcess })
  assert.equal(read(join(lockDirectory, 'owner.json')).pid, cadProcess.pid)
  lock.retain()
  const otherOwner = { ...read(join(lockDirectory, 'owner.json')), token: 'other-token', workerId: 'other-owner' }
  writeFileSync(join(lockDirectory, 'owner.json'), JSON.stringify(otherOwner))
  assert.throws(() => lock.assert(), /SHARED_LOCK_LOST/)
  assert.throws(() => lock.heartbeat(), /SHARED_LOCK_LOST/)
  assert.throws(() => lock.release(), /SHARED_LOCK_LOST/)
  assert.equal(read(join(lockDirectory, 'owner.json')).token, 'other-token')
})

for (const code of ['EPERM', 'EBUSY']) test('shared lock retries transient ' + code + ' without removing its owner', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  const originalOwner = read(ownerPath), originalHash = hash(ownerPath), stagedPaths = new Set()
  let attempts = 0
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    attempts++; stagedPaths.add(from)
    assert.equal(hash(ownerPath), originalHash)
    assert.equal(fs.lstatSync(from).nlink, 1)
    assert.equal(read(from).pid, nativeProcess.pid)
    if (attempts === 1) throw Object.assign(new Error('transient rename failure'), { code })
    return rename(from, to)
  })
  lock.bindProcess({ kind: 'native', pid: nativeProcess.pid, executable: nativeProcess.executable })
  assert.equal(attempts, 2)
  assert.equal(stagedPaths.size, 1)
  assert.equal(read(ownerPath).token, originalOwner.token)
  assert.equal(read(ownerPath).workerId, originalOwner.workerId)
  assert.equal(read(ownerPath).pid, nativeProcess.pid)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

test('permanent owner rename EPERM exhausts bounded retries and preserves the prior owner', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  const originalHash = hash(ownerPath), failure = Object.assign(new Error('permanent rename failure'), { code: 'EPERM' })
  let attempts = 0
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    attempts++; assert.equal(hash(ownerPath), originalHash)
    throw failure
  })
  const started = performance.now()
  assert.throws(() => lock.heartbeat(), error => error === failure)
  t.diagnostic('bounded rename retries elapsed ' + Math.round(performance.now() - started) + ' ms')
  assert.equal(attempts, 6)
  assert.equal(hash(ownerPath), originalHash)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

test('owner token change after transient rename failure stops before another commit', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  const otherOwner = { ...read(ownerPath), token: 'other-token', workerId: 'other-owner' }
  let attempts = 0, otherHash
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    attempts++
    writeFileSync(ownerPath, JSON.stringify(otherOwner))
    otherHash = hash(ownerPath)
    throw Object.assign(new Error('transient rename failure'), { code: 'EPERM' })
  })
  assert.throws(() => lock.heartbeat(), /SHARED_LOCK_LOST/)
  assert.equal(attempts, 1)
  assert.equal(hash(ownerPath), otherHash)
  assert.deepEqual(read(ownerPath), otherOwner)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

test('retry rejects a newly linked owner instead of replacing either name', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json'), linkedOwner = join(root, 'linked-owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  const originalHash = hash(ownerPath)
  let attempts = 0
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    attempts++; fs.linkSync(ownerPath, linkedOwner)
    throw Object.assign(new Error('transient rename failure'), { code: 'EBUSY' })
  })
  assert.throws(() => lock.heartbeat(), /SHARED_OUTPUT_FILE/)
  assert.equal(attempts, 1)
  assert.equal(hash(ownerPath), originalHash)
  assert.equal(hash(linkedOwner), originalHash)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

test('rename retry does not acquire or overwrite an existing old lock', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  rawFile(ownerPath, JSON.stringify({ workerId: 'old-other-owner', token: 'old-other-token', heartbeatAt: '2000-01-01T00:00:00.000Z' }))
  const originalHash = hash(ownerPath)
  let attempts = 0
  mockRename(t, (_from, _to, _rename) => { attempts++; assert.fail('existing lock must not reach rename') })
  assert.throws(() => acquireLock({ lockDirectory }), /SHARED_LOCK_BUSY/)
  assert.equal(attempts, 0)
  assert.equal(hash(ownerPath), originalHash)
  assert.deepEqual(readdirSync(dirname(lockDirectory)), ['.native-worker-instance.lock'])
})

test('owner rename errors outside EPERM and EBUSY remain immediate failures', t => {
  const root = temporary(t), lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 })
  t.after(() => { try { lock.release() } catch {} })
  const originalHash = hash(ownerPath), failure = Object.assign(new Error('access denied'), { code: 'EACCES' })
  let attempts = 0
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    attempts++; throw failure
  })
  assert.throws(() => lock.heartbeat(), error => error === failure)
  assert.equal(attempts, 1)
  assert.equal(hash(ownerPath), originalHash)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

test('supervision keeps heartbeat through native exit, CAD residency and temporary query failure', async () => {
  const lock = fakeLock(), queryError = new Error('CIM temporarily unavailable')
  let step = 0, stopped = 0
  const results = [[nativeProcess, cadProcess], queryError, [cadProcess], []]
  const observations = []
  const result = await superviseCadSession({
    lock,
    observeNative: () => ({ pid: nativeProcess.pid, ended: step > 0, cadClaim, executable: nativeProcess.executable }),
    queryProcesses: () => { const value = results[step]; if (value instanceof Error) throw value; return value },
    wait: async () => { step++ },
    onObservation: value => observations.push(value.state),
    requestStop: () => stopped++,
  })
  assert.equal(result.error, null)
  assert.deepEqual(result.secondaryErrors, [])
  assert.equal(result.warnings.length, 1)
  assert.equal(result.warnings[0].error, queryError.message)
  assert.equal(result.warnings[0].attempts, 1)
  assert.ok(result.warnings[0].recoveredAt)
  assert.equal(stopped, 0)
  assert.equal(result.cadExited, true)
  assert.equal(step, 3)
  assert.ok(lock.heartbeats >= 4)
  assert.ok(lock.owners.some(owner => owner.kind === 'cad' && owner.pid === cadProcess.pid))
  assert.equal(lock.owners.filter(owner => owner.kind === 'cad').length, 2)
  assert.equal(lock.owners.some(owner => owner.reason === 'process_query_unavailable'), false)
  assert.deepEqual(observations, ['waiting_for_cad_exit', 'process_query_unavailable', 'process_query_recovered', 'waiting_for_cad_exit', 'cad_exited'])
  assert.equal(lock.released, false)
})

test('query failure before CAD identity confirmation binds the still-live supervisor', async () => {
  const lock = fakeLock(), queryFailure = new Error('identity query unavailable')
  let step = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ ended: true }),
    queryProcesses: () => { if (!step) throw queryFailure; return [] },
    wait: async () => { step++ },
  })
  assert.equal(result.error, null)
  assert.equal(result.warnings[0].error, queryFailure.message)
  assert.ok(result.warnings[0].recoveredAt)
  assert.ok(lock.owners.some(owner => owner.kind === 'runner' && owner.pid === process.pid && owner.reason === 'process_query_unavailable'))
})

test('continuous query unavailability keeps the lock and heartbeat until querying recovers', async () => {
  const lock = fakeLock(), observations = []
  let step = 0, stopped = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ ended: true }),
    queryProcesses: () => { if (step < 4) throw new Error('PROCESS_IDENTITY_UNAVAILABLE ' + step); return [] },
    wait: async () => {
      assert.equal(lock.released, false)
      assert.equal(lock.owners.at(-1).pid, process.pid)
      assert.equal(lock.heartbeats, step + 1)
      assert.equal(observations.at(-1).warnings[0].recoveredAt, null)
      step++
    },
    onObservation: value => observations.push(value), requestStop: () => stopped++,
  })
  assert.equal(result.error, null)
  assert.equal(step, 4)
  assert.equal(lock.heartbeats, 5)
  assert.equal(lock.released, false)
  assert.equal(stopped, 0)
  assert.equal(result.warnings.length, 1)
  assert.equal(result.warnings[0].attempts, 4)
  assert.equal(result.warnings[0].lastError, 'PROCESS_IDENTITY_UNAVAILABLE 3')
  assert.ok(result.warnings[0].recoveredAt)
  assert.deepEqual(observations.slice(0, 4).map(value => value.warnings[0].attempts), [1, 2, 3, 4])
  assert.equal(observations.at(-1).state, 'cad_exited')
  assert.deepEqual(observations.at(-1).warnings, result.warnings)
})

test('supervision preserves native failure when cleanup/query/token checks also fail', async () => {
  const lock = fakeLock(), nativeFailure = new Error('original native failure'), cleanupFailure = new Error('CIM error')
  let step = 0, stopped = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ ended: true, error: nativeFailure }),
    queryProcesses: () => { if (!step) throw cleanupFailure; return [] },
    wait: async () => { step++; lock.failure = new Error('SHARED_LOCK_LOST') },
    requestStop: () => stopped++,
  })
  assert.equal(result.error, nativeFailure)
  assert.equal(result.secondaryErrors.includes(cleanupFailure), false)
  assert.equal(result.warnings[0].error, cleanupFailure.message)
  assert.ok(result.warnings[0].recoveredAt)
  assert.ok(result.secondaryErrors.some(error => /LOCK_LOST/.test(error.message)))
  assert.equal(stopped, 1)
})

test('query warnings cannot hide a later native failure, cancellation or lost lock', async () => {
  for (const kind of ['native', 'cancel', 'lock']) {
    const lock = fakeLock(), cancellation = new AbortController(), failure = new Error(kind === 'lock' ? 'SHARED_LOCK_LOST' : 'later native failure')
    let step = 0
    const result = await superviseCadSession({
      lock, signal: cancellation.signal,
      observeNative: () => ({ ended: true, error: kind === 'native' && step ? failure : null }),
      queryProcesses: () => { if (!step) throw new Error('transient query failure'); return [] },
      wait: async () => {
        step++
        if (kind === 'cancel') cancellation.abort()
        if (kind === 'lock') lock.failure = failure
      },
    })
    if (kind === 'cancel') assert.equal(result.error.message, 'TRIAL_INTERRUPTED')
    else assert.equal(result.error, failure)
    assert.equal(result.warnings.length, 1)
    assert.ok(result.warnings[0].recoveredAt)
    assert.deepEqual(result.secondaryErrors, [])
  }
})

test('observation persistence failures remain fatal while process querying recovers', async () => {
  const lock = fakeLock(), failure = new Error('supervision evidence write failed')
  let step = 0, stopped = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ ended: true }),
    queryProcesses: () => { if (!step) throw new Error('transient query failure'); return [] },
    wait: async () => { step++ }, requestStop: () => stopped++,
    onObservation: () => { if (!step) throw failure },
  })
  assert.equal(result.error, failure)
  assert.equal(stopped, 1)
  assert.ok(result.warnings[0].recoveredAt)
})

test('supervision rejects a reused CAD PID and waits for all CAD processes to exit', async () => {
  const lock = fakeLock()
  let step = 0, stopped = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ ended: true, cadClaim }),
    queryProcesses: () => step === 0 ? [{ ...cadProcess, startedAtUtc: '2026-11-12T01:00:00Z' }] : [],
    wait: async () => { step++ }, requestStop: () => stopped++,
  })
  assert.match(result.error.message, /IDENTITY_MISMATCH/)
  assert.deepEqual(JSON.parse(result.error.message.slice('CAD_PROCESS_IDENTITY_MISMATCH '.length)), {
    claim: cadClaim, observed: { ...cadProcess, startedAtUtc: '2026-11-12T01:00:00Z' },
  })
  assert.equal(stopped, 1)
  assert.equal(step, 1)
  assert.equal(lock.owners.some(owner => owner.kind === 'cad'), false)
})

test('supervision records confirmed CAD PID reuse only after clean native exit and still waits for the monitor', async t => {
  const reused = { ...cadProcess, name: 'git.exe', startedAtUtc: '2026-11-12T01:00:00.0000000Z', executable: rawFile(join(temporary(t), 'git.exe'), 'offline process fixture; never execute') }
  const monitor = { ...cadProcess, name: 'sldProcMon.exe', pid: 1003 }, lock = fakeLock(), observations = []
  let step = 0, stopped = 0
  const result = await superviseCadSession({
    lock, observeNative: () => ({ pid: nativeProcess.pid, ended: step > 0, code: step ? 0 : null, error: null, cadClaim }),
    queryProcesses: () => [[nativeProcess, cadProcess], [monitor, reused], [reused]][step],
    wait: async () => { step++; assert.ok(step < 3) }, requestStop: () => stopped++,
    onObservation: value => observations.push(value),
  })
  assert.equal(result.error, null)
  assert.deepEqual(result.secondaryErrors, [])
  assert.equal(stopped, 0)
  assert.equal(step, 2)
  assert.equal(result.warnings.length, 1)
  const warning = result.warnings[0]
  assert.equal(warning.code, 'CAD_PID_REUSED_AFTER_NATIVE_EXIT')
  assert.deepEqual(warning.claim, cadClaim)
  assert.deepEqual(warning.observed, reused)
  assert.deepEqual(warning.previousConfirmation.claim, cadClaim)
  assert.deepEqual(warning.previousConfirmation.observed, { ...cadProcess, kind: 'cad' })
  assert.ok(Number.isFinite(Date.parse(warning.previousConfirmation.confirmedAt)))
  assert.deepEqual(warning.native, { ended: true, code: 0, error: null, signal: null })
  assert.notEqual(warning.canonicalExecutables.claimed, warning.canonicalExecutables.observed)
  assert.deepEqual(observations.map(value => value.state), ['waiting_for_cad_exit', 'waiting_for_cad_exit', 'cad_exited'])
  assert.deepEqual(observations.at(-1).cad, [])
  assert.deepEqual(observations.at(-1).warnings, result.warnings)
  assert.deepEqual(lock.owners.slice(1).map(owner => owner.kind), ['runner', 'runner'])
  assert.equal(lock.owners.some(owner => owner.executable === reused.executable), false)
  assert.equal(lock.released, false)
})

test('supervision keeps every unproven or unsafe CAD PID mismatch fatal', async t => {
  const root = temporary(t), reused = { ...cadProcess, name: 'git.exe', startedAtUtc: '2026-11-12T01:00:00.0000000Z', executable: rawFile(join(root, 'git.exe')) }
  const cases = [
    { name: 'claim_without_prior_query_confirmation', prior: false },
    { name: 'native_not_ended', native: { ended: false } },
    { name: 'native_nonzero_exit', native: { code: 1 } },
    { name: 'native_missing_exit_code', native: { code: null } },
    { name: 'native_string_exit_code', native: { code: '0' } },
    { name: 'native_error', native: { error: new Error('native failure') } },
    { name: 'native_termination_signal', native: { signal: 'SIGTERM' } },
    { name: 'missing_start_time', observed: { startedAtUtc: undefined } },
    { name: 'invalid_start_time', observed: { startedAtUtc: 'unknown' } },
    { name: 'earlier_start_time', observed: { startedAtUtc: nativeProcess.startedAtUtc } },
    { name: 'equal_start_time', observed: { startedAtUtc: cadProcess.startedAtUtc } },
    { name: 'start_time_not_provably_later_at_parser_precision', observed: { startedAtUtc: '2026-11-12T00:00:01.1234568Z' } },
    { name: 'same_CAD_executable', observed: { executable: cadProcess.executable } },
    { name: 'new_SLDWORKS_process', observed: { name: 'SLDWORKS.exe' } },
    { name: 'new_sldProcMon_process', observed: { name: 'sldProcMon.exe' } },
    { name: 'CAD_executable_disguised_by_name', observed: { executable: rawFile(join(root, 'SLDWORKS.exe')) } },
    { name: 'CAD_monitor_executable_disguised_by_name', observed: { executable: rawFile(join(root, 'sldProcMon.exe')) } },
    { name: 'unknown_executable', observed: { executable: join(root, 'missing.exe') } },
    { name: 'missing_process_name', observed: { name: '' } },
    { name: 'claim_changed_since_confirmation', claim: { ...cadClaim, startedAtUtc: '2026-11-12T00:00:02.0000000Z' } },
  ]
  for (const scenario of cases) await t.test(scenario.name, async () => {
    const lock = fakeLock(), observed = { ...reused, ...scenario.observed }
    let step = scenario.prior === false ? 1 : 0, stopped = 0
    const result = await superviseCadSession({
      lock, observeNative: () => step === 0 ? { pid: nativeProcess.pid, ended: false, cadClaim } :
        { ended: true, code: 0, error: null, cadClaim: scenario.claim || cadClaim, ...(step === 1 ? scenario.native : {}) },
      queryProcesses: () => step === 0 ? [cadProcess] : step === 1 ? [observed] : [],
      wait: async () => { step++; assert.ok(step <= 2) }, requestStop: () => stopped++,
    })
    assert.ok([result.error, ...result.secondaryErrors].some(error => /CAD_PROCESS_IDENTITY_MISMATCH/.test(error?.message)))
    if (scenario.native?.error) assert.equal(result.error, scenario.native.error)
    assert.ok(stopped > 0)
    assert.equal(result.warnings.some(warning => warning.code === 'CAD_PID_REUSED_AFTER_NATIVE_EXIT'), false)
    assert.equal(lock.owners.filter(owner => owner.kind === 'cad').length, scenario.prior === false ? 0 : 1)
    assert.equal(lock.owners.some(owner => owner.executable === reused.executable), false)
  })
})

test('PID reuse cannot supply a missing CAD confirmation while querying recovers', async t => {
  for (const prior of [true, false]) await t.test(prior ? 'query_failure_after_confirmation' : 'query_failure_before_confirmation', async t => {
    const reused = { ...cadProcess, name: 'git.exe', startedAtUtc: '2026-11-12T01:00:00.0000000Z', executable: rawFile(join(temporary(t), 'git.exe')) }
    const lock = fakeLock(), queryFailure = new Error('temporary process query failure')
    let step = prior ? 0 : 1, stopped = 0
    const result = await superviseCadSession({
      lock, observeNative: () => ({ pid: nativeProcess.pid, ended: step > 0, code: step ? 0 : null, error: null, cadClaim }),
      queryProcesses: () => { if (step === 1) throw queryFailure; return step === 0 ? [cadProcess] : [reused] },
      wait: async () => { step++; assert.ok(step <= 2) }, requestStop: () => stopped++,
    })
    assert.equal(result.warnings[0].code, 'PROCESS_QUERY_UNAVAILABLE')
    assert.ok(result.warnings[0].recoveredAt)
    if (prior) {
      assert.equal(result.error, null)
      assert.equal(stopped, 0)
      assert.equal(result.warnings[1].code, 'CAD_PID_REUSED_AFTER_NATIVE_EXIT')
    } else {
      assert.match(result.error.message, /CAD_PROCESS_IDENTITY_MISMATCH/)
      assert.equal(stopped, 1)
      assert.equal(result.warnings.length, 1)
    }
    assert.equal(lock.owners.some(owner => owner.executable === reused.executable), false)
    assert.equal(lock.released, false)
  })
})

test('confirmed PID reuse cannot hide cancellation, lost lock or evidence persistence failure', async t => {
  for (const kind of ['cancel', 'lock', 'evidence']) await t.test(kind, async t => {
    const reused = { ...cadProcess, name: 'git.exe', startedAtUtc: '2026-11-12T01:00:00.0000000Z', executable: rawFile(join(temporary(t), 'git.exe')) }
    const lock = fakeLock(), cancellation = new AbortController(), failure = new Error(kind === 'lock' ? 'SHARED_LOCK_LOST' : 'supervision evidence write failed')
    let step = 0, stopped = 0
    const result = await superviseCadSession({
      lock, signal: cancellation.signal,
      observeNative: () => ({ ended: step > 0, code: step ? 0 : null, error: null, cadClaim }),
      queryProcesses: () => step === 0 ? [cadProcess] : [reused],
      wait: async () => {
        step++; assert.equal(step, 1)
        if (kind === 'cancel') cancellation.abort()
        if (kind === 'lock') lock.failure = failure
      },
      onObservation: () => { if (kind === 'evidence' && !step) throw failure }, requestStop: () => stopped++,
    })
    if (kind === 'cancel') assert.equal(result.error.message, 'TRIAL_INTERRUPTED')
    else assert.equal(result.error, failure)
    assert.ok(result.secondaryErrors.some(error => /CAD_PROCESS_IDENTITY_MISMATCH/.test(error.message)))
    assert.ok(stopped > 0)
    assert.equal(result.warnings.some(warning => warning.code === 'CAD_PID_REUSED_AFTER_NATIVE_EXIT'), false)
    assert.equal(lock.owners.some(owner => owner.executable === reused.executable), false)
    assert.equal(lock.released, false)
  })
})

test('prior trial pointers are confined to a direct run child and its cad/result paths', t => {
  const root = temporary(t), dataRoot = join(root, 'data'), dir = join(dataRoot, 'runs', 'prior')
  directory(join(dir, 'cad'), dataRoot)
  const pointer = { dir, cadDirectory: join(dir, 'cad'), nativeResult: join(dir, 'native-result.json') }
  assert.equal(trialSource(pointer, { dataRoot }), pointer.cadDirectory)
  assert.throws(() => trialSource({ ...pointer, cadDirectory: root }, { dataRoot }), /POINTER_INVALID/)
  assert.throws(() => trialSource({ ...pointer, dir: root }, { dataRoot }), /PATH_ESCAPE/)
})

test('native runner uses a validated fake toolchain and supervises after its child exits', async t => {
  const fixture = toolFixture(t), { dataRoot, sourceRoot } = fixture
  const dir = directory(join(dataRoot, 'runs', 'fake-native'), dataRoot, true)
  directory(join(dir, 'cad'), dataRoot)
  const lock = fakeLock(), child = new EventEmitter()
  child.pid = nativeProcess.pid; child.stdout = new PassThrough(); child.stderr = new PassThrough(); child.kill = () => true
  let step = 0, spawned = 0
  const result = await runNativeJob({ action: 'fixture-only', cadDirectory: join(dir, 'cad') }, dir, {
    lock, dataRoot, sourceRoot,
    spawnChild(exe, args) {
      spawned++; assert.equal(exe, fixture.manifest.exe)
      assert.equal(read(args[0]).cadDirectory, join(dir, 'cad'))
      assert.equal(read(args[0]).toolchainPath, join(dir, 'toolchain.json'))
      assert.deepEqual(read(join(dir, 'toolchain.json')), fixture.manifest)
      return child
    },
    queryProcesses: () => {
      if (step === 2) throw new Error('PROCESS_IDENTITY_UNAVAILABLE')
      return step === 0 ? [nativeProcess] : step === 1 ? [cadProcess] : []
    },
    wait: async () => {
      step++
      if (step === 1) {
        child.stdout.write(JSON.stringify(cadClaim) + '\n')
        write(join(dir, 'native-result.json'), { production_release: false, events: [{ inspection: 'finished', success: true, cadExited: true }] }, { root: dataRoot })
        child.emit('close', 0, null)
      }
    },
  })
  assert.equal(spawned, 1)
  assert.equal(step, 3)
  assert.equal(result.code, 0)
  assert.equal(result.warnings[0].error, 'PROCESS_IDENTITY_UNAVAILABLE')
  assert.ok(result.warnings[0].recoveredAt)
  assert.equal(existsSync(join(dir, 'cancel.json')), false)
  const supervision = read(join(dir, 'supervision.json'))
  assert.equal(supervision.state, 'cad_exited')
  assert.deepEqual(supervision.warnings, result.warnings)
  assert.ok(lock.owners.some(owner => owner.pid === nativeProcess.pid && owner.kind === 'native'))
  assert.ok(lock.owners.some(owner => owner.pid === cadProcess.pid && owner.kind === 'cad'))
  assert.equal(read(join(dir, 'job.json')).resultPath, join(dir, 'native-result.json'))
})

test('native runner persists PID reuse evidence and retains its native completion gates', async t => {
  const cases = [
    { name: 'clean_native_completion' },
    { name: 'production_release_true', result: { production_release: true } },
    { name: 'missing_finished_event', events: [] },
    { name: 'finished_success_false', finished: { success: false } },
    { name: 'finished_cadExited_false', finished: { cadExited: false } },
  ]
  for (const scenario of cases) await t.test(scenario.name, async t => {
    const fixture = toolFixture(t), { dataRoot, sourceRoot } = fixture
    const dir = directory(join(dataRoot, 'runs', 'pid-reuse-native'), dataRoot, true)
    directory(join(dir, 'cad'), dataRoot)
    const reused = { ...cadProcess, name: 'git.exe', startedAtUtc: '2026-11-12T01:00:00.0000000Z', executable: rawFile(join(fixture.root, 'git.exe')) }
    const monitor = { ...cadProcess, name: 'sldProcMon.exe', pid: 1003 }, lock = fakeLock(), child = new EventEmitter()
    child.pid = nativeProcess.pid; child.stdout = new PassThrough(); child.stderr = new PassThrough()
    child.kill = () => assert.fail('native, CAD and reused processes must never be killed')
    let step = 0
    const execution = runNativeJob({ action: 'fixture-only', cadDirectory: join(dir, 'cad') }, dir, {
      lock, dataRoot, sourceRoot, spawnChild: () => child,
      queryProcesses: () => [[nativeProcess], [cadProcess], [monitor, reused], [reused]][step],
      wait: async () => {
        step++; assert.ok(step <= 3)
        if (step === 1) child.stdout.write(JSON.stringify(cadClaim) + '\n')
        if (step === 2) {
          const events = scenario.events || [{ inspection: 'finished', success: true, cadExited: true, ...scenario.finished }]
          write(join(dir, 'native-result.json'), { production_release: false, events, ...scenario.result }, { root: dataRoot })
          child.emit('close', 0, null)
        }
      },
    })
    if (scenario.name === 'clean_native_completion') {
      const result = await execution
      assert.equal(result.code, 0)
      assert.equal(result.cadExited, true)
      assert.deepEqual(result.warnings, read(join(dir, 'supervision.json')).warnings)
    } else await assert.rejects(execution, /NATIVE_COMPLETION_EVIDENCE_INVALID/)
    const supervision = read(join(dir, 'supervision.json'))
    assert.equal(step, 3)
    assert.equal(supervision.state, 'cad_exited')
    assert.equal(supervision.nativeEnded, true)
    assert.deepEqual(supervision.cad, [])
    assert.equal(supervision.warnings.length, 1)
    assert.equal(supervision.warnings[0].code, 'CAD_PID_REUSED_AFTER_NATIVE_EXIT')
    assert.equal(existsSync(join(dir, 'cancel.json')), false)
    assert.equal(lock.owners.some(owner => owner.executable === reused.executable), false)
  })
})

test('native runner rejects DLL tampering before spawn and cannot escape its run directory', async t => {
  const fixture = toolFixture(t), { dataRoot, sourceRoot, manifest } = fixture
  const dir = directory(join(dataRoot, 'runs', 'fake-native'), dataRoot, true)
  directory(join(dir, 'cad'), dataRoot)
  let spawned = 0
  const options = { lock: fakeLock(), dataRoot, sourceRoot, spawnChild: () => { spawned++; throw new Error('must not execute') } }
  await assert.rejects(runNativeJob({ cadDirectory: fixture.root }, dir, options), /PATH_ESCAPE/)
  writeFileSync(manifest.dlls[0].path, 'tampered')
  await assert.rejects(runNativeJob({ cadDirectory: join(dir, 'cad') }, dir, options), /BUILD_CHANGED/)
  assert.equal(spawned, 0)
  assert.equal(existsSync(join(dir, 'job.json')), false)
})

test('explicit diagnostic toolchain stays isolated from the current pointer and rejects tampering before spawn', async t => {
  const fixture = toolFixture(t), { dataRoot, sourceRoot, manifest } = fixture
  const pointer = join(dataRoot, 'toolchain-current.json'), pointerBefore = hash(pointer)
  const dir = directory(join(dataRoot, 'runs', 'explicit-diagnostic'), dataRoot, true)
  directory(join(dir, 'cad'), dataRoot)
  let spawned = 0
  const options = { lock: fakeLock(), dataRoot, sourceRoot, toolchainManifest: manifest,
    spawnChild(exe, args) {
      spawned++; assert.equal(exe, manifest.exe)
      assert.deepEqual(read(join(dir, 'toolchain.json')), manifest)
      assert.equal(read(args[0]).toolchainPath, join(dir, 'toolchain.json'))
      throw new Error('EXPLICIT_FIXTURE_NO_EXECUTION')
    }, queryProcesses: () => [], wait: async () => {} }
  await assert.rejects(runNativeJob({ cadDirectory: join(dir, 'cad') }, dir, options), /EXPLICIT_FIXTURE_NO_EXECUTION/)
  assert.equal(spawned, 1)
  assert.equal(hash(pointer), pointerBefore)
  const invalidDir = directory(join(dataRoot, 'runs', 'explicit-tampered'), dataRoot, true)
  directory(join(invalidDir, 'cad'), dataRoot)
  writeFileSync(manifest.dlls[0].path, 'tampered')
  await assert.rejects(runNativeJob({ cadDirectory: join(invalidDir, 'cad') }, invalidDir, options), /BUILD_CHANGED/)
  assert.equal(spawned, 1)
  assert.equal(existsSync(join(invalidDir, 'job.json')), false)
  assert.equal(hash(pointer), pointerBefore)
})

test('native cancellation writes the agreed marker and preserves its failure while CAD cleans up', async t => {
  const fixture = toolFixture(t), { dataRoot, sourceRoot } = fixture
  const dir = directory(join(dataRoot, 'runs', 'cancelled-native'), dataRoot, true)
  directory(join(dir, 'cad'), dataRoot)
  const lock = fakeLock(), child = new EventEmitter(), cancellation = new AbortController()
  child.pid = nativeProcess.pid; child.stdout = new PassThrough(); child.stderr = new PassThrough()
  child.kill = () => assert.fail('native or CAD processes must never be forcefully killed')
  let step = 0
  await assert.rejects(runNativeJob({ action: 'fixture-only', cadDirectory: join(dir, 'cad'), cancellationPath: 'untrusted override' }, dir, {
    lock, dataRoot, sourceRoot, signal: cancellation.signal,
    spawnChild: () => child,
    queryProcesses: () => step === 0 ? [nativeProcess] : step < 3 ? [cadProcess] : [],
    wait: async () => {
      step++
      if (step === 1) {
        child.stdout.write(JSON.stringify(cadClaim) + '\n')
        cancellation.abort()
        assert.equal(read(join(dir, 'cancel.json')).reason, 'TRIAL_INTERRUPTED')
      }
      if (step === 2) child.emit('close', 1, null)
    },
  }), error => error.message === 'TRIAL_INTERRUPTED' && error.cadSupervised === true)
  assert.equal(step, 3)
  const job = read(join(dir, 'job.json'))
  assert.equal(job.cancellationPath, join(dir, 'cancel.json'))
  assert.equal(job.sharedLockDirectory, lock.lockDir)
  assert.equal(job.sharedWorkerId, lock.owner.workerId)
  assert.equal(job.sharedToken, lock.owner.token)
  assert.ok(lock.owners.some(owner => owner.kind === 'cad'))
})

test('exhausted owner rename retries still cancel the native runner through its agreed marker', async t => {
  const fixture = toolFixture(t), { root, dataRoot, sourceRoot } = fixture
  const dir = directory(join(dataRoot, 'runs', 'owner-rename-failure'), dataRoot, true)
  directory(join(dir, 'cad'), dataRoot)
  const lockDirectory = join(root, 'coordination', '.native-worker-instance.lock'), ownerPath = join(lockDirectory, 'owner.json')
  const lock = acquireLock({ lockDirectory, heartbeatMs: 60000 }), child = new EventEmitter()
  t.after(() => { try { lock.release() } catch {} })
  child.pid = nativeProcess.pid; child.stdout = new PassThrough(); child.stderr = new PassThrough()
  child.kill = () => assert.fail('native or CAD processes must never be forcefully killed')
  const failure = Object.assign(new Error('persistent owner rename EPERM'), { code: 'EPERM' })
  let bound = false, failures = 0, step = 0, boundHash
  mockRename(t, (from, to, rename) => {
    if (to !== ownerPath) return rename(from, to)
    if (!bound) { rename(from, to); bound = true; boundHash = hash(ownerPath); return }
    if (failures < 6) {
      failures++; assert.equal(hash(ownerPath), boundHash)
      throw failure
    }
    return rename(from, to)
  })
  await assert.rejects(runNativeJob({ action: 'fixture-only', cadDirectory: join(dir, 'cad') }, dir, {
    lock, dataRoot, sourceRoot, spawnChild: () => child,
    queryProcesses: () => step ? [] : [nativeProcess],
    wait: async () => {
      assert.equal(read(join(dir, 'cancel.json')).reason, failure.message)
      assert.equal(failures, 6)
      step++; child.emit('close', 1, null)
    },
  }), error => error === failure && error.cadSupervised === true)
  assert.equal(step, 1)
  assert.equal(failures, 6)
  assert.equal(read(join(dir, 'cancel.json')).reason, failure.message)
  assert.equal(read(join(dir, 'supervision.json')).state, 'cad_exited')
  assert.equal(read(ownerPath).token, lock.owner.token)
  assert.deepEqual(readdirSync(lockDirectory), ['owner.json'])
})

function phaseFixture(t) {
  const root = temporary(t), dataRoot = join(root, 'data', 'trial'), seed = join(root, 'seed'), lock = fakeLock()
  rawFile(join(seed, 'panel.SLDPRT'), 'protected seed bytes')
  mkdirSync(join(root, 'tools'), { recursive: true })
  mkdirSync(join(root, 'data', 'master_v2', 'reports'), { recursive: true })
  rawFile(join(root, 'docs', 'PLAN_方案A+_工艺内建参数化母版_20261112.md'), 'protected plan fixture')
  mkdirSync(join(root, 'data', 'native_model_requests'), { recursive: true })
  let acquired = false, idleChecks = 0
  return {
    root, dataRoot, seed, lock,
    options: { root, dataRoot, seed,
      checkIdle() { idleChecks++; if (idleChecks === 2) assert.equal(acquired, true) },
      lockFactory() { acquired = true; return lock },
      queryProcesses: () => [], wait: async () => {},
    },
  }
}

test('protected phase locks before snapshots, overrides CAD source, checks before release and promotes only success', async t => {
  const fixture = phaseFixture(t), { dataRoot, seed, lock } = fixture
  let observedDirectory
  const release = lock.release.bind(lock)
  lock.release = () => {
    assert.equal(read(join(observedDirectory, 'protection-result.json')).pass, true)
    assert.equal(lock.released, false)
    release()
  }
  const result = await runProtectedPhase({ tag: 'fixture', sourceDirectory: seed, job: { action: 'fixture-only', sourceCadDirectory: 'untrusted override', cadDirectory: 'untrusted output' }, currentFile: 'fixture-current.json' }, {
    ...fixture.options,
    executeNative: async (job, dir) => {
      observedDirectory = dir
      assert.equal(lock.released, false)
      assert.equal(job.sourceCadDirectory, seed)
      assert.equal(job.cadDirectory, join(dir, 'cad'))
      assert.equal(readFileSync(join(job.cadDirectory, 'panel.SLDPRT'), 'utf8'), 'protected seed bytes')
      assert.equal(read(join(dir, 'copy-manifest.json')).files.length, 1)
      assert.ok(read(join(dir, 'protected-before.json')).scopes.some(scope => scope.path === seed))
      return { code: 0, cadExited: true }
    },
  })
  assert.equal(result.pass, true)
  assert.equal(lock.released, true)
  const protection = read(join(result.dir, 'protection-result.json'))
  assert.deepEqual(protection.changed, [])
  assert.equal(protection.pass, true)
  assert.equal(protection.nativeTaskIndex.rawHashChanged, false)
  assert.equal(protection.nativeTaskIndex.hashChangeOnlyRebuiltAt, false)
  assert.equal(read(protection.nativeTaskIndex.diagnosticPath).mode, 'index_not_present')
  assert.equal(read(join(dataRoot, 'fixture-current.json')).dir, result.dir)
})

test('protected phase keeps the original native exception when protection detects a newly created formal task', async t => {
  const fixture = phaseFixture(t), { root, dataRoot, seed, lock } = fixture
  const original = new Error('native original failure')
  let runDirectory, queryAttempts = 0
  await assert.rejects(runProtectedPhase({ tag: 'failed', sourceDirectory: seed, job: { action: 'fixture-only' }, currentFile: 'failed-current.json' }, {
    ...fixture.options,
    queryProcesses: () => { if (!queryAttempts++) throw new Error('cleanup query temporarily unavailable'); return [] },
    executeNative: async (_job, dir) => {
      runDirectory = dir
      rawFile(join(root, 'data', 'native_model_requests', 'PARAMETRIC-added.json'), '{}')
      throw original
    },
  }), error => error === original && error.secondaryErrors.some(item => /PROTECTED_FILES_CHANGED/.test(item.message)))
  assert.equal(read(join(runDirectory, 'protection-result.json')).pass, false)
  assert.equal(existsSync(join(dataRoot, 'failed-current.json')), false)
  assert.equal(existsSync(join(runDirectory, 'result.json')), false)
  const supervision = read(join(runDirectory, 'supervision.json'))
  assert.equal(supervision.state, 'cad_exited')
  assert.equal(supervision.warnings[0].error, 'cleanup query temporarily unavailable')
  assert.ok(supervision.warnings[0].recoveredAt)
  assert.equal(original.secondaryErrors.some(error => /query temporarily unavailable/.test(error.message)), false)
  assert.equal(lock.released, true)
})

test('protected phase archives index rebuiltAt diagnostics without changing old failure evidence', async t => {
  const fixture = phaseFixture(t), { root, dataRoot, seed } = fixture
  const indexPath = join(root, 'data', 'native_model_requests', 'native_model_request_index.json')
  const initial = { schema: 'winnsen.native_model_request_index.v2', rebuiltAt: '2026-11-13T00:00:00.000Z', requests: [] }
  rawFile(indexPath, JSON.stringify(initial, null, 2) + '\n')
  const historical = rawFile(join(dataRoot, 'runs', 'old-failed', 'protection-result.json'), '{"pass":false,"changed":["historical-index"]}\n')
  const historicalHash = hash(historical)
  const result = await runProtectedPhase({ tag: 'index-rebuild', sourceDirectory: seed, job: { action: 'fixture-only' } }, {
    ...fixture.options,
    executeNative: async () => {
      rawFile(indexPath, JSON.stringify({ ...initial, rebuiltAt: '2026-11-13T00:01:00.001Z' }, null, 2) + '\n')
      return { code: 0, cadExited: true }
    },
  })
  const protection = read(join(result.dir, 'protection-result.json')), diagnostic = read(protection.nativeTaskIndex.diagnosticPath)
  assert.equal(protection.pass, true)
  assert.equal(protection.nativeTaskIndex.accepted, true)
  assert.equal(protection.nativeTaskIndex.rawHashChanged, true)
  assert.equal(protection.nativeTaskIndex.hashChangeOnlyRebuiltAt, true)
  assert.deepEqual(protection.nativeTaskIndex.changedJsonFields, ['rebuiltAt'])
  assert.equal(JSON.stringify(protection).includes('rawBytesBase64'), false)
  assert.notEqual(diagnostic.before.sha256, diagnostic.after.sha256)
  assert.equal(diagnostic.accepted, true)
  assert.equal(diagnostic.before.rawBytesBase64, read(join(result.dir, 'protected-before.json')).nativeTaskIndex.rawBytesBase64)
  assert.equal(hash(historical), historicalHash)
})

test('protected phase rejects invalid index before native work and persists failed index diagnostics', async t => {
  const fixture = phaseFixture(t), { root, dataRoot, seed } = fixture
  rawFile(join(root, 'data', 'native_model_requests', 'native_model_request_index.json'),
    JSON.stringify({ schema: 'winnsen.native_model_request_index.v2', requests: [] }, null, 2) + '\n')
  let nativeCalls = 0
  await assert.rejects(runProtectedPhase({ tag: 'invalid-index', sourceDirectory: seed, job: { action: 'fixture-only' } }, {
    ...fixture.options, executeNative: async () => { nativeCalls++; return { code: 0 } },
  }), /PROTECTED_NATIVE_INDEX_INVALID/)
  assert.equal(nativeCalls, 0)
  const run = join(dataRoot, 'runs', readdirSync(join(dataRoot, 'runs'))[0])
  assert.equal(read(join(run, 'protection-result.json')).pass, false)
  assert.equal(read(join(run, 'native-index-protection.json')).accepted, false)
  assert.equal(existsSync(join(run, 'result.json')), false)
})
