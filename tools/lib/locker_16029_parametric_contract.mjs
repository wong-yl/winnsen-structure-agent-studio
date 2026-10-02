export const PARAMETRIC_16029_CONTRACT_SCHEMA = 'winnsen.locker16029.parametric_contract.v1'
const V35 = 'workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1'
const WIDTH_EVIDENCE = 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.cs'

export function build16029ParametricContract(request) {
  const cabinet = { widthMm: Number(request?.cabinet?.widthMm), heightMm: Number(request?.cabinet?.heightMm ?? 1917), depthMm: Number(request?.cabinet?.depthMm ?? 550) }
  if (!Object.values(cabinet).every(value => Number.isFinite(value) && value > 0)) throw new Error('柜宽、柜高、柜深必须是正数。')
  if (cabinet.widthMm <= 126) throw new Error('柜宽不足以容纳两列门和已确认的固定结构余量 126 mm。')
  if (!Array.isArray(request.columns) || request.columns.length !== 2 || request.columns[0].side !== 'L' || request.columns[1].side !== 'R') throw new Error('当前结构必须按 L、R 提供两列门序，门序自下向上。')
  const gap = 7
  const pitchBudgetMm = cabinet.heightMm - 87
  const rowsByColumn = {}, boundaries = {}
  for (const column of request.columns) {
    if (!Array.isArray(column.doors) || !column.doors.length || column.doors.length > 24) throw new Error('每列需提供 1–24 扇门；输入受理不表示整个范围已通过原生实跑。')
    const unitsMode = column.doors.every(door => Number.isFinite(door.heightUnits) && door.heightUnits > 0 && door.heightMm === undefined)
    const heightMode = column.doors.every(door => Number.isFinite(door.heightMm) && door.heightMm > 0 && door.heightUnits === undefined)
    if (!unitsMode && !heightMode) throw new Error('同一列必须统一提供正的 heightUnits 或 heightMm。')
    const unitSum = unitsMode ? column.doors.reduce((sum, door) => sum + door.heightUnits, 0) : 0
    if (unitsMode && !Number.isFinite(unitSum)) throw new Error('门高权重合计必须是有限正数。')
    const heights = column.doors.map(door => unitsMode ? pitchBudgetMm * door.heightUnits / unitSum - gap : door.heightMm)
    if (heights.some(height => !Number.isFinite(height))) throw new Error('计算门高必须是有限正数。')
    if (heights.some(height => height < 100)) throw new Error('门高不足 100 mm，不能容纳现有上下铰链接口。')
    if (Math.abs(heights.reduce((sum, height) => sum + height + gap, 0) - pitchBudgetMm) > 0.001) throw new Error('各门高加每门 7 mm 间隙后必须等于柜高减 87 mm。')
    let bottom = 32
    rowsByColumn[column.side] = heights.map((height, index) => {
      const row = { side: column.side, index: index + 1, bottomYmm: bottom, topYmm: bottom + height, centerYmm: bottom + height / 2, doorHeightMm: height, heightUnits: unitsMode ? column.doors[index].heightUnits : null }
      bottom += height + gap
      return row
    })
    boundaries[column.side] = rowsByColumn[column.side].slice(0, -1).map(row => ({ index: row.index, doorGapCenterYmm: row.topYmm + gap / 2, shelfCenterYmm: row.topYmm + gap / 2 - 1, crossbarCenterYmm: row.topYmm + gap / 2 - 6 }))
  }
  const W = cabinet.widthMm, H = cabinet.heightMm, D = cabinet.depthMm
  const doorLeafWidthMm = (W - 126) / 2
  const columnDoorCounts = request.columns.map(column => column.doors.length)
  const doorCount = columnDoorCounts.reduce((a, b) => a + b)
  return {
    schema: PARAMETRIC_16029_CONTRACT_SCHEMA,
    input: structuredClone(request),
    support: { parameterCalculation: true, widthRuntimeVerified: false, heightDepthRuntimeVerified: false, modelReady: false, runtimeRequired: ['native dimensions', 'derived parts', 'door modules', 'lock topology', 'full assembly', 'flat pattern', 'interference', 'relocated reopen'] },
    calibration: { structuralSource: '1000W original', qualityTarget: 'V35', seedWidthMm: 740, seedHeightMm: 1917, seedDepthMm: 550, coordinateOrder: 'bottom-up', pitchBudgetMm },
    geometry: { cabinet, columns: 2, doorCount, columnDoorCounts, doorLeafWidthMm, interiorWidthMm: W - 126 }, rowsByColumn, boundaries,
    moduleDimensions: {
      sidePanels: { outerXmm: [-W / 2, W / 2], heightDeltaMm: H - 1917, depthDeltaMm: D - 550 },
      topCover: { widthMm: W, bottomWidthMm: W - 2, leftXmm: [-W / 2, -W / 2 + 59.2] },
      base: { widthMm: W, bottomWidthMm: W - 42, reinforcementWidthMm: W - 20.6, holeXmm: [-(W - 150) / 2, (W - 150) / 2], footXmm: [-(W / 2 - 35), W / 2 - 35] },
      frontFrame: { topBottomWidthMm: W - 2.8, sideWidthMm: 20 },
      partitions: { lockInterfaceXmm: [-55, 55], centerWidthInvariant: true },
      shelves: { leftXmm: [-W / 2 + 2.3, -64.5], rightXmm: [64.5, W / 2 - 2.3], stiffenerWidthMm: (W - 142) / 2, count: doorCount - 2 },
      crossbars: { leftXmm: [-W / 2 + 18.5, -36.5], rightXmm: [36.5, W / 2 - 18.5], count: doorCount - 2 },
      doors: { widthMm: doorLeafWidthMm, centerXmm: { L: -(W / 4 + 8.5), R: W / 4 + 8.5 }, stiffenerLengthByColumnMm: Object.fromEntries(['L', 'R'].map(side => [side, rowsByColumn[side].map(row => row.doorHeightMm - 10.5)])) },
      hinges: { localAbsXmm: doorLeafWidthMm / 2 - 10, latchYInsetMm: 28.8 },
      lockTongues: { localAbsXmm: doorLeafWidthMm / 2 - 15, globalXmm: { L: -55, R: 55 }, zMm: -11.3 },
      lockHoles: { count: doorCount, centerYByColumnMm: Object.fromEntries(['L', 'R'].map(side => [side, rowsByColumn[side].map(row => row.centerYmm)])), pairedCircleOffsetMm: -60, pairedCircleDiameterMm: 5 },
    },
    fixedProcessDimensions: { doorGapMm: gap, bottomDoorYmm: 32, widthAllowanceMm: 126, doorStiffenerHeightAllowanceMm: 10.5, panelThicknessMm: 0.8, electricalComponentsAllowed: false },
    evidence: [V35 + '/evidence/v35_lock_topology_validation.json', WIDTH_EVIDENCE, 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.cs'],
  }
}
