import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from './width_adapter.mjs'
import { adaptDoorSource, DOOR_SOURCE_PATH } from './door_adapter.mjs'
import { adaptLockSource, LOCK_SOURCE_PATH } from './lock_adapter.mjs'

const root = fileURLToPath(new URL('../../../', import.meta.url))
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const expectedDoorSha256 = 'A07174DEDF4F9F0958A731E679655B742E53BEDA063709F5F2F10BF2376572B0'
const specifications = [
  [WIDTH_SOURCE_PATH, 'C62D8E31BF027146E8ACA42FEC71CA2F48AFE56CCC0AB3370000D469807DB4C6', adaptWidthSource],
  [DOOR_SOURCE_PATH, expectedDoorSha256, source => adaptDoorSource(source, expectedDoorSha256)],
  [LOCK_SOURCE_PATH, 'D317BBAAEE1F38898680796F711768CC28105C3304AB3FB06DA99347E982C7D2', adaptLockSource],
]
const sources = specifications.map(([path, expected, adapt]) => {
  const fullPath = resolve(root, path)
  const bytes = readFileSync(fullPath)
  assert.equal(sha256(bytes), expected, `${path}: reviewed source changed`)
  const source = bytes.toString('utf8')
  const generated = adapt(source)
  assert.ok(generated.length > 0)
  assert.throws(() => adapt(source + '\n'), /hash mismatch|digest mismatch|revision changed/)
  assert.equal(sha256(readFileSync(fullPath)), expected, `${path}: adapter mutated input`)
  return { path, sha256: expected, generatedSha256: sha256(generated) }
})
console.log(JSON.stringify({ passed: true, verified: ['reviewed source bytes', 'source drift rejected', 'input files unchanged'], sources }))
