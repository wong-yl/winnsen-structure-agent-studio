import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { cpSync, linkSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, resolve } from 'node:path'
import { adaptDoorSource, DOOR_SOURCE_PATH } from '../workers/native_model_requests/parametric_v1/door_adapter.mjs'
import { adaptLockSource, LOCK_SOURCE_PATH } from '../workers/native_model_requests/parametric_v1/lock_adapter.mjs'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from '../workers/native_model_requests/parametric_v1/width_adapter.mjs'
import { build16029PhysicalAuditFixture } from './fixtures/locker_16029_physical_audit.mjs'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
import { current16029ParametricBuildSources, normalizedNativeSourceHash, read16029ParametricRuntimeRepair, READONLY_OPEN_REPAIR_PROFILE, READONLY_OPEN_REPAIR_PROFILES, verify16029ParametricBuild } from './lib/locker_16029_parametric_build.mjs'
import { compare16029PhysicalAudit } from './verify_16029_parametric_physical_quality.mjs'

const root = mkdtempSync(resolve(tmpdir(), '16029-build-selection-'))
const sourceFolder = resolve(root, 'workers/native_model_requests/parametric_v1')
const attemptId = 'PARAM-acceptance-fixture'
const attempt = resolve(root, 'data/parametric_attempts', attemptId)
const buildTag = 'engineer-feedback-20261008-r4'
const rearWidthRepairTag = 'readonly-open-20261109-r4-rearwidth1'
const rearGeometryRepairTag = 'readonly-open-20261110-r4-reargeometry1'
const doorPrecisionRepairTag = 'readonly-open-20261110-r4-doorprecision1'
const doorPrecision2RepairTag = 'readonly-open-20261110-r4-doorprecision2'
const doorPrecision3RepairTag = 'readonly-open-20261110-r4-doorprecision3'
const hashBytes = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const hash = path => hashBytes(readFileSync(path))
const write = (path, value) => { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, JSON.stringify(value, null, 2) + '\n') }
const writeText = (path, value) => { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, value) }
const rawSources = [WIDTH_SOURCE_PATH, DOOR_SOURCE_PATH, LOCK_SOURCE_PATH]
const nativeNames = ['ParametricContext.cs', 'NativeParametricAssembly.cs', 'NativeParametricVerification.cs', 'NativeParametricLocks.cs', 'NativeParametricPhysicalAudit.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricEnvelope.cs']

function copyBuildInputs() {
  for (const path of rawSources.concat(nativeNames.map(name => 'workers/native_model_requests/parametric_v1/' + name))) {
    const destination = resolve(root, path)
    mkdirSync(dirname(destination), { recursive: true })
    cpSync(path, destination)
  }
}

function createBuild(tag = buildTag) {
  const sources = current16029ParametricBuildSources(root)
  const bin = resolve(sourceFolder, 'bin-' + tag)
  const generated = resolve(sourceFolder, 'build-' + tag)
  mkdirSync(bin, { recursive: true })
  mkdirSync(generated, { recursive: true })
  for (const [name, text] of sources) {
    const path = nativeNames.includes(name) ? resolve(sourceFolder, name) : resolve(generated, name)
    if (!nativeNames.includes(name)) writeText(path, text)
  }
  const executable = resolve(bin, 'NativeParametricAssembly.exe')
  writeText(executable, 'synthetic executable for build-selection validation\n')
  const manifestSources = [...sources.keys()].map(name => ({
    path: nativeNames.includes(name) ? resolve(sourceFolder, name) : resolve(generated, name),
    sha256: hash(nativeNames.includes(name) ? resolve(sourceFolder, name) : resolve(generated, name)),
    normalizedSha256: normalizedNativeSourceHash(sources.get(name)),
  }))
  write(resolve(bin, 'build.json'), {
    executable,
    executableSha256: hash(executable),
    sources: manifestSources,
    sourceHashes: manifestSources.map(row => ({ name: basename(row.path), normalizedSha256: row.normalizedSha256 })),
  })
  return { executable, executableSha256: hash(executable), sources }
}

