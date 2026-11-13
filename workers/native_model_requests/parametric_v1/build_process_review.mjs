import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync, copyFileSync, existsSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { current16029ProcessReviewSources } from './process_review_source.mjs'
import { verify16029ParametricBuild } from '../../../tools/lib/locker_16029_parametric_build.mjs'
import { INDOOR_16029_PROCESS_REVIEW_PROFILE, INDOOR_16029_PROCESS_REVIEW_BUILD, INDOOR_16029_PROCESS_REVIEW_BASE } from '../../../tools/lib/locker_16029_production_profile.mjs'

const folder = dirname(fileURLToPath(import.meta.url)), root = resolve(folder, '../../..')
const tag = process.argv[2]
if (tag !== INDOOR_16029_PROCESS_REVIEW_BUILD) throw new Error('explicit recognized process review build tag required')
const base = verify16029ParametricBuild(INDOOR_16029_PROCESS_REVIEW_BASE, { root, repairProfile: INDOOR_16029_PROCESS_REVIEW_BASE })
const bin = resolve(folder, 'bin-' + tag), generated = resolve(folder, 'build-' + tag)
if (existsSync(bin) || existsSync(generated)) throw new Error('process build outputs already exist; use a new version instead of overwriting evidence')
const sources = current16029ProcessReviewSources(root)
mkdirSync(bin); mkdirSync(generated)
for (const [name, source] of sources) writeFileSync(resolve(generated, name), source, 'utf8')
for (const name of ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']) copyFileSync(resolve(base.folder, name), resolve(bin, name))
const executable = resolve(bin, 'NativeParametricAssembly.exe')
const args = ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/main:Winnsen.StructureAgent.Generation.NativeParametricAssembly', '/out:' + executable,
  '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
  ...['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll'].map(name => '/reference:' + resolve(bin, name)), ...[...sources.keys()].map(name => resolve(generated, name))]
const result = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { encoding: 'utf8', windowsHide: true })
if (result.error || result.status !== 0) throw new Error([result.error?.message, result.stdout, result.stderr].filter(Boolean).join('\n'))
const sha = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const manifest = { productionProfile: INDOOR_16029_PROCESS_REVIEW_PROFILE, processReviewProvenance: {
  schema: 'winnsen.locker16029.process_review_source.v1', baseBuildTag: base.tag, baseExecutableSha256: base.manifest.executableSha256,
  baseManifestSha256: sha(readFileSync(resolve(base.folder, 'build.json'))), productionRelease: false,
}, executable, executableSha256: sha(readFileSync(executable)), sources: [...sources].map(([name, source]) => ({
  path: resolve(generated, name), sha256: sha(readFileSync(resolve(generated, name))), normalizedSha256: sha(source),
})) }
manifest.sourceHashes = manifest.sources.map(row => ({ name: row.path.split(/[\\/]/).at(-1), normalizedSha256: row.normalizedSha256 }))
writeFileSync(resolve(bin, 'build.json'), JSON.stringify(manifest, null, 2) + '\n')
console.log(JSON.stringify({ compiled: true, tag, executableSha256: manifest.executableSha256, base: base.tag, productionRelease: false }))
