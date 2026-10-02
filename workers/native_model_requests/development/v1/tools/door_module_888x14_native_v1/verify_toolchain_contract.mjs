import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const executablePath = resolve(here, 'NativeDoorModule888x14.exe')

const output = execFileSync(executablePath, ['--toolchain-self-test'], {
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
  'accepts_exact_five_generic_and_one_lock_composite',
  'lock_composite_binds_primary_and_two_inspectors',
  'lock_composite_binds_live_validator',
  'rejects_lock_manifest_missing_required_tool',
  'rejects_lock_manifest_with_extra_top_level_key',
  'rejects_lock_manifest_with_extra_tool_key',
  'rejects_lock_manifest_with_tampered_live_artifact',
  'rejects_generic_manifest_for_lock_tool',
  'rejects_composite_manifest_for_non_lock_tool',
  'generic_source_normalization_masks_only_identity_stamps',
  'generic_source_normalization_preserves_declaration_format',
  'generic_source_normalization_detects_nonstamp_tamper',
]) {
  assert.equal(result.checks?.[name], true, `missing behavior gate: ${name}`)
}

const liveOutput = execFileSync(executablePath, ['--toolchain-live-check'], {
  cwd: here,
  encoding: 'utf8',
  windowsHide: true,
  timeout: 30_000,
})
const live = JSON.parse(liveOutput)
assert.equal(live.status, 'PASS')
assert.equal(live.cadStarted, false)
assert.equal(Object.keys(live.manifestHashes || {}).length, 6)
assert.equal(live.schemas?.native_lock_topology_888x14_v1,
  'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1')
for (const hash of Object.values(live.manifestHashes || {})) {
  assert.match(hash, /^[A-F0-9]{64}$/)
}

console.log(JSON.stringify({
  status: 'STATIC_PASS_RUNTIME_NOT_RUN',
  checksTotal: Object.keys(result.checks).length + 8,
  checksFailed: 0,
}, null, 2))
