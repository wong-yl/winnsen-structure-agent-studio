import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync, lstatSync, readdirSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'

const folder = dirname(fileURLToPath(import.meta.url))
const nativeFolder = resolve(folder, '../..')
const tag = 'readonly-open-20261107-r5-ownerreplace6b'
const baseline = resolve(nativeFolder, 'bin-engineer-feedback-20261008-r4')
const bin = resolve(nativeFolder, 'bin-' + tag)
const generated = resolve(nativeFolder, 'build-' + tag)
const sourceDirectory = resolve(folder, 'source')
const names = ['NativeParametricWidth.cs', 'NativeParametricDoor.cs', 'NativeParametricLockBase.cs', 'ParametricContext.cs', 'NativeParametricAssembly.cs', 'NativeParametricVerification.cs', 'NativeParametricLocks.cs', 'NativeParametricPhysicalAudit.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricEnvelope.cs']
const changed = new Set(['NativeParametricAssembly.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricPhysicalAudit.cs', 'NativeParametricDoor.cs', 'NativeParametricWidth.cs', 'NativeParametricLocks.cs'])
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const normalizedHash = path => createHash('sha256').update(readFileSync(path, 'utf8').replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')).digest('hex').toUpperCase()
const requireValue = (value, message) => { if (!value) throw new Error(message) }
const normalize = text => text.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
function lockBaseSaveGuardsOnly(source, baselineSource) {
  const text = normalize(readFileSync(source, 'utf8'))
  const restored = text.replace('Winnsen.StructureAgent.Generation.NativeParametricAssembly.GuardedSave3(model, ', 'model.Save3(')
    .replace('Winnsen.StructureAgent.Generation.NativeParametricAssembly.GuardedSaveAs(temporaryRight, ', 'temporaryRight.Extension.SaveAs(')
  return restored !== text && restored === normalize(readFileSync(baselineSource, 'utf8'))
}
function locksSaveGuardsOnly(source, baselineSource) {
  const text = normalize(readFileSync(source, 'utf8'))
  const restored = text.replace('NativeParametricAssembly.GuardedSaveAs(model,rightSource,', 'model.Extension.SaveAs(rightSource,')
    .replace('NativeParametricAssembly.GuardedSaveAs(mirrored,built,', 'mirrored.Extension.SaveAs(built,')
  return restored !== text && restored === normalize(readFileSync(baselineSource, 'utf8'))
}
function auxiliarySaveGuardOnly(source, baselineSource) {
  const text = normalize(readFileSync(source, 'utf8'))
  const restored = text.replace('GuardedAuxiliarySave("SavePackAndGo", output, delegate { return model.Extension.SavePackAndGo(pack); })', 'model.Extension.SavePackAndGo(pack)')
    .replace('(bool)NativeParametricAssembly.GuardedAuxiliarySave("SketchBlockDefinition.Save",seed.path,delegate { return instance.Definition.Save(seed.path); })', 'instance.Definition.Save(seed.path)')
  return restored !== text && restored === normalize(readFileSync(baselineSource, 'utf8'))
}
const baselineManifestSha256 = 'F7E382224436CD52E03BBFA966BE3DF8845015E1C69841DD0DFC41BEA3A0AE80'
const baselineExecutableSha256 = '1D7CFDA1EDB0442E22E3BC45938AF9F6673A7D337FDF29B3DF95B6AE275FD11D'
requireValue(!existsSync(bin) && !existsSync(generated), 'repair build refuses to overwrite an existing build')
requireValue(hash(resolve(baseline, 'build.json')) === baselineManifestSha256, 'baseline manifest changed')
requireValue(hash(resolve(baseline, 'NativeParametricAssembly.exe')) === baselineExecutableSha256, 'baseline executable changed')
requireValue(readdirSync(sourceDirectory).length === names.length, 'repair source coverage changed')
const original = JSON.parse(readFileSync(resolve(baseline, 'build.json'), 'utf8'))
for (const name of names) {
  const source = resolve(sourceDirectory, name)
  const stat = lstatSync(source)
  requireValue(stat.isFile() && !stat.isSymbolicLink() && stat.nlink === 1, 'repair source is not an ordinary file: ' + name)
  const baselineSource = resolve(baseline, 'source-snapshot', name)
  const row = original.sources.find(row => row.path.replaceAll('\\', '/').split('/').at(-1) === name)
  requireValue(row && hash(baselineSource) === row.sha256, 'baseline source changed: ' + name)
  requireValue(changed.has(name) || hash(source) === row.sha256 || (name === 'NativeParametricLockBase.cs' && lockBaseSaveGuardsOnly(source, baselineSource)) || (name === 'NativeParametricLocks.cs' && locksSaveGuardsOnly(source, baselineSource)) || (['NativeParametricVerification.cs', 'NativeParametricEnvelope.cs'].includes(name) && auxiliarySaveGuardOnly(source, baselineSource)), 'unrelated repair source change: ' + name)
}
const interop = process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR
  ? resolve(process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR)
  : resolve(process.env.ProgramFiles || 'C:/Program Files', 'SOLIDWORKS Corp/SOLIDWORKS/api/redist')
for (const name of ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']) requireValue(existsSync(resolve(interop, name)), 'missing SDK DLL; set WINNSEN_SOLIDWORKS_INTEROP_DIR: ' + name)
mkdirSync(bin)
mkdirSync(generated)
for (const name of names) copyFileSync(resolve(sourceDirectory, name), resolve(generated, name))
for (const name of ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll']) copyFileSync(resolve(interop, name), resolve(bin, name))
const executable = resolve(bin, 'NativeParametricAssembly.exe')
const args = ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001',
  '/main:Winnsen.StructureAgent.Generation.NativeParametricAssembly', '/out:' + executable,
  '/reference:System.Management.dll', '/reference:System.Web.Extensions.dll',
  '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
  '/reference:' + resolve(bin, 'SolidWorks.Interop.sldworks.dll'), '/reference:' + resolve(bin, 'SolidWorks.Interop.swconst.dll'),
  ...names.map(name => resolve(generated, name))]
const result = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { encoding: 'utf8', windowsHide: true })
requireValue(result.status === 0, result.stdout + result.stderr)
const sources = names.map(name => ({ path: resolve(generated, name), sha256: hash(resolve(generated, name)), normalizedSha256: normalizedHash(resolve(generated, name)) }))
const manifest = { executable, executableSha256: hash(executable), sources,
  sourceHashes: names.map((name, i) => ({ name, normalizedSha256: sources[i].normalizedSha256 })),
  repairProfile: tag,
  repairProvenance: { schema: 'winnsen.locker16029.parametric_repair_source.v1', baselineBuildTag: 'engineer-feedback-20261008-r4', baselineManifestSha256, baselineExecutableSha256, sourceDirectory } }
writeFileSync(resolve(bin, 'build.json'), JSON.stringify(manifest, null, 2) + '\n')
process.stdout.write(JSON.stringify({ compiled: true, tag, executable, executableSha256: manifest.executableSha256, manifestSha256: hash(resolve(bin, 'build.json')) }) + '\n')
