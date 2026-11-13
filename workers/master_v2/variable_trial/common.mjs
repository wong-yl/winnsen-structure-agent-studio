import { readFileSync, writeFileSync, mkdirSync, readdirSync, lstatSync, realpathSync, existsSync, renameSync, unlinkSync, rmdirSync, copyFileSync, constants } from 'node:fs'
import { resolve, dirname, join, relative, isAbsolute, basename, extname, parse } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash, randomBytes } from 'node:crypto'
import { hostname } from 'node:os'
import { spawnSync } from 'node:child_process'
import { isDeepStrictEqual } from 'node:util'

export const SOURCE = dirname(fileURLToPath(import.meta.url))
export const ROOT = resolve(SOURCE, '../../..')
export const DATA = resolve(ROOT, 'data/master_v2/variable_trial_20261112')
export const SEED = resolve(ROOT, 'data/parametric_attempts/PARAM-20261107121230852-8D5DE5/package')
export const TOOLCHAIN_SCHEMA = 'winnsen.variable_trial.toolchain.v2'
export const INTEROP_DLLS = ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']
export const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
export const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const samePath = (left, right) => resolve(left).toLowerCase() === resolve(right).toLowerCase()
const renameRetryDelaysMs = [10, 20, 40, 80, 160]
const renameRetryWait = new Int32Array(new SharedArrayBuffer(4))

function statOrMissing(path) {
  try { return lstatSync(path) } catch (error) { if (error.code === 'ENOENT') return null; throw error }
}

