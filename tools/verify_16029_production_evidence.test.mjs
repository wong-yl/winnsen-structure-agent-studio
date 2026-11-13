import assert from 'node:assert/strict'
import { same16029PackageMarksEvidence } from './lib/locker_16029_production_evidence.mjs'

const sourcePath = 'C:\\cad\\source.SLDPRT'
const targetPath = 'C:\\cad\\target.SLDPRT'
const segment = (x) => ({ kind: 'line', a: [x, 0, 0], b: [x, 1, 0], length: 1 })
const contact = (index) => ({
  sourceComponent: `assembly/source-${index}`,
  sourcePath,
  sourceConfiguration: 'Default',
  sourceSha256: 'A'.repeat(64),
  sourceToRoot: [1, 0, 0, 1],
  targetComponent: `assembly/target-${index}`,
  targetPath,
  targetConfiguration: 'Default',
  rootToTarget: [1, 0, 0, 1],
  originalContactOuterLoop: [segment(0), segment(1)],
  projectedContactOuterLoop: [segment(0), segment(1)],
  materialClippedContactOuterLoop: [segment(0), segment(1)],
})
const baseline = [
  {
    family: '锁扣与挂钩真实接触轮廓', path: targetPath, configuration: 'Default', component: 'assembly/target-1',
    expectedSegments: [segment(0), segment(1)], actualSegments: [segment(0), segment(1)],
    sourceContactsAndTransforms: [contact(1), contact(2)],
  },
  {
    family: '侧加强筋朝上箭头与上', path: sourcePath, configuration: 'Default', component: 'assembly/source-1',
    expectedSegments: [segment(0), segment(1)], actualSegments: [segment(0), segment(1)],
    sourceContactsAndTransforms: [{ sourceContact: contact(1) }, { sourceContact: contact(2) }],
  },
]
const copy = () => structuredClone(baseline)
const changed = (edit) => {
  const value = copy()
  edit(value)
  return value
}

const reordered = changed(rows => {
  rows.reverse()
  for (const row of rows) {
    row.expectedSegments.reverse()
    row.actualSegments.reverse()
    row.sourceContactsAndTransforms.reverse()
    for (const entry of row.sourceContactsAndTransforms) {
      const item = entry.sourceContact || entry
      item.originalContactOuterLoop.reverse()
      item.projectedContactOuterLoop.reverse()
      item.materialClippedContactOuterLoop.reverse()
    }
  }
  rows.find(row => row.family === '锁扣与挂钩真实接触轮廓').component = 'assembly/target-2'
  rows.find(row => row.family === '侧加强筋朝上箭头与上').component = 'assembly/source-2'
})
assert.equal(same16029PackageMarksEvidence(baseline, reordered), true)
assert.equal(same16029PackageMarksEvidence(reordered, baseline), true)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].expectedSegments[0].a[0] += .001 })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].expectedSegments[1] = structuredClone(rows[0].expectedSegments[0]) })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].expectedSegments.pop() })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].sourceToRoot[0] += .001 })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].rootToTarget[0] += .001 })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].component = 'assembly/unrelated' })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].path = 'C:\\cad\\other.SLDPRT' })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].sourceSha256 = 'B'.repeat(64) })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].sourcePath = 'C:\\cad\\other.SLDPRT' })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].targetConfiguration = 'Other' })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[0].projectedContactOuterLoop[0].a[0] += .001 })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms[1] = structuredClone(rows[0].sourceContactsAndTransforms[0]) })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[0].sourceContactsAndTransforms.pop() })), false)
assert.equal(same16029PackageMarksEvidence(baseline, changed(rows => { rows[1].family = '锁扣与挂钩真实接触轮廓' })), false)
const wrongTargetOwner = changed(rows => { rows[0].path = sourcePath; rows[0].component = 'assembly/source-1' })
const wrongSourceOwner = changed(rows => { rows[1].path = targetPath; rows[1].component = 'assembly/target-1' })
assert.equal(same16029PackageMarksEvidence(wrongTargetOwner, wrongTargetOwner), false)
assert.equal(same16029PackageMarksEvidence(wrongSourceOwner, wrongSourceOwner), false)
console.log('production mark comparison: reordered geometry and valid representatives accepted; changed geometry, provenance and contacts rejected')
