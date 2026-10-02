import assert from 'node:assert/strict'
import { createHash, randomBytes } from 'node:crypto'
import { existsSync, linkSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { NATIVE_TOOLCHAIN_MANIFEST_PATHS } from './lib/locker_16029_native_stage_contract.mjs'
import {
  resolveTrustedNativeToolchainArtifacts,
  resolveTrustedNativeToolchainManifests,
} from './lib/locker_16029_native_toolchain_manifests.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const TEMP_ROOT = resolve(ROOT, `tmp/verify-native-toolchain-${process.pid}-${randomBytes(4).toString('hex')}`)
const sha256 = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const STANDARD_ARTIFACTS = {
  native_seed_pack_888x14_v1: ['workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.cs', 'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/SeedPack888Native.exe', 'workers/native_model_requests/development/v1/tools/seed_pack_888_native_v1/Verify-SeedPack888Native.ps1'],
  native_width_888_v1: ['workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.cs', 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.exe', 'workers/native_model_requests/development/v1/tools/width_888_native_v1/verify_static.mjs'],
  native_door_module_888x14_v1: ['workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.cs', 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/NativeDoorModule888x14.exe', 'workers/native_model_requests/development/v1/tools/door_module_888x14_native_v1/Verify-NativeDoorModule888x14.ps1'],
  native_root_assembly_888x14_v1: ['workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/NativeRootAssembly888x14.cs', 'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/NativeRootAssembly888x14.exe', 'workers/native_model_requests/development/v1/tools/root_assembly_888x14_native_v1/verify_static.mjs'],
  native_final_pack_888x14_v1: ['workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/FinalPack888x14Native.cs', 'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/FinalPack888x14Native.exe', 'workers/native_model_requests/development/v1/tools/final_pack_888x14_native_v1/verify_static.mjs'],
}
const LOCK_ARTIFACTS = {
  native_lock_topology_888x14_v1: ['workers/native_model_requests/development/v1/tools/BuildLockTopology888x14.cs', 'workers/native_model_requests/development/v1/tools/bin/BuildLockTopology888x14.exe'],
  native_lock_topology_inspector_v1: ['workers/native_model_requests/development/v1/tools/InspectLockTopology888x14.cs', 'workers/native_model_requests/development/v1/tools/bin/InspectLockTopology888x14.exe'],
  native_assembly_tongue_inspector_888x14_v1: ['workers/native_model_requests/development/v1/tools/InspectAssemblyTongues888x14.cs', 'workers/native_model_requests/development/v1/tools/bin/InspectAssemblyTongues888x14.exe'],
}

function writeFixture(root) {
  for (const [id, relativePath] of Object.entries(NATIVE_TOOLCHAIN_MANIFEST_PATHS)) {
    const path = resolve(root, relativePath)
    mkdirSync(dirname(path), { recursive: true })
    if (id === 'native_lock_topology_888x14_v1') {
      const tools = {}
      for (const toolId of ['native_lock_topology_888x14_v1', 'native_lock_topology_inspector_v1',
        'native_assembly_tongue_inspector_888x14_v1']) {
        const sourcePath = resolve(root, LOCK_ARTIFACTS[toolId][0])
        const executablePath = resolve(root, LOCK_ARTIFACTS[toolId][1])
        mkdirSync(dirname(sourcePath), { recursive: true })
        mkdirSync(dirname(executablePath), { recursive: true })
        writeFileSync(sourcePath,
          toolId === 'native_assembly_tongue_inspector_888x14_v1'
            ? 'private const string ExpectedSourceSha256="__SOURCE_SHA256__";\n'
            : 'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";\n', 'utf8')
        writeFileSync(executablePath, `compiled:${toolId}`, 'utf8')
        tools[toolId] = { sourcePath, sourceNormalizedSha256: sha256(readFileSync(sourcePath)),
          executablePath, executableSha256: sha256(readFileSync(executablePath)) }
      }
      const validatorPath = resolve(root,
        'workers/native_model_requests/development/v1/tools/ValidateLockTopology888x14.mjs')
      writeFileSync(validatorPath, 'export const trusted = true\n', 'utf8')
      writeFileSync(path, `${JSON.stringify({
        schema: 'winnsen.locker16029.native_888x14_lock_toolchain_manifest.v1',
        generatedBy: 'VerifyLockTopology888x14Static.mjs', tools,
        validator: { path: validatorPath, sha256: sha256(readFileSync(validatorPath)) },
      }, null, 2)}\n`, 'utf8')
      continue
    }
    const [sourceRelativePath, executableRelativePath, verifierRelativePath] = STANDARD_ARTIFACTS[id]
    const sourcePath = resolve(root, sourceRelativePath)
    const executablePath = resolve(root, executableRelativePath)
    const verifierPath = resolve(root, verifierRelativePath)
    mkdirSync(dirname(sourcePath), { recursive: true })
    mkdirSync(dirname(executablePath), { recursive: true })
    mkdirSync(dirname(verifierPath), { recursive: true })
    writeFileSync(sourcePath,
      id === 'native_final_pack_888x14_v1'
        ? 'private const string ExpectedSourceSha256="__SOURCE_SHA256__";\nprivate const string ExpectedContractSnapshotSha256="__CONTRACT_SHA256__";\n'
        : 'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";\n', 'utf8')
    writeFileSync(executablePath, `compiled:${id}`, 'utf8')
    writeFileSync(verifierPath, 'export const trusted = true\n', 'utf8')
    const relativeArtifact = (artifactPath) => artifactPath.slice(resolve(root).length + 1).replaceAll('\\', '/')
    writeFileSync(path, `${JSON.stringify({
      schema: 'winnsen.16029.native_toolchain_manifest.v1', generatedBy: 'trusted-static-verifier',
      tool: { id, sourcePath: relativeArtifact(sourcePath),
        sourceNormalizedSha256: sha256(readFileSync(sourcePath)),
        executablePath: relativeArtifact(executablePath), executableSha256: sha256(readFileSync(executablePath)),
        verifierPath: relativeArtifact(verifierPath), verifierSha256: sha256(readFileSync(verifierPath)) },
    }, null, 2)}\n`, 'utf8')
  }
}

const checks = []
async function check(name, action) {
  try { await action(); checks.push({ name, ok: true }) }
  catch (error) { checks.push({ name, ok: false, error: `${error?.code || ''}: ${error?.message || error}` }) }
}

rmSync(TEMP_ROOT, { recursive: true, force: true })
mkdirSync(TEMP_ROOT, { recursive: true })

await check('returns_exact_six_live_manifest_hashes', () => {
  const root = resolve(TEMP_ROOT, 'valid')
  writeFixture(root)
  const actual = resolveTrustedNativeToolchainManifests({ repositoryRoot: root })
  assert.deepEqual(Object.keys(actual), Object.keys(NATIVE_TOOLCHAIN_MANIFEST_PATHS))
  for (const [id, relativePath] of Object.entries(NATIVE_TOOLCHAIN_MANIFEST_PATHS)) {
    assert.equal(actual[id], sha256(readFileSync(resolve(root, relativePath))))
  }
})

await check('returns_primary_executable_and_lock_auxiliary_identities', () => {
  const root = resolve(TEMP_ROOT, 'artifact-identities')
  writeFixture(root)
  const actual = resolveTrustedNativeToolchainArtifacts({ repositoryRoot: root })
  assert.deepEqual(Object.keys(actual), Object.keys(NATIVE_TOOLCHAIN_MANIFEST_PATHS))
  assert.equal(actual.native_width_888_v1.tool.id, 'native_width_888_v1')
  assert.equal(actual.native_width_888_v1.tool.executablePath,
    resolve(root, STANDARD_ARTIFACTS.native_width_888_v1[1]))
  assert.deepEqual(Object.keys(actual.native_lock_topology_888x14_v1.auxiliaries).sort(),
    ['native_assembly_tongue_inspector_888x14_v1', 'native_lock_topology_inspector_v1', 'validator'])
})

await check('missing_or_wrong_identity_manifest_fails_closed', () => {
  const root = resolve(TEMP_ROOT, 'identity')
  writeFixture(root)
  const firstPath = resolve(root, Object.values(NATIVE_TOOLCHAIN_MANIFEST_PATHS)[0])
  writeFileSync(firstPath, '{"schema":"wrong","tool":{"id":"wrong"}}\n', 'utf8')
  assert.throws(
    () => resolveTrustedNativeToolchainManifests({ repositoryRoot: root }),
    (error) => error?.code === 'NATIVE_TOOLCHAIN_MANIFEST_INVALID',
  )
  rmSync(firstPath, { force: true })
  assert.throws(
    () => resolveTrustedNativeToolchainManifests({ repositoryRoot: root }),
    (error) => error?.code === 'NATIVE_TOOLCHAIN_MANIFEST_MISSING',
  )
})

await check('hardlinked_manifest_fails_closed', () => {
  const root = resolve(TEMP_ROOT, 'hardlink')
  writeFixture(root)
  const firstPath = resolve(root, Object.values(NATIVE_TOOLCHAIN_MANIFEST_PATHS)[0])
  linkSync(firstPath, `${firstPath}.second-link`)
  assert.throws(
    () => resolveTrustedNativeToolchainManifests({ repositoryRoot: root }),
    (error) => error?.code === 'NATIVE_TOOLCHAIN_MANIFEST_UNSAFE',
  )
})

await check('tampered_live_tool_artifact_fails_closed', () => {
  const root = resolve(TEMP_ROOT, 'tampered-artifact')
  writeFixture(root)
  const manifestPath = resolve(root, NATIVE_TOOLCHAIN_MANIFEST_PATHS.native_width_888_v1)
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  writeFileSync(resolve(root, manifest.tool.executablePath), 'replaced-with-different-bytes', 'utf8')
  assert.throws(
    () => resolveTrustedNativeToolchainManifests({ repositoryRoot: root }),
    (error) => error?.code === 'NATIVE_TOOLCHAIN_ARTIFACT_INVALID',
  )
})

await check('malformed_lock_composite_manifest_fails_closed', () => {
  const root = resolve(TEMP_ROOT, 'malformed-lock')
  writeFixture(root)
  const manifestPath = resolve(root, NATIVE_TOOLCHAIN_MANIFEST_PATHS.native_lock_topology_888x14_v1)
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  delete manifest.tools.native_lock_topology_inspector_v1
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8')
  assert.throws(
    () => resolveTrustedNativeToolchainManifests({ repositoryRoot: root }),
    (error) => error?.code === 'NATIVE_TOOLCHAIN_MANIFEST_INVALID',
  )
})

await check('temporary_fixture_is_removed', () => {
  rmSync(TEMP_ROOT, { recursive: true, force: true })
  assert.equal(existsSync(TEMP_ROOT), false)
})

const failed = checks.filter((row) => !row.ok)
process.stdout.write(`${JSON.stringify({ status: failed.length ? 'FAIL' : 'PASS', checksTotal: checks.length,
  checksFailed: failed.length, checks }, null, 2)}\n`)
if (failed.length) process.exit(1)
