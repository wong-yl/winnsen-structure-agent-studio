import test from 'node:test'
import assert from 'node:assert/strict'
import { compileDoor, compileLayout } from './layout.mjs'
import { build16029ParametricContract } from '../../../tools/lib/locker_16029_parametric_contract.mjs'

const processDraft = {
  version: '16029-variable-trial-process-v1',
  thicknessMm: 0.8,
  innerRadiusMm: 0.2,
  bendDeductionMm: 1.4,
  status: 'draft_pending_factory_confirmation',
}

function specFor(widthMm = 1000, leftUnits = [1, 1, 1, 1], rightUnits = [1, 1, 1, 1], heightMm = 2000, depthMm = 500) {
  return {
    cabinet: { widthMm, heightMm, depthMm },
    columns: [
      { side: 'L', doors: leftUnits.map(heightUnits => ({ heightUnits })) },
      { side: 'R', doors: rightUnits.map(heightUnits => ({ heightUnits })) },
    ],
  }
}

function near(actual, expected, label) {
  assert.ok(Math.abs(actual - expected) < 1e-9, `${label}: ${actual} !== ${expected}`)
}

function freezeDeep(value) {
  if (value && typeof value === 'object') {
    Object.values(value).forEach(freezeDeep)
    Object.freeze(value)
  }
  return value
}

function oldLayout(spec) {
  const legacy = build16029ParametricContract(spec)
  return {
    cabinet: legacy.geometry.cabinet,
    doorLeafWidthMm: legacy.geometry.doorLeafWidthMm,
    rowsByColumn: legacy.rowsByColumn,
    boundaries: legacy.boundaries,
    columnDoorCounts: legacy.geometry.columnDoorCounts,
    doorCount: legacy.geometry.doorCount,
  }
}

test('1000×2000×500 L4R4 has exact baseline door sizes and locations', () => {
  const layout = compileLayout(specFor())
  assert.deepEqual(layout.cabinet, { widthMm: 1000, heightMm: 2000, depthMm: 500 })
  assert.equal(layout.doorLeafWidthMm, 437)
  assert.deepEqual(layout.columnDoorCounts, [4, 4])
  assert.equal(layout.doorCount, 8)
  const expectedPositions = [
    [32, 503.25, 267.625],
    [510.25, 981.5, 745.875],
    [988.5, 1459.75, 1224.125],
    [1466.75, 1938, 1702.375],
  ]
  const expectedBoundaries = [
    { index: 1, doorGapCenterYmm: 506.75, shelfCenterYmm: 505.75, crossbarCenterYmm: 500.75 },
    { index: 2, doorGapCenterYmm: 985, shelfCenterYmm: 984, crossbarCenterYmm: 979 },
    { index: 3, doorGapCenterYmm: 1463.25, shelfCenterYmm: 1462.25, crossbarCenterYmm: 1457.25 },
  ]
  for (const side of ['L', 'R']) {
    assert.deepEqual(layout.rowsByColumn[side], expectedPositions.map(([bottomYmm, topYmm, centerYmm], index) => ({
      side, index: index + 1, bottomYmm, topYmm, centerYmm, doorHeightMm: 471.25, heightUnits: 1,
    })))
    assert.deepEqual(layout.boundaries[side], expectedBoundaries)
  }
})

test('weighted L[1,2,1,2] R[2,1,2] keeps precise door positions and closes both columns', () => {
  const layout = compileLayout(specFor(1000, [1, 2, 1, 2], [2, 1, 2]))
  assert.equal(layout.doorLeafWidthMm, 437)
  assert.deepEqual(layout.columnDoorCounts, [4, 3])
  assert.equal(layout.doorCount, 7)
  const expected = {
    L: [
      [311.8333333333333, 32, 343.8333333333333, 187.91666666666666],
      [630.6666666666666, 350.8333333333333, 981.5, 666.1666666666666],
      [311.8333333333333, 988.5, 1300.3333333333333, 1144.4166666666667],
      [630.6666666666666, 1307.3333333333333, 1938, 1622.6666666666665],
    ],
    R: [
      [758.2, 32, 790.2, 411.1],
      [375.6, 797.2, 1172.8, 985],
      [758.2, 1179.8, 1938, 1558.9],
    ],
  }
  for (const side of ['L', 'R']) {
    layout.rowsByColumn[side].forEach((row, index) => {
      const [height, bottom, top, center] = expected[side][index]
      near(row.doorHeightMm, height, `${side}${index + 1} height`)
      near(row.bottomYmm, bottom, `${side}${index + 1} bottom`)
      near(row.topYmm, top, `${side}${index + 1} top`)
      near(row.centerYmm, center, `${side}${index + 1} center`)
    })
    near(layout.rowsByColumn[side].at(-1).topYmm, 1938, `${side} final top`)
    assert.equal(layout.boundaries[side].length, layout.rowsByColumn[side].length - 1)
    layout.boundaries[side].forEach((boundary, index) => {
      near(boundary.shelfCenterYmm, expected[side][index][2] + 2.5, `${side} shelf ${index + 1}`)
      near(boundary.crossbarCenterYmm, expected[side][index][2] - 2.5, `${side} crossbar ${index + 1}`)
    })
  }
})

