import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { basename, dirname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { compileLayout } from './layout.mjs'
import { execute } from './execute.mjs'
import { runProtectedPhase } from './run.mjs'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '../../..')
const BASE_SPEC_PATH = join(MODULE_DIR, 'inputs', 'specification.json')
const BASE_PROCESS_PATH = join(MODULE_DIR, 'inputs', 'process.json')
const DXF_CHECK_SCRIPT = join(MODULE_DIR, 'dxf_check.py')

export const PORTAL_DIMENSION_LIMITS = Object.freeze({
  widthMm: [700, 1200],
  heightMm: [1700, 2200],
  depthMm: [250, 650],
  doorWidthMm: [287, 537],
  maxDoorsPerColumn: 24,
  maxTotalDoors: 34,
  minDoorHeightMm: 100,
  doorGapMm: 7,
  verticalAllowanceMm: 87,
  closureToleranceMm: 0.001,
  envelopeToleranceMm: 0.05,
})

export const PORTAL_PRESETS = Object.freeze([
  {
    id: 'case1_baseline',
    label: 'Case 1 基线 · 1000×2000×500 (L4/R4)',
    description: '标准 4×4 等分门格，含 34 件钣金展开 DXF 基准。',
    cabinet: { widthMm: 1000, heightMm: 2000, depthMm: 500 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
      { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
    ],
  },
  {
    id: 'case2_min_limit',
    label: 'Case 2 最小包络 · 740×1700×250 (L4/R3)',
    description: '最小极限包络 740W × 1700H × 250D，左右不等门数。',
    cabinet: { widthMm: 740, heightMm: 1700, depthMm: 250 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
      { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
    ],
  },
  {
    id: 'case3_unequal',
    label: 'Case 3 不等比例 · 850×1850×485 (L5/R2)',
    description: '典型非对称工程配置：左 5 门变比例、右 2 门加高。',
    cabinet: { widthMm: 850, heightMm: 1850, depthMm: 485 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 2 }, { heightUnits: 1 }, { heightUnits: 1 }] },
      { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 2 }] },
    ],
  },
  {
    id: 'case4_max_limit',
    label: 'Case 4 最大包络 · 1200×2200×650 (L5/R5)',
    description: '最大极限包络 1200W × 2200H × 650D，含大门比例混合。',
    cabinet: { widthMm: 1200, heightMm: 2200, depthMm: 650 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 2 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 2 }] },
      { side: 'R', doors: [{ heightUnits: 2 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 2 }] },
    ],
  },
  {
    id: 'v37_760w_6door',
    label: '760W 6门 · 760×1917×550 (L3/R3)',
    description: '兼容 760 宽 6 门常用结构比例。',
    cabinet: { widthMm: 760, heightMm: 1917, depthMm: 550 },
    allocationMode: 'ratio',
    columns: [
      { side: 'L', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
      { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 1 }, { heightUnits: 1 }] },
    ],
  },
])

function round6(value) {
  return Math.round(Number(value) * 1e6) / 1e6
}

function sha256File(filePath) {
  return createHash('sha256').update(readFileSync(filePath)).digest('hex').toUpperCase()
}

function failValidation(code, message) {
  return { ok: false, code, message }
}

export function normalizeVariableTrialPortalRequest(payload) {
  if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
    return failValidation('invalid_payload', '请求体必须是 JSON 对象。')
  }

  const customerRequirementReference = String(payload.customerRequirementReference || '').trim().slice(0, 120)
  const rawCabinet = payload.cabinet && typeof payload.cabinet === 'object' ? payload.cabinet : {}
  const widthInputMode = payload.widthInputMode === 'installed_door_panel_width'
    ? 'installed_door_panel_width'
    : 'cabinet_outer_width'

  let widthMm = rawCabinet.widthMm ?? payload.cabinetWidth
  if (widthInputMode === 'installed_door_panel_width') {
    const requestedDoorWidth = Number(payload.requestedWidthMm ?? payload.doorLeafWidthMm)
    if (!Number.isFinite(requestedDoorWidth)) {
      return failValidation('invalid_door_width', '装后门板单宽必须是有限数值。')
    }
    if (requestedDoorWidth < PORTAL_DIMENSION_LIMITS.doorWidthMm[0] || requestedDoorWidth > PORTAL_DIMENSION_LIMITS.doorWidthMm[1]) {
      return failValidation(
        'door_width_out_of_range',
        `装后门板单宽必须在 ${PORTAL_DIMENSION_LIMITS.doorWidthMm[0]}–${PORTAL_DIMENSION_LIMITS.doorWidthMm[1]} mm 内。`,
      )
    }
    const derivedCabinetWidth = requestedDoorWidth * 2 + 126
    if (widthMm == null) widthMm = derivedCabinetWidth
    else if (Math.abs(Number(widthMm) - derivedCabinetWidth) > 0.001) {
      return failValidation('width_mode_mismatch', '整柜外宽与装后门板单宽不一致，请确认宽度输入口径。')
    }
  } else if (widthMm == null && payload.requestedWidthMm != null) {
    widthMm = payload.requestedWidthMm
  }

  widthMm = Number(widthMm)
  const heightMm = Number(rawCabinet.heightMm ?? payload.cabinetHeight)
  const depthMm = Number(rawCabinet.depthMm ?? payload.cabinetDepth)

  for (const [key, value, label] of [
    ['widthMm', widthMm, '柜宽'],
    ['heightMm', heightMm, '柜高'],
    ['depthMm', depthMm, '柜深'],
  ]) {
    if (!Number.isFinite(value) || !Number.isInteger(value)) {
      return failValidation(`invalid_${key}`, `${label}必须是整数 mm。`)
    }
    const [min, max] = PORTAL_DIMENSION_LIMITS[key]
    if (value < min || value > max) {
      return failValidation(`${key}_out_of_range`, `${label}必须在 ${min}–${max} mm 内。`)
    }
  }

  const rawColumns = Array.isArray(payload.columns) ? payload.columns : null
  if (!rawColumns || rawColumns.length !== 2 || rawColumns[0]?.side !== 'L' || rawColumns[1]?.side !== 'R') {
    return failValidation('invalid_columns', 'columns 必须按 L、R 顺序提供左右两列门格配置。')
  }

  const allocationMode = payload.allocationMode === 'height_mm' ? 'height_mm' : 'ratio'
  const pitchBudgetMm = heightMm - PORTAL_DIMENSION_LIMITS.verticalAllowanceMm
  const normalizedColumns = []
  const trialColumns = []

  for (const col of rawColumns) {
    const label = col.side === 'L' ? '左列' : '右列'
    const doors = Array.isArray(col.doors) ? col.doors : null
    if (!doors || doors.length < 1 || doors.length > PORTAL_DIMENSION_LIMITS.maxDoorsPerColumn) {
      return failValidation('invalid_column_door_count', `${label}门数必须在 1–${PORTAL_DIMENSION_LIMITS.maxDoorsPerColumn} 之间。`)
    }
    const cleanDoors = []
    const trialDoors = []
    let hasHeightMm = false
    let hasUnits = false

    for (let i = 0; i < doors.length; i += 1) {
      const door = doors[i]
      if (!door || typeof door !== 'object' || Array.isArray(door)) {
        return failValidation('invalid_door_item', `${label}第 ${i + 1} 扇门配置无效。`)
      }
      if (door.heightMm !== undefined && door.heightMm !== null) {
        hasHeightMm = true
        const mm = Number(door.heightMm)
        if (!Number.isFinite(mm) || mm < PORTAL_DIMENSION_LIMITS.minDoorHeightMm) {
          return failValidation('door_height_too_small', `${label}第 ${i + 1} 扇门高必须不少于 ${PORTAL_DIMENSION_LIMITS.minDoorHeightMm} mm。`)
        }
        const unit = round6(mm + PORTAL_DIMENSION_LIMITS.doorGapMm)
        cleanDoors.push({ heightMm: round6(mm), heightUnits: unit })
        trialDoors.push({ heightUnits: unit })
      } else {
        hasUnits = true
        const units = Number(door.heightUnits ?? 1)
        if (!Number.isFinite(units) || units <= 0) {
          return failValidation('invalid_height_units', `${label}第 ${i + 1} 扇门比例系数必须是正数。`)
        }
        cleanDoors.push({ heightUnits: round6(units) })
        trialDoors.push({ heightUnits: round6(units) })
      }
    }

    if (hasHeightMm && hasUnits) {
      return failValidation('mixed_door_mode', `${label}同一列内不能混用固定门高与比例系数。`)
    }
    if (hasHeightMm) {
      const totalPitchMm = cleanDoors.reduce((sum, item) => sum + item.heightMm + PORTAL_DIMENSION_LIMITS.doorGapMm, 0)
      if (Math.abs(totalPitchMm - pitchBudgetMm) > PORTAL_DIMENSION_LIMITS.closureToleranceMm) {
        return failValidation(
          'column_height_closure_failed',
          `${label}门高加每门 7 mm 间隙总和为 ${totalPitchMm.toFixed(3)} mm，未能闭合到柜高减 87 mm（${pitchBudgetMm} mm）。`,
        )
      }
    }

    normalizedColumns.push({ side: col.side, doors: cleanDoors })
    trialColumns.push({ side: col.side, doors: trialDoors })
  }

  const trialAssemblyCase = {
    cabinet: { widthMm, heightMm, depthMm },
    columns: trialColumns,
  }

  let layoutSummary
  try {
    layoutSummary = compileLayout(trialAssemblyCase)
  } catch (error) {
    return failValidation('layout_validation_failed', error instanceof Error ? error.message : String(error))
  }

  const normalizedRequest = {
    schema: 'winnsen.locker16029.variable_trial_request.v1',
    cabinet: { widthMm, heightMm, depthMm },
    widthInputMode,
    allocationMode: normalizedColumns.every((col) => col.doors.every((d) => d.heightMm !== undefined))
      ? 'height_mm'
      : allocationMode,
    columns: normalizedColumns,
    ...(customerRequirementReference ? { customerRequirementReference } : {}),
  }

  const fingerprintPayload = {
    cabinet: normalizedRequest.cabinet,
    columns: trialAssemblyCase.columns,
  }
  const requestFingerprint = createHash('sha256')
    .update(JSON.stringify(fingerprintPayload))
    .digest('hex')
    .toUpperCase()

  return {
    ok: true,
    normalizedRequest,
    trialAssemblyCase,
    layoutSummary,
    requestFingerprint,
  }
}

