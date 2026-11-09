import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { INDOOR_16029_PRODUCTION_PROFILE } from './lib/locker_16029_production_profile.mjs'

export const PHYSICAL_QUALITY_SCHEMA = 'winnsen.locker16029.physical_quality.v2'
const COVERAGE_SCHEMA = 'winnsen.locker16029.physical_coverage.v1'
const leaf = value => String(value ?? '').split(/[\\/]/).at(-1)
const fileKey = value => leaf(value).toLowerCase()
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const referenceFiles = new Set(['标准寄存柜 模型.SLDPRT', '上盖 模型.sldprt', '底座 模型.sldprt'].map(fileKey))
const structuralSheets = new Set([
  '上盖壳体前侧板.sldprt', '上盖壳体右侧板.SLDPRT', '上盖壳体后侧板.sldprt', '上盖壳体左侧板.sldprt', '上盖壳体底板.sldprt',
  '底座加强筋.sldprt', '底座外框.sldprt', '底座底板.sldprt', '箱体侧板加强筋1.sldprt', '箱体侧板加强筋2.sldprt',
  '箱体右侧板.sldprt', '箱体左侧板.sldprt', '箱体横层板L.sldprt', '箱体横层板R.SLDPRT', '箱体横层板加强筋.SLDPRT',
  '箱体竖隔板L.sldprt', '箱体竖隔板R.SLDPRT', '箱体竖隔板R_parametric.SLDPRT', '箱体竖隔板R参数源.SLDPRT', '箱体竖隔板加强件.sldprt', '锁控维护条_源钣金.SLDPRT', '维护门锁支架_原生.SLDPRT',
  '门框 上.sldprt', '门框 下.sldprt', '门框 右.sldprt', '门框 左.sldprt', '门框 横隔板.sldprt', '门框 横隔板R.SLDPRT', '门框 竖隔板L.sldprt', '门框 竖隔板R.SLDPRT',
].map(fileKey))
const sheetRoles = new Set(['door_panel', 'door_stiffener', 'hinge_latch_plate', 'u_hook_pad', 'structural_sheet_metal'])

function partRole(file) {
  const key = fileKey(file)
  if (referenceFiles.has(key)) return 'reference_model'
  for (const prefix of ['door_panel_', 'door_stiffener_', 'hinge_latch_plate_', 'u_hook_pad_', 'circlip_', 'door_hinge_pin_', 'mechanical_lock_tongue_', 'plastic_bushing_', 'top_latch_']) {
    if (key.startsWith(prefix)) return prefix.slice(0, -1)
  }
  if (['螺母m12.sldprt', '调整脚 m12x60(模型).sldprt'].includes(key)) return 'levelling_hardware'
  return structuralSheets.has(key) ? 'structural_sheet_metal' : 'unknown'
}

export { partRole as physical16029PartRole }

const boxValid = box => Array.isArray(box) && box.length === 6 && box.every(Number.isFinite) && [0, 1, 2].every(axis => box[axis] <= box[axis + 3])
const sameBox = (a, b) => boxValid(a) && boxValid(b) && a.every((value, index) => Math.abs(value - b[index]) < .001)
const partKey = row => JSON.stringify([fileKey(row.file), row.configuration])
const flatKey = row => JSON.stringify([fileKey(row.file), row.configuration, row.feature])
const bodyId = value => JSON.stringify(JSON.parse(value))
const pairKey = row => JSON.stringify([bodyId(row.bodyA), bodyId(row.bodyB)].sort())

function uniqueMap(rows, key, label) {
  requireValue(Array.isArray(rows), label + ' missing')
  const result = new Map()
  for (const row of rows) {
    const id = key(row)
    requireValue(!result.has(id), 'duplicate ' + label + ': ' + id)
    result.set(id, row)
  }
  return result
}

function sameIdentities(actual, expected, label) {
  requireValue(actual.size === expected.size && [...expected.keys()].every(key => actual.has(key)), label + ' identity coverage mismatch')
}

function coverageRow(evidence, purpose) {
  const rows = evidence?.rows?.filter(row => row.inspection === 'native_physical_coverage')
  requireValue(rows?.length === 1 && rows[0].schema === COVERAGE_SCHEMA && rows[0].purpose === purpose, purpose + ' coverage schema missing or duplicated')
  return rows[0]
}

