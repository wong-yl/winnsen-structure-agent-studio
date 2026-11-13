// Throwaway diagnostic CLI. No stage pointer or engineering acceptance is produced.
import { join, resolve } from 'node:path'
import { randomBytes } from 'node:crypto'
import { DATA, ROOT, directory, inside, read, write, hash, copyTree, compare, inventory, files } from './common.mjs'
import { runProtectedPhase } from './run.mjs'

const [mode, sourceRun] = process.argv.slice(2)
if (!['A', 'B'].includes(mode)) throw new Error('USAGE: node probe_structure.mjs <A|B> <failed-case3-run>')
const run = inside(resolve(sourceRun), join(DATA, 'runs'))
const evidence = join(run, 'cad/assemblies/case3/assembly-evidence.json'), prior = read(evidence)
if (prior.success !== false || !String(prior.error).includes('ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED') || !String(prior.error).includes('门框 下.sldprt')) throw new Error('EXACT_FAILED_BOTTOM_FRAME_REQUIRED')
const closure = inside(join(run, 'cad/assemblies/case3/structure'), run), input = inventory(files(closure))
const staging = directory(join(DATA, 'runs', new Date().toISOString().replace(/[:.]/g, '-') + '-probe-source-' + randomBytes(4).toString('hex')), DATA, true)
const copy = copyTree(closure, join(staging, 'cad'), { sourceRoot: ROOT })
write(join(staging, 'diagnostic-input.json'), { mode, evidence, evidenceSha256: hash(evidence), copy, production_release: false, stageAcceptance: false })
try {
  console.log(JSON.stringify(await runProtectedPhase({ tag: 'bottom-frame-probe-' + mode, sourceDirectory: join(staging, 'cad'), job: { action: 'structure-refresh-diagnostic', sourceFirst: mode === 'B', production_release: false, diagnosticOnly: true, sourceEvidence: { path: evidence, sha256: hash(evidence) } } })))
} finally {
  const changed = compare(input)
  write(join(staging, 'source-unchanged.json'), { pass: changed.length === 0, changed, production_release: false })
  if (changed.length) throw new Error('PROBE_ORIGINAL_CLOSURE_CHANGED ' + JSON.stringify(changed))
}
