import assert from 'node:assert/strict'
import { compare16029PhysicalAudit, inspect16029LegacyPhysicalHealth, physical16029PartRole } from './verify_16029_parametric_physical_quality.mjs'
import { build16029PhysicalAuditFixture } from './fixtures/locker_16029_physical_audit.mjs'

const fixture = build16029PhysicalAuditFixture({ sheetCount: 48 })
const compare = (candidate = fixture.candidate, options = fixture.options) => compare16029PhysicalAudit(fixture.reference, candidate, options)
const coverage = candidate => candidate.rows.find(row => row.inspection === 'native_physical_coverage')
const interference = candidate => candidate.rows.find(row => row.inspection === 'native_exact_solid_interference')
const valid = compare()
assert.equal(valid.pass, true)
assert.equal(valid.coverageVerified, true)
assert.equal(valid.flatPatternCount, 48)
assert.equal(valid.expectedFlatPatternCount, 48)
assert.equal(valid.excludedReferenceInstanceCount, 1)
assert.equal(physical16029PartRole('箱体竖隔板R_parametric.SLDPRT'), 'structural_sheet_metal')
assert.equal(physical16029PartRole('箱体竖隔板R_parametric_other.SLDPRT'), 'unknown')
const generatedRight = JSON.parse(JSON.stringify(build16029PhysicalAuditFixture({ sheetCount: 2 }))
  .replaceAll('door_panel_left_W317_H100.SLDPRT', '箱体竖隔板R_parametric.SLDPRT'))
for (const evidence of [generatedRight.candidate, generatedRight.options.packageVerification]) {
  const manifest = coverage(evidence)
  manifest.parts.find(part => part.file === '箱体竖隔板R_parametric.SLDPRT').role = 'structural_sheet_metal'
  manifest.partEdits = manifest.partEdits.filter(part => part.file !== '箱体竖隔板R_parametric.SLDPRT')
  for (const row of evidence.rows) if (row.inspection === 'native_flat_pattern' && row.file === '箱体竖隔板R_parametric.SLDPRT') row.role = 'structural_sheet_metal'
}
const compareGeneratedRight = candidate => compare16029PhysicalAudit(generatedRight.reference, candidate, generatedRight.options)
assert.equal(compareGeneratedRight(generatedRight.candidate).pass, true, 'generated right partition requires full structural coverage')
const generatedRightMissingFlat = structuredClone(generatedRight.candidate)
generatedRightMissingFlat.rows = generatedRightMissingFlat.rows.filter(row => !(row.inspection === 'native_flat_pattern' && row.file === '箱体竖隔板R_parametric.SLDPRT'))
assert.equal(compareGeneratedRight(generatedRightMissingFlat).pass, false, 'generated right partition cannot omit its flat')
const generatedRightHashDrift = structuredClone(generatedRight.candidate)
coverage(generatedRightHashDrift).parts.find(part => part.file === '箱体竖隔板R_parametric.SLDPRT').sha256 = 'F'.repeat(64)
assert.equal(compareGeneratedRight(generatedRightHashDrift).pass, false, 'generated right partition hash drift rejected')

