// Read-only postrun audit. This file writes only a new research report beside itself.
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { basename, dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { DERIVED_NAMES, ROOT, checkClone } from './run-stage-continuation.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const ATTEMPT = resolve(ROOT, 'data/parametric_attempts/PARAM-20261110095819500-614465')
const WORKING = resolve(ATTEMPT, 'native_cad/working_pack')
const DRYRUN = resolve(HERE, 'dry-run-20261110095448513-5596.json')
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b)
const stable = value => Array.isArray(value) ? value.map(stable) : value && typeof value === 'object' ? Object.fromEntries(Object.keys(value).sort().map(key => [key, stable(value[key])])) : value
const semanticHash = value => sha(JSON.stringify(stable(value)))
function file(path) { const stat = lstatSync(path); return { path, sha256: hash(path), size: stat.size, mtimeUtc: stat.mtime.toISOString(), isRegularSingleLink: stat.isFile() && !stat.isSymbolicLink() && stat.nlink === 1 } }
function walk(directory) { return readdirSync(directory, { withFileTypes: true }).flatMap(entry => { const path = resolve(directory, entry.name); if (entry.isSymbolicLink()) throw Error('Audit refuses linked input: ' + path); return entry.isDirectory() ? walk(path) : [file(path)] }) }
const auditStartedUtc = new Date().toISOString()
const bindingPaths = [DRYRUN, ...walk(ATTEMPT).filter(row => !/\.SLD(?:PRT|ASM)$/i.test(row.path)).map(row => row.path), resolve(HERE, 'run-stage-continuation.mjs'), fileURLToPath(import.meta.url)]
const bindingsBefore = [...new Set(bindingPaths)].map(file)
const plan = read(resolve(ATTEMPT, 'parametric_execution.json')), mappingProof = read(resolve(ATTEMPT, 'research/source-and-copy-mapping.json'))
const clone = read(resolve(ATTEMPT, 'research/clone-observation.json')), nativeClone = read(resolve(ATTEMPT, 'evidence/clone.json'))
const envelope = read(resolve(ATTEMPT, 'evidence/envelope.json')), processEvidence = read(resolve(ATTEMPT, 'evidence/envelope.process.json'))
const failure = read(resolve(ATTEMPT, 'research/failure.json')), protectedBefore = read(resolve(ATTEMPT, 'research/protected-before.json'))
const savedProtectedAfterPath = resolve(ATTEMPT, 'research/protected-after-failure.json')
const savedProtectedAfter = existsSync(savedProtectedAfterPath) ? read(savedProtectedAfterPath) : null
const rows = envelope.rows, failures = []
const requireCheck = (pass, code, details = null) => { if (!pass) failures.push({ code, details }); return pass }
const one = (predicate, label) => { const matches = rows.filter(predicate); requireCheck(matches.length === 1, 'EVIDENCE_ROW_COUNT', { label, count: matches.length }); return matches[0] }
const boxMax = (a, b) => Array.isArray(a) && Array.isArray(b) && a.length === 6 && b.length === 6 && a.every(Number.isFinite) && b.every(Number.isFinite) ? Math.max(...a.map((value, index) => Math.abs(value - b[index]))) : null
requireCheck(envelope.phase === 'envelope' && envelope.success === true && processEvidence.code === 0 && processEvidence.stderr === '' && processEvidence.cleanup?.verified === true, 'NATIVE_ENVELOPE_PROCESS_OR_EVIDENCE_FAILED')
const cloneBinding = { planMatches: clone.planSha256 === hash(resolve(ATTEMPT, 'parametric_execution.json')), evidenceMatches: clone.originalNativeEvidence.sha256 === hash(resolve(ATTEMPT, 'evidence/clone.json')), authorizationMatches: clone.authorizationSha256 === hash(resolve(ATTEMPT, 'authorizations/clone.json')), replayedObservation: null }
try { cloneBinding.replayedObservation = checkClone(nativeClone, mappingProof.files, mappingProof.frozenExpectedDependencies) } catch (error) { failures.push({ code: 'CLONE_RECHECK_FAILED', error: error.message }) }
requireCheck(cloneBinding.planMatches && cloneBinding.evidenceMatches && cloneBinding.authorizationMatches, 'CLONE_BINDING_FAILED')
const summaries = rows.filter(row => row.diagnosticOnly === true && Object.hasOwn(row, 'saved'))
requireCheck(summaries.length === 27, 'DERIVED_SUMMARY_COUNT', summaries.length)
const summaryChecks = DERIVED_NAMES.map(name => {
  const row = one(row => row.diagnosticOnly === true && row.file === name && Object.hasOwn(row, 'saved'), 'derived ' + name)
  if (!row) return { file: name, missing: true }
  const flagsPass = row.rebuilt === true && row.healthy === true && row.saved === true
  requireCheck(flagsPass, 'DERIVED_REBUILD_HEALTH_SAVE', name)
  requireCheck(Array.isArray(row.issues) && row.issues.length === 0, 'DERIVED_ISSUES', { file: name, issues: row.issues })
  requireCheck(Array.isArray(row.links), 'DERIVED_LINKS_NOT_ARRAY', name)
  for (const link of row.links || []) {
    if (link.blockedByBrokenReference === true) failures.push({ code: 'BLOCKED_BY_BROKEN_REFERENCE', file: name, link })
    const outsideReferences = (link.referenceFiles || []).filter(path => !mappingProof.files.some(file => file.destination.toLowerCase() === path.toLowerCase()))
    requireCheck(!outsideReferences.length, 'DERIVED_REFERENCE_OUTSIDE_EXACT_PACK', { file: name, outsideReferences })
  }
  return { file: name, rebuilt: row.rebuilt, healthy: row.healthy, saved: row.saved, issues: row.issues, links: row.links, beforeGeometry: row.beforeGeometry, afterGeometry: row.afterGeometry, flagsAndNoIssuesPass: flagsPass && row.issues?.length === 0 }
})
const boundaryRows = rows.filter(row => row.inspection === 'envelope_target_boundary')
requireCheck(boundaryRows.length === 26, 'BOUNDARY_COUNT', boundaryRows.length)
const boundaryChecks = DERIVED_NAMES.filter(name => name !== '箱体竖隔板R.SLDPRT').map(name => {
  const row = one(row => row.inspection === 'envelope_target_boundary' && row.file === name, 'boundary ' + name)
  const maxAbsoluteErrorM = boxMax(row?.afterGeometry?.part_box_m, row?.expectedBox)
  const pass = row?.healthy === true && maxAbsoluteErrorM !== null && maxAbsoluteErrorM < .0002
  requireCheck(pass, 'NATIVE_TARGET_BOUNDARY_FAILED', { file: name, maxAbsoluteErrorM })
  return { file: name, pass, healthy: row?.healthy, expectedBox: row?.expectedBox, observedBox: row?.afterGeometry?.part_box_m, maxAbsoluteErrorM, toleranceM: .0002, boundarySource: 'frozen native evidence expectedBox' }
})
const master = one(row => row.file === '标准寄存柜 模型.SLDPRT' && row.dimension === 'D2@草图1', 'master dimension')
const masterReopen = one(row => row.inspection === 'master_frame_pattern_saved_reopened', 'master reopen')
const cabinet = plan.contract.geometry.cabinet
const masterChecks = { targetMm: master?.targetMm, readbackMm: master?.readbackMm, contractCabinet: cabinet, rebuilt: master?.rebuilt, healthy: master?.healthy,
  heightReadbackPass: master?.targetMm === cabinet.heightMm && Math.abs(master.readbackMm - cabinet.heightMm) < .001,
  savedReopenRebuilt: masterReopen?.rebuilt, savedReopenBodyCountMatches: masterReopen?.snapshot?.body_count === master?.afterGeometry?.body_count,
  savedReopenBoxMaxErrorM: boxMax(masterReopen?.snapshot?.part_box_m, master?.afterGeometry?.part_box_m), savedReopenSnapshot: masterReopen?.snapshot,
  combinedHeightDepthGateReachedInPinnedNativeBuild: envelope.success === true, explicitDepthDimensionReadbackInJson: false,
  observedMasterBoxDepthExtentM: master?.afterGeometry?.part_box_m ? master.afterGeometry.part_box_m[5] - master.afterGeometry.part_box_m[2] : null }