test('900 mm cabinet changes door width to 387 mm and preserves the same weighted vertical layout', () => {
  const baseline = compileLayout(specFor(1000, [1, 2, 1, 2], [2, 1, 2]))
  const narrow = compileLayout(specFor(900, [1, 2, 1, 2], [2, 1, 2]))
  assert.equal(narrow.doorLeafWidthMm, 387)
  assert.equal(narrow.cabinet.widthMm, 900)
  assert.deepEqual(narrow.rowsByColumn, baseline.rowsByColumn)
  assert.deepEqual(narrow.boundaries, baseline.boundaries)
})

test('each column has exactly count−1 shelves and crossbars, including single-door columns', () => {
  for (const [leftCount, rightCount] of [[1, 1], [1, 4], [4, 3], [17, 17]]) {
    const layout = compileLayout(specFor(1000, Array(leftCount).fill(1), Array(rightCount).fill(1), 2200))
    for (const [index, side] of ['L', 'R'].entries()) {
      const boundaries = layout.boundaries[side]
      assert.equal(boundaries.length, layout.columnDoorCounts[index] - 1)
      assert.ok(boundaries.every(boundary => Number.isFinite(boundary.shelfCenterYmm) && Number.isFinite(boundary.crossbarCenterYmm)))
    }
    assert.equal(layout.boundaries.L.length + layout.boundaries.R.length, layout.doorCount - 2)
  }
})

test('baseline → mixed rows → 900 mm → baseline leaves no stale state or input mutations', () => {
  const spec = freezeDeep(specFor())
  const original = structuredClone(spec)
  const first = compileLayout(spec)
  const expected = structuredClone(first)
  first.cabinet.widthMm = 1
  first.rowsByColumn.L[0].doorHeightMm = 1
  first.boundaries.R.push({ index: 99 })
  first.columnDoorCounts[0] = 99
  compileLayout(specFor(1000, [1, 2, 1, 2], [2, 1, 2]))
  compileLayout(specFor(900, [1, 2, 1, 2], [2, 1, 2]))
  assert.deepEqual(compileLayout(spec), expected)
  assert.deepEqual(spec, original)
})

test('all returned layout fields match the old contract for the requested trial matrix', () => {
  for (const spec of [
    specFor(),
    specFor(1000, [1, 2, 1, 2], [2, 1, 2]),
    specFor(900, [1, 2, 1, 2], [2, 1, 2]),
  ]) {
    assert.deepEqual(compileLayout(spec), oldLayout(spec))
  }
})

test('range-edge and all valid door-count pairs match every corresponding old-contract output', () => {
  let verifiedCases = 0
  for (const widthMm of [700, 900, 1000, 1200]) {
    for (const heightMm of [1700, 2000, 2200]) {
      for (const depthMm of [250, 500, 650]) {
        for (let leftCount = 1; leftCount <= 24; leftCount++) {
          for (let rightCount = 1; rightCount <= 24; rightCount++) {
            if (leftCount + rightCount > 34) continue
            const spec = specFor(widthMm, Array(leftCount).fill(1), Array(rightCount).fill(1), heightMm, depthMm)
            let expected
            try {
              expected = oldLayout(spec)
            } catch (error) {
              assert.match(error.message, /门高不足 100 mm/)
              assert.throws(() => compileLayout(spec), /100 mm/)
              continue
            }
            const actual = compileLayout(spec)
            assert.deepEqual(actual, expected)
            for (const side of ['L', 'R']) {
              assert.equal(actual.boundaries[side].length, actual.rowsByColumn[side].length - 1)
              near(actual.rowsByColumn[side][0].bottomYmm, 32, `${side} first bottom`)
              near(actual.rowsByColumn[side].at(-1).topYmm, heightMm - 62, `${side} final top`)
            }
            verifiedCases++
          }
        }
      }
    }
  }
  assert.equal(verifiedCases, 12 * (15 ** 2 + 17 ** 2 + 19 ** 2 - 10))
})