function rejected(change, message) {
  const candidate = structuredClone(fixture.candidate)
  change(candidate)
  assert.equal(compare(candidate).pass, false, message)
}
rejected(candidate => candidate.rows.splice(candidate.rows.findIndex(row => row.inspection === 'native_flat_pattern'), 1), '48 minus one flat rejected')
rejected(candidate => candidate.rows.push(structuredClone(candidate.rows.find(row => row.inspection === 'native_flat_pattern'))), 'duplicate flat rejected')
rejected(candidate => candidate.rows.find(row => row.inspection === 'native_flat_pattern').configuration = 'Wrong', 'wrong flat configuration rejected')
rejected(candidate => candidate.rows.find(row => row.inspection === 'native_flat_pattern').sha256 = 'F'.repeat(64), 'flat hash drift rejected')
rejected(candidate => candidate.rows.find(row => row.inspection === 'native_flat_pattern').file = '/outside/' + candidate.rows.find(row => row.inspection === 'native_flat_pattern').file, 'flat path escape rejected')
rejected(candidate => candidate.rows.find(row => row.inspection === 'native_flat_pattern').rebuilt = false, 'failed flat rejected')
rejected(candidate => interference(candidate).bodyCount = 0, 'zero body count rejected')
rejected(candidate => interference(candidate).checkedPairs = 0, 'zero pair count with eligible pairs rejected')
rejected(candidate => interference(candidate).pairs.pop(), 'missing checked pair rejected')
rejected(candidate => interference(candidate).pairs.push(structuredClone(interference(candidate).pairs[0])), 'duplicate checked pair rejected')
rejected(candidate => candidate.rows.find(row => row.inspection === 'native_world_body_bounds').bodies.pop(), 'missing measured body rejected')
rejected(candidate => coverage(candidate).instances.pop(), 'missing reference instance rejected')
rejected(candidate => coverage(candidate).instances.push({ component: 'Ghost-1', file: 'outside.sldprt', sha256: 'F'.repeat(64), configuration: 'Default', documentType: 'part', disposition: 'excluded', exclusionReason: 'suppressed_configuration', suppressed: true, hidden: false, solidBodyCount: 0, bodies: [] }), 'suppressed outside-package instance rejected')
rejected(candidate => coverage(candidate).instances.find(row => row.exclusionReason === 'hidden_reference_model').hidden = false, 'reference hidden proof required')
rejected(candidate => coverage(candidate).instances[0].hidden = true, 'hidden structural part rejected')
rejected(candidate => coverage(candidate).parts[0].flatPatternNames = [], 'sheet role cannot escape missing flat')
rejected(candidate => coverage(candidate).parts[0].role = 'unknown', 'unknown role rejected')
rejected(candidate => coverage(candidate).parts[0].sha256 = 'F'.repeat(64), 'part hash drift rejected')
rejected(candidate => coverage(candidate).parts[0].file = '/outside/' + coverage(candidate).parts[0].file, 'part path escape rejected')
rejected(candidate => coverage(candidate).root.file = '16029_/' + coverage(candidate).root.file, 'root path escape rejected')
rejected(candidate => coverage(candidate).partEdits[0].role = 'right_panel_mirror_part', 'left panel cannot claim right panel role')
rejected(candidate => coverage(candidate).partEdits[0].evidence = 'door-other.json', 'part edit evidence must match independent measurement')
rejected(candidate => coverage(candidate).partEdits[0].evidence = '/outside/door-fixture.json', 'part edit evidence path escape rejected')
rejected(candidate => coverage(candidate).partEdits[0].sourceSha256 = 'F'.repeat(64), 'part edit source must bind final package')
rejected(candidate => coverage(candidate).partEdits.pop(), 'part edit coverage gap rejected')
rejected(candidate => coverage(candidate).parts.push(structuredClone(coverage(candidate).parts[0])), 'duplicate part configuration rejected')
rejected(candidate => coverage(candidate).instances[0].bodies = [], 'null/empty leaf body evidence rejected')
rejected(candidate => { coverage(candidate).instances[0].bodies[0].boxMm[3] = NaN }, 'non-finite body bounds rejected')
assert.equal(compare(fixture.candidate, { doorCount: 6 }).pass, false, 'independent inventory required')
const alteredVerification = structuredClone(fixture.options)
alteredVerification.packageVerification.rows.find(row => row.name).worldBodyBoundsMm = []
assert.equal(compare(fixture.candidate, alteredVerification).pass, false, 'independent measurement required')

const disjoint = build16029PhysicalAuditFixture({ sheetCount: 2, separated: true })
const zeroPairs = compare16029PhysicalAudit(disjoint.reference, disjoint.candidate, disjoint.options)
assert.equal(zeroPairs.pass, true)
assert.equal(zeroPairs.expectedCheckedPairs, 0)
assert.equal(zeroPairs.contactCount, 0)
const hardwareOnly = build16029PhysicalAuditFixture({ sheetCount: 0, separated: true })
assert.equal(compare16029PhysicalAudit(hardwareOnly.reference, hardwareOnly.candidate, hardwareOnly.options).pass, true, 'non-sheet parts do not require a flat pattern')

