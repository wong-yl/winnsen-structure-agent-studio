import { createHash } from 'node:crypto'
import { existsSync, lstatSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { NATIVE_TOOLCHAIN_MANIFEST_PATHS } from './locker_16029_native_stage_contract.mjs'

const DEFAULT_REPOSITORY_ROOT = resolve(fileURLToPath(new URL('../..', import.meta.url)))
const STANDARD_SCHEMA = 'winnsen.16029.native_toolchain_manifest.v1'
const LOCK_SCHEMA = 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1'
const STANDARD_TOOL_ARTIFACTS = Object.freeze({
  native_seed_pack_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.exe',
    verifierPath: 'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1',
  }),
  native_width_888_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.exe',
    verifierPath: 'workers/native_model_requests/development/v1/tools/width_888_native_v1/verify_static.mjs',
  }),
  native_door_module_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.exe',
    verifierPath: 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/Verify-NativeDoorModule888x14.ps1',
  }),
  native_root_assembly_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/NativeRootAssembly888x14.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/NativeRootAssembly888x14.exe',
    verifierPath: 'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/verify_static.mjs',
  }),
  native_final_pack_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/FinalPack888x14Native.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/FinalPack888x14Native.exe',
    verifierPath: 'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/verify_static.mjs',
  }),
})
const LOCK_TOOL_ARTIFACTS = Object.freeze({
  native_lock_topology_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/BuildLockTopology888x14.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/bin/BuildLockTopology888x14.exe',
  }),
  native_lock_topology_inspector_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/InspectLockTopology888x14.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/bin/InspectLockTopology888x14.exe',
  }),
  native_assembly_tongue_inspector_888x14_v1: Object.freeze({
    sourcePath: 'workers/native_model_requests/development/v1/tools/InspectAssemblyTongues888x14.cs',
    executablePath: 'workers/native_model_requests/development/v1/tools/bin/InspectAssemblyTongues888x14.exe',
  }),
})
const LOCK_VALIDATOR_PATH = 'workers/native_model_requests/development/v1/tools/ValidateLockTopology888x14.mjs'

function fail(code, message) {
  const error = new Error(message)
  error.code = code
  throw error
}

function underRoot(candidate, root) {
  const rel = relative(resolve(root), resolve(candidate))
  return rel === '' || (!rel.startsWith('..') && !isAbsolute(rel))
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex').toUpperCase()
}

function exactKeys(value, expected) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
}