function round2(value) {
  const num = Number(value)
  return Number.isFinite(num) ? Math.round(num * 100) / 100 : null
}

function normalizePartRelPath(rawPath) {
  const normalized = String(rawPath || '').replace(/\//g, '\\').trim()
  if (!normalized) return ''
  const lower = normalized.toLowerCase()
  const asmIdx = lower.lastIndexOf('\\assemblies\\')
  if (asmIdx >= 0) {
    const afterAsm = normalized.slice(asmIdx + '\\assemblies\\'.length)
    const nextSep = afterAsm.indexOf('\\')
    if (nextSep >= 0) return afterAsm.slice(nextSep + 1)
  }
  for (const marker of ['\\parts\\', '\\structure\\']) {
    const idx = lower.lastIndexOf(marker)
    if (idx >= 0) return normalized.slice(idx + 1)
  }
  return normalized.replace(/^\\+/, '')
}

function dxfRelToPartRelLower(dxfRelPath) {
  return normalizePartRelPath(String(dxfRelPath || '').replace(/\.flat\.dxf$/i, '.sldprt')).toLowerCase()
}

function partRelToDxfRel(partRelPath) {
  return normalizePartRelPath(String(partRelPath || '').replace(/\.sldprt$/i, '.flat.dxf'))
}

function normalizeDxfRelPath(rawRelPath) {
  const normalized = String(rawRelPath || '').replace(/\//g, '\\').trim()
  if (!normalized) return ''
  return normalized.replace(/^(?:\.\\)+/, '').replace(/^\\+/, '')
}

function normalizeFlatRelFromRecord(flatPath, dxfDir) {
  const raw = String(flatPath || '').trim()
  if (!raw) return ''
  if (dxfDir) {
    const rel = relative(dxfDir, raw)
    if (rel && !rel.startsWith('..') && !isAbsolute(rel)) {
      return normalizeDxfRelPath(rel)
    }
  }
  return normalizePartRelPath(raw)
}

function classifySubsystem(relPath) {
  const lower = String(relPath || '').toLowerCase()
  const base = basename(relPath).toLowerCase()
  if (/^parts\\l_/i.test(lower)) return { order: 1, label: '左列门体' }
  if (/^parts\\r_/i.test(lower)) return { order: 2, label: '右列门体' }
  if (lower.startsWith('parts\\trial_')) return { order: 3, label: '门锁铰链及五金' }
  if (base.includes('底座') || base.includes('螺母') || base.includes('调整脚')) return { order: 4, label: '底座组件' }
  if (base.includes('上盖')) return { order: 5, label: '上盖组件' }
  if (base.includes('门框')) return { order: 6, label: '门框组件' }
  if (base.includes('竖隔板')) return { order: 7, label: '箱体竖隔板组件' }
  if (base.includes('横层板')) return { order: 8, label: '箱体横层板组件' }
  if (base.includes('维护') || base.includes('锁控')) return { order: 9, label: '维护门锁组件' }
  if (base.includes('箱体')) return { order: 10, label: '箱体围板与加强筋' }
  return { order: 11, label: '其他结构件' }
}

function extractMaterialsFromEvidence(assemblyEvidence) {
  const byLower = new Map()
  const events = Array.isArray(assemblyEvidence?.events) ? assemblyEvidence.events : []
  for (const ev of events) {
    if (ev?.inspection !== 'assembly_native_feature_health' || !ev?.path) continue
    const relLower = normalizePartRelPath(ev.path).toLowerCase()
    const matFeat = (ev.features || []).find((f) => f?.type === 'MaterialFolder' && f?.name)
    if (!matFeat) continue
    const isAfter = /^full_(?:after|reopen)_/i.test(String(ev.phase || ''))
    if (isAfter || !byLower.has(relLower)) {
      byLower.set(relLower, String(matFeat.name))
    }
  }
  return byLower
}

function extract3dBoundsFromEvidence(assemblyEvidence) {
  const byLower = new Map()
  const parts = Array.isArray(assemblyEvidence?.parts) ? assemblyEvidence.parts : []
  for (const part of parts) {
    if (!part?.path) continue
    const bodies = Array.isArray(part?.after?.bodies) ? part.after.bodies : []
    if (bodies.length === 0) continue
    let minX = Infinity
    let minY = Infinity
    let minZ = Infinity
    let maxX = -Infinity
    let maxY = -Infinity
    let maxZ = -Infinity
    for (const body of bodies) {
      const b = body?.boxMm
      if (!Array.isArray(b) || b.length < 6) continue
      minX = Math.min(minX, b[0], b[3])
      minY = Math.min(minY, b[1], b[4])
      minZ = Math.min(minZ, b[2], b[5])
      maxX = Math.max(maxX, b[0], b[3])
      maxY = Math.max(maxY, b[1], b[4])
      maxZ = Math.max(maxZ, b[2], b[5])
    }
    if (!Number.isFinite(minX)) continue
    const relLower = normalizePartRelPath(part.path).toLowerCase()
    byLower.set(relLower, [round2(maxX - minX), round2(maxY - minY), round2(maxZ - minZ)])
  }
  return byLower
}

export function extractInstalledPartSummary(assemblyEvidence) {
  const manifest = Array.isArray(assemblyEvidence?.expectedManifest) ? assemblyEvidence.expectedManifest : []
  const byLower = new Map()
  const materials = extractMaterialsFromEvidence(assemblyEvidence)
  const bounds3d = extract3dBoundsFromEvidence(assemblyEvidence)

  for (const entry of manifest) {
    const canonicalRel = normalizePartRelPath(entry?.path)
    if (!canonicalRel) continue
    const relLower = canonicalRel.toLowerCase()
    let rec = byLower.get(relLower)
    if (!rec) {
      rec = {
        relPath: canonicalRel,
        relLower,
        fileName: basename(canonicalRel),
        quantity: 0,
        material: materials.get(relLower) || '',
        size3dMm: bounds3d.get(relLower) || null,
        identities: [],
      }
      byLower.set(relLower, rec)
    }
    rec.quantity += 1
    if (entry?.identity && rec.identities.length < 4 && !rec.identities.includes(entry.identity)) {
      rec.identities.push(entry.identity)
    }
  }

  const items = [...byLower.values()]
  const installedRelPaths = new Set(items.map((item) => item.relPath))
  const installedRelLower = new Set(items.map((item) => item.relLower))
  return {
    hasManifest: items.length > 0,
    totalInstanceCount: manifest.length,
    uniquePartCount: items.length,
    installedRelPaths,
    installedRelLower,
    byLower,
    items,
  }
}

function escapeCsvCell(value) {
  const str = String(value ?? '')
  return /[",\r\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str
}

function formatDimsMm(dims) {
  if (!Array.isArray(dims) || dims.length === 0 || dims.some((v) => v === null || v === undefined || !Number.isFinite(Number(v)))) {
    return '-'
  }
  return dims.map((v) => Number(v).toFixed(2).replace(/\.00$/, '')).join(' × ')
}

export function buildDeliverableBom({ installedSummary, dxfAuditReport }) {
  const dxfByPartLower = new Map()
  for (const row of dxfAuditReport?.results || []) {
    const partLower = dxfRelToPartRelLower(row.relPath)
    if (!dxfByPartLower.has(partLower)) dxfByPartLower.set(partLower, row)
  }

  const rawItems = installedSummary?.hasManifest
    ? installedSummary.items
    : (dxfAuditReport?.results || []).map((row) => ({
      relPath: normalizePartRelPath(row.relPath).replace(/\.flat\.dxf$/i, '.SLDPRT'),
      relLower: dxfRelToPartRelLower(row.relPath),
      fileName: basename(row.relPath).replace(/\.flat\.dxf$/i, '.SLDPRT'),
      quantity: 1,
      material: '',
      size3dMm: null,
      identities: [],
    }))

  const joined = rawItems.map((item) => {
    const dxf = dxfByPartLower.get(item.relLower) || null
    const subsystem = classifySubsystem(item.relPath)
    const hasDxf = Boolean(dxf)
    return {
      orderWeight: (hasDxf ? 0 : 100) + subsystem.order,
      category: hasDxf ? '钣金激光下料件' : '五金/标准/外购件',
      subsystem: subsystem.label,
      partName: item.fileName.replace(/\.(sldprt|flat\.dxf)$/i, ''),
      quantity: item.quantity,
      material: item.material || (hasDxf ? '钣金件' : '五金/标准件'),
      size3dMm: item.size3dMm,
      flatSizeMm: hasDxf && dxf.flatWidthMm != null && dxf.flatHeightMm != null
        ? [dxf.flatWidthMm, dxf.flatHeightMm]
        : null,
      hasDxf,
      bendEntityCount: hasDxf ? Number(dxf.bendEntityCount ?? 0) : 0,
      markEntityCount: hasDxf ? Number(dxf.markEntityCount ?? 0) : 0,
      removedFastenerBoresCount: hasDxf ? Number(dxf.removedFastenerBoresCount ?? 0) : 0,
      formTools: hasDxf ? (dxf.formTools || []) : [],
      dxfRelPath: hasDxf ? partRelToDxfRel(item.relPath) : '-',
      cadRelPath: item.relPath,
    }
  })

  joined.sort((a, b) => (a.orderWeight - b.orderWeight) || a.cadRelPath.localeCompare(b.cadRelPath, 'zh-Hans-CN'))
  const bomItems = joined.map((entry, idx) => ({
    index: idx + 1,
    ...entry,
  }))
  const sheetMetalItems = bomItems.filter((i) => i.hasDxf)
  const nonSheetMetalItems = bomItems.filter((i) => !i.hasDxf)

  return {
    totalInstanceCount: bomItems.reduce((sum, item) => sum + item.quantity, 0),
    uniquePartCount: bomItems.length,
    sheetMetalUniqueCount: sheetMetalItems.length,
    sheetMetalInstanceCount: sheetMetalItems.reduce((sum, item) => sum + item.quantity, 0),
    nonSheetMetalUniqueCount: nonSheetMetalItems.length,
    nonSheetMetalInstanceCount: nonSheetMetalItems.reduce((sum, item) => sum + item.quantity, 0),
    bomItems,
  }
}

export function formatBomCsv(bomSummary) {
  const headers = [
    '序号',
    '类别',
    '子系统',
    '零件名称',
    '单台数量',
    '材质',
    '3D包络尺寸(mm)',
    'DXF展开尺寸(宽×高 mm)',
    '折弯线数(BEND)',
    '模具打标数(MARK)',
    '压铆预孔过滤数',
    'DXF相对路径',
    '3D模型相对路径',
  ]
  const lines = [headers.map(escapeCsvCell).join(',')]
  for (const item of bomSummary.bomItems) {
    lines.push([
      item.index,
      item.category,
      item.subsystem,
      item.partName,
      item.quantity,
      item.material,
      formatDimsMm(item.size3dMm),
      formatDimsMm(item.flatSizeMm),
      item.hasDxf ? item.bendEntityCount : '-',
      item.hasDxf ? item.markEntityCount : '-',
      item.hasDxf ? item.removedFastenerBoresCount : '-',
      item.hasDxf ? item.dxfRelPath : '-',
      item.cadRelPath,
    ].map(escapeCsvCell).join(','))
  }
  return '\uFEFF' + lines.join('\r\n') + '\r\n'
}

export function formatBomMarkdown(bomSummary, requestSummary, assemblyEvidence) {
  const reqCab = requestSummary?.cabinet || {}
  const layoutSummary = requestSummary?.layoutSummary || {}
  const lines = [
    `# 16029 全参数柜体 BOM 与激光下料清单`,
    '',
    `- **任务编号**: \`${requestSummary?.taskId || '-'}\``,
    `- **定制规格**: \`${reqCab.widthMm}W × ${reqCab.heightMm}H × ${reqCab.depthMm}D mm\` | **门叶宽**: \`${layoutSummary.doorLeafWidthMm ?? '-'} mm\` | **门格**: \`L${layoutSummary.columnDoorCounts?.[0] ?? '-'} / R${layoutSummary.columnDoorCounts?.[1] ?? '-'}\``,
    `- **总装实测外廓**: \`${assemblyEvidence?.measuredEnvelopeMm?.widthMm?.toFixed?.(3) ?? '-'} × ${assemblyEvidence?.measuredEnvelopeMm?.heightMm?.toFixed?.(3) ?? '-'} × ${assemblyEvidence?.measuredEnvelopeMm?.depthMm?.toFixed?.(3)} mm\``,
    `- **装机统计**: 共 **${bomSummary.totalInstanceCount} 件**实例 / **${bomSummary.uniquePartCount} 种**唯一零件（钣金激光下料件 **${bomSummary.sheetMetalUniqueCount} 种 / ${bomSummary.sheetMetalInstanceCount} 件** + 五金/标准/外购件 **${bomSummary.nonSheetMetalUniqueCount} 种 / ${bomSummary.nonSheetMetalInstanceCount} 件**）`,
    `- **发布状态**: \`production_release = false\`（工程审图/辅助确认模型，非直接生产放行）`,
    '',
    '## 一、钣金激光下料件（含展开 DXF）',
    '',
    '| 序号 | 子系统 | 零件名称 | 单台数量 | 材质 | 3D尺寸 (mm) | DXF展开尺寸 (mm) | BEND | MARK | 预孔过滤 | DXF 路径 |',
    '|---:|---|---|---:|---|---|---|---:|---:|---:|---|',
  ]
  for (const item of bomSummary.bomItems.filter((x) => x.hasDxf)) {
    lines.push(`| ${item.index} | ${item.subsystem} | ${item.partName} | ${item.quantity} | ${item.material} | ${formatDimsMm(item.size3dMm)} | ${formatDimsMm(item.flatSizeMm)} | ${item.bendEntityCount} | ${item.markEntityCount} | ${item.removedFastenerBoresCount} | \`DXF_展开图/${item.dxfRelPath.replace(/\\/g, '/')}\` |`)
  }
  const hardwareItems = bomSummary.bomItems.filter((x) => !x.hasDxf)
  if (hardwareItems.length > 0) {
    lines.push(
      '',
      '## 二、五金 / 标准 / 外购件（无展开 DXF）',
      '',
      '| 序号 | 子系统 | 零件名称 | 单台数量 | 材质 | 3D尺寸 (mm) | 3D 模型路径 |',
      '|---:|---|---|---:|---|---|---|',
    )
    for (const item of hardwareItems) {
      lines.push(`| ${item.index} | ${item.subsystem} | ${item.partName} | ${item.quantity} | ${item.material} | ${formatDimsMm(item.size3dMm)} | \`3D_CAD/${item.cadRelPath.replace(/\\/g, '/')}\` |`)
    }
  }
  return lines.join('\n') + '\n'
}

export function formatCadEntryGuide() {
  return [
    'Winnsen 16029 3D CAD 总装打开说明',
    '====================================',
    '1. 唯一总装入口文件：请直接在 SolidWorks 2020 中打开本目录根目录下的：',
    '   16029_variable_full_cabinet.SLDASM',
    '',
    '2. 关于 structure\\ 子目录中的参考与闭包文件：',
    '   为确保打开任何参数化派生/镜像零件及总装时不出现外部参考断链弹窗，structure\\ 中保留了',
    '   部分母板驱动件与源参考装配体（如 标准寄存柜 模型.SLDPRT、箱体竖隔板R参数源.SLDPRT、',
    '   *_W437_H471p25.*、16029_1000W_2000H_500D.SLDASM、各类焊接子装壳体等）。',
    '   这些参考文件仅用于维持外部引用链路完整性，并未实际装配在本定制柜体内。',
    '',
    '3. 本定制柜体实际装机清单（共 42 种零件 / 200 件实例）及下料数量，',
    '   请以交付包根目录《BOM_零件与下料清单.csv》（或 .md）及《DXF_展开图/》为准。',
  ].join('\r\n') + '\r\n'
}

function collectFilesRecursively(rootDir, predicate) {
  const results = []
  function walk(currentDir) {
    const entries = readdirSync(currentDir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))
    for (const entry of entries) {
      const fullPath = join(currentDir, entry.name)
      if (entry.isDirectory()) walk(fullPath)
      else if (!predicate || predicate(fullPath)) results.push(fullPath)
    }
  }
  if (existsSync(rootDir)) walk(rootDir)
  return results
}

function copyTreeFiltered(sourceDir, targetDir, filterFn = null) {
  mkdirSync(targetDir, { recursive: true })
  const sourceFiles = collectFilesRecursively(sourceDir)
  let copiedCount = 0
  for (const src of sourceFiles) {
    const rel = relative(sourceDir, src)
    if (filterFn && !filterFn(src, rel)) continue
    const dst = join(targetDir, rel)
    mkdirSync(dirname(dst), { recursive: true })
    copyFileSync(src, dst)
    copiedCount += 1
  }
  return copiedCount
}

export function auditFlatDxfDirectory(dxfDir, expectedParts = [], { pythonExe = 'python', spawnSyncFn = spawnSync, installedRelPaths = null } = {}) {
  if (!existsSync(dxfDir)) {
    return {
      pass: false,
      error: 'DXF_OUTPUT_DIR_MISSING',
      identityPass: false,
      expectedCountMatches: false,
      sheetMetalCount: 0,
      dxfFileCount: 0,
      dxfAuditPassCount: 0,
      dxfAuditFailCount: 0,
      identityMatchedCount: 0,
      missingExportRecords: [],
      missingDxfFiles: [],
      unexpectedDxfFiles: [],
      duplicateMappings: [],
      pathIdentityMismatches: [],
      shaMismatches: [],
      identityErrors: ['DXF_OUTPUT_DIR_MISSING'],
      totalMarkEntities: 0,
      totalProductionMarkEntities: 0,
      formToolMarkParts: 0,
      totalRemovedFastenerBores: 0,
      excludedUninstalledFiles: [],
      results: [],
    }
  }

  const installedByLower = new Map()
  if (installedRelPaths) {
    for (const item of installedRelPaths) {
      const norm = normalizePartRelPath(item)
      if (norm) installedByLower.set(norm.toLowerCase(), norm)
    }
  }
  const installedLower = installedByLower.size > 0 ? new Set(installedByLower.keys()) : null

  const allDxfFiles = collectFilesRecursively(dxfDir, (p) => p.toLowerCase().endsWith('.flat.dxf'))
  const excludedUninstalledFiles = []
  const dxfFiles = installedLower
    ? allDxfFiles.filter((dxfPath) => {
      const rel = normalizeDxfRelPath(relative(dxfDir, dxfPath))
      const keep = installedLower.has(dxfRelToPartRelLower(rel))
      if (!keep) excludedUninstalledFiles.push(rel)
      return keep
    })
    : allDxfFiles

  const missingExportRecords = []
  const missingDxfFiles = []
  const unexpectedDxfFiles = []
  const duplicateMappings = []
  const pathIdentityMismatches = []
  const shaMismatches = []

  const rawExpected = Array.isArray(expectedParts) ? expectedParts : []
  const seenExpectedPartLower = new Set()
  const duplicatePartLower = new Set()
  const classifiedPartLower = new Set()
  const sheetMetalExpected = []
  const expectedByDxfRelLower = new Map()
  const duplicateDxfRelLower = new Set()

  for (const item of rawExpected) {
    const normPartRel = normalizePartRelPath(item?.relPath)
    if (!normPartRel) {
      pathIdentityMismatches.push({
        type: 'missing_record_rel_path',
        relPath: '',
        flatPath: String(item?.flatPath || ''),
      })
      continue
    }
    const partLower = normPartRel.toLowerCase()
    if (installedLower && !installedLower.has(partLower)) continue

    classifiedPartLower.add(partLower)
    if (seenExpectedPartLower.has(partLower)) {
      duplicatePartLower.add(partLower)
      duplicateMappings.push({
        type: 'duplicate_part_record',
        relPath: normPartRel,
        partRelLower: partLower,
      })
    } else {
      seenExpectedPartLower.add(partLower)
    }

    if (item?.classification !== 'sheet_metal') continue

    const expectedDxfRel = partRelToDxfRel(normPartRel)
    const expectedDxfRelLower = expectedDxfRel.toLowerCase()
    const recordedFlatRel = normalizeFlatRelFromRecord(item?.flatPath, dxfDir)
    const recordedFlatRelLower = recordedFlatRel.toLowerCase()
    const pathIdentityMatches = !recordedFlatRel || recordedFlatRelLower === expectedDxfRelLower
    if (!pathIdentityMatches) {
      pathIdentityMismatches.push({
        type: 'record_flat_path_mismatch',
        relPath: normPartRel,
        expectedDxfRelPath: expectedDxfRel,
        recordedFlatRelPath: recordedFlatRel,
      })
    }

    const expectedSha256 = String(item?.sha256 || '').trim().toUpperCase()
    const entry = {
      raw: item,
      relPath: normPartRel,
      partLower,
      expectedDxfRel,
      expectedDxfRelLower,
      recordedFlatRel,
      pathIdentityMatches,
      expectedSha256,
    }
    sheetMetalExpected.push(entry)

    if (expectedByDxfRelLower.has(expectedDxfRelLower)) {
      duplicateDxfRelLower.add(expectedDxfRelLower)
      duplicateMappings.push({
        type: 'duplicate_dxf_mapping',
        relPath: normPartRel,
        dxfRelPath: expectedDxfRel,
        existingRelPath: expectedByDxfRelLower.get(expectedDxfRelLower).relPath,
      })
    } else {
      expectedByDxfRelLower.set(expectedDxfRelLower, entry)
    }
  }

  if (installedLower) {
    for (const [partLower, normPart] of installedByLower.entries()) {
      if (!classifiedPartLower.has(partLower)) {
        missingExportRecords.push({
          relPath: normPart,
          reason: 'INSTALLED_PART_MISSING_EXPORT_RECORD',
        })
      }
    }
  }

  const matchedExpectedDxfLower = new Set()
  const results = []
  let passCount = 0
  let failCount = 0

  for (const dxfPath of dxfFiles) {
    const dxfRel = normalizeDxfRelPath(relative(dxfDir, dxfPath))
    const dxfRelLower = dxfRel.toLowerCase()
    const expected = expectedByDxfRelLower.get(dxfRelLower) || null
    const actualSha = sha256File(dxfPath)
    const recordMatched = Boolean(expected)
    const isDuplicateMapping = Boolean(
      expected && (duplicateDxfRelLower.has(dxfRelLower) || duplicatePartLower.has(expected.partLower)),
    )
    const pathIdentityMatches = Boolean(expected && expected.pathIdentityMatches)
    const expectedShaValid = Boolean(expected && /^[A-F0-9]{64}$/.test(expected.expectedSha256))
    const shaMatches = Boolean(recordMatched && expectedShaValid && expected.expectedSha256 === actualSha)

    if (recordMatched) {
      matchedExpectedDxfLower.add(dxfRelLower)
      if (!shaMatches) {
        shaMismatches.push({
          relPath: expected.relPath,
          dxfRelPath: dxfRel,
          expectedSha256: expected.expectedSha256 || '',
          actualSha256: actualSha,
        })
      }
    } else {
      unexpectedDxfFiles.push({
        dxfRelPath: dxfRel,
        derivedPartRelPath: dxfRelToPartRelLower(dxfRel),
        flatPath: dxfPath,
        sha256: actualSha,
        reason: 'UNEXPECTED_OR_UNRECORDED_DXF_FILE',
      })
    }

    const proc = spawnSyncFn(pythonExe, [DXF_CHECK_SCRIPT, dxfPath], {
      cwd: ROOT,
      windowsHide: true,
      encoding: 'utf8',
    })
    let report = null
    let parseError = ''
    try {
      report = JSON.parse(String(proc.stdout || '').trim())
    } catch (error) {
      parseError = error instanceof Error ? error.message : String(error)
    }

    const splineCount = Number(report?.entityTypeCounts?.SPLINE || 0)
    const openChains = Number(report?.closedContours?.openChainCount ?? -1)
    const dupCount = Array.isArray(report?.duplicateSegments) ? report.duplicateSegments.length : -1
    const ixCount = Array.isArray(report?.cutIntersections) ? report.cutIntersections.length : -1
    const zeroCount = Array.isArray(report?.zeroLengthSegments) ? report.zeroLengthSegments.length : -1
    const unsupportedCount = Array.isArray(report?.unsupportedEntities) ? report.unsupportedEntities.length : -1
    const insunits = Number(report?.units?.insunits ?? -1)

    const geometryPass = Boolean(
      proc.status === 0
      && report?.auditPassed === true
      && insunits === 4
      && report?.units?.verified === true
      && splineCount === 0
      && openChains === 0
      && dupCount === 0
      && ixCount === 0
      && zeroCount === 0
      && unsupportedCount === 0,
    )

    const fileErrors = []
    if (parseError) fileErrors.push(parseError)
    if (Array.isArray(report?.errors)) fileErrors.push(...report.errors)
    if (!recordMatched) fileErrors.push('UNEXPECTED_OR_UNRECORDED_DXF_FILE')
    else {
      if (!expectedShaValid) fileErrors.push('EXPECTED_SHA256_MISSING_OR_INVALID')
      else if (!shaMatches) fileErrors.push('SHA256_MISMATCH')
      if (!pathIdentityMatches) fileErrors.push('RECORD_FLAT_PATH_IDENTITY_MISMATCH')
      if (isDuplicateMapping) fileErrors.push('DUPLICATE_DXF_MAPPING')
    }

    const auditPass = Boolean(
      geometryPass
      && recordMatched
      && shaMatches
      && pathIdentityMatches
      && !isDuplicateMapping,
    )

    if (auditPass) passCount += 1
    else failCount += 1

    const extents = report?.measurements?.allExtents || report?.measurements?.cutExtents || {}
    results.push({
      relPath: expected?.relPath || dxfRel,
      dxfRelPath: dxfRel,
      flatPath: dxfPath,
      recordMatched,
      pathIdentityMatches,
      duplicateMapping: isDuplicateMapping,
      expectedSha256: expected?.expectedSha256 || '',
      sha256: actualSha,
      shaMatches,
      geometryPassed: geometryPass,
      auditPassed: auditPass,
      insunits,
      flatWidthMm: round2(extents.width),
      flatHeightMm: round2(extents.height),
      roundHoleCount: Number(report?.roundHoleCount ?? 0),
      closedContourCount: Number(report?.closedContours?.closedContourCount ?? 0),
      cutEntityCount: Number(report?.cuts?.entityCount ?? 0),
      bendEntityCount: Number(report?.bends?.segmentCount ?? 0),
      markEntityCount: Number(report?.marks?.entityCount ?? expected?.raw?.markEntityCount ?? 0),
      productionMarkEntityCount: Number(expected?.raw?.productionMarkEntityCount ?? 0),
      removedFastenerBoresCount: Number(expected?.raw?.removedFastenerBoresCount ?? 0),
      formTools: expected?.raw?.formTools || [],
      splineCount,
      openChainCount: openChains,
      duplicateSegmentCount: dupCount,
      cutIntersectionCount: ixCount,
      zeroLengthCount: zeroCount,
      unsupportedEntityCount: unsupportedCount,
      errors: fileErrors,
    })
  }

  for (const entry of sheetMetalExpected) {
    if (!matchedExpectedDxfLower.has(entry.expectedDxfRelLower)) {
      missingDxfFiles.push({
        relPath: entry.relPath,
        expectedDxfRelPath: entry.expectedDxfRel,
        expectedSha256: entry.expectedSha256,
        reason: 'MISSING_DXF_FILE',
      })
    }
  }

  const identityErrors = []
  if (sheetMetalExpected.length === 0) identityErrors.push('EXPECTED_SHEET_METAL_MANIFEST_EMPTY')
  if (missingExportRecords.length > 0) identityErrors.push(`MISSING_EXPORT_RECORDS:${missingExportRecords.length}`)
  if (missingDxfFiles.length > 0) identityErrors.push(`MISSING_DXF_FILES:${missingDxfFiles.length}`)
  if (unexpectedDxfFiles.length > 0) identityErrors.push(`UNEXPECTED_DXF_FILES:${unexpectedDxfFiles.length}`)
  if (duplicateMappings.length > 0) identityErrors.push(`DUPLICATE_MAPPINGS:${duplicateMappings.length}`)
  if (pathIdentityMismatches.length > 0) identityErrors.push(`PATH_IDENTITY_MISMATCHES:${pathIdentityMismatches.length}`)
  if (shaMismatches.length > 0) identityErrors.push(`SHA256_MISMATCHES:${shaMismatches.length}`)

  const identityMatchedCount = matchedExpectedDxfLower.size
  const uniqueExpectedCount = expectedByDxfRelLower.size
  const identityPass = Boolean(
    sheetMetalExpected.length > 0
    && identityErrors.length === 0
    && identityMatchedCount === uniqueExpectedCount
    && dxfFiles.length === uniqueExpectedCount,
  )
  const expectedCountMatches = identityPass
  const totalMarkEntities = results.reduce((sum, item) => sum + item.markEntityCount, 0)
  const totalProductionMarkEntities = results.reduce((sum, item) => sum + item.productionMarkEntityCount, 0)
  const formToolMarkParts = results.filter((item) => item.markEntityCount > 0).length
  const totalRemovedFastenerBores = results.reduce((sum, item) => sum + item.removedFastenerBoresCount, 0)

  return {
    pass: dxfFiles.length > 0 && failCount === 0 && identityPass,
    identityPass,
    expectedCountMatches,
    sheetMetalCount: uniqueExpectedCount,
    dxfFileCount: dxfFiles.length,
    dxfAuditPassCount: passCount,
    dxfAuditFailCount: failCount,
    identityMatchedCount,
    missingExportRecords,
    missingDxfFiles,
    unexpectedDxfFiles,
    duplicateMappings,
    pathIdentityMismatches,
    shaMismatches,
    identityErrors,
    totalMarkEntities,
    totalProductionMarkEntities,
    formToolMarkParts,
    totalRemovedFastenerBores,
    excludedUninstalledFiles,
    results,
  }
}

export function formatPackageReadme({ requestSummary, assemblyEvidence, dxfAuditReport, drawingSummary = null, bomSummary }) {
  const drawingLines = drawingSummary && Number(drawingSummary.drawingPdfCount) > 0
    ? [
      `2D 工程图: 共 ${drawingSummary.drawingSlddrwCount} 张 SLDDRW + ${drawingSummary.drawingPdfCount} 份 PDF（含总装多视图与各唯一门板折弯态/展开态/工艺坐标表）`,
    ]
    : []
  const drawingFileList = drawingSummary?.files?.length
    ? [
      '',
      '2D 工程图目录（2D_工程图/）:',
      ...drawingSummary.files.map((item) => `  - 2D_工程图/${item}`),
    ]
    : []
  return [
    'Winnsen 16029 全尺寸参数化工程辅助包（3D + 钣金展开 DXF + 2D 工程图 + BOM 下料清单）',
    `发布状态: production_release = false（工程审图/辅助确认模型，非直接生产放行）`,
    `规格: ${requestSummary.cabinet.widthMm}W × ${requestSummary.cabinet.heightMm}H × ${requestSummary.cabinet.depthMm}D mm | 门叶宽 ${requestSummary.layoutSummary?.doorLeafWidthMm} mm | 门格 L${requestSummary.layoutSummary?.columnDoorCounts?.[0]} / R${requestSummary.layoutSummary?.columnDoorCounts?.[1]}`,
    `实测外廓: ${assemblyEvidence.measuredEnvelopeMm?.widthMm?.toFixed?.(3)} × ${assemblyEvidence.measuredEnvelopeMm?.heightMm?.toFixed?.(3)} × ${assemblyEvidence.measuredEnvelopeMm?.depthMm?.toFixed?.(3)} mm`,
    `3D 校验: structuralCollisionCount = ${assemblyEvidence.interferenceClassification?.structuralCollisionCount ?? 0}, missingMaterialCount = ${assemblyEvidence.materialCheck?.missingMaterialCount ?? 0}`,
    `装机 BOM: 共 ${bomSummary.totalInstanceCount} 件实例 / ${bomSummary.uniquePartCount} 种零件（钣金 ${bomSummary.sheetMetalUniqueCount} 种 ${bomSummary.sheetMetalInstanceCount} 件 + 五金/标准件 ${bomSummary.nonSheetMetalUniqueCount} 种 ${bomSummary.nonSheetMetalInstanceCount} 件）`,
    `DXF 审计: ${dxfAuditReport.dxfAuditPassCount}/${dxfAuditReport.sheetMetalCount} 全部通过 (已精准过滤至实际装机钣金件；单位 mm, SPLINE=0, 断链=0, 重影=0)`,
    ...drawingLines,
    `Q3 模具与生产工艺 MARK 分层: ${dxfAuditReport.formToolMarkParts} 个零件共 ${dxfAuditReport.totalMarkEntities} 个打标图元已归入 MARK 层（其中焊接定位/方向打标 ${dxfAuditReport.totalProductionMarkEntities ?? 0} 个）`,
    `Q4 压铆预孔过滤: 已清理 ${dxfAuditReport.totalRemovedFastenerBores} 个同心内孔，仅保留外圈激光预孔`,
    `4项生产工艺集成: 左门R1.5半圆/右门3×1.5三角缺口 + 门上销孔内偏0.5mm(prebias=${assemblyEvidence.interferenceClassification?.approvedUpperPinPrebiasCount ?? 0}) + 横层板去Ø2定位孔 + 3D/DXF激光焊接打标`,
    '',
    '目录与文件说明:',
    '  BOM_零件与下料清单.csv      - Excel 可直接打开的完整装机 BOM、单台件数及展开尺寸表（UTF-8 BOM）',
    '  BOM_零件与下料清单.md       - Markdown 格式工程审图 BOM 速查表',
    '  3D_CAD/                     - SolidWorks 2020 完整装配包；请打开根目录 16029_variable_full_cabinet.SLDASM',
    '                                （已移除重复的 assembly-evidence.json；structure\\ 内保留的母板仅维持外部参考链路）',
    '  DXF_展开图/                 - 仅包含实际装机钣金件的平展 DXF（.flat.dxf，含 CUT/BEND/MARK 图层）',
    '  2D_工程图/                  - 总装配体多视图及各唯一门板 2D 工程图（.SLDDRW 原生图 + .pdf 审图文件）',
    '  reports/                    - 3D 干涉与尺寸测量报告、DXF 全检报告及文件 SHA-256 清单',
    ...drawingFileList,
    '',
    '装机钣金下料速查（序号 | 零件 | 单台数量 | DXF 展开宽×高 mm | DXF 路径）:',
    ...bomSummary.bomItems
      .filter((item) => item.hasDxf)
      .map((item) => `  ${String(item.index).padStart(2, '0')}. ${item.partName} × ${item.quantity} | ${formatDimsMm(item.flatSizeMm)} mm | ${item.dxfRelPath}`),
  ].join('\r\n') + '\r\n'
}

export function packageVariableTrialDeliverable({
  archiveDir,
  zipFileName,
  buildCadAssemblyDir,
  dxfOutputDir,
  drawingOutputDir = null,
  drawingExportSummary = null,
  assemblyEvidence,
  dxfAuditReport,
  requestSummary,
}) {
  mkdirSync(archiveDir, { recursive: true })
  const stageDir = join(archiveDir, '.zip-stage-' + Date.now())
  const zipPath = join(archiveDir, zipFileName)
  rmSync(stageDir, { recursive: true, force: true })
  rmSync(zipPath, { force: true })

  try {
    const installedSummary = extractInstalledPartSummary(assemblyEvidence)
    copyTreeFiltered(
      buildCadAssemblyDir,
      join(stageDir, '3D_CAD'),
      (_src, rel) => basename(rel).toLowerCase() !== 'assembly-evidence.json',
    )
    if (drawingOutputDir && existsSync(join(drawingOutputDir, '.cad_flat_config_parts'))) {
      copyTreeFiltered(join(drawingOutputDir, '.cad_flat_config_parts'), join(stageDir, '3D_CAD'))
    }
    writeFileSync(join(stageDir, '3D_CAD', '请打开此总装_说明.txt'), formatCadEntryGuide(), 'utf8')

    const auditedDxfByLower = new Map()
    for (const row of dxfAuditReport?.results || []) {
      if (row?.auditPassed !== true) continue
      const key = normalizeDxfRelPath(row.dxfRelPath || partRelToDxfRel(row.relPath)).toLowerCase()
      if (key) auditedDxfByLower.set(key, String(row.sha256 || '').toUpperCase())
    }
    const dxfFileCount = copyTreeFiltered(
      dxfOutputDir,
      join(stageDir, 'DXF_展开图'),
      (src, rel) => {
        if (!src.toLowerCase().endsWith('.flat.dxf')) return false
        const normRelLower = normalizeDxfRelPath(rel).toLowerCase()
        if (auditedDxfByLower.size > 0) return auditedDxfByLower.has(normRelLower)
        if (!installedSummary.hasManifest) return true
        return installedSummary.installedRelLower.has(dxfRelToPartRelLower(rel))
      },
    )

    let drawingFileCount = 0
    if (drawingOutputDir && existsSync(drawingOutputDir)) {
      drawingFileCount = copyTreeFiltered(
        drawingOutputDir,
        join(stageDir, '2D_工程图'),
        (src, rel) => !rel.startsWith('.cad_flat_config_parts') && /\.(slddrw|pdf)$/i.test(src),
      )
    }

    const reportsDir = join(stageDir, 'reports')
    mkdirSync(reportsDir, { recursive: true })

    const bomSummary = buildDeliverableBom({ installedSummary, dxfAuditReport })
    const guideRelLower = '请打开此总装_说明.txt'
    const allCadFiles = collectFilesRecursively(join(stageDir, '3D_CAD'))
    const cadFileCount = allCadFiles.filter((p) => basename(p) !== guideRelLower).length
    const cadInventory = allCadFiles.map((path) => ({
      relPath: relative(join(stageDir, '3D_CAD'), path),
      sha256: sha256File(path),
      sizeBytes: statSync(path).size,
    }))
    const dxfInventory = collectFilesRecursively(join(stageDir, 'DXF_展开图')).map((path) => ({
      relPath: relative(join(stageDir, 'DXF_展开图'), path),
      sha256: sha256File(path),
      sizeBytes: statSync(path).size,
    }))
    if (auditedDxfByLower.size > 0) {
      if (dxfInventory.length !== auditedDxfByLower.size) {
        throw new Error(`DELIVERABLE_DXF_COUNT_MISMATCH staged=${dxfInventory.length} audited=${auditedDxfByLower.size}`)
      }
      for (const item of dxfInventory) {
        const key = normalizeDxfRelPath(item.relPath).toLowerCase()
        const expectedSha = auditedDxfByLower.get(key)
        if (!expectedSha || expectedSha !== item.sha256) {
          throw new Error(`DELIVERABLE_DXF_IDENTITY_MISMATCH ${item.relPath}`)
        }
      }
    }
    const drawingInventory = collectFilesRecursively(join(stageDir, '2D_工程图')).map((path) => {
      if (path.toLowerCase().endsWith('.pdf')) {
        const hdr = readFileSync(path).subarray(0, 5).toString('ascii')
        if (hdr !== '%PDF-') throw new Error('DRAWING_PDF_HEADER_INVALID ' + path)
      }
      return {
        relPath: relative(join(stageDir, '2D_工程图'), path),
        sha256: sha256File(path),
        sizeBytes: statSync(path).size,
      }
    })
    const drawingSlddrwCount = drawingInventory.filter((i) => i.relPath.toLowerCase().endsWith('.slddrw')).length
    const drawingPdfCount = drawingInventory.filter((i) => i.relPath.toLowerCase().endsWith('.pdf')).length
    const drawingSummary = {
      drawingFileCount,
      drawingSlddrwCount,
      drawingPdfCount,
      files: drawingInventory.map((i) => i.relPath.replace(/\\/g, '/')),
      details: drawingExportSummary?.drawings || [],
    }

    const deliveryManifest = {
      schema: 'winnsen.locker16029.variable_trial_delivery.v1',
      production_release: false,
      createdAt: new Date().toISOString(),
      request: requestSummary,
      assemblySummary: {
        measuredEnvelopeMm: assemblyEvidence.measuredEnvelopeMm,
        doorLeafWidthMm: assemblyEvidence.layout?.doorLeafWidthMm,
        columnDoorCounts: assemblyEvidence.layout?.columnDoorCounts,
        interferencePass: assemblyEvidence.interferencePass,
        structuralCollisionCount: assemblyEvidence.interferenceClassification?.structuralCollisionCount ?? 0,
        unapprovedInterferenceCount: assemblyEvidence.interferenceClassification?.unapprovedInterferenceCount ?? 0,
        belowFloorNonFootPass: assemblyEvidence.belowFloorNonFootPass,
        missingMaterialCount: assemblyEvidence.materialCheck?.missingMaterialCount ?? 0,
        inputFilesUnchanged: assemblyEvidence.inputFilesUnchanged,
      },
      dxfSummary: {
        sheetMetalCount: dxfAuditReport.sheetMetalCount,
        dxfFileCount,
        dxfAuditPassCount: dxfAuditReport.dxfAuditPassCount,
        dxfAuditFailCount: dxfAuditReport.dxfAuditFailCount,
        formToolMarkParts: dxfAuditReport.formToolMarkParts,
        totalMarkEntities: dxfAuditReport.totalMarkEntities,
        totalRemovedFastenerBores: dxfAuditReport.totalRemovedFastenerBores,
        excludedUninstalledFiles: dxfAuditReport.excludedUninstalledFiles || [],
      },
      drawingSummary,
      bomSummary,
      cadFileCount,
      dxfFileCount,
      drawingFileCount,
      drawingSlddrwCount,
      drawingPdfCount,
      cadInventory,
      dxfInventory,
      drawingInventory,
    }

    writeFileSync(join(reportsDir, 'assembly-evidence.json'), JSON.stringify(assemblyEvidence, null, 2) + '\n', 'utf8')
    writeFileSync(join(reportsDir, 'flat-dxf-audit-report.json'), JSON.stringify(dxfAuditReport, null, 2) + '\n', 'utf8')
    writeFileSync(join(reportsDir, 'delivery-manifest.json'), JSON.stringify(deliveryManifest, null, 2) + '\n', 'utf8')
    writeFileSync(join(stageDir, 'BOM_零件与下料清单.csv'), formatBomCsv(bomSummary), 'utf8')
    writeFileSync(join(stageDir, 'BOM_零件与下料清单.md'), formatBomMarkdown(bomSummary, requestSummary, assemblyEvidence), 'utf8')
    writeFileSync(
      join(stageDir, 'README_工程说明.txt'),
      formatPackageReadme({ requestSummary, assemblyEvidence, dxfAuditReport, drawingSummary, bomSummary }),
      'utf8',
    )

    const psScript = [
      "Add-Type -AssemblyName System.IO.Compression.FileSystem",
      "$src = $env:VT_ZIP_SRC",
      "$dst = $env:VT_ZIP_DST",
      "if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Force }",
      "[System.IO.Compression.ZipFile]::CreateFromDirectory($src, $dst, [System.IO.Compression.CompressionLevel]::Optimal, $false, [System.Text.Encoding]::UTF8)",
      "$zip = [System.IO.Compression.ZipFile]::OpenRead($dst)",
      "try { $zip.Entries.Count } finally { $zip.Dispose() }",
    ].join('; ')

    const zipProc = spawnSync('powershell.exe', ['-NoProfile', '-Command', psScript], {
      env: { ...process.env, VT_ZIP_SRC: stageDir, VT_ZIP_DST: zipPath },
      windowsHide: true,
      encoding: 'utf8',
    })
    if (zipProc.status !== 0 || !existsSync(zipPath)) {
      throw new Error('ZIP_CREATION_FAILED: ' + String(zipProc.stderr || zipProc.stdout || '').trim())
    }
    const entryCount = Number(String(zipProc.stdout || '').trim().split(/\r?\n/).pop())
    // 3D_CAD files (cadFileCount + 1 guide) + dxfFileCount + drawingFileCount + 3 reports + README + 2 BOM files
    const expectedMinEntries = cadFileCount + dxfFileCount + drawingFileCount + 7
    if (!Number.isInteger(entryCount) || entryCount < expectedMinEntries) {
      throw new Error(`ZIP_ENTRY_COUNT_MISMATCH: expected >= ${expectedMinEntries}, got ${entryCount}`)
    }

    return {
      zipPath,
      zipFileName,
      sizeBytes: statSync(zipPath).size,
      sha256: sha256File(zipPath),
      cadFileCount,
      dxfFileCount,
      drawingFileCount,
      drawingSlddrwCount,
      drawingPdfCount,
      entryCount,
      bomUniquePartCount: bomSummary.uniquePartCount,
      bomTotalInstanceCount: bomSummary.totalInstanceCount,
    }
  } finally {
    rmSync(stageDir, { recursive: true, force: true })
  }
}

export async function runVariableTrialPackage(normalizedInput, { archiveDir, taskId, signal = null, onStage = null, hooks = {} }) {
  const reportStage = (stage, progressDetail) => {
    if (typeof onStage === 'function') onStage(stage, progressDetail)
  }

  mkdirSync(archiveDir, { recursive: true })
  const baseSpec = JSON.parse(readFileSync(BASE_SPEC_PATH, 'utf8'))
  const baseProcess = JSON.parse(readFileSync(BASE_PROCESS_PATH, 'utf8'))

  const taskSpec = {
    doorCases: baseSpec.doorCases,
    envelopeMode: 'parametric_envelope',
    assemblyCases: [normalizedInput.trialAssemblyCase],
  }
  const specPath = join(archiveDir, 'specification.json')
  const processPath = join(archiveDir, 'process.json')
  writeFileSync(specPath, JSON.stringify(taskSpec, null, 2) + '\n', 'utf8')
  writeFileSync(processPath, JSON.stringify(baseProcess, null, 2) + '\n', 'utf8')

  reportStage('building_3d', '正在执行 SolidWorks 2020 全参数化 3D 装配构建与干涉/外廓检验...')
  const runBuild = hooks.runBuild || ((sp, pp) => execute('assembly', sp, pp, {
    parametricEnvelope: true,
    caseIndex: 1,
    ...(signal ? { signal } : {}),
  }))
  const buildRes = await runBuild(specPath, processPath)
  const buildProtectionPath = join(buildRes.dir, 'protection-result.json')
  const buildProtection = existsSync(buildProtectionPath) ? JSON.parse(readFileSync(buildProtectionPath, 'utf8')) : { pass: false }
  if (buildRes.pass !== true || buildProtection.pass !== true) {
    throw new Error('BUILD_PROTECTION_OR_PHASE_FAILED')
  }

  const buildNativePath = join(buildRes.dir, 'phases', 'case1-build', 'native-result.json')
  const buildNative = JSON.parse(readFileSync(buildNativePath, 'utf8'))
  const minAsmEvent = [...(buildNative.events || [])].reverse().find((e) => e.inspection === 'minimum_assembly')
  const assemblyEvidence = minAsmEvent?.result
  if (!assemblyEvidence || assemblyEvidence.success !== true || assemblyEvidence.assemblyBuilt !== true) {
    throw new Error('ASSEMBLY_BUILD_EVIDENCE_INCOMPLETE')
  }
  if (assemblyEvidence.interferencePass !== true
      || Number(assemblyEvidence.interferenceClassification?.structuralCollisionCount ?? -1) !== 0
      || Number(assemblyEvidence.interferenceClassification?.unapprovedInterferenceCount ?? -1) !== 0) {
    throw new Error('ASSEMBLY_INTERFERENCE_GATE_FAILED')
  }
  if (assemblyEvidence.belowFloorNonFootPass !== true) {
    throw new Error('ASSEMBLY_BELOW_FLOOR_GATE_FAILED')
  }
  if (Number(assemblyEvidence.materialCheck?.missingMaterialCount ?? -1) !== 0) {
    throw new Error('ASSEMBLY_MATERIAL_CHECK_FAILED')
  }
  if (assemblyEvidence.inputFilesUnchanged !== true) {
    throw new Error('ASSEMBLY_INPUT_FILES_MODIFIED')
  }
  const reqCab = normalizedInput.normalizedRequest.cabinet
  const meas = assemblyEvidence.measuredEnvelopeMm || {}
  const tol = PORTAL_DIMENSION_LIMITS.envelopeToleranceMm
  if (Math.abs(Number(meas.widthMm) - reqCab.widthMm) > tol
      || Math.abs(Number(meas.heightMm) - reqCab.heightMm) > tol
      || Math.abs(Number(meas.depthMm) - reqCab.depthMm) > tol) {
    throw new Error(`ASSEMBLY_ENVELOPE_MISMATCH: expected ${reqCab.widthMm}x${reqCab.heightMm}x${reqCab.depthMm}, got ${meas.widthMm}x${meas.heightMm}x${meas.depthMm}`)
  }

  reportStage('exporting_dxf', '3D 检验通过，正在从隔离副本导出全套钣金展开 DXF 并执行 Q3/Q4 图层过滤...')
  const runDxf = hooks.runDxf || ((sourceCadDir) => runProtectedPhase({
    tag: 'T3-envelope-dxf',
    sourceDirectory: sourceCadDir,
    job: { action: 'assembly-flat-dxf', production_release: false, sourceBuildRun: buildRes.dir },
  }, { ...(signal ? { signal } : {}) }))
  const dxfRes = await runDxf(join(buildRes.dir, 'cad'))
  const dxfProtectionPath = join(dxfRes.dir, 'protection-result.json')
  const dxfProtection = existsSync(dxfProtectionPath) ? JSON.parse(readFileSync(dxfProtectionPath, 'utf8')) : { pass: false }
  if (dxfRes.pass !== true || dxfProtection.pass !== true) {
    throw new Error('DXF_EXPORT_PROTECTION_OR_PHASE_FAILED')
  }

  const dxfNative = JSON.parse(readFileSync(join(dxfRes.dir, 'native-result.json'), 'utf8'))
  const dxfExportEvent = [...(dxfNative.events || [])].reverse().find((e) => e.inspection === 'assembly_flat_dxfs')
  const dxfExportSummary = dxfExportEvent?.result
  if (!dxfExportSummary || dxfExportSummary.inputHashesUnchanged !== true || Number(dxfExportSummary.sheetMetalCount) <= 0) {
    throw new Error('DXF_EXPORT_EVIDENCE_INVALID')
  }

  reportStage('auditing_dxf', '正在逐一审计全套钣金展开 DXF（闭合性/断链/样条/单位/模具打标）...')
  const installedSummary = extractInstalledPartSummary(assemblyEvidence)
  const auditFn = hooks.auditDxf || auditFlatDxfDirectory
  const dxfAuditReport = auditFn(
    dxfExportSummary.dxfOutputDir,
    dxfExportSummary.parts || [],
    { installedRelPaths: installedSummary.installedRelPaths },
  )
  if (dxfAuditReport.pass !== true) {
    throw new Error(`DXF_AUDIT_FAILED: ${dxfAuditReport.dxfAuditPassCount}/${dxfAuditReport.sheetMetalCount} passed`)
  }

  reportStage('exporting_drawings', 'DXF 全检通过，正在生成总装配体与各唯一门板 2D 工程图（SLDDRW + PDF）...')
  const runDrawings = hooks.runDrawings || ((sourceCadDir) => runProtectedPhase({
    tag: 'T3-envelope-drawings',
    sourceDirectory: sourceCadDir,
    job: { action: 'assembly-drawings', production_release: false, sourceBuildRun: buildRes.dir },
  }, { ...(signal ? { signal } : {}) }))
  const drawingRes = await runDrawings(join(buildRes.dir, 'cad'))
  const drawingProtectionPath = join(drawingRes.dir, 'protection-result.json')
  const drawingProtection = existsSync(drawingProtectionPath) ? JSON.parse(readFileSync(drawingProtectionPath, 'utf8')) : { pass: false }
  if (drawingRes.pass !== true || drawingProtection.pass !== true) {
    throw new Error('DRAWING_EXPORT_PROTECTION_OR_PHASE_FAILED')
  }

  const drawingNative = JSON.parse(readFileSync(join(drawingRes.dir, 'native-result.json'), 'utf8'))
  const drawingExportEvent = [...(drawingNative.events || [])].reverse().find((e) => e.inspection === 'assembly_engineering_drawings')
  const drawingExportSummary = drawingExportEvent?.result
  if (!drawingExportSummary
      || drawingExportSummary.inputHashesUnchanged !== true
      || Number(drawingExportSummary.pdfFileCount) <= 0
      || Number(drawingExportSummary.slddrwFileCount) !== Number(drawingExportSummary.pdfFileCount)) {
    throw new Error('DRAWING_EXPORT_EVIDENCE_INVALID')
  }

  reportStage('packaging_zip', '正在封装 3D 装配体、全套 DXF 与 2D 工程图交付包并校验完整性...')
  const [lCount, rCount] = normalizedInput.layoutSummary.columnDoorCounts
  const zipFileName = `16029_全参数_${reqCab.widthMm}W_${reqCab.heightMm}H_${reqCab.depthMm}D_L${lCount}-R${rCount}_3D与DXF_${taskId}.zip`
  const packageFn = hooks.packageZip || packageVariableTrialDeliverable
  const zipInfo = packageFn({
    archiveDir,
    zipFileName,
    buildCadAssemblyDir: join(buildRes.dir, 'cad', 'assemblies', 'case1'),
    dxfOutputDir: dxfExportSummary.dxfOutputDir,
    drawingOutputDir: drawingExportSummary.drawingOutputDir,
    drawingExportSummary,
    assemblyEvidence,
    dxfAuditReport,
    requestSummary: {
      taskId,
      cabinet: reqCab,
      layoutSummary: normalizedInput.layoutSummary,
      customerRequirementReference: normalizedInput.normalizedRequest.customerRequirementReference || '',
    },
  })

  const report = {
    schema: 'winnsen.locker16029.variable_trial_report.v1',
    production_release: false,
    taskId,
    generatedAt: new Date().toISOString(),
    request: normalizedInput.normalizedRequest,
    layoutSummary: normalizedInput.layoutSummary,
    assemblyEvidence: {
      measuredEnvelopeMm: assemblyEvidence.measuredEnvelopeMm,
      interferencePass: assemblyEvidence.interferencePass,
      structuralCollisionCount: assemblyEvidence.interferenceClassification?.structuralCollisionCount ?? 0,
      unapprovedInterferenceCount: assemblyEvidence.interferenceClassification?.unapprovedInterferenceCount ?? 0,
      approvedCirclipHingePinCount: assemblyEvidence.interferenceClassification?.approvedCirclipHingePinCount ?? 0,
      approvedUpperPinPrebiasCount: assemblyEvidence.interferenceClassification?.approvedUpperPinPrebiasCount ?? 0,
      productionMarksSummary: assemblyEvidence.productionMarks || null,
      belowFloorNonFootPass: assemblyEvidence.belowFloorNonFootPass,
      materialCheck: assemblyEvidence.materialCheck,
      inputFilesUnchanged: assemblyEvidence.inputFilesUnchanged,
      fullScopeSummary: assemblyEvidence.fullScopeSummary,
    },
    dxfAuditSummary: {
      sheetMetalCount: dxfAuditReport.sheetMetalCount,
      dxfFileCount: dxfAuditReport.dxfFileCount,
      dxfAuditPassCount: dxfAuditReport.dxfAuditPassCount,
      dxfAuditFailCount: dxfAuditReport.dxfAuditFailCount,
      formToolMarkParts: dxfAuditReport.formToolMarkParts,
      totalMarkEntities: dxfAuditReport.totalMarkEntities,
      totalRemovedFastenerBores: dxfAuditReport.totalRemovedFastenerBores,
      excludedUninstalledFiles: dxfAuditReport.excludedUninstalledFiles || [],
      results: dxfAuditReport.results,
    },
    drawingSummary: {
      drawingCount: drawingExportSummary.drawingCount,
      slddrwFileCount: zipInfo.drawingSlddrwCount ?? drawingExportSummary.slddrwFileCount,
      pdfFileCount: zipInfo.drawingPdfCount ?? drawingExportSummary.pdfFileCount,
      drawings: drawingExportSummary.drawings || [],
    },
    buildRunDir: buildRes.dir,
    dxfRunDir: dxfRes.dir,
    drawingRunDir: drawingRes.dir,
  }
  const reportPath = join(archiveDir, 'report.json')
  writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', 'utf8')

  const nonSheetMetalCount = installedSummary.hasManifest
    ? Math.max(0, installedSummary.uniquePartCount - dxfAuditReport.sheetMetalCount)
    : Number(dxfExportSummary.nonSheetMetalCount ?? 0)

  const publication = {
    zipFileName: zipInfo.zipFileName,
    zipPath: zipInfo.zipPath,
    sizeBytes: zipInfo.sizeBytes,
    sha256: zipInfo.sha256,
    reportPath,
    cadFileCount: zipInfo.cadFileCount,
    dxfFileCount: zipInfo.dxfFileCount,
    drawingFileCount: zipInfo.drawingFileCount ?? 0,
    drawingSlddrwCount: zipInfo.drawingSlddrwCount ?? 0,
    drawingPdfCount: zipInfo.drawingPdfCount ?? 0,
    bomUniquePartCount: zipInfo.bomUniquePartCount,
    bomTotalInstanceCount: zipInfo.bomTotalInstanceCount,
    sheetMetalCount: dxfAuditReport.sheetMetalCount,
    nonSheetMetalCount,
    formToolMarkParts: dxfAuditReport.formToolMarkParts,
    totalMarkEntities: dxfAuditReport.totalMarkEntities,
    totalProductionMarkEntities: dxfAuditReport.totalProductionMarkEntities ?? 0,
    removedFastenerBores: dxfAuditReport.totalRemovedFastenerBores,
    approvedUpperPinPrebiasCount: assemblyEvidence.interferenceClassification?.approvedUpperPinPrebiasCount ?? 0,
    dxfAuditPassCount: dxfAuditReport.dxfAuditPassCount,
    dxfAuditTotalCount: dxfAuditReport.sheetMetalCount,
    measuredEnvelopeMm: assemblyEvidence.measuredEnvelopeMm,
    structuralCollisionCount: assemblyEvidence.interferenceClassification?.structuralCollisionCount ?? 0,
    unapprovedInterferenceCount: assemblyEvidence.interferenceClassification?.unapprovedInterferenceCount ?? 0,
    missingMaterialCount: assemblyEvidence.materialCheck?.missingMaterialCount ?? 0,
    buildRunDir: buildRes.dir,
    dxfRunDir: dxfRes.dir,
    drawingRunDir: drawingRes.dir,
    createdAt: new Date().toISOString(),
  }

  return { productionRelease: false, publication, report }
}