const contactFixture = build16029PhysicalAuditFixture()
const pair = interference(contactFixture.candidate).pairs[0]
const findBody = id => contactFixture.candidate.rows[0].bodies.find(body => body.id === id)
const contact = { ...pair, componentA: findBody(pair.bodyA).component, componentB: findBody(pair.bodyB).component, volumeMm3: .1, interference: true, intersectionBoundsMm: [[0, 0, 0, 1, 1, 1]] }
interference(contactFixture.candidate).collisions.push(contact)
assert.equal(compare16029PhysicalAudit(contactFixture.reference, contactFixture.candidate, contactFixture.options).pass, false, 'new contact category rejected')
contactFixture.reference.rows[0].collisions.push(structuredClone(contact))
assert.equal(compare16029PhysicalAudit(contactFixture.reference, contactFixture.candidate, contactFixture.options).pass, true)
contact.volumeMm3 = 10
assert.equal(compare16029PhysicalAudit(contactFixture.reference, contactFixture.candidate, contactFixture.options).pass, false, 'oversized known contact rejected')
contact.volumeMm3 = .1
interference(contactFixture.candidate).collisions.push(structuredClone(contact))
assert.equal(compare16029PhysicalAudit(contactFixture.reference, contactFixture.candidate, contactFixture.options).pass, false, 'duplicate body contact rejected')

const legacy = structuredClone(fixture.candidate)
legacy.rows = legacy.rows.filter(row => row.inspection !== 'native_physical_coverage')
const legacyStrict = compare(legacy)
assert.equal(legacyStrict.pass, false)
assert.equal(legacyStrict.coverageVerified, false)
const legacyHealth = inspect16029LegacyPhysicalHealth(fixture.reference, legacy, fixture.options)
assert.equal(legacyHealth.pass, false)
assert.equal(legacyHealth.legacyHealthPass, true)
assert.equal(legacyHealth.coverageVerified, false)

const revised = build16029PhysicalAuditFixture({ sheetCount: 2 })
const revisedCoverage = coverage(revised.candidate), revisedEdit = revisedCoverage.partEdits[0], oldDoorSha256 = 'A'.repeat(64)
const productionEvidence = { ok: true, throughPhase: 'package', productionProfile: 'indoor-weld-locators-20261029-r1', taskId: 'PARAM-production-fixture',
  ...Object.fromEntries(['planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256', 'packageEvidenceSha256'].map(field => [field, 'B'.repeat(64)])),
  revisions: [{ file: revisedEdit.file, path: '/fixture/working/' + revisedEdit.file, role: 'door_panel', productionProfile: 'indoor-weld-locators-20261029-r1', oldSha256: oldDoorSha256, newProductionSha256: revisedEdit.packageSha256,
    oldDoorSha256, doorEvidenceSha256: 'C'.repeat(64), hasSheetMetal: true, hasFlatPattern: true }] }
const revision = productionEvidence.revisions[0]
productionEvidence.packageRevisions = [{ sourcePath: revision.path, sourceSha256: revision.newProductionSha256, path: '/fixture/package/' + revision.file,
  sha256: revision.newProductionSha256, file: revision.file, allowedChangeReason: 'byte_identical' }]
revisedEdit.sourceSha256 = oldDoorSha256
revisedEdit.productionRevision = { ...Object.fromEntries(['taskId', 'planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256'].map(field => [field, productionEvidence[field]])),
  ...Object.fromEntries(['productionProfile', 'role', 'oldSha256', 'newProductionSha256', 'oldDoorSha256', 'doorEvidenceSha256', 'hasSheetMetal', 'hasFlatPattern'].map(field => [field, revision[field]])) }