function setupAcceptance(build, targetAttempt = attempt, selectedTag = buildTag, selectDefault = true) {
  const fixture = build16029PhysicalAuditFixture({ sheetCount: 2 })
  const baselinePath = resolve(root, 'data/parametric_attempts/PARAM-baseline/evidence/physical-audit.json')
  write(baselinePath, fixture.reference)
  const packageDir = resolve(targetAttempt, 'package')
  const packageRows = fixture.options.packageInventory.map(row => {
    const file = basename(row.path)
    const path = resolve(packageDir, file)
    writeText(path, fixture.content(file))
    return { path, sha256: hash(path) }
  })
  const packageEvidence = { phase: 'package', success: true, rows: packageRows }
  const packageVerification = structuredClone(fixture.options.packageVerification)
  packageVerification.phase = 'verify-package'
  const auditPath = resolve(targetAttempt, 'evidence/physical-audit.json')
  const packageEvidencePath = resolve(targetAttempt, 'evidence/package.json')
  const packageVerificationPath = resolve(targetAttempt, 'evidence/verify-package.json')
  write(auditPath, fixture.candidate)
  write(packageEvidencePath, packageEvidence)
  write(packageVerificationPath, packageVerification)
  const request = { cabinet: { widthMm: 850, heightMm: 1917, depthMm: 550 }, columns: ['L', 'R'].map(side => ({ side, doors: [1, 1, 1].map(heightUnits => ({ heightUnits })) })) }
  const contract = build16029ParametricContract(request)
  const planPath = resolve(targetAttempt, 'parametric_execution.json')
  write(planPath, { schema: 'winnsen.locker16029.parametric_execution.v1', taskId: 'NATIVE-fixture', contract })
  const pinned = resolve(targetAttempt, 'tools/NativeParametricAssembly.exe')
  writeText(pinned, readFileSync(build.executable))
  writeText(resolve(targetAttempt, 'tools/build.json'), readFileSync(resolve(sourceFolder, 'bin-' + selectedTag, 'build.json')))
  const authorizationPath = resolve(targetAttempt, 'authorizations/physical-audit.json')
  write(authorizationPath, { phase: 'physical-audit', taskId: 'NATIVE-fixture', planSha256: hash(planPath), executableSha256: build.executableSha256 })
  write(resolve(targetAttempt, 'receipts/physical-audit.json'), { phase: 'physical-audit', taskId: 'NATIVE-fixture', planSha256: hash(planPath), authorizationSha256: hash(authorizationPath), evidenceSha256: hash(auditPath), protectedSourcesUnchanged: true })
  const protectedPath = resolve(root, WIDTH_SOURCE_PATH)
  write(resolve(targetAttempt, 'protected_sources.before.json'), [{ path: protectedPath, sha256: hash(protectedPath) }])
  const measured = compare16029PhysicalAudit(fixture.reference, fixture.candidate, { doorCount: 6, packageInventory: packageRows, packageVerification })
  const qualityPath = resolve(targetAttempt, 'evidence/physical-quality.json')
  write(qualityPath, { ...measured, baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(auditPath), packageEvidenceSha256: hash(packageEvidencePath), packageVerificationEvidenceSha256: hash(packageVerificationPath) })
  if (selectDefault) write(resolve(sourceFolder, 'build-selection.json'), { schema: 'winnsen.locker16029.native_build_selection.v1', verified: true, buildTag: selectedTag, executableSha256: build.executableSha256, acceptanceAttempt: basename(targetAttempt), acceptanceQualitySha256: hash(qualityPath) })
  return { fixture, request, packageRows, packageEvidence, packageVerification, auditPath, packageEvidencePath, packageVerificationPath, qualityPath, baselinePath, planPath, pinned, authorizationPath }
}

