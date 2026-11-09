import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { closeSync, cpSync, mkdirSync, mkdtempSync, readFileSync, readSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, relative, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
import { compare16029PhysicalAudit } from './verify_16029_parametric_physical_quality.mjs'
import { build16029PhysicalAuditFixture } from './fixtures/locker_16029_physical_audit.mjs'
import { inventory16029ProductionFiles, verify16029ProductionEvidence } from './lib/locker_16029_production_evidence.mjs'

const isolated = mkdtempSync(resolve(tmpdir(), '16029-publication-'))
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const write = (path, value) => { mkdirSync(resolve(path, '..'), { recursive: true }); writeFileSync(path, JSON.stringify(value, null, 2) + '\n') }
const copiedModules = new Set(['tools/lib/locker_16029_parametric_publication.mjs'])
function copyModule(path) {
  const source = relative(resolve('.'), resolve(path)).replaceAll('\\', '/')
  if (copiedModules.has(source)) return
  copiedModules.add(source)
  const text = readFileSync(source, 'utf8'), target = resolve(isolated, source)
  mkdirSync(dirname(target), { recursive: true }); writeFileSync(target, text)
  for (const match of text.matchAll(/\bfrom\s+['"](\.[^'"]+)['"]|\bimport\s+['"](\.[^'"]+)['"]/g)) copyModule(resolve(dirname(source), match[1] || match[2]))
}
try {
  const fixture = build16029PhysicalAuditFixture()
  const base = resolve(isolated, 'data/parametric_attempts'), attempt = resolve(base, 'PARAM-fixture')
  const baselinePath = resolve(base, 'PARAM-20260905130307482-41DE53/evidence/physical-audit.json')
  write(baselinePath, fixture.reference)
  mkdirSync(resolve(isolated, 'tools/lib'), { recursive: true })
  for (const path of ['tools/verify_16029_parametric_physical_quality.mjs', 'tools/lib/locker_16029_parametric_contract.mjs', 'tools/lib/locker_16029_production_evidence.mjs', 'tools/lib/locker_16029_production_profile.mjs']) cpSync(path, resolve(isolated, path))
  for (const directory of ['package', 'unzipped_native_model', 'tools']) mkdirSync(resolve(attempt, directory), { recursive: true })
  const request = { cabinet: { widthMm: 850, heightMm: 1917, depthMm: 550 }, columns: ['L', 'R'].map(side => ({ side, doors: [1, 1, 1].map(heightUnits => ({ heightUnits })) })) }
  const contract = build16029ParametricContract(request)
  const plan = { schema: 'winnsen.locker16029.parametric_execution.v1', taskId: 'PARAM-fixture', contract }
  const planPath = resolve(attempt, 'parametric_execution.json'); write(planPath, plan)
  const executable = resolve(attempt, 'tools/NativeParametricAssembly.exe'); writeFileSync(executable, 'synthetic executable; never run\n')
  const inventory = fixture.options.packageInventory.map(row => {
    const file = row.path.split('/').at(-1), path = resolve(attempt, 'package', file)
    writeFileSync(path, fixture.content(file)); cpSync(path, resolve(attempt, 'unzipped_native_model', file))
    return { path, sha256: hash(path) }
  })
  const packageVerification = fixture.options.packageVerification
  const measured = { actualDoors: 6, actualTongues: 6, featureIssueCount: 0, measuredWidthMm: 850,
    tonguePositionsMm: ['L', 'R'].flatMap(side => contract.rowsByColumn[side].map(row => [side === 'L' ? -55 : 55, row.centerYmm, -11.3])) }
  packageVerification.rows.push(measured)
  const archivePath = resolve(attempt, 'native_model.zip'); writeFileSync(archivePath, 'synthetic archive fixture\n')
  const relocation = { archive: archivePath, sha256: hash(archivePath), cadFileCount: inventory.length, extractedFilesByteIdentical: true }
  const relocated = structuredClone(packageVerification); relocated.phase = 'verify-relocated'; relocated.rows.push(relocation)
  const evidenceByPhase = { package: { success: true, rows: inventory }, 'verify-package': packageVerification, 'verify-relocated': relocated, 'physical-audit': fixture.candidate }
  for (const phase of ['dimensions', 'derived', 'base-hole', 'assemblies', 'door-603', 'locks-verify', 'root', 'package', 'verify-package', 'verify-relocated', 'physical-audit']) {
    const evidence = evidenceByPhase[phase] || { success: true, rows: [] }, evidencePath = resolve(attempt, 'evidence', phase + '.json')
    write(evidencePath, evidence)
    const authorizationPath = resolve(attempt, 'authorizations', phase + '.json')
    write(authorizationPath, { taskId: plan.taskId, planSha256: hash(planPath), executableSha256: hash(executable) })
    write(resolve(attempt, 'receipts', phase + '.json'), { phase, taskId: plan.taskId, planSha256: hash(planPath), protectedSourcesUnchanged: true, authorizationSha256: hash(authorizationPath), evidenceSha256: hash(evidencePath), predecessor: '' })
  }
  write(resolve(attempt, 'protected_sources.before.json'), [])
  const qualityPath = resolve(attempt, 'evidence/physical-quality.json')
  const quality = compare16029PhysicalAudit(fixture.reference, fixture.candidate, { ...fixture.options, packageInventory: inventory })
  write(qualityPath, { ...quality, baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(resolve(attempt, 'evidence/physical-audit.json')), packageEvidenceSha256: hash(resolve(attempt, 'evidence/package.json')), packageVerificationEvidenceSha256: hash(resolve(attempt, 'evidence/verify-package.json')) })
  const publicationSource = readFileSync('tools/lib/locker_16029_parametric_publication.mjs', 'utf8')
    .replace('0C94B7F93014C2FAEC11151A63D2331D49B1983EB3E6C7A86281B56B2842B89F', hash(baselinePath))
    .replace('PARAM-20261003090451084-BB5B30', 'PARAM-fixture')
    .replace('D6961B92B9BFD4DD14F4B249C6F89DB2B6BE96625E8C4B12D643203621D7621E', relocation.sha256)
  writeFileSync(resolve(isolated, 'tools/lib/locker_16029_parametric_publication.mjs'), publicationSource)
  const { validate16029ParametricPublication, validate16029ParametricCandidateReuse, inspect16029ParametricTaskPublication, open16029ParametricTaskArchive } = await import(pathToFileURL(resolve(isolated, 'tools/lib/locker_16029_parametric_publication.mjs')))
  const valid = validate16029ParametricPublication(attempt, { expectedRequest: request })
  assert.equal(valid.ok, true, valid.reason)
  assert.equal(valid.coverageVerified, true)
  assert.equal(valid.quality.pass, true)
  assert.equal(valid.quality.expectedFlatPatternCount, 2)
  assert.equal(validate16029ParametricPublication(attempt, { expectedRequest: { ...request, cabinet: { ...request.cabinet, widthMm: 913 } } }).ok, false)
  cpSync('tools/lib/locker_16029_native_task_store.mjs', resolve(isolated, 'tools/lib/locker_16029_native_task_store.mjs'))
  const { createNativeTaskStore } = await import(pathToFileURL(resolve(isolated, 'tools/lib/locker_16029_native_task_store.mjs')))
  const store = createNativeTaskStore({ dataDir: resolve(isolated, 'task-store') })
  const previousResult = { schema: 'winnsen.locker16029.parametric_publication.v1', archiveSha256: 'A'.repeat(64), attempt: 'preserved-original-attempt' }
  const oldTask = await store.createOrImport({ id: 'NATIVE-revision-test', username: 'engineer-a',
    createdAt: '2026-10-08T01:00:00.000Z', requestFingerprint: 'revision-fixture', parametricRequest: request,
    status: 'native_assistance_model_ready', taskType: 'native_solidworks_build_task',
    nativeBuild: { state: 'ready', modelReady: true, executionStarted: true, result: previousResult } })
  const revisionArgs = { taskId: oldTask.id, username: oldTask.username, expectedRevision: oldTask.revision,
    transitionId: 'revision-tested', expectedArchiveSha256: previousResult.archiveSha256,
    candidateAttempt: attempt, reason: 'Three structural corrections; retain original result for review.' }
  await assert.rejects(store.publishParametricRevision({ ...revisionArgs, username: 'another-owner' }), error => error.code === 'TASK_NOT_FOUND')
  await assert.rejects(store.publishParametricRevision({ ...revisionArgs, expectedRevision: oldTask.revision + 1 }), error => error.code === 'REVISION_CONFLICT')
  await assert.rejects(store.publishParametricRevision({ ...revisionArgs, expectedArchiveSha256: 'B'.repeat(64) }), error => error.code === 'REVISION_PUBLICATION_STATE_CHANGED')
  await assert.rejects(store.publishParametricRevision({ ...revisionArgs, candidateAttempt: isolated }), error => error.code === 'REVISION_PUBLICATION_NOT_ACCEPTED')
  assert.equal((await store.read(oldTask.id)).revision, oldTask.revision, 'rejected revisions must not modify the task')
  const revisedTask = await store.publishParametricRevision(revisionArgs)
  assert.equal(revisedTask.nativeBuild.state, 'ready')
  assert.equal(revisedTask.nativeBuild.result.archiveSha256, valid.archiveSha256)
  assert.deepEqual(revisedTask.nativeBuild.deliveryRevisionHistory[0].result, previousResult)
  assert.deepEqual(revisedTask.parametricRequest, oldTask.parametricRequest)
  assert.equal((await store.publishParametricRevision(revisionArgs)).revision, revisedTask.revision, 'repeated revision publication must be idempotent')
  await assert.rejects(store.publishParametricRevision({ ...revisionArgs, expectedRevision: revisedTask.revision, transitionId: 'same-model', expectedArchiveSha256: valid.archiveSha256 }), error => error.code === 'REVISION_PUBLICATION_IDENTICAL')
  function rebind(phase, evidence) {
    const evidencePath = resolve(attempt, 'evidence', phase + '.json'); write(evidencePath, evidence)
    const receiptPath = resolve(attempt, 'receipts', phase + '.json'), receipt = JSON.parse(readFileSync(receiptPath))
    receipt.evidenceSha256 = hash(evidencePath); write(receiptPath, receipt)
  }
  plan.feedbackRepairRequired = true; write(planPath, plan)
  for (const phase of ['dimensions', 'derived', 'base-hole', 'assemblies', 'door-603', 'locks-verify', 'root', 'package', 'verify-package', 'verify-relocated', 'physical-audit']) {
    const receiptPath = resolve(attempt, 'receipts', phase + '.json'), authorizationPath = resolve(attempt, 'authorizations', phase + '.json')
    const receipt = JSON.parse(readFileSync(receiptPath)), authorization = JSON.parse(readFileSync(authorizationPath))
    authorization.planSha256 = hash(planPath); write(authorizationPath, authorization)
    receipt.planSha256 = hash(planPath); receipt.authorizationSha256 = hash(authorizationPath); write(receiptPath, receipt)
  }
  assert.match(validate16029ParametricPublication(attempt).reason, /missing engineer feedback repair stage/)
  delete plan.feedbackRepairRequired; write(planPath, plan)
  for (const phase of ['dimensions', 'derived', 'base-hole', 'assemblies', 'door-603', 'locks-verify', 'root', 'package', 'verify-package', 'verify-relocated', 'physical-audit']) {
    const receiptPath = resolve(attempt, 'receipts', phase + '.json'), authorizationPath = resolve(attempt, 'authorizations', phase + '.json')
    const receipt = JSON.parse(readFileSync(receiptPath)), authorization = JSON.parse(readFileSync(authorizationPath))
    authorization.planSha256 = hash(planPath); write(authorizationPath, authorization)
    receipt.planSha256 = hash(planPath); receipt.authorizationSha256 = hash(authorizationPath); write(receiptPath, receipt)
  }
  const originalQuality = readFileSync(qualityPath)
  write(qualityPath, { ...quality, schema: 'old', baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(resolve(attempt, 'evidence/physical-audit.json')) })
  assert.match(validate16029ParametricPublication(attempt).reason, /quality schema/)
  writeFileSync(qualityPath, originalQuality)
  const broken = structuredClone(fixture.candidate); broken.rows.splice(broken.rows.findIndex(row => row.inspection === 'native_flat_pattern'), 1)
  rebind('physical-audit', broken)
  const refreshed = JSON.parse(readFileSync(qualityPath)); refreshed.candidateEvidenceSha256 = hash(resolve(attempt, 'evidence/physical-audit.json')); write(qualityPath, refreshed)
  assert.match(validate16029ParametricPublication(attempt).reason, /flat pattern.*coverage mismatch/)
  rebind('physical-audit', fixture.candidate); writeFileSync(qualityPath, originalQuality)

  const legacyAudit = structuredClone(fixture.candidate); legacyAudit.rows = legacyAudit.rows.filter(row => row.inspection !== 'native_physical_coverage')
  rebind('physical-audit', legacyAudit)
  write(qualityPath, { ...quality, schema: undefined, coverageVerified: undefined, baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(resolve(attempt, 'evidence/physical-audit.json')) })
  assert.equal(validate16029ParametricPublication(attempt, { legacyPublished: true }).ok, false, 'new generation cannot request legacy downgrade')
  const historicalTask = { id: 'NATIVE-20261003T090446-8F7DE2', updatedAt: '2026-10-03T09:37:05.261Z', status: 'native_assistance_model_ready', parametricRequest: request, nativeBuild: { state: 'ready', modelReady: true, result: { schema: 'winnsen.locker16029.parametric_publication.v1', ...valid } } }
  const historic = inspect16029ParametricTaskPublication(historicalTask)
  assert.equal(historic.ok, true, historic.reason)
  assert.equal(historic.coverageVerified, false)
  assert.equal(historic.quality.pass, false)
  assert.equal(historic.quality.coverageStatus, 'legacy_unverified')
  assert.equal(inspect16029ParametricTaskPublication({ ...historicalTask, id: 'NEW-REQUEST' }).ok, false)
  assert.equal(inspect16029ParametricTaskPublication({ ...historicalTask, updatedAt: '2026-10-05T00:00:00.000Z' }).ok, false)
  const tampered = structuredClone(historicalTask); tampered.nativeBuild.result.archiveSha256 = 'F'.repeat(64)
  assert.equal(inspect16029ParametricTaskPublication(tampered).ok, false)
  assert.equal(validate16029ParametricPublication(isolated).ok, false)
  const evidencePath = resolve(attempt, 'evidence/verify-package.json')
  writeFileSync(evidencePath, readFileSync(evidencePath, 'utf8') + '\n')
  assert.match(inspect16029ParametricTaskPublication(historicalTask).reason, /receipt evidence or authorization hash mismatch/)

  const productionAttempt = resolve(base, 'PARAM-production-fixture'), productionFixture = build16029PhysicalAuditFixture({ sheetCount: 3 })
  const productionRootName = '16029_850W_1917H_550D.SLDASM', productionSourceRootName = '标准寄存柜1917×1000×550(总装配).SLDASM'
  const fixtureRootName = productionFixture.candidate.rows.find(row => row.inspection === 'native_physical_coverage').root.file, fixtureContent = productionFixture.content
  productionFixture.content = file => fixtureContent(file === productionRootName || file === productionSourceRootName ? fixtureRootName : file)
  for (const evidence of [productionFixture.candidate, productionFixture.options.packageVerification]) {
    const coverage = evidence.rows.find(row => row.inspection === 'native_physical_coverage')
    coverage.root.file = productionRootName
    coverage.packageFiles.find(row => row.file === fixtureRootName).file = productionRootName
  }
  productionFixture.options.packageInventory.find(row => basename(row.path) === fixtureRootName).path = '/fixture/package/' + productionRootName
  for (const [index, file] of ['箱体横层板L.sldprt', '箱体横层板R.SLDPRT'].entries()) {
    const sha256 = createHash('sha256').update(productionFixture.content(file)).digest('hex').toUpperCase(), configuration = 'Default', component = file.replace(/\.sldprt$/i, '') + '-1'
    const x = 100 + index * 30, body = { id: JSON.stringify([component, configuration, 'Solid1', 0]), component, configuration, bodyName: 'Solid1', bodyIndex: 0, boxMm: [x, 0, 0, x + 20, 20, 1] }
    const part = { file, sha256, configuration, role: 'structural_sheet_metal', exclusionReason: '', solidBodyCount: 1, hasSheetMetal: true, requiredFlatPattern: true, flatPatternNames: ['Flat-Pattern1'] }
    const instance = { component, file, sha256, configuration, documentType: 'part', disposition: 'included', exclusionReason: '', suppressed: false, hidden: false, solidBodyCount: 1, bodies: [body] }
    for (const evidence of [productionFixture.candidate, productionFixture.options.packageVerification]) {
      const coverage = evidence.rows.find(row => row.inspection === 'native_physical_coverage')
      coverage.packageFiles.push({ file, sha256 }); coverage.parts.push(structuredClone(part)); coverage.instances.push(structuredClone(instance))
    }
    productionFixture.options.packageInventory.push({ path: '/fixture/package/' + file, sha256 })
    productionFixture.options.packageVerification.rows.push({ name: component, path: '/fixture/package/' + file, issues: [], worldBodyBoundsMm: [body.boxMm] })
    productionFixture.candidate.rows.find(row => row.inspection === 'native_world_body_bounds').bodies.push(body)
    productionFixture.candidate.rows.find(row => row.inspection === 'native_exact_solid_interference').bodyCount++
    productionFixture.candidate.rows.push({ inspection: 'native_flat_pattern', file, sha256, configuration, role: part.role, feature: 'Flat-Pattern1', unsuppressed: true, rebuilt: true, flatBoxMm: [0, 0, 0, 20, 30, 1], issues: [] })
  }
  const productionRequest = { ...request, orderReference: '20261029-1917x1200x485-L12-R13', columns: [{ side: 'L', doors: [1, 2, 3].map(heightUnits => ({ heightUnits })) }, { side: 'R', doors: [3, 2, 1].map(heightUnits => ({ heightUnits })) }], productionProfile: 'indoor-weld-locators-20261029-r1' }, productionContract = build16029ParametricContract(productionRequest)
  const productionPlanPath = resolve(productionAttempt, 'parametric_execution.json'), productionExecutable = resolve(productionAttempt, 'tools/NativeParametricAssembly.exe')
  const productionPlan = { schema: 'winnsen.locker16029.parametric_execution.v1', taskId: 'PARAM-production-fixture', workerId: 'production-fixture-worker', taskPath: resolve(productionAttempt, 'task_store/PARAM-production-fixture.json'), contract: productionContract,
    contractSha256: createHash('sha256').update(JSON.stringify(productionContract)).digest('hex').toUpperCase(), feedbackRepairRequired: true, resumedEvidence: null }
  write(productionPlanPath, productionPlan)
  write(productionPlan.taskPath, { id: productionPlan.taskId, taskType: 'native_solidworks_build_task', revision: 1, nativeBuild: { state: 'blocked', lease: null },
    requestFingerprint: createHash('sha256').update(JSON.stringify(productionRequest)).digest('hex').toUpperCase(), cabinetWidth: 850, cabinetHeight: 1917, cabinetDepth: 550, columns: 2, doorCount: 6 })
  write(productionExecutable, 'synthetic production executable; never run')
  const productionSource = resolve(isolated, 'workers/production_fixture.cs'); write(productionSource, 'synthetic source; never compiled')
  write(resolve(productionAttempt, 'tools/build.json'), { executableSha256: hash(productionExecutable), productionProfile: productionRequest.productionProfile, sources: [{ path: productionSource, sha256: hash(productionSource) }] })
  const productionWorking = resolve(productionAttempt, 'native_cad/working_pack'), productionDoors = resolve(productionAttempt, 'doors')
  mkdirSync(productionWorking, { recursive: true }); mkdirSync(productionDoors, { recursive: true })
  const doorFiles = productionFixture.options.packageVerification.rows.find(row => row.inspection === 'native_physical_coverage').partEdits.map(row => row.file)
  for (const item of productionFixture.options.packageInventory.filter(item => !doorFiles.includes(basename(item.path)))) writeFileSync(resolve(productionWorking, basename(item.path) === productionRootName ? productionSourceRootName : basename(item.path)), /箱体横层板/.test(item.path) ? 'original shelf ' + basename(item.path) : productionFixture.content(basename(item.path)))
  let productionInventory = inventory16029ProductionFiles(productionWorking, { cadOnly: true }), productionDoorInventory = [], previousProductionReceipt = ''
  const productionPhases = []
  function productionStage(phase, { rows = [], result, after = productionInventory, doorAfter = productionDoorInventory } = {}) {
    const authPath = resolve(productionAttempt, 'authorizations', phase + '.json'), evidencePath = resolve(productionAttempt, 'evidence', phase + '.json'), receiptPath = resolve(productionAttempt, 'receipts', phase + '.json')
    const protectedDoorFilesBefore = inventory16029ProductionFiles(productionDoors)
    write(authPath, { phase, taskId: productionPlan.taskId, workerId: productionPlan.workerId, leaseId: 'production-fixture-lease', expiresAt: '2026-10-29T12:00:00.000Z', planSha256: hash(productionPlanPath), executableSha256: hash(productionExecutable), preInventory: productionInventory, preDoorInventory: productionDoorInventory, protectedDoorFilesBefore, predecessor: previousProductionReceipt })
    write(evidencePath, { phase, ...(/^door-\d+$/.test(phase) ? { heightMm: Number(phase.slice(5)) } : {}), success: true, rows, ...(result ? { result } : {}) })
    write(receiptPath, { phase, taskId: productionPlan.taskId, planSha256: hash(productionPlanPath), authorizationSha256: hash(authPath), evidenceSha256: hash(evidencePath), before: productionInventory, after, beforeDoorInventory: productionDoorInventory, doorInventory: doorAfter, protectedDoorFilesBefore, protectedDoorFilesAfter: protectedDoorFilesBefore, protectedSourcesUnchanged: true, predecessor: previousProductionReceipt })
    productionInventory = after; productionDoorInventory = doorAfter; previousProductionReceipt = hash(receiptPath); productionPhases.push(phase)
  }
  for (const phase of ['clone', 'probe', 'dimensions', 'derived', 'base-hole', 'assemblies']) productionStage(phase)
  const productionEdits = []
  for (const [index, height] of [298, 603, 908].entries()) {
    const module = resolve(productionDoors, 'H' + height), transaction = resolve(productionAttempt, 'transactions', productionPlan.taskId + '-H' + height + '-20261029T120000000Z')
    mkdirSync(transaction, { recursive: true })
    const staging = resolve(transaction, 'candidate'), relocated = resolve(transaction, 'relocation_probe')
    const names = [doorFiles[index], 'door_panel_right_W362_H' + height + '.SLDPRT', 'door_stiffener_W362_H' + height + '.SLDPRT']
    const outputs = [...names.map(file => 'parts/' + file), ...['hinge_latch_plate', 'u_hook_pad', 'plastic_bushing', 'door_hinge_pin', 'circlip', 'mechanical_lock_tongue'].map(file => 'parts/' + file + '_W362_H' + height + '.SLDPRT'),
      ...['door_weld_left', 'door_weld_right', 'ordinary_door_left', 'ordinary_door_right'].map(file => 'assemblies/' + file + '_W362_H' + height + '.SLDASM'), 'door_module_flat_import_manifest.json']
    for (const name of outputs) { const path = resolve(module, name); mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, 'original door ' + name) }
    const edits = names.map((file, editIndex) => ({ path: resolve(staging, 'parts', file), role: ['left_panel', 'right_panel_mirror_part', 'stiffener'][editIndex],
      sha256_after_save: hash(resolve(module, 'parts', file)), target_width_mm: editIndex === 2 ? 0 : productionContract.geometry.doorLeafWidthMm, target_height_mm: height - (editIndex === 2 ? 10.5 : 0),
      after: { has_sheet_metal: true, has_flat_pattern: true, body_count: 1, traversal_complete: true, foreign_feature_count: 0, error_feature_count: 0, warning_feature_count: 0, mirror_part_feature_count: 0, detached_mirror_folder_count: editIndex === 1 ? 1 : 0 } }))
    const path = resolve(module, 'parts', names[0]); productionEdits.push({ path, role: 'left_panel', sha256_after_save: hash(path) })
    productionStage('door-' + height, { result: { transaction_root: transaction, staging_root: staging, relocation_probe_root: relocated, committed_output_dir: module,
      committed_reopen_passed: true, relocation_reopen_passed: true, flat_import_manifest_path: resolve(module, 'door_module_flat_import_manifest.json'), flat_import_manifest_sha256: hash(resolve(module, 'door_module_flat_import_manifest.json')),
      part_edits: edits, output_files: inventory16029ProductionFiles(module).map(item => ({ relative_path: item.relativePath.replaceAll('\\', '/'), sha256: item.sha256, size_bytes: readFileSync(item.path).length })),
      reopen_checks: edits.flatMap((edit, editIndex) => [['read_only_reopen', staging], ['relocation_reopen', relocated], ['committed_reopen', module]].map(([phase, root]) => ({ phase, role: ['left_panel', 'right_panel', 'stiffener'][editIndex], path: resolve(root, 'parts', basename(edit.path)), sha256: edit.sha256_after_save, gate: true }))) },
      doorAfter: inventory16029ProductionFiles(productionDoors, { cadOnly: true }) })
  }
  productionStage('locks-verify'); productionStage('root')
  const localizedFiles = productionEdits.map(edit => {
    const path = resolve(productionWorking, basename(edit.path)); cpSync(edit.path, path)
    return { inspection: 'production_localized_file', file: basename(path), path, sourcePath: edit.path, sourceSha256: edit.sha256_after_save, sha256: hash(path), productionProfile: productionRequest.productionProfile }
  })
  productionStage('repair-engineer-feedback', { rows: localizedFiles, after: inventory16029ProductionFiles(productionWorking, { cadOnly: true }) })
  const productionRevisions = localizedFiles.map(item => {
    writeFileSync(item.path, productionFixture.content(item.file))
    return { inspection: 'production_part_revision', file: item.file, path: item.path, role: 'door_panel', productionProfile: productionRequest.productionProfile, oldSha256: item.sha256, newProductionSha256: hash(item.path), oldDoorSha256: item.sourceSha256,
      doorEvidenceSha256: hash(resolve(productionAttempt, 'evidence/door-' + basename(dirname(dirname(item.sourcePath))).slice(1) + '.json')), hasSheetMetal: true, hasFlatPattern: true }
  })
  const leftShelf = resolve(productionWorking, '箱体横层板L.sldprt'), rightShelf = resolve(productionWorking, '箱体横层板R.SLDPRT')
  for (const path of [leftShelf, rightShelf]) {
    const oldSha256 = hash(path); writeFileSync(path, productionFixture.content(basename(path)))
    productionRevisions.push({ inspection: 'production_part_revision', file: basename(path), path, role: 'structural_sheet_metal', productionProfile: productionRequest.productionProfile,
      oldSha256, newProductionSha256: hash(path), oldDoorSha256: '', doorEvidenceSha256: '', hasSheetMetal: true, hasFlatPattern: true })
  }
  const solid = { geometrySha256: 'D'.repeat(64), volumeM3: .0001, exactBoundsM: [0, 0, 0, .4, .6, .0008], bodies: 1, vertices: 8, edges: 12, faces: 6 }
  const addedVolumeMm3 = 8 * Math.PI * .8, volumeBeforeM3 = solid.volumeM3 - addedVolumeMm3 * 1e-9
  const leftCentersModelM = [-.1361, -.2961].flatMap(x => [-.3785, -.1285, -.1685, -.4185].map(z => [x, 1.7175, z])), rightCentersModelM = leftCentersModelM.map(([x, y, z]) => [-x, y, z])
  const mirrorLink = { path: rightShelf, sourcePath: leftShelf, configuration: 'Default', sourceConfiguration: 'Default', feature: '箱体横层板L镜向', type: 'MirrorStock', references: [1, 2].map(() => ({ modelPath: leftShelf, status: 3 })) }
  const locatorSource = { inspection: 'production_locator_pair_source', leftPath: leftShelf, rightPath: rightShelf, leftConfiguration: 'Default', rightConfiguration: 'Default', leftVolumeBeforeM3: volumeBeforeM3, rightVolumeBeforeM3: volumeBeforeM3, leftCentersModelM, rightCentersModelM, mirrorLink }
  const locatorChanges = [leftShelf, rightShelf].map((path, index) => ({ inspection: 'production_locator_change', path, axis: 1, removedHoleCount: 8, diameterMm: 2,
    centersModelM: index ? rightCentersModelM : leftCentersModelM, addedVolumeMm3, feature: index ? mirrorLink.feature : '生产_取消已验证定位孔', configuration: 'Default',
    volumeBeforeM3, volumeAfterM3: solid.volumeM3, holeCountBefore: 8, holeCountAfter: 0, replacement: 'actual_weld_contact_short_lines',
    strategy: index ? 'mirrored_source_heal' : 'direct_heal', sourcePath: index ? leftShelf : '', sourceFeature: index ? '生产_取消已验证定位孔' : '', ...(index ? { mirrorLink } : {}) }))
  productionStage('production-apply', { after: inventory16029ProductionFiles(productionWorking, { cadOnly: true }), rows: [...productionRevisions, ...productionRevisions.map(item => ({ inspection: 'production_marks_apply', path: item.path, configuration: 'Default', sourceFileSha256BeforeCallerSave: item.oldSha256, markSolidBefore: solid, markSolidAfter: solid, protectedFeatureStateSha256: 'E'.repeat(64) })), locatorSource, ...locatorChanges] })
  productionStage('production-verify', { rows: [...productionRevisions.map(item => ({ inspection: 'production_part_verification', file: item.file, path: item.path, sha256: item.newProductionSha256, role: item.role, productionProfile: item.productionProfile, pass: true })),
    ...productionRevisions.map(item => ({ inspection: 'production_marks_verify', path: item.path, configuration: 'Default', sha256: item.newProductionSha256, markSolidCurrent: solid, protectedFeatureStateSha256: 'E'.repeat(64) })),
    ...locatorChanges.map(change => ({ inspection: 'production_locator_verification', path: change.path, removedHoleCount: 8, holeCount: 0, axis: 1, diameterMm: 2,
      strategy: change.strategy, sourcePath: change.sourcePath, sourceFeature: change.sourceFeature, feature: change.feature, configuration: change.configuration,
      volumeM3: change.volumeAfterM3, sha256: hash(change.path), replacement: change.replacement, pass: true, ...(change.mirrorLink ? { mirrorLink } : {}) }))] })
  productionStage('verify')
  const packageInput = resolve(productionAttempt, 'package_input'), sourceRoot = resolve(productionWorking, productionSourceRootName)
  const sourceTargets = new Map(productionInventory.map(item => [item.path, resolve(productionAttempt, 'package', item.path === sourceRoot ? productionRootName : basename(item.path))]))
  const digestInventory = rows => createHash('sha256').update([...rows].sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0).map(item => item.path + '\t' + item.sha256 + '\n').join('')).digest('hex').toUpperCase()
  const productionPackageInventory = productionInventory.map(item => {
    const path = sourceTargets.get(item.path); mkdirSync(resolve(path, '..'), { recursive: true }); cpSync(item.path, path)
    const isolatedPath = resolve(packageInput, basename(item.path)); mkdirSync(packageInput, { recursive: true }); cpSync(item.path, isolatedPath)
    const relocatedPath = resolve(productionAttempt, 'unzipped_native_model', basename(path)); mkdirSync(resolve(relocatedPath, '..'), { recursive: true }); cpSync(path, relocatedPath)
    const assembly = /\.SLDASM$/i.test(path), sourceDependencies = assembly ? productionInventory.filter(other => other.path !== item.path).map(other => other.path) : []
    const state = [{ configuration: 'Default', documentType: assembly ? 'assembly' : 'part', geometrySha256: assembly ? '' : item.sha256, featureStateSha256: 'E'.repeat(64), assemblyStateSha256: assembly ? 'D'.repeat(64) : '' }]
    return { path, sha256: hash(path), sourcePath: item.path, sourceSha256: item.sha256, isolatedPath, isolatedCopySha256: item.sha256, packInputSha256: item.sha256,
      isolatedAfterSha256: item.sha256, sourceDependencies, targetDependencies: sourceDependencies.map(source => sourceTargets.get(source)), isolatedAfterDependencies: sourceDependencies.map(source => resolve(packageInput, basename(source))),
      sourceState: state, isolatedAfterState: structuredClone(state), targetState: structuredClone(state), allowedChangeReason: assembly ? 'root_rename_and_reference_rewrite' : 'byte_identical' }
  })
  const remapPackaged = value => {
    if (typeof value === 'string') return sourceTargets.get(value) || value
    if (Array.isArray(value)) return value.map(remapPackaged)
    if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([field, current]) => [field, remapPackaged(current)]))
    return value
  }
  const productionNativeRows = JSON.parse(readFileSync(resolve(productionAttempt, 'evidence/production-verify.json'), 'utf8')).rows.map(row => {
    const mapped = remapPackaged(row)
    if (row.inspection === 'production_part_verification') return { ...mapped, inspection: 'production_packaged_part_verification', sourcePath: row.path, sourceSha256: row.sha256 }
    if (row.inspection === 'production_marks_verify') return { ...mapped, readOnly: true, independentlyRecomputed: true, saved: false, verificationSolidBefore: solid, verificationSolidAfter: solid }
    return mapped
  })
  productionPackageInventory.find(row => row.sourcePath === sourceRoot).provenance = { schema: 'winnsen.locker16029.package_provenance.v1', taskId: productionPlan.taskId,
    planSha256: hash(productionPlanPath), executableSha256: hash(productionExecutable), sourceRoot, isolatedRoot: resolve(packageInput, productionSourceRootName), targetRoot: sourceTargets.get(sourceRoot),
    includeSuppressed: false, sourceInventorySha256: digestInventory(productionInventory), protectedDoorFilesBeforeSha256: digestInventory(inventory16029ProductionFiles(productionDoors)),
    protectedDoorFilesAfterSha256: digestInventory(inventory16029ProductionFiles(productionDoors)), rootDependencyClosure: productionInventory.map(row => row.path), excluded: [], productionVerificationRows: productionNativeRows }
  productionStage('package', { rows: productionPackageInventory })
  const productionEvidence = verify16029ProductionEvidence(productionAttempt)
  const attachProductionBinding = evidence => {
    const coverage = evidence.rows.find(row => row.inspection === 'native_physical_coverage')
    for (const edit of coverage.partEdits) {
      const revision = productionRevisions.find(row => row.file === edit.file); edit.sourceSha256 = revision.oldDoorSha256
      edit.evidence = 'door-' + basename(dirname(dirname(localizedFiles.find(row => row.file === edit.file).sourcePath))).slice(1) + '.json'
      edit.productionRevision = { ...Object.fromEntries(['taskId', 'planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256'].map(field => [field, productionEvidence[field]])),
        ...Object.fromEntries(['productionProfile', 'role', 'oldSha256', 'newProductionSha256', 'oldDoorSha256', 'doorEvidenceSha256', 'hasSheetMetal', 'hasFlatPattern'].map(field => [field, revision[field]])) }
    }
  }
  attachProductionBinding(productionFixture.candidate); attachProductionBinding(productionFixture.options.packageVerification)
  const identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], bracket = [...identity]; bracket[10] = ((1917 - 92) / 2 - 104) / 1000; bracket[11] = .0152
  const feedbackMeasurements = [{ inspection: 'feedback_base_slots', pass: true, straightLengthMm: 3, totalSlotLengthMm: 6, minimumWebMm: 3, slotCount: 15 },
    { inspection: 'feedback_rear_holes', pass: true, part: '上盖壳体后侧板.sldprt', remainingTargetHoleCount: 0 },
    { inspection: 'feedback_maintenance_weldment', pass: true, componentCount: 2, bracketFile: '维护门锁支架_原生.SLDPRT', bracketTransform: bracket, panelTransform: identity }]
  const productionMeasured = { ...measured, tonguePositionsMm: ['L', 'R'].flatMap(side => productionContract.rowsByColumn[side].map(row => [side === 'L' ? -55 : 55, row.centerYmm, -11.3])) }
  const productionVerification = productionFixture.options.packageVerification; productionVerification.rows.push(productionMeasured, ...feedbackMeasurements)
  productionStage('verify-package', { rows: productionVerification.rows })
  const productionArchive = resolve(productionAttempt, 'native_model.zip'); writeFileSync(productionArchive, 'synthetic production archive')
  productionStage('verify-relocated', { rows: [...productionVerification.rows, { archive: productionArchive, sha256: hash(productionArchive), cadFileCount: productionPackageInventory.length, extractedFilesByteIdentical: true }] })
  productionStage('physical-audit', { rows: productionFixture.candidate.rows })
  write(resolve(productionAttempt, 'protected_sources.before.json'), [])
  const productionQuality = compare16029PhysicalAudit(fixture.reference, productionFixture.candidate, { ...productionFixture.options, packageInventory: productionPackageInventory, productionEvidence })
  write(resolve(productionAttempt, 'evidence/physical-quality.json'), { ...productionQuality, baselinePath, baselineSha256: hash(baselinePath), candidateEvidenceSha256: hash(resolve(productionAttempt, 'evidence/physical-audit.json')), packageEvidenceSha256: hash(resolve(productionAttempt, 'evidence/package.json')), packageVerificationEvidenceSha256: hash(resolve(productionAttempt, 'evidence/verify-package.json')) })
  const productionPublication = validate16029ParametricPublication(productionAttempt, { expectedRequest: productionRequest })
  assert.equal(productionPublication.ok, true, productionPublication.reason)
  assert.equal(productionPublication.coverageVerified, true)
  assert.equal(productionContract.input.orderReference, productionRequest.orderReference)
  assert.equal(productionPublication.request.orderReference, productionRequest.orderReference)
  assert.equal(productionPublication.productionProfile, productionRequest.productionProfile)
  assert.equal(productionPublication.candidateSource.taskId, productionPlan.taskId)
  assert.equal(productionPublication.candidateSource.planSha256, hash(productionPlanPath))
  assert.equal(productionPublication.candidateSource.contractSha256, productionPlan.contractSha256)
  assert.equal(productionPublication.candidateSource.productionEvidence.verificationEvidenceSha256, productionEvidence.verificationEvidenceSha256)
  assert.equal(validate16029ParametricPublication(productionAttempt, { expectedRequest: request }).ok, false, 'expected request cannot omit production profile')

  copyModule('tools/run_16029_parametric_portal_worker.mjs')
  const { run16029ParametricPortalTask, runNext16029ParametricPortalTask } = await import(pathToFileURL(resolve(isolated, 'tools/run_16029_parametric_portal_worker.mjs')))
  const standardFields = { doorType: 'ordinary_door_panel', lockType: '机械锁舌；排除电控锁实体', hingeType: '暗铰链', latchType: '锁舌接口',
    reinforcement: '按模板规则生成', openings: '一门一锁孔；保留已验证定位基准', material: '待工程深化确认', thickness: '待工程深化确认' }
  const makeReuseTask = (id, overrides = {}) => store.createOrImport({ id, username: 'reuse-owner', createdAt: '2026-10-29T01:00:00.000Z', requestFingerprint: 'reuse-' + id,
    status: 'native_task_created', taskType: 'native_solidworks_build_task', parametricRequest: productionRequest, productionProfile: productionRequest.productionProfile,
    ...standardFields, nativeBuild: { state: 'created', modelReady: false }, ...overrides })
  let modelCalls = 0, mockValidationCalls = 0, cadChecks = 0
  const reuseOptions = { dataDir: store.dataDir, reuseCandidate: { attempt: productionAttempt, archiveSha256: productionPublication.archiveSha256 },
    runModel: async () => { modelCalls++; assert.fail('accepted candidate reuse must not start model generation') },
    validate: () => { mockValidationCalls++; assert.fail('candidate reuse must use the trusted store validator') },
    cadAvailable: () => { cadChecks++; return false } }
  await makeReuseTask('NATIVE-reused-candidate')
  const adopted = await runNext16029ParametricPortalTask(reuseOptions)
  assert.equal(adopted.state, 'ready', adopted.error)
  assert.equal(adopted.executionMode, 'accepted_candidate_reuse'); assert.equal(adopted.cadStarted, false)
  const adoptedTask = await store.read(adopted.taskId), adoptedResult = adoptedTask.nativeBuild.result
  assert.deepEqual(adoptedTask.nativeBuild.statusHistory.filter(row => row.to).map(row => row.to), ['claimed', 'planning', 'building', 'validating', 'ready'])
  assert.equal(adoptedTask.nativeBuild.statusHistory.at(-1).operation, 'complete_parametric_candidate')
  assert.equal(adoptedTask.nativeBuild.execution.cadStarted, false)
  assert.equal(adoptedTask.nativeBuild.execution.modelGenerated, false)
  assert.equal(adoptedResult.portalTaskId, adoptedTask.id)
  assert.equal(adoptedResult.requestFingerprint, adoptedTask.requestFingerprint)
  assert.notEqual(adoptedResult.candidateSource.taskId, adoptedTask.id)
  assert.deepEqual(adoptedResult.candidateSource, productionPublication.candidateSource)
  assert.equal(adoptedResult.request.orderReference, productionRequest.orderReference)
  assert.equal(inspect16029ParametricTaskPublication(adoptedTask).ok, true)
  const archive = open16029ParametricTaskArchive(adoptedTask)
  try {
    const bytes = Buffer.alloc(archive.stat.size); assert.equal(readSync(archive.fd, bytes, 0, bytes.length, 0), bytes.length)
    assert.equal(createHash('sha256').update(bytes).digest('hex').toUpperCase(), productionPublication.archiveSha256)
  } finally { closeSync(archive.fd) }
  assert.equal(validate16029ParametricCandidateReuse(productionAttempt, { task: { ...adoptedTask, material: '由结构工程师在项目深化中确定', thickness: '由结构工程师在项目深化中确定' }, expectedArchiveSha256: productionPublication.archiveSha256 }).ok, true)
  const withoutOrderReference = structuredClone(productionRequest); delete withoutOrderReference.orderReference
  for (const parametricRequest of [withoutOrderReference, ...['A', '订单备注', 'A'.repeat(200)].map(orderReference => ({ ...productionRequest, orderReference }))]) {
    const checked = validate16029ParametricCandidateReuse(productionAttempt, { task: { ...adoptedTask, parametricRequest }, expectedArchiveSha256: productionPublication.archiveSha256 })
    assert.equal(checked.ok, true, checked.reason)
    assert.deepEqual(checked.candidateSource, productionPublication.candidateSource, 'non-geometric order metadata cannot rewrite candidate identity')
  }
  for (const orderReference of [undefined, null, 1, true, {}, [], '', ' ', ' A', 'A ', 'A'.repeat(201), 'A\nB', 'A\tB', 'A\u0000B', 'A\u007FB']) {
    const checked = validate16029ParametricCandidateReuse(productionAttempt, { task: { ...adoptedTask, parametricRequest: { ...productionRequest, orderReference } }, expectedArchiveSha256: productionPublication.archiveSha256 })
    assert.equal(checked.ok, false, 'invalid orderReference must fail: ' + JSON.stringify(orderReference))
    assert.match(checked.reason, /orderReference must be a trimmed string/)
  }
  for (const mutate of [task => { task.nativeBuild.result.candidateSource.taskId = task.id }, task => { delete task.nativeBuild.result.executionMode }, task => { task.nativeBuild.result.cadStarted = true },
    task => { task.nativeBuild.result.portalTaskId = 'another-task' }, task => { task.nativeBuild.result.requestFingerprint = 'another-fingerprint' }, task => { task.nativeBuild.result.productionProfile = '' }]) {
    const tampered = structuredClone(adoptedTask); mutate(tampered)
    assert.equal(inspect16029ParametricTaskPublication(tampered).ok, false, 'reused publication provenance must remain bound')
  }
  const productionQualityPath = resolve(productionAttempt, 'evidence/physical-quality.json'), savedProductionQuality = readFileSync(productionQualityPath)
  writeFileSync(productionQualityPath, Buffer.concat([savedProductionQuality, Buffer.from('\n')]))
  assert.equal(validate16029ParametricPublication(productionAttempt).ok, true, 'unchanged accepted measurements remain valid after harmless evidence reserialization')
  assert.match(inspect16029ParametricTaskPublication(adoptedTask).reason, /provenance binding/, 'adoption must still bind the exact accepted evidence version')
  assert.throws(() => open16029ParametricTaskArchive(adoptedTask), /provenance binding/)
  writeFileSync(productionQualityPath, savedProductionQuality)

  const noProfile = structuredClone(productionRequest); delete noProfile.productionProfile
  const reordered = structuredClone(productionRequest); reordered.columns[0].doors.reverse()
  const mismatches = [
    { parametricRequest: { ...productionRequest, cabinet: { ...productionRequest.cabinet, widthMm: 913 } } },
    { parametricRequest: noProfile, productionProfile: undefined }, { parametricRequest: reordered },
    { productionProfile: 'different-profile' }, { parametricRequest: { ...productionRequest, material: 'custom steel' } },
    { parametricRequest: { ...productionRequest, orderReferenceExtra: 'uncovered custom requirement' } },
    { parametricRequest: { ...productionRequest, cabinet: { ...productionRequest.cabinet, orderReference: productionRequest.orderReference } } },
    { parametricRequest: { ...productionRequest, orderReference: {} } },
    ...Object.keys(standardFields).map(field => ({ [field]: 'custom requirement' })),
  ]
  for (const [index, overrides] of mismatches.entries()) {
    const task = await makeReuseTask('NATIVE-reuse-mismatch-' + index, overrides)
    const refused = await run16029ParametricPortalTask(task.id, reuseOptions)
    assert.equal(refused.state, 'failed', 'candidate must reject request mismatch ' + index)
    assert.equal(refused.modelReady, false)
    assert.equal((await store.read(task.id)).nativeBuild.result, undefined)
  }
  await makeReuseTask('NATIVE-reuse-wrong-archive')
  assert.equal((await run16029ParametricPortalTask('NATIVE-reuse-wrong-archive', { ...reuseOptions, reuseCandidate: { attempt: productionAttempt, archiveSha256: 'F'.repeat(64) } })).state, 'failed')
  await makeReuseTask('NATIVE-reuse-cancelled')
  assert.equal((await run16029ParametricPortalTask('NATIVE-reuse-cancelled', { ...reuseOptions, signal: AbortSignal.abort() })).state, 'cancelled')

  const directInput = await makeReuseTask('NATIVE-reuse-direct'), workerId = 'reuse-direct-worker'
  let direct = await store.claimTask({ taskId: directInput.id, workerId }), directLease = direct.nativeBuild.lease.id
  for (const toState of ['planning', 'building', 'validating']) direct = await store.transition({ taskId: direct.id, workerId, leaseId: directLease, expectedRevision: direct.revision, transitionId: 'direct-' + toState, toState })
  const directArgs = { taskId: direct.id, workerId, leaseId: directLease, expectedRevision: direct.revision, transitionId: 'direct-adopt-ready', candidateAttempt: productionAttempt, expectedArchiveSha256: productionPublication.archiveSha256 }
  await assert.rejects(store.completeParametricCandidate({ ...directArgs, expectedRevision: direct.revision - 1 }), error => error.code === 'REVISION_CONFLICT')
  await assert.rejects(store.completeParametricCandidate({ ...directArgs, leaseId: 'foreign-lease' }), error => error.code === 'LEASE_MISMATCH')
  await assert.rejects(store.completeParametricCandidate({ ...directArgs, workerId: 'foreign-worker' }), error => error.code === 'LEASE_WORKER_MISMATCH')
  const stop = new AbortController()
  writeFileSync(store.lockPath, JSON.stringify({ token: 'reuse-completion-lock-test', pid: process.pid }))
  const waitingCompletion = store.completeParametricCandidate({ ...directArgs, signal: stop.signal })
  await new Promise(done => setTimeout(done, 20)); stop.abort(); rmSync(store.lockPath)
  await assert.rejects(waitingCompletion, error => error.code === 'COMPLETE_ABORTED')
  assert.deepEqual(await store.read(direct.id), direct)
  const directReady = await store.completeParametricCandidate(directArgs)
  assert.equal(directReady.nativeBuild.state, 'ready')
  assert.equal((await store.completeParametricCandidate({ ...directArgs, signal: AbortSignal.abort() })).revision, directReady.revision)
  await assert.rejects(store.completeParametricCandidate({ ...directArgs, expectedArchiveSha256: 'F'.repeat(64) }), error => error.code === 'TRANSITION_ID_REUSED')

  const driftInput = await makeReuseTask('NATIVE-reuse-evidence-drift')
  let drift = await store.claimTask({ taskId: driftInput.id, workerId }), driftLease = drift.nativeBuild.lease.id
  for (const toState of ['planning', 'building', 'validating']) drift = await store.transition({ taskId: drift.id, workerId, leaseId: driftLease, expectedRevision: drift.revision, transitionId: 'drift-' + toState, toState })
  const driftArgs = { ...directArgs, taskId: drift.id, leaseId: driftLease, expectedRevision: drift.revision, transitionId: 'drift-adopt-ready' }
  const productionVerifyPath = resolve(productionAttempt, 'evidence/production-verify.json'), savedProductionVerify = readFileSync(productionVerifyPath)
  writeFileSync(store.lockPath, JSON.stringify({ token: 'reuse-evidence-lock-test', pid: process.pid }))
  const driftCompletion = store.completeParametricCandidate(driftArgs)
  await new Promise(done => setTimeout(done, 20)); writeFileSync(productionVerifyPath, Buffer.concat([savedProductionVerify, Buffer.from('\n')]))
  rmSync(store.lockPath)
  await assert.rejects(driftCompletion, error => error.code === 'CANDIDATE_REUSE_NOT_ACCEPTED')
  assert.deepEqual(await store.read(drift.id), drift)
  writeFileSync(productionVerifyPath, savedProductionVerify)
  const cancelledDrift = await store.cancel({ taskId: drift.id, expectedRevision: drift.revision, transitionId: 'drift-cancel', reason: 'User cancelled before adoption' })
  await assert.rejects(store.completeParametricCandidate({ ...driftArgs, expectedRevision: cancelledDrift.revision }), error => error.code === 'INVALID_TRANSITION')
  assert.equal(modelCalls, 0); assert.equal(mockValidationCalls, 0); assert.equal(cadChecks, 0)
  assert.deepEqual(validate16029ParametricPublication(productionAttempt).candidateSource, productionPublication.candidateSource, 'adoption cannot rewrite source plans, receipts or evidence')
  const productionApplyPath = resolve(productionAttempt, 'evidence/production-apply.json'), originalProductionApply = readFileSync(productionApplyPath)
  const productionApply = JSON.parse(originalProductionApply); productionApply.rows[0].oldDoorSha256 = 'F'.repeat(64); write(productionApplyPath, productionApply)
  let rebindPrevious = ''
  for (const phase of productionPhases) {
    const authPath = resolve(productionAttempt, 'authorizations', phase + '.json'), receiptPath = resolve(productionAttempt, 'receipts', phase + '.json')
    const authorization = JSON.parse(readFileSync(authPath)), receipt = JSON.parse(readFileSync(receiptPath)); authorization.predecessor = rebindPrevious; write(authPath, authorization)
    receipt.predecessor = rebindPrevious; receipt.authorizationSha256 = hash(authPath); receipt.evidenceSha256 = hash(resolve(productionAttempt, 'evidence', phase + '.json')); write(receiptPath, receipt); rebindPrevious = hash(receiptPath)
  }
  assert.match(validate16029ParametricPublication(productionAttempt).reason, /door revision provenance mismatch/, 'rehashed publication still rejects original door mismatch')
  console.log('parametric publication: full production evidence and exact provenance accepted; explicit worker adoption completes and downloads with busy CAD and zero model calls; geometry/profile/ordered-door/custom-field/hash/evidence drift, lease, revision and cancellation guards verified; idempotency and historical download retained; CAD not started')
} finally { rmSync(isolated, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 }) }
