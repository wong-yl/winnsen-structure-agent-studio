import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, realpathSync, readdirSync, statSync } from 'node:fs'
import { basename, dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const attempt = resolve(process.argv[2] ?? '')
const attemptsRoot = resolve(repoRoot, 'data/native_model_requests/attempts')
const taskRoot = dirname(attempt)
const toolDir = resolve(attempt,
  'toolchain_snapshot/topcover_parts_native_v1_3E967749')

assert.equal(basename(attempt).toLowerCase(), 'attempt-0001')
assert.match(basename(taskRoot), /^TOPCOVER-PILOT-[A-Z0-9-]{8,120}$/)
assert.equal(dirname(taskRoot), attemptsRoot)

const evidencePath = resolve(attempt, 'evidence/private/topcover_front_760_native_pilot.json')
const receiptPath = resolve(attempt, 'receipts/topcover_front_760_native_pilot.json')
const authorizationPath = resolve(attempt,
  'execution_authorizations/topcover_parts_native_v1.json')
const partPath = resolve(attempt,
  'native_cad/front_760_native_pilot/上盖壳体前侧板.sldprt')
const sourcePath = resolve(toolDir, 'TopCoverPartsNative.cs')
const executablePath = resolve(toolDir, 'TopCoverPartsNative.exe')
const manifestPath = resolve(toolDir, 'toolchain_manifest.json')
const staticPath = resolve(toolDir, 'static_verification.json')
const snapshotManifestPath = resolve(toolDir, 'snapshot_manifest.json')

const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name,
  pass: Boolean(pass),
  ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance) =>
  Number.isFinite(left) && Number.isFinite(right) && Math.abs(left - right) <= tolerance
const exactKeys = (value, expected) => value && typeof value === 'object' &&
  !Array.isArray(value) &&
  JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
