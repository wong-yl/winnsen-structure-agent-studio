import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { compare16029PhysicalAudit } from './verify_16029_parametric_physical_quality.mjs'

const read = path => JSON.parse(readFileSync(path, 'utf8'))
const reference = read('data/parametric_attempts/PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
const corrected = read('data/parametric_attempts/PARAM-20260906050855850-6D6F04/evidence/physical-audit.json')
const unfixedFeet = read('data/parametric_attempts/PARAM-20260905125832647-3366EB/evidence/physical-audit.json')
assert.equal(compare16029PhysicalAudit(reference, corrected, { doorCount: 6 }).pass, true)
const rejected = compare16029PhysicalAudit(reference, unfixedFeet, { doorCount: 6 })
assert.equal(rejected.pass, false)
assert.ok(rejected.unexpected.some(row => row.reason === 'new_contact_category'))
const brokenFlat = structuredClone(corrected)
brokenFlat.rows.find(row => row.inspection === 'native_flat_pattern').rebuilt = false
assert.equal(compare16029PhysicalAudit(reference, brokenFlat, { doorCount: 6 }).pass, false)
const duplicated = structuredClone(corrected)
const contact = duplicated.rows.find(row => row.inspection === 'native_exact_solid_interference')
contact.collisions.push(structuredClone(contact.collisions.find(row => /hinge_latch_plate_bottom/.test(row.componentA + row.componentB))))
assert.equal(compare16029PhysicalAudit(reference, duplicated, { doorCount: 6 }).pass, false)
console.log('physical quality: actual V35 baseline and corrected model accepted; real foot regressions, failed flat pattern, duplicated contact rejected')
