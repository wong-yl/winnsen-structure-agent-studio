import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, readdirSync } from 'node:fs'
import { basename, dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from '../../workers/native_model_requests/parametric_v1/width_adapter.mjs'
import { adaptDoorSource, DOOR_SOURCE_PATH } from '../../workers/native_model_requests/parametric_v1/door_adapter.mjs'
import { adaptLockSource, LOCK_SOURCE_PATH } from '../../workers/native_model_requests/parametric_v1/lock_adapter.mjs'
import { compare16029PhysicalAudit, PHYSICAL_QUALITY_SCHEMA } from '../verify_16029_parametric_physical_quality.mjs'
import { current16029ProductionSources, PRODUCTION_PROCESS_VERSION } from '../../workers/native_model_requests/parametric_v1/production_source.mjs'
import { production16029Profile, verify16029ProductionEvidence } from './locker_16029_production_evidence.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SOURCE_FOLDER = 'workers/native_model_requests/parametric_v1'
const V35_AUDIT = resolve(ROOT, 'data/parametric_attempts/PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
const V35_AUDIT_SHA256 = '0C94B7F93014C2FAEC11151A63D2331D49B1983EB3E6C7A86281B56B2842B89F'
const nativeNames = ['ParametricContext.cs', 'NativeParametricAssembly.cs', 'NativeParametricVerification.cs', 'NativeParametricLocks.cs', 'NativeParametricPhysicalAudit.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricEnvelope.cs']
const buildSourceNames = ['NativeParametricWidth.cs', 'NativeParametricDoor.cs', 'NativeParametricLockBase.cs', ...nativeNames]
export const READONLY_OPEN_REPAIR_PROFILE = 'readonly-open-20261105-r1'
export const READONLY_OPEN_REPAIR_PROFILES = Object.freeze([READONLY_OPEN_REPAIR_PROFILE, 'readonly-open-20261105-r2', 'readonly-open-20261105-r3', 'readonly-open-20261105-r4', 'readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7', 'readonly-open-20261109-r4-rearwidth1'])
const REPAIR_BASELINE_TAG = 'engineer-feedback-20261008-r4'
const repairMutableSources = new Set(['NativeParametricAssembly.cs', 'NativeParametricDoorPlacement.cs'])
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const SHA256 = /^[A-F0-9]{64}$/
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const readJson = path => JSON.parse(readFileSync(path, 'utf8'))
const isCad = path => !basename(path).startsWith('~$') && /\.SLD(PRT|ASM)$/i.test(path)
const inside = (root, path) => {
  const fullRoot = resolve(root), fullPath = resolve(path), rel = relative(fullRoot, fullPath)
  return Boolean(rel && !rel.startsWith('..') && !rel.includes('..' + '\\') && !rel.includes('../'))
}
const assertRegularFile = (path, label) => {
  requireValue(existsSync(path), label + ' is missing')
  const stat = lstatSync(path)
  requireValue(stat.isFile() && stat.nlink === 1, label + ' is not an ordinary file')
  return sha(readFileSync(path))
}
export const normalizedNativeSourceHash = text => sha(text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n'))

function verifyProtectedSources(root, attempt) {
  const path = resolve(attempt, 'protected_sources.before.json')
  const rows = readJson(path)
  requireValue(Array.isArray(rows) && rows.length > 0, 'protected source evidence is missing')
  const seen = new Set()
  for (const row of rows) {
    requireValue(typeof row?.path === 'string' && SHA256.test(String(row.sha256).toUpperCase()) && inside(root, row.path), 'protected source evidence row is invalid')
    const pathKey = resolve(row.path).toLowerCase()
    requireValue(!seen.has(pathKey), 'protected source evidence contains duplicate paths')
    seen.add(pathKey)
    requireValue(assertRegularFile(row.path, 'protected source ' + row.path) === String(row.sha256).toUpperCase(), 'protected source hash changed: ' + row.path)
  }
}

function verifyPackageInventory(attempt, evidence) {
  requireValue(evidence?.phase === 'package' && evidence.success === true && Array.isArray(evidence.rows) && evidence.rows.length > 0, 'package evidence is incomplete')
  const packageDir = resolve(attempt, 'package')
  const rows = evidence.rows
  const seen = new Set()
  for (const row of rows) {
    requireValue(typeof row?.path === 'string' && SHA256.test(String(row.sha256).toUpperCase()) && isCad(row.path), 'package evidence row is invalid')
    const path = resolve(row.path)
    requireValue(inside(packageDir, path) && dirname(path) === packageDir, 'package evidence path leaves package directory')
    const key = basename(path).toLowerCase()
    requireValue(!seen.has(key), 'package evidence contains duplicate files')
    seen.add(key)
    requireValue(assertRegularFile(path, 'package file ' + path) === String(row.sha256).toUpperCase(), 'package file hash changed: ' + path)
  }
  const actual = readdirSync(packageDir, { withFileTypes: true }).filter(entry => entry.isFile() && isCad(entry.name))
  requireValue(actual.length === rows.length && actual.every(entry => seen.has(entry.name.toLowerCase())), 'package file inventory differs from package evidence')
}

function verifyPhysicalAcceptance(root, attempt, quality, selection, packageEvidence, packageVerificationEvidence, auditPath, baselinePathOverride = '') {
  requireValue(quality.schema === PHYSICAL_QUALITY_SCHEMA && quality.pass === true && quality.coverageVerified === true, 'native default build physical coverage has not passed')
  requireValue(quality.candidateEvidenceSha256 === assertRegularFile(auditPath, 'physical audit evidence'), 'physical quality does not bind physical audit evidence')
  requireValue(packageEvidence?.phase === 'package' && packageEvidence.success === true && packageVerificationEvidence?.phase === 'verify-package' && packageVerificationEvidence.success === true, 'package acceptance evidence is incomplete')
   const baselinePath = resolve(String(baselinePathOverride || quality.baselinePath || ''))
   if (baselinePathOverride) {
     requireValue(inside(root, baselinePath), 'test physical baseline leaves the repository root')
     requireValue(SHA256.test(String(quality.baselineSha256 || '').toUpperCase()) && assertRegularFile(baselinePath, 'test physical baseline') === String(quality.baselineSha256).toUpperCase(), 'test physical baseline hash changed')
   } else {
     requireValue(baselinePath === V35_AUDIT && inside(root, baselinePath), 'unrecognized physical baseline')
     requireValue(String(quality.baselineSha256 || '').toUpperCase() === V35_AUDIT_SHA256 && assertRegularFile(baselinePath, 'physical baseline') === V35_AUDIT_SHA256, 'physical baseline hash changed')
   }
  const baseline = readJson(baselinePath)
  const candidate = readJson(auditPath)
  requireValue(candidate.phase === 'physical-audit' && candidate.success === true, 'physical audit evidence did not pass')
  const productionProfile = production16029Profile(readJson(resolve(attempt, 'parametric_execution.json')).contract?.input)
  requireValue((selection.productionProfile || '') === productionProfile, 'selected build production profile differs from its accepted task')
  const measured = compare16029PhysicalAudit(baseline, candidate, { doorCount: quality.doorCount, packageInventory: packageEvidence.rows, packageVerification: packageVerificationEvidence, ...(productionProfile ? { productionEvidence: verify16029ProductionEvidence(attempt) } : {}) })
  for (const key of ['schema', 'measuredHealthPass', 'doorCount', 'referenceContactCount', 'contactCount', 'comparedContactCategories', 'unexpected', 'flatPatternCount', 'flatErrors', 'boundary', 'coverageVerified', 'coverageStatus', 'coverageErrors', 'expectedFlatPatternCount', 'bodyCount', 'expectedCheckedPairs', 'excludedReferenceInstanceCount', 'pass']) {
    requireValue(JSON.stringify(quality[key]) === JSON.stringify(measured[key]), 'physical quality measurement changed: ' + key)
  }
  requireValue(quality.packageEvidenceSha256 === assertRegularFile(resolve(attempt, 'evidence/package.json'), 'package evidence'), 'physical quality does not bind package evidence')
  requireValue(quality.packageVerificationEvidenceSha256 === assertRegularFile(resolve(attempt, 'evidence/verify-package.json'), 'package verification evidence'), 'physical quality does not bind package verification evidence')
  const receiptPath = resolve(attempt, 'receipts/physical-audit.json')
  const authorizationPath = resolve(attempt, 'authorizations/physical-audit.json')
  const planPath = resolve(attempt, 'parametric_execution.json')
  const receipt = readJson(receiptPath), authorization = readJson(authorizationPath), plan = readJson(planPath)
  const receiptSha = assertRegularFile(receiptPath, 'physical audit receipt')
  const authorizationSha = assertRegularFile(authorizationPath, 'physical audit authorization')
  requireValue(plan.schema === 'winnsen.locker16029.parametric_execution.v1' && typeof plan.taskId === 'string' && plan.taskId.length > 0, 'physical acceptance plan is invalid')
  requireValue(receipt.phase === 'physical-audit' && receipt.taskId === plan.taskId && receipt.planSha256 === assertRegularFile(planPath, 'physical acceptance plan'), 'physical audit receipt plan binding failed')
  requireValue(receipt.evidenceSha256 === quality.candidateEvidenceSha256 && receipt.evidenceSha256 === assertRegularFile(auditPath, 'physical audit evidence') && receipt.authorizationSha256 === authorizationSha, 'physical audit receipt evidence binding failed')
  requireValue(receipt.protectedSourcesUnchanged === true && authorization.phase === 'physical-audit' && authorization.taskId === plan.taskId && authorization.planSha256 === receipt.planSha256, 'physical audit authorization binding failed')
  requireValue(authorization.executableSha256 === selection.executableSha256 && receiptSha && authorizationSha, 'physical audit executable authorization binding failed')
  verifyProtectedSources(root, attempt)
}

export function current16029ParametricBuildSources(root = ROOT) {
  return new Map([
    ['NativeParametricWidth.cs', adaptWidthSource(readFileSync(resolve(root, WIDTH_SOURCE_PATH), 'utf8'))],
    ['NativeParametricDoor.cs', adaptDoorSource(readFileSync(resolve(root, DOOR_SOURCE_PATH), 'utf8'), 'A07174DEDF4F9F0958A731E679655B742E53BEDA063709F5F2F10BF2376572B0')],
    ['NativeParametricLockBase.cs', adaptLockSource(readFileSync(resolve(root, LOCK_SOURCE_PATH), 'utf8'))],
    ...nativeNames.map(name => [name, readFileSync(resolve(root, SOURCE_FOLDER, name), 'utf8')]),
  ])
}

export function resolve16029ParametricBuildTag(buildTag, { root = ROOT, baselinePathOverride = '' } = {}) {
  if (!buildTag) {
    const selectionPath = resolve(root, SOURCE_FOLDER, 'build-selection.json')
    if (!existsSync(selectionPath)) throw new Error('native default build has not been verified and selected; provide buildTag explicitly')
    const selection = readJson(selectionPath)
    if (selection.schema !== 'winnsen.locker16029.native_build_selection.v1' || selection.verified !== true || !SHA256.test(String(selection.executableSha256 || '').toUpperCase()) || !/^[A-Za-z0-9_-]+$/.test(String(selection.acceptanceAttempt || '')) || !SHA256.test(String(selection.acceptanceQualitySha256 || '').toUpperCase())) throw new Error('native default build selection is not backed by physical acceptance')
    const attempt = resolve(root, 'data/parametric_attempts', selection.acceptanceAttempt)
    requireValue(dirname(attempt) === resolve(root, 'data/parametric_attempts'), 'native default build attempt path is invalid')
    const qualityPath = resolve(attempt, 'evidence/physical-quality.json')
    const qualitySha = assertRegularFile(qualityPath, 'native default build acceptance evidence')
    if (qualitySha !== String(selection.acceptanceQualitySha256).toUpperCase()) throw new Error('native default build acceptance evidence changed')
    const quality = readJson(qualityPath)
    const auditPath = resolve(attempt, 'evidence/physical-audit.json')
    const packageEvidencePath = resolve(attempt, 'evidence/package.json')
    const packageVerificationEvidencePath = resolve(attempt, 'evidence/verify-package.json')
    const packageEvidence = readJson(packageEvidencePath)
    const packageVerificationEvidence = readJson(packageVerificationEvidencePath)
    verifyPackageInventory(attempt, packageEvidence)
     verifyPhysicalAcceptance(root, attempt, quality, selection, packageEvidence, packageVerificationEvidence, auditPath, baselinePathOverride)
    buildTag = selection.buildTag
    if (typeof buildTag !== 'string' || !/^[a-zA-Z0-9_-]+$/.test(buildTag)) throw new Error('invalid selected native build tag')
    const pinnedExecutable = resolve(attempt, 'tools/NativeParametricAssembly.exe')
    const selectedExecutable = resolve(root, SOURCE_FOLDER, 'bin-' + buildTag, 'NativeParametricAssembly.exe')
    if (assertRegularFile(pinnedExecutable, 'physical acceptance executable') !== String(selection.executableSha256).toUpperCase() || assertRegularFile(selectedExecutable, 'selected native executable') !== String(selection.executableSha256).toUpperCase()) throw new Error('selected native executable changed')
  }
  if (typeof buildTag !== 'string' || !/^[a-zA-Z0-9_-]+$/.test(buildTag)) throw new Error('invalid native build tag')
  return buildTag
}

function acceptedSourceSnapshot(tag, folder, manifest, { root, baselinePathOverride }) {
  const snapshot = resolve(folder, 'source-snapshot')
  if (!existsSync(snapshot)) return null
  requireValue(!manifest.productionProfile, 'source snapshot is only supported for an accepted non-production build')
  requireValue(resolve16029ParametricBuildTag(undefined, { root, baselinePathOverride }) === tag, 'source snapshot must belong to the accepted default build')
  const selection = readJson(resolve(root, SOURCE_FOLDER, 'build-selection.json'))
  const acceptedManifest = resolve(root, 'data/parametric_attempts', selection.acceptanceAttempt, 'tools/build.json')
  const manifestSha256 = assertRegularFile(resolve(folder, 'build.json'), 'selected build manifest')
  requireValue(assertRegularFile(acceptedManifest, 'accepted build manifest') === manifestSha256, 'source snapshot build manifest differs from the accepted manifest')
  for (let path = snapshot; path !== resolve(root); path = dirname(path)) {
    requireValue(inside(root, path) && lstatSync(path).isDirectory() && !lstatSync(path).isSymbolicLink(), 'source snapshot refuses linked or external directories')
  }
  const files = readdirSync(snapshot)
  requireValue(files.length === buildSourceNames.length && buildSourceNames.every(name => files.includes(name)), 'source snapshot identity coverage mismatch')
  const expected = new Map()
  const sources = buildSourceNames.map(name => {
    const rows = (manifest.sources || []).filter(row => basename(row.path) === name)
    requireValue(rows.length === 1 && SHA256.test(String(rows[0].sha256)), 'source snapshot manifest identity mismatch: ' + name)
    const path = resolve(snapshot, name)
    const sourceSha256 = assertRegularFile(path, 'source snapshot ' + name)
    requireValue(sourceSha256 === rows[0].sha256, 'source snapshot hash mismatch: ' + name)
    expected.set(name, readFileSync(path, 'utf8'))
    return { name, path, sha256: sourceSha256 }
  })
  return { expected, provenance: { folder: snapshot, acceptanceAttempt: selection.acceptanceAttempt, manifestSha256, sources } }
}

function lockBaseSaveGuardsOnly(path, baselinePath) {
  const normalize = text => text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
  const text = normalize(readFileSync(path, 'utf8'))
  const restored = text.replace('Winnsen.StructureAgent.Generation.NativeParametricAssembly.GuardedSave3(model, ', 'model.Save3(')
    .replace('Winnsen.StructureAgent.Generation.NativeParametricAssembly.GuardedSaveAs(temporaryRight, ', 'temporaryRight.Extension.SaveAs(')
  return restored !== text && restored === normalize(readFileSync(baselinePath, 'utf8'))
}
function locksSaveGuardsOnly(path, baselinePath) {
  const normalize = text => text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
  const text = normalize(readFileSync(path, 'utf8'))
  const restored = text.replace('NativeParametricAssembly.GuardedSaveAs(model,rightSource,', 'model.Extension.SaveAs(rightSource,')
    .replace('NativeParametricAssembly.GuardedSaveAs(mirrored,built,', 'mirrored.Extension.SaveAs(built,')
  return restored !== text && restored === normalize(readFileSync(baselinePath, 'utf8'))
}
function auxiliarySaveGuardOnly(path, baselinePath) {
  const normalize = text => text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
  const text = normalize(readFileSync(path, 'utf8'))
  const restored = text.replace('GuardedAuxiliarySave("SavePackAndGo", output, delegate { return model.Extension.SavePackAndGo(pack); })', 'model.Extension.SavePackAndGo(pack)')
    .replace('(bool)NativeParametricAssembly.GuardedAuxiliarySave("SketchBlockDefinition.Save",seed.path,delegate { return instance.Definition.Save(seed.path); })', 'instance.Definition.Save(seed.path)')
  return restored !== text && restored === normalize(readFileSync(baselinePath, 'utf8'))
}

function repairSourceIdentity(tag, folder, manifest, { root, baselinePathOverride, repairProfile }) {
  requireValue(READONLY_OPEN_REPAIR_PROFILES.includes(repairProfile) && manifest.repairProfile === repairProfile,
    'native repair build requires its explicit recognized repair profile')
  requireValue(tag === repairProfile && !manifest.productionProfile && !existsSync(resolve(folder, 'source-snapshot')),
    'native repair build identity is invalid')
  const provenance = manifest.repairProvenance
  const sourceDirectory = resolve(root, SOURCE_FOLDER, 'repairs', repairProfile, 'source')
  requireValue(provenance?.schema === 'winnsen.locker16029.parametric_repair_source.v1' &&
    provenance.baselineBuildTag === REPAIR_BASELINE_TAG && typeof provenance.sourceDirectory === 'string' &&
    resolve(provenance.sourceDirectory) === sourceDirectory,
  'native repair source provenance is invalid')
  const baseline = verify16029ParametricBuild(REPAIR_BASELINE_TAG, { root, baselinePathOverride })
  requireValue(baseline.sourceSnapshot && provenance.baselineManifestSha256 === baseline.sourceSnapshot.manifestSha256 &&
    provenance.baselineExecutableSha256 === baseline.manifest.executableSha256,
  'native repair baseline identity mismatch')
  for (const directory of [folder, resolve(root, SOURCE_FOLDER, 'build-' + tag), sourceDirectory]) {
    for (let path = directory; path !== resolve(root); path = dirname(path)) {
      requireValue(inside(root, path) && lstatSync(path).isDirectory() && !lstatSync(path).isSymbolicLink(),
        'native repair source refuses linked or external directories')
    }
  }
  assertRegularFile(resolve(folder, 'build.json'), 'native repair manifest')
  assertRegularFile(resolve(folder, 'NativeParametricAssembly.exe'), 'native repair executable')
  const files = readdirSync(sourceDirectory)
  requireValue(files.length === buildSourceNames.length && buildSourceNames.every(name => files.includes(name)),
    'native repair source identity coverage mismatch')
  const expected = new Map()
  const sources = buildSourceNames.map(name => {
    const path = resolve(sourceDirectory, name)
    const sourceSha256 = assertRegularFile(path, 'native repair source ' + name)
    const baselineSource = baseline.sourceSnapshot.sources.find(row => row.name === name)
    requireValue(repairMutableSources.has(name) ||
      (['readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && name === 'NativeParametricLocks.cs') ||
      (['readonly-open-20261105-r4', 'readonly-open-20261109-r4-rearwidth1', 'readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && name === 'NativeParametricPhysicalAudit.cs') ||
      (['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && ['NativeParametricDoor.cs', 'NativeParametricWidth.cs'].includes(name)) ||
      (['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && name === 'NativeParametricLockBase.cs' && lockBaseSaveGuardsOnly(path, baselineSource.path)) ||
      (['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && name === 'NativeParametricLocks.cs' && locksSaveGuardsOnly(path, baselineSource.path)) ||
      (['readonly-open-20261105-r5', 'readonly-open-20261105-r5-monitor1', 'readonly-open-20261105-r5-locksession1', 'readonly-open-20261105-r5-lockcontext2', 'readonly-open-20261105-r5-lockconfig3', 'readonly-open-20261105-r5-lockreadonly4', 'readonly-open-20261105-r5-locksettle5', 'readonly-open-20261107-r5-ownerreplace6', 'readonly-open-20261107-r5-ownerreplace6b', 'readonly-open-20261107-r5-framesession7'].includes(repairProfile) && ['NativeParametricVerification.cs', 'NativeParametricEnvelope.cs'].includes(name) && auxiliarySaveGuardOnly(path, baselineSource.path)) ||
      sourceSha256 === baselineSource.sha256,
      'native repair changed a protected source: ' + name)
    expected.set(name, readFileSync(path, 'utf8'))
    return { name, path, sha256: sourceSha256, baselineSha256: baselineSource.sha256 }
  })
  return { expected, provenance: { ...provenance, sources } }
}

export function verify16029ParametricBuild(buildTag, { root = ROOT, baselinePathOverride = '', repairProfile = '' } = {}) {
  requireValue(!repairProfile || Boolean(buildTag), 'native repair build requires an explicit build tag')
  const tag = resolve16029ParametricBuildTag(buildTag, { root, baselinePathOverride })
  const folder = resolve(root, SOURCE_FOLDER, 'bin-' + tag)
  const manifest = JSON.parse(readFileSync(resolve(folder, 'build.json'), 'utf8'))
  const executable = resolve(folder, 'NativeParametricAssembly.exe')
  if (resolve(manifest.executable) !== executable || sha(readFileSync(executable)) !== manifest.executableSha256) throw new Error('native executable no longer matches the selected build manifest')
  const production = manifest.productionProfile === PRODUCTION_PROCESS_VERSION
  if (manifest.productionProfile && !production) throw new Error('unrecognized native production profile')
  const repair = repairProfile || manifest.repairProfile || manifest.repairProvenance
    ? repairSourceIdentity(tag, folder, manifest, { root, baselinePathOverride, repairProfile }) : null
  const snapshot = repair ? null : acceptedSourceSnapshot(tag, folder, manifest, { root, baselinePathOverride })
  const expected = repair?.expected || snapshot?.expected || (production ? current16029ProductionSources(root) : current16029ParametricBuildSources(root))
  if (!Array.isArray(manifest.sources) || manifest.sources.length !== expected.size || new Set(manifest.sources.map(row => basename(row.path))).size !== expected.size) throw new Error('native build source identity coverage mismatch')
  const hasNormalized = manifest.sourceHashes !== undefined
  if (repair && !hasNormalized) throw new Error('native repair build requires normalized source identity coverage')
  if (hasNormalized && (!Array.isArray(manifest.sourceHashes) || manifest.sourceHashes.length !== expected.size || new Set(manifest.sourceHashes.map(row => row.name)).size !== expected.size)) throw new Error('native manifest sourceHashes identity mismatch')
  const normalized = new Map((manifest.sourceHashes || []).map(row => [row.name, row.normalizedSha256]))
  for (const row of manifest.sources) {
    const name = basename(row.path), text = expected.get(name)
    if (text === undefined) throw new Error('native build source identity changed: ' + name)
    const sourcePath = snapshot ? resolve(snapshot.provenance.folder, name) : row.path
    const builtText = readFileSync(sourcePath, 'utf8')
    const expectedSourcePath = resolve(root, SOURCE_FOLDER, !repair && !production && nativeNames.includes(name) ? name : 'build-' + tag + '/' + name)
    if (resolve(row.path) !== expectedSourcePath || normalizedNativeSourceHash(text) !== normalizedNativeSourceHash(builtText)) throw new Error('native build source drift: ' + name)
    const actual = sha(readFileSync(sourcePath))
    if (repair && (assertRegularFile(sourcePath, 'native repair build source ' + name) !== row.sha256 ||
        actual !== repair.provenance.sources.find(source => source.name === name).sha256)) throw new Error('native repair build source hash mismatch: ' + name)
    if (hasNormalized) {
      if (normalized.get(name) !== normalizedNativeSourceHash(text) || row.normalizedSha256 !== normalized.get(name)) throw new Error('native build sourceHashes drift: ' + name)
    } else if (actual !== row.sha256) throw new Error('native build source hash mismatch: ' + name)
  }
  return { tag, folder, manifest, executable, ...(snapshot ? { sourceSnapshot: snapshot.provenance } : {}),
    ...(repair ? { repairSource: repair.provenance } : {}) }
}

export function read16029ParametricRuntimeRepair({ root = ROOT, baselinePathOverride = '' } = {}) {
  const selectionPath = resolve(root, SOURCE_FOLDER, 'runtime-repair-selection.json')
  if (!existsSync(selectionPath)) return {}
  assertRegularFile(selectionPath, 'runtime repair selection')
  const selection = readJson(selectionPath)
  requireValue(selection.schema === 'winnsen.locker16029.parametric_runtime_repair.v1' && selection.verified === true &&
    READONLY_OPEN_REPAIR_PROFILES.includes(selection.buildTag) && selection.repairProfile === selection.buildTag &&
    /^[A-Za-z0-9_-]+$/.test(String(selection.acceptanceAttempt || '')) &&
    SHA256.test(String(selection.acceptanceQualitySha256 || '')) && SHA256.test(String(selection.executableSha256 || '')),
  'runtime repair selection requires an accepted recognized repair build')
  const build = verify16029ParametricBuild(selection.buildTag, { root, baselinePathOverride, repairProfile: selection.repairProfile })
  requireValue(build.manifest.executableSha256 === selection.executableSha256, 'runtime repair executable identity mismatch')
  const attempt = resolve(root, 'data/parametric_attempts', selection.acceptanceAttempt)
  const qualityPath = resolve(attempt, 'evidence/physical-quality.json')
  requireValue(assertRegularFile(qualityPath, 'runtime repair acceptance evidence') === selection.acceptanceQualitySha256,
    'runtime repair acceptance evidence changed')
  requireValue(assertRegularFile(resolve(attempt, 'tools/NativeParametricAssembly.exe'), 'accepted runtime repair executable') === selection.executableSha256 &&
    assertRegularFile(resolve(attempt, 'tools/build.json'), 'accepted runtime repair manifest') === assertRegularFile(resolve(build.folder, 'build.json'), 'runtime repair manifest'),
  'runtime repair build differs from its accepted attempt')
  const quality = readJson(qualityPath), packageEvidence = readJson(resolve(attempt, 'evidence/package.json'))
  const packageVerification = readJson(resolve(attempt, 'evidence/verify-package.json'))
  verifyPackageInventory(attempt, packageEvidence)
  verifyPhysicalAcceptance(root, attempt, quality, selection, packageEvidence, packageVerification,
    resolve(attempt, 'evidence/physical-audit.json'), baselinePathOverride)
  return { buildTag: selection.buildTag, repairProfile: selection.repairProfile }
}