const under = (candidate, root) => {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function regularSingleLinkNoReparse(path) {
  if (!existsSync(path) || !under(path, attempt)) return false
  const info = statSync(path)
  if (!info.isFile() || Number(info.nlink) !== 1 || lstatSync(path).isSymbolicLink()) return false
  return under(realpathSync.native(path), realpathSync.native(attempt))
}

function normalizedSourceHash(text) {
  const normalized = text.replace(/\r\n?/g, '\n')
  const pattern = /(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/g
  assert.equal([...normalized.matchAll(pattern)].length, 1)
  return hash(Buffer.from(normalized.replace(pattern, '$1__SOURCE_SHA256__$2'), 'utf8'))
}

for (const path of [evidencePath, receiptPath, authorizationPath, partPath,
  sourcePath, executablePath, manifestPath, staticPath, snapshotManifestPath]) {
  add(`single_link_no_reparse:${basename(path)}`, regularSingleLinkNoReparse(path))
}

const evidenceBytes = readFileSync(evidencePath)
const receiptBytes = readFileSync(receiptPath)
const authorizationBytes = readFileSync(authorizationPath)
const evidence = JSON.parse(evidenceBytes.toString('utf8'))
const receipt = JSON.parse(receiptBytes.toString('utf8'))
const authorization = JSON.parse(authorizationBytes.toString('utf8'))
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
const staticReport = JSON.parse(readFileSync(staticPath, 'utf8'))
const snapshotManifest = JSON.parse(readFileSync(snapshotManifestPath, 'utf8'))

add('pilot_boundary', evidence.schema === 'winnsen.16029.topcover_front_native_pilot.v3' &&
  evidence.purpose === 'structure_engineering_assistance' &&
  evidence.pilotPass === true && evidence.consumable === false &&
  evidence.status === 'TOPCOVER_FRONT_760_NATIVE_PILOT_COMPLETE')
const outputNames = readdirSync(dirname(partPath))
add('one_exact_output_file', outputNames.length === 1 &&
  outputNames[0].toLowerCase() === '上盖壳体前侧板.sldprt'.toLowerCase(), outputNames)
add('output_path_and_hash', resolve(evidence.outputPath) === partPath &&
  evidence.outputSha256 === fileHash(partPath) &&
  evidence.freshReadOnlyVerification?.fileSha256Before === fileHash(partPath) &&
  evidence.freshReadOnlyVerification?.fileSha256After === fileHash(partPath))

const receiptKeys = [
  'schema', 'phase', 'success', 'consumable', 'completedAtUtc',
  'authorizationId', 'authorizationSha256', 'authorizationIssuedAtUtc',
  'authorizationExpiresAtUtc', 'ruleSha256', 'toolId',
  'toolSourceNormalizedSha256', 'toolExecutableSha256',
  'outputRelativePath', 'outputSha256', 'evidenceRelativePath',
  'evidenceSha256', 'qualityBoundary',
]
add('receipt_exact_pair', exactKeys(receipt, receiptKeys) &&
  receipt.schema === 'winnsen.16029.topcover_front_native_receipt.v1' &&
  receipt.success === true && receipt.consumable === false &&
  receipt.authorizationId === evidence.authorizationId &&
  receipt.authorizationSha256 === fileHash(authorizationPath) &&
  receipt.evidenceSha256 === hash(evidenceBytes) &&
  receipt.outputSha256 === fileHash(partPath) &&
  receipt.ruleSha256 === evidence.ruleSha256)

add('authorization_live_at_completion',
  authorization.schema === 'winnsen.16029.topcover_front_native_authorization.v1' &&
  authorization.approved === true &&
  authorization.authorizationId === evidence.authorizationId &&
  evidence.authorizationSha256 === fileHash(authorizationPath) &&
  Date.parse(authorization.issuedAtUtc) <= Date.parse(evidence.completedAtUtc) &&
  Date.parse(evidence.completedAtUtc) <= Date.parse(authorization.expiresAtUtc))

const sourceText = readFileSync(sourcePath, 'utf8')
const sourceNormalizedSha256 = normalizedSourceHash(sourceText)
const executableSha256 = fileHash(executablePath)
add('toolchain_frozen_hash_binding',
  evidence.toolSourceNormalizedSha256 === sourceNormalizedSha256 &&
  evidence.toolExecutableSha256 === executableSha256 &&
  authorization.toolSourceNormalizedSha256 === sourceNormalizedSha256 &&
  authorization.toolExecutableSha256 === executableSha256 &&
  manifest.tool?.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  manifest.tool?.executableSha256 === executableSha256 &&
  staticReport.status === 'PASS' && staticReport.cadStarted === false &&
  staticReport.failed === 0)
add('frozen_toolchain_snapshot',
  snapshotManifest.schema ===
    'winnsen.16029.topcover_front_native_toolchain_snapshot.v1' &&
  snapshotManifest.sourceNormalizedSha256 === sourceNormalizedSha256 &&
  snapshotManifest.evidenceSha256 === hash(evidenceBytes) &&
  snapshotManifest.receiptSha256 === hash(receiptBytes) &&
  snapshotManifest.outputSha256 === fileHash(partPath) &&
  Array.isArray(snapshotManifest.files) && snapshotManifest.files.length === 12 &&
  snapshotManifest.files.every((entry) =>
    existsSync(resolve(toolDir, entry.name)) &&
    fileHash(resolve(toolDir, entry.name)) === entry.sha256))

const rulePath = resolve(repoRoot, evidence.rulePath)
add('rule_live_hash_binding', under(rulePath, repoRoot) &&
  fileHash(rulePath) === evidence.ruleSha256 &&
  evidence.ruleSha256 ===
    '15CDAA0B19189F3D6E41D7A277F15AA1A0CEF58CA304B6BE5E6B207F43B14553')

const expectedBox = [-380, 1839.19998, -56.9, 380, 1917.000005, -45.99995]
const expectedRadii = [0.05, 0.2, 0.85, 1,
  1.75, 1.75, 1.75, 1.75, 1.75, 5, 5, 5, 5, 5]
function geometryPass(g) {
  return g?.bodyCount === 1 && g?.finite === true &&
    g.bodyBoxMm?.length === 6 && g.bodyBoxMm.every((value, index) =>
      near(value, expectedBox[index], 0.02)) &&
    near(g.volumeMm3, 27485.460097, 2) &&
    near(g.surfaceAreaMm2, 70383.22241, 5) &&
    near(g.massKg, 0.215760862, 0.00002) &&
    g.faceCount === 48 && g.edgeCount === 112 &&
    g.cylinderFaceCount === 14 && g.loopCount === 78 &&
    g.loopEdgeReferenceCount === 224 && g.circleEdgeReferenceCount === 56 &&
    g.cylinderRadiiMm?.length === expectedRadii.length &&
    g.cylinderRadiiMm.every((value, index) => near(value, expectedRadii[index], 0.002))
}
add('live_geometry_exact', geometryPass(evidence.liveGeometry))
add('fresh_geometry_exact', geometryPass(evidence.freshReadOnlyVerification?.geometry))
add('live_and_fresh_geometry_stable',
  JSON.stringify(evidence.liveGeometry?.bodyBoxMm) ===
    JSON.stringify(evidence.freshReadOnlyVerification?.geometry?.bodyBoxMm) &&
  near(evidence.liveGeometry?.volumeMm3,
    evidence.freshReadOnlyVerification?.geometry?.volumeMm3, 0.000001))

function featurePass(f) {
  const bends = f?.bends ?? []
  return f?.pass === true && f.sheetMetalCount === 1 &&
    f.oneBendCount === 2 && f.flatPatternCount === 1 &&
    f.issues?.length === 0 && near(f.sheetMetalThicknessMm, 0.8, 0.0001) &&
    near(f.sheetMetalRadiusMm, 0.2, 0.0001) && near(f.sheetMetalKFactor, 0.5, 1e-6) &&
    f.sheetMetalAllowanceType === 2 && bends.length === 2 &&
    bends[0].bendType === 2 && bends[0].order === 1 &&
    near(bends[0].angleRadians, Math.PI / 2, 1e-6) &&
    bends[0].direction === 1 && bends[0].down === false &&
    near(bends[0].radiusMm, 0.2, 0.0001) &&
    bends[1].bendType === 2 && bends[1].order === 2 &&
    near(bends[1].angleRadians, Math.PI, 1e-6) &&
    bends[1].direction === 2 && bends[1].down === true &&
    near(bends[1].radiusMm, 0.05, 0.0001)
}
add('live_native_feature_contract', featurePass(evidence.liveFeatures))
add('fresh_native_feature_contract',
  featurePass(evidence.freshReadOnlyVerification?.features))

add('build_stage_sequence',
  JSON.stringify(evidence.buildStages?.map((value) => value.stage)) ===
    JSON.stringify(['base_flange', 'through_all_holes', 'native_bend_1', 'native_bend_2']) &&
  evidence.buildStages.every((value) => value.pass === true))
add('fresh_read_only_gate',
  evidence.freshReadOnlyVerification?.pass === true &&
  evidence.freshReadOnlyVerification?.readOnly === true &&
  evidence.freshReadOnlyVerification?.openErrors === 0 &&
  evidence.freshReadOnlyVerification?.openWarnings === 0 &&
  evidence.freshReadOnlyVerification?.externalReferenceCount === 0 &&
  evidence.freshReadOnlyVerification?.fileUnchanged === true)

const sessions = evidence.processAudit?.sessions ?? []
add('owned_cad_sessions_closed', sessions.length === 2 &&
  sessions.every((value) => value.ExitProven === true && value.exitState === 'exited' &&
    value.monitorsExitProven === true && value.sldworks?.executableSha256 ===
      '1318AE1BE2F1B06AD360938760217378582B6FCA95CC2B2EB181C21262948978' &&
    value.monitors.every((monitor) => monitor.executableSha256 ===
      'A858328B0A0D24CB0C6FCDEF6FD00DB735E07F897654E1E4C4A18235AC492B70' &&
      monitor.parentPid === value.sldworks.pid &&
      new RegExp(`(?:^|\\s)--ppid=${value.sldworks.pid}(?:\\s|$)`).test(monitor.commandLine))))
add('final_process_gate', evidence.processAudit?.finalGate === true &&
  evidence.processAudit?.finalSldworks?.length === 0 &&
  evidence.processAudit?.finalMonitors?.length === 0 &&
  evidence.processAudit?.cleanupErrors?.length === 0)

const passed = checks.filter((value) => value.pass).length
const report = {
  schema: 'winnsen.16029.topcover_front_native_pilot_independent_verification.v1',
  status: passed === checks.length ? 'PASS' : 'FAIL',
  attempt,
  checkCount: checks.length,
  passed,
  failed: checks.length - passed,
  evidenceSha256: hash(evidenceBytes),
  receiptSha256: hash(receiptBytes),
  outputSha256: fileHash(partPath),
  checks,
}

process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
