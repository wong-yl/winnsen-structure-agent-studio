import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

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

export function compare16029PhysicalAudit(reference, candidate, { doorCount } = {}) {
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
  const flatErrors = flats.filter(row => row.rebuilt !== true || row.unsuppressed !== true || row.issues?.length || !Array.isArray(row.flatBoxMm) || row.flatBoxMm.length !== 6 || !row.flatBoxMm.every(Number.isFinite))
  return { pass: unexpected.length === 0 && flatErrors.length === 0 && flats.length > 0, doorCount, referenceContactCount: ref.collisions.length, contactCount: current.collisions.length, comparedContactCategories: [...groups.keys()], unexpected, flatPatternCount: flats.length, flatErrors, boundary: 'static engineering-assistance comparison to V35; not a production release or hinge-sweep approval' }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [refPath, candidatePath, doorCount] = process.argv.slice(2)
  if (!refPath || !candidatePath || !doorCount) throw new Error('Usage: node tools/verify_16029_parametric_physical_quality.mjs <v35-audit.json> <candidate-audit.json> <door-count>')
  const result = compare16029PhysicalAudit(JSON.parse(readFileSync(refPath, 'utf8')), JSON.parse(readFileSync(candidatePath, 'utf8')), { doorCount: Number(doorCount) })
  console.log(JSON.stringify(result, null, 2))
  if (!result.pass) process.exitCode = 1
}
