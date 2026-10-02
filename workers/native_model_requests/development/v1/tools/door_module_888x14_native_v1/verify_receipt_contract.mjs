import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  NATIVE_RUNTIME_RECEIPT_KEYS,
  buildNativeStageReceiptContracts,
} from '../../../../../../tools/lib/locker_16029_native_stage_contract.mjs'
import { TRUSTED_NATIVE_RECIPE_REGISTRY } from '../../../../../../tools/locker_16029_native_recipe_registry.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(here, 'NativeDoorModule888x14.cs')
const executablePath = resolve(here, 'NativeDoorModule888x14.exe')
const source = readFileSync(sourcePath, 'utf8')
const recipe = TRUSTED_NATIVE_RECIPE_REGISTRY['winnsen-16029-888w-14door-native-v1']
const contract = buildNativeStageReceiptContracts(recipe).door_module_888x14

assert.equal(contract.schema, 'winnsen.16029.native_door_module_receipt.v1')
assert.equal(contract.path, 'receipts/door_module_888x14.json')
assert.equal(contract.predecessor, 'assemblies')
assert.deepEqual(NATIVE_RUNTIME_RECEIPT_KEYS, [
  'schema', 'phase', 'success', 'completedAt', 'taskId', 'taskRevision',
  'taskDigest', 'requestDigest', 'leaseId', 'leaseExpiresAt', 'attempt',
  'planSha256', 'authorizationId', 'authorizationSha256',
  'authorizationJsonBase64', 'authorizationIssuedAt', 'authorizationExpiresAt',
  'recipeId', 'recipeDigest', 'toolId', 'toolSourceNormalizedSha256',
  'toolExecutableSha256', 'evidencePath', 'evidenceSha256',
  'evidenceCommitmentSha256', 'preInventoryDigest', 'postInventoryDigest',
  'predecessorReceiptSha256',
])
assert.equal(source.includes('000D1C0B084A899065127E825F66A8DDBB94290BE8A2A609279FDEF0A0F8386C'), false)

const output = execFileSync(executablePath, ['--receipt-self-test'], {
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
  'exact_runtime_receipt_keys',
  'consumes_exact_width_assemblies_predecessor',
  'evidence_commitment_matches_shared_float_vector',
  'evidence_then_receipt_pair_has_no_hash_cycle',
  'rejects_modified_predecessor_receipt',
  'rejects_modified_evidence',
  'rejects_expired_authorization_before_commit',
  'refuses_unpaired_existing_evidence_or_receipt',
  'cleanup_refuses_unowned_existing_artifact',
  'cleanup_removes_only_owned_unpaired_artifacts',
  'source_inventory_digest_is_canonical_9e9c',
  'exact_evidence_and_receipt_paths',
  'safe_cleanup_refuses_reparse_or_hardlink',
]) {
  assert.equal(result.checks?.[name], true, `missing behavior gate: ${name}`)
}

console.log(JSON.stringify({
  status: 'STATIC_PASS_RUNTIME_NOT_RUN',
  checksTotal: Object.keys(result.checks).length + 6,
  checksFailed: 0,
}, null, 2))