test('layout rejects missing, nonfinite, coerced and out-of-range cabinet dimensions', () => {
  for (const input of [undefined, null, [], {}, { cabinet: null }]) {
    assert.throws(() => compileLayout(input), TypeError)
  }
  for (const field of ['widthMm', 'heightMm', 'depthMm']) {
    for (const value of [undefined, null, '1000', NaN, Infinity, -Infinity, 0, -1]) {
      const spec = specFor()
      spec.cabinet[field] = value
      assert.throws(() => compileLayout(spec), new RegExp(field))
    }
  }
  for (const [field, values] of Object.entries({ widthMm: [699.999, 1200.001], heightMm: [1699.999, 2200.001], depthMm: [249.999, 650.001] })) {
    for (const value of values) {
      const spec = specFor()
      spec.cabinet[field] = value
      assert.throws(() => compileLayout(spec), new RegExp(field))
    }
  }
})

test('layout rejects invalid column order, counts, missing units and conflicting height modes', () => {
  for (const columns of [undefined, null, [], [{}], [{ side: 'R', doors: [{ heightUnits: 1 }] }, { side: 'L', doors: [{ heightUnits: 1 }] }], [null, null]]) {
    assert.throws(() => compileLayout({ ...specFor(), columns }), /columns/)
  }
  for (const doors of [undefined, null, [], Array.from({ length: 25 }, () => ({ heightUnits: 1 }))]) {
    const spec = specFor()
    spec.columns[0].doors = doors
    assert.throws(() => compileLayout(spec), /1–24/)
  }
  for (const door of [null, [], {}, { heightUnits: undefined }, { heightUnits: NaN }, { heightUnits: Infinity }, { heightUnits: -Infinity }, { heightUnits: 0 }, { heightUnits: -1 }, { heightUnits: '1' }, { heightMm: 471.25 }, { heightUnits: 1, heightMm: 471.25 }]) {
    const spec = specFor()
    spec.columns[0].doors[0] = door
    assert.throws(() => compileLayout(spec))
  }
})

test('layout rejects 24 doors that cannot fit, more than 34 total doors, and nonfinite calculations', () => {
  assert.throws(() => compileLayout(specFor(1000, Array(24).fill(1), [1], 2200)), /100 mm/)
  assert.throws(() => compileLayout(specFor(1000, Array(18).fill(1), Array(17).fill(1), 2200)), /34/)
  assert.throws(() => compileLayout(specFor(1000, [Number.MAX_VALUE, Number.MAX_VALUE], [1])), /合计/)
  assert.throws(() => compileLayout(specFor(1000, [Number.MAX_VALUE], [1])), /有限数/)
  assert.throws(() => compileLayout(specFor(1000, [1, 1e12], [1])), /100 mm/)
})

test('layout rejects sparse door arrays instead of returning missing rows', () => {
  const spec = specFor()
  const sparseDoors = Array(4)
  sparseDoors[0] = { heightUnits: 1 }
  spec.columns[0].doors = sparseDoors
  assert.throws(() => compileLayout(spec), /doors\[1\]/)
})

test('layout accepts the exact 100 mm door-height limit and rejects the next smaller result', () => {
  const spec = specFor(1000, [107, 1806], [1])
  assert.equal(compileLayout(spec).rowsByColumn.L[0].doorHeightMm, 100)
  spec.columns[0].doors[0].heightUnits = 106.999
  assert.throws(() => compileLayout(spec), /100 mm/)
})

test('explicit nonclosing heights are rejected before any layout is produced', () => {
  const spec = specFor()
  spec.columns[0].doors = [{ heightMm: 400 }, { heightMm: 400 }]
  assert.throws(() => compileLayout(spec), /仅接受 heightUnits/)
})

