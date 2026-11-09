// Isolated verbatim lock helpers from the qualified shared worker; same dataDir and token protocol.
import { createHash, randomBytes } from 'node:crypto'
import { closeSync, existsSync, fsyncSync, mkdirSync, openSync, readFileSync, renameSync, rmSync, statSync, unlinkSync, writeFileSync } from 'node:fs'
import { hostname } from 'node:os'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
const WORKER_LOCK_STALE_MS = 120000
function isoNow(clock = () => new Date()) {
  const value = clock()
  return (value instanceof Date ? value : new Date(value)).toISOString()
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}

function stableJson(value) {
  return JSON.stringify(stableValue(value))
}

function assertSafeIdentifier(value, label) {
  const normalized = String(value || '')
  if (!/^[A-Za-z0-9_.-]{1,160}$/.test(normalized)) {
    const error = new Error(`${label} is not a safe identifier`)
    error.code = 'unsafe_identifier'
    throw error
  }
  return normalized
}

function ensureUnderRoot(candidate, root, label) {
  const rel = relative(resolve(root), resolve(candidate))
  if (rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))) return resolve(candidate)
  throw new Error(`${label} escaped its trusted root`)
}

function nativeTaskRoot(dataDir) {
  return ensureUnderRoot(resolve(dataDir, 'native_model_requests'), dataDir, 'worker task root')
}

function atomicWriteJson(filePath, value, { immutable = false } = {}) {
  mkdirSync(dirname(filePath), { recursive: true })
  const content = `${JSON.stringify(value, null, 2)}\n`
  if (immutable && existsSync(filePath)) {
    const existing = readFileSync(filePath, 'utf8')
    if (existing !== content) {
      const error = new Error(`immutable artifact already exists with different content: ${basename(filePath)}`)
      error.code = 'immutable_artifact_conflict'
      throw error
    }
    return { created: false, sha256: sha256(existing) }
  }
  const tempPath = `${filePath}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`
  let descriptor = null
  try {
    descriptor = openSync(tempPath, 'wx')
    writeFileSync(descriptor, content, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = null
    if (immutable && existsSync(filePath)) {
      const existing = readFileSync(filePath, 'utf8')
      if (existing !== content) {
        const error = new Error(`immutable artifact already exists with different content: ${basename(filePath)}`)
        error.code = 'immutable_artifact_conflict'
        throw error
      }
      unlinkSync(tempPath)
      return { created: false, sha256: sha256(existing) }
    }
    renameSync(tempPath, filePath)
    return { created: true, sha256: sha256(content) }
  } finally {
    if (descriptor !== null) closeSync(descriptor)
    if (existsSync(tempPath)) unlinkSync(tempPath)
  }
}

function isProcessLive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

export function acquireNativeWorkerInstanceLock({
  dataDir,
  workerId,
  clock = () => new Date(),
  staleMs = WORKER_LOCK_STALE_MS,
} = {}) {
  const safeWorkerId = assertSafeIdentifier(workerId, 'workerId')
  const currentMs = () => {
    const value = clock()
    return (value instanceof Date ? value : new Date(value)).getTime()
  }
  const taskRoot = nativeTaskRoot(dataDir)
  const lockDir = ensureUnderRoot(resolve(taskRoot, '.native-worker-instance.lock'), taskRoot, 'worker lock')
  const ownerPath = resolve(lockDir, 'owner.json')
  const token = randomBytes(16).toString('hex')
  mkdirSync(taskRoot, { recursive: true })

  const attemptAcquire = () => {
    try {
      mkdirSync(lockDir)
      return true
    } catch (error) {
      if (error?.code !== 'EEXIST') throw error
      return false
    }
  }

  if (!attemptAcquire()) {
    let owner = null
    try {
      owner = JSON.parse(readFileSync(ownerPath, 'utf8'))
    } catch {
      // The directory timestamp remains the fallback for an interrupted owner write.
    }
    const heartbeatMs = Date.parse(String(owner?.heartbeatAt || ''))
    const fallbackMs = statSync(lockDir).mtimeMs
    const stale = currentMs() - (Number.isFinite(heartbeatMs) ? heartbeatMs : fallbackMs) > staleMs
    if (!stale || isProcessLive(Number(owner?.pid))) {
      const error = new Error(`native worker instance is already active: ${owner?.workerId || 'unknown'}`)
      error.code = 'native_worker_already_active'
      throw error
    }
    rmSync(lockDir, { recursive: true, force: true })
    if (!attemptAcquire()) {
      const error = new Error('native worker instance lock was claimed concurrently')
      error.code = 'native_worker_already_active'
      throw error
    }
  }

  const owner = {
    schema: 'winnsen.native_worker_instance_lock.v1',
    workerId: safeWorkerId,
    token,
    pid: process.pid,
    host: hostname(),
    acquiredAt: isoNow(clock),
    heartbeatAt: isoNow(clock),
  }
  atomicWriteJson(ownerPath, owner)

  return {
    lockDir,
    heartbeat() {
      const current = JSON.parse(readFileSync(ownerPath, 'utf8'))
      if (current.token !== token || current.workerId !== safeWorkerId) {
        const error = new Error('native worker instance lock ownership changed')
        error.code = 'native_worker_lock_lost'
        throw error
      }
      current.heartbeatAt = isoNow(clock)
      atomicWriteJson(ownerPath, current)
      return current
    },
    release() {
      if (!existsSync(ownerPath)) return false
      const current = JSON.parse(readFileSync(ownerPath, 'utf8'))
      if (current.token !== token || current.workerId !== safeWorkerId) return false
      rmSync(lockDir, { recursive: true, force: true })
      return true
    },
  }
}