coverage(revised.options.packageVerification).partEdits[0] = structuredClone(revisedEdit)
const revisedOptions = { ...revised.options, productionEvidence }
const revisedCompare = (candidate = revised.candidate, options = revisedOptions) => compare16029PhysicalAudit(revised.reference, candidate, options)
assert.equal(revisedCompare().pass, true, 'door generation hash reaches revised package only through verified production evidence')
assert.equal(revisedCompare(revised.candidate, revised.options).pass, false, 'unverified revision cannot replace original door hash')
for (const field of ['taskId', 'planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256', 'productionProfile', 'role', 'oldSha256', 'newProductionSha256', 'oldDoorSha256', 'doorEvidenceSha256', 'hasSheetMetal', 'hasFlatPattern']) {
  const candidate = structuredClone(revised.candidate), options = structuredClone(revisedOptions)
  coverage(candidate).partEdits[0].productionRevision[field] = 'tampered'
  coverage(options.packageVerification).partEdits[0].productionRevision[field] = 'tampered'
  assert.equal(revisedCompare(candidate, options).pass, false, 'rebound physical revision ' + field + ' rejected')
}
const noRevision = structuredClone(revisedOptions); noRevision.productionEvidence.revisions = []
assert.equal(revisedCompare(revised.candidate, noRevision).pass, false, 'missing independent revision rejected')
const falseRevision = structuredClone(revisedOptions); falseRevision.productionEvidence.revisions[0].hasFlatPattern = false
assert.equal(revisedCompare(revised.candidate, falseRevision).pass, false, 'revision cannot relax measured sheet-metal health')
const rewrittenCandidate = structuredClone(revised.candidate), rewrittenOptions = structuredClone(revisedOptions), rewrittenSha256 = 'F'.repeat(64)
const rewriteEvidenceHashes = evidence => {
  const visit = value => {
    if (!value || typeof value !== 'object') return
    if (value.file === revision.file && Object.hasOwn(value, 'sha256')) value.sha256 = rewrittenSha256
    for (const item of Object.values(value)) visit(item)
  }
  visit(evidence)
  coverage(evidence).partEdits.find(edit => edit.file === revision.file).packageSha256 = rewrittenSha256
}
rewriteEvidenceHashes(rewrittenCandidate); rewriteEvidenceHashes(rewrittenOptions.packageVerification)
rewrittenOptions.packageInventory.find(item => item.path.endsWith('/' + revision.file)).sha256 = rewrittenSha256
rewrittenOptions.productionEvidence.packageRevisions[0].sha256 = rewrittenSha256
rewrittenOptions.productionEvidence.packageRevisions[0].allowedChangeReason = 'reference_rewrite'
assert.equal(revisedCompare(rewrittenCandidate, rewrittenOptions).pass, true, 'proven reference rewrite binds final bytes while preserving the original production revision hash')
for (const change of [
  options => { delete options.productionEvidence.packageRevisions },
  options => { options.productionEvidence.packageRevisions = [] },
  options => { options.productionEvidence.packageRevisions.push(structuredClone(options.productionEvidence.packageRevisions[0])) },
  options => { options.productionEvidence.packageRevisions[0].sourcePath = '/other/' + revision.file },
  options => { options.productionEvidence.packageRevisions[0].sourceSha256 = rewrittenSha256 },
  options => { options.productionEvidence.packageRevisions[0].sha256 = revision.newProductionSha256 },
  options => { options.productionEvidence.packageRevisions[0].path = '/other/' + revision.file },
  options => { options.productionEvidence.packageRevisions[0].allowedChangeReason = 'byte_identical' },
  options => { options.productionEvidence.packageRevisions[0].allowedChangeReason = 'unchecked_copy' },
]) {
  const options = structuredClone(rewrittenOptions); change(options)
  assert.equal(revisedCompare(rewrittenCandidate, options).pass, false, 'missing, duplicated, rebound or unsupported package revision mapping is rejected')
}
console.log('physical quality: complete identities accepted; missing/duplicate flats, body/pair gaps, false exclusions, changed configuration/hash, unknown roles and contacts rejected; complete zero-pair/non-sheet packages accepted; legacy coverage stays unverified; CAD not started')