test('door matrix produces exact numeric equations, separate handedness, and safe unique identities', () => {
  const identities = new Set()
  for (const [widthMm, heightMm] of [[437, 471.25], [287, 200], [537, 1000]]) {
    for (const hand of ['L', 'R']) {
      const door = compileDoor({ widthMm, heightMm, hand }, processDraft)
      assert.deepEqual(door.variables, { DOOR_W: widthMm, DOOR_H: heightMm, T: 0.8, BEND_R: 0.2, BEND_DED: 1.4 })
      assert.ok(Object.values(door.variables).every(value => typeof value === 'number' && Number.isFinite(value)))
      assert.equal(Object.hasOwn(door.variables, 'HAND'), false)
      assert.equal(door.hand, hand)
      assert.deepEqual(door.properties, { widthMm, heightMm, hand, thicknessMm: 0.8, innerRadiusMm: 0.2, bendDeductionMm: 1.4, processVersion: processDraft.version, processStatus: processDraft.status })
      assert.deepEqual(door.process, processDraft)
      assert.equal(door.identity, `16029-door_W${widthMm}_H${heightMm}_hand${hand}_t0.8_process${processDraft.version}_leaf`)
      assert.doesNotMatch(door.identity, /[<>:"/\\|?*\x00-\x1f]/)
      identities.add(door.identity)
    }
  }
  assert.equal(identities.size, 6)
})

test('door identity changes with process version and safely encodes filename punctuation', () => {
  const input = { widthMm: 437, heightMm: 471.25, hand: 'L' }
  const first = compileDoor(input, processDraft)
  const versionTwo = compileDoor(input, { ...processDraft, version: 'v2' })
  assert.notEqual(versionTwo.identity, first.identity)
  const punctuationVersion = '试验:*?<>|/\\"!()版本.'
  const escaped = compileDoor(input, { ...processDraft, version: punctuationVersion })
  assert.doesNotMatch(escaped.identity, /[<>:"/\\|?*\x00-\x1f]/)
  assert.ok(escaped.identity.endsWith('_leaf'))
  assert.equal(escaped.process.version, punctuationVersion)
  assert.notEqual(escaped.identity, compileDoor(input, { ...processDraft, version: encodeURIComponent(punctuationVersion) }).identity)
})

test('door compiler has no input mutation or shared state across return values', () => {
  const input = freezeDeep({ widthMm: 437, heightMm: 471.25, hand: 'L' })
  const process = freezeDeep({ ...processDraft })
  const first = compileDoor(input, process)
  const expected = structuredClone(first)
  first.variables.DOOR_W = 1
  first.properties.hand = 'R'
  first.process.thicknessMm = 1
  compileDoor({ widthMm: 287, heightMm: 200, hand: 'R' }, process)
  assert.deepEqual(compileDoor(input, process), expected)
  assert.deepEqual(input, { widthMm: 437, heightMm: 471.25, hand: 'L' })
  assert.deepEqual(process, processDraft)
})

test('door compiler rejects missing dimensions, nonfinite values and unsupported hands', () => {
  for (const input of [undefined, null, [], {}]) assert.throws(() => compileDoor(input, processDraft), TypeError)
  for (const field of ['widthMm', 'heightMm']) {
    for (const value of [undefined, null, '437', NaN, Infinity, -Infinity, 0, -1]) {
      assert.throws(() => compileDoor({ widthMm: 437, heightMm: 471.25, hand: 'L', [field]: value }, processDraft), new RegExp(field))
    }
  }
  for (const hand of [undefined, null, 'l', 'r', 'left', '', 0]) {
    assert.throws(() => compileDoor({ widthMm: 437, heightMm: 471.25, hand }, processDraft), /hand/)
  }
})

test('door compiler rejects missing or conflicting process evidence', () => {
  const input = { widthMm: 437, heightMm: 471.25, hand: 'L' }
  for (const process of [undefined, null, [], {}]) assert.throws(() => compileDoor(input, process), TypeError)
  for (const field of Object.keys(processDraft)) {
    const missing = { ...processDraft }
    delete missing[field]
    assert.throws(() => compileDoor(input, missing), new RegExp(`process\\.${field}`))
  }
  for (const version of [undefined, null, 1, '', ' ', ' v1', 'v1 ']) {
    assert.throws(() => compileDoor(input, { ...processDraft, version }), /process.version/)
  }
  for (const [field, value] of [['thicknessMm', 1], ['thicknessMm', '0.8'], ['innerRadiusMm', 0.3], ['bendDeductionMm', 1.5], ['bendDeductionMm', NaN], ['status', 'confirmed']]) {
    assert.throws(() => compileDoor(input, { ...processDraft, [field]: value }), new RegExp(`process\\.${field}`))
  }
})