function validateCoverageManifest(coverage, inventory, productionEvidence) {
  const files = uniqueMap(coverage.packageFiles, row => fileKey(row.file), 'package file')
  sameIdentities(files, inventory, 'package file')
  for (const [key, row] of files) requireValue(row.file === leaf(row.file) && row.sha256 === inventory.get(key).sha256, 'package file hash mismatch: ' + key)
  const root = coverage.root
  requireValue(root && root.file === leaf(root.file) && /^16029_.*\.SLDASM$/i.test(root.file) && root.sha256 === files.get(fileKey(root.file))?.sha256 && typeof root.configuration === 'string' && root.configuration.length, 'root configuration or package hash missing')
  requireValue(coverage.pairFilter?.minimumOverlapMm === .001 && coverage.pairFilter?.excludeSameInstance === true, 'unrecognized interference pair filter')
  const parts = uniqueMap(coverage.parts, partKey, 'part configuration')
  const partFiles = new Map([...parts.values()].map(part => [fileKey(part.file), part]))
  sameIdentities(partFiles, new Map([...files].filter(([file]) => /\.sldprt$/i.test(file))), 'part file')
  const expectedFlats = new Map()
  for (const part of parts.values()) {
    requireValue(part.file === leaf(part.file) && part.sha256 === files.get(fileKey(part.file))?.sha256 && typeof part.configuration === 'string' && part.configuration.length && Number.isInteger(part.solidBodyCount) && part.solidBodyCount > 0, 'part configuration measurement missing: ' + partKey(part))
    const knownRole = partRole(part.file)
    requireValue(knownRole !== 'unknown' && part.role === knownRole, 'unrecognized or changed part role: ' + part.file)
    requireValue(typeof part.hasSheetMetal === 'boolean' && Array.isArray(part.flatPatternNames) && part.flatPatternNames.every(name => typeof name === 'string' && name.length), 'part sheet-metal classification missing: ' + part.file)
    const required = knownRole !== 'reference_model' && (sheetRoles.has(knownRole) || part.hasSheetMetal || part.flatPatternNames.length > 0)
    requireValue(part.exclusionReason === (knownRole === 'reference_model' ? 'reference_model_not_manufactured' : '') && part.requiredFlatPattern === required && (!required || part.flatPatternNames.length > 0), 'required flat pattern missing or exclusion invalid: ' + partKey(part))
    if (!required) continue
    for (const feature of part.flatPatternNames) {
      const identity = flatKey({ ...part, feature })
      requireValue(!expectedFlats.has(identity), 'duplicate expected flat pattern: ' + identity)
      expectedFlats.set(identity, part)
    }
  }
  const edits = uniqueMap(coverage.partEdits, row => fileKey(row.file), 'door part edit binding')
  const revisions = new Map()
  if (productionEvidence) {
    requireValue(productionEvidence.ok === true && productionEvidence.throughPhase === 'package' && productionEvidence.productionProfile === INDOOR_16029_PRODUCTION_PROFILE &&
      typeof productionEvidence.taskId === 'string' && productionEvidence.taskId.length > 0 &&
      ['planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256', 'packageEvidenceSha256'].every(field => /^[A-F0-9]{64}$/.test(productionEvidence[field])), 'production evidence identity incomplete')
    const packageRevisions = uniqueMap(productionEvidence.packageRevisions, row => fileKey(row.file), 'production package revision')
    const sourceRevisions = uniqueMap(productionEvidence.revisions, row => fileKey(row.file), 'production part revision')
    sameIdentities(sourceRevisions, packageRevisions, 'production package revision')
    for (const [key, revision] of sourceRevisions) {
      const part = [...parts.values()].find(row => fileKey(row.file) === key)
      const packaged = packageRevisions.get(key)
      requireValue(part && revision.file === leaf(revision.file) && revision.productionProfile === productionEvidence.productionProfile &&
        revision.role === part.role && packaged.file === revision.file && packaged.sourcePath === revision.path && packaged.sourceSha256 === revision.newProductionSha256 &&
        typeof packaged.path === 'string' && packaged.path === inventory.get(key)?.path && fileKey(packaged.path) === key && /^[A-F0-9]{64}$/.test(packaged.sha256) && packaged.sha256 === files.get(key)?.sha256 &&
        (packaged.allowedChangeReason === 'byte_identical' ? packaged.sourceSha256 === packaged.sha256 : packaged.allowedChangeReason === 'reference_rewrite' && packaged.sourceSha256 !== packaged.sha256) &&
        /^[A-F0-9]{64}$/.test(revision.newProductionSha256) && /^[A-F0-9]{64}$/.test(revision.oldSha256) &&
        revision.oldSha256 !== revision.newProductionSha256 && revision.hasSheetMetal === part.hasSheetMetal && revision.hasFlatPattern === (part.flatPatternNames.length > 0), 'production revision differs from final part health or package: ' + key)
      revisions.set(key, revision)
    }
    requireValue(revisions.size > 0, 'production revision coverage empty')
  }
  for (const [key, edit] of edits) {
    const part = [...parts.values()].find(row => fileKey(row.file) === key)
    requireValue(edit.file === leaf(edit.file) && part && edit.packageSha256 === files.get(key)?.sha256 && /^[A-F0-9]{64}$/.test(edit.sourceSha256) && typeof edit.role === 'string' && edit.role.length && typeof edit.hasSheetMetal === 'boolean' && typeof edit.hasFlatPattern === 'boolean', 'door part edit is not bound to final package: ' + key)
    const revision = revisions.get(key)
    if (revision) {
      const binding = Object.fromEntries(['taskId', 'planSha256', 'contractSha256', 'executableSha256', 'applyReceiptSha256', 'applyEvidenceSha256', 'verificationReceiptSha256', 'verificationEvidenceSha256'].map(field => [field, productionEvidence[field]]))
      for (const field of ['productionProfile', 'role', 'oldSha256', 'newProductionSha256', 'oldDoorSha256', 'doorEvidenceSha256', 'hasSheetMetal', 'hasFlatPattern']) binding[field] = revision[field]
      requireValue(revision.oldDoorSha256 === edit.sourceSha256 && /^[A-F0-9]{64}$/.test(revision.doorEvidenceSha256) &&
        edit.productionRevision && Object.keys(edit.productionRevision).length === Object.keys(binding).length &&
        Object.entries(binding).every(([field, value]) => edit.productionRevision[field] === value), 'door production revision binding mismatch: ' + key)
    } else requireValue(edit.sourceSha256 === edit.packageSha256 && edit.productionRevision === undefined, 'door part edit source must bind final package without an unverified revision: ' + key)
    requireValue(typeof edit.evidence === 'string' && edit.evidence === leaf(edit.evidence) && /^door-[a-zA-Z0-9_-]+\.json$/.test(edit.evidence), 'door part edit evidence identity invalid: ' + key)
    if (part.role === 'door_panel') {
      const expectedRoles = fileKey(part.file).startsWith('door_panel_left_') ? ['left_panel'] : fileKey(part.file).startsWith('door_panel_right_') ? ['right_panel_mirror_part', 'right_panel'] : []
      requireValue(expectedRoles.includes(edit.role), 'door panel edit handedness or role mismatch: ' + key)
    }
    if (part.role === 'door_stiffener') requireValue(edit.role === 'stiffener', 'door stiffener edit role mismatch: ' + key)
    requireValue(edit.hasSheetMetal === part.hasSheetMetal && edit.hasFlatPattern === (part.flatPatternNames.length > 0), 'door part edit health differs from final package: ' + key)
  }
  for (const part of parts.values()) if (part.role === 'door_panel' || part.role === 'door_stiffener') requireValue(edits.has(fileKey(part.file)), 'missing door part edit binding: ' + part.file)
  const instances = uniqueMap(coverage.instances, row => row.component, 'assembly instance')
  requireValue(instances.size > 0, 'assembly instance coverage empty')
  const includedBodies = new Map(), allBodies = new Map(), usedParts = new Map()
  for (const row of instances.values()) {
    requireValue(typeof row.component === 'string' && row.component.length && typeof row.configuration === 'string' && row.configuration.length && typeof row.hidden === 'boolean' && typeof row.suppressed === 'boolean' && Array.isArray(row.bodies), 'instance state or configuration missing')
    requireValue(['part', 'assembly'].includes(row.documentType), 'instance document type invalid: ' + row.component)
    requireValue(row.file === leaf(row.file) && files.get(fileKey(row.file))?.sha256 === row.sha256, 'instance package binding missing: ' + row.component)
    if (row.suppressed) {
      requireValue(row.disposition === 'excluded' && row.exclusionReason === 'suppressed_configuration' && row.solidBodyCount === 0 && row.bodies.length === 0, 'suppressed instance exclusion invalid: ' + row.component)
      if (row.documentType === 'assembly') requireValue(/\.SLDASM$/i.test(row.file), 'suppressed assembly file identity invalid: ' + row.component)
      else {
        const suppressedPart = parts.get(partKey(row))
        requireValue(/\.SLDPRT$/i.test(row.file) && suppressedPart && suppressedPart.role === partRole(row.file), 'suppressed part identity or role invalid: ' + row.component)
      }
      continue
    }
    if (row.documentType === 'assembly') {
      requireValue(/\.SLDASM$/i.test(row.file) && row.disposition === 'container' && row.solidBodyCount === 0 && row.bodies.length === 0, 'assembly container invalid: ' + row.component)
      continue
    }
    const part = parts.get(partKey(row))
    requireValue(part && row.solidBodyCount === part.solidBodyCount && row.bodies.length === part.solidBodyCount, 'instance body count differs from part configuration: ' + row.component)
    usedParts.set(partKey(row), part)
    if (referenceFiles.has(fileKey(row.file))) {
      requireValue(row.hidden === true && row.disposition === 'excluded' && row.exclusionReason === 'hidden_reference_model', 'reference exclusion lacks hidden proof: ' + row.component)
    } else requireValue(row.hidden === false && row.disposition === 'included' && row.exclusionReason === '', 'physical instance hidden or excluded: ' + row.component)
    const bodyIndices = new Set()
    for (const body of row.bodies) {
      requireValue(body.component === row.component && body.configuration === row.configuration && typeof body.bodyName === 'string' && body.bodyName.length && Number.isInteger(body.bodyIndex) && body.bodyIndex >= 0 && body.bodyIndex < row.solidBodyCount && !bodyIndices.has(body.bodyIndex) && boxValid(body.boxMm), 'instance body identity or bounds invalid: ' + row.component)
      bodyIndices.add(body.bodyIndex)
      const id = bodyId(body.id)
      requireValue(id === JSON.stringify([row.component, row.configuration, body.bodyName, body.bodyIndex]) && !allBodies.has(id), 'duplicate or changed body identity: ' + row.component)
      allBodies.set(id, body)
      if (row.disposition === 'included') includedBodies.set(id, body)
    }
  }
  for (const part of parts.values()) {
    const configs = [...parts.values()].filter(other => fileKey(other.file) === fileKey(part.file))
    requireValue(usedParts.has(partKey(part)) || configs.length === 1 && ![...usedParts.values()].some(other => fileKey(other.file) === fileKey(part.file)), 'unexpected part configuration: ' + partKey(part))
  }
  requireValue(includedBodies.size > 0, 'physical body coverage empty')
  return { files, parts, instances, includedBodies, allBodies, expectedFlats, edits }
}

