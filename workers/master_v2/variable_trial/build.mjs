import { copyFileSync, readdirSync, constants } from 'node:fs'
import { resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { SOURCE, DATA, TOOLCHAIN_SCHEMA, INTEROP_DLLS, inside, directory, hash, write, validateToolchain } from './common.mjs'

export function archiveSources(sourceRoot, bin, { dataRoot = DATA } = {}) {
  const names = readdirSync(inside(sourceRoot, sourceRoot)).filter(name => name.endsWith('.cs')).sort()
  if (!names.length) throw new Error('NO_NATIVE_SOURCES')
  directory(join(bin, 'sources'), dataRoot, true)
  return names.map(name => {
    const path = inside(join(sourceRoot, name), sourceRoot), archivePath = inside(join(bin, 'sources', name), dataRoot), sha256 = hash(path)
    copyFileSync(path, archivePath, constants.COPYFILE_EXCL)
    if (hash(archivePath) !== sha256 || hash(path) !== sha256) throw new Error('SOURCE_BYTES_CHANGED ' + path)
    return { path, archivePath, sha256 }
  })
}

export function buildToolchain() {
  const bin = directory(resolve(DATA, 'toolchain', new Date().toISOString().replace(/[:.]/g, '-') + '-' + randomBytes(4).toString('hex')), DATA, true)
  const sources = archiveSources(SOURCE, bin)
  const dlls = INTEROP_DLLS.map(name => {
    const source = inside(resolve('D:/soildworks2020/SOLIDWORKS/api/redist', name), 'D:/soildworks2020/SOLIDWORKS/api/redist')
    const path = inside(join(bin, name), DATA), sha256 = hash(source)
    copyFileSync(source, path, constants.COPYFILE_EXCL)
    if (hash(path) !== sha256 || hash(source) !== sha256) throw new Error('DLL_COPY_CHANGED ' + name)
    return { path, sha256 }
  })
  const exe = inside(join(bin, 'VariableTrial.exe'), DATA)
  const args = ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/out:' + exe,
    '/reference:System.Web.Extensions.dll', '/reference:System.Management.dll', ...dlls.map(item => '/reference:' + item.path), ...sources.map(item => item.archivePath)]
  const result = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { windowsHide: true, encoding: 'utf8' })
  if (result.error || result.status !== 0) throw new Error(result.error?.message || String(result.stdout) + String(result.stderr))
  const manifest = { schema: TOOLCHAIN_SCHEMA, exe, sha256: hash(exe), sources, dlls }
  validateToolchain(manifest)
  write(join(bin, 'manifest.json'), manifest)
  write(resolve(DATA, 'toolchain-current.json'), manifest)
  return manifest
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(buildToolchain())) } catch (error) { console.error(error); process.exitCode = 1 }
}
