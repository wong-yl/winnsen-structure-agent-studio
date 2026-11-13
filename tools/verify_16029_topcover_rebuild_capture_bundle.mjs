import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import {
  existsSync,
  lstatSync,
  openSync,
  closeSync,
  fsyncSync,
  readFileSync,
  realpathSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const attemptsRoot = resolve(root, 'data/native_model_requests/attempts')
const v37Root = resolve(root,
  'workers/generated_models/review_generation_requests/' +
  'v43-int-v37-760w-six-door-l642-r246-r1/native_cad/' +
  'final_native_hierarchical_pack_and_go')
const goldRoot = resolve(root,
  'workers/analysis/desktop_reference/16029_金标准原始素材_U盘_20260526/1.工程图')

const EXPECTED_PARTS = Object.freeze([
  '上盖壳体左侧板.sldprt',
  '上盖壳体右侧板.SLDPRT',
  '上盖壳体前侧板.sldprt',
  '上盖壳体后侧板.sldprt',
  '上盖壳体底板.sldprt',
])
const EXPECTED_BENDS = Object.freeze([5, 5, 2, 5, 7])
const EXPECTED_V37_EXTERNAL = Object.freeze([2, 6, 2, 2, 9])
const EXPECTED_GOLD_EXTERNAL = Object.freeze([2, 6, 2, 2, 13])
const EXPECTED_DXF = Object.freeze({
  '上盖壳体左侧板.sldprt': [84, '3EB9D097FDEE6687CFD0E71AF48FFCCE55FA44039042539FB52844B814AB5F82'],
  '上盖壳体右侧板.SLDPRT': [84, '5F666F6826054FD4E3B2C498E9F579FA2FA7D64234F696825E179E03DDF5A5D4'],
  '上盖壳体前侧板.sldprt': [34, 'BE669CE428A3D7EAF629B7A54C91C416F0D8B79E658BBD60E9700C0E5120DF94'],
  '上盖壳体后侧板.sldprt': [91, '23C5D30F66C039AEE3948445B625699DCD20789E295238E7A0D912F03755DDEC'],
  '上盖壳体底板.sldprt': [84, '346F032C4CE8EE3F35B700286B4B1864DE906DE42EF19FECEC25EF5A2896A9CA'],
})
const EXPECTED_V37_ROOT_SHA =
  '5AED314E89A65A3875C517B2F4DC179635E9C684E0CD1877AFEBD33CAA77CA5B'

const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const sha256File = (path) => sha256(readFileSync(path))
const sameSet = (actual, expected) => actual.length === expected.length &&
  [...actual].sort((a, b) => a.localeCompare(b, 'en', { sensitivity: 'base' }))
    .every((value, index) => value.toUpperCase() ===
      [...expected].sort((a, b) => a.localeCompare(b, 'en', { sensitivity: 'base' }))[index].toUpperCase())
const finiteArray = (value, count) => Array.isArray(value) && value.length === count &&
  value.every((item) => Number.isFinite(Number(item)))

function assertPathChainSafe(path, stop) {
  const absolute = resolve(path)
  const rootPath = resolve(stop)
  const rel = relative(rootPath, absolute)
  assert.equal(!rel.startsWith('..') && !isAbsolute(rel), true,
    `path escaped fixed root: ${absolute}`)
  let current = rootPath
  for (const segment of rel.split(sep).filter(Boolean)) {
    current = resolve(current, segment)
    assert.equal(lstatSync(current).isSymbolicLink(), false,
      `path chain contains a symlink: ${current}`)
  }
  assert.equal(!relative(realpathSync.native(rootPath), realpathSync.native(absolute)).startsWith('..'),
    true, `real path escaped fixed root: ${absolute}`)
}

function readExactJson(path) {
  assert.equal(existsSync(path), true, `missing evidence: ${path}`)
  assert.equal(lstatSync(path).isSymbolicLink(), false, `evidence is a symlink: ${path}`)
  assert.equal(Number(statSync(path).nlink), 1, `evidence is linked: ${path}`)
  return JSON.parse(readFileSync(path, 'utf8'))
}

function processSnapshot() {
  const output = execFileSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ','"],
  { cwd: root, encoding: 'utf8', windowsHide: true }).trim()
  return output ? output.split(',') : []
}

