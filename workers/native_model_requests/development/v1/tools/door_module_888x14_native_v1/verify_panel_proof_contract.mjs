import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const executablePath = resolve(here, 'NativeDoorModule888x14.exe')

const output = execFileSync(executablePath, ['--panel-proof-contract-self-test'], {
  cwd: here,
  encoding: 'utf8',
  windowsHide: true,
  timeout: 30_000,
})
const result = JSON.parse(output)

assert.equal(result.status, 'PASS')
assert.equal(result.cadStarted, false)
assert.equal(result.checksFailed, 0)
for (const name of [
  'exact_fixed_proof_and_audit_paths',
  'exact_proof_keys_and_commitment',
  'exact_audit_keys_and_commitment',
  'proof_binds_task_attempt_tool_authorization_and_source',
  'rejects_extra_proof_key',
  'rejects_tampered_proof_commitment',
  'rejects_tampered_audit_bytes',
  'rejects_wrong_authorization_binding',
  'rejects_wrong_source_hash',
  'rejects_expired_same_authorization',
  'panel_proof_does_not_create_stage_receipt',
]) {
  assert.equal(result.checks?.[name], true, `missing behavior gate: ${name}`)
}

console.log(JSON.stringify({
  status: 'STATIC_PASS_RUNTIME_NOT_RUN',
  checksTotal: Object.keys(result.checks).length + 3,
  checksFailed: 0,
}, null, 2))
