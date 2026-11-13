import { readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { current16029ProductionSources, PRODUCTION_PROCESS_VERSION } from './production_source.mjs'

const folder = dirname(fileURLToPath(import.meta.url)), root = resolve(folder, '../../..')
const tag = process.argv[2]
if (!/^production-20261029-[a-z0-9_-]+$/.test(tag || '')) throw new Error('explicit candidate production tag required')
const bin = resolve(folder, 'bin-' + tag), generated = resolve(folder, 'build-' + tag)
mkdirSync(bin, { recursive:true }); mkdirSync(generated, { recursive:true })
const sources = current16029ProductionSources(root)
for (const [name, source] of sources) writeFileSync(resolve(generated, name), source, 'utf8')
const interop = process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR || 'D:/soildworks2020/SOLIDWORKS/api/redist'
for (const name of ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']) copyFileSync(resolve(interop, name), resolve(bin, name))
const executable = resolve(bin, 'NativeParametricAssembly.exe')
const args = ['/nologo','/target:exe','/platform:x64','/optimize+','/codepage:65001','/main:Winnsen.StructureAgent.Generation.NativeParametricAssembly', '/out:' + executable,
  '/reference:System.Management.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll',
  ...['SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll'].map(name=>'/reference:' + resolve(bin,name)), ...[...sources.keys()].map(name=>resolve(generated,name))]
const compiled = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { encoding:'utf8', windowsHide:true })
if (compiled.error || compiled.status !== 0) throw new Error([compiled.error?.message, compiled.stdout, compiled.stderr].filter(Boolean).join('\n'))
const hash = text => createHash('sha256').update(text).digest('hex').toUpperCase()
const manifest = { productionProfile:PRODUCTION_PROCESS_VERSION, executable, executableSha256:hash(readFileSync(executable)), sources:[...sources].map(([name, source])=>({path:resolve(generated,name),sha256:hash(readFileSync(resolve(generated,name))),normalizedSha256:hash(source.replace(/^\uFEFF/,'').replaceAll('\r\n','\n'))})) }
manifest.sourceHashes = manifest.sources.map(row=>({name:row.path.split(/[\\/]/).at(-1),normalizedSha256:row.normalizedSha256}))
writeFileSync(resolve(bin,'build.json'),JSON.stringify(manifest,null,2)+'\n')
console.log(JSON.stringify({compiled:true,tag,sha256:manifest.executableSha256,profile:manifest.productionProfile}))