function verifyPhysicalCoverage(candidate, { packageInventory, packageVerification, productionEvidence }) {
  try {
    requireValue(packageVerification?.success === true, 'independent package verification missing')
    const inventory = uniqueMap(packageInventory, row => fileKey(row.path), 'independent package inventory')
    requireValue(inventory.size > 0 && [...inventory.values()].every(row => /\.SLD(PRT|ASM)$/i.test(row.path) && /^[A-F0-9]{64}$/.test(row.sha256)), 'independent package inventory invalid')
    const actualCoverage = coverageRow(candidate, 'physical_audit'), expectedCoverage = coverageRow(packageVerification, 'package_verification')
    const actual = validateCoverageManifest(actualCoverage, inventory, productionEvidence), expected = validateCoverageManifest(expectedCoverage, inventory, productionEvidence)
    requireValue(JSON.stringify(actualCoverage.root) === JSON.stringify(expectedCoverage.root), 'root identity differs from independent verification')
    for (const field of ['parts', 'instances', 'allBodies', 'expectedFlats', 'edits']) sameIdentities(actual[field], expected[field], field)
    for (const [key, edit] of actual.edits) requireValue(JSON.stringify(edit) === JSON.stringify(expected.edits.get(key)), 'door part edit differs from independent verification: ' + key)
    for (const [key, part] of actual.parts) requireValue(JSON.stringify(part) === JSON.stringify(expected.parts.get(key)), 'part configuration differs from independent verification: ' + key)
    for (const [key, row] of actual.instances) {
      const { bodies: _a, ...a } = row, { bodies: _b, ...b } = expected.instances.get(key)
      requireValue(JSON.stringify(a) === JSON.stringify(b), 'instance state differs from independent verification: ' + key)
      if (!row.suppressed) {
        const measured = packageVerification.rows.filter(item => item.name === row.component)
        requireValue(measured.length === 1 && measured[0].worldBodyBoundsMm?.length === row.bodies.length && row.bodies.every((body, index) => sameBox(body.boxMm, measured[0].worldBodyBoundsMm[index])), 'independent measured instance bounds missing: ' + key)
      }
    }
    for (const [key, body] of actual.allBodies) requireValue(sameBox(body.boxMm, expected.allBodies.get(key).boxMm), 'body bounds differ from independent verification: ' + key)
    const bounds = candidate.rows.filter(row => row.inspection === 'native_world_body_bounds')
    requireValue(bounds.length === 1, 'world body bounds missing or duplicated')
    const bodies = uniqueMap(bounds[0].bodies, row => bodyId(row.id), 'measured world body')
    sameIdentities(bodies, actual.includedBodies, 'measured body')
    for (const [key, body] of bodies) {
      const expected = actual.includedBodies.get(key)
      requireValue(body.component === expected.component && body.configuration === expected.configuration && body.bodyName === expected.bodyName && body.bodyIndex === expected.bodyIndex && sameBox(body.boxMm, expected.boxMm), 'measured body identity or bounds differs from instance coverage: ' + key)
    }
    const measurements = candidate.rows.filter(row => row.inspection === 'native_exact_solid_interference')
    requireValue(measurements.length === 1, 'interference measurement missing or duplicated')
    const measured = measurements[0], eligible = new Map(), rows = [...bodies.values()]
    for (let i = 0; i < rows.length; i++) for (let j = i + 1; j < rows.length; j++) {
      const a = rows[i], b = rows[j]
      if (a.component === b.component || [0, 1, 2].some(axis => Math.min(a.boxMm[axis + 3], b.boxMm[axis + 3]) - Math.max(a.boxMm[axis], b.boxMm[axis]) < .001)) continue
      const pair = { bodyA: a.id, bodyB: b.id }; eligible.set(pairKey(pair), pair)
    }
    const checked = uniqueMap(measured.pairs, pairKey, 'checked body pair')
    sameIdentities(checked, eligible, 'checked body pair')
    requireValue(measured.bodyCount === bodies.size && measured.checkedPairs === eligible.size, 'body or checked-pair count mismatch')
    const contacts = uniqueMap(measured.collisions, pairKey, 'contact body pair')
    for (const [key, contact] of contacts) requireValue(eligible.has(key) && contact.componentA === bodies.get(bodyId(contact.bodyA))?.component && contact.componentB === bodies.get(bodyId(contact.bodyB))?.component && typeof contact.interference === 'boolean' && Number.isFinite(contact.volumeMm3) && contact.volumeMm3 >= 0 && Array.isArray(contact.intersectionBoundsMm) && contact.intersectionBoundsMm.every(boxValid), 'contact measurement does not bind a checked body pair')
    const flats = uniqueMap(candidate.rows.filter(row => row.inspection === 'native_flat_pattern'), flatKey, 'measured flat pattern')
    sameIdentities(flats, actual.expectedFlats, 'flat pattern')
    for (const [key, row] of flats) requireValue(row.file === leaf(row.file) && row.sha256 === actual.expectedFlats.get(key).sha256 && row.configuration === actual.expectedFlats.get(key).configuration && row.role === actual.expectedFlats.get(key).role, 'flat pattern package, configuration or role binding mismatch: ' + key)
    return { coverageVerified: true, coverageStatus: 'complete', coverageErrors: [], expectedFlatPatternCount: actual.expectedFlats.size, bodyCount: bodies.size, expectedCheckedPairs: eligible.size, excludedReferenceInstanceCount: [...actual.instances.values()].filter(row => row.exclusionReason === 'hidden_reference_model').length }
  } catch (error) { return { coverageVerified: false, coverageStatus: 'unverified', coverageErrors: [{ reason: error.message }] } }
}

