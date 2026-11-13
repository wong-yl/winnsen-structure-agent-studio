import { createHash } from 'node:crypto'
import { copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs'
import { basename, dirname, isAbsolute, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

export const PRODUCTION_DRAWINGS_BUILD_SCHEMA = 'winnsen.locker16029.production_drawings_build.v1'
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const SOURCE = resolve(ROOT, 'workers/native_model_requests/parametric_v1/ProductionOrderDrawings.cs')
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
const requireValue = (value, message) => { if (!value) throw new Error(message) }

function plainPath(path, file = false, allowHardlinks = false) {
  requireValue(typeof path === 'string' && isAbsolute(path), 'drawing build path must be absolute')
  let current = resolve(path)
  while (true) {
    const stat = lstatSync(current)
    requireValue(!stat.isSymbolicLink() && (!stat.isFile() || stat.nlink === 1 || allowHardlinks), 'drawing build refuses linked paths: ' + current)
    if (current === resolve(path)) requireValue(file ? stat.isFile() : stat.isDirectory(), 'invalid drawing build path type')
    const parent = dirname(current)
    if (parent === current) break
    current = parent
  }
  return realpathSync.native(resolve(path))
}

export function build16029ProductionDrawings(outputDirectory, {
  interopDirectory = process.env.WINNSEN_SOLIDWORKS_INTEROP_DIR || 'D:/soildworks2020/SOLIDWORKS/api/redist',
  compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe',
} = {}) {
  requireValue(typeof outputDirectory === 'string' && isAbsolute(outputDirectory), 'fresh absolute drawing build directory required')
  const requestedOutput = resolve(outputDirectory)
  requireValue(!existsSync(requestedOutput), 'preserve existing drawing build directory: ' + requestedOutput)
  const output = resolve(plainPath(dirname(requestedOutput)), basename(requestedOutput))
  plainPath(SOURCE, true)
  const compiler = plainPath(compilerPath, true, true)
  const sourceSha256 = hash(SOURCE)
  const interop = ['SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll'].map(name => {
    const path = plainPath(resolve(interopDirectory, name), true)
    return { name, path, sha256: hash(path) }
  })
  mkdirSync(output)
  const copiedSource = resolve(output, 'ProductionOrderDrawings.cs')
  copyFileSync(SOURCE, copiedSource, 1)
  for (const row of interop) copyFileSync(row.path, resolve(output, row.name), 1)
  requireValue(hash(copiedSource) === sourceSha256 && interop.every(row => hash(resolve(output, row.name)) === row.sha256), 'drawing build input copy mismatch')
  const executable = resolve(output, 'ProductionOrderDrawings.exe')
  const compiled = spawnSync(compiler, [
    '/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/utf8output',
    '/main:Winnsen.StructureAgent.Generation.ProductionOrderDrawings', '/out:' + executable,
    '/reference:System.Web.Extensions.dll',
    ...interop.map(row => '/reference:' + resolve(output, row.name)), copiedSource,
  ], { encoding: 'utf8', windowsHide: true })
  requireValue(!compiled.error && compiled.status === 0 && existsSync(executable), 'drawing static compilation failed: ' + (compiled.error?.message || '') + '\n' + compiled.stdout + compiled.stderr)
  requireValue(hash(SOURCE) === sourceSha256 && hash(copiedSource) === sourceSha256, 'drawing source changed during compilation')
  const manifest = {
    schema: PRODUCTION_DRAWINGS_BUILD_SCHEMA, classification: 'engineering-review-only', productionReleased: false,
    compiledAt: new Date().toISOString(), executable, executableSha256: hash(executable),
    source: { path: SOURCE, sha256: sourceSha256 }, copiedSource: { path: copiedSource, sha256: hash(copiedSource) },
    compiler: { path: compiler, sha256: hash(compiler) },
    interop: interop.map(row => ({ ...row, copiedPath: resolve(output, row.name), copiedSha256: hash(resolve(output, row.name)) })),
    compilerOutput: (compiled.stdout + compiled.stderr).trim(), cadStarted: false,
  }
  writeFileSync(resolve(output, 'build.json'), JSON.stringify(manifest, null, 2) + '\n', { flag: 'wx' })
  return manifest
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2)
    if (args.length === 1 && args[0] === '--help') console.log('node tools/build_16029_production_drawings.mjs <fresh-absolute-build-directory>\nStatic compilation only; does not start CAD.')
    else {
      requireValue(args.length === 1, 'usage: node tools/build_16029_production_drawings.mjs <fresh-absolute-build-directory>')
      const manifest = build16029ProductionDrawings(args[0])
      console.log(JSON.stringify({ compiled: true, executable: manifest.executable, executableSha256: manifest.executableSha256, sourceSha256: manifest.source.sha256, cadStarted: false }))
    }
  } catch (error) { console.error(error.message); process.exitCode = 1 }
}
