const SIDES = ['L', 'R']
const DIMENSION_LIMITS = {
  widthMm: [700, 1200],
  heightMm: [1700, 2200],
  depthMm: [250, 650],
}
const PROCESS_VALUES = {
  thicknessMm: 0.8,
  innerRadiusMm: 0.2,
  bendDeductionMm: 1.4,
  status: 'draft_pending_factory_confirmation',
}
const DOOR_GAP_MM = 7
const BOTTOM_DOOR_Y_MM = 32
const CLOSURE_TOLERANCE_MM = 0.001

function requireObject(value, label) {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${label} 必须是对象。`)
  }
}

function requirePositiveNumber(value, label) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    throw new TypeError(`${label} 必须是有限正数。`)
  }
  return value
}

function requireHand(hand) {
  if (hand !== 'L' && hand !== 'R') throw new TypeError('hand 必须是 L 或 R。')
  return hand
}

export function compileLayout(spec) {
  requireObject(spec, 'spec')
  requireObject(spec.cabinet, 'cabinet')
  const cabinet = {}
  for (const [name, [minimum, maximum]] of Object.entries(DIMENSION_LIMITS)) {
    const value = requirePositiveNumber(spec.cabinet[name], `cabinet.${name}`)
    if (value < minimum || value > maximum) {
      throw new RangeError(`cabinet.${name} 必须在 ${minimum}–${maximum} mm 内。`)
    }
    cabinet[name] = value
  }
  if (!Array.isArray(spec.columns) || spec.columns.length !== 2 ||
      spec.columns[0]?.side !== 'L' || spec.columns[1]?.side !== 'R') {
    throw new TypeError('columns 必须按 L、R 提供两列门序，门序自下向上。')
  }

  const columnDoorCounts = spec.columns.map((column, columnIndex) => {
    requireObject(column, `columns[${columnIndex}]`)
    if (!Array.isArray(column.doors) || column.doors.length < 1 || column.doors.length > 24) {
      throw new RangeError(`${SIDES[columnIndex]} 列必须提供 1–24 扇门。`)
    }
    return column.doors.length
  })
  const doorCount = columnDoorCounts.reduce((sum, count) => sum + count, 0)
  if (doorCount > 34) throw new RangeError('总门数不得超过 34 扇。')

  const pitchBudgetMm = cabinet.heightMm - 87
  const rowsByColumn = {}
  const boundaries = {}
  for (const column of spec.columns) {
    const units = Array.from(column.doors, (door, index) => {
      requireObject(door, `${column.side}.doors[${index}]`)
      if (door.heightMm !== undefined) {
        throw new TypeError('门高仅接受 heightUnits，不能同时或单独提供 heightMm。')
      }
      return requirePositiveNumber(door.heightUnits, `${column.side}.doors[${index}].heightUnits`)
    })
    const unitSum = units.reduce((sum, value) => sum + value, 0)
    if (!Number.isFinite(unitSum) || unitSum <= 0) {
      throw new RangeError('heightUnits 合计必须是有限正数。')
    }
    const heights = units.map(value => pitchBudgetMm * value / unitSum - DOOR_GAP_MM)
    if (heights.some(height => !Number.isFinite(height) || height < 100)) {
      throw new RangeError('每扇门高必须是有限数且不少于 100 mm。')
    }
    const totalPitchMm = heights.reduce((sum, height) => sum + height + DOOR_GAP_MM, 0)
    if (!Number.isFinite(totalPitchMm) || Math.abs(totalPitchMm - pitchBudgetMm) > CLOSURE_TOLERANCE_MM) {
      throw new RangeError('门高加每门 7 mm 间隙必须闭合到柜高减 87 mm。')
    }

    let bottomYmm = BOTTOM_DOOR_Y_MM
    rowsByColumn[column.side] = heights.map((height, index) => {
      const row = {
        side: column.side,
        index: index + 1,
        bottomYmm,
        topYmm: bottomYmm + height,
        centerYmm: bottomYmm + height / 2,
        doorHeightMm: height,
        heightUnits: units[index],
      }
      bottomYmm += height + DOOR_GAP_MM
      return row
    })
    boundaries[column.side] = rowsByColumn[column.side].slice(0, -1).map(row => ({
      index: row.index,
      doorGapCenterYmm: row.topYmm + DOOR_GAP_MM / 2,
      shelfCenterYmm: row.topYmm + DOOR_GAP_MM / 2 - 1,
      crossbarCenterYmm: row.topYmm + DOOR_GAP_MM / 2 - 6,
    }))
  }

  return {
    cabinet,
    doorLeafWidthMm: (cabinet.widthMm - 126) / 2,
    rowsByColumn,
    boundaries,
    columnDoorCounts,
    doorCount,
  }
}

export function compileDoor(door, process) {
  requireObject(door, 'door')
  const widthMm = requirePositiveNumber(door.widthMm, 'door.widthMm')
  const heightMm = requirePositiveNumber(door.heightMm, 'door.heightMm')
  const hand = requireHand(door.hand)
  requireObject(process, 'process')
  if (typeof process.version !== 'string' || !process.version.length || process.version.trim() !== process.version) {
    throw new TypeError('process.version 必须是非空且无首尾空白的字符串。')
  }
  for (const [name, expected] of Object.entries(PROCESS_VALUES)) {
    if (process[name] !== expected) {
      throw new TypeError(`process.${name} 必须明确提供为 ${expected}。`)
    }
  }
  const versionForFilename = encodeURIComponent(process.version).replace(/[!'()*]/g,
    character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`)
  const identity = `16029-door_W${widthMm}_H${heightMm}_hand${hand}_t${PROCESS_VALUES.thicknessMm}_process${versionForFilename}_leaf`
  const canonicalProcess = { version: process.version, ...PROCESS_VALUES }
  return {
    identity,
    hand,
    variables: {
      DOOR_W: widthMm,
      DOOR_H: heightMm,
      T: PROCESS_VALUES.thicknessMm,
      BEND_R: PROCESS_VALUES.innerRadiusMm,
      BEND_DED: PROCESS_VALUES.bendDeductionMm,
    },
    properties: {
      widthMm,
      heightMm,
      hand,
      thicknessMm: PROCESS_VALUES.thicknessMm,
      innerRadiusMm: PROCESS_VALUES.innerRadiusMm,
      bendDeductionMm: PROCESS_VALUES.bendDeductionMm,
      processVersion: process.version,
      processStatus: PROCESS_VALUES.status,
    },
    process: canonicalProcess,
  }
}