function role(name) {
  const leaf = name.split('/').at(-1)
  if (/^(door_panel_|储物柜门板)/.test(leaf)) return 'door_panel'
  if (/^hinge_latch_plate_bottom/.test(leaf) || /^插销固定板-/.test(leaf)) return 'hinge_bottom'
  if (/^hinge_latch_plate_top/.test(leaf) || /^插销固定板\d/.test(leaf)) return 'hinge_top'
  if (/^(circlip_|开口挡圈)/.test(leaf)) return 'circlip'
  if (/^(door_hinge_pin|门轴销)/.test(leaf)) return 'hinge_pin'
  return leaf.replace(/-\d+$/, '')
}

function contactKey(contact) {
  const sameModule = contact.componentA.split('/')[0] === contact.componentB.split('/')[0]
  return [sameModule ? 'same_module' : 'different_modules', ...[role(contact.componentA), role(contact.componentB)].sort()].join('|')
}

function physicalHealth(reference, candidate, { doorCount } = {}) {
  if (reference.success !== true || candidate.success !== true) throw new Error('physical measurement run did not complete')
  const ref = reference.rows.find(row => row.inspection === 'native_exact_solid_interference')
  const current = candidate.rows.find(row => row.inspection === 'native_exact_solid_interference')
  if (!ref || !current || !Number.isInteger(doorCount) || doorCount < 2) throw new Error('physical evidence or expected door count missing')
  const referenceGroups = Map.groupBy(ref.collisions, contactKey)
  const groups = Map.groupBy(current.collisions, contactKey)
  const unexpected = []
  for (const [key, contacts] of groups) {
    const known = referenceGroups.get(key)
    if (!known) { unexpected.push({ key, reason: 'new_contact_category', contacts }); continue }
    const maximumVolume = Math.max(...known.map(contact => contact.volumeMm3)) + 0.001
    const countLimit = key.startsWith('same_module') ? doorCount : doorCount - 2
    const exceeded = contacts.filter(contact => !Number.isFinite(contact.volumeMm3) || contact.volumeMm3 > maximumVolume)
    if (exceeded.length || contacts.length > countLimit) unexpected.push({ key, reason: 'contact_exceeds_v35', countLimit, count: contacts.length, maximumVolume, contacts: exceeded })
  }
  const flats = candidate.rows.filter(row => row.inspection === 'native_flat_pattern')
  const flatErrors = flats.filter(row => row.rebuilt !== true || row.unsuppressed !== true || !Array.isArray(row.issues) || row.issues.length || !boxValid(row.flatBoxMm))
  return { measuredHealthPass: unexpected.length === 0 && flatErrors.length === 0, doorCount, referenceContactCount: ref.collisions.length, contactCount: current.collisions.length, comparedContactCategories: [...groups.keys()], unexpected, flatPatternCount: flats.length, flatErrors, boundary: 'static engineering-assistance comparison to V35; not a production release or hinge-sweep approval' }
}