function createRepairBuild(tag) {
  const baselineFolder = resolve(sourceFolder, 'bin-' + buildTag)
  const baselineManifestPath = resolve(baselineFolder, 'build.json')
  const baselineManifest = JSON.parse(readFileSync(baselineManifestPath, 'utf8'))
  const directory = resolve(sourceFolder, 'repairs', tag, 'source')
  const generated = resolve(sourceFolder, 'build-' + tag)
  const executable = resolve(sourceFolder, 'bin-' + tag, 'NativeParametricAssembly.exe')
  writeText(executable, 'synthetic independent readonly repair executable: ' + tag + '\n')
  const sources = baselineManifest.sources.map(source => {
    const name = basename(source.path)
    let text = readFileSync(resolve(baselineFolder, 'source-snapshot', name), 'utf8')
    if (['NativeParametricAssembly.cs', 'NativeParametricDoorPlacement.cs'].includes(name) ||
      ([doorPrecision2RepairTag, doorPrecision3RepairTag].includes(tag) && name === 'ParametricContext.cs') ||
      (tag === doorPrecision3RepairTag && name === 'NativeParametricWidth.cs') ||
      (['readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(tag) && name === 'NativeParametricLocks.cs') ||
      (['readonly-open-20261105-r4', rearWidthRepairTag, rearGeometryRepairTag, doorPrecisionRepairTag, doorPrecision2RepairTag, doorPrecision3RepairTag, 'readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(tag) && name === 'NativeParametricPhysicalAudit.cs') ||
      (['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(tag) && ['NativeParametricDoor.cs', 'NativeParametricWidth.cs'].includes(name))) text += '\n// independent writable-open repair fixture: ' + tag + '\n'
    writeText(resolve(directory, name), text)
    const path = resolve(generated, name)
    writeText(path, text)
    return { path, sha256: hash(path), normalizedSha256: normalizedNativeSourceHash(text) }
  })
  const manifest = { executable, executableSha256: hash(executable), sources,
    sourceHashes: sources.map(row => ({ name: basename(row.path), normalizedSha256: row.normalizedSha256 })),
    repairProfile: tag,
    repairProvenance: { schema: 'winnsen.locker16029.parametric_repair_source.v1', baselineBuildTag: buildTag,
      baselineManifestSha256: hash(baselineManifestPath), baselineExecutableSha256: baselineManifest.executableSha256,
      sourceDirectory: directory },
  }
  const manifestPath = resolve(sourceFolder, 'bin-' + tag, 'build.json')
  write(manifestPath, manifest)
  return { tag, directory, generated, executable, manifestPath, manifest }
}

try {
  copyBuildInputs()
  const build = createBuild()
  const acceptance = setupAcceptance(build)
  assert.equal(verify16029ParametricBuild(buildTag, { root }).tag, buildTag)
   assert.equal(verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }).tag, buildTag)
   assert.throws(() => verify16029ParametricBuild(undefined, { root }), /unrecognized physical baseline|missing/)

  const selectionPath = resolve(sourceFolder, 'build-selection.json')
  const originalQuality = readFileSync(acceptance.qualityPath)
  const originalAudit = readFileSync(acceptance.auditPath)
  const originalPackage = readFileSync(acceptance.packageRows[0].path)
  const originalPackageVerification = readFileSync(acceptance.packageVerificationPath)
  const refreshSelection = () => {
    const selection = JSON.parse(readFileSync(selectionPath, 'utf8'))
    selection.acceptanceQualitySha256 = hash(acceptance.qualityPath)
    write(selectionPath, selection)
  }

  const alternateBaselinePath = resolve(root, 'data/parametric_attempts/PARAM-baseline-copy/evidence/physical-audit.json')
  cpSync(acceptance.baselinePath, alternateBaselinePath)
  const alternateQuality = JSON.parse(originalQuality)
  alternateQuality.baselinePath = alternateBaselinePath
  write(acceptance.qualityPath, alternateQuality)
  refreshSelection()
  assert.throws(() => verify16029ParametricBuild(undefined, { root }), /unrecognized physical baseline/)
  writeText(acceptance.qualityPath, originalQuality)
  refreshSelection()

  rmSync(acceptance.auditPath)
   assert.throws(() => verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }), /physical audit evidence|missing/)
  writeText(acceptance.auditPath, originalAudit)

  const quality = JSON.parse(originalQuality)
  quality.candidateEvidenceSha256 = 'F'.repeat(64)
  write(acceptance.qualityPath, quality)
  refreshSelection()
   assert.throws(() => verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }), /physical quality does not bind physical audit evidence/)
  writeText(acceptance.qualityPath, originalQuality)
  refreshSelection()

  const failedAudit = structuredClone(acceptance.fixture.candidate)
  failedAudit.success = false
  write(acceptance.auditPath, failedAudit)
  const failedQuality = JSON.parse(originalQuality)
  failedQuality.candidateEvidenceSha256 = hash(acceptance.auditPath)
  write(acceptance.qualityPath, failedQuality)
  refreshSelection()
   assert.throws(() => verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }), /physical audit evidence did not pass|physical quality measurement changed/)
  writeText(acceptance.auditPath, originalAudit)
  writeText(acceptance.qualityPath, originalQuality)
  refreshSelection()

  writeText(acceptance.packageRows[0].path, Buffer.concat([originalPackage, Buffer.from('drift')]))
   assert.throws(() => verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }), /package file hash changed/)
  writeText(acceptance.packageRows[0].path, originalPackage)

  const verification = JSON.parse(originalPackageVerification)
  verification.rows.find(row => row.inspection === 'native_physical_coverage').root.configuration = 'Drift'
  write(acceptance.packageVerificationPath, verification)
  const packageQuality = JSON.parse(originalQuality)
  packageQuality.packageVerificationEvidenceSha256 = hash(acceptance.packageVerificationPath)
  write(acceptance.qualityPath, packageQuality)
  refreshSelection()
   assert.throws(() => verify16029ParametricBuild(undefined, { root, baselinePathOverride: acceptance.baselinePath }), /physical quality measurement changed|coverage|package verification/)
  writeText(acceptance.packageVerificationPath, originalPackageVerification)
  writeText(acceptance.qualityPath, originalQuality)
  refreshSelection()

  writeText(resolve(sourceFolder, 'NativeParametricAssembly.cs'), readFileSync(resolve(sourceFolder, 'NativeParametricAssembly.cs'), 'utf8') + '\nsource drift\n')
  assert.throws(() => verify16029ParametricBuild(buildTag, { root }), /native build source(?: drift|Hashes drift)/)
  copyBuildInputs()
  const changedExecutable = resolve(sourceFolder, 'bin-' + buildTag, 'NativeParametricAssembly.exe')
  writeText(changedExecutable, 'executable drift\n')
  assert.throws(() => verify16029ParametricBuild(buildTag, { root }), /native executable no longer matches/)

  writeText(changedExecutable, readFileSync(acceptance.pinned))
  const manifestPath = resolve(sourceFolder, 'bin-' + buildTag, 'build.json')
  const originalManifest = readFileSync(manifestPath)
  const manifest = JSON.parse(originalManifest)
  const snapshot = resolve(sourceFolder, 'bin-' + buildTag, 'source-snapshot')
  mkdirSync(snapshot)
  for (const source of manifest.sources) cpSync(source.path, resolve(snapshot, basename(source.path)))
  const snapshotOptions = { root, baselinePathOverride: acceptance.baselinePath }
  assert.equal(verify16029ParametricBuild(undefined, snapshotOptions).sourceSnapshot.sources.length, 10)
  writeText(resolve(sourceFolder, 'NativeParametricAssembly.cs'), 'newer working source must remain independent\n')
  assert.equal(verify16029ParametricBuild(undefined, snapshotOptions).tag, buildTag)
  assert.equal(verify16029ParametricBuild(buildTag, snapshotOptions).tag, buildTag)
  assert.throws(() => verify16029ParametricBuild(buildTag, { root }), /unrecognized physical baseline/)
  const frozenSource = resolve(snapshot, 'NativeParametricAssembly.cs')
  const originalFrozen = readFileSync(frozenSource)
  writeText(frozenSource, 'snapshot source drift\n')
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot hash mismatch/)
  writeText(frozenSource, Buffer.concat([Buffer.from('\uFEFF'), originalFrozen]))
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot hash mismatch/)
  writeText(frozenSource, originalFrozen)
  const unexpected = resolve(snapshot, 'unexpected.cs')
  writeText(unexpected, 'extra source\n')
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot identity coverage mismatch/)
  rmSync(unexpected)
  rmSync(frozenSource)
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot identity coverage mismatch/)
  writeText(frozenSource, originalFrozen)
  write(manifestPath, { ...manifest, changed: true })
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot build manifest differs/)
  writeText(manifestPath, originalManifest)
  writeText(acceptance.packageRows[0].path, Buffer.concat([originalPackage, Buffer.from('frozen-build-package-drift')]))
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /package file hash changed/)
  writeText(acceptance.packageRows[0].path, originalPackage)
  assert.equal(verify16029ParametricBuild(undefined, snapshotOptions).sourceSnapshot.acceptanceAttempt, attemptId)
  const linkedSource = resolve(root, 'linked-source.cs')
  linkSync(frozenSource, linkedSource)
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /not an ordinary file/)
  rmSync(linkedSource)
  const movedSnapshot = resolve(root, 'moved-source-snapshot')
  renameSync(snapshot, movedSnapshot)
  symlinkSync(movedSnapshot, snapshot, 'junction')
  assert.throws(() => verify16029ParametricBuild(undefined, snapshotOptions), /source snapshot refuses linked or external directories/)
  rmSync(snapshot)
  renameSync(movedSnapshot, snapshot)
  const oldRawInput = readFileSync(resolve(root, DOOR_SOURCE_PATH))
  rmSync(resolve(root, DOOR_SOURCE_PATH))
  assert.equal(verify16029ParametricBuild(undefined, snapshotOptions).tag, buildTag)
  writeText(resolve(root, DOOR_SOURCE_PATH), oldRawInput)
  const unselectedTag = 'unselected-frozen-build'
  createBuild(unselectedTag)
  mkdirSync(resolve(sourceFolder, 'bin-' + unselectedTag, 'source-snapshot'))
  assert.throws(() => verify16029ParametricBuild(unselectedTag, snapshotOptions), /source snapshot must belong to the accepted default build/)

  assert.equal(READONLY_OPEN_REPAIR_PROFILE, 'readonly-open-20261105-r1')
  assert.deepEqual(read16029ParametricRuntimeRepair(snapshotOptions), {})
  assert.deepEqual(READONLY_OPEN_REPAIR_PROFILES, ['readonly-open-20261105-r1', 'readonly-open-20261105-r2', 'readonly-open-20261105-r3', 'readonly-open-20261105-r4', 'readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7', rearWidthRepairTag, rearGeometryRepairTag, doorPrecisionRepairTag, doorPrecision2RepairTag, doorPrecision3RepairTag])
  for (const repairTag of READONLY_OPEN_REPAIR_PROFILES) {
    const repair = createRepairBuild(repairTag)
    const repairOptions = { ...snapshotOptions, repairProfile: repair.tag }
    const otherProfiles = READONLY_OPEN_REPAIR_PROFILES.filter(profile => profile !== repair.tag)
    const repairManifestBytes = readFileSync(repair.manifestPath)
    const selectedBuildBytes = readFileSync(selectionPath)
    const checkedRepair = verify16029ParametricBuild(repair.tag, repairOptions)
    if (['readonly-open-20261105-r4', rearWidthRepairTag, rearGeometryRepairTag, doorPrecisionRepairTag, doorPrecision2RepairTag, doorPrecision3RepairTag].includes(repair.tag)) {
      const runtimePath = resolve(sourceFolder, 'runtime-repair-selection.json')
      const acceptedId = 'PARAM-runtime-repair-acceptance-fixture-' + repair.tag
      const accepted = setupAcceptance({ executable: repair.executable, executableSha256: hash(repair.executable) },
        resolve(root, 'data/parametric_attempts', acceptedId), repair.tag, false)
      const selection = { schema: 'winnsen.locker16029.parametric_runtime_repair.v1', verified: true,
        buildTag: repair.tag, repairProfile: repair.tag, executableSha256: hash(repair.executable),
        acceptanceAttempt: acceptedId, acceptanceQualitySha256: hash(accepted.qualityPath) }
      write(runtimePath, selection)
      assert.deepEqual(read16029ParametricRuntimeRepair(snapshotOptions), { buildTag: repair.tag, repairProfile: repair.tag })
      assert.deepEqual(readFileSync(selectionPath), selectedBuildBytes)
      for (const patch of [{ verified: false }, { buildTag: 'unrecognized-repair' }, { repairProfile: 'other-profile' },
        { acceptanceAttempt: '../foreign' }, { executableSha256: 'A'.repeat(64) }, { acceptanceQualitySha256: 'B'.repeat(64) }]) {
        write(runtimePath, { ...selection, ...patch })
        assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), /runtime repair/)
      }
      write(runtimePath, selection)
      const qualityBytes = readFileSync(accepted.qualityPath)
      write(accepted.qualityPath, { ...JSON.parse(qualityBytes), pass: false })
      write(runtimePath, { ...selection, acceptanceQualitySha256: hash(accepted.qualityPath) })
      assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), /physical coverage has not passed/)
      writeText(accepted.qualityPath, qualityBytes)
      write(runtimePath, selection)
      const packageBytes = readFileSync(accepted.packageRows[0].path)
      writeText(accepted.packageRows[0].path, Buffer.concat([packageBytes, Buffer.from('changed runtime package')]))
      assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), /package file hash changed/)
      writeText(accepted.packageRows[0].path, packageBytes)
      const pinnedManifestPath = resolve(root, 'data/parametric_attempts', acceptedId, 'tools/build.json')
      const pinnedManifest = readFileSync(pinnedManifestPath)
      writeText(pinnedManifestPath, Buffer.concat([pinnedManifest, Buffer.from(' ')]))
      assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), /differs from its accepted attempt/)
      writeText(pinnedManifestPath, pinnedManifest)
      const pinnedExecutable = readFileSync(accepted.pinned)
      writeText(accepted.pinned, Buffer.concat([pinnedExecutable, Buffer.from('changed accepted executable')]))
      assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), /differs from its accepted attempt/)
      writeText(accepted.pinned, pinnedExecutable)
      const receiptPath = resolve(root, 'data/parametric_attempts', acceptedId, 'receipts/physical-audit.json')
      const protectedEvidencePath = resolve(root, 'data/parametric_attempts', acceptedId, 'protected_sources.before.json')
      for (const [path, patch, error] of [
        [accepted.auditPath, value => ({ ...value, success: false }), /physical quality does not bind physical audit evidence/],
        [accepted.packageVerificationPath, value => ({ ...value, success: false }), /package acceptance evidence is incomplete/],
        [accepted.planPath, value => ({ ...value, taskId: 'NATIVE-other-fixture' }), /physical audit receipt plan binding failed/],
        [receiptPath, value => ({ ...value, evidenceSha256: 'C'.repeat(64) }), /physical audit receipt evidence binding failed/],
        [accepted.authorizationPath, value => ({ ...value, executableSha256: 'D'.repeat(64) }), /physical audit receipt evidence binding failed/],
        [protectedEvidencePath, value => value.map(row => ({ ...row, sha256: 'E'.repeat(64) })), /protected source hash changed/],
      ]) {
        const bytes = readFileSync(path)
        write(path, patch(JSON.parse(bytes)))
        assert.throws(() => read16029ParametricRuntimeRepair(snapshotOptions), error)
        writeText(path, bytes)
      }
      assert.equal(read16029ParametricRuntimeRepair(snapshotOptions).buildTag, repair.tag)
      rmSync(runtimePath)
      assert.deepEqual(read16029ParametricRuntimeRepair(snapshotOptions), {})
    }
    assert.equal(checkedRepair.repairSource.sources.length, 10)
    assert.equal(checkedRepair.sourceSnapshot, undefined)
    const mutableNames = ['NativeParametricAssembly.cs', 'NativeParametricDoorPlacement.cs',
      ...([doorPrecision2RepairTag, doorPrecision3RepairTag].includes(repairTag) ? ['ParametricContext.cs'] : []),
      ...(repairTag === doorPrecision3RepairTag ? ['NativeParametricWidth.cs'] : []),
      ...(['readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairTag) ? ['NativeParametricLocks.cs'] : []),
      ...(['readonly-open-20261105-r4', rearWidthRepairTag, rearGeometryRepairTag, doorPrecisionRepairTag, doorPrecision2RepairTag, doorPrecision3RepairTag, 'readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairTag) ? ['NativeParametricPhysicalAudit.cs'] : []),
      ...(['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairTag) ? ['NativeParametricDoor.cs', 'NativeParametricWidth.cs'] : [])]
    assert.deepEqual(checkedRepair.repairSource.sources.filter(row => row.sha256 !== row.baselineSha256).map(row => row.name).sort(), mutableNames.sort())
    assert.equal(verify16029ParametricBuild(undefined, snapshotOptions).tag, buildTag)
    assert.deepEqual(readFileSync(selectionPath), selectedBuildBytes)
    assert.throws(() => verify16029ParametricBuild(repair.tag, snapshotOptions), /explicit recognized repair profile/)
    assert.throws(() => verify16029ParametricBuild(undefined, repairOptions), /explicit build tag/)
    assert.throws(() => verify16029ParametricBuild(buildTag, repairOptions), /explicit recognized repair profile/)
    assert.throws(() => verify16029ParametricBuild(repair.tag, { ...repairOptions, repairProfile: 'unrecognized-repair' }), /explicit recognized repair profile/)
    assert.throws(() => verify16029ParametricBuild(repair.tag, { ...repairOptions, repairProfile: 'readonly-open-20261105-r6' }), /explicit recognized repair profile/)
    for (const otherProfile of otherProfiles) {
      assert.throws(() => verify16029ParametricBuild(repair.tag, { ...repairOptions, repairProfile: otherProfile }), /explicit recognized repair profile/)
      write(repair.manifestPath, { ...repair.manifest, repairProfile: otherProfile })
      assert.throws(() => verify16029ParametricBuild(repair.tag, { ...repairOptions, repairProfile: otherProfile }), /native repair build identity is invalid/)
      writeText(repair.manifestPath, repairManifestBytes)
    }

    for (const [key, value] of [['schema', 'unrecognized'], ['baselineBuildTag', 'other-build'],
      ['baselineManifestSha256', 'F'.repeat(64)], ['baselineExecutableSha256', 'E'.repeat(64)],
      ['sourceDirectory', resolve(root, 'foreign-repair-source')],
      ...otherProfiles.map(profile => ['sourceDirectory', resolve(sourceFolder, 'repairs', profile, 'source')])]) {
      const changed = structuredClone(repair.manifest)
      changed.repairProvenance[key] = value
      write(repair.manifestPath, changed)
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /repair source provenance|repair baseline identity/)
      writeText(repair.manifestPath, repairManifestBytes)
    }
    write(repair.manifestPath, { ...repair.manifest, productionProfile: 'unrecognized-production' })
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /unrecognized native production profile/)
    writeText(repair.manifestPath, repairManifestBytes)
    const missingNormalized = structuredClone(repair.manifest)
    delete missingNormalized.sourceHashes
    write(repair.manifestPath, missingNormalized)
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /requires normalized source identity coverage/)
    writeText(repair.manifestPath, repairManifestBytes)
    const redirected = structuredClone(repair.manifest)
    redirected.sources.find(row => basename(row.path) === 'NativeParametricAssembly.cs').path = resolve(sourceFolder, 'NativeParametricAssembly.cs')
    write(repair.manifestPath, redirected)
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /native build source drift/)
    writeText(repair.manifestPath, repairManifestBytes)

    const protectedRepairSource = resolve(repair.directory, 'ParametricContext.cs')
    const protectedRepairBytes = readFileSync(protectedRepairSource)
    writeText(protectedRepairSource, Buffer.concat([protectedRepairBytes, Buffer.from('\nunauthorized change\n')]))
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), [doorPrecision2RepairTag, doorPrecision3RepairTag].includes(repairTag)
      ? /native build source drift: ParametricContext.cs/ : /changed a protected source: ParametricContext.cs/)
    writeText(protectedRepairSource, protectedRepairBytes)
    if (['readonly-open-20261105-r4', rearWidthRepairTag, rearGeometryRepairTag, doorPrecisionRepairTag, doorPrecision2RepairTag, doorPrecision3RepairTag].includes(repairTag)) for (const name of ['NativeParametricLockBase.cs', 'NativeParametricVerification.cs', 'NativeParametricEnvelope.cs']) {
      const protectedSource = resolve(repair.directory, name), bytes = readFileSync(protectedSource)
      writeText(protectedSource, Buffer.concat([bytes, Buffer.from('\nunauthorized repair source change\n')]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), new RegExp('changed a protected source: ' + name.replace('.', '\\.')))
      writeText(protectedSource, bytes)
    }
    if (!mutableNames.includes('NativeParametricPhysicalAudit.cs')) {
      const protectedAuditSource = resolve(repair.directory, 'NativeParametricPhysicalAudit.cs')
      const protectedAuditBytes = readFileSync(protectedAuditSource)
      writeText(protectedAuditSource, Buffer.concat([protectedAuditBytes, Buffer.from('\nunauthorized physical audit change\n')]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /changed a protected source: NativeParametricPhysicalAudit.cs/)
      writeText(protectedAuditSource, protectedAuditBytes)
    }
    if (!['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairTag)) for (const name of ['NativeParametricDoor.cs', 'NativeParametricWidth.cs']) {
      const protectedSource = resolve(repair.directory, name), bytes = readFileSync(protectedSource)
      writeText(protectedSource, Buffer.concat([bytes, Buffer.from('\nunauthorized process cleanup change\n')]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), new RegExp((repairTag === doorPrecision3RepairTag && name === 'NativeParametricWidth.cs' ? 'native build source drift: ' : 'changed a protected source: ') + name.replace('.', '\\.')))
      writeText(protectedSource, bytes)
    }
    if (!['readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairTag)) {
      const protectedLockSource = resolve(repair.directory, 'NativeParametricLocks.cs')
      const originalLocks = readFileSync(protectedLockSource)
      writeText(protectedLockSource, Buffer.concat([originalLocks, Buffer.from('\nunauthorized lock-session change\n')]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /changed a protected source: NativeParametricLocks.cs/)
      writeText(protectedLockSource, originalLocks)
    }
    for (const mutableName of mutableNames) {
      const mutableRepairSource = resolve(repair.directory, mutableName)
      const mutableRepairBytes = readFileSync(mutableRepairSource)
      writeText(mutableRepairSource, Buffer.concat([mutableRepairBytes, Buffer.from('\nsource drift\n')]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /native build source drift/)
      writeText(mutableRepairSource, mutableRepairBytes)
      const builtMutableSource = resolve(repair.generated, mutableName)
      writeText(builtMutableSource, Buffer.concat([Buffer.from('\uFEFF'), mutableRepairBytes]))
      assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /native repair build source hash mismatch/)
      writeText(builtMutableSource, mutableRepairBytes)
    }
    const extraRepairSource = resolve(repair.directory, 'unexpected.cs')
    writeText(extraRepairSource, 'unexpected repair source\n')
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /repair source identity coverage mismatch/)
    rmSync(extraRepairSource)
    rmSync(protectedRepairSource)
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /repair source identity coverage mismatch/)
    writeText(protectedRepairSource, protectedRepairBytes)
    const repairLink = resolve(root, 'linked-repair-source.cs')
    linkSync(protectedRepairSource, repairLink)
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /not an ordinary file/)
    rmSync(repairLink)
    const movedRepair = resolve(root, 'moved-repair-source')
    renameSync(repair.directory, movedRepair)
    symlinkSync(movedRepair, repair.directory, 'junction')
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /repair source refuses linked or external directories/)
    rmSync(repair.directory)
    renameSync(movedRepair, repair.directory)
    write(manifestPath, { ...manifest, changedBaseline: true })
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /source snapshot build manifest differs/)
    writeText(manifestPath, originalManifest)
    writeText(changedExecutable, 'baseline executable drift\n')
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /native executable no longer matches/)
    writeText(changedExecutable, readFileSync(acceptance.pinned))
    writeText(frozenSource, 'baseline source snapshot drift\n')
    assert.throws(() => verify16029ParametricBuild(repair.tag, repairOptions), /source snapshot hash mismatch/)
    writeText(frozenSource, originalFrozen)
    assert.equal(verify16029ParametricBuild(repair.tag, repairOptions).tag, repair.tag)
    assert.deepEqual(readFileSync(selectionPath), selectedBuildBytes)

  }

  console.log('parametric build selection: default preserved; explicit repair profiles bind 10 sources; runtime repair requires matching accepted executable, manifest, package and passing physical coverage; invalid selections and artifact drift rejected')
} finally {
  rmSync(root, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 })
}
