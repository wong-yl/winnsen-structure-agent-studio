import { createHash } from 'node:crypto'

// Synthetic evidence for validator behavior; this fixture never opens CAD.
export function build16029PhysicalAuditFixture({ sheetCount = 2, separated = false, withReference = true } = {}) {
  const content = file => 'synthetic CAD fixture: ' + file + '\n'
  const sha256 = file => createHash('sha256').update(content(file)).digest('hex').toUpperCase()
  const root = { file: '16029_fixture.SLDASM', sha256: sha256('16029_fixture.SLDASM'), configuration: 'Default' }
  const packageFiles = [{ file: root.file, sha256: root.sha256 }]
  const parts = [], instances = [], flats = []
  function addPart(file, role, sheet, excluded = false) {
    const configuration = 'Default', index = parts.length
    const flatPatternNames = sheet && !excluded ? ['Flat-Pattern1'] : []
    const part = { file, sha256: sha256(file), configuration, role, exclusionReason: excluded ? 'reference_model_not_manufactured' : '', solidBodyCount: 1, hasSheetMetal: sheet, requiredFlatPattern: flatPatternNames.length > 0, flatPatternNames }
    packageFiles.push({ file, sha256: part.sha256 }); parts.push(part)
    const component = file.replace(/\.sldprt$/i, '') + '-1'
    const x = separated ? index * 30 : index * .1
    const body = { id: JSON.stringify([component, configuration, 'Solid1', 0]), component, configuration, bodyName: 'Solid1', bodyIndex: 0, boxMm: [x, 0, 0, x + 20, 20, 1] }
    instances.push({ component, file, sha256: part.sha256, configuration, documentType: 'part', disposition: excluded ? 'excluded' : 'included', exclusionReason: excluded ? 'hidden_reference_model' : '', suppressed: false, hidden: excluded, solidBodyCount: 1, bodies: [body] })
    for (const feature of flatPatternNames) flats.push({ inspection: 'native_flat_pattern', file, sha256: part.sha256, configuration, role, feature, unsuppressed: true, rebuilt: true, flatBoxMm: [0, 0, 0, 20, 30, 1], issues: [] })
  }
  for (let index = 0; index < sheetCount; index++) addPart('door_panel_left_W317_H' + (100 + index) + '.SLDPRT', 'door_panel', true)
  addPart('circlip_W317_H100.SLDPRT', 'circlip', false)
  if (withReference) addPart('标准寄存柜 模型.SLDPRT', 'reference_model', true, true)
  const bodies = instances.filter(row => row.disposition === 'included').flatMap(row => row.bodies)
  const pairs = []
  for (let i = 0; i < bodies.length; i++) for (let j = i + 1; j < bodies.length; j++) {
    const a = bodies[i], b = bodies[j]
    if (a.component === b.component || [0, 1, 2].some(axis => Math.min(a.boxMm[axis + 3], b.boxMm[axis + 3]) - Math.max(a.boxMm[axis], b.boxMm[axis]) < .001)) continue
    pairs.push({ bodyA: a.id, bodyB: b.id })
  }
  const partEdits = parts.filter(part => part.role === 'door_panel').map(part => ({ evidence: 'door-fixture.json', file: part.file, role: 'left_panel', hasSheetMetal: true, hasFlatPattern: true, sourceSha256: part.sha256, packageSha256: part.sha256 }))
  const coverage = { inspection: 'native_physical_coverage', schema: 'winnsen.locker16029.physical_coverage.v1', purpose: 'physical_audit', root, packageFiles, parts, instances, partEdits, pairFilter: { minimumOverlapMm: .001, excludeSameInstance: true } }
  const candidate = { phase: 'physical-audit', success: true, rows: [
    { inspection: 'native_world_body_bounds', bodies },
    { inspection: 'native_exact_solid_interference', bodyCount: bodies.length, checkedPairs: pairs.length, pairs, collisions: [] },
    coverage, ...flats,
  ] }
  const packageVerification = { phase: 'verify-package', success: true, rows: [
    ...instances.map(row => ({ name: row.component, path: '/fixture/package/' + row.file, issues: [], worldBodyBoundsMm: row.bodies.map(body => body.boxMm) })),
    { ...structuredClone(coverage), purpose: 'package_verification' },
  ] }
  const reference = { success: true, rows: [{ inspection: 'native_exact_solid_interference', collisions: [] }] }
  const packageInventory = packageFiles.map(row => ({ path: '/fixture/package/' + row.file, sha256: row.sha256 }))
  return { reference, candidate, options: { doorCount: 6, packageInventory, packageVerification }, content }
}