export function compare16029PhysicalAudit(reference, candidate, options = {}) {
  const health = physicalHealth(reference, candidate, options)
  const coverage = verifyPhysicalCoverage(candidate, options)
  return { schema: PHYSICAL_QUALITY_SCHEMA, ...health, ...coverage, pass: health.measuredHealthPass && coverage.coverageVerified }
}

export function inspect16029LegacyPhysicalHealth(reference, candidate, options = {}) {
  const health = physicalHealth(reference, candidate, options)
  return { ...health, pass: false, legacyHealthPass: health.measuredHealthPass && health.flatPatternCount > 0, coverageVerified: false, coverageStatus: 'legacy_unverified' }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [refPath, candidatePath, doorCount, inventoryPath, verificationPath] = process.argv.slice(2)
  if (!refPath || !candidatePath || !doorCount || !inventoryPath || !verificationPath) throw new Error('Usage: node tools/verify_16029_parametric_physical_quality.mjs <v35-audit.json> <candidate-audit.json> <door-count> <package.json> <verify-package.json>')
  const read = path => JSON.parse(readFileSync(path, 'utf8'))
  const result = compare16029PhysicalAudit(read(refPath), read(candidatePath), { doorCount: Number(doorCount), packageInventory: read(inventoryPath).rows, packageVerification: read(verificationPath) })
  console.log(JSON.stringify(result, null, 2))
  if (!result.pass) process.exitCode = 1
}
