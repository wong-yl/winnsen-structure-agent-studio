import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { randomUUID } from 'node:crypto'
import { normalizeOutdoorColumns } from './outdoor_22057_layout.mjs'

export const outdoorTaskId = id => /^OUT22057-[a-f0-9-]{36}$/.test(id)
export const outdoorTasksDir = dataDir => resolve(dataDir, 'outdoor_22057_tasks')
export function readOutdoorJson(path, fallback = null) {
  if (!existsSync(path)) return fallback
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}
export function writeOutdoorJson(path, value) {
  mkdirSync(resolve(path, '..'), { recursive: true })
  const temporary = path + '.' + randomUUID() + '.tmp'
  writeFileSync(temporary, JSON.stringify(value, null, 2), 'utf8')
  renameSync(temporary, path)
}
export function readOutdoorTask(dataDir, id) {
  return outdoorTaskId(id) ? readOutdoorJson(resolve(outdoorTasksDir(dataDir), id + '.json')) : null
}
export function listOutdoorTasks(dataDir) {
  const dir = outdoorTasksDir(dataDir)
  if (!existsSync(dir)) return []
  return readdirSync(dir).filter(name => name.endsWith('.json') && outdoorTaskId(name.slice(0, -5)))
    .map(name => readOutdoorJson(resolve(dir, name))).sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}
export async function updateOutdoorTask(dataDir, id, update) {
  if (!outdoorTaskId(id)) throw new Error('Invalid outdoor task ID')
  const dir = outdoorTasksDir(dataDir)
  mkdirSync(dir, { recursive: true })
  const lock = resolve(dir, id + '.write-lock')
  let acquired = false
  for (let i = 0; i < 100 && !acquired; i++) {
    try { mkdirSync(lock); acquired = true; writeFileSync(resolve(lock, 'owner.json'), JSON.stringify({ pid: process.pid })) }
    catch (error) {
      if (error.code !== 'EEXIST') throw error
      const owner = readOutdoorJson(resolve(lock, 'owner.json'))
      if (owner?.pid) {
        try { process.kill(owner.pid, 0) }
        catch (probeError) { if (probeError.code === 'ESRCH') rmSync(lock, { recursive: true, force: true }) }
      }
      await new Promise(yes => setTimeout(yes, 25))
    }
  }
  if (!acquired) throw new Error('Outdoor task write is busy')
  try {
    const current = readOutdoorTask(dataDir, id)
    const next = update(current)
    if (next) writeOutdoorJson(resolve(dir, id + '.json'), next)
    return next
  } finally { rmSync(lock, { recursive: true, force: true }) }
}
export function outdoorSpecification(params, gate) {
  const dimensions = {}
  const editableLayout = gate.generation?.layout?.enabled === true
  if (!editableLayout && String(params.get('doors')) !== '14') throw new Error('当前保持双列14门布局')
  for (const key of ['height', 'width', 'depth']) {
    const text = String(params.get(key) ?? '').trim()
    const value = text ? Number(text) : NaN
    const range = gate.generation?.ranges_mm?.[key]
    if (!gate.generation?.enabled || !range || !Number.isFinite(value) || value < range[0] || value > range[1]) {
      throw new Error(range ? `${key === 'height' ? '柜高' : key === 'width' ? '柜宽' : '柜深'}须在${range[0]}–${range[1]} mm内` : '变尺寸生成暂未开放')
    }
    dimensions[key] = value
  }
  if (editableLayout) {
    if (Object.values(dimensions).some(value => Math.abs(value * 1000 - Math.round(value * 1000)) > 1e-6)) throw new Error('尺寸最多保留三位小数')
    const columns = normalizeOutdoorColumns(params.get('columns'), dimensions.height)
    const doors = columns.reduce((sum, column) => sum + column.doors.length, 0)
    if (Number(params.get('doors')) !== doors) throw new Error('总门数与左右列布局不一致')
    return { ...dimensions, doors, columns }
  }
  return { ...dimensions, doors: 14 }
}