function normalizedStandardSourceSha256(path) {
  let source = readFileSync(path, 'utf8').replace(/\r\n?/g, '\n')
  source = source.replace(
    /(private const string ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/,
    '$1__SOURCE_SHA256__$2',
  )
  source = source.replace(
    /(private const string ExpectedContractSnapshotSha256\s*=\s*")(?:__CONTRACT_SHA256__|[A-F0-9]{64})(";)/,
    '$1__CONTRACT_SHA256__$2',
  )
  return sha256(Buffer.from(source, 'utf8'))
}

function normalizedLockSourceSha256(path) {
  const source = readFileSync(path, 'utf8').replace(
    /(private const string ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)/,
    '$1__SOURCE_SHA256__$2',
  )
  return sha256(Buffer.from(source, 'utf8'))
}

function resolveRegisteredArtifact(repositoryRoot, registeredRelativePath, actualPath, label, { absolute = false } = {}) {
  const normalized = String(actualPath || '')
  if (absolute ? !isAbsolute(normalized) : isAbsolute(normalized)) {
    fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', `${label} path kind is invalid`)
  }
  const expected = resolve(repositoryRoot, registeredRelativePath)
  const actual = absolute ? resolve(normalized) : resolve(repositoryRoot, normalized)
  if (actual !== expected) fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', `${label} path is not the registered artifact`)
  assertSafeManifestPath(actual, repositoryRoot)
  return actual
}

function validateStandardManifest(manifest, toolId, repositoryRoot) {
  const expected = STANDARD_TOOL_ARTIFACTS[toolId]
  const tool = manifest?.tool
  if (!expected || !exactKeys(manifest, ['schema', 'generatedBy', 'tool']) ||
      manifest.schema !== STANDARD_SCHEMA || typeof manifest.generatedBy !== 'string' || !manifest.generatedBy ||
      !exactKeys(tool, ['id', 'sourcePath', 'sourceNormalizedSha256', 'executablePath', 'executableSha256',
        'verifierPath', 'verifierSha256']) || tool.id !== toolId) {
    fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', `standard toolchain manifest contract is invalid for ${toolId}`)
  }
  const sourcePath = resolveRegisteredArtifact(repositoryRoot, expected.sourcePath, tool.sourcePath, `${toolId} source`)
  const executablePath = resolveRegisteredArtifact(repositoryRoot, expected.executablePath, tool.executablePath,
    `${toolId} executable`)
  const verifierPath = resolveRegisteredArtifact(repositoryRoot, expected.verifierPath, tool.verifierPath,
    `${toolId} verifier`)
  if (normalizedStandardSourceSha256(sourcePath) !== String(tool.sourceNormalizedSha256 || '').toUpperCase() ||
      sha256(readFileSync(executablePath)) !== String(tool.executableSha256 || '').toUpperCase() ||
      sha256(readFileSync(verifierPath)) !== String(tool.verifierSha256 || '').toUpperCase()) {
    fail('NATIVE_TOOLCHAIN_ARTIFACT_INVALID', `live toolchain artifact hash mismatch for ${toolId}`)
  }
  return Object.freeze({
    id: toolId,
    sourcePath,
    sourceNormalizedSha256: String(tool.sourceNormalizedSha256).toUpperCase(),
    executablePath,
    executableSha256: String(tool.executableSha256).toUpperCase(),
    verifierPath,
    verifierSha256: String(tool.verifierSha256).toUpperCase(),
  })
}

function validateLockManifest(manifest, repositoryRoot) {
  if (!exactKeys(manifest, ['schema', 'generatedBy', 'tools', 'validator']) || manifest.schema !== LOCK_SCHEMA ||
      manifest.generatedBy !== 'VerifyLockTopology888x14Static.mjs' ||
      !exactKeys(manifest.tools, Object.keys(LOCK_TOOL_ARTIFACTS)) ||
      !exactKeys(manifest.validator, ['path', 'sha256'])) {
    fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', 'lock toolchain composite manifest contract is invalid')
  }
  const liveTools = {}
  for (const [toolId, expected] of Object.entries(LOCK_TOOL_ARTIFACTS)) {
    const tool = manifest.tools[toolId]
    if (!exactKeys(tool, ['sourcePath', 'sourceNormalizedSha256', 'executablePath', 'executableSha256'])) {
      fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', `lock tool identity is invalid for ${toolId}`)
    }
    const sourcePath = resolveRegisteredArtifact(repositoryRoot, expected.sourcePath, tool.sourcePath,
      `${toolId} source`, { absolute: true })
    const executablePath = resolveRegisteredArtifact(repositoryRoot, expected.executablePath, tool.executablePath,
      `${toolId} executable`, { absolute: true })
    if (normalizedLockSourceSha256(sourcePath) !== String(tool.sourceNormalizedSha256 || '').toUpperCase() ||
        sha256(readFileSync(executablePath)) !== String(tool.executableSha256 || '').toUpperCase()) {
      fail('NATIVE_TOOLCHAIN_ARTIFACT_INVALID', `live lock tool hash mismatch for ${toolId}`)
    }
    liveTools[toolId] = Object.freeze({
      id: toolId,
      sourcePath,
      sourceNormalizedSha256: String(tool.sourceNormalizedSha256).toUpperCase(),
      executablePath,
      executableSha256: String(tool.executableSha256).toUpperCase(),
    })
  }
  const validatorPath = resolveRegisteredArtifact(repositoryRoot, LOCK_VALIDATOR_PATH, manifest.validator.path,
    'lock validator', { absolute: true })
  if (sha256(readFileSync(validatorPath)) !== String(manifest.validator.sha256 || '').toUpperCase()) {
    fail('NATIVE_TOOLCHAIN_ARTIFACT_INVALID', 'live lock validator hash mismatch')
  }
  return Object.freeze({
    tool: liveTools.native_lock_topology_888x14_v1,
    auxiliaries: Object.freeze({
      native_lock_topology_inspector_v1: liveTools.native_lock_topology_inspector_v1,
      native_assembly_tongue_inspector_888x14_v1: liveTools.native_assembly_tongue_inspector_888x14_v1,
      validator: Object.freeze({ path: validatorPath, sha256: String(manifest.validator.sha256).toUpperCase() }),
    }),
  })
}

function assertSafeManifestPath(path, repositoryRoot) {
  if (!existsSync(path)) fail('NATIVE_TOOLCHAIN_MANIFEST_MISSING', `toolchain manifest is missing: ${path}`)
  const root = resolve(repositoryRoot)
  const relativePath = relative(root, path)
  if (!relativePath || relativePath.startsWith('..') || isAbsolute(relativePath)) {
    fail('NATIVE_TOOLCHAIN_MANIFEST_UNSAFE', `toolchain manifest escaped repository root: ${path}`)
  }
  let current = root
  for (const segment of relativePath.split(sep)) {
    current = resolve(current, segment)
    const info = lstatSync(current)
    if (info.isSymbolicLink()) {
      fail('NATIVE_TOOLCHAIN_MANIFEST_UNSAFE', `toolchain manifest path contains a reparse point: ${current}`)
    }
  }
  const info = statSync(path)
  if (!info.isFile() || Number(info.nlink) !== 1 || !underRoot(realpathSync.native(path), realpathSync.native(root))) {
    fail('NATIVE_TOOLCHAIN_MANIFEST_UNSAFE', `toolchain manifest is not an isolated regular file: ${path}`)
  }
}

export function resolveTrustedNativeToolchainArtifacts({
  repositoryRoot = DEFAULT_REPOSITORY_ROOT,
} = {}) {
  const root = resolve(repositoryRoot)
  const output = {}
  for (const [toolId, relativePath] of Object.entries(NATIVE_TOOLCHAIN_MANIFEST_PATHS)) {
    if (isAbsolute(relativePath) || relativePath.replaceAll('\\', '/').split('/').some((row) => row === '..')) {
      fail('NATIVE_TOOLCHAIN_MANIFEST_UNSAFE', `registered manifest path is unsafe: ${relativePath}`)
    }
    const path = resolve(root, relativePath)
    assertSafeManifestPath(path, root)
    const bytes = readFileSync(path)
    let manifest
    try { manifest = JSON.parse(bytes.toString('utf8')) }
    catch { fail('NATIVE_TOOLCHAIN_MANIFEST_INVALID', `toolchain manifest is not valid JSON: ${relativePath}`) }
    const validated = toolId === 'native_lock_topology_888x14_v1'
      ? validateLockManifest(manifest, root)
      : { tool: validateStandardManifest(manifest, toolId, root), auxiliaries: Object.freeze({}) }
    output[toolId] = Object.freeze({
      id: toolId,
      manifestPath: path,
      manifestSha256: sha256(bytes),
      tool: validated.tool,
      auxiliaries: validated.auxiliaries,
    })
  }
  return Object.freeze(output)
}

export function resolveTrustedNativeToolchainManifests(options = {}) {
  const artifacts = resolveTrustedNativeToolchainArtifacts(options)
  return Object.freeze(Object.fromEntries(Object.entries(artifacts)
    .map(([toolId, value]) => [toolId, value.manifestSha256])))
}