function validatePart(part, expectedName, bends, externalCount, group) {
  assert.equal(part.name.toUpperCase(), expectedName.toUpperCase())
  assert.equal(part.complete, true)
  assert.deepEqual(part.unresolved, [])
  assert.equal(part.fault, '')
  assert.equal(part.readOnly, true)
  assert.equal(part.openErrors, 0)
  assert.equal(part.openWarnings, 0)
  assert.equal(part.rebuilt, true)
  assert.equal(part.sessionExitProven, true)
  assert.equal(part.sourceBinding?.unchanged, true)
  assert.equal(part.measurement?.bodyCount, 1)
  assert.deepEqual(part.measurement?.featureIssues, [])
  assert.equal(finiteArray(part.measurement?.partBoxMm, 6), true)
  assert.equal(Number(part.measurement?.modelExternalReferenceCount), externalCount)
  assert.equal(Number(part.measurement?.extensionExternalReferenceCount), externalCount)
  assert.equal((part.sketches ?? []).length > 0, true)
  assert.equal((part.sketches ?? []).every((value) => value.complete === true), true)
  assert.equal((part.oneBends ?? []).length, bends)
  assert.equal((part.oneBends ?? []).every((value) => value.complete === true &&
    value.supplementRequired === true && Number(value.direction) > 0 &&
    Number(value.order) > 0 && Number(value.radius) > 0 &&
    Number.isFinite(Number(value.angle)) && Number.isFinite(Number(value.kFactor))), true)
  assert.equal((part.processBends ?? []).length, 1)
  assert.equal(part.processBends[0].complete, true)
  assert.equal((part.flatPatterns ?? []).length, 1)
  assert.equal(part.flatPatterns[0].complete, true)
  if (group === 'gold' && expectedName === '上盖壳体底板.sldprt') {
    assert.equal(part.knownGoldBottomHistoricalDependencyAccepted, true)
  } else {
    assert.equal(part.preopenDependencies?.all_target_local, true)
  }
}

const args = process.argv.slice(2)
assert.equal(args.length, 2)
assert.equal(args[0], '--attempt-dir')
const attempt = resolve(args[1])
assert.deepEqual(processSnapshot(), [], 'bundle verification requires zero CAD processes')
assertPathChainSafe(attempt, attemptsRoot)
const privateDir = resolve(attempt, 'evidence/private')
assertPathChainSafe(privateDir, attempt)

const cadPath = resolve(privateDir, 'topcover_native_rebuild_capture_probe_only.json')
const dxfPath = resolve(privateDir, 'topcover_native_rebuild_gold_dxf_capture.json')
const cleanupPath = resolve(privateDir, 'topcover_native_rebuild_capture_probe_cleanup.json')
const manualCleanupPath = resolve(privateDir, 'topcover_native_rebuild_capture_manual_cleanup.json')
const cad = readExactJson(cadPath)
const dxf = readExactJson(dxfPath)
const cleanup = readExactJson(cleanupPath)
const manualCleanup = existsSync(manualCleanupPath) ? readExactJson(manualCleanupPath) : null

assert.equal(cad.schema, 'winnsen.16029.topcover_native_rebuild_capture_probe.v1')
assert.equal(cad.probeOnly, true)
assert.equal(cad.consumable, false)
assert.equal(cad.datasetComplete, true)
assert.equal(cad.privateEvidenceWritten, true)
assert.equal(cad.v37SourceExact75Unchanged, true)
assert.equal(cad.workingPackExact75Unchanged, true)
assert.equal(cad.goldInputsUnchanged, true)
assert.equal(cad.goldSourceLockStateUnchanged, true)
assert.equal(cad.goldCapturePackComplete, true)
assert.equal(cad.goldAssembly?.complete, true)
assert.equal(cad.masterExcludedFromNewAssembly, true)
assert.equal((cad.physicalIdentityTransforms ?? []).length, 5)
assert.equal(cad.physicalIdentityTransforms.every((value) => finiteArray(value.transform16, 16)), true)
assert.equal((cad.goldInputs ?? []).length, 19)
assert.equal(cad.goldInputs.every((value) => value.unchanged === true), true)
assert.equal(sameSet(cad.v37Parts.map((value) => value.name), EXPECTED_PARTS), true)
assert.equal(sameSet(cad.goldParts.map((value) => value.name), EXPECTED_PARTS), true)
for (let index = 0; index < EXPECTED_PARTS.length; index += 1) {
  validatePart(cad.v37Parts.find((value) => value.name.toUpperCase() ===
    EXPECTED_PARTS[index].toUpperCase()), EXPECTED_PARTS[index], EXPECTED_BENDS[index],
  EXPECTED_V37_EXTERNAL[index], 'v37')
  validatePart(cad.goldParts.find((value) => value.name.toUpperCase() ===
    EXPECTED_PARTS[index].toUpperCase()), EXPECTED_PARTS[index], EXPECTED_BENDS[index],
  EXPECTED_GOLD_EXTERNAL[index], 'gold')
}