export function inside(path, root) {
  if (typeof path !== 'string' || !path || typeof root !== 'string' || !root || path.includes('\0')) throw new Error('INVALID_PATH')
  const full = resolve(path), tail = relative(resolve(root), full)
  if (tail === '..' || tail.startsWith('..\\') || tail.startsWith('../') || isAbsolute(tail)) throw new Error('PATH_ESCAPE ' + path)
  const segments = full.slice(parse(full).root.length).split(/[\\/]/)
  if (segments.some(segment => /[:<>"|?*\x00-\x1f]/.test(segment) || /[. ]$/.test(segment) || /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)/i.test(segment))) throw new Error('AMBIGUOUS_PATH ' + path)
  for (let current = full; ; current = dirname(current)) {
    const stat = statOrMissing(current)
    if (stat?.isSymbolicLink()) throw new Error('REPARSE_PATH ' + current)
    if (stat && !samePath(realpathSync.native(current), current)) throw new Error('REDIRECTED_PATH ' + current)
    if (current === dirname(current)) break
  }
  return full
}

export function directory(path, root = DATA, exclusive = false) {
  const full = inside(path, root)
  if (exclusive) { directory(dirname(full), root); mkdirSync(full) } else mkdirSync(full, { recursive: true })
  inside(full, root)
  return full
}

function atomicText(path, content, root, beforeCommit) {
  const full = inside(path, root)
  directory(dirname(full), root)
  const existing = statOrMissing(full)
  if (existing && (!existing.isFile() || existing.nlink > 1)) throw new Error('SHARED_OUTPUT_FILE ' + full)
  const temporary = inside(join(dirname(full), '.' + basename(full) + '.' + randomBytes(8).toString('hex') + '.tmp'), root)
  try {
    writeFileSync(temporary, content, { flag: 'wx' })
    for (let attempt = 0; ; attempt++) {
      inside(full, root)
      const current = statOrMissing(full), staged = lstatSync(inside(temporary, root))
      if (current && (!current.isFile() || current.nlink > 1)) throw new Error('SHARED_OUTPUT_FILE ' + full)
      if (!staged.isFile() || staged.nlink > 1) throw new Error('SHARED_OUTPUT_FILE ' + temporary)
      beforeCommit?.()
      try { renameSync(temporary, full); break } catch (error) {
        if (!['EPERM', 'EBUSY'].includes(error.code) || attempt >= renameRetryDelaysMs.length) throw error
        Atomics.wait(renameRetryWait, 0, 0, renameRetryDelaysMs[attempt])
      }
    }
  } finally { if (existsSync(temporary)) unlinkSync(temporary) }
}

export function write(path, value, { root = DATA, beforeCommit } = {}) {
  atomicText(path, JSON.stringify(value, null, 2) + '\n', root, beforeCommit)
}
export function writeText(path, text, { root = DATA } = {}) { atomicText(path, text, root) }

export function files(folder, { excludePrefixes = [], excludeDirectoryPattern } = {}) {
  const full = inside(folder, folder), stat = statOrMissing(full)
  if (!stat) return []
  if (!stat.isDirectory()) throw new Error('DIRECTORY_REQUIRED ' + full)
  return readdirSync(full).sort().filter(name => !excludePrefixes.some(prefix => name.startsWith(prefix))).flatMap(name => {
    const path = inside(join(full, name), full), item = lstatSync(path)
    if (item.isDirectory()) return excludeDirectoryPattern && new RegExp(excludeDirectoryPattern, 'i').test(name) ? [] : files(path, { excludePrefixes, excludeDirectoryPattern })
    if (!item.isFile()) throw new Error('REGULAR_FILE_REQUIRED ' + path)
    return [path]
  })
}

export function inventory(paths) {
  return [...new Set(paths.map(path => inside(path, path)))].sort().map(path => {
    const stat = lstatSync(path)
    if (!stat.isFile()) throw new Error('REGULAR_FILE_REQUIRED ' + path)
    return { path, size: stat.size, sha256: hash(path) }
  })
}
export function compare(items) { return items.filter(item => !existsSync(item.path) || hash(item.path) !== item.sha256).map(item => item.path) }

function nativeTaskIndexSnapshot(path, raw = readFileSync(path)) {
  const validationErrors = []
  let semantic = null
  try { semantic = JSON.parse(raw.toString('utf8')) } catch { validationErrors.push('invalid_json') }
  if (!semantic || typeof semantic !== 'object' || Array.isArray(semantic) ||
      Object.keys(semantic).sort().join(',') !== 'rebuiltAt,requests,schema' || !Array.isArray(semantic.requests)) validationErrors.push('invalid_index_fields')
  if (semantic?.schema !== 'winnsen.native_model_request_index.v2') validationErrors.push('invalid_index_schema')
  const timestamp = semantic?.rebuiltAt
  if (typeof timestamp !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(timestamp) ||
      !Number.isFinite(Date.parse(timestamp)) || new Date(timestamp).toISOString() !== timestamp) validationErrors.push('invalid_rebuiltAt')
  if (!raw.equals(Buffer.from(JSON.stringify(semantic, null, 2) + '\n'))) validationErrors.push('not_owner_json_format')
  return { path, size: raw.length, sha256: createHash('sha256').update(raw).digest('hex').toUpperCase(),
    rawBytesBase64: raw.toString('base64'), semantic, validationErrors }
}

export function protectionSnapshot(scopes, { root = ROOT } = {}) {
  const indexPath = resolve(root, 'data/native_model_requests/native_model_request_index.json')
  let nativeTaskIndex = null
  const snapshot = { schema: 'winnsen.variable_trial.protection.v3', scopes: scopes.map(scope => {
    const path = inside(scope.path, scope.path), stat = statOrMissing(path)
    if (scope.required && !stat) throw new Error('PROTECTED_INPUT_MISSING ' + path)
    const accepted = file => {
      const tail = relative(path, file), parts = tail.split(/[\\/]/)
      return (!scope.extensions || scope.extensions.includes(extname(file).toLowerCase())) &&
        !parts.some(part => scope.excludePrefixes?.some(prefix => part.startsWith(prefix))) &&
        !parts.slice(0, -1).some(part => scope.excludeDirectoryPattern && new RegExp(scope.excludeDirectoryPattern, 'i').test(part))
    }
    const members = !stat ? [] : stat.isDirectory() ? files(path, scope).filter(accepted) : [path]
    const recorded = inventory(members).map(member => {
      if (!samePath(member.path, indexPath)) return member
      nativeTaskIndex ||= nativeTaskIndexSnapshot(member.path)
      return { path: member.path, size: nativeTaskIndex.size, sha256: nativeTaskIndex.sha256 }
    })
    return { ...scope, path, exists: Boolean(stat), kind: !stat ? 'missing' : stat.isDirectory() ? 'directory' : 'file', members: recorded }
  }) }
  return { ...snapshot, nativeTaskIndex }
}

export function compareProtection(before, { root = ROOT, onNativeTaskIndexComparison = () => {} } = {}) {
  const after = protectionSnapshot(before.scopes.map(({ members, exists, kind, required, ...scope }) => scope), { root })
  const changed = new Set()
  before.scopes.forEach((scope, index) => {
    const next = after.scopes[index]
    if (scope.exists !== next.exists || scope.kind !== next.kind) changed.add(scope.path)
    const previousMembers = new Map(scope.members.map(item => [item.path, item]))
    const nextMembers = new Map(next.members.map(item => [item.path, item]))
    for (const path of new Set([...previousMembers.keys(), ...nextMembers.keys()])) {
      if (previousMembers.get(path)?.sha256 !== nextMembers.get(path)?.sha256) changed.add(path)
    }
  })
  const indexPath = resolve(root, 'data/native_model_requests/native_model_request_index.json')
  const member = snapshot => snapshot.scopes.flatMap(scope => scope.members).find(row => samePath(row.path, indexPath))
  const previousIndex = member(before), nextIndex = member(after)
  if (previousIndex || nextIndex) {
    const storeDirectory = dirname(indexPath)
    const taskFilesChanged = [...changed].filter(path => {
      const tail = relative(storeDirectory, path)
      return !samePath(path, indexPath) && tail !== '..' && !tail.startsWith('..\\') && !tail.startsWith('../') && !isAbsolute(tail)
    }).sort()
    const diagnostic = { schema: 'winnsen.variable_trial.native_index_protection.v1', path: indexPath,
      mode: before.schema === 'winnsen.variable_trial.protection.v3' ? 'recorded_bytes_and_rebuiltAt' : 'legacy_strict_sha256',
      before: previousIndex || null, after: after.nativeTaskIndex,
      rawHashChanged: previousIndex?.sha256 !== nextIndex?.sha256, hashChangeOnlyRebuiltAt: false,
      changedJsonFields: [], taskFilesChanged, accepted: false, rejectionReasons: [] }
    if (diagnostic.mode === 'legacy_strict_sha256') {
      diagnostic.accepted = !diagnostic.rawHashChanged
      if (!diagnostic.accepted) diagnostic.rejectionReasons.push('legacy_snapshot_requires_identical_sha256')
    } else {
      const checked = (record, row) => {
        if (!record || !row || typeof record.rawBytesBase64 !== 'string') return { ...row, validationErrors: ['recorded_index_bytes_missing'] }
        const bytes = Buffer.from(record.rawBytesBase64, 'base64'), result = nativeTaskIndexSnapshot(indexPath, bytes)
        if (record.rawBytesBase64 !== bytes.toString('base64') || record.path !== row.path ||
            result.sha256 !== row.sha256 || result.sha256 !== record.sha256 || result.size !== row.size ||
            result.size !== record.size || !isDeepStrictEqual(record.semantic, result.semantic)) result.validationErrors.push('recorded_index_bytes_or_semantic_mismatch')
        return result
      }
      diagnostic.before = checked(before.nativeTaskIndex, previousIndex)
      diagnostic.after = checked(after.nativeTaskIndex, nextIndex)
      const a = diagnostic.before, b = diagnostic.after
      diagnostic.rejectionReasons.push(...a.validationErrors.map(value => 'before_' + value), ...b.validationErrors.map(value => 'after_' + value))
      if (a.semantic && b.semantic) {
        diagnostic.changedJsonFields = [...new Set([...Object.keys(a.semantic), ...Object.keys(b.semantic)])]
          .filter(key => !isDeepStrictEqual(a.semantic[key], b.semantic[key])).sort()
      }
      if (!diagnostic.rejectionReasons.length && diagnostic.rawHashChanged) {
        const restored = { ...b.semantic, rebuiltAt: a.semantic.rebuiltAt }
        diagnostic.hashChangeOnlyRebuiltAt = diagnostic.changedJsonFields.length === 1 && diagnostic.changedJsonFields[0] === 'rebuiltAt' &&
          Buffer.from(a.rawBytesBase64, 'base64').equals(Buffer.from(JSON.stringify(restored, null, 2) + '\n'))
        if (!diagnostic.hashChangeOnlyRebuiltAt) diagnostic.rejectionReasons.push('index_change_not_only_rebuiltAt_value')
      }
      if (taskFilesChanged.length) diagnostic.rejectionReasons.push('protected_task_files_changed')
      diagnostic.accepted = diagnostic.rejectionReasons.length === 0
      if (diagnostic.accepted) changed.delete(previousIndex.path)
      else changed.add(indexPath)
    }
    onNativeTaskIndexComparison(diagnostic)
  }
  return [...changed].sort()
}

export function processState({ pids = [], runner = spawnSync } = {}) {
  if (!pids.every(pid => Number.isSafeInteger(pid) && pid > 0)) throw new Error('INVALID_PROCESS_ID')
  const filter = ["Name='SLDWORKS.exe'", "Name='sldProcMon.exe'", ...new Set(pids.map(pid => 'ProcessId=' + pid))].join(' OR ')
  const command = "$ErrorActionPreference='Stop';try {$items=@(Get-CimInstance Win32_Process -Filter \"" + filter + "\" | ForEach-Object { if(-not $_.CreationDate -or -not $_.ExecutablePath){throw 'PROCESS_IDENTITY_UNAVAILABLE'}; [pscustomobject]@{name=$_.Name;pid=[int]$_.ProcessId;parentPid=[int]$_.ParentProcessId;startedAtUtc=$_.CreationDate.ToUniversalTime().ToString('o');executable=$_.ExecutablePath} }); ConvertTo-Json -InputObject @{schema='winnsen.variable_trial.processes.v1';processes=@($items)} -Depth 4 -Compress;exit 0}catch{[Console]::Error.WriteLine($_.Exception.ToString());exit 1}"
  const result = runner('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], { encoding: 'utf8', windowsHide: true, timeout: 15000 })
  if (result.error || result.status !== 0 || result.signal || String(result.stderr || '').trim()) throw new Error('PROCESS_QUERY_FAILED ' + (result.error?.message || result.stderr || result.signal || result.status))
  let response
  try { response = JSON.parse(String(result.stdout)) } catch (cause) { throw new Error('PROCESS_QUERY_INVALID_JSON', { cause }) }
  if (response?.schema !== 'winnsen.variable_trial.processes.v1' || !Array.isArray(response.processes)) throw new Error('PROCESS_QUERY_INVALID_SHAPE')
  const seen = new Set()
  for (const item of response.processes) {
    if (!Number.isSafeInteger(item?.pid) || item.pid < 1 || seen.has(item.pid) || !Number.isSafeInteger(item.parentPid) || item.parentPid < 0 ||
        typeof item.name !== 'string' || !item.name || typeof item.startedAtUtc !== 'string' || !Number.isFinite(Date.parse(item.startedAtUtc)) ||
        typeof item.executable !== 'string' || !isAbsolute(item.executable)) throw new Error('PROCESS_IDENTITY_INVALID')
    if (!/^sldworks\.exe$/i.test(item.name) && !/^sldprocmon\.exe$/i.test(item.name) && !pids.includes(item.pid)) throw new Error('UNREQUESTED_PROCESS_IDENTITY')
    seen.add(item.pid)
  }
  return response.processes
}

export function activeTasks(taskDirectory = resolve(ROOT, 'data/native_model_requests')) {
  inside(taskDirectory, taskDirectory)
  return readdirSync(taskDirectory).filter(name => /^(NATIVE|PARAMETRIC)-.*\.json$/i.test(name)).flatMap(name => {
    const task = read(inside(join(taskDirectory, name), taskDirectory)), build = task.nativeBuild || {}
    return ['created', 'queued', 'claimed', 'planning', 'building', 'running', 'validating'].includes(build.state) || build.lease ? [{ id: task.id, state: build.state, leased: Boolean(build.lease) }] : []
  })
}
export function assertIdle({ query = processState, tasks = activeTasks } = {}) {
  const cad = query(), pending = tasks()
  if (cad.length || pending.length) throw new Error('CAD_RESOURCE_BUSY ' + JSON.stringify({ cad, tasks: pending }))
}

export function acquireLock({ lockDirectory = resolve(ROOT, 'data/native_model_requests/.native-worker-instance.lock'), clock = () => new Date(), heartbeatMs = 10000, onLost = () => {} } = {}) {
  const lockDir = inside(lockDirectory, dirname(lockDirectory))
  directory(dirname(lockDir), dirname(lockDir))
  const ownerPath = join(lockDir, 'owner.json'), workerId = 'variable-trial-' + process.pid + '-' + randomBytes(4).toString('hex'), token = randomBytes(16).toString('hex')
  const now = () => clock().toISOString()
  const owner = { schema: 'winnsen.native_worker_instance_lock.v1', workerId, token, pid: process.pid, runnerPid: process.pid, host: hostname(), acquiredAt: now(), heartbeatAt: now() }
  if (existsSync(lockDir)) throw new Error('SHARED_LOCK_BUSY')
  const staging = inside(lockDir + '.' + workerId, dirname(lockDir)), stagedOwner = join(staging, 'owner.json')
  mkdirSync(staging)
  try {
    write(stagedOwner, owner, { root: staging })
    inside(lockDir, dirname(lockDir))
    if (existsSync(lockDir)) throw new Error('SHARED_LOCK_BUSY')
    renameSync(staging, lockDir)
  } catch (error) {
    if (existsSync(stagedOwner)) unlinkSync(stagedOwner)
    if (existsSync(staging)) rmdirSync(staging)
    if (['EEXIST', 'ENOTEMPTY', 'EPERM', 'EACCES'].includes(error.code) && existsSync(lockDir)) throw new Error('SHARED_LOCK_BUSY', { cause: error })
    throw error
  }
  let failure = null, timer
  const assert = () => {
    if (failure) throw failure
    try {
      const current = read(inside(ownerPath, lockDir))
      if (current.token !== token || current.workerId !== workerId) throw new Error('SHARED_LOCK_LOST')
      return current
    } catch (error) { failure = error; throw error }
  }
  const update = patch => {
    const current = assert(), next = { ...current, ...patch, heartbeatAt: now() }
    write(ownerPath, next, { root: lockDir, beforeCommit: assert })
    Object.assign(owner, next)
    return next
  }
  const heartbeat = () => update({})
  timer = setInterval(() => { try { heartbeat() } catch (error) { if (!failure) failure = error; try { onLost(error) } catch {} } }, heartbeatMs)
  return {
    owner, lockDir, assert, heartbeat,
    bindProcess(identity) {
      if (!Number.isSafeInteger(identity?.pid) || identity.pid < 1 || !['native', 'cad', 'runner'].includes(identity.kind)) throw new Error('INVALID_LOCK_PROCESS')
      if (identity.kind === 'cad' && (!identity.startedAtUtc || !identity.executable)) throw new Error('CAD_LOCK_IDENTITY_REQUIRED')
      return update({ pid: identity.pid, protectedProcess: { ...identity } })
    },
    release() { clearInterval(timer); assert(); unlinkSync(ownerPath); rmdirSync(lockDir) },
    retain() { heartbeat() },
  }
}

export function copyTree(source, target, { outputRoot = DATA, sourceRoot = ROOT, copyFile = copyFileSync, excludeRelativeDirectories = [] } = {}) {
  const from = inside(source, sourceRoot), to = inside(target, outputRoot)
  if (!lstatSync(from).isDirectory()) throw new Error('COPY_SOURCE_DIRECTORY_REQUIRED')
  const overlap = relative(from, to)
  if (overlap === '' || (!isAbsolute(overlap) && overlap !== '..' && !overlap.startsWith('..\\') && !overlap.startsWith('../'))) throw new Error('COPY_SOURCE_OUTPUT_OVERLAP')
  const before = protectionSnapshot([{ path: from, required: true }])
  if (!Array.isArray(excludeRelativeDirectories) || excludeRelativeDirectories.some(item => typeof item !== 'string' || !/^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*$/.test(item))) throw new Error('INVALID_COPY_EXCLUSION')
  const exclusions = excludeRelativeDirectories.map(item => inside(join(from, item), from))
  for (const item of exclusions) if (!lstatSync(item).isDirectory()) throw new Error('COPY_EXCLUSION_DIRECTORY_REQUIRED')
  const entries = before.scopes[0].members.filter(item => !exclusions.some(excluded => item.path.startsWith(excluded + '\\')))
  directory(to, outputRoot, true)
  for (const item of entries) {
    const out = inside(join(to, relative(from, item.path)), to)
    directory(dirname(out), to)
    copyFile(item.path, out, constants.COPYFILE_EXCL)
    if (hash(out) !== item.sha256) throw new Error('COPY_BYTES_MISMATCH ' + out)
  }
  const changed = compareProtection(before)
  if (changed.length) throw new Error('COPY_SOURCE_CHANGED ' + JSON.stringify(changed))
  const copied = inventory(files(to)), expected = new Map(entries.map(item => [relative(from, item.path), item.sha256]))
  if (copied.length !== entries.length || copied.some(item => expected.get(relative(to, item.path)) !== item.sha256)) throw new Error('COPY_MEMBER_MISMATCH')
  return { source: from, target: to, excludedRelativeDirectories: [...excludeRelativeDirectories], files: entries.map(item => ({ relativePath: relative(from, item.path), size: item.size, sha256: item.sha256 })) }
}

export function validateToolchain(manifest, { dataRoot = DATA, sourceRoot = SOURCE } = {}) {
  if (manifest?.schema !== TOOLCHAIN_SCHEMA || !Array.isArray(manifest.sources) || !Array.isArray(manifest.dlls)) throw new Error('TOOLCHAIN_MANIFEST_V2_REQUIRED')
  const exe = inside(manifest.exe, join(dataRoot, 'toolchain')), bin = dirname(exe), binName = relative(join(dataRoot, 'toolchain'), bin)
  if (!binName || /[\\/]/.test(binName) || basename(exe) !== 'VariableTrial.exe') throw new Error('TOOLCHAIN_EXECUTABLE_PATH_INVALID')
  const expectedSources = readdirSync(inside(sourceRoot, sourceRoot)).filter(name => name.endsWith('.cs')).sort().map(name => join(sourceRoot, name))
  if (!expectedSources.length || manifest.sources.length !== expectedSources.length) throw new Error('TOOLCHAIN_SOURCE_SET_CHANGED')
  const checkHash = (path, sha256) => {
    if (!/^[A-F0-9]{64}$/.test(sha256 || '') || !lstatSync(path).isFile() || hash(path) !== sha256) throw new Error('BUILD_CHANGED_REBUILD_REQUIRED ' + path)
  }
  checkHash(exe, manifest.sha256)
  manifest.sources.forEach((item, index) => {
    const expected = inside(expectedSources[index], sourceRoot), archive = inside(join(bin, 'sources', basename(expected)), bin)
    if (!samePath(item.path, expected) || !samePath(item.archivePath, archive)) throw new Error('TOOLCHAIN_SOURCE_PATH_INVALID')
    inside(item.path, sourceRoot); inside(item.archivePath, bin)
    checkHash(item.path, item.sha256); checkHash(item.archivePath, item.sha256)
  })
  if (manifest.dlls.length !== INTEROP_DLLS.length) throw new Error('TOOLCHAIN_DLL_SET_INVALID')
  manifest.dlls.forEach((item, index) => {
    const expected = inside(join(bin, INTEROP_DLLS[index]), bin)
    if (!samePath(item.path, expected)) throw new Error('TOOLCHAIN_DLL_PATH_INVALID')
    checkHash(expected, item.sha256)
  })
  const actualDlls = readdirSync(bin).filter(name => /\.dll$/i.test(name)).sort()
  if (JSON.stringify(actualDlls) !== JSON.stringify([...INTEROP_DLLS].sort()) || existsSync(exe + '.config')) throw new Error('TOOLCHAIN_UNMANIFESTED_DEPENDENCY')
  return manifest
}
