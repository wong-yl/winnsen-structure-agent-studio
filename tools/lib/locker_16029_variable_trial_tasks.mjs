import { randomBytes } from 'node:crypto'
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { dirname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '../..')
export const DEFAULT_VARIABLE_TRIAL_TASKS_DIR = resolve(ROOT, 'data/variable_trial_portal_tasks')
const SAFE_ID_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/
const ACTIVE_STATUSES = new Set(['queued', 'running', 'cancel_requested'])

function atomicWriteJson(targetPath, data) {
  mkdirSync(dirname(targetPath), { recursive: true })
  const tmpPath = `${targetPath}.tmp-${process.pid}-${randomBytes(4).toString('hex')}`
  writeFileSync(tmpPath, JSON.stringify(data, null, 2) + '\n', 'utf8')
  renameSync(tmpPath, targetPath)
}

function readJsonSafe(filePath) {
  try {
    return JSON.parse(readFileSync(filePath, 'utf8'))
  } catch {
    return null
  }
}

export function isPathInsideRoot(candidatePath, rootDir) {
  const rel = relative(resolve(rootDir), resolve(candidatePath))
  return Boolean(rel) && !rel.startsWith('..') && !isAbsolute(rel)
}

export function isSafeTaskId(taskId) {
  return typeof taskId === 'string' && SAFE_ID_PATTERN.test(taskId)
}

export function generateVariableTrialTaskId() {
  const stamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z')
  const hex = randomBytes(3).toString('hex').toUpperCase()
  return `VT16029-${stamp}-${hex}`
}

export function createVariableTrialTaskStore({ dataDir = DEFAULT_VARIABLE_TRIAL_TASKS_DIR } = {}) {
  const rootDir = resolve(dataDir)
  mkdirSync(rootDir, { recursive: true })

  const taskDir = (taskId) => {
    if (!isSafeTaskId(taskId)) throw new Error('INVALID_TASK_ID')
    return join(rootDir, taskId)
  }

  const taskJsonPath = (taskId) => join(taskDir(taskId), 'task.json')

  function read(taskId) {
    if (!isSafeTaskId(taskId)) return null
    const path = taskJsonPath(taskId)
    if (!existsSync(path)) return null
    return readJsonSafe(path)
  }

  function list({ username = null } = {}) {
    if (!existsSync(rootDir)) return []
    const items = []
    for (const entry of readdirSync(rootDir, { withFileTypes: true })) {
      if (!entry.isDirectory() || entry.name.startsWith('.')) continue
      if (!isSafeTaskId(entry.name)) continue
      const task = readJsonSafe(join(rootDir, entry.name, 'task.json'))
      if (!task) continue
      if (username && task.username !== username) continue
      items.push(task)
    }
    return items.sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')))
  }

  function findReusableMatch({ username, requestFingerprint }) {
    if (!requestFingerprint) return null
    const candidates = list({ username })
    return candidates.find((task) => task.requestFingerprint === requestFingerprint
      && (task.status === 'ready' || ACTIVE_STATUSES.has(task.status))) || null
  }

  function create({
    id = generateVariableTrialTaskId(),
    username,
    normalizedRequest,
    trialAssemblyCase,
    layoutSummary,
    requestFingerprint,
  }) {
    if (!isSafeTaskId(id)) throw new Error('INVALID_TASK_ID')
    const now = new Date().toISOString()
    const record = {
      schema: 'winnsen.locker16029.variable_trial_task.v1',
      id,
      username,
      createdAt: now,
      updatedAt: now,
      status: 'queued',
      stage: 'queued',
      progressDetail: '任务已入队，等待空闲 SolidWorks 2020 资源开始构建...',
      productionRelease: false,
      requestFingerprint,
      request: normalizedRequest,
      trialAssemblyCase,
      layoutSummary,
      workerPid: null,
      publication: null,
      error: null,
    }
    atomicWriteJson(taskJsonPath(id), record)
    return record
  }

  function update(taskId, mutator) {
    const existing = read(taskId)
    if (!existing) throw new Error('TASK_NOT_FOUND')
    const next = mutator(structuredClone(existing))
    next.updatedAt = new Date().toISOString()
    atomicWriteJson(taskJsonPath(taskId), next)
    return next
  }

  function claimNextQueued(workerPid = process.pid) {
    const queued = list()
      .filter((item) => item.status === 'queued')
      .sort((a, b) => String(a.createdAt || '').localeCompare(String(b.createdAt || '')))
    if (!queued.length) return null
    const target = queued[0]
    return update(target.id, (task) => {
      if (task.status !== 'queued') return task
      task.status = 'running'
      task.stage = 'claiming'
      task.progressDetail = '工作进程已接单，正在准备隔离参数规格...'
      task.workerPid = workerPid
      task.error = null
      return task
    })
  }

  function requestCancel(taskId) {
    const existing = read(taskId)
    if (!existing) return { ok: false, status: 404, error: '任务不存在。' }
    if (existing.status === 'ready' || existing.status === 'failed' || existing.status === 'cancelled') {
      return { ok: false, status: 409, error: `当前状态（${existing.status}）不可取消。` }
    }
    const updated = update(taskId, (task) => {
      if (task.status === 'queued') {
        task.status = 'cancelled'
        task.stage = 'cancelled'
        task.progressDetail = '已取消排队任务。'
      } else if (task.status === 'running') {
        task.status = 'cancel_requested'
        task.progressDetail = '已收到取消请求，正在安全中止 SolidWorks 任务并清理现场...'
      }
      return task
    })
    return { ok: true, task: updated }
  }

  function retryTask(taskId) {
    const existing = read(taskId)
    if (!existing) return { ok: false, status: 404, error: '任务不存在。' }
    if (existing.status !== 'failed' && existing.status !== 'cancelled') {
      return { ok: false, status: 409, error: '仅失败或已取消的任务支持重新排队。' }
    }
    const updated = update(taskId, (task) => {
      task.status = 'queued'
      task.stage = 'queued'
      task.progressDetail = '任务已重新加入构建队列，等待空闲 CAD 资源...'
      task.workerPid = null
      task.error = null
      return task
    })
    return { ok: true, task: updated }
  }

  function removeTask(taskId) {
    const existing = read(taskId)
    if (!existing) return { ok: false, status: 404, error: '任务不存在。' }
    if (ACTIVE_STATUSES.has(existing.status)) {
      return { ok: false, status: 409, error: '排队或构建中的任务请先取消后再删除。' }
    }
    const dir = taskDir(taskId)
    if (!isPathInsideRoot(dir, rootDir)) throw new Error('PATH_ESCAPE')
    rmSync(dir, { recursive: true, force: true })
    return { ok: true, deletedId: taskId }
  }

  function hasActiveTasks() {
    return list().some((item) => ACTIVE_STATUSES.has(item.status))
  }

  return {
    rootDir,
    taskDir,
    read,
    list,
    findReusableMatch,
    create,
    update,
    claimNextQueued,
    requestCancel,
    retryTask,
    removeTask,
    hasActiveTasks,
  }
}