assert.equal(dxf.schema, 'winnsen.16029.topcover_native_rebuild_gold_dxf_capture.v1')
assert.equal(dxf.probeOnly, true)
assert.equal(dxf.consumable, false)
assert.equal(dxf.capturePass, true)
assert.equal(dxf.sourceFilesUnchanged, true)
assert.equal((dxf.files ?? []).length, 5)
for (const row of dxf.files) {
  const expected = EXPECTED_DXF[row.partName]
  assert.ok(expected, `unexpected DXF part: ${row.partName}`)
  assert.equal(row.complete, true)
  assert.equal(row.sourceUnchanged, true)
  assert.equal(Number(row.entityCount), expected[0])
  assert.equal(row.canonicalGeometrySha256, expected[1])
  assert.equal((row.entities ?? []).length, expected[0])
}

const cleanupPass = cleanup.cleanupPass === true || manualCleanup?.cleanupPass === true
assert.equal(cleanupPass, true)
assert.equal(manualCleanup?.workingPackAbsent ?? cleanup.targetAbsent, true)
assert.equal(manualCleanup?.goldCapturePackAbsent ?? cleanup.goldCapturePackAbsent, true)
assert.equal(existsSync(resolve(attempt, 'native_cad/working_pack')), false)
assert.equal(existsSync(resolve(attempt, 'native_cad/gold_capture_pack')), false)
assert.equal(existsSync(resolve(attempt, 'receipts/clone_native_seed.json')), false)
assert.equal(existsSync(resolve(attempt, 'evidence/seed_pack_888_native_v1.json')), false)

assert.equal(sha256File(resolve(v37Root,
  '标准寄存柜1917×760×550(总装配).SLDASM')), EXPECTED_V37_ROOT_SHA)
assert.equal(cad.goldInputs.filter((value) => value.role === 'gold_engineering').every((value) =>
  sha256File(resolve(goldRoot, value.name)) === value.sha256Before), true)

const bundle = {
  schema: 'winnsen.16029.topcover_native_rebuild_capture_bundle.v1',
  probeOnly: true,
  consumable: false,
  generatedAtUtc: new Date().toISOString(),
  attemptId: attempt.slice(attemptsRoot.length + 1).replaceAll('\\', '/'),
  inputs: {
    cadEvidence: { path: relative(attempt, cadPath).replaceAll('\\', '/'), sha256: sha256File(cadPath) },
    dxfEvidence: { path: relative(attempt, dxfPath).replaceAll('\\', '/'), sha256: sha256File(dxfPath) },
    cleanupEvidence: {
      path: relative(attempt, manualCleanup ? manualCleanupPath : cleanupPath).replaceAll('\\', '/'),
      sha256: sha256File(manualCleanup ? manualCleanupPath : cleanupPath),
    },
  },
  gates: {
    cadDatasetComplete: true,
    dxfDatasetComplete: true,
    sourceStateUnchanged: true,
    cleanupComplete: true,
    sharedReceiptAbsent: true,
    physicalPartCount: 5,
    goldAssemblyComponentCount: 6,
    newAssemblyPhysicalMemberCount: 5,
    goldDxfEntityCount: dxf.files.reduce((sum, value) => sum + value.entityCount, 0),
  },
  qualityBoundary: 'Reconstruction evidence bundle only; no new native CAD part or assembly exists.',
}
const outputPath = resolve(privateDir, 'topcover_native_rebuild_capture_bundle.json')
assert.equal(existsSync(outputPath), false, `immutable bundle already exists: ${outputPath}`)
const bytes = Buffer.from(`${JSON.stringify(bundle, null, 2)}\n`, 'utf8')
const descriptor = openSync(outputPath, 'wx', 0o600)
try {
  writeFileSync(descriptor, bytes)
  fsyncSync(descriptor)
} finally {
  closeSync(descriptor)
}
assert.deepEqual(readFileSync(outputPath), bytes)
assert.equal(Number(statSync(outputPath).nlink), 1)
assert.equal(lstatSync(outputPath).isSymbolicLink(), false)
assert.deepEqual(processSnapshot(), [], 'bundle verification must not start CAD')
process.stdout.write(`${JSON.stringify({ status: 'PASS', outputPath,
  outputSha256: sha256File(outputPath), gates: bundle.gates }, null, 2)}\n`)