requireCheck(masterChecks.heightReadbackPass && masterChecks.rebuilt === true && masterChecks.healthy === true, 'MASTER_DIMENSION_FAILED')
requireCheck(masterChecks.savedReopenRebuilt === true && masterChecks.savedReopenBodyCountMatches && masterChecks.savedReopenBoxMaxErrorM !== null && masterChecks.savedReopenBoxMaxErrorM < .000001, 'MASTER_REOPEN_FAILED')
const sideReopenChecks = ['箱体右侧板.sldprt', '箱体左侧板.sldprt'].map(name => {
  const reopened = one(row => row.inspection === 'rear_hole_saved_reopened' && row.file === name, 'side reopen ' + name)
  const summary = summaries.find(row => row.file === name)
  const maxBoxErrorM = boxMax(reopened?.snapshot?.part_box_m, summary?.afterGeometry?.part_box_m)
  const pass = maxBoxErrorM !== null && maxBoxErrorM < .000001 && reopened?.snapshot?.body_count === summary?.afterGeometry?.body_count && reopened?.snapshot?.error_feature_count === 0 && reopened?.snapshot?.warning_feature_count === 0
  requireCheck(pass, 'SIDE_REOPEN_FAILED', name)
  return { file: name, pass, maxBoxErrorM, snapshot: reopened?.snapshot }
})
const terminal = one(row => row.diagnosticOnly === false && row.deliveryEligible === false && row.targetHeightMm === cabinet.heightMm && row.targetDepthMm === cabinet.depthMm, 'envelope final target')
const workingBefore = walk(WORKING), currentRows = workingBefore.filter(row => /\.SLD(?:PRT|ASM)$/i.test(row.path)).map(row => ({ ...row, relativePath: relative(WORKING, row.path) }))
requireCheck(currentRows.length === 75 && workingBefore.length === 75 && currentRows.every(row => dirname(row.path) === WORKING), 'WORKING_INVENTORY_NOT_EXACT_FLAT75')
const allowedNames = new Set(['标准寄存柜 模型.SLDPRT', ...DERIVED_NAMES, '箱体横层板L焊接.SLDASM', '箱体横层板R焊接.SLDASM'].map(name => name.toLowerCase()))
const comparedToClone = clone.afterInventory.map(before => {
  const current = currentRows.find(row => row.path.toLowerCase() === before.path.toLowerCase())
  return { path: before.path, beforeSha256: before.sha256, currentSha256: current?.sha256 || null, changed: current?.sha256 !== before.sha256, allowedEnvelopeTarget: allowedNames.has(basename(before.path).toLowerCase()) }
})
const nonTargets = comparedToClone.filter(row => !row.allowedEnvelopeTarget), nonTargetChanges = nonTargets.filter(row => row.changed)
requireCheck(!nonTargetChanges.length, 'NON_ENVELOPE_TARGET_CHANGED', nonTargetChanges)
const currentProtection = protectedBefore.rows.map(before => {
  try { const now = file(before.path); return { path: before.path, beforeSha256: before.sha256, currentSha256: now.sha256, changed: now.sha256 !== before.sha256, isRegularSingleLink: now.isRegularSingleLink, currentMtimeUtc: now.mtimeUtc } }
  catch (error) { return { path: before.path, beforeSha256: before.sha256, currentSha256: null, changed: true, error: error.message } }
})
const protectionChanges = currentProtection.filter(row => row.changed)
requireCheck(!protectionChanges.length, 'PROTECTED_FILE_CURRENT_HASH_CHANGED', protectionChanges)
const portalBefore = read(resolve(ATTEMPT, 'research/portal-before.json')), dryRun = read(DRYRUN)
const portalProjection = row => ({ id: row.id, state: row.nativeBuild?.state ?? row.state, lease: row.nativeBuild ? row.nativeBuild.lease : row.lease, updatedAt: row.updatedAt, blocker: (row.nativeBuild ? row.nativeBuild.blocker : row.blocker) || null })
const portalCurrent = readdirSync(resolve(ROOT, 'data/native_model_requests')).filter(name => name.endsWith('.json')).flatMap(name => { const path = resolve(ROOT, 'data/native_model_requests', name), row = read(path); return row.id && row.nativeBuild ? [{ ...file(path), row }] : [] })
const portalChecks = portalBefore.records.map(before => {
  const current = portalCurrent.find(item => item.row.id === before.id), dry = dryRun.portalSnapshotReadOnly.find(row => row.id === before.id)
  const beforeProjection = portalProjection(before), currentProjection = current ? portalProjection(current.row) : null
  return { id: before.id, beforeProjection, currentProjection, beforeProjectionSha256: semanticHash(beforeProjection), currentProjectionSha256: currentProjection ? semanticHash(currentProjection) : null,
    projectedFieldsUnchanged: current ? same(stable(beforeProjection), stable(currentProjection)) : false, currentWholeFileSha256: current?.sha256 || null, currentPath: current?.path || null,
    dryRunProjectionMatchesRunBefore: dry ? same(stable(portalProjection(dry)), stable(beforeProjection)) : false, originalWholeFileSha256Available: false, causalAttributionOfAnyChange: 'not_established' }
})
const portalChanges = portalChecks.filter(row => !row.projectedFieldsUnchanged)
const bindingsAfter = bindingsBefore.map(before => ({ ...before, afterSha256: existsSync(before.path) ? hash(before.path) : null }))
const auditInputChanges = bindingsAfter.filter(row => row.sha256 !== row.afterSha256)
const workingAfter = currentRows.map(before => ({ path: before.path, beforeAuditSha256: before.sha256, afterAuditSha256: hash(before.path) }))
const cadChangedDuringAudit = workingAfter.filter(row => row.beforeAuditSha256 !== row.afterAuditSha256)
requireCheck(!auditInputChanges.length && !cadChangedDuringAudit.length, 'AUDIT_INPUTS_CHANGED_DURING_AUDIT', { auditInputChanges, cadChangedDuringAudit })
const processScript = "$ErrorActionPreference='Stop'; @{processes=@(Get-Process -Name SLDWORKS,sldProcMon,sldworks_fs -ErrorAction SilentlyContinue | ForEach-Object { @{pid=$_.Id;name=$_.ProcessName;startTicks=$_.StartTime.ToUniversalTime().Ticks.ToString();executable=$_.Path} })} | ConvertTo-Json -Depth 4 -Compress"
const processRead = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', processScript], { windowsHide: true, encoding: 'utf8', timeout: 10000 })
const processSnapshot = processRead.status === 0 ? JSON.parse(processRead.stdout) : { inspectionFailed: true, error: processRead.stderr || processRead.error?.message }
const onlyBrokenReferenceGate = failures.length === 1 && failures[0].code === 'BLOCKED_BY_BROKEN_REFERENCE' && failures[0].file === '箱体竖隔板R.SLDPRT'
const report = { schema: 'obsolete-door-stage-continuation-postrun-audit-v1', auditStartedUtc, auditFinishedUtc: new Date().toISOString(), purpose: 'read_only_research_audit_only', attempt: ATTEMPT,
  auditScript: file(fileURLToPath(import.meta.url)), nativeCadExecutedByAudit: false, attemptModifiedByAudit: false, originalRunnerModifiedByAudit: false,
  inputBindingsBeforeAfter: bindingsAfter, auditInputChanges, workingInventoryAfterEnvelopeAtAudit: currentRows, workingCadChangedDuringAudit: cadChangedDuringAudit,
  cloneBinding, nativeEnvelopeProcess: processEvidence, nativeEnvelopeSuccess: envelope.success, runnerFailure: failure,
  scopeConclusion: { onlyOneObservedGateFailureInRequestedChecks: onlyBrokenReferenceGate, observedFailures: failures, notes: ['This covers the specified research checks; it does not accept full geometry or future stages.', 'Any portal difference is reported without assigning cause to this run.'] },
  derivedSummaryCount: summaries.length, summaryChecks, nativeTargetBoundaryCount: boundaryRows.length, nativeTargetBoundaryChecks: boundaryChecks, masterChecks, sideReopenChecks, terminalTarget: terminal,
  afterCloneFileComparison: comparedToClone, nonEnvelopeTargetsChecked: nonTargets.length, nonEnvelopeTargetChanges: nonTargetChanges,
  protectedFileCount: protectedBefore.rows.length, currentProtectedFileChanges: protectionChanges, currentProtectionHashAudit: currentProtection,
  existingFailureProtectionReport: { exists: !!savedProtectedAfter, binding: savedProtectedAfter ? file(savedProtectedAfterPath) : null, reportedUnchanged: savedProtectedAfter?.unchanged, count: savedProtectedAfter?.rows?.length,
    historicalRecordedDifferences: savedProtectedAfter?.rows?.filter(row => row.beforeSha256 !== row.afterSha256 || row.error) || [] },
  portalAudit: { runBeforeBinding: file(resolve(ATTEMPT, 'research/portal-before.json')), dryRunBinding: file(DRYRUN), recordsChecked: portalChecks.length, changes: portalChanges, records: portalChecks,
    originalWholeTaskFileHashBaselineMissing: true, limitation: 'Dry-run and portal-before keep selected record fields, not whole-task-file SHA256. Selected-field semantic hashes can be compared; whole-file historical equality is unverified.',
    existingBlockedTaskFailuresResolved: false, changedPortalRecordsAttributedToThisRun: false },
  processSnapshot, warning32Unresolved: true, finiteBoundariesFullyCompared: false, completeColdReopenVerified: false, productionReceiptCreated: false, modelAccepted: false, modelReady: false, releaseEligible: false, downloadProduced: false }
const reportPath = resolve(HERE, 'postrun-audit-' + new Date().toISOString().replace(/[^0-9]/g, '') + '-' + process.pid + '.json')
writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' })
console.log(JSON.stringify({ reportPath, sha256: hash(reportPath), auditScriptSha256: hash(fileURLToPath(import.meta.url)), onlyOneObservedGateFailureInRequestedChecks: onlyBrokenReferenceGate, observedFailures: failures.map(row => ({ code: row.code, file: row.file })),
  derivedSummaries: summaries.length, boundaryCount: boundaryRows.length, nonEnvelopeTargetsChecked: nonTargets.length, nonEnvelopeTargetChanges: nonTargetChanges.length, protectedFileCount: protectedBefore.rows.length, protectedCurrentChanges: protectionChanges.length,
  portalProjectedFieldChanges: portalChanges.length, historicalWholeTaskFileShaAvailable: false, nativeCadExecutedByAudit: false, modelAccepted: false }, null, 2))
