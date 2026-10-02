import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { adaptDoorSource, DOOR_SOURCE_PATH } from './door_adapter.mjs'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from './width_adapter.mjs'
import { adaptLockSource, LOCK_SOURCE_PATH } from './lock_adapter.mjs'

const folder = dirname(fileURLToPath(import.meta.url))
const root = resolve(folder, '../../..')
const buildTag = process.argv[2] || ''
if (buildTag && !/^[a-zA-Z0-9_-]+$/.test(buildTag)) throw new Error('invalid native build tag')
const bin = resolve(folder, buildTag ? 'bin-' + buildTag : 'bin')
const generated = resolve(folder, buildTag ? 'build-' + buildTag : 'build')
const interop = process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR
  ? resolve(process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR)
  : resolve(process.env.ProgramFiles || 'C:/Program Files', 'SOLIDWORKS Corp/SOLIDWORKS/api/redist')
const interopFiles = ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']
if (interopFiles.some(file => !existsSync(resolve(interop, file)))) {
  throw new Error('SOLIDWORKS SDK interop DLLs are required; set WINNSEN_SOLIDWORKS_INTEROP_DIR to the local api/redist directory')
}
mkdirSync(bin, { recursive: true })
mkdirSync(generated, { recursive: true })
const widthPath = resolve(root, WIDTH_SOURCE_PATH)
const sources = [
  [resolve(generated, 'NativeParametricWidth.cs'), adaptWidthSource(readFileSync(widthPath, 'utf8'))],
  [resolve(generated, 'NativeParametricDoor.cs'), adaptDoorSource(readFileSync(resolve(root, DOOR_SOURCE_PATH), 'utf8'), 'A07174DEDF4F9F0958A731E679655B742E53BEDA063709F5F2F10BF2376572B0')],
  [resolve(generated, 'NativeParametricLockBase.cs'), adaptLockSource(readFileSync(resolve(root, LOCK_SOURCE_PATH), 'utf8'))],
]
for (const [path, text] of sources) writeFileSync(path, text, 'utf8')
for (const file of interopFiles) copyFileSync(resolve(interop, file), resolve(bin, file))
const executable = resolve(bin, 'NativeParametricAssembly.exe')
const args = [
  '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001',
  '/main:Winnsen.StructureAgent.Generation.NativeParametricAssembly', `/out:${executable}`,
  '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll',
  '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
  `/reference:${resolve(bin, 'SolidWorks.Interop.sldworks.dll')}`, `/reference:${resolve(bin, 'SolidWorks.Interop.swconst.dll')}`,
  ...sources.map(([path]) => path), resolve(folder, 'ParametricContext.cs'), resolve(folder, 'NativeParametricAssembly.cs'), resolve(folder, 'NativeParametricVerification.cs'), resolve(folder, 'NativeParametricLocks.cs'), resolve(folder, 'NativeParametricPhysicalAudit.cs'), resolve(folder, 'NativeParametricDoorPlacement.cs'), resolve(folder, 'NativeParametricEnvelope.cs'),
]
const result = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { encoding: 'utf8', windowsHide: true })
if (result.status !== 0) throw new Error(result.stdout + result.stderr)
const hash = (path) => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const manifest = { executable, executableSha256: hash(executable), sources: [...sources.map(([path]) => path), ...['ParametricContext.cs','NativeParametricAssembly.cs','NativeParametricVerification.cs','NativeParametricLocks.cs','NativeParametricPhysicalAudit.cs','NativeParametricDoorPlacement.cs','NativeParametricEnvelope.cs'].map(file => resolve(folder,file))].map(path => ({ path, sha256: hash(path) })) }
writeFileSync(resolve(bin, 'build.json'), JSON.stringify(manifest, null, 2) + '\n')
process.stdout.write(JSON.stringify({ compiled: true, ...manifest }) + '\n')
