import assert from 'node:assert/strict'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'

const request = weights => ({
  cabinet: { widthMm: 850, heightMm: 1917, depthMm: 550 },
  columns: ['L', 'R'].map(side => ({ side, doors: weights.map(heightUnits => ({ heightUnits })) })),
})

const standard = build16029ParametricContract(request([6, 4, 2]))
assert.deepEqual(standard.rowsByColumn.L.map(row => row.doorHeightMm), [908, 603, 298])
assert.deepEqual(standard.rowsByColumn.L.map(row => row.centerYmm), [486, 1248.5, 1706])
assert.equal(standard.rowsByColumn.L.at(-1).topYmm, 1855)
assert.equal(standard.geometry.doorLeafWidthMm, 362)

const tiny = build16029ParametricContract(request([1e-300, 1e-300, 1e-300]))
assert.ok(tiny.rowsByColumn.L.every(row => Number.isFinite(row.doorHeightMm)))
assert.ok(Math.abs(tiny.rowsByColumn.L.at(-1).topYmm - 1855) < 0.001)
for (const weights of [[1e308, 1e308, 1e308], [1e307], [Infinity], [NaN]]) {
  assert.throws(() => build16029ParametricContract(request(weights)), /有限|正的/)
}

console.log('parametric input boundaries: calibrated dimensions and finite tiny weights accepted; non-finite totals, computed heights and inputs rejected; CAD not started')
