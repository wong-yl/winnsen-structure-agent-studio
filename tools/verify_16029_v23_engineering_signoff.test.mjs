import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const SCRIPT = resolve(ROOT, 'tools/verify_16029_v23_engineering_signoff.mjs')
const TEMPLATE = resolve(ROOT, 'data/locker_16029_v23_engineering_signoff.template.json')
const TMP_REL = 'tmp/locker_16029_v23_engineering_signoff_test'
const TMP = resolve(ROOT, TMP_REL)

function assert(ok, message) {
  if (!ok) throw new Error(message)
}

function runCase(name, signedPayload, expectedStatus, expectedExitCode) {
  const signedRel = `${TMP_REL}/${name}.json`
  const gateRel = `${TMP_REL}/${name}.gate.json`
  if (signedPayload) {
    writeFileSync(resolve(ROOT, signedRel), `${JSON.stringify(signedPayload, null, 2)}\n`, 'utf8')
  }
  const result = spawnSync(
    process.execPath,
    [SCRIPT, '--signed-file', signedRel, '--gate-file', gateRel],
    { cwd: ROOT, encoding: 'utf8' },
  )
  assert(result.status === expectedExitCode, `${name}: exit ${result.status}, expected ${expectedExitCode}\n${result.stderr}`)
  const gate = JSON.parse(result.stdout)
  assert(gate.status === expectedStatus, `${name}: status ${gate.status}, expected ${expectedStatus}`)
  assert(gate.production_release_eligible === false, `${name}: production release must stay false`)
}

rmSync(TMP, { recursive: true, force: true })
mkdirSync(TMP, { recursive: true })

try {
  const template = JSON.parse(readFileSync(TEMPLATE, 'utf8'))
  runCase('missing', null, 'AWAITING_ENGINEERING_SIGNOFF', 0)

  const accepted = structuredClone(template)
  accepted.status = 'SIGNED'
  accepted.decision = 'ACCEPT_FOR_PROTOTYPE'
  accepted.accepted_for_prototype = true
  accepted.historical_source_provenance_acknowledged = true
  accepted.prototype_boundary_acknowledged = true
  accepted.signer = {
    structure_engineer: 'TEST_ENGINEER',
    department: 'TEST_ONLY',
    signed_at: '2026-07-13T00:00:00Z',
  }
  accepted.review_items = accepted.review_items.map((item) => ({
    ...item,
    result: 'PASS',
    evidence: `test evidence for ${item.id}`,
  }))
  runCase('accepted', accepted, 'READY_FOR_PROTOTYPE', 0)

  const revision = structuredClone(accepted)
  revision.decision = 'RETURN_FOR_STRUCTURE_REVISION'
  revision.accepted_for_prototype = false
  revision.review_items[0].result = 'FAIL'
  revision.review_items[0].notes = 'test-only failure'
  runCase('revision', revision, 'STRUCTURE_REVISION_REQUIRED', 0)

  const invalidRelease = structuredClone(accepted)
  invalidRelease.production_release_eligible = true
  runCase('invalid_release', invalidRelease, 'INVALID_ENGINEERING_SIGNOFF', 1)

  console.log('v23 engineering signoff gate tests: PASS (4 cases)')
} finally {
  rmSync(TMP, { recursive: true, force: true })
}
