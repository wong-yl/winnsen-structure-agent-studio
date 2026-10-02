#!/usr/bin/env node

import { createHash } from 'node:crypto'
import {
  closeSync, existsSync, fsyncSync, lstatSync, mkdirSync, openSync, readFileSync,
  mkdtempSync, renameSync, rmSync, statSync, unlinkSync, writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, extname, isAbsolute, join, relative, resolve, win32 } from 'node:path'

const SCHEMA = 'winnsen.locker16029.native_888x14_lock_topology_validation.v2'
const BUILD_SCHEMA = 'winnsen.locker16029.native_888x14_lock_topology.v2'
const INSPECTOR_SCHEMA = 'winnsen.locker16029.native_888x14_lock_topology_inspector.v1'
const TONGUE_SCHEMA = 'winnsen.locker16029.native_888x14_assembly_tongues.v1'
const TOOLCHAIN_SCHEMA = 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1'
const COMMON_TOOLCHAIN_SCHEMA = 'winnsen.16029.native_toolchain_manifest.v1'
const ROOT_TOOL_ID = 'native_root_assembly_888x14_v1'
const ROOT_EVIDENCE_SCHEMA = 'winnsen.16029.native_root_assembly_result.v1'
const ROOT_EVIDENCE_RELATIVE_PATH = 'evidence/root_assembly_888x14.result.v1.json'
const TOOLCHAIN_PLAN_KEY = 'native_lock_topology_888x14_v1'
const TRUSTED_TOOLCHAIN_MANIFEST_KEYS = Object.freeze([
  'native_seed_pack_888x14_v1', 'native_width_888_v1', 'native_door_module_888x14_v1',
  TOOLCHAIN_PLAN_KEY, 'native_root_assembly_888x14_v1', 'native_final_pack_888x14_v1',
])
const STAGE_RECEIPT_ORDER = Object.freeze([
  'clone_native_seed', 'dimensions', 'derived', 'base-hole', 'assemblies', 'door_module_888x14',
  'lock_topology_888x14', 'root_assembly_888x14', 'final_pack_and_relocated_reopen',
])
const STAGE_RECEIPT_CONTRACTS = Object.freeze({
  clone_native_seed: { schema: 'winnsen.16029.native_seed_pack_receipt.v1', path: 'receipts/clone_native_seed.json', producerToolId: 'native_seed_pack_888x14_v1', predecessor: '' },
  dimensions: { schema: 'winnsen.native_width_888.phase_receipt.v1', path: 'evidence/width-dimensions.receipt.json', producerToolId: 'native_width_888_v1', predecessor: 'clone_native_seed' },
  derived: { schema: 'winnsen.native_width_888.phase_receipt.v1', path: 'evidence/width-derived.receipt.json', producerToolId: 'native_width_888_v1', predecessor: 'dimensions' },
  'base-hole': { schema: 'winnsen.native_width_888.phase_receipt.v1', path: 'evidence/width-base-hole.receipt.json', producerToolId: 'native_width_888_v1', predecessor: 'derived' },
  assemblies: { schema: 'winnsen.native_width_888.phase_receipt.v1', path: 'evidence/width-assemblies.receipt.json', producerToolId: 'native_width_888_v1', predecessor: 'base-hole' },
  door_module_888x14: { schema: 'winnsen.16029.native_door_module_receipt.v1', path: 'receipts/door_module_888x14.json', producerToolId: 'native_door_module_888x14_v1', predecessor: 'assemblies' },
  lock_topology_888x14: { schema: 'winnsen.16029.native_lock_topology_receipt.v1', path: 'receipts/lock_topology_888x14.json', producerToolId: 'native_lock_topology_888x14_v1', predecessor: 'door_module_888x14' },
  root_assembly_888x14: { schema: 'winnsen.16029.native_root_assembly_receipt.v1', path: 'receipts/root_assembly_888x14.json', producerToolId: 'native_root_assembly_888x14_v1', predecessor: 'lock_topology_888x14' },
  final_pack_and_relocated_reopen: { schema: 'winnsen.16029.native_final_pack_receipt.v1', path: 'receipts/final_pack_and_relocated_reopen.json', producerToolId: 'native_final_pack_888x14_v1', predecessor: 'root_assembly_888x14' },
})
const LOCK_PREDECESSOR_RECEIPTS = STAGE_RECEIPT_ORDER.slice(0, 6)
const RUNTIME_RECEIPT_KEYS = Object.freeze([
  'schema','phase','success','completedAt','taskId','taskRevision','taskDigest','requestDigest',
  'leaseId','leaseExpiresAt','attempt','planSha256','authorizationId','authorizationSha256',
  'authorizationJsonBase64','authorizationIssuedAt','authorizationExpiresAt','recipeId','recipeDigest',
  'toolId','toolSourceNormalizedSha256','toolExecutableSha256','evidencePath','evidenceSha256',
  'evidenceCommitmentSha256','preInventoryDigest','postInventoryDigest','predecessorReceiptSha256',
])
const SEED_RECEIPT_KEYS = Object.freeze([
  'schema','phase','success','completedAt','taskId','taskRevision','taskDigest','requestDigest',
  'leaseId','leaseExpiresAt','attempt','planSha256','authorizationId','authorizationSha256',
  'authorizationJsonBase64','authorizationIssuedAt','authorizationExpiresAt','recipeId','recipeDigest',
  'toolId','toolSourceNormalizedSha256','toolExecutableSha256','evidencePath','evidenceSha256',
  'evidenceCommitmentSha256','sourceInventoryDigest','targetInventoryDigest','nonRootFileCount',
  'nonRootExactSource','rootFileName','rootShaBefore','rootShaAfterStable','initialOpenErrors',
  'initialOpenWarnings','stabilizeSaveErrors','stabilizeSaveWarnings','reopenErrors','reopenWarnings',
  'dependencyClosureCount','dependenciesAllTargetLocal','knownRootIssueGate','predecessorReceiptSha256',
])
const WORKSPACE_ROOT = 'D:\\Winnsen_Structure_Agent_Studio'
const ROOT_TOOLCHAIN_MANIFEST_PATH = join(WORKSPACE_ROOT, 'workers', 'native_model_requests', 'development', 'v1', 'tools', 'root_assembly_888x14_native_v1', 'toolchain_manifest.json')
const ROOT_CONTRACT_SNAPSHOT_PATH = join(WORKSPACE_ROOT, 'workers', 'native_model_requests', 'development', 'v1', 'tools', 'root_assembly_888x14_native_v1', 'assembly_contract.snapshot.json')
const EXPECTED_SW_EXE_PATH = 'D:\\soildworks2020\\SOLIDWORKS\\SLDWORKS.exe'
const RECIPE_ID = 'winnsen-16029-888w-14door-native-v1'
const RECIPE_DIGEST = 'F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8'
const TRUSTED_TONGUE_NAME = '锁舌.SLDPRT'
const TRUSTED_TONGUE_SHA256 = '26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719'
const EXPECTED_SW_EXE_SHA256 = '1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978'
const PART_INSPECTOR_SOURCE_PATH = join(dirname(new URL(import.meta.url).pathname.replace(/^\//, '')), 'InspectLockTopology888x14.cs')
const PART_INSPECTOR_EXE_PATH = join(dirname(new URL(import.meta.url).pathname.replace(/^\//, '')), 'bin', 'InspectLockTopology888x14.exe')
const TONGUE_INSPECTOR_SOURCE_PATH = join(dirname(new URL(import.meta.url).pathname.replace(/^\//, '')), 'InspectAssemblyTongues888x14.cs')
const TONGUE_INSPECTOR_EXE_PATH = join(dirname(new URL(import.meta.url).pathname.replace(/^\//, '')), 'bin', 'InspectAssemblyTongues888x14.exe')
function normalizedInspectorHash(path) {
  return sha256Bytes(Buffer.from(readFileSync(path, 'utf8').replace(/private const string ExpectedSourceSha256\s*=\s*"(?:__SOURCE_SHA256__|[0-9A-F]{64})";/, (match) => match.replace(/[0-9A-F]{64}/, '__SOURCE_SHA256__'))))
}
const EXPECTED_PART_INSPECTOR_SOURCE_SHA256 = existsSync(PART_INSPECTOR_SOURCE_PATH) ? normalizedInspectorHash(PART_INSPECTOR_SOURCE_PATH) : ''
const EXPECTED_TONGUE_INSPECTOR_SOURCE_SHA256 = existsSync(TONGUE_INSPECTOR_SOURCE_PATH) ? normalizedInspectorHash(TONGUE_INSPECTOR_SOURCE_PATH) : ''
const SLOT_ROWS = Array.from({ length: 7 }, (_, index) => 32 + ((((12 / 7) * 152.5) - 7) / 2) + index * ((12 / 7) * 152.5))
const CIRCLE_ROWS = SLOT_ROWS.map((value) => value - 60)
const SLOT_TOLERANCE_MM = 0.01
const EDGE_TOLERANCE_MM = 0.0005
const TONGUE_TOLERANCE_MM = 0.001
const CIRCLE_LENGTH_MM = Math.PI * 5
const EVIDENCE_COMMITMENT_EXCLUDED_KEYS = Object.freeze([
  'evidence_write_attempted','evidence_write_succeeded','evidence_finalize_write_succeeded',
  'evidence_finalize_error','failure_evidence_write_succeeded','phase_receipt_path',
  'phase_receipt_sha256','phase_receipt_committed','authorization_checkpoints',
  'completed_at_utc','commit_completed_at_utc','evidence_commitment_sha256',
  'backup_deleted','backup_delete_error',
])

const SLOT_TEMPLATES = [
  [0.788322, [61.7, -5.708124, -19.3], [62.2, -5.639994, -19.8]],
  [0.788322, [62.2, 5.639994, -19.8], [61.7, 5.708124, -19.3]],
  [0.8, [61.7, -5.708124, -19.3], [61.7, -5.708124, -18.5]],
  [0.8, [62.2, -5.639994, -19.8], [63, -5.639994, -19.8]],
  [0.8, [62.2, -4.497259, -41.604672], [63, -4.497259, -41.604672]],
  [0.8, [62.2, -2.5, -43.5], [63, -2.5, -43.5]],
  [0.8, [62.2, 2.5, -43.5], [63, 2.5, -43.5]],
  [0.8, [62.2, 4.497259, -41.604672], [63, 4.497259, -41.604672]],
  [0.8, [62.2, 5.639994, -19.8], [63, 5.639994, -19.8]],
  [0.8, [61.7, 5.708124, -19.3], [61.7, 5.708124, -18.5]],
  [2.043171, [61.7, -5.708124, -18.5], [63, -5.639994, -19.8]],
  [2.043171, [63, 5.639994, -19.8], [61.7, 5.708124, -18.5]],
  [3.036873, [62.2, -2.5, -43.5], [62.2, -4.497259, -41.604672]],
  [3.036873, [63, -2.5, -43.5], [63, -4.497259, -41.604672]],
  [3.036873, [62.2, 4.497259, -41.604672], [62.2, 2.5, -43.5]],
  [3.036873, [63, 4.497259, -41.604672], [63, 2.5, -43.5]],
  [5, [63, 2.5, -43.5], [63, -2.5, -43.5]],
  [5, [62.2, 2.5, -43.5], [62.2, -2.5, -43.5]],
  [21.834595, [63, -4.497259, -41.604672], [63, -5.639994, -19.8]],
  [21.834595, [62.2, -4.497259, -41.604672], [62.2, -5.639994, -19.8]],
  [21.834595, [62.2, 5.639994, -19.8], [62.2, 4.497259, -41.604672]],
  [21.834595, [63, 5.639994, -19.8], [63, 4.497259, -41.604672]],
].map(([length, a, b], index) => ({ id: `E${String(index + 1).padStart(2, '0')}`, length, a, b }))

function usage() {
  return [
    'Usage:',
    '  node ValidateLockTopology888x14.mjs --build <build.json> --inspector <inspector.json> --tongues <assembly-tongues.json> --output <result.json>',
    '  node ValidateLockTopology888x14.mjs --self-test --toolchain-manifest <manifest.json> --toolchain-manifest-sha256 <SHA256>',
  ].join('\n')
}

function parseArguments(argv) {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true }
  const allowed = new Set(['--build', '--inspector', '--tongues', '--output', '--toolchain-manifest', '--toolchain-manifest-sha256'])
  const values = {}
  const selfTestRequested = argv.includes('--self-test')
  const filtered = argv.filter((value) => value !== '--self-test')
  for (let index = 0; index < filtered.length; index += 2) {
    const key = filtered[index]
    const value = filtered[index + 1]
    if (!allowed.has(key) || !value || value.startsWith('--')) throw new Error(usage())
    const names = { '--toolchain-manifest': 'toolchainManifest', '--toolchain-manifest-sha256': 'toolchainManifestSha256' }
    values[names[key] ?? key.slice(2)] = value
  }
  if (selfTestRequested) { values.selfTest = true; return values }
  if (!values.build || !values.inspector || !values.tongues || !values.output || !values.toolchainManifest || !values.toolchainManifestSha256) throw new Error(usage())
  return values
}

function sha256Bytes(bytes) {
  return createHash('sha256').update(bytes).digest('hex').toUpperCase()
}

function sha256File(path) {
  return sha256Bytes(readFileSync(path))
}

function readEvidence(path) {
  const absolute = resolve(path)
  if (!existsSync(absolute)) throw new Error(`Evidence file does not exist: ${absolute}`)
  const bytes = readFileSync(absolute)
  return { absolute, sha256: sha256Bytes(bytes), document: JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, '')) }
}

function samePath(left, right) {
  return win32.normalize(resolve(String(left ?? ''))).toLowerCase() === win32.normalize(resolve(String(right ?? ''))).toLowerCase()
}

function isUnder(path, root) {
  const value = resolve(path)
  const base = resolve(root)
  const rel = relative(base, value)
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function close(left, right, tolerance = SLOT_TOLERANCE_MM) {
  return Number.isFinite(Number(left)) && Number.isFinite(Number(right)) && Math.abs(Number(left) - Number(right)) <= tolerance
}

function rowsEqual(actual, expected, tolerance = SLOT_TOLERANCE_MM) {
  return Array.isArray(actual) && actual.length === expected.length && actual.every((value, index) => close(value, expected[index], tolerance))
}

function add(checks, name, ok, actual, expected) {
  checks.push({ name, ok: Boolean(ok), actual, expected })
}

function exactKeys(value, expected) {
  return value && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function canonicalReceiptPath(path) {
  return typeof path === 'string' && /^(?:receipts|evidence)\/[A-Za-z0-9_.-]+\.json$/.test(path) &&
    !path.includes('\\') && !path.includes('..')
}

function stageReceiptContractsMatch(value) {
  return exactKeys(value, STAGE_RECEIPT_ORDER) && STAGE_RECEIPT_ORDER.every((id) => {
    const row = value[id]
    const expected = STAGE_RECEIPT_CONTRACTS[id]
    return exactKeys(row, ['schema','path','producerToolId','predecessor']) &&
      row.schema === expected.schema && row.path === expected.path &&
      row.producerToolId === expected.producerToolId && row.predecessor === expected.predecessor &&
      canonicalReceiptPath(row.path)
  })
}

function trustedToolchainManifestsMatch(value, lockManifestSha256) {
  return exactKeys(value, TRUSTED_TOOLCHAIN_MANIFEST_KEYS) &&
    TRUSTED_TOOLCHAIN_MANIFEST_KEYS.every((id) => /^[A-F0-9]{64}$/i.test(String(value[id] ?? ''))) &&
    String(value[TOOLCHAIN_PLAN_KEY]).toUpperCase() === String(lockManifestSha256).toUpperCase()
}

function stableValue(value) {
  if (Array.isArray(value)) return value.map(stableValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stableValue(value[key])]))
}
function selfDigest(doc) {
  const copy = { ...doc }; delete copy.self_digest
  return sha256Bytes(Buffer.from(JSON.stringify(stableValue(copy))))
}
function evidenceCommitment(doc) {
  const copy = { ...doc }
  for (const key of EVIDENCE_COMMITMENT_EXCLUDED_KEYS) delete copy[key]
  return sha256Bytes(Buffer.from(JSON.stringify(stableValue(copy))))
}
function loadToolchain(path, expectedSha) {
  const absolute = resolve(path)
  if (!samePath(absolute, join(dirname(PART_INSPECTOR_SOURCE_PATH), 'lock_toolchain_manifest.json')) || sha256File(absolute) !== String(expectedSha).toUpperCase()) throw new Error('toolchain manifest path/hash mismatch')
  const doc = JSON.parse(readFileSync(absolute, 'utf8'))
  if (!exactKeys(doc, ['schema','generatedBy','tools','validator']) || doc.schema !== TOOLCHAIN_SCHEMA || doc.generatedBy !== 'VerifyLockTopology888x14Static.mjs' || !exactKeys(doc.tools, ['native_lock_topology_888x14_v1','native_lock_topology_inspector_v1','native_assembly_tongue_inspector_888x14_v1']) || !samePath(doc.validator.path, new URL(import.meta.url).pathname.replace(/^\//,'')) || sha256File(doc.validator.path) !== String(doc.validator.sha256).toUpperCase()) throw new Error('toolchain manifest contract mismatch')
  for (const [id, tool] of Object.entries(doc.tools)) {
    if (!exactKeys(tool, ['sourcePath','sourceNormalizedSha256','executablePath','executableSha256']) || !samePath(tool.sourcePath, id === 'native_lock_topology_888x14_v1' ? join(dirname(PART_INSPECTOR_SOURCE_PATH), 'BuildLockTopology888x14.cs') : id === 'native_lock_topology_inspector_v1' ? PART_INSPECTOR_SOURCE_PATH : TONGUE_INSPECTOR_SOURCE_PATH) || !liveNormalizedSourceHash(tool.sourcePath, tool.sourceNormalizedSha256) || !liveHashAnywhere(tool.executablePath, tool.executableSha256)) throw new Error(`toolchain manifest live identity mismatch: ${id}`)
  }
  return { absolute, sha256: String(expectedSha).toUpperCase(), document: doc }
}

function authorizationIdentityMatches(authorization) {
  if (!authorization || typeof authorization !== 'object') return false
  const bindingKeys = ['workerId','task','request','plan','recipe','tool','seed','execution','issuedAt','expiresAt']
  if (!bindingKeys.every((key) => Object.hasOwn(authorization, key))) return false
  const binding = Object.fromEntries(bindingKeys.map((key) => [key, authorization[key]]))
  const expected = `native-auth-${sha256Bytes(Buffer.from(JSON.stringify(stableValue(binding)))).slice(0, 32).toLowerCase()}`
  return authorization.authorizationId === expected
}

function manifestArtifactPath(path) {
  if (typeof path !== 'string' || path.length === 0) return ''
  return isAbsolute(path) ? resolve(path) : resolve(WORKSPACE_ROOT, path)
}

function liveRootNormalizedSourceHash(path, expectedHash) {
  if (typeof path !== 'string' || !isAbsolute(path) || !existsSync(path) ||
      !isUnder(path, WORKSPACE_ROOT) || pathHasLink(path, WORKSPACE_ROOT) ||
      !/^[0-9A-F]{64}$/i.test(String(expectedHash ?? ''))) return false
  const source = readFileSync(path, 'utf8').replace(/\r\n?/g, '\n')
    .replace(/private const string ExpectedSourceSha256 = "[A-Z0-9_]+";/,
      'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";')
    .replace(/private const string ExpectedContractSnapshotSha256 = "[A-Z0-9_]+";/,
      'private const string ExpectedContractSnapshotSha256 = "__CONTRACT_SHA256__";')
  return sha256Bytes(Buffer.from(source, 'utf8')) === String(expectedHash).toUpperCase()
}

function rootToolchainBinding(plan, rootEvidenceTool, rootManifestTool) {
  try {
    const manifests = plan?.trustedToolchainManifests
    if (!trustedToolchainManifestsMatch(manifests, manifests?.[TOOLCHAIN_PLAN_KEY]) ||
        !existsSync(ROOT_TOOLCHAIN_MANIFEST_PATH) || pathHasLink(ROOT_TOOLCHAIN_MANIFEST_PATH, WORKSPACE_ROOT) ||
        sha256File(ROOT_TOOLCHAIN_MANIFEST_PATH) !== String(manifests?.[ROOT_TOOL_ID] ?? '').toUpperCase()) return false
    const doc = JSON.parse(readFileSync(ROOT_TOOLCHAIN_MANIFEST_PATH, 'utf8'))
    const tool = doc.tool ?? {}
    if (!exactKeys(doc, ['schema','generatedBy','tool']) || doc.schema !== COMMON_TOOLCHAIN_SCHEMA ||
        typeof doc.generatedBy !== 'string' || doc.generatedBy.length === 0 ||
        !exactKeys(tool, ['id','sourcePath','sourceNormalizedSha256','executablePath','executableSha256','verifierPath','verifierSha256']) ||
        tool.id !== ROOT_TOOL_ID) return false
    const sourcePath = manifestArtifactPath(tool.sourcePath)
    const executablePath = manifestArtifactPath(tool.executablePath)
    const verifierPath = manifestArtifactPath(tool.verifierPath)
    return liveRootNormalizedSourceHash(sourcePath, tool.sourceNormalizedSha256) &&
      liveHashAnywhere(executablePath, tool.executableSha256) && liveHashAnywhere(verifierPath, tool.verifierSha256) &&
      exactKeys(rootEvidenceTool, ['id','sourceNormalizedSha256','executableSha256','contractSnapshotSha256']) &&
      rootEvidenceTool.id === ROOT_TOOL_ID &&
      String(rootEvidenceTool.sourceNormalizedSha256).toUpperCase() === String(tool.sourceNormalizedSha256).toUpperCase() &&
      String(rootEvidenceTool.executableSha256).toUpperCase() === String(tool.executableSha256).toUpperCase() &&
      liveHashAnywhere(ROOT_CONTRACT_SNAPSHOT_PATH, rootEvidenceTool.contractSnapshotSha256) &&
      exactKeys(rootManifestTool, ['id','sourceNormalizedSha256','executableSha256']) &&
      rootManifestTool.id === ROOT_TOOL_ID &&
      String(rootManifestTool.sourceNormalizedSha256).toUpperCase() === String(tool.sourceNormalizedSha256).toUpperCase() &&
      String(rootManifestTool.executableSha256).toUpperCase() === String(tool.executableSha256).toUpperCase()
  } catch { return false }
}

function ownedMonitorEvidence(session) {
  return Array.isArray(session?.monitor_processes) && session.monitor_processes.every((row) =>
    row?.ownership_verified === true && Number(row.pid) > 0 && Number(row.start_ticks_utc) > 0 &&
    Number(row.parent_sldworks_process_id) === Number(session.sldworks_process_id) &&
    new RegExp(`(?:^|\\s)--ppid=${Number(session.sldworks_process_id)}(?:\\s|$)`).test(String(row.command_line ?? '')))
}

function pinnedPartInspector(tool, toolchain) {
  const pinned = toolchain?.tools?.native_lock_topology_inspector_v1
  return pinned && tool?.id === 'native_lock_topology_inspector_v1' && samePath(tool.source_path, pinned.sourcePath) &&
    samePath(tool.executable_path, pinned.executablePath) && String(tool.source_sha256).toUpperCase() === String(pinned.sourceNormalizedSha256).toUpperCase() && String(tool.executable_sha256).toUpperCase() === String(pinned.executableSha256).toUpperCase() &&
    liveHashAnywhere(tool.executable_path, tool.executable_sha256)
}

function pathHasLink(path, stopRoot) {
  if (!existsSync(path)) return true
  let current = resolve(path)
  const root = resolve(stopRoot)
  while (isUnder(current, root)) {
    const stat = lstatSync(current)
    if (stat.isSymbolicLink() || (stat.isFile() && stat.nlink !== 1)) return true
    if (samePath(current, root)) return false
    current = dirname(current)
  }
  return true
}

function liveHashGate(path, expectedHash, root) {
  return typeof path === 'string' && existsSync(path) && isUnder(path, root) &&
    !pathHasLink(path, root) && /^[0-9A-F]{64}$/i.test(String(expectedHash ?? '')) &&
    sha256File(path) === String(expectedHash).toUpperCase()
}

function liveHashAnywhere(path, expectedHash) {
  if (typeof path !== 'string' || !isAbsolute(path) || !existsSync(path) ||
      !isUnder(path, WORKSPACE_ROOT) || pathHasLink(path, WORKSPACE_ROOT) ||
      !/^[0-9A-F]{64}$/i.test(String(expectedHash ?? ''))) return false
  const stat = lstatSync(path)
  return !stat.isSymbolicLink() && (!stat.isFile() || stat.nlink === 1) &&
    sha256File(path) === String(expectedHash).toUpperCase()
}

function liveNormalizedSourceHash(path, expectedHash) {
  if (typeof path !== 'string' || !isAbsolute(path) || !existsSync(path) ||
      !isUnder(path, WORKSPACE_ROOT) || pathHasLink(path, WORKSPACE_ROOT) ||
      !/^[0-9A-F]{64}$/i.test(String(expectedHash ?? ''))) return false
  const stat = lstatSync(path)
  return stat.isFile() && stat.nlink === 1 &&
    normalizedInspectorHash(path) === String(expectedHash).toUpperCase()
}

function point(value) {
  return Array.isArray(value) && value.length >= 3 && value.slice(0, 3).every((number) => Number.isFinite(Number(number)))
}

function pointClose(left, right, tolerance = SLOT_TOLERANCE_MM) {
  return point(left) && point(right) && [0, 1, 2].every((index) => close(left[index], right[index], tolerance))
}

function edgeMatches(edge, template, row) {
  if (!close(edge.length_mm, template.length, EDGE_TOLERANCE_MM) || !point(edge.vertex_start_mm) || !point(edge.vertex_end_mm)) return false
  const normalize = (value) => [Math.abs(Number(value[0])), Number(value[1]) - row, Number(value[2])]
  const a = normalize(edge.vertex_start_mm)
  const b = normalize(edge.vertex_end_mm)
  return (pointClose(a, template.a) && pointClose(b, template.b)) || (pointClose(a, template.b) && pointClose(b, template.a))
}

function slotAt(edges, row, side) {
  const used = new Set()
  const matches = SLOT_TEMPLATES.map((template) => {
    const indexes = edges.filter((edge) => edgeMatches(edge, template, row)).map((edge) => edge.index)
    indexes.forEach((index) => used.add(index))
    return { id: template.id, indexes }
  })
  const sign = side === 'L' ? -1 : 1
  const orientation = [...used].every((index) => {
    const edge = edges.find((candidate) => candidate.index === index)
    return edge && [edge.vertex_start_mm, edge.vertex_end_mm].every((value) => point(value) && Math.sign(Number(value[0])) === sign)
  })
  return { row, edgeCount: used.size, complete: used.size === 22 && matches.every((item) => item.indexes.length === 1) && orientation }
}

function circleAt(edges, row, side) {
  const sign = side === 'L' ? -1 : 1
  const templates = [{ x: 42.5, z: -19.3 }, { x: 47.5, z: -18.5 }]
  const matches = templates.map((template) => edges.filter((edge) => {
    const start = point(edge.param_start_mm) ? edge.param_start_mm : edge.vertex_start_mm
    return point(start) && close(edge.length_mm, CIRCLE_LENGTH_MM, EDGE_TOLERANCE_MM) &&
      close(Math.abs(start[0]), template.x) && close(start[1], row) && close(start[2], template.z) &&
      Math.sign(Number(start[0])) === sign
  }))
  return { row, complete: matches.every((values) => values.length === 1), edgeCount: new Set(matches.flat().map((edge) => edge.index)).size }
}

function analyzePart(part, side, sourcePath, attemptRoot, checks) {
  const edges = Array.isArray(part?.edges) ? part.edges.map((edge, index) => ({ ...edge, index: Number(edge.index ?? index + 1) })) : []
  const slots = SLOT_ROWS.map((row) => slotAt(edges, row, side))
  const circles = CIRCLE_ROWS.map((row) => circleAt(edges, row, side))
  const sourceSha = String(part?.source_sha256 ?? '').toUpperCase()
  add(checks, `${side}_inspector_source_path_and_live_hash`, samePath(part?.source_path, sourcePath) && liveHashGate(sourcePath, sourceSha, attemptRoot), { path: part?.source_path, sha256: sourceSha }, { path: sourcePath, liveHash: true })
  add(checks, `${side}_independent_readonly_reopen`, part?.opened === true && part?.read_only === true && part?.save_api_calls === 0 && part?.source_unchanged === true && part?.open_errors === 0 && part?.open_warnings === 0 && part?.rebuild_succeeded === true, part, { readOnlyFreshReopen: true, saveApiCalls: 0, errors: 0, warnings: 0 })
  add(checks, `${side}_native_sheet_metal_health`, part?.body_count === 1 && part?.has_sheet_metal === true && part?.has_flat_pattern === true && part?.traversal_complete === true && part?.error_feature_count === 0 && part?.warning_feature_count === 0 && part?.forbidden_import_feature_count === 0 && part?.external_reference_count === 0, part, { oneBody: true, SheetMetal: true, FlatPattern: true, BaseBodyOrImported: 0, externalReferences: 0 })
  if (side === 'R') add(checks, 'R_native_mirror_feature', Number(part?.mirror_feature_count) >= 1 && Array.isArray(part?.feature_types) && part.feature_types.some((type) => /^(MirrorStock|MirrorPart)$/i.test(String(type))), { count: part?.mirror_feature_count, types: part?.feature_types }, { MirrorStockOrMirrorPart: true })
  add(checks, `${side}_exact_7x22_slots`, slots.every((item) => item.complete), slots, SLOT_ROWS.map((row) => ({ row, edgeCount: 22, complete: true })))
  add(checks, `${side}_exact_7_paired_diameter5_holes`, circles.every((item) => item.complete && item.edgeCount === 2), circles, CIRCLE_ROWS.map((row) => ({ row, edgeCount: 2, complete: true })))
  return { sourcePath, sourceSha256: sourceSha, slots, circles, edgeDigest: sha256Bytes(Buffer.from(JSON.stringify(edges))) }
}

function validateLockReceiptAndPredecessors(build, context, toolchainBinding, checks) {
  const doc = build.document
  const receiptPath = join(context.attemptRoot, 'receipts', 'lock_topology_888x14.json')
  let receipt = {}
  try { if (existsSync(receiptPath)) receipt = JSON.parse(readFileSync(receiptPath, 'utf8')) } catch {}
  let authorizationBytes = Buffer.alloc(0)
  try { authorizationBytes = Buffer.from(String(receipt.authorizationJsonBase64 ?? ''), 'base64') } catch {}
  const doorReceiptPath = join(context.attemptRoot, ...STAGE_RECEIPT_CONTRACTS.door_module_888x14.path.split('/'))
  add(checks, 'lock_receipt_atomic_evidence_pair_binding', existsSync(receiptPath) &&
    !pathHasLink(receiptPath, context.attemptRoot) && exactKeys(receipt, RUNTIME_RECEIPT_KEYS) &&
    receipt.schema === 'winnsen.16029.native_lock_topology_receipt.v1' &&
    receipt.phase === 'lock_topology_888x14' && receipt.success === true &&
    receipt.completedAt === doc.completed_at_utc && receipt.taskId === doc.task_id &&
    Number(receipt.taskRevision) === Number(doc.task_revision) &&
    String(receipt.taskDigest).toUpperCase() === String(doc.task_digest).toUpperCase() &&
    String(receipt.requestDigest).toUpperCase() === String(doc.request_digest).toUpperCase() &&
    receipt.leaseId === doc.lease_id && receipt.leaseExpiresAt === doc.lease_expires_at_utc &&
    Number(receipt.attempt) === Number(doc.attempt_number) &&
    String(receipt.planSha256).toUpperCase() === String(doc.plan_sha256).toUpperCase() &&
    receipt.authorizationId === doc.authorization_id &&
    String(receipt.authorizationSha256).toUpperCase() === String(doc.authorization_sha256).toUpperCase() &&
    sha256Bytes(authorizationBytes) === String(receipt.authorizationSha256).toUpperCase() &&
    Buffer.compare(authorizationBytes, readFileSync(doc.authorization_path)) === 0 &&
    receipt.authorizationIssuedAt === doc.authorization_issued_at_utc &&
    receipt.authorizationExpiresAt === doc.authorization_expires_at_utc &&
    receipt.recipeId === RECIPE_ID && String(receipt.recipeDigest).toUpperCase() === RECIPE_DIGEST &&
    receipt.toolId === TOOLCHAIN_PLAN_KEY &&
    String(receipt.toolSourceNormalizedSha256).toUpperCase() === String(toolchainBinding.document.tools[TOOLCHAIN_PLAN_KEY].sourceNormalizedSha256).toUpperCase() &&
    String(receipt.toolExecutableSha256).toUpperCase() === String(toolchainBinding.document.tools[TOOLCHAIN_PLAN_KEY].executableSha256).toUpperCase() &&
    receipt.evidencePath === 'evidence/lock_topology_888x14.v2.json' &&
    String(receipt.evidenceSha256).toUpperCase() === build.sha256 &&
    String(receipt.evidenceCommitmentSha256).toUpperCase() === evidenceCommitment(doc) &&
    String(doc.evidence_commitment_sha256 ?? '').toUpperCase() === evidenceCommitment(doc) &&
    String(receipt.preInventoryDigest).toUpperCase() === String(doc.initial_inventory_digest).toUpperCase() &&
    String(receipt.postInventoryDigest).toUpperCase() === String(doc.post_inventory_digest).toUpperCase() &&
    String(receipt.predecessorReceiptSha256).toUpperCase() === String(doc.predecessor_receipt_sha256).toUpperCase() &&
    existsSync(doorReceiptPath) && !pathHasLink(doorReceiptPath, context.attemptRoot) &&
    sha256File(doorReceiptPath) === String(receipt.predecessorReceiptSha256).toUpperCase() &&
    !Object.hasOwn(doc, 'phase_receipt_path') && !Object.hasOwn(doc, 'phase_receipt_sha256') &&
    !Object.hasOwn(doc, 'phase_receipt_committed'),
  { path: receiptPath, receipt, buildSha256: build.sha256, buildCommitment: evidenceCommitment(doc),
    doorReceiptPath, doorReceiptSha256: existsSync(doorReceiptPath) ? sha256File(doorReceiptPath) : '' },
  { exactRuntimeReceipt: true, actualEvidenceSha256: build.sha256, semanticCommitment: true,
    currentAuthorizationSnapshot: true, liveDoorPredecessorSha256: true })

  const records = Array.isArray(doc.predecessor_receipts) ? doc.predecessor_receipts : []
  let predecessorSha = ''
  const recordsValid = records.length === LOCK_PREDECESSOR_RECEIPTS.length &&
    records.every((record, index) => {
      const phase = LOCK_PREDECESSOR_RECEIPTS[index]
      const contract = STAGE_RECEIPT_CONTRACTS[phase]
      const expectedReceiptPath = join(context.attemptRoot, ...contract.path.split('/'))
      const ok = exactKeys(record, ['phase','path','sha256','evidence_path','evidence_sha256','evidence_commitment_sha256','input_inventory_digest','output_inventory_digest','predecessor_receipt_sha256','authorization_id','authorization_sha256','tool_id','validated']) &&
        record.phase === phase && samePath(record.path, expectedReceiptPath) &&
        liveHashGate(record.path, record.sha256, context.attemptRoot) &&
        isUnder(record.evidence_path, context.attemptRoot) &&
        liveHashGate(record.evidence_path, record.evidence_sha256, context.attemptRoot) &&
        evidenceCommitment(JSON.parse(readFileSync(record.evidence_path, 'utf8'))) === String(record.evidence_commitment_sha256).toUpperCase() &&
        String(record.predecessor_receipt_sha256).toUpperCase() === predecessorSha &&
        record.tool_id === contract.producerToolId && record.validated === true
      predecessorSha = String(record.sha256 ?? '').toUpperCase()
      return ok
    })
  add(checks, 'lock_build_recorded_six_live_predecessor_receipts', doc.stage_receipt_contract_gate === true &&
    doc.predecessor_receipt_chain_gate === true && recordsValid &&
    predecessorSha === String(doc.predecessor_receipt_sha256 ?? '').toUpperCase(),
  records, { phases: LOCK_PREDECESSOR_RECEIPTS, exactLiveHashAndEvidenceCommitmentChain: true })
}

function validateBuild(build, toolchainBinding, checks) {
  const doc = build.document
  const attemptRoot = resolve(String(doc.attempt_root ?? ''))
  const cadDirectory = join(attemptRoot, 'native_cad', 'working_pack')
  const expectedOutput = join(attemptRoot, 'evidence', 'lock_topology_888x14.v2.json')
  add(checks, 'build_schema_status_and_output_binding', doc.schema === BUILD_SCHEMA && doc.success === true && doc.status === 'LOCK_TOPOLOGY_888X14_COMPLETE' && doc.single_evidence_commit === true && doc.evidence_commit_verified === true && samePath(build.absolute, expectedOutput) && samePath(doc.output_path, expectedOutput), { schema: doc.schema, success: doc.success, status: doc.status, single: doc.single_evidence_commit, verified: doc.evidence_commit_verified, input: build.absolute, output: doc.output_path }, { schema: BUILD_SCHEMA, success: true, status: 'LOCK_TOPOLOGY_888X14_COMPLETE', single: true, evidence_commit_verified: true, output: expectedOutput })
  add(checks, 'build_attempt_and_working_pack_binding', /[\\/]attempt-\d{4}$/i.test(attemptRoot) && samePath(doc.cad_directory, cadDirectory) && existsSync(cadDirectory) && isUnder(cadDirectory, attemptRoot) && !pathHasLink(cadDirectory, attemptRoot), { attemptRoot, cadDirectory: doc.cad_directory }, { cadDirectory, noLinks: true })
  const planLive = liveHashGate(doc.plan_path, doc.plan_sha256, attemptRoot) ? JSON.parse(readFileSync(doc.plan_path, 'utf8')) : {}
  const authorizationLive = liveHashGate(doc.authorization_path, doc.authorization_sha256, attemptRoot) ? JSON.parse(readFileSync(doc.authorization_path, 'utf8')) : {}
  const issued = Date.parse(authorizationLive.issuedAt)
  const expires = Date.parse(authorizationLive.expiresAt)
  const generated = Date.parse(doc.generated_at_utc)
  const leaseExpiry = Date.parse(authorizationLive.task?.leaseExpiresAt)
  const completed = Date.parse(doc.completed_at_utc)
  add(checks, 'build_plan_authorization_live_hash_binding', liveHashGate(doc.plan_path, doc.plan_sha256, attemptRoot) && liveHashGate(doc.authorization_path, doc.authorization_sha256, attemptRoot) && samePath(doc.authorization_path, join(attemptRoot, 'execution_authorizations', 'native_lock_topology_888x14_v1.json')) && doc.authorization_gate === true && planLive.schema === 'winnsen.native_build_plan.v1' && planLive.qualityBoundary?.planningOnly === true && planLive.executionBoundary?.executorImplemented === false && planLive.recipe?.id === RECIPE_ID && String(planLive.recipe?.digest ?? '').toUpperCase() === RECIPE_DIGEST && authorizationLive.schema === 'winnsen.native_execution_authorization.v1' && authorizationLive.purpose === 'structure_engineering_assistance' && exactKeys(authorizationLive, ['schema','authorizationId','issuedAt','expiresAt','purpose','workerId','task','request','plan','recipe','tool','seed','execution','qualityBoundary']) && exactKeys(authorizationLive.qualityBoundary, ['engineeringAssistanceReady','readyOnlyAfterEveryRequiredCheckPasses']) && authorizationLive.qualityBoundary.engineeringAssistanceReady === false && authorizationLive.qualityBoundary.readyOnlyAfterEveryRequiredCheckPasses === true && authorizationLive.execution?.authorized === true && JSON.stringify(authorizationLive.execution?.phases) === JSON.stringify(['lock_topology_888x14']) && authorizationLive.tool?.id === 'native_lock_topology_888x14_v1' && String(authorizationLive.tool?.sourceNormalizedSha256 ?? '').toUpperCase() === String(doc.tool_source_normalized_sha256 ?? '').toUpperCase() && String(authorizationLive.tool?.executableSha256 ?? '').toUpperCase() === String(doc.tool_executable_sha256 ?? '').toUpperCase() && Number.isFinite(issued) && Number.isFinite(expires) && Number.isFinite(leaseExpiry) && Number.isFinite(generated) && Number.isFinite(completed) && expires > issued && expires <= leaseExpiry && generated >= issued && completed <= expires && completed <= leaseExpiry, { plan: [doc.plan_path, doc.plan_sha256, planLive.qualityBoundary, planLive.executionBoundary, planLive.recipe], authorization: [doc.authorization_path, doc.authorization_sha256, doc.authorization_gate, authorizationLive.execution, authorizationLive.tool, authorizationLive.issuedAt, authorizationLive.expiresAt, authorizationLive.task?.leaseExpiresAt], completed: doc.completed_at_utc }, { liveHashes: true, exactSharedAuthorization: true, completionBeforeAuthorizationAndLeaseExpiry: true })
  add(checks, 'build_exact_shared_authorization_identity',
    exactKeys(authorizationLive.task, ['id','revision','digest','leaseId','leaseExpiresAt']) &&
    exactKeys(authorizationLive.request, ['fingerprint','digest']) && exactKeys(authorizationLive.plan, ['sha256']) &&
    exactKeys(authorizationLive.recipe, ['id','version','digest']) &&
    exactKeys(authorizationLive.tool, ['id','sourceNormalizedSha256','executableSha256']) &&
    exactKeys(authorizationLive.seed, ['inventoryDigest']) &&
    exactKeys(authorizationLive.execution, ['authorized','attempt','phases']) &&
    authorizationLive.workerId === planLive.workerId && authorizationLive.task.id === doc.task_id &&
    Number(authorizationLive.task.revision) === Number(doc.task_revision) &&
    String(authorizationLive.task.digest).toUpperCase() === String(doc.task_digest).toUpperCase() &&
    authorizationLive.request.fingerprint === planLive.request?.fingerprint &&
    String(authorizationLive.request.digest).toUpperCase() === String(doc.request_digest).toUpperCase() &&
    String(authorizationLive.plan.sha256).toUpperCase() === String(doc.plan_sha256).toUpperCase() &&
    authorizationLive.recipe.id === RECIPE_ID && Number(authorizationLive.recipe.version) === 1 &&
    String(authorizationLive.recipe.digest).toUpperCase() === RECIPE_DIGEST &&
    String(authorizationLive.seed.inventoryDigest).toUpperCase() === String(doc.initial_inventory_digest).toUpperCase() &&
    Number(authorizationLive.execution.attempt) === Number(doc.attempt_number) &&
    authorizationLive.authorizationId === doc.authorization_id &&
    authorizationLive.issuedAt === doc.authorization_issued_at_utc &&
    authorizationLive.expiresAt === doc.authorization_expires_at_utc &&
    authorizationLive.task.leaseId === doc.lease_id &&
    authorizationLive.task.leaseExpiresAt === doc.lease_expires_at_utc &&
    authorizationIdentityMatches(authorizationLive),
  { authorization: authorizationLive, build: { task: [doc.task_id, doc.task_revision, doc.task_digest],
    request: [doc.request_fingerprint, doc.request_digest], authorizationId: doc.authorization_id,
    attempt: doc.attempt_number, seed: doc.initial_inventory_digest } },
  { exactNestedKeys: true, stableAuthorizationId: true, exactTaskRequestPlanRecipeToolSeedAttempt: true })
  add(checks, 'build_plan_frozen_toolchain_manifest_binding', trustedToolchainManifestsMatch(planLive.trustedToolchainManifests, toolchainBinding.sha256), { plan: planLive.trustedToolchainManifests, validator: toolchainBinding.sha256 }, { exactPlanKeys: TRUSTED_TOOLCHAIN_MANIFEST_KEYS, lockManifestSha256: toolchainBinding.sha256 })
  add(checks, 'build_plan_exact_stage_receipt_contracts', stageReceiptContractsMatch(planLive.stageReceiptContracts), planLive.stageReceiptContracts, STAGE_RECEIPT_CONTRACTS)
  add(checks, 'build_recipe_and_tool_live_hash_binding', doc.trusted_recipe_id === RECIPE_ID && String(doc.authorization_seed_inventory_digest ?? '').toUpperCase() === String(doc.initial_inventory_digest ?? '').toUpperCase() && liveHashAnywhere(doc.tool_source_path, doc.tool_source_normalized_sha256) && liveHashAnywhere(doc.tool_executable_path, doc.tool_executable_sha256), { recipe: doc.trusted_recipe_id, seed: [doc.authorization_seed_inventory_digest, doc.initial_inventory_digest], tool: [doc.tool_source_path, doc.tool_source_normalized_sha256, doc.tool_executable_path, doc.tool_executable_sha256] }, { recipe: RECIPE_ID, seedBound: true, liveToolSourceAndExeHashes: true })
  const inventory = Array.isArray(doc.post_inventory) ? doc.post_inventory : []
  const unique = new Set(inventory.map((row) => String(row.relative_path ?? '').toLowerCase()))
  const parts = inventory.filter((row) => extname(String(row.relative_path ?? '')).toLowerCase() === '.sldprt').length
  const assemblies = inventory.filter((row) => extname(String(row.relative_path ?? '')).toLowerCase() === '.sldasm').length
  const liveInventory = inventory.every((row) => !/[\\/]/.test(String(row.relative_path ?? '')) && liveHashGate(join(cadDirectory, row.relative_path), row.sha256, attemptRoot) && statSync(join(cadDirectory, row.relative_path)).size === Number(row.size))
  const changed = new Set((doc.changed_files ?? []).map((value) => String(value).toLowerCase()))
  const expectedChanged = new Set(['箱体竖隔板l.sldprt', '箱体竖隔板r.sldprt'])
  add(checks, 'build_complete_live_75_inventory', inventory.length === 75 && unique.size === 75 && parts === 53 && assemblies === 22 && liveInventory && doc.post_inventory_gate === true && (doc.added_files ?? []).length === 0 && (doc.deleted_files ?? []).length === 0 && changed.size === 2 && [...expectedChanged].every((name) => changed.has(name)), { count: inventory.length, unique: unique.size, parts, assemblies, liveInventory, added: doc.added_files, deleted: doc.deleted_files, changed: doc.changed_files }, { count: 75, parts: 53, assemblies: 22, liveInventory: true, added: [], deleted: [], changed: [...expectedChanged] })
  const sessionPurposes = (doc.sessions ?? []).map((session) => session.purpose)
  const requiredPurposes = ['edit_left_lock_seed_and_pattern', 'mirror_native_right_partition', 'temporary_right_reopen', 'fresh_readonly_reopen_left', 'committed_right_reopen']
  add(checks, 'build_five_owned_sw2020_sessions', (doc.sessions ?? []).length === 5 && new Set((doc.sessions ?? []).map((session) => session.sldworks_process_id)).size === 5 && requiredPurposes.every((purpose) => sessionPurposes.includes(purpose)) && (doc.sessions ?? []).every((session) => session.created === true && session.solidworks_2020_exact_gate === true && String(session.solidworks_revision ?? '').startsWith('28.') && samePath(session.solidworks_executable_path, EXPECTED_SW_EXE_PATH) && String(session.solidworks_executable_sha256 ?? '').toUpperCase() === EXPECTED_SW_EXE_SHA256 && session.process_exited === true && session.monitors_exited === true && session.sldprocmon_identity_gate === true), doc.sessions, { count: 5, purposes: requiredPurposes, exactOwnedSW2020: true })
  add(checks, 'build_mirror_transaction_and_fresh_reopen', doc.mirror_api === 'IPartDoc.MirrorPart2' && doc.mirror_break_link === true && doc.temporary_right_external_reference_count === 0 && doc.right_replaced_transactionally === true && doc.temporary_right_reopen?.read_only_reopen === true && doc.right_reopen?.read_only_reopen === true && Number(doc.right_reopen?.mirror_feature_count) >= 1 && Number(doc.right_reopen?.forbidden_import_feature_count) === 0 && doc.final_process_cleanup_gate === true, { api: doc.mirror_api, breakLink: doc.mirror_break_link, refs: doc.temporary_right_external_reference_count, replaced: doc.right_replaced_transactionally, temp: doc.temporary_right_reopen, committed: doc.right_reopen, cleanup: doc.final_process_cleanup_gate }, { nativeMirror: true, externalReferences: 0, transactionReplace: true, freshReopens: true, cleanup: true })
  const context = { attemptRoot, cadDirectory, leftPath: resolve(String(doc.left_part_path ?? '')), rightPath: resolve(String(doc.right_part_path ?? '')), document: doc, planLive, authorizationLive }
  validateLockReceiptAndPredecessors(build, context, toolchainBinding, checks)
  return context
}

function validateInspector(inspector, build, context, toolchain, checks) {
  const doc = inspector.document
  const expectedPath = join(context.attemptRoot, 'evidence', 'lock_topology_888x14.inspector.v1.json')
  add(checks, 'inspector_schema_path_and_build_binding', exactKeys(doc, ['schema','attempt_root','cad_directory','build_evidence_sha256','generated_at_utc','inspector','parts','session','final_process_cleanup_gate','self_digest']) && selfDigest(doc) === String(doc.self_digest).toUpperCase() && doc.schema === INSPECTOR_SCHEMA && samePath(inspector.absolute, expectedPath) && String(doc.build_evidence_sha256 ?? '').toUpperCase() === build.sha256 && samePath(doc.attempt_root, context.attemptRoot) && samePath(doc.cad_directory, context.cadDirectory), { schema: doc.schema, path: inspector.absolute, build: doc.build_evidence_sha256, attempt: doc.attempt_root, cad: doc.cad_directory }, { exactSchemaKeysAndSelfDigest: true, schema: INSPECTOR_SCHEMA, path: expectedPath, build: build.sha256 })
  const tool = doc.inspector ?? {}
  add(checks, 'inspector_tool_live_source_exe_hash_binding', pinnedPartInspector(tool, toolchain), tool, { id: 'native_lock_topology_inspector_v1', pinnedSourceAndExe: true })
  add(checks, 'inspector_owned_sw2020_cleanup', doc.session?.purpose === 'inspect_lock_topology_readonly' && doc.session?.created === true && Number(doc.session?.sldworks_process_id) > 0 && ownedMonitorEvidence(doc.session) && doc.session?.solidworks_2020_exact_gate === true && String(doc.session?.solidworks_revision ?? '').startsWith('28.') && samePath(doc.session?.solidworks_executable_path, EXPECTED_SW_EXE_PATH) && String(doc.session?.solidworks_executable_sha256 ?? '').toUpperCase() === EXPECTED_SW_EXE_SHA256 && doc.session?.process_exited === true && doc.session?.monitors_exited === true && doc.session?.sldprocmon_identity_gate === true && doc.final_process_cleanup_gate === true, { session: doc.session, cleanup: doc.final_process_cleanup_gate }, { exactOwnedSW2020: true, purpose: 'inspect_lock_topology_readonly', ownedMonitors: true, cleanup: true })
  const left = analyzePart(doc.parts?.L, 'L', context.leftPath, context.attemptRoot, checks)
  const right = analyzePart(doc.parts?.R, 'R', context.rightPath, context.attemptRoot, checks)
  add(checks, 'inspector_matches_build_reopen_hashes', left.sourceSha256 === String(context.document.left_reopen?.source_sha256 ?? '').toUpperCase() && right.sourceSha256 === String(context.document.right_reopen?.source_sha256 ?? '').toUpperCase(), { inspector: [left.sourceSha256, right.sourceSha256], build: [context.document.left_reopen?.source_sha256, context.document.right_reopen?.source_sha256] }, { exact: true })
  return { left, right }
}

function validRotation(rotation) {
  if (!Array.isArray(rotation) || rotation.length !== 9 || !rotation.every((value) => Number.isFinite(Number(value)))) return false
  const r = rotation.map(Number)
  const dot = (a, b) => a.reduce((sum, value, index) => sum + value * b[index], 0)
  const rows = [r.slice(0, 3), r.slice(3, 6), r.slice(6, 9)]
  const determinant = r[0] * (r[4] * r[8] - r[5] * r[7]) - r[1] * (r[3] * r[8] - r[5] * r[6]) + r[2] * (r[3] * r[7] - r[4] * r[6])
  return rows.every((row) => close(dot(row, row), 1, 1e-6)) && close(dot(rows[0], rows[1]), 0, 1e-6) && close(dot(rows[0], rows[2]), 0, 1e-6) && close(dot(rows[1], rows[2]), 0, 1e-6) && close(determinant, 1, 1e-6)
}

function validateRootStageEvidence(manifest, context, tongueRoot, checks) {
  const expectedPath = join(context.attemptRoot, ...ROOT_EVIDENCE_RELATIVE_PATH.split('/'))
  const manifestEvidence = manifest?.evidence ?? {}
  let doc = {}
  const liveEvidence = samePath(manifestEvidence.path, expectedPath) &&
    liveHashGate(expectedPath, manifestEvidence.sha256, context.attemptRoot)
  try { if (liveEvidence) doc = JSON.parse(readFileSync(expectedPath, 'utf8')) } catch {}
  add(checks, 'root_stage_evidence_fixed_live_hash_and_status', liveEvidence &&
    exactKeys(doc, ['schema','generatedAtUtc','completedAtUtc','status','success','committed',
      'preflightPassed','stageReceiptContractsValidated','errorCode','error','exitCode','taskId',
      'attemptsRoot','attemptDir','workingPack','doorModuleOutput','contractPath','contractSnapshot',
      'plan','authorization','tool','execution','inventory','doorModule','frame','root','processes',
      'transaction','evidence','qualityBoundary','sessions','authorizationCheckpoints',
      'toolchainManifests','stageReceipts','initialHashes','rootManifestPath','rootManifestSha256',
      'predecessorReceiptSha256','evidence_commitment_sha256']) &&
    doc.schema === ROOT_EVIDENCE_SCHEMA && doc.success === true && doc.committed === true &&
    doc.status === 'NATIVE_ROOT_ASSEMBLY_888X14_PASS' && doc.preflightPassed === true &&
    doc.stageReceiptContractsValidated === true && doc.processes?.finalGate === true &&
    doc.qualityBoundary?.purpose === 'structure_engineering_assistance' &&
    doc.qualityBoundary?.engineeringAssistanceReady === false &&
    doc.qualityBoundary?.finalPackAndRelocatedReopenRequired === true &&
    doc.qualityBoundary?.structuralEngineerReviewRequired === true &&
    /^[A-F0-9]{64}$/i.test(String(doc.evidence_commitment_sha256 ?? '')) &&
    evidenceCommitment(doc) === String(doc.evidence_commitment_sha256).toUpperCase(),
  { path: manifestEvidence.path, sha256: manifestEvidence.sha256, keys: Object.keys(doc), schema: doc.schema, success: doc.success,
    committed: doc.committed, status: doc.status, commitment: doc.evidence_commitment_sha256 },
  { path: expectedPath, exactTopLevelKeys: true, liveSha256: true, schema: ROOT_EVIDENCE_SCHEMA, committedPass: true, semanticCommitment: true })

  const plan = doc.plan ?? {}
  const authorization = doc.authorization ?? {}
  const tool = doc.tool ?? {}
  const execution = doc.execution ?? {}
  const rawAuthorization = authorization.raw ?? {}
  const rawTask = rawAuthorization.task ?? {}
  const rawRequest = rawAuthorization.request ?? {}
  const rawPlan = rawAuthorization.plan ?? {}
  const rawRecipe = rawAuthorization.recipe ?? {}
  const rawTool = rawAuthorization.tool ?? {}
  const rawSeed = rawAuthorization.seed ?? {}
  const rawExecution = rawAuthorization.execution ?? {}
  const rawQuality = rawAuthorization.qualityBoundary ?? {}
  const authExpectedPath = join(context.attemptRoot, 'execution_authorizations', `${ROOT_TOOL_ID}.json`)
  let liveAuthorization = {}
  const authLive = samePath(authorization.path, authExpectedPath) &&
    liveHashGate(authExpectedPath, authorization.sha256, context.attemptRoot)
  try { if (authLive) liveAuthorization = JSON.parse(readFileSync(authExpectedPath, 'utf8')) } catch {}
  const issued = Date.parse(rawAuthorization.issuedAt)
  const expires = Date.parse(rawAuthorization.expiresAt)
  const leaseExpires = Date.parse(rawTask.leaseExpiresAt)
  const completed = Date.parse(doc.completedAtUtc)
  const exactAuthorization = exactKeys(rawAuthorization, ['schema','authorizationId','issuedAt','expiresAt','purpose','workerId','task','request','plan','recipe','tool','seed','execution','qualityBoundary']) &&
    exactKeys(rawTask, ['id','revision','digest','leaseId','leaseExpiresAt']) &&
    exactKeys(rawRequest, ['fingerprint','digest']) && exactKeys(rawPlan, ['sha256']) &&
    exactKeys(rawRecipe, ['id','version','digest']) &&
    exactKeys(rawTool, ['id','sourceNormalizedSha256','executableSha256']) &&
    exactKeys(rawSeed, ['inventoryDigest']) && exactKeys(rawExecution, ['authorized','attempt','phases']) &&
    exactKeys(rawQuality, ['engineeringAssistanceReady','readyOnlyAfterEveryRequiredCheckPasses'])
  add(checks, 'root_stage_exact_plan_authorization_tool_binding',
    exactKeys(plan, ['path','sha256','workerId','taskRevision','taskDigest','requestFingerprint','requestDigest','recipeDigest','raw']) &&
    exactKeys(authorization, ['path','sha256','authorizationId','issuedAt','expiresAt','leaseId','leaseExpiresAt','workerId','raw']) &&
    exactKeys(tool, ['id','sourceNormalizedSha256','executableSha256','contractSnapshotSha256']) &&
    exactKeys(execution, ['authorized','attempt','phases']) &&
    samePath(plan.path, context.document.plan_path) && liveHashGate(plan.path, plan.sha256, context.attemptRoot) &&
    String(plan.sha256).toUpperCase() === String(context.document.plan_sha256).toUpperCase() &&
    plan.workerId === context.planLive.workerId && Number(plan.taskRevision) === Number(context.document.task_revision) &&
    String(plan.taskDigest).toUpperCase() === String(context.document.task_digest).toUpperCase() &&
    plan.requestFingerprint === context.planLive.request?.fingerprint &&
    String(plan.requestDigest).toUpperCase() === String(context.planLive.request?.digest).toUpperCase() &&
    String(plan.recipeDigest).toUpperCase() === RECIPE_DIGEST && doc.taskId === context.document.task_id &&
    samePath(doc.attemptDir, context.attemptRoot) && samePath(doc.workingPack, context.cadDirectory) &&
    authLive && JSON.stringify(stableValue(liveAuthorization)) === JSON.stringify(stableValue(rawAuthorization)) &&
    exactAuthorization && rawAuthorization.schema === 'winnsen.native_execution_authorization.v1' &&
    rawAuthorization.purpose === 'structure_engineering_assistance' &&
    rawAuthorization.workerId === plan.workerId && rawTask.id === context.document.task_id &&
    Number(rawTask.revision) === Number(context.document.task_revision) &&
    String(rawTask.digest).toUpperCase() === String(context.document.task_digest).toUpperCase() &&
    rawRequest.fingerprint === context.planLive.request?.fingerprint &&
    String(rawRequest.digest).toUpperCase() === String(context.planLive.request?.digest).toUpperCase() &&
    String(rawPlan.sha256).toUpperCase() === String(context.document.plan_sha256).toUpperCase() &&
    rawRecipe.id === RECIPE_ID && Number(rawRecipe.version) === 1 &&
    String(rawRecipe.digest).toUpperCase() === RECIPE_DIGEST && rawTool.id === ROOT_TOOL_ID &&
    String(rawTool.sourceNormalizedSha256).toUpperCase() === String(tool.sourceNormalizedSha256).toUpperCase() &&
    String(rawTool.executableSha256).toUpperCase() === String(tool.executableSha256).toUpperCase() &&
    String(rawSeed.inventoryDigest).toUpperCase() === String(doc.inventory?.preDigest ?? '').toUpperCase() &&
    rawExecution.authorized === true && Number(rawExecution.attempt) === Number(context.document.attempt_number) &&
    JSON.stringify(rawExecution.phases) === JSON.stringify(['root_assembly_888x14']) &&
    rawQuality.engineeringAssistanceReady === false && rawQuality.readyOnlyAfterEveryRequiredCheckPasses === true &&
    authorization.authorizationId === rawAuthorization.authorizationId &&
    authorization.issuedAt === rawAuthorization.issuedAt && authorization.expiresAt === rawAuthorization.expiresAt &&
    authorization.leaseId === rawTask.leaseId && authorization.leaseExpiresAt === rawTask.leaseExpiresAt &&
    authorization.workerId === rawAuthorization.workerId && authorizationIdentityMatches(rawAuthorization) &&
    execution.authorized === true && Number(execution.attempt) === Number(context.document.attempt_number) &&
    JSON.stringify(execution.phases) === JSON.stringify(['root_assembly_888x14']) &&
    Number.isFinite(issued) && Number.isFinite(expires) && Number.isFinite(leaseExpires) &&
    Number.isFinite(completed) && issued <= completed && completed <= expires && expires <= leaseExpires &&
    rootToolchainBinding(context.planLive, tool, manifest.tool),
  { plan, authorization, tool, execution, liveAuthorization, completedAtUtc: doc.completedAtUtc,
    rootToolchainManifestPath: ROOT_TOOLCHAIN_MANIFEST_PATH },
  { exactNestedSchemas: true, livePlanAndAuthorization: true, historicalAuthorizationIdentity: true,
    frozenRootToolchainIdentity: true, completionWithinAuthorizationAndLease: true })

  add(checks, 'root_stage_evidence_root_binding', exactKeys(doc.root ?? {}, ['path','writableOpenErrors','writableOpenWarnings','saveErrors','saveWarnings','readonlyOpenErrors','readonlyOpenWarnings','sha256','freshReadonlyReopenPassed','deletedInstances','doorPlacements','shelfPlacements','afterEdit','afterReadonlyReopen']) &&
    samePath(doc.root?.path, tongueRoot?.path) && String(doc.root?.sha256 ?? '').toUpperCase() === String(tongueRoot?.sha256 ?? '').toUpperCase() &&
    doc.root?.freshReadonlyReopenPassed === true && Number(doc.root?.readonlyOpenErrors) === 0 &&
    Number(doc.root?.readonlyOpenWarnings) === 0,
  doc.root, { exactRootEvidence: true, tongueRootPathSha256: true, freshReadonlyReopen: true })
}

function validateTongues(tongues, build, inspector, context, toolchain, checks) {
  const doc = tongues.document
  const expectedPath = join(context.attemptRoot, 'evidence', 'assembly_lock_tongues_888x14.json')
  add(checks, 'tongue_schema_path_and_upstream_hash_binding', exactKeys(doc, ['schema','attempt_root','build_evidence_sha256','inspector_evidence_sha256','root_manifest_path','root_manifest_sha256','generated_at_utc','inspector','root_assembly','tongue_source','instances','session','final_process_cleanup_gate','self_digest']) && selfDigest(doc) === String(doc.self_digest).toUpperCase() && doc.schema === TONGUE_SCHEMA && samePath(doc.attempt_root, context.attemptRoot) && samePath(tongues.absolute, expectedPath) && String(doc.build_evidence_sha256 ?? '').toUpperCase() === build.sha256 && String(doc.inspector_evidence_sha256 ?? '').toUpperCase() === inspector.sha256, { schema: doc.schema, path: tongues.absolute, build: doc.build_evidence_sha256, inspector: doc.inspector_evidence_sha256 }, { exactSchemaKeysAndSelfDigest: true, schema: TONGUE_SCHEMA, path: expectedPath, build: build.sha256, inspector: inspector.sha256 })
  const tongueTool = doc.inspector ?? {}
  const tonguePinned = toolchain?.tools?.native_assembly_tongue_inspector_888x14_v1
  add(checks, 'tongue_inspector_pinned_identity', tonguePinned && tongueTool.id === 'native_assembly_tongue_inspector_888x14_v1' && samePath(tongueTool.source_path, tonguePinned.sourcePath) && samePath(tongueTool.executable_path, tonguePinned.executablePath) && String(tongueTool.source_sha256).toUpperCase() === String(tonguePinned.sourceNormalizedSha256).toUpperCase() && String(tongueTool.executable_sha256).toUpperCase() === String(tonguePinned.executableSha256).toUpperCase() && liveHashAnywhere(tongueTool.executable_path, tongueTool.executable_sha256), tongueTool, { pinnedInspector: true })
  const manifestPath = join(context.attemptRoot, 'evidence', 'root_assembly_888x14.v1.json')
  let manifest = {}
  try { manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) } catch {}
  add(checks, 'tongue_root_manifest_binding', samePath(doc.root_manifest_path, manifestPath) && liveHashGate(manifestPath, doc.root_manifest_sha256, context.attemptRoot) && exactKeys(manifest, ['schema','task','recipe','plan','tool','evidence','rootAssembly','generatedAtUtc']) && manifest.schema === 'winnsen.locker16029.native_888x14_root_assembly_manifest.v1' && exactKeys(manifest.task,['id','revision','digest']) && manifest.task.id === context.document.task_id && Number(manifest.task.revision) === Number(context.document.task_revision) && String(manifest.task.digest).toUpperCase() === String(context.document.task_digest).toUpperCase() && exactKeys(manifest.recipe,['id','version','digest']) && manifest.recipe.id === RECIPE_ID && Number(manifest.recipe.version) === 1 && String(manifest.recipe.digest).toUpperCase() === RECIPE_DIGEST && exactKeys(manifest.plan,['sha256']) && String(manifest.plan.sha256).toUpperCase() === String(context.document.plan_sha256).toUpperCase() && exactKeys(manifest.tool,['id','sourceNormalizedSha256','executableSha256']) && manifest.tool.id === ROOT_TOOL_ID && /^[A-F0-9]{64}$/i.test(manifest.tool.sourceNormalizedSha256) && /^[A-F0-9]{64}$/i.test(manifest.tool.executableSha256) && exactKeys(manifest.evidence,['path','sha256']) && samePath(manifest.evidence.path, join(context.attemptRoot, ...ROOT_EVIDENCE_RELATIVE_PATH.split('/'))) && liveHashGate(manifest.evidence.path, manifest.evidence.sha256, context.attemptRoot) && exactKeys(manifest.rootAssembly, ['path','sha256']) && samePath(manifest.rootAssembly.path, doc.root_assembly?.path) && String(manifest.rootAssembly.sha256).toUpperCase() === String(doc.root_assembly?.sha256).toUpperCase(), { path: doc.root_manifest_path, sha: doc.root_manifest_sha256, manifest }, { exactTrustedRootStageManifest: true, fixedRootEvidencePath: ROOT_EVIDENCE_RELATIVE_PATH })
  const root = doc.root_assembly ?? {}
  validateRootStageEvidence(manifest, context, root, checks)
  add(checks, 'tongue_root_assembly_live_sha_and_reopen', liveHashGate(root.path, root.sha256, context.attemptRoot) && isUnder(root.path, context.cadDirectory) && extname(String(root.path ?? '')).toLowerCase() === '.sldasm' && root.read_only_reopen === true && root.save_api_calls === 0 && root.source_unchanged === true && root.open_errors === 0 && root.open_warnings === 0 && root.rebuild_succeeded === true && doc.session?.purpose === 'inspect_root_assembly_tongues_readonly' && doc.session?.created === true && Number(doc.session?.sldworks_process_id)>0 && ownedMonitorEvidence(doc.session) && doc.session?.solidworks_2020_exact_gate === true && String(doc.session?.solidworks_revision ?? '').startsWith('28.') && samePath(doc.session?.solidworks_executable_path, EXPECTED_SW_EXE_PATH) && String(doc.session?.solidworks_executable_sha256 ?? '').toUpperCase() === EXPECTED_SW_EXE_SHA256 && doc.session?.process_exited === true && doc.session?.monitors_exited === true && doc.session?.sldprocmon_identity_gate === true && doc.final_process_cleanup_gate === true, { root, session: doc.session, cleanup: doc.final_process_cleanup_gate }, { liveRootSha: true, freshReadOnlyReopen: true, errors: 0, warnings: 0, exactOwnedSW2020Cleanup: true })
  const source = doc.tongue_source ?? {}
  add(checks, 'tongue_trusted_source_live_hash', win32.basename(String(source.path ?? '')).toLowerCase() === TRUSTED_TONGUE_NAME.toLowerCase() && String(source.sha256 ?? '').toUpperCase() === TRUSTED_TONGUE_SHA256 && liveHashGate(source.path, source.sha256, context.attemptRoot), source, { basename: TRUSTED_TONGUE_NAME, sha256: TRUSTED_TONGUE_SHA256, live: true })
  const instances = Array.isArray(doc.instances) ? doc.instances : []
  const exact = ['L', 'R'].every((side) => {
    const sideInstances = instances.filter((row) => row.side === side).sort((a, b) => Number(a.row_index) - Number(b.row_index))
    return sideInstances.length === 7 && sideInstances.every((row, index) => {
      const xyz = row.translation_mm
      return exactKeys(row, ['component_name','component_instance_path','source_path','source_sha256',
        'side','row_index','translation_mm','rotation','suppressed','hidden']) &&
        Number(row.row_index) === index + 1 && Array.isArray(xyz) && xyz.length === 3 &&
        close(xyz[0], side === 'L' ? -55 : 55, TONGUE_TOLERANCE_MM) &&
        close(xyz[1], SLOT_ROWS[index], TONGUE_TOLERANCE_MM) && close(xyz[2], -11.3, TONGUE_TOLERANCE_MM) &&
        typeof row.component_instance_path === 'string' && row.component_instance_path.length > 0 && validRotation(row.rotation) && row.suppressed === false && row.hidden === false &&
        String(row.source_sha256 ?? '').toUpperCase() === TRUSTED_TONGUE_SHA256 &&
        samePath(row.source_path, source.path) && !/electric/i.test(String(row.component_name ?? ''))
    })
  })
  add(checks, 'tongue_complete_14_rotation_xyz_readback', instances.length === 14 &&
    new Set(instances.map((row) => row.component_name)).size === 14 &&
    new Set(instances.map((row) => row.component_instance_path)).size === 14 && exact,
  instances, { total: 14, uniqueNamesAndRecursiveInstancePaths: 14, perSide: 7,
    exactInstanceSchemaAndCompleteOrthonormalRotationXYZ: true })
}

function validateBundle(bundle, toolchainBinding = { sha256: '', document: {} }) {
  const checks = []
  const context = validateBuild(bundle.build, toolchainBinding, checks)
  validateInspector(bundle.inspector, bundle.build, context, toolchainBinding.document, checks)
  validateTongues(bundle.tongues, bundle.build, bundle.inspector, context, toolchainBinding.document, checks)
  const failedChecks = checks.filter((check) => !check.ok)
  return {
    schema: SCHEMA,
    generatedAtUtc: new Date().toISOString(),
    purpose: 'structure_engineering_assistance',
    status: failedChecks.length === 0 ? 'PASS' : 'FAIL',
    structureEngineeringEvidencePass: failedChecks.length === 0,
    inputs: {
      build: { path: bundle.build.absolute, sha256: bundle.build.sha256 },
      inspector: { path: bundle.inspector.absolute, sha256: bundle.inspector.sha256 },
      tongues: { path: bundle.tongues.absolute, sha256: bundle.tongues.sha256 },
    },
    expectedRowsMm: { slots: SLOT_ROWS, circles: CIRCLE_ROWS },
    checks,
    failedCheckCount: failedChecks.length,
    failedChecks: failedChecks.map((check) => check.name),
    limitation: 'This validator requires live CAD hashes plus independent SolidWorks 2020 inspector and root-assembly tongue-transform evidence; templates alone cannot pass.',
  }
}

function writeJsonAtomicNew(path, value) {
  const absolute = resolve(path)
  if (existsSync(absolute)) throw new Error(`Output already exists: ${absolute}`)
  mkdirSync(dirname(absolute), { recursive: true })
  const temporary = `${absolute}.tmp-${process.pid}-${Date.now()}`
  let descriptor
  try {
    descriptor = openSync(temporary, 'wx')
    writeFileSync(descriptor, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
    fsyncSync(descriptor)
    closeSync(descriptor)
    descriptor = undefined
    renameSync(temporary, absolute)
  } finally {
    if (descriptor !== undefined) closeSync(descriptor)
    if (existsSync(temporary)) unlinkSync(temporary)
  }
}

function fixtureSession(purpose, processId) {
  return {
    purpose, created: true, sldworks_process_id: processId, sldworks_start_ticks_utc: processId * 1000,
    solidworks_revision: '28.5.1', solidworks_executable_path: EXPECTED_SW_EXE_PATH,
    solidworks_executable_sha256: EXPECTED_SW_EXE_SHA256, solidworks_2020_exact_gate: true,
    monitor_processes: [], process_exited: true, monitors_exited: true, sldprocmon_identity_gate: true,
  }
}

function fixtureEdges(side) {
  const sign = side === 'L' ? -1 : 1
  const edges = []
  for (const row of SLOT_ROWS) {
    for (const template of SLOT_TEMPLATES) {
      const map = (point) => [sign * Math.abs(point[0]), point[1] + row, point[2]]
      edges.push({ index: edges.length + 1, length_mm: template.length,
        param_start_mm: [], vertex_start_mm: map(template.a), vertex_end_mm: map(template.b) })
    }
  }
  for (const row of CIRCLE_ROWS) {
    for (const template of [{ x: 42.5, z: -19.3 }, { x: 47.5, z: -18.5 }]) {
      const point = [sign * template.x, row, template.z]
      edges.push({ index: edges.length + 1, length_mm: CIRCLE_LENGTH_MM,
        param_start_mm: point, vertex_start_mm: point, vertex_end_mm: point })
    }
  }
  return edges
}

function writeFixtureEvidence(path, document) {
  writeFileSync(path, `${JSON.stringify(document, null, 2)}\n`, 'utf8')
  return readEvidence(path)
}

function createHandWrittenBundle(base, label, toolchainBinding, planManifestMode, stageContractMode = 'matching') {
  const attemptRoot = join(base, `fixture-${label}`, 'attempt-0001')
  const cadDirectory = join(attemptRoot, 'native_cad', 'working_pack')
  const evidenceDirectory = join(attemptRoot, 'evidence')
  const authorizationDirectory = join(attemptRoot, 'execution_authorizations')
  mkdirSync(cadDirectory, { recursive: true })
  mkdirSync(evidenceDirectory, { recursive: true })
  mkdirSync(authorizationDirectory, { recursive: true })

  const leftName = '绠变綋绔栭殧鏉縧.sldprt'
  const rightName = '绠变綋绔栭殧鏉縭.sldprt'
  const partNames = [leftName, rightName, TRUSTED_TONGUE_NAME,
    ...Array.from({ length: 50 }, (_, index) => `fixture-part-${String(index + 1).padStart(2, '0')}.SLDPRT`)]
  const rootName = 'fixture-root.SLDASM'
  const assemblyNames = [rootName,
    ...Array.from({ length: 21 }, (_, index) => `fixture-assembly-${String(index + 1).padStart(2, '0')}.SLDASM`)]
  for (const name of [...partNames, ...assemblyNames]) writeFileSync(join(cadDirectory, name), `hand-written-${name}\n`, 'utf8')
  const inventory = [...partNames, ...assemblyNames].map((name) => ({
    relative_path: name, size: statSync(join(cadDirectory, name)).size, sha256: sha256File(join(cadDirectory, name)),
  }))
  const taskDigest = 'A'.repeat(64)
  const requestDigest = 'B'.repeat(64)
  const requestFingerprint = 'C'.repeat(64)
  const seedDigest = 'D'.repeat(64)
  const plan = {
    schema: 'winnsen.native_build_plan.v1', purpose: 'structure_engineering_assistance', workerId: 'fixture-worker',
    task: { id: `fixture-${label}`, revisionAtPlanning: 7, digest: taskDigest },
    request: { fingerprint: requestFingerprint, digest: requestDigest },
    recipe: { id: RECIPE_ID, version: 1, digest: RECIPE_DIGEST },
    requiredChecks: ['one_door_one_lock', 'rebuild_save_reopen', 'relocated_reopen'],
    qualityBoundary: { planningOnly: true }, executionBoundary: { executorImplemented: false },
    stageReceiptContracts: JSON.parse(JSON.stringify(STAGE_RECEIPT_CONTRACTS)),
  }
  if (stageContractMode === 'missing') delete plan.stageReceiptContracts
  if (stageContractMode === 'wrong') plan.stageReceiptContracts.door_module_888x14.path = 'evidence/forged-door-receipt.json'
  if (planManifestMode !== 'missing') {
    plan.trustedToolchainManifests = Object.fromEntries(TRUSTED_TOOLCHAIN_MANIFEST_KEYS.map((id, index) =>
      [id, id === TOOLCHAIN_PLAN_KEY
        ? (planManifestMode === 'wrong' ? 'F'.repeat(64) : toolchainBinding.sha256)
        : String(index + 1).repeat(64)]))
  }
  const planPath = join(attemptRoot, 'native_build_plan.json')
  writeFileSync(planPath, `${JSON.stringify(plan, null, 2)}\n`, 'utf8')
  const planSha = sha256File(planPath)
  const buildTool = toolchainBinding.document.tools.native_lock_topology_888x14_v1
  const issuedAt = '2098-01-01T00:00:00.000Z'
  const expiresAt = '2098-01-01T00:10:00.000Z'
  const leaseExpiresAt = '2098-01-01T00:20:00.000Z'
  const authorization = {
    schema: 'winnsen.native_execution_authorization.v1', authorizationId: '',
    issuedAt, expiresAt, purpose: 'structure_engineering_assistance', workerId: 'fixture-worker',
    task: { id: `fixture-${label}`, revision: 7, digest: taskDigest, leaseId: 'fixture-lease', leaseExpiresAt },
    request: { fingerprint: requestFingerprint, digest: requestDigest }, plan: { sha256: planSha },
    recipe: { id: RECIPE_ID, version: 1, digest: RECIPE_DIGEST },
    tool: { id: TOOLCHAIN_PLAN_KEY, sourceNormalizedSha256: buildTool.sourceNormalizedSha256, executableSha256: buildTool.executableSha256 },
    seed: { inventoryDigest: seedDigest }, execution: { authorized: true, attempt: 1, phases: ['lock_topology_888x14'] },
    qualityBoundary: { engineeringAssistanceReady: false, readyOnlyAfterEveryRequiredCheckPasses: true },
  }
  const authorizationBinding = Object.fromEntries(['workerId','task','request','plan','recipe','tool','seed','execution','issuedAt','expiresAt'].map((key) => [key, authorization[key]]))
  authorization.authorizationId = `native-auth-${sha256Bytes(Buffer.from(JSON.stringify(stableValue(authorizationBinding)))).slice(0, 32).toLowerCase()}`
  const authorizationPath = join(authorizationDirectory, `${TOOLCHAIN_PLAN_KEY}.json`)
  writeFileSync(authorizationPath, `${JSON.stringify(authorization, null, 2)}\n`, 'utf8')
  const authorizationBytesBase64 = readFileSync(authorizationPath).toString('base64')
  const receiptsDirectory = join(attemptRoot, 'receipts')
  mkdirSync(receiptsDirectory, { recursive: true })
  const predecessorRecords = []
  let predecessorReceiptSha256 = ''
  let predecessorInventoryDigest = seedDigest
  for (const phase of LOCK_PREDECESSOR_RECEIPTS) {
    const contract = STAGE_RECEIPT_CONTRACTS[phase]
    const evidenceRelativePath = phase === 'clone_native_seed' ? 'evidence/seed_pack_888_native_v1.json'
      : ['dimensions','derived','base-hole','assemblies'].includes(phase) ? `evidence/width-${phase}.json`
      : 'evidence/door_module_888x14.result.v1.json'
    const stageEvidencePath = join(attemptRoot, ...evidenceRelativePath.split('/'))
    const stageEvidenceDocument = { schema: `hand-written.${phase}.v1`, success: true, phase,
      taskId: `fixture-${label}`, inventoryDigest: predecessorInventoryDigest }
    writeFileSync(stageEvidencePath, `${JSON.stringify(stageEvidenceDocument, null, 2)}\n`, 'utf8')
    const stageEvidenceSha256 = sha256File(stageEvidencePath)
    const stageCommitment = evidenceCommitment(stageEvidenceDocument)
    const receiptBase = {
      schema: contract.schema, phase, success: true, completedAt: '2098-01-01T00:00:30.000Z',
      taskId: `fixture-${label}`, taskRevision: 7, taskDigest, requestDigest,
      leaseId: 'fixture-lease', leaseExpiresAt, attempt: 1, planSha256: planSha,
      authorizationId: authorization.authorizationId, authorizationSha256: sha256File(authorizationPath),
      authorizationJsonBase64: authorizationBytesBase64, authorizationIssuedAt: issuedAt,
      authorizationExpiresAt: expiresAt, recipeId: RECIPE_ID, recipeDigest: RECIPE_DIGEST,
      toolId: contract.producerToolId, toolSourceNormalizedSha256: '6'.repeat(64),
      toolExecutableSha256: '7'.repeat(64), evidencePath: evidenceRelativePath,
      evidenceSha256: stageEvidenceSha256, evidenceCommitmentSha256: stageCommitment,
      predecessorReceiptSha256,
    }
    const receiptDocument = phase === 'clone_native_seed' ? {
      ...receiptBase, sourceInventoryDigest: predecessorInventoryDigest,
      targetInventoryDigest: predecessorInventoryDigest, nonRootFileCount: 74,
      nonRootExactSource: true, rootFileName: rootName, rootShaBefore: '8'.repeat(64),
      rootShaAfterStable: '9'.repeat(64), initialOpenErrors: 0, initialOpenWarnings: 0,
      stabilizeSaveErrors: 0, stabilizeSaveWarnings: 0, reopenErrors: 0, reopenWarnings: 0,
      dependencyClosureCount: 75, dependenciesAllTargetLocal: true, knownRootIssueGate: true,
    } : { ...receiptBase, preInventoryDigest: predecessorInventoryDigest,
      postInventoryDigest: predecessorInventoryDigest }
    if (!exactKeys(receiptDocument, phase === 'clone_native_seed' ? SEED_RECEIPT_KEYS : RUNTIME_RECEIPT_KEYS)) throw new Error('fixture receipt key drift')
    const stageReceiptPath = join(attemptRoot, ...contract.path.split('/'))
    mkdirSync(dirname(stageReceiptPath), { recursive: true })
    writeFileSync(stageReceiptPath, `${JSON.stringify(receiptDocument, null, 2)}\n`, 'utf8')
    const stageReceiptSha256 = sha256File(stageReceiptPath)
    predecessorRecords.push({ phase, path: stageReceiptPath, sha256: stageReceiptSha256,
      evidence_path: stageEvidencePath, evidence_sha256: stageEvidenceSha256,
      evidence_commitment_sha256: stageCommitment, input_inventory_digest: predecessorInventoryDigest,
      output_inventory_digest: predecessorInventoryDigest,
      predecessor_receipt_sha256: predecessorReceiptSha256,
      authorization_id: authorization.authorizationId, authorization_sha256: sha256File(authorizationPath),
      tool_id: contract.producerToolId, validated: true })
    predecessorReceiptSha256 = stageReceiptSha256
  }
  const rootPath = join(cadDirectory, rootName)
  const leftPath = join(cadDirectory, leftName)
  const rightPath = join(cadDirectory, rightName)
  const buildDocument = {
    schema: BUILD_SCHEMA, success: true, status: 'LOCK_TOPOLOGY_888X14_COMPLETE', single_evidence_commit: true,
    evidence_commit_verified: true, output_path: join(evidenceDirectory, 'lock_topology_888x14.v2.json'),
    attempt_root: attemptRoot, cad_directory: cadDirectory, plan_path: planPath, plan_sha256: planSha,
    authorization_path: authorizationPath, authorization_sha256: sha256File(authorizationPath), authorization_gate: true,
    generated_at_utc: '2098-01-01T00:01:00.000Z', completed_at_utc: '2098-01-01T00:02:00.000Z',
    trusted_recipe_id: RECIPE_ID, authorization_seed_inventory_digest: seedDigest, initial_inventory_digest: seedDigest,
    post_inventory_digest: seedDigest, worker_id: 'fixture-worker', request_fingerprint: requestFingerprint,
    request_digest: requestDigest, lease_id: 'fixture-lease', lease_expires_at_utc: leaseExpiresAt,
    authorization_id: authorization.authorizationId, authorization_json_base64: authorizationBytesBase64,
    authorization_issued_at_utc: issuedAt, authorization_expires_at_utc: expiresAt, attempt_number: 1,
    tool_source_path: buildTool.sourcePath, tool_source_normalized_sha256: buildTool.sourceNormalizedSha256,
    tool_executable_path: buildTool.executablePath, tool_executable_sha256: buildTool.executableSha256,
    task_id: `fixture-${label}`, task_revision: 7, task_digest: taskDigest,
    left_part_path: leftPath, right_part_path: rightPath, post_inventory: inventory, post_inventory_gate: true,
    added_files: [], deleted_files: [], changed_files: [leftName, rightName],
    sessions: ['edit_left_lock_seed_and_pattern', 'mirror_native_right_partition', 'temporary_right_reopen',
      'fresh_readonly_reopen_left', 'committed_right_reopen'].map((purpose, index) => fixtureSession(purpose, index + 101)),
    mirror_api: 'IPartDoc.MirrorPart2', mirror_break_link: true, temporary_right_external_reference_count: 0,
    right_replaced_transactionally: true, temporary_right_reopen: { read_only_reopen: true },
    left_reopen: { source_sha256: sha256File(leftPath) },
    right_reopen: { read_only_reopen: true, mirror_feature_count: 1, forbidden_import_feature_count: 0, source_sha256: sha256File(rightPath) },
    final_process_cleanup_gate: true,
    toolchain_manifest_path: toolchainBinding.absolute, toolchain_manifest_sha256: toolchainBinding.sha256,
    trusted_toolchain_manifest_gate: true, stage_receipt_contract_gate: true,
    predecessor_receipt_chain_gate: true, predecessor_receipt_sha256: predecessorReceiptSha256,
    predecessor_receipts: predecessorRecords, evidence_commitment_sha256: '',
  }
  buildDocument.evidence_commitment_sha256 = evidenceCommitment(buildDocument)
  const build = writeFixtureEvidence(buildDocument.output_path, buildDocument)
  const lockReceipt = {
    schema: 'winnsen.16029.native_lock_topology_receipt.v1', phase: 'lock_topology_888x14',
    success: true, completedAt: buildDocument.completed_at_utc, taskId: buildDocument.task_id,
    taskRevision: buildDocument.task_revision, taskDigest, requestDigest,
    leaseId: 'fixture-lease', leaseExpiresAt, attempt: 1, planSha256: planSha,
    authorizationId: authorization.authorizationId, authorizationSha256: sha256File(authorizationPath),
    authorizationJsonBase64: authorizationBytesBase64, authorizationIssuedAt: issuedAt,
    authorizationExpiresAt: expiresAt, recipeId: RECIPE_ID, recipeDigest: RECIPE_DIGEST,
    toolId: TOOLCHAIN_PLAN_KEY, toolSourceNormalizedSha256: buildTool.sourceNormalizedSha256,
    toolExecutableSha256: buildTool.executableSha256,
    evidencePath: 'evidence/lock_topology_888x14.v2.json', evidenceSha256: build.sha256,
    evidenceCommitmentSha256: buildDocument.evidence_commitment_sha256,
    preInventoryDigest: seedDigest, postInventoryDigest: seedDigest, predecessorReceiptSha256,
  }
  if (!exactKeys(lockReceipt, RUNTIME_RECEIPT_KEYS)) throw new Error('fixture lock receipt key drift')
  writeFileSync(join(receiptsDirectory, 'lock_topology_888x14.json'), `${JSON.stringify(lockReceipt, null, 2)}\n`, 'utf8')
  const partTool = toolchainBinding.document.tools.native_lock_topology_inspector_v1
  const part = (side, path) => ({
    side, source_path: path, source_sha256: sha256File(path), opened: true, read_only: true, save_api_calls: 0,
    source_unchanged: true, open_errors: 0, open_warnings: 0, rebuild_succeeded: true, body_count: 1,
    feature_count: 2, feature_types: side === 'R' ? ['SheetMetal', 'FlatPattern', 'MirrorPart'] : ['SheetMetal', 'FlatPattern'],
    has_sheet_metal: true, has_flat_pattern: true, traversal_complete: true, error_feature_count: 0,
    warning_feature_count: 0, mirror_feature_count: side === 'R' ? 1 : 0, forbidden_import_feature_count: 0,
    external_reference_count: 0, edges: fixtureEdges(side),
  })
  const inspectorDocument = {
    schema: INSPECTOR_SCHEMA, attempt_root: attemptRoot, cad_directory: cadDirectory,
    build_evidence_sha256: build.sha256, generated_at_utc: '2098-01-01T00:03:00.000Z',
    inspector: { id: 'native_lock_topology_inspector_v1', source_path: partTool.sourcePath,
      source_sha256: partTool.sourceNormalizedSha256, executable_path: partTool.executablePath,
      executable_sha256: partTool.executableSha256 },
    parts: { L: part('L', leftPath), R: part('R', rightPath) },
    session: fixtureSession('inspect_lock_topology_readonly', 201), final_process_cleanup_gate: true,
  }
  inspectorDocument.self_digest = selfDigest(inspectorDocument)
  const inspector = writeFixtureEvidence(join(evidenceDirectory, 'lock_topology_888x14.inspector.v1.json'), inspectorDocument)
  const rootStageEvidencePath = join(evidenceDirectory, 'root_assembly_888x14.result.v1.json')
  const rootAuthorization = {
    schema: 'winnsen.native_execution_authorization.v1', authorizationId: '', issuedAt,
    expiresAt, purpose: 'structure_engineering_assistance', workerId: 'fixture-worker',
    task: { id: `fixture-${label}`, revision: 7, digest: taskDigest, leaseId: 'fixture-root-lease', leaseExpiresAt },
    request: { fingerprint: requestFingerprint, digest: requestDigest }, plan: { sha256: planSha },
    recipe: { id: RECIPE_ID, version: 1, digest: RECIPE_DIGEST },
    tool: { id: ROOT_TOOL_ID, sourceNormalizedSha256: '2'.repeat(64), executableSha256: '3'.repeat(64) },
    seed: { inventoryDigest: seedDigest },
    execution: { authorized: true, attempt: 1, phases: ['root_assembly_888x14'] },
    qualityBoundary: { engineeringAssistanceReady: false, readyOnlyAfterEveryRequiredCheckPasses: true },
  }
  const rootBinding = Object.fromEntries(['workerId','task','request','plan','recipe','tool','seed','execution','issuedAt','expiresAt'].map((key) => [key, rootAuthorization[key]]))
  rootAuthorization.authorizationId = `native-auth-${sha256Bytes(Buffer.from(JSON.stringify(stableValue(rootBinding)))).slice(0, 32).toLowerCase()}`
  const rootAuthorizationPath = join(authorizationDirectory, `${ROOT_TOOL_ID}.json`)
  writeFileSync(rootAuthorizationPath, `${JSON.stringify(rootAuthorization, null, 2)}\n`, 'utf8')
  const rootEvidenceDocument = {
    schema: ROOT_EVIDENCE_SCHEMA, generatedAtUtc: '2098-01-01T00:03:30.000Z',
    completedAtUtc: '2098-01-01T00:04:00.000Z', status: 'NATIVE_ROOT_ASSEMBLY_888X14_PASS',
    success: true, committed: true, preflightPassed: true, stageReceiptContractsValidated: true,
    errorCode: '', error: '', exitCode: 0, taskId: `fixture-${label}`,
    attemptsRoot: base, attemptDir: attemptRoot, workingPack: cadDirectory,
    doorModuleOutput: join(attemptRoot, 'door_module'), contractPath: join(attemptRoot, 'contract.json'),
    contractSnapshot: {},
    plan: { path: planPath, sha256: planSha, workerId: 'fixture-worker', taskRevision: 7,
      taskDigest, requestFingerprint, requestDigest, recipeDigest: RECIPE_DIGEST, raw: plan },
    authorization: { path: rootAuthorizationPath, sha256: sha256File(rootAuthorizationPath),
      authorizationId: rootAuthorization.authorizationId, issuedAt, expiresAt,
      leaseId: 'fixture-root-lease', leaseExpiresAt, workerId: 'fixture-worker', raw: rootAuthorization },
    tool: { id: ROOT_TOOL_ID, sourceNormalizedSha256: '2'.repeat(64),
      executableSha256: '3'.repeat(64), contractSnapshotSha256: '4'.repeat(64) },
    execution: { authorized: true, attempt: 1, phases: ['root_assembly_888x14'] },
    inventory: { preDigest: seedDigest }, doorModule: {}, frame: {},
    root: { path: rootPath, writableOpenErrors: 0, writableOpenWarnings: 0, saveErrors: 0,
      saveWarnings: 0, readonlyOpenErrors: 0, readonlyOpenWarnings: 0, sha256: sha256File(rootPath),
      freshReadonlyReopenPassed: true, deletedInstances: [], doorPlacements: [], shelfPlacements: [],
      afterEdit: {}, afterReadonlyReopen: {} },
    processes: { finalGate: true }, transaction: {},
    evidence: { schema: ROOT_EVIDENCE_SCHEMA, path: rootStageEvidencePath, sha256: '', written: true, writeError: '' },
    qualityBoundary: { purpose: 'structure_engineering_assistance', engineeringAssistanceReady: false,
      finalPackAndRelocatedReopenRequired: true, structuralEngineerReviewRequired: true },
    sessions: [], authorizationCheckpoints: [], toolchainManifests: [], stageReceipts: [],
    initialHashes: {}, rootManifestPath: join(evidenceDirectory, 'root_assembly_888x14.v1.json'),
    rootManifestSha256: '', predecessorReceiptSha256: '5'.repeat(64), evidence_commitment_sha256: '',
  }
  rootEvidenceDocument.evidence_commitment_sha256 = evidenceCommitment(rootEvidenceDocument)
  writeFileSync(rootStageEvidencePath, `${JSON.stringify(rootEvidenceDocument, null, 2)}\n`, 'utf8')
  const rootManifest = {
    schema: 'winnsen.locker16029.native_888x14_root_assembly_manifest.v1',
    task: { id: `fixture-${label}`, revision: 7, digest: taskDigest },
    recipe: { id: RECIPE_ID, version: 1, digest: RECIPE_DIGEST }, plan: { sha256: planSha },
    tool: { id: 'native_root_assembly_888x14_v1', sourceNormalizedSha256: '2'.repeat(64), executableSha256: '3'.repeat(64) },
    evidence: { path: rootStageEvidencePath, sha256: sha256File(rootStageEvidencePath) },
    rootAssembly: { path: rootPath, sha256: sha256File(rootPath) }, generatedAtUtc: '2098-01-01T00:04:00.000Z',
  }
  const rootManifestPath = join(evidenceDirectory, 'root_assembly_888x14.v1.json')
  writeFileSync(rootManifestPath, `${JSON.stringify(rootManifest, null, 2)}\n`, 'utf8')
  const tongueTool = toolchainBinding.document.tools.native_assembly_tongue_inspector_888x14_v1
  const tonguePath = join(cadDirectory, TRUSTED_TONGUE_NAME)
  const instances = ['L', 'R'].flatMap((side) => SLOT_ROWS.map((row, index) => ({
    component_name: `${side}-tongue-${index + 1}`, component_instance_path: `/root/${side}/tongue-${index + 1}`,
    source_path: tonguePath, source_sha256: TRUSTED_TONGUE_SHA256, side, row_index: index + 1,
    translation_mm: [side === 'L' ? -55 : 55, row, -11.3], rotation: [1,0,0,0,1,0,0,0,1],
    suppressed: false, hidden: false,
  })))
  const tonguesDocument = {
    schema: TONGUE_SCHEMA, attempt_root: attemptRoot, build_evidence_sha256: build.sha256,
    inspector_evidence_sha256: inspector.sha256, root_manifest_path: rootManifestPath,
    root_manifest_sha256: sha256File(rootManifestPath), generated_at_utc: '2098-01-01T00:05:00.000Z',
    inspector: { id: 'native_assembly_tongue_inspector_888x14_v1', source_path: tongueTool.sourcePath,
      source_sha256: tongueTool.sourceNormalizedSha256, executable_path: tongueTool.executablePath,
      executable_sha256: tongueTool.executableSha256 },
    root_assembly: { path: rootPath, sha256: sha256File(rootPath), read_only_reopen: true, save_api_calls: 0,
      source_unchanged: true, open_errors: 0, open_warnings: 0, rebuild_succeeded: true },
    tongue_source: { path: tonguePath, sha256: sha256File(tonguePath) }, instances,
    session: fixtureSession('inspect_root_assembly_tongues_readonly', 301), final_process_cleanup_gate: true,
  }
  tonguesDocument.self_digest = selfDigest(tonguesDocument)
  const tongues = writeFixtureEvidence(join(evidenceDirectory, 'assembly_lock_tongues_888x14.json'), tonguesDocument)
  return { build, inspector, tongues }
}

function selfTest(toolchainBinding) {
  let missingInspectorRejected = false
  try { parseArguments(['--build', 'a', '--tongues', 'b', '--output', 'c']) } catch { missingInspectorRejected = true }
  const rotations = validRotation([1, 0, 0, 0, 1, 0, 0, 0, 1]) && !validRotation([1, 0, 0, 0, 1, 0, 0, 0, 2])
  const rows = rowsEqual(SLOT_ROWS, [159.2142857142857, 420.6428571428571, 682.0714285714284, 943.5, 1204.9285714285713, 1466.3571428571427, 1727.7857142857142], 1e-9)
  const fake = { absolute: 'C:\\nonexistent\\evidence.json', sha256: 'A'.repeat(64), document: {} }
  const syntheticEvidenceCanPass = validateBundle({ build: fake, inspector: fake, tongues: fake }, toolchainBinding).status === 'PASS'
  const wrongInspectorIdentityCanPass = pinnedPartInspector({ id: 'forged', source_path: PART_INSPECTOR_SOURCE_PATH, source_sha256: EXPECTED_PART_INSPECTOR_SOURCE_SHA256, executable_path: PART_INSPECTOR_EXE_PATH, executable_sha256: existsSync(PART_INSPECTOR_EXE_PATH) ? sha256File(PART_INSPECTOR_EXE_PATH) : '' }, toolchainBinding.document)
  const fixtureBase = mkdtempSync(join(tmpdir(), 'winnsen-lock-validator-selftest-'))
  let completeHandWrittenEvidenceCanPass = true
  let matchingPlanManifestBindingPasses = false
  let missingPlanManifestCanPass = true
  let wrongPlanManifestCanPass = true
  let matchingStageReceiptContractPasses = false
  let missingStageReceiptContractCanPass = true
  let wrongStageReceiptContractCanPass = true
  let completeHandWrittenRootStageCanPass = true
  try {
    const matching = validateBundle(createHandWrittenBundle(fixtureBase, 'matching', toolchainBinding, 'matching'), toolchainBinding)
    const missing = validateBundle(createHandWrittenBundle(fixtureBase, 'missing', toolchainBinding, 'missing'), toolchainBinding)
    const wrong = validateBundle(createHandWrittenBundle(fixtureBase, 'wrong', toolchainBinding, 'wrong'), toolchainBinding)
    const missingStage = validateBundle(createHandWrittenBundle(fixtureBase, 'missing-stage', toolchainBinding, 'matching', 'missing'), toolchainBinding)
    const wrongStage = validateBundle(createHandWrittenBundle(fixtureBase, 'wrong-stage', toolchainBinding, 'matching', 'wrong'), toolchainBinding)
    completeHandWrittenEvidenceCanPass = matching.status === 'PASS'
    completeHandWrittenRootStageCanPass = matching.checks
      .filter((check) => check.name.startsWith('root_stage_') || check.name === 'tongue_root_manifest_binding')
      .every((check) => check.ok)
    matchingPlanManifestBindingPasses = matching.checks.find((check) => check.name === 'build_plan_frozen_toolchain_manifest_binding')?.ok === true
    missingPlanManifestCanPass = !missing.failedChecks.includes('build_plan_frozen_toolchain_manifest_binding')
    wrongPlanManifestCanPass = !wrong.failedChecks.includes('build_plan_frozen_toolchain_manifest_binding')
    matchingStageReceiptContractPasses = matching.checks.find((check) => check.name === 'build_plan_exact_stage_receipt_contracts')?.ok === true
    missingStageReceiptContractCanPass = !missingStage.failedChecks.includes('build_plan_exact_stage_receipt_contracts')
    wrongStageReceiptContractCanPass = !wrongStage.failedChecks.includes('build_plan_exact_stage_receipt_contracts')
  } finally {
    rmSync(fixtureBase, { recursive: true, force: true })
  }
  const passed = missingInspectorRejected && rotations && rows && !syntheticEvidenceCanPass &&
    !completeHandWrittenEvidenceCanPass && !wrongInspectorIdentityCanPass && matchingPlanManifestBindingPasses &&
    !missingPlanManifestCanPass && !wrongPlanManifestCanPass && matchingStageReceiptContractPasses &&
    !missingStageReceiptContractCanPass && !wrongStageReceiptContractCanPass &&
    !completeHandWrittenRootStageCanPass
  console.log(JSON.stringify({ selfTest: passed ? 'PASS' : 'FAIL', solidWorksStarted: false,
    syntheticEvidenceCanPass, completeHandWrittenEvidenceCanPass, wrongInspectorIdentityCanPass,
    matchingPlanManifestBindingPasses, missingPlanManifestCanPass, wrongPlanManifestCanPass,
    matchingStageReceiptContractPasses, missingStageReceiptContractCanPass, wrongStageReceiptContractCanPass,
    completeHandWrittenRootStageCanPass,
    missingInspectorRejected, rotationGate: rotations, rowFormulaGate: rows }, null, 2))
  if (!passed) process.exitCode = 1
}

function main() {
  try {
    const args = parseArguments(process.argv.slice(2))
    if (args.help) { console.log(usage()); return }
    const toolchainBinding = loadToolchain(args.toolchainManifest, args.toolchainManifestSha256)
    if (args.selfTest) { selfTest(toolchainBinding); return }
    const bundle = { build: readEvidence(args.build), inspector: readEvidence(args.inspector), tongues: readEvidence(args.tongues) }
    const output = resolve(args.output)
    if (Object.values(bundle).some((evidence) => samePath(evidence.absolute, output))) throw new Error('Output must not overwrite input evidence.')
    const expectedOutput = join(resolve(String(bundle.build.document.attempt_root ?? '')), 'evidence', 'lock_topology_888x14.validation.v2.json')
    if (!samePath(output, expectedOutput)) throw new Error(`Output path must be exact: ${expectedOutput}`)
    const result = validateBundle(bundle, toolchainBinding)
    writeJsonAtomicNew(output, result)
    console.log(JSON.stringify({ output, status: result.status, failedCheckCount: result.failedCheckCount, structureEngineeringEvidencePass: result.structureEngineeringEvidencePass }))
    process.exitCode = result.status === 'PASS' ? 0 : 1
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exitCode = 2
  }
}

main()
