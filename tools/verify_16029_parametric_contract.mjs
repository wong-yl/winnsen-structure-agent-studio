import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
const request = width => ({ cabinet: { widthMm: width, heightMm: 1917, depthMm: 550 }, columns: [{ side: 'L', doors: [6, 4, 2].map(heightUnits => ({ heightUnits })) }, { side: 'R', doors: [2, 4, 6].map(heightUnits => ({ heightUnits })) }] })
const baseline = JSON.parse(readFileSync('workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/evidence/v35_lock_topology_validation.json', 'utf8'))
const reference = build16029ParametricContract(request(740))
for (const side of ['L', 'R']) assert.deepEqual(reference.rowsByColumn[side].map(row => row.centerYmm), baseline.checks.find(check => check.name === side + '_exact_three_slot_rows').actual)
const hierarchy = JSON.parse(readFileSync('workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/evidence/v35_native_hierarchy_final_reopen_validation.json', 'utf8'))
for (const side of ['L', 'R']) {
  const shelves = hierarchy.components.filter(component => component.depth === 0 && component.name.startsWith('箱体横层板' + side + '焊接') && !component.is_suppressed).map(component => (component.box.ymin_mm + component.box.ymax_mm) / 2).sort((a,b) => a-b)
  assert.ok(reference.boundaries[side].every((boundary,index) => Math.abs(boundary.shelfCenterYmm - shelves[index]) < .001))
  const crossbars = hierarchy.components.filter(component => component.depth === 1 && component.name.startsWith('门框焊接-1/门框 横隔板' + (side === 'R' ? 'R-' : '-')) && !component.is_suppressed).map(component => (component.box.ymin_mm + component.box.ymax_mm) / 2).sort((a,b)=>a-b)
  assert.ok(reference.boundaries[side].every((boundary,index) => Math.abs(boundary.crossbarCenterYmm - crossbars[index]) < .001))
}
for (const width of [740, 760, 887, 913]) {
  const contract = build16029ParametricContract(request(width))
  assert.equal(2 * contract.geometry.doorLeafWidthMm + 126, width)
  for (const side of ['L', 'R']) {
    for (const row of contract.boundaries[side]) assert.equal(row.doorGapCenterYmm, (contract.rowsByColumn[side][row.index - 1].topYmm + contract.rowsByColumn[side][row.index].bottomYmm) / 2)
  }
  assert.equal(contract.moduleDimensions.lockTongues.globalXmm.L, -55)
}
for (const count of [4, 6, 14]) {
  const input = request(887)
  input.columns.forEach(column => { column.doors = Array.from({ length: count / 2 }, () => ({ heightUnits: 1 })) })
  const contract = build16029ParametricContract(input)
  assert.equal(contract.geometry.doorCount, count)
  for (const side of ['L', 'R']) assert.ok(Math.abs(contract.rowsByColumn[side].at(-1).topYmm - 1855) < 1e-9)
}
const bad = request(887); bad.columns[0].doors = [{ heightMm: 100 }]
assert.throws(() => build16029ParametricContract(bad), /等于柜高/)
assert.throws(() => build16029ParametricContract(request(120)), /126/)
console.log('parametric contract: V35 measured slot centers + continuous widths + 4/6/14 door closure + invalid request rejection passed; CAD not started')
