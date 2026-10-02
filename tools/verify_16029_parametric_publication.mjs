import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { validate16029ParametricPublication } from './lib/locker_16029_parametric_publication.mjs'

const complete = 'data/parametric_attempts/PARAM-20260906115210111-B86FBD'
const sha256 = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const valid = validate16029ParametricPublication(complete)
assert.equal(valid.ok, true, valid.reason)
assert.equal(valid.cadFileCount, 58)
assert.equal(valid.quality.flatPatternCount, 33)
assert.equal(valid.ancestorCount, 5)
const fourteen = validate16029ParametricPublication('data/parametric_attempts/PARAM-20260907151732299-D42ADE')
assert.equal(fourteen.ok, true, fourteen.reason)
assert.equal(fourteen.quality.doorCount, 14)
assert.equal(fourteen.quality.contactCount, 40)
const taller = validate16029ParametricPublication('data/parametric_attempts/PARAM-20260908052928978-57BA55')
assert.equal(taller.ok, true, taller.reason)
assert.equal(taller.request.cabinet.heightMm, 2017)
assert.equal(taller.quality.contactCount, 10)
const other = JSON.parse(readFileSync('data/parametric_839_fourteen_request.json', 'utf8'))
assert.match(validate16029ParametricPublication(complete, { expectedRequest: other }).reason, /requested parameters differ/)
assert.equal(validate16029ParametricPublication('data/parametric_attempts/PARAM-20260906050855850-6D6F04').ok, false)
assert.equal(validate16029ParametricPublication('data/parametric_attempts/PARAM-20260907145146239-6A867E').ok, false)
assert.equal(validate16029ParametricPublication('data/parametric_attempts/PARAM-20260906130141243-FB6337').ok, false)
assert.equal(validate16029ParametricPublication('data').ok, false)

const fixture = mkdtempSync(resolve('data/parametric_attempts', 'parametric_publication_test-'))
try {
  cpSync(complete, fixture, { recursive: true })
  const sourceEvidence = resolve(complete, 'evidence/verify-package.json')
  const copiedEvidence = resolve(fixture, 'evidence/verify-package.json')
  const sourceHash = sha256(sourceEvidence)
  assert.equal(sha256(copiedEvidence), sourceHash)
  writeFileSync(copiedEvidence, `${readFileSync(copiedEvidence, 'utf8')}\n`, 'utf8')
  assert.notEqual(sha256(copiedEvidence), sourceHash)
  const tampered = validate16029ParametricPublication(fixture)
  assert.equal(tampered.ok, false)
  assert.equal(tampered.modelReady, false)
  assert.match(tampered.reason, /receipt evidence or authorization hash mismatch/)
  assert.equal(sha256(sourceEvidence), sourceHash)
} finally {
  rmSync(fixture, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 })
}

console.log('parametric publication: complete 913 and 839 candidates accepted; parameter mismatch, missing relocation, failed physical comparison, diagnostic probe, outside path and isolated evidence tampering rejected')
