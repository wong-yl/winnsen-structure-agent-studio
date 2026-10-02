import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { spawnSync } from 'node:child_process'

const here = dirname(fileURLToPath(import.meta.url))
const sourcePath = join(here, 'ConfigureNativeWidth888.cs')
const exePath = join(here, 'ConfigureNativeWidth888.exe')
const verifierPath = fileURLToPath(import.meta.url)
const manifestPath = join(here, 'toolchain_manifest.json')
const repositoryRoot = resolve(here, '..', '..', '..', '..', '..', '..')
const sourceRelativePath = 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.cs'
const executableRelativePath = 'workers/native_model_requests/development/v1/tools/width_888_native_v1/ConfigureNativeWidth888.exe'
const verifierRelativePath = 'workers/native_model_requests/development/v1/tools/width_888_native_v1/verify_static.mjs'
const sharedStageContractPath = join(repositoryRoot, 'tools', 'lib',
  'locker_16029_native_stage_contract.mjs')
const sharedStageContract = await import(pathToFileURL(sharedStageContractPath).href)
const source = readFileSync(sourcePath, 'utf8')
const checks = []
const check = (name, ok, detail = '') => checks.push({ name, ok: Boolean(ok), detail })
const sha = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const nativeRightInputs = [
  ['rule',
    'data/native_model_requests/attempts/NATIVE-20260824T132708Z-888W14D-RIGHT-RULE-R1/attempt-0001/evidence/private/topcover_right_native_rule_v2.json',
    'EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0'],
  ['R34 evidence',
    'data/native_model_requests/attempts/TOPCOVER-RIGHT-PILOT-20260828T152022Z-760-RIGHT-R34/attempt-0001/evidence/private/topcover_right_760_native_pilot.json',
    'E23D31674308456A341D4100C4EA833911193367389DF281F327B2D26BC819BE'],
  ['R34 receipt',
    'data/native_model_requests/attempts/TOPCOVER-RIGHT-PILOT-20260828T152022Z-760-RIGHT-R34/attempt-0001/receipts/topcover_right_760_native_pilot.json',
    'C9FA85375C2906752AECCC28887B0FAD361B271D0C1C2CFF48B0BAB81A9B2E91'],
  ['R34 part',
    'data/native_model_requests/attempts/TOPCOVER-RIGHT-PILOT-20260828T152022Z-760-RIGHT-R34/attempt-0001/native_cad/right_760_native_pilot/上盖壳体右侧板.SLDPRT',
    '64EF1FBC4FD86EA816EAACC08CA5F376314BB2D5B95EBFA535E0FAC75FDB6319'],
  ['R34 authorization',
    'data/native_model_requests/attempts/TOPCOVER-RIGHT-PILOT-20260828T152022Z-760-RIGHT-R34/attempt-0001/execution_authorizations/topcover_right_760_native_v1.json',
    'C87D85C8B8790A75B60FC9C533F92C978DBE46A34975FDB8F445E63AF4F01AB7'],
]
const nativeFrameCrossbarInputs = [
  ['pilot evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T062627Z-3130A1/attempt-0001/diagnostics/frame-crossbar-native-detail-v11.json',
    '940ECCBF4F46DD1FBE021436EE5A52D00A8C7A66F7F5625BFCFAFCB23D66A95F'],
  ['Boolean difference',
    'data/native_model_requests/attempts/NATIVE-20260830T062627Z-3130A1/attempt-0001/diagnostics/frame-crossbar-reference-vs-v11-boolean-diff.json',
    '1F5C34D8B9F2588FE22E189B2AE543B2F545A020B986DE4472264AF4E12FA5B0'],
  ['fresh reopen evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T062627Z-3130A1/attempt-0001/diagnostics/frame-crossbar-native-detail-v11-fresh-reopen.json',
    '7F8898DD69660B530F9F38F0B87A3F1CB7BAFF6D93F6751E44EEE9210CB5FB41'],
  ['native pilot part',
    'data/native_model_requests/attempts/NATIVE-20260830T062627Z-3130A1/attempt-0001/diagnostics/frame-crossbar-native-detail-v11/门框 横隔板R.SLDPRT',
    '2DB68BB29F96E5277E9D0FFA444D2BB9D8C2DDCB2383305B7EEAE26A24DB5BD8'],
  ['reference part',
    'data/native_model_requests/attempts/NATIVE-20260830T062627Z-3130A1/attempt-0001/native_cad/working_pack/门框 横隔板R.SLDPRT',
    '7044D2E8523E7E97371B7168423B224EBA55269F18B18110C9379F72C35EB29F'],
]
const nativeDoorFrameRightInputs = [
  ['pilot evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/diagnostics/door-frame-right-native-detail-v12.json',
    'B11F8E8D6A6CD7B7E05AE822B66DC36A209426FA9C3DC7811A3F3CB2496EA99A'],
  ['Boolean difference',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/diagnostics/door-frame-right-reference-vs-v12-boolean-diff.json',
    '91C6BD7411B4CFDA007C599E8C4E708F168D33887E4DBDAC80D7B8D7789DAAC7'],
  ['fresh reopen evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/diagnostics/door-frame-right-native-detail-v12-fresh-reopen.json',
    '420D139CBD828FF084F7B8045DCDA833B489CA00472058ACB943BE6DBE5A66A5'],
  ['native pilot part',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/diagnostics/door-frame-right-native-detail-v12/door-frame-right-native-v12.SLDPRT',
    '5DC7C08E37D23CBC4C842EF5147238F0CFDA233162C30306878A3DA984E1864F'],
  ['reference part',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/native_cad/working_pack/门框 右.sldprt',
    '5BE5FA1D9B4BC36385F98088CCF640A6D30FC25EACC86ACB25BDBA1FBAF51C7A'],
]
const nativeCabinetShelfRightInputs = [
  ['pilot evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T160538Z-ED5C8C/attempt-0001/diagnostics/cabinet-shelf-right-detail-v25.json',
    'A815B8D1373F829052ABC3D2282C33ED9D390618EA6C4C04C1D4D543FC52C1F4'],
  ['Boolean difference',
    'data/native_model_requests/attempts/NATIVE-20260830T160538Z-ED5C8C/attempt-0001/diagnostics/cabinet-shelf-right-reference-vs-v25-boolean-diff.json',
    '4475C4C99DE90C1D713286D12E1586D32C2A107512A58676E26D096045352CC7'],
  ['fresh reopen evidence',
    'data/native_model_requests/attempts/NATIVE-20260830T160538Z-ED5C8C/attempt-0001/diagnostics/cabinet-shelf-right-detail-v25-fresh-reopen.json',
    'A4E30B9186A82BF8CBD281851D8FC1A83671F601FF81DEB09C11226624DD6C69'],
  ['native pilot part',
    'data/native_model_requests/attempts/NATIVE-20260830T160538Z-ED5C8C/attempt-0001/diagnostics/cabinet-shelf-right-detail-v25/cabinet-shelf-right-v25.SLDPRT',
    '8ACC93419989341416D4B96A3C0BF49C0D945A6CF2B2CBBA672D69355AB03397'],
  ['reference part',
    'data/native_model_requests/attempts/NATIVE-20260830T083519Z-75C0A0/attempt-0001/native_cad/working_pack/箱体横层板R.SLDPRT',
    'F828AE08CADAAB1F3DE998921580D63F0348CA5D78734D8ACD4B7E021552E5BC'],
]

function method(sourceText, name) {
  const signature = new RegExp(`private\\s+static\\s+[^;{}]+\\b${name}\\s*\\([^;{}]*\\)\\s*\\{`, 'm').exec(sourceText)
  if (!signature) return ''
  const open = signature.index + signature[0].lastIndexOf('{')
  let depth = 0
  let quote = ''
  let escaped = false
  let lineComment = false
  let blockComment = false
  for (let index = open; index < sourceText.length; index += 1) {
    const char = sourceText[index]
    const next = sourceText[index + 1] || ''
    if (lineComment) {
      if (char === '\n') lineComment = false
      continue
    }
    if (blockComment) {
      if (char === '*' && next === '/') { blockComment = false; index += 1 }
      continue
    }
    if (quote) {
      if (escaped) escaped = false
      else if (char === '\\') escaped = true
      else if (char === quote) quote = ''
      continue
    }
    if (char === '/' && next === '/') { lineComment = true; index += 1; continue }
    if (char === '/' && next === '*') { blockComment = true; index += 1; continue }
    if (char === '"' || char === "'") { quote = char; continue }
    if (char === '{') depth += 1
    else if (char === '}') {
      depth -= 1
      if (depth === 0) return sourceText.slice(signature.index, index + 1)
    }
  }
  return ''
}

function stringArray(sourceText, name) {
  const match = new RegExp(`(?:string\\[\\]|readonly\\s+string\\[\\])\\s+${name}\\s*=\\s*\\{([\\s\\S]*?)\\};`, 'm')
    .exec(sourceText)
  return match ? [...match[1].matchAll(/"([^"]+)"/g)].map((row) => row[1]) : []
}

function processRows() {
  const script = "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ','"
  return String(spawnSync('powershell.exe', ['-NoProfile', '-Command', script], { encoding: 'utf8' }).stdout || '').trim()
}

function behaviorSelfTest() {
  const testRoot = join(tmpdir(), `winnsen-width-888-static-${process.pid}-${Date.now()}`)
  mkdirSync(testRoot, { recursive: true })
  const escapedExe = exePath.replaceAll("'", "''")
  const escapedRoot = testRoot.replaceAll("'", "''")
  const script = [
    `$assembly = [Reflection.Assembly]::LoadFile('${escapedExe}')`,
    "$type = $assembly.GetType('Winnsen.StructureAgent.Generation.ConfigureNativeWidth888', $true)",
    "$flags = [Reflection.BindingFlags]'NonPublic,Static'",
    "$method = $type.GetMethod('RunStaticSelfTests', $flags)",
    "if ($null -eq $method) { throw 'RunStaticSelfTests private helper is missing' }",
    `$method.Invoke($null, @('${escapedRoot}'))`,
  ].join('; ')
  try {
    const run = spawnSync('powershell.exe', ['-NoProfile', '-Command', script], {
      encoding: 'utf8',
      windowsHide: true,
    })
    let parsed = null
    try { parsed = JSON.parse(String(run.stdout || '').trim()) } catch {}
    return {
      ok: run.status === 0 && parsed?.status === 'PASS' && parsed?.checksFailed === 0,
      status: run.status,
      stdout: String(run.stdout || '').trim(),
      stderr: String(run.stderr || '').trim(),
      parsed,
    }
  } finally {
    rmSync(testRoot, { recursive: true, force: true })
  }
}

function evidenceCommitmentBehaviorTest() {
  const fixture = {
    z: 7,
    a: 0.1592142857142857,
    b: -1.4150714285714288,
    c: 0.0000001,
    d: 0.000001,
    e: 1e20,
    f: 1e21,
    g: 254.4285714285714,
    ticks: 638906112000000000,
    html: '<structure>&engineering',
    nested: { beta: true, alpha: ['x', 2, null] },
    phase_receipt_sha256: 'excluded',
    authorization_checkpoints: [{ valid: false }],
    completed_at_utc: 'excluded',
    evidence_commitment_sha256: 'excluded',
  }
  const expected = sharedStageContract.nativeEvidenceCommitmentSha256(fixture)
  const encoded = Buffer.from(JSON.stringify(fixture), 'utf8').toString('base64')
  const escapedExe = exePath.replaceAll("'", "''")
  const script = [
    `$assembly = [Reflection.Assembly]::LoadFile('${escapedExe}')`,
    "$type = $assembly.GetType('Winnsen.StructureAgent.Generation.ConfigureNativeWidth888', $true)",
    "$flags = [Reflection.BindingFlags]'NonPublic,Static'",
    "$method = $type.GetMethod('RunEvidenceCommitmentVector', $flags)",
    "if ($null -eq $method) { throw 'RunEvidenceCommitmentVector private helper is missing' }",
    `$json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('${encoded}'))`,
    "$method.Invoke($null, @($json))",
  ].join('; ')
  const run = spawnSync('powershell.exe', ['-NoProfile', '-Command', script], {
    encoding: 'utf8', windowsHide: true,
  })
  const actual = String(run.stdout || '').trim()
  return { ok: run.status === 0 && actual === expected, status: run.status, expected, actual,
    stderr: String(run.stderr || '').trim() }
}

function canonicalWidthEvidenceBehaviorTest() {
  const escapedExe = exePath.replaceAll("'", "''")
  const script = [
    `$assembly = [Reflection.Assembly]::LoadFile('${escapedExe}')`,
    "$type = $assembly.GetType('Winnsen.StructureAgent.Generation.ConfigureNativeWidth888', $true)",
    "$flags = [Reflection.BindingFlags]'NonPublic,Static'",
    "$method = $type.GetMethod('RunCanonicalWidthEvidenceVector', $flags)",
    "if ($null -eq $method) { throw 'RunCanonicalWidthEvidenceVector private helper is missing' }",
    "$method.Invoke($null, @())",
  ].join('; ')
  const run = spawnSync('powershell.exe', ['-NoProfile', '-Command', script], {
    encoding: 'utf8', windowsHide: true,
  })
  let parsed = null
  try { parsed = JSON.parse(String(run.stdout || '').trim()) } catch {}
  let actual = ''
  try { actual = sharedStageContract.nativeEvidenceCommitmentSha256(parsed) } catch {}
  return {
    ok: run.status === 0 && parsed?.schema === 'winnsen.locker16029.native_width_888.v1' &&
      parsed?.floating_artifact === 381 && parsed?.near_zero_noise === 0 &&
      parsed?.evidence_commitment_sha256 === actual,
    status: run.status,
    expected: parsed?.evidence_commitment_sha256 || '',
    actual,
    stdout: String(run.stdout || '').trim(),
    stderr: String(run.stderr || '').trim(),
  }
}

function ordered(body, fragments) {
  let cursor = -1
  return fragments.every((fragment) => {
    cursor = body.indexOf(fragment, cursor + 1)
    return cursor >= 0
  })
}

function contractGate(text) {
  const main = method(text, 'Main')
  const sequence = method(text, 'ValidatePhaseSequence')
  const receipt = method(text, 'ValidatePhaseReceipt')
  const prepareReceipt = method(text, 'PreparePhaseReceipt')
  const commitReceipt = method(text, 'CommitPreparedReceipt')
  const evidenceCommitment = method(text, 'EvidenceCommitmentDigest')
  const jsonEvidence = method(text, 'JsonEvidenceCommitted')
  const readJson = method(text, 'ReadJsonObject')
  const cadTree = method(text, 'CaptureCadTree')
  const flatTree = method(text, 'AssertFlatSafeCadTree')
  const rollback = method(text, 'RollBack')
  const restore = method(text, 'RestoreRegularFileFromBackup')
  const rollbackAudit = method(text, 'AssertRollbackInventory')
  const auth = method(text, 'AssertAuthorizationStillValid')
  const authContract = method(text, 'AuthorizationContractValidForPhases')
  const save = method(text, 'SaveModel')
  const partTraverse = method(text, 'CapturePartFeatureRecursive')
  const assemblyTraverse = method(text, 'CaptureAssemblyFeatureRecursive')
  const partHealth = method(text, 'PartHealthGate')
  const derivedHealth = method(text, 'DerivedPartHealthGate')
  const assemblyHealth = method(text, 'AssemblyHealthGate')
  if ([main, sequence, receipt, prepareReceipt, commitReceipt, evidenceCommitment, jsonEvidence, readJson,
    cadTree, flatTree, rollback, restore, rollbackAudit, auth, authContract, save,
    partTraverse, assemblyTraverse, partHealth,
    derivedHealth, assemblyHealth].some((value) => !value)) return false

  const mainOrder = ordered(main, [
    'AcquirePhaseLock(result, phase)',
    'AssertInitialInventory(cadDir, result, phase)',
    'ValidatePhaseSequence(result, phase)',
    'AssertAuthorizationStillValid(result, "phase_boundary_after_sequence")',
  ])
  const rollbackAssertions = (main.match(/AssertRollbackInventory\(result\)/g) || []).length >= 2
  const sequenceGate = sequence.includes('predecessorReceiptSha = Sha256(receiptPath)') &&
    sequence.includes('predecessorPostDigest = receiptPostDigest') &&
    sequence.includes('result.initial_inventory_digest') &&
    sequence.includes('WIDTH_STAGE_PREDECESSOR_POST_TO_CURRENT_PRE_MISMATCH')
  const receiptGate = receipt.includes('predecessorReceiptSha256') &&
    receipt.includes('expectedPredecessorReceiptSha') && receipt.includes('expectedPreInventoryDigest') &&
    receipt.includes('authorizationJsonBase64') && receipt.includes('authorizationBytesSha') &&
    receipt.includes('historicalAuthorizationGate') && receipt.includes('authorizationId') &&
    receipt.includes('evidenceCommitmentSha256') && receipt.includes('initial_inventory_digest') &&
    receipt.includes('post_inventory_digest') && receipt.includes('toolSourceNormalizedSha256') &&
    receipt.includes('FileLinkCount(receiptPath) != 1') &&
    !receipt.includes('InventoryDigest(CadInventory(result.cad_directory))')
  const receiptWriteGate = prepareReceipt.includes('preInventoryDigest') &&
    prepareReceipt.includes('postInventoryDigest') && prepareReceipt.includes('predecessorReceiptSha256') &&
    prepareReceipt.includes('authorizationJsonBase64') &&
    prepareReceipt.includes('PHASE_PREDECESSOR_RECEIPT_CHANGED_DURING_EXECUTION') &&
    commitReceipt.includes('File.Move(prepared.temporary_path, prepared.path)') &&
    commitReceipt.includes('FileLinkCount(prepared.path) == 1')
  const recursiveInventoryGate = cadTree.includes('Queue<string>') &&
    cadTree.includes('Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly)') &&
    cadTree.includes('pending.Enqueue(fullChild)') && cadTree.includes('snapshot.cad_files.Add')
  const fileIdentityGate = flatTree.includes('tree.directories.Count == 0') &&
    flatTree.includes('!HasReparsePoint(path)') && flatTree.includes('FileLinkCount(path) == 1')
  const rollbackGate = rollback.includes('HashSet<string>') && rollback.includes('Path.GetFullPath(row.path)') &&
    rollback.includes('File.Delete(path)') && rollback.includes('rollback_deleted_new_cad_paths') &&
    rollback.includes('!Directory.EnumerateFileSystemEntries(directory).Any()') &&
    rollback.includes('RestoreRegularFileFromBackup') &&
    restore.includes('File.Delete(target)') && restore.includes('File.Copy(backup, target, false)') &&
    ordered(restore, ['File.Delete(target)', '!File.Exists(target)', 'File.Copy(backup, target, false)']) &&
    restore.includes('FileLinkCount(target) == 1') &&
    rollbackAudit.includes('FileLinkCount(path) == 1') && rollbackAudit.includes('tree.directories.Count == 0')
  const authorizationGate = authContract.includes('ExactKeys(authorization, AuthorizationTopLevelKeys)') &&
    authContract.includes('ExactKeys(quality, AuthorizationQualityBoundaryKeys)') &&
    authContract.includes('ExactBoolean(quality, "engineeringAssistanceReady", false)') &&
    authContract.includes('ExactBoolean(quality, "readyOnlyAfterEveryRequiredCheckPasses", true)') &&
    authContract.includes('TimeSpan.FromMinutes(30)') && authContract.includes('expires > leaseExpires') &&
    authContract.includes('completed < issued || completed > expires || completed > leaseExpires') &&
    authContract.includes('string expectedId = "native-auth-"') &&
    auth.includes('FileLinkCount(result.authorization_path) == 1') &&
    auth.includes('AuthorizationContractValid(authorization') &&
    auth.includes('this tool does not claim continuous task-store CAS validation') &&
    ordered(save, ['AssertAuthorizationStillValid(result, "before_cad_save")', 'model.Save3']) &&
    ordered(main, ['AssertAuthorizationStillValid(result, "before_evidence_and_receipt_commit")',
      'WriteJsonAtomicNew(outJson, CanonicalWidthEvidenceDocument(result))',
      'AssertAuthorizationStillValid(result, "after_evidence_commit_before_receipt")',
      'PreparePhaseReceipt(result, evidenceSha256)', 'CommitPreparedReceipt(preparedReceipt)',
      'AssertAuthorizationStillValid(result, "after_receipt_commit")'])
  const evidenceGate = text.includes('foreach (string key in EvidenceCommitmentExcludedKeys)') &&
    readJson.includes('MaxJsonLength = int.MaxValue') && readJson.includes('RecursionLimit = 512') &&
    jsonEvidence.includes('!row.ContainsKey("phase_receipt_committed")') &&
    jsonEvidence.includes('EvidenceCommitmentDigest(row)') &&
    main.includes('DeleteCurrentPhaseReceiptBestEffort(result)') &&
    main.includes('DeleteUnpairedSuccessEvidenceBestEffort(outJson, result)') &&
    main.includes('WriteFailureEvidenceBestEffort(outJson, result)')
  contractGate.evidence = {
    projection: text.includes('foreach (string key in EvidenceCommitmentExcludedKeys)'),
    commitment: text.includes('foreach (string key in EvidenceCommitmentExcludedKeys)'),
    receipt: jsonEvidence.includes('!row.ContainsKey("phase_receipt_committed")'),
    digest: jsonEvidence.includes('EvidenceCommitmentDigest(row)'),
    deleteReceipt: main.includes('DeleteCurrentPhaseReceiptBestEffort(result)'),
    failureEvidence: main.includes('WriteFailureEvidenceBestEffort(outJson, result)'),
  }
  const traversalGate = [partTraverse, assemblyTraverse].every((body) =>
    body.includes('traversal_complete = false') && body.includes('catch')) &&
    partHealth.includes('!snapshot.traversal_complete') &&
    derivedHealth.includes('!snapshot.traversal_complete') &&
    derivedHealth.includes('!baseline.traversal_complete') &&
    assemblyHealth.includes('!snapshot.traversal_complete')
  contractGate.last = { mainOrder, rollbackAssertions, sequenceGate, receiptGate, receiptWriteGate,
    recursiveInventoryGate, fileIdentityGate, rollbackGate, authorizationGate, evidenceGate,
    traversalGate, evidenceParts: contractGate.evidence }
  return Object.values(contractGate.last).every(Boolean)
}

function baseHoleGate(text) {
  const main = method(text, 'Main')
  const allowlist = method(text, 'PhaseAllowlist')
  const edit = method(text, 'RunBaseHoleEdit')
  const verify = method(text, 'RunBaseHoleVerify')
  const captureDimensions = method(text, 'CaptureBaseHoleDimensions')
  const captureConsumer = method(text, 'CaptureBaseHoleConsumerFeature')
  const consumerGate = method(text, 'BaseHoleConsumerFeatureGate')
  const captureBlocks = method(text, 'CaptureBaseHoleBlocks')
  const captureRelations = method(text, 'CaptureBaseHoleRelations')
  const relationBaseline = method(text, 'BaseHoleRelationBaselineGate')
  const relationEquality = method(text, 'BaseHoleRelationsEqual')
  const setPositions = method(text, 'SetBaseHoleBlockPositions')
  const blockGate = method(text, 'BaseHoleBlocksGate')
  if ([main, allowlist, edit, verify, captureDimensions, captureConsumer, consumerGate,
    captureBlocks, captureRelations, relationBaseline, relationEquality, setPositions,
    blockGate].some((value) => !value)) return false

  const constantsGate = text.includes('BaseHolePartFileName = "底座底板.sldprt"') &&
    text.includes('BaseHoleSketchName = "草图9"') &&
    text.includes('BaseHoleConsumerFeatureName = "切除-拉伸5"') &&
    text.includes('BaseHoleSourceAbsXMm = 305.0') &&
    text.includes('BaseHoleTargetAbsXMm = 369.0') &&
    text.includes('BaseHoleSourceSpanMm = 610.0') &&
    text.includes('BaseHoleTargetSpanMm = 738.0') &&
    text.includes('BaseHoleDepthSpanMm = 426.0') &&
    JSON.stringify(stringArray(text, 'BaseHoleBlockNames')) === JSON.stringify([
      '块-敲落孔φ45mm-1', '块-敲落孔φ45mm-3',
      '块-敲落孔φ45mm-4', '块-敲落孔φ45mm-5',
    ])
  const dispatchGate = main.includes('else if (phase == "base-hole") RunBaseHoleEdit') &&
    main.includes('else RunBaseHoleVerify') &&
    main.includes('int expectedChanges = result.base_hole != null && result.base_hole.changed ? 1 : 0') &&
    main.includes('result.actual_changed_paths.Count == expectedChanges')
  const allowlistGate = allowlist.includes('phase == "base-hole"') &&
    allowlist.includes('? new[] { "底座底板.sldprt" }') &&
    !allowlist.includes('? new[] { "底座底板.sldprt",')
  const baselineGate = edit.includes('PartHealthGate(record.before, true)') &&
    edit.includes('record.before.feature_count == 78') &&
    edit.includes('CaptureLinkSnapshot(model, sourcePlan, cadDir, true)') &&
    edit.includes('LinkGate(record.links_before, sourcePlan, true)') &&
    edit.includes('BaseHoleConsumerFeatureGate(record.consumer_before)') &&
    edit.includes('record.precondition_source = BaseHoleBlocksGate(record.before_blocks,') &&
    edit.includes('record.precondition_target = BaseHoleBlocksGate(record.before_blocks,') &&
    edit.includes('(record.precondition_source || record.precondition_target)') &&
    edit.includes('record.d1_driven_state_before == 1') &&
    edit.includes('record.d2_driven_state_before == 1') &&
    edit.includes('Near(record.d2_before_mm, BaseHoleDepthSpanMm') &&
    edit.includes('BaseHoleRelationBaselineGate(record.relations_before)')
  const sourceContextGate =
    edit.includes('source_path = ExactFile(cadDir, sourcePlan.source_file_name)') &&
    edit.includes('record.source_sha_before = Sha256(record.source_path)') &&
    edit.includes('sourceModel = OpenDocument(sw, record.source_path,') &&
    edit.includes('(int)swDocumentTypes_e.swDocPART, true, ref sourceErrors, ref sourceWarnings)') &&
    edit.indexOf('sourceModel = OpenDocument(sw, record.source_path,') <
      edit.indexOf('model = OpenDocument(sw, path,') &&
    edit.includes('CloseDocument(sw, ref sourceModel)') &&
    edit.includes('record.source_hash_unchanged = string.Equals(record.source_sha_before,') &&
    edit.includes('BASE_HOLE_SOURCE_CONTEXT_CHANGED_HASH') &&
    verify.includes('sourceModel = OpenDocument(sw, record.source_path,') &&
    verify.includes('(int)swDocumentTypes_e.swDocPART, true, ref sourceErrors, ref sourceWarnings)') &&
    verify.indexOf('sourceModel = OpenDocument(sw, record.source_path,') <
      verify.indexOf('model = OpenDocument(sw, record.path,') &&
    verify.includes('CloseDocument(sw, ref sourceModel)') &&
    verify.includes('record.source_reopen_hash_unchanged = string.Equals(record.source_sha_before,') &&
    verify.includes('BASE_HOLE_READONLY_SOURCE_CONTEXT_CHANGED_HASH')
  const editGate = edit.includes('SetBaseHoleBlockPositions(active, math,') &&
    edit.includes('BaseHoleTargetAbsXMm, record.position_updates') &&
    edit.includes('record.block_positions_set == 4') &&
    edit.includes('record.changed = true') &&
    edit.includes('record.idempotent_already_target = true') &&
    edit.includes('BaseHoleBlocksGate(record.after_blocks,') &&
    edit.includes('BaseHoleTargetAbsXMm') &&
    edit.includes('Near(record.d1_after_mm,') && edit.includes('BaseHoleTargetSpanMm') &&
    edit.includes('Near(record.d2_after_mm, BaseHoleDepthSpanMm') &&
    edit.includes('record.topology_preserved_after') && edit.includes('record.part_box_preserved_after') &&
    edit.includes('BaseHoleConsumerFeatureGate(record.consumer_after)') &&
    edit.includes('LinkSnapshotsEqual(record.links_before, record.links_after)') &&
    edit.includes('BaseHoleRelationsEqual(record.relations_before, record.relations_after)') &&
    edit.includes('if (record.changed)') && edit.includes('SaveModel(model, record, result)') &&
    edit.includes('record.save_not_required_idempotent = true') &&
    edit.includes('X=±369 with D1=738') && !edit.includes('X=±305 with D1=610')
  const positionGate = setPositions.includes('double targetX = before[0] < 0 ? -targetAbsXMm : targetAbsXMm') &&
    setPositions.includes('targetX / 1000.0, before[1], before[2]') &&
    setPositions.includes('Near(update.after_y_mm, update.before_y_mm') &&
    setPositions.includes('Near(update.after_z_mm, update.before_z_mm') &&
    setPositions.includes('Near(update.after_angle_rad, update.before_angle_rad') &&
    setPositions.includes('Near(update.after_scale, update.before_scale') &&
    setPositions.includes('if (update.position_set && update.readback_pass) count++')
  const blockGateStructure = blockGate.includes('rows.Count != 4') &&
    blockGate.includes('names.SequenceEqual(expected, StringComparer.Ordinal)') &&
    blockGate.includes('{ "块-敲落孔φ45mm-1", new[] { -absXMm, 500.5 } }') &&
    blockGate.includes('{ "块-敲落孔φ45mm-3", new[] { -absXMm, 74.5 } }') &&
    blockGate.includes('{ "块-敲落孔φ45mm-4", new[] { absXMm, 500.5 } }') &&
    blockGate.includes('{ "块-敲落孔φ45mm-5", new[] { absXMm, 74.5 } }') &&
    blockGate.includes('Near(row.z_mm, 0.0') &&
    blockGate.includes('Near(row.angle_rad, 4.71238898038469') &&
    blockGate.includes('Near(row.scale, 1.0')
  const relationGate = captureRelations.includes('snapshot.digest_sha256 = Sha256Text') &&
    relationBaseline.includes('snapshot.relation_count != 6') &&
    relationBaseline.includes('snapshot.coincident_count != 0') &&
    relationBaseline.includes('return horizontal == 4 && distance == 2') &&
    relationEquality.includes('BaseHoleRelationBaselineGate(left)') &&
    relationEquality.includes('BaseHoleRelationBaselineGate(right)') &&
    relationEquality.includes('left.digest_sha256, right.digest_sha256')
  const consumerFeatureGate = captureConsumer.includes('feature.GetErrorCode()') &&
    consumerGate.includes('string.Equals(health.type, "ICE"') &&
    consumerGate.includes('!health.suppressed') && consumerGate.includes('health.error_code == 0') &&
    consumerGate.includes('health.error_code2 == 0') && consumerGate.includes('!health.warning')
  const dimensionGate = captureDimensions.includes('"D1@" + BaseHoleSketchName') &&
    captureDimensions.includes('"D2@" + BaseHoleSketchName') &&
    captureDimensions.includes('d1.DrivenState') && captureDimensions.includes('d2.DrivenState')
  const reopenGate = verify.includes('OpenDocument(sw, record.path, (int)swDocumentTypes_e.swDocPART, true') &&
    verify.includes('errors == 0 && warnings == 0') &&
    verify.includes('BaseHoleBlocksGate(record.reopen_blocks, BaseHoleTargetAbsXMm)') &&
    verify.includes('Near(record.d1_reopen_mm, BaseHoleTargetSpanMm') &&
    verify.includes('Near(record.d2_reopen_mm, BaseHoleDepthSpanMm') &&
    verify.includes('record.d1_driven_state_reopen == 1') &&
    verify.includes('record.d2_driven_state_reopen == 1') &&
    verify.includes('CaptureLinkSnapshot(model, sourcePlan, cadDir, true)') &&
    verify.includes('record.topology_preserved_reopen') && verify.includes('record.part_box_preserved_reopen') &&
    verify.includes('LinkGate(record.links_reopen, sourcePlan, true)') &&
    verify.includes('BaseHoleRelationsEqual(record.relations_before, record.relations_reopen)') &&
    verify.includes('BaseHoleConsumerFeatureGate(record.consumer_reopen)') &&
    verify.includes('LinkSnapshotsEqual(record.links_before, record.links_reopen)') &&
    verify.includes('file.sha_after_save, hash') &&
    verify.includes('BASE_HOLE_READONLY_REOPEN_CHANGED_HASH') &&
    verify.includes('X=±369/D1=738') && !verify.includes('X=±305/D1=610')
  baseHoleGate.last = { constantsGate, dispatchGate, allowlistGate, baselineGate, sourceContextGate, editGate,
    positionGate, blockGateStructure, relationGate, consumerFeatureGate, dimensionGate, reopenGate }
  return Object.values(baseHoleGate.last).every(Boolean)
}

function splitBodyLockGate(text) {
  const edit = method(text, 'RunDerivedEdit')
  const verify = method(text, 'RunDerivedVerify')
  const plans = method(text, 'DerivedPlans')
  const planFactory = method(text, 'P')
  const capture = method(text, 'CaptureLinkSnapshot')
  const linkGate = method(text, 'LinkGate')
  if ([edit, verify, plans, planFactory, capture, linkGate].some((value) => !value)) return false
  const planGate = plans.includes('P("箱体竖隔板L.sldprt", master, "SplitBody",') &&
    plans.includes('-63.002525, -21.2, -63.002525, -21.2') &&
    planFactory.includes('lock_after_refresh = true') &&
    text.includes('public bool lock_after_refresh;') &&
    text.includes('geometry unchanged; local SplitBody reference is rebound and locked')
  const sequenceGate = edit.includes('if (plan.relock_required)') &&
    edit.includes('swExternalFileReferencesunlockAll') &&
    edit.includes('swExternalFileReferencesUpdateNone') &&
    edit.includes('if (plan.lock_after_refresh)') &&
    edit.includes('swExternalFileReferencesLockAll') &&
    ordered(edit, [
      'swExternalFileReferencesunlockAll',
      'swExternalFileReferencesUpdateNone',
      'if (plan.lock_after_refresh)',
      'swExternalFileReferencesLockAll',
      'SaveModel(model, record, result)',
    ]) &&
    edit.includes('CaptureLinkSnapshot(model, plan, cadDir, false)') &&
    edit.includes('CaptureLinkSnapshot(model, plan, cadDir, true)') &&
    edit.includes('DERIVED_SPLITBODY_RELOCK_STATUS_GATE_FAILED')
  const reopenGate = verify.includes('CaptureLinkSnapshot(model, plan, cadDir,') &&
    verify.includes('plan.lock_after_refresh') &&
    verify.includes('LinkGate(record.links_reopen, plan, true)') &&
    verify.includes('DERIVED_READONLY_REOPEN_CHANGED_HASH')
  const captureGate = capture.includes('return CaptureLinkSnapshot(model, plan, cadDir, plan.relock_required)') &&
    text.includes('string cadDir, bool expectLocked)') &&
    text.includes('expected_reference_status = expectLocked ?') &&
    linkGate.includes('plan.lock_after_refresh && !snapshot.all_primary_locked')
  splitBodyLockGate.last = { planGate, sequenceGate, reopenGate, captureGate }
  return Object.values(splitBodyLockGate.last).every(Boolean)
}

function assembliesGate(text) {
  const main = method(text, 'Main')
  const allowlist = method(text, 'PhaseAllowlist')
  const editIsolated = method(text, 'RunAssembliesEditIsolated')
  const editPlan = method(text, 'RunAssemblyEditPlan')
  const verifyIsolated = method(text, 'RunAssembliesVerifyIsolated')
  const verifyPlan = method(text, 'RunAssemblyVerifyPlan')
  const applyTarget = method(text, 'ApplyTransformTarget')
  const plans = method(text, 'AssemblyPlans')
  const outerDoor = method(text, 'OuterDoorPlan')
  const snapshot = method(text, 'CaptureAssemblySnapshot')
  const traverse = method(text, 'CaptureAssemblyFeatureRecursive')
  const health = method(text, 'AssemblyHealthGate')
  const initialOpen = method(text, 'AssemblyInitialOpenGate')
  const reopenGate = method(text, 'AssemblyReopenGate')
  const needsRegen = method(text, 'AssemblyNeedsRegenWarningAllowlist')
  const verifyTransforms = method(text, 'VerifyTransformRows')
  const nonTarget = method(text, 'CaptureNonTargetDigest')
  const nonTargetEqual = method(text, 'NonTargetStatesEqual')
  if ([main, allowlist, editIsolated, editPlan, verifyIsolated, verifyPlan,
    applyTarget, plans, outerDoor, snapshot, traverse, health, initialOpen,
    reopenGate, needsRegen, verifyTransforms, nonTarget, nonTargetEqual]
    .some((value) => !value)) return false

  const dispatchGate = ordered(main, [
    'CloseOwnedSession(ref editSw, result, editSession)',
    'RunAssembliesEditIsolated(cadDir, result, "primary")',
    'RunAssembliesEditIsolated(cadDir, result, "stabilize")',
    'RunAssembliesVerifyIsolated(cadDir, result)',
  ]) && main.includes('ASSEMBLIES_PHASE_VERIFIED_WITH_EXACT_NEEDS_REGEN')
  const allowlistGate = allowlist.includes(': AssemblyPlans(cadDir).Select')
  const expectedNeedsRegenFiles = [
    '储物柜门2╱12焊接_左.SLDASM', '储物柜门2╱12焊接_右.SLDASM',
    '储物柜门4╱12焊接_左.SLDASM', '储物柜门4╱12焊接_右.SLDASM',
    '储物柜门6╱12焊接_左.SLDASM', '储物柜门6╱12焊接_右.SLDASM',
    '储物柜门装配_L2.SLDASM', '储物柜门装配_R2.SLDASM',
    '储物柜门装配_L4.SLDASM', '储物柜门装配_R4.SLDASM',
    '储物柜门装配_L6.SLDASM', '储物柜门装配_R6.SLDASM',
    '底座焊接.SLDASM', '上盖焊接.SLDASM', '门框焊接.sldasm',
    '箱体横层板L焊接.SLDASM', '箱体横层板R焊接.SLDASM',
    '箱体竖隔板L焊接.SLDASM', '箱体竖隔板R焊接.SLDASM',
    '箱体左侧板焊接.sldasm', '箱体右侧板焊接.SLDASM',
  ]
  const actualNeedsRegenFiles = [...needsRegen.matchAll(/"([^"\r\n]+\.sldasm)"/gi)]
    .map((row) => row[1]).sort((left, right) => left.localeCompare(right, 'zh-CN'))
  const expectedNeedsRegenSorted = [...expectedNeedsRegenFiles]
    .sort((left, right) => left.localeCompare(right, 'zh-CN'))
  const planGate = (plans.match(/foreach \(string ratio in new\[\] \{ "2", "4", "6" \}\)/g) || []).length === 2 &&
    plans.includes('new AssemblyPlan("储物柜门" + ratio + "╱12焊接_左.SLDASM", false,') &&
    !plans.includes('new AssemblyPlan("储物柜门" + ratio + "╱12焊接_左.SLDASM", false, 1,') &&
    plans.includes('new AssemblyPlan("储物柜门" + ratio + "╱12焊接_右.SLDASM", false,') &&
    !plans.includes('new AssemblyPlan("储物柜门" + ratio + "╱12焊接_右.SLDASM", false, 1,') &&
    plans.includes('plans.Add(OuterDoorPlan("L" + ratio, true))') &&
    plans.includes('plans.Add(OuterDoorPlan("R" + ratio, false))') &&
    plans.includes('new AssemblyPlan("底座焊接.SLDASM", false,') &&
    plans.includes('T("螺母M12-1", "螺母M12.SLDPRT", -345, -409)') &&
    plans.includes('T("螺母M12-4", "螺母M12.SLDPRT", -345, -409)') &&
    plans.includes('new AssemblyPlan("箱体横层板L焊接.SLDASM", false,') &&
    plans.includes('T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", -221.1, -285.1)') &&
    plans.includes('new AssemblyPlan("箱体横层板R焊接.SLDASM", false, 1,') &&
    plans.includes('T("箱体横层板加强筋-1", "箱体横层板加强筋.SLDPRT", 221.1, 285.1)') &&
    plans.includes('new AssemblyPlan("箱体竖隔板R焊接.SLDASM", false,') &&
    plans.includes('T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", 442.2, 506.2)') &&
    plans.includes('new AssemblyPlan("箱体竖隔板L焊接.SLDASM", false,') &&
    plans.includes('T("箱体侧板加强筋1-1", "箱体侧板加强筋1.sldprt", -442.2, -506.2)') &&
    plans.includes('new AssemblyPlan("箱体左侧板焊接.sldasm", false,') &&
    plans.includes('T("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt", -369.6, -401.6)') &&
    plans.includes('new AssemblyPlan("箱体右侧板焊接.SLDASM", false,') &&
    plans.includes('TZ("箱体侧板加强筋1-3", "箱体侧板加强筋1.sldprt",') &&
    plans.includes('72.6, -170.0, 104.6, -106.0)') &&
    plans.includes('new AssemblyPlan("上盖焊接.SLDASM", false, 1)') &&
    plans.includes('new AssemblyPlan("门框焊接.sldasm", false, 1)') &&
    plans.includes('new AssemblyPlan(SeedRootFileName, true,') &&
    plans.includes('T("储物柜门装配_L2-2", "储物柜门装配_L2.SLDASM", -198.5, -230.5)') &&
    plans.includes('T("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", 198.5, 230.5)') &&
    plans.includes('T("调整脚 M12X60(模型)-5", "调整脚 M12X60(模型).SLDPRT", -345, -409)') &&
    plans.includes('T("调整脚 M12X60(模型)-8", "调整脚 M12X60(模型).SLDPRT", -345, -409)') &&
    outerDoor.includes('T("开口挡圈5-1", "开口挡圈5.SLDPRT", outerSource, outerTarget)') &&
    outerDoor.includes('T("塑料轴套(云绅模具)-2", "塑料轴套(云绅模具).SLDPRT", outerSource, outerTarget)') &&
    outerDoor.includes('T("锁舌-1", "锁舌.SLDPRT", innerSource, innerTarget)')
  const isolatedSessionGate = editIsolated.includes('purpose = "assembly_edit_pass:" + editPass') &&
    editIsolated.includes('OwnedSessionIsExclusive(session)') &&
    editIsolated.includes('foreach (AssemblyPlan plan in AssemblyPlans(cadDir))') &&
    editIsolated.includes('RunAssemblyEditPlan(sw, cadDir, result, plan, editPass)') &&
    editIsolated.includes('session.process_exited')
  const editGate = editPlan.includes('ExactComponents(components, componentPath, target.component_name)') ||
    (applyTarget.includes('ExactComponents(components, componentPath, target.component_name)') &&
      editPlan.includes('row.unique_match && row.precondition_source_or_target && row.target_readback') &&
      editPlan.includes('row.rotation_y_z_preserved && row.fixed_state_preserved') &&
      editPlan.includes('row.suppressed_state_preserved') &&
      editPlan.includes('Require(!changed, "ASSEMBLY_STABILIZATION_TARGET_DRIFT"') &&
      editPlan.includes('AssemblyHealthGate(record.after, plan.is_root)') &&
      editPlan.includes('VerifyTransformRows(components,') &&
      editPlan.includes('record.non_target_after_rebuild_unchanged') &&
      editPlan.includes('NonTargetStatesEqual(') &&
      editPlan.includes('record.left_shelf_root_instance_count == 2') &&
      editPlan.includes('record.right_shelf_root_instance_count == 2') &&
      editPlan.includes('SaveModel(model, record, result)'))
  const targetGate = applyTarget.includes('matches.Count == 1') &&
    applyTarget.includes('bool sourcePosition = Near(row.before_x_mm, target.source_x_mm') &&
    applyTarget.includes('Near(row.before_z_mm, target.source_z_mm') &&
    applyTarget.includes('bool targetPosition = Near(row.before_x_mm, target.target_x_mm') &&
    applyTarget.includes('Near(row.before_z_mm, target.target_z_mm') &&
    applyTarget.includes('data[9] = target.target_x_mm / 1000.0') &&
    applyTarget.includes('if (target.explicit_z_target) data[11] = target.target_z_mm / 1000.0') &&
    applyTarget.includes('component.SetTransformAndSolve2(targetTransform)') &&
    applyTarget.includes('assembly.UnfixComponent()') && applyTarget.includes('assembly.FixComponent()') &&
    applyTarget.includes('row.target_z_readback = !target.explicit_z_target') &&
    applyTarget.includes('SameExceptXZ(before, after, 1e-10) : SameExceptX(before, after, 1e-10)') &&
    applyTarget.includes('row.fixed_state_preserved = row.was_fixed == row.is_fixed_after') &&
    applyTarget.includes('row.suppressed_state_preserved = row.was_suppressed == row.is_suppressed_after')
  const nonTargetGate = nonTarget.includes('if (IsAssemblyTarget(path, name, plan, cadDir)) continue') &&
    nonTarget.includes('transform = TransformData(transform)') &&
    nonTarget.includes('is_fixed = Safe') && nonTarget.includes('is_suppressed = Safe') &&
    nonTarget.includes('digest.sha256 = Sha256Text') &&
    nonTargetEqual.includes('baseline.Count != current.Count') &&
    nonTargetEqual.includes('SamePath(left.component_path, right.component_path)') &&
    nonTargetEqual.includes('!SameTransform(left.transform, right.transform, 1e-10)')
  const healthGate = snapshot.includes('snapshot.top_level_component_count = TopLevelComponents(model).Count') &&
    traverse.includes('snapshot.traversal_complete = false') &&
    health.includes('return snapshot.issues.Count == 0') &&
    !health.includes('issue.error_code == 51') && !health.includes('箱体右侧板焊接-1')
  const warningGate = initialOpen.includes('warnings == stable || warnings == needsRegen') &&
    reopenGate.includes('warnings == plan.expected_stable_open_warnings') &&
    reopenGate.includes('AssemblyNeedsRegenWarningAllowlist().Contains(plan.file_name)') &&
    JSON.stringify(actualNeedsRegenFiles) === JSON.stringify(expectedNeedsRegenSorted) &&
    needsRegen.includes('SeedRootFileName') &&
    !needsRegen.includes('BasePartNotLoaded')
  const verifyGate = verifyIsolated.includes('plans.Count == 22') &&
    verifyIsolated.includes('result.assemblies.Count == 44') &&
    verifyIsolated.includes('plans.Count * 2') &&
    verifyIsolated.includes('primaryRecord.edit_pass, "primary"') &&
    verifyIsolated.includes('stabilizationRecord.edit_pass, "stabilize"') &&
    verifyIsolated.includes('OwnedSessionIsExclusive(session)') &&
    verifyIsolated.includes('RunAssemblyVerifyPlan(sw, cadDir, result, plan, record)') &&
    verifyIsolated.includes('session.process_exited') &&
    verifyPlan.includes('swDocASSEMBLY, true') &&
    verifyPlan.includes('AssemblyReopenGate(plan, errors, warnings)') &&
    verifyPlan.includes('AssemblyHealthGate(record.reopen, plan.is_root)') &&
    verifyPlan.includes('record.reopen_transform_gate') &&
    verifyPlan.includes('record.non_target_reopen_unchanged = NonTargetStatesEqual(') &&
    verifyPlan.includes('record.reopen_left_shelf_root_instance_count == 2') &&
    verifyPlan.includes('record.reopen_right_shelf_root_instance_count == 2') &&
    verifyPlan.includes('record.verify_gate = true') &&
    verifyPlan.includes('file.sha_after_save, hash') &&
    verifyPlan.includes('ASSEMBLY_READONLY_REOPEN_CHANGED_HASH')
  const transformVerifyGate = verifyTransforms.includes('matches.Count == 1') &&
    verifyTransforms.includes('Near(xMm, row.target_x_mm') &&
    verifyTransforms.includes('Near(zMm, row.target_z_mm') &&
    verifyTransforms.includes('row.explicit_z_target ? SameExceptXZ(row.before_transform, values, 1e-10)') &&
    verifyTransforms.includes('fixedState == row.was_fixed') &&
    verifyTransforms.includes('suppressedState == row.was_suppressed')
  assembliesGate.last = { dispatchGate, allowlistGate, planGate, isolatedSessionGate,
    editGate, targetGate, nonTargetGate, healthGate, warningGate, verifyGate,
    transformVerifyGate }
  return Object.values(assembliesGate.last).every(Boolean)
}

function nativeRightTopCoverGate(text) {
  const derivedEdit = method(text, 'RunDerivedEdit')
  const derivedVerify = method(text, 'RunDerivedVerify')
  const edit = method(text, 'RunNativeRightTopCoverEdit')
  const verify = method(text, 'RunNativeRightTopCoverVerify')
  const inputGate = method(text, 'ValidateNativeRightEvidenceInputs')
  const build = method(text, 'BuildNativeRight888SharpInsertBends')
  const createHoles = method(text, 'NativeRightCreateThroughHolesOnSelectedFace')
  const createPem = method(text, 'NativeRightCreatePemBossesOnSelectedFace')
  const sketchPoint = method(text, 'NativeRightToActiveSketchPoint')
  const ray = method(text, 'NativeRightSelectFaceByRay')
  const fixedRay = method(text, 'NativeRightSelectFixedFaceByRay')
  const geometry = method(text, 'NativeRightGeometryMatchesContract')
  const features = method(text, 'NativeRightFeatureMatchesContract')
  const insertBends = method(text, 'NativeRightInsertBendsCoreMatches')
  const plans = method(text, 'DerivedPlans')
  const nativePlan = method(text, 'PNativeRightTopCover')
  if ([derivedEdit, derivedVerify, edit, verify, inputGate, build, createHoles, createPem, sketchPoint,
    ray, fixedRay, geometry, features, insertBends, plans, nativePlan]
    .some((value) => !value)) return false

  const branchGate = derivedEdit.includes('if (plan.native_right_topcover)') &&
    derivedEdit.includes('RunNativeRightTopCoverEdit(sw, cadDir, result, plan)') &&
    derivedVerify.includes('if (plan.native_right_topcover)') &&
    derivedVerify.includes('RunNativeRightTopCoverVerify(sw, cadDir, result, plan, record)')
  const fixedInputGate = [
    'NativeRightRuleSha256', 'NativeRightR34EvidenceSha256',
    'NativeRightR34ReceiptSha256', 'NativeRightR34PartSha256',
    'NativeRightR34AuthorizationSha256',
  ].every((token) => inputGate.includes(token)) &&
    inputGate.includes('BoolValue(evidence, "pilotPass")') &&
    inputGate.includes('BoolValue(receipt, "success")') &&
    inputGate.includes('record.native_right_evidence_inputs_gate = true')
  const identityConstantsGate = [
    'EFAB0AE94FC50C0ECC05CA2384D24A93DE3E276918BAAB68CA96EFADBCAEF1F0',
    'E23D31674308456A341D4100C4EA833911193367389DF281F327B2D26BC819BE',
    'C9FA85375C2906752AECCC28887B0FAD361B271D0C1C2CFF48B0BAB81A9B2E91',
    '64EF1FBC4FD86EA816EAACC08CA5F376314BB2D5B95EBFA535E0FAC75FDB6319',
    'C87D85C8B8790A75B60FC9C533F92C978DBE46A34975FDB8F445E63AF4F01AB7',
  ].every((digest) => text.includes(digest))
  const replaceGate = ordered(edit, [
    'ValidateNativeRightEvidenceInputs(result.repository_root, record)',
    'OpenDocument(sw, path',
    'CaptureLinkSnapshot(prior, legacyMirror, cadDir)',
    'CloseDocument(sw, ref prior)',
    'backup.backup_created',
    'AssertAuthorizationStillValid(result, "before_native_right_replace")',
    'File.Delete(path)',
    'sw.NewDocument(template, 0, 0, 0)',
    'BuildNativeRight888SharpInsertBends(sw, model, record)',
    'CaptureNativeRightGeometry(model)',
    'CaptureNativeRightFeatures(model)',
    'ListExternalFileReferencesCount()',
    'NativeRightGeometryMatchesContract',
    'NativeRightFeatureMatchesContract',
    'record.native_right_external_reference_count == 0',
    'AssertAuthorizationStillValid(result, "before_native_right_save")',
    'model.Extension.SaveAs3(path',
  ]) && edit.includes('FileLinkCount(path) == 1') &&
    edit.includes('FileLinkCount(backup.backup_path) == 1')
  const reopenGate = verify.includes('model.IsOpenedReadOnly()') &&
    verify.includes('model.ForceRebuild3(false)') &&
    verify.includes('record.native_right_reopen_external_reference_count == 0') &&
    verify.includes('NativeRightGeometryMatchesContract') &&
    verify.includes('NativeRightFeatureMatchesContract') &&
    verify.includes('Require(string.Equals(file.sha_after_save, hash') &&
    verify.includes('NATIVE_RIGHT_READONLY_REOPEN_CHANGED_HASH')
  const translationGate = text.includes('NativeRightDeltaXMm = 64.0') &&
    sketchPoint.includes('(modelPointMm.x + NativeRightDeltaXMm) / 1000.0') &&
    ray.includes('x + NativeRightDeltaXMm / 1000.0') &&
    fixedRay.includes('0.355 + NativeRightDeltaXMm / 1000.0')
  const buildGate = build.includes('FeatureExtrusionThin2') &&
    build.includes('InsertBends2(0.0005, "", 0.5, 0.0,') &&
    build.includes('NativeRightCreatePostFeatures(model, math)') &&
    build.includes('record.native_right_sharp_stock_gate') &&
    build.includes('record.native_right_insert_bends_gate') &&
    ordered(createHoles, ['model.SetAddToDB(true)', 'CreateCircleByRadius',
      'model.SetAddToDB(false)']) &&
    ordered(createPem, ['model.SetAddToDB(true)', 'CreateCircleByRadius',
      'model.SetAddToDB(false)'])
  const geometryGate = geometry.includes('384.8, 1839.2, -549.0, 444.0, 1917.0, -47.0') &&
    geometry.includes('71396.31748296092') && geometry.includes('179242.4290120627') &&
    geometry.includes('0.5604610922412432') && geometry.includes('faceCount != 135') &&
    geometry.includes('edgeCount != 324') && geometry.includes('loopCount != 188') &&
    geometry.includes('loopEdgeReferenceCount != 648') &&
    geometry.includes('cylinderFaceCount != 27') &&
    geometry.includes('circleEdgeReferenceCount != 108') &&
    geometry.includes('NativeRightR34DetailedSignature')
  const featureGate = insertBends.includes('value.oneBendCount == 5') &&
    insertBends.includes('value.sheetMetalCount == 1') &&
    insertBends.includes('value.flatPatternCount == 1') &&
    insertBends.includes('swBendType_e.swSharpBend') &&
    features.includes('value.mirrorPartCount == 0') &&
    features.includes('value.forbiddenImportFeatureCount == 0') &&
    features.includes('value.sketchedBendGroupCount == 0')
  const planGate = plans.includes('PNativeRightTopCover("上盖壳体右侧板.SLDPRT"') &&
    plans.includes('"上盖壳体左侧板.sldprt"') &&
    (plans.match(/"MirrorStock"/g) || []).length === 0 &&
    nativePlan.includes('native_right_topcover = true') &&
    nativePlan.includes('link_type = NativeRightTopCoverRoute')
  nativeRightTopCoverGate.last = { branchGate, fixedInputGate, identityConstantsGate,
    replaceGate, reopenGate,
    translationGate, buildGate, geometryGate, featureGate, planGate }
  return Object.values(nativeRightTopCoverGate.last).every(Boolean)
}

function nativeFrameCrossbarGate(text) {
  const derivedEdit = method(text, 'RunDerivedEdit')
  const derivedVerify = method(text, 'RunDerivedVerify')
  const edit = method(text, 'RunNativeFrameCrossbarRightEdit')
  const verify = method(text, 'RunNativeFrameCrossbarRightVerify')
  const inputGate = method(text, 'ValidateNativeFrameCrossbarEvidenceInputs')
  const build = method(text, 'BuildNativeFrameCrossbarRight888')
  const geometry = method(text, 'NativeFrameCrossbarGeometryMatchesContract')
  const features = method(text, 'NativeFrameCrossbarFeatureMatchesContract')
  const corrections = method(text, 'FrameCrossbarApplyBooleanDifferenceCorrections')
  const sketchPoint = method(text, 'FrameCrossbarToActiveSketchPoint')
  const plans = method(text, 'DerivedPlans')
  const nativePlan = method(text, 'PNativeFrameCrossbarRight')
  if ([derivedEdit, derivedVerify, edit, verify, inputGate, build, geometry,
    features, corrections, sketchPoint, plans, nativePlan]
    .some((value) => !value)) return false

  const branchGate = derivedEdit.includes('if (plan.native_frame_crossbar_right)') &&
    derivedEdit.includes('RunNativeFrameCrossbarRightEdit(sw, cadDir, result, plan)') &&
    derivedVerify.includes('if (plan.native_frame_crossbar_right)') &&
    derivedVerify.includes('RunNativeFrameCrossbarRightVerify(sw, cadDir, result, plan, record)')
  const inputIdentityGate = [
    '940ECCBF4F46DD1FBE021436EE5A52D00A8C7A66F7F5625BFCFAFCB23D66A95F',
    '1F5C34D8B9F2588FE22E189B2AE543B2F545A020B986DE4472264AF4E12FA5B0',
    '7F8898DD69660B530F9F38F0B87A3F1CB7BAFF6D93F6751E44EEE9210CB5FB41',
    '2DB68BB29F96E5277E9D0FFA444D2BB9D8C2DDCB2383305B7EEAE26A24DB5BD8',
    '7044D2E8523E7E97371B7168423B224EBA55269F18B18110C9379F72C35EB29F',
  ].every((digest) => text.includes(digest)) &&
    ['NativeFrameCrossbarPilotEvidenceSha256',
      'NativeFrameCrossbarBooleanDiffSha256',
      'NativeFrameCrossbarReopenEvidenceSha256',
      'NativeFrameCrossbarPilotPartSha256',
      'NativeFrameCrossbarReferencePartSha256',
    ].every((token) => inputGate.includes(token)) &&
    inputGate.includes('BoolValue(difference, "success")') &&
    inputGate.includes('ExactNumber(referenceMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('record.native_frame_crossbar_evidence_inputs_gate = true')
  const replaceGate = ordered(edit, [
    'ValidateNativeFrameCrossbarEvidenceInputs(result.repository_root, record)',
    'OpenDocument(sw, path',
    'CaptureLinkSnapshot(prior, legacyMirror, cadDir)',
    'CloseDocument(sw, ref prior)',
    'backup.backup_created',
    'AssertAuthorizationStillValid(result, "before_native_frame_crossbar_replace")',
    'File.Delete(path)',
    'sw.NewDocument(template, 0, 0, 0)',
    'BuildNativeFrameCrossbarRight888(sw, model)',
    'CaptureNativeRightGeometry(model)',
    'CaptureNativeRightFeatures(model)',
    'record.native_frame_crossbar_external_reference_count == 0',
    'AssertAuthorizationStillValid(result, "before_native_frame_crossbar_save")',
    'model.Extension.SaveAs3(path',
  ]) && edit.includes('FileLinkCount(path) == 1') &&
    edit.includes('FileLinkCount(backup.backup_path) == 1')
  const reopenGate = verify.includes('model.IsOpenedReadOnly()') &&
    verify.includes('model.ForceRebuild3(false)') &&
    verify.includes('record.native_frame_crossbar_reopen_external_reference_count == 0') &&
    verify.includes('NativeFrameCrossbarGeometryMatchesContract') &&
    verify.includes('NativeFrameCrossbarFeatureMatchesContract') &&
    verify.includes('NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_CHANGED_HASH')
  const buildGate = build.includes('FeatureExtrusionThin2') &&
    build.includes('InsertBends2(0.0002,') &&
    build.includes('FrameCrossbarCutBendSectors') &&
    build.includes('FrameCrossbarApplyBooleanDifferenceCorrections') &&
    ordered(build, ['InsertBends2(0.0002,',
      '"FRAME_CROSSBAR_THROUGH_SECTION_END_DETAILS"',
      'FrameCrossbarCutBendSectors(model, math, startPlane']) &&
    corrections.includes('FrameCrossbarCutEndPlanePolygons') &&
    corrections.includes('FrameCrossbarCreateBoss')
  const coordinateGate = sketchPoint.includes('modelPointMm.x / 1000.0') &&
    !sketchPoint.includes('NativeRightDeltaXMm') &&
    build.includes('FrameCrossbarCreateXOffsetPlane(model, 36.5)') &&
    build.includes('FrameCrossbarCreateXOffsetPlane(model, 425.5)')
  const geometryGate = geometry.includes('36.5, 1692.5, -19.7, 425.5, 1707.5, 0.0') &&
    geometry.includes('23042.325502284795') &&
    geometry.includes('39559.96757532858') &&
    geometry.includes('0.18088225519293563') &&
    geometry.includes('geometry.faceCount == 40') &&
    geometry.includes('geometry.edgeCount == 105') &&
    geometry.includes('geometry.loopCount == 55') &&
    geometry.includes('geometry.loopEdgeReferenceCount == 210') &&
    geometry.includes('geometry.cylinderFaceCount == 11') &&
    geometry.includes('geometry.circleEdgeReferenceCount == 44') &&
    geometry.includes('NativeFrameCrossbarDetailedSignature')
  const featureGate = features.includes('value.sheetMetalCount == 1') &&
    features.includes('value.oneBendCount == 3') &&
    features.includes('value.flatPatternCount == 1') &&
    features.includes('value.mirrorPartCount == 0') &&
    features.includes('value.forbiddenImportFeatureCount == 0') &&
    features.includes('Near(value.sheetMetalThicknessMm, 1.2') &&
    features.includes('Near(value.sheetMetalRadiusMm, 0.2') &&
    features.includes('Near(value.sheetMetalKFactor, 0.333333')
  const planGate = plans.includes(
    'PNativeFrameCrossbarRight("门框 横隔板R.SLDPRT"') &&
    !plans.includes(
      'P("门框 横隔板R.SLDPRT", "门框 横隔板.sldprt", "MirrorStock"') &&
    (plans.match(/"MirrorStock"/g) || []).length === 0 &&
    nativePlan.includes('native_frame_crossbar_right = true') &&
    nativePlan.includes('link_type = NativeFrameCrossbarRightRoute')
  nativeFrameCrossbarGate.last = { branchGate, inputIdentityGate, replaceGate,
    reopenGate, buildGate, coordinateGate, geometryGate, featureGate, planGate }
  return Object.values(nativeFrameCrossbarGate.last).every(Boolean)
}

function nativeDoorFrameRightGate(text) {
  const derivedEdit = method(text, 'RunDerivedEdit')
  const derivedVerify = method(text, 'RunDerivedVerify')
  const edit = method(text, 'RunNativeDoorFrameRightEdit')
  const verify = method(text, 'RunNativeDoorFrameRightVerify')
  const inputGate = method(text, 'ValidateNativeDoorFrameRightEvidenceInputs')
  const build = method(text, 'BuildNativeDoorFrameRight888')
  const cutDetails = method(text, 'DoorFrameRightCreateCutDetails')
  const removeSectors = method(text, 'DoorFrameRightRemoveEndBendSectors')
  const blocks = method(text, 'DoorFrameRightApplyReferenceBlocks')
  const createBoss = method(text, 'DoorFrameRightCreateBoss')
  const splits = method(text, 'DoorFrameRightApplyReferenceFaceSplits')
  const splitProject = method(text, 'DoorFrameRightCreateProjectionSplit')
  const geometry = method(text, 'NativeDoorFrameRightGeometryMatchesContract')
  const features = method(text, 'NativeDoorFrameRightFeatureMatchesContract')
  const plans = method(text, 'DerivedPlans')
  const nativePlan = method(text, 'PNativeDoorFrameRight')
  if ([derivedEdit, derivedVerify, edit, verify, inputGate, build, cutDetails,
    removeSectors, blocks, createBoss, splits, splitProject, geometry, features, plans,
    nativePlan].some((value) => !value)) return false

  const branchGate = derivedEdit.includes('if (plan.native_door_frame_right)') &&
    derivedEdit.includes('RunNativeDoorFrameRightEdit(sw, cadDir, result, plan)') &&
    derivedVerify.includes('if (plan.native_door_frame_right)') &&
    derivedVerify.includes('RunNativeDoorFrameRightVerify(sw, cadDir, result, plan, record)')
  const inputIdentityGate = [
    'B11F8E8D6A6CD7B7E05AE822B66DC36A209426FA9C3DC7811A3F3CB2496EA99A',
    '91C6BD7411B4CFDA007C599E8C4E708F168D33887E4DBDAC80D7B8D7789DAAC7',
    '420D139CBD828FF084F7B8045DCDA833B489CA00472058ACB943BE6DBE5A66A5',
    '5DC7C08E37D23CBC4C842EF5147238F0CFDA233162C30306878A3DA984E1864F',
    '5BE5FA1D9B4BC36385F98088CCF640A6D30FC25EACC86ACB25BDBA1FBAF51C7A',
  ].every((digest) => text.includes(digest)) &&
    ['NativeDoorFrameRightPilotEvidenceSha256',
      'NativeDoorFrameRightBooleanDiffSha256',
      'NativeDoorFrameRightReopenEvidenceSha256',
      'NativeDoorFrameRightPilotPartSha256',
      'NativeDoorFrameRightReferencePartSha256',
    ].every((token) => inputGate.includes(token)) &&
    inputGate.includes('SamePath(TextValue(pilot, "part_path"), pilotPartPath)') &&
    inputGate.includes('SamePath(TextValue(difference, "reference_path"), referencePartPath)') &&
    inputGate.includes('SamePath(TextValue(difference, "candidate_path"), pilotPartPath)') &&
    inputGate.includes('SamePath(TextValue(reopen, "part_path"), pilotPartPath)') &&
    inputGate.includes('ExactNumber(referenceMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('record.native_door_frame_right_evidence_inputs_gate = true')
  const replaceGate = ordered(edit, [
    'ValidateNativeDoorFrameRightEvidenceInputs(result.repository_root, record)',
    'OpenDocument(sw, path',
    'CaptureLinkSnapshot(prior, legacyMirror, cadDir)',
    'CloseDocument(sw, ref prior)',
    'backup.backup_created',
    'AssertAuthorizationStillValid(result, "before_native_door_frame_right_replace")',
    'File.Delete(path)',
    'sw.NewDocument(template, 0, 0, 0)',
    'BuildNativeDoorFrameRight888(sw, model)',
    'CaptureNativeRightGeometry(model)',
    'CaptureNativeRightFeatures(model)',
    'record.native_door_frame_right_external_reference_count == 0',
    'AssertAuthorizationStillValid(result, "before_native_door_frame_right_save")',
    'model.Extension.SaveAs3(path',
  ]) && edit.includes('FileLinkCount(path) == 1') &&
    edit.includes('FileLinkCount(backup.backup_path) == 1')
  const reopenGate = verify.includes('model.IsOpenedReadOnly()') &&
    verify.includes('model.ForceRebuild3(false)') &&
    verify.includes('record.native_door_frame_right_reopen_external_reference_count == 0') &&
    verify.includes('NativeDoorFrameRightGeometryMatchesContract') &&
    verify.includes('NativeDoorFrameRightFeatureMatchesContract') &&
    verify.includes('NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_CHANGED_HASH')
  const buildOrderGate = ordered(build, ['InsertBends2(0.0002,',
    'DoorFrameRightCreateCutDetails(model, math)',
    'DoorFrameRightRemoveEndBendSectors(model, math)',
    'DoorFrameRightApplyReferenceBlocks(model, math)',
    'DoorFrameRightApplyReferenceFaceSplits(model, math)'])
  const repeatedDetailsGate =
    cutDetails.includes('centerY = 181.0 + 152.5 * index') &&
    cutDetails.includes('104.75 + 152.5 * index') &&
    (cutDetails.match(/index < 11/g) || []).length === 2
  const splitLineGate = splitProject.includes('sketch.Select2(false, 4)') &&
    splitProject.includes('DoorFrameRightSelectFaceByRayMark') &&
    splitProject.includes('model.InsertSplitLineProject(false, false)')
  const buildGate = build.includes('FeatureExtrusionThin2') &&
    build.includes('InsertBends2(0.0002,') &&
    buildOrderGate && repeatedDetailsGate &&
    createBoss.includes('FeatureExtrusion2') &&
    splitLineGate
  const geometryGate = geometry.includes('424.0, 0.0, -45.0, 444.0, 1917.0, 0.0') &&
    geometry.includes('232924.52059047756') &&
    geometry.includes('393348.7176599304') &&
    geometry.includes('1.828457486635249') &&
    geometry.includes('geometry.faceCount == 109') &&
    geometry.includes('geometry.edgeCount == 278') &&
    geometry.includes('geometry.loopCount == 142') &&
    geometry.includes('geometry.loopEdgeReferenceCount == 556') &&
    geometry.includes('geometry.cylinderFaceCount == 19') &&
    geometry.includes('geometry.circleEdgeReferenceCount == 76') &&
    geometry.includes('NativeDoorFrameRightDetailedSignature')
  const featureGate = features.includes('value.sheetMetalCount == 1') &&
    features.includes('value.oneBendCount == 4') &&
    features.includes('value.flatPatternCount == 1') &&
    features.includes('value.mirrorPartCount == 0') &&
    features.includes('value.forbiddenImportFeatureCount == 0') &&
    features.includes('Near(value.sheetMetalThicknessMm, 1.2') &&
    features.includes('Near(value.sheetMetalRadiusMm, 0.2') &&
    features.includes('Near(value.sheetMetalKFactor, 0.333333') &&
    features.includes('swBendType_e.swSharpBend')
  const planGate = plans.includes(
    'PNativeDoorFrameRight("门框 右.sldprt"') &&
    !plans.includes(
      'P("门框 右.sldprt", "门框 左.sldprt", "MirrorStock"') &&
    (plans.match(/"MirrorStock"/g) || []).length === 0 &&
    nativePlan.includes('native_door_frame_right = true') &&
    nativePlan.includes('link_type = NativeDoorFrameRightRoute')
  nativeDoorFrameRightGate.last = { branchGate, inputIdentityGate, replaceGate,
    reopenGate, buildGate, buildOrderGate, repeatedDetailsGate, splitLineGate,
    geometryGate, featureGate, planGate,
    buildHasThin: build.includes('FeatureExtrusionThin2'),
    buildHasInsertBends: build.includes('InsertBends2(0.0002,'),
    blocksHasExtrusion: createBoss.includes('FeatureExtrusion2') }
  return Object.values(nativeDoorFrameRightGate.last).every(Boolean)
}

function nativeCabinetShelfRightGate(text) {
  const derivedEdit = method(text, 'RunDerivedEdit')
  const derivedVerify = method(text, 'RunDerivedVerify')
  const edit = method(text, 'RunNativeCabinetShelfRightEdit')
  const verify = method(text, 'RunNativeCabinetShelfRightVerify')
  const inputGate = method(text, 'ValidateNativeCabinetShelfRightEvidenceInputs')
  const build = method(text, 'BuildNativeCabinetShelfRight888')
  const sideBack = method(text, 'CabinetShelfRightApplySideAndBackDetails')
  const baseFront = method(text, 'CabinetShelfRightApplyBaseAndFrontDetails')
  const corrections = method(text, 'CabinetShelfRightApplyExactBooleanCorrections')
  const splits = method(text, 'CabinetShelfRightApplyReferenceFaceSplits')
  const projection = method(text, 'CabinetShelfRightProjectionSplit')
  const geometry = method(text, 'NativeCabinetShelfRightGeometryMatchesContract')
  const features = method(text, 'NativeCabinetShelfRightFeatureMatchesContract')
  const plans = method(text, 'DerivedPlans')
  const nativePlan = method(text, 'PNativeCabinetShelfRight')
  if ([derivedEdit, derivedVerify, edit, verify, inputGate, build, sideBack,
    baseFront, corrections, splits, projection, geometry, features, plans,
    nativePlan].some((value) => !value)) return false

  const branchGate = derivedEdit.includes('if (plan.native_cabinet_shelf_right)') &&
    derivedEdit.includes('RunNativeCabinetShelfRightEdit(sw, cadDir, result, plan)') &&
    derivedVerify.includes('if (plan.native_cabinet_shelf_right)') &&
    derivedVerify.includes('RunNativeCabinetShelfRightVerify(sw, cadDir, result, plan, record)')
  const inputIdentityGate = [
    'A815B8D1373F829052ABC3D2282C33ED9D390618EA6C4C04C1D4D543FC52C1F4',
    '4475C4C99DE90C1D713286D12E1586D32C2A107512A58676E26D096045352CC7',
    'A4E30B9186A82BF8CBD281851D8FC1A83671F601FF81DEB09C11226624DD6C69',
    '8ACC93419989341416D4B96A3C0BF49C0D945A6CF2B2CBBA672D69355AB03397',
    'F828AE08CADAAB1F3DE998921580D63F0348CA5D78734D8ACD4B7E021552E5BC',
  ].every((digest) => text.includes(digest)) &&
    ['NativeCabinetShelfRightPilotEvidenceSha256',
      'NativeCabinetShelfRightBooleanDiffSha256',
      'NativeCabinetShelfRightReopenEvidenceSha256',
      'NativeCabinetShelfRightPilotPartSha256',
      'NativeCabinetShelfRightReferencePartSha256',
    ].every((token) => inputGate.includes(token)) &&
    inputGate.includes('SamePath(TextValue(pilot, "part_path"), pilotPartPath)') &&
    inputGate.includes('SamePath(TextValue(difference, "reference_path"), referencePartPath)') &&
    inputGate.includes('SamePath(TextValue(difference, "candidate_path"), pilotPartPath)') &&
    inputGate.includes('SamePath(TextValue(reopen, "part_path"), pilotPartPath)') &&
    inputGate.includes('ExactNumber(referenceMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(referenceMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "error", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "body_count", 0)') &&
    inputGate.includes('ExactNumber(candidateMinus, "total_volume_mm3", 0)') &&
    inputGate.includes('record.native_cabinet_shelf_right_evidence_inputs_gate = true')
  const replaceGate = ordered(edit, [
    'ValidateNativeCabinetShelfRightEvidenceInputs(result.repository_root, record)',
    'OpenDocument(sw, path',
    'CaptureLinkSnapshot(prior, legacyMirror, cadDir)',
    'CloseDocument(sw, ref prior)',
    'backup.backup_created',
    'AssertAuthorizationStillValid(result,',
    'File.Delete(path)',
    'sw.NewDocument(template, 0, 0, 0)',
    'BuildNativeCabinetShelfRight888(sw, model)',
    'CaptureNativeRightGeometry(model)',
    'CaptureNativeRightFeatures(model)',
    'record.native_cabinet_shelf_right_external_reference_count == 0',
    'model.Extension.SaveAs3(path',
  ]) && edit.includes('before_native_cabinet_shelf_right_replace') &&
    edit.includes('before_native_cabinet_shelf_right_save') &&
    edit.includes('FileLinkCount(path) == 1') &&
    edit.includes('FileLinkCount(backup.backup_path) == 1')
  const reopenGate = verify.includes('model.IsOpenedReadOnly()') &&
    verify.includes('model.ForceRebuild3(false)') &&
    verify.includes('record.native_cabinet_shelf_right_reopen_external_reference_count == 0') &&
    verify.includes('NativeCabinetShelfRightGeometryMatchesContract') &&
    verify.includes('NativeCabinetShelfRightFeatureMatchesContract') &&
    verify.includes('NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_CHANGED_HASH')
  const buildOrderGate = ordered(build, [
    'CabinetShelfRightCreateBasePlate(model, math)',
    'CabinetShelfRightCreateThinWall(model, math',
    'CabinetShelfRightApplySideAndBackDetails(model, math)',
    'InsertBends2(0.0005,',
    'CabinetShelfRightApplyBaseAndFrontDetails(model, math)',
    'CabinetShelfRightApplyExactBooleanCorrections(model, math)',
    'CabinetShelfRightApplyReferenceFaceSplits(model, math)',
  ]) && build.includes('0.0, true, 0.5, true')
  const detailGate = sideBack.includes('CabinetShelfRightTrimSideWall') &&
    sideBack.includes('CabinetShelfRightTrimBackWall') &&
    sideBack.includes('CabinetShelfRightCutRightFrontCornerSector') &&
    baseFront.includes('173.1, 333.1') &&
    baseFront.includes('-418.5, -378.5, -168.5, -128.5') &&
    baseFront.includes('new NativeRightPoint3(419.0, 1695.5, -20.5)')
  const correctionGate = corrections.includes('68.18599434057') &&
    corrections.includes('1695.77841282804') &&
    corrections.includes('1700.58840339566') &&
    corrections.includes('CABINET_SHELF_RIGHT_FRONT_RIGHT_EXCESS') &&
    corrections.includes('CabinetShelfRightAddBossFromOffsetPlane') &&
    corrections.includes('CabinetShelfRightCutFromOffsetPlane')
  const splitGate = (splits.match(/CabinetShelfRightProjectionSplit/g) || []).length === 10 &&
    projection.includes('sketch.Select2(false, 4)') &&
    projection.includes('model.InsertSplitLineProject(false, false)') &&
    projection.includes('after == before + expectedFaceIncrease')
  const geometryGate = geometry.includes('64.5, 1692.5, -547.7, 441.7, 1717.5, -20.5') &&
    geometry.includes('178255.1993277') &&
    geometry.includes('447469.005578149') &&
    geometry.includes('1.39930331472244') &&
    geometry.includes('geometry.faceCount == 152') &&
    geometry.includes('geometry.edgeCount == 395') &&
    geometry.includes('geometry.loopCount == 195') &&
    geometry.includes('geometry.loopEdgeReferenceCount == 790') &&
    geometry.includes('geometry.cylinderFaceCount == 32') &&
    geometry.includes('geometry.circleEdgeReferenceCount == 128') &&
    geometry.includes('NativeCabinetShelfRightDetailedSignature')
  const featureGate = features.includes('value.sheetMetalCount == 1') &&
    features.includes('value.oneBendCount == 4') &&
    features.includes('value.flatPatternCount == 1') &&
    features.includes('value.mirrorPartCount == 0') &&
    features.includes('value.forbiddenImportFeatureCount == 0') &&
    features.includes('Near(value.sheetMetalThicknessMm, 0.8') &&
    features.includes('Near(value.sheetMetalRadiusMm, 0.5') &&
    features.includes('Near(value.sheetMetalKFactor, 0.333333') &&
    features.includes('Near(value.sheetMetalReliefRatio, 0.5') &&
    features.includes('swBendType_e.swSharpBend')
  const planGate = plans.includes(
    'PNativeCabinetShelfRight("箱体横层板R.SLDPRT"') &&
    !plans.includes(
      'P("箱体横层板R.SLDPRT", "箱体横层板L.sldprt", "MirrorStock"') &&
    (plans.match(/"MirrorStock"/g) || []).length === 0 &&
    nativePlan.includes('native_cabinet_shelf_right = true') &&
    nativePlan.includes('link_type = NativeCabinetShelfRightRoute')
  nativeCabinetShelfRightGate.last = { branchGate, inputIdentityGate,
    replaceGate, reopenGate, buildOrderGate, detailGate, correctionGate,
    splitGate, geometryGate, featureGate, planGate }
  return Object.values(nativeCabinetShelfRightGate.last).every(Boolean)
}

const normalizedSource = source.replaceAll('\r\n', '\n').replaceAll('\r', '\n').replace(
  /private const string ExpectedSourceSha256 = "[A-Z0-9_]+";/,
  'private const string ExpectedSourceSha256 = "__SOURCE_SHA256__";'
)
const stampedSourceHash = /ExpectedSourceSha256 = "([A-F0-9]{64})"/.exec(source)?.[1] || ''
const seedMethod = method(source, 'SeedInventoryShaByFile')
const seedRows = [...seedMethod.matchAll(/\{\s*"([^"]+)",\s*"([A-F0-9]{64})"\s*\}/g)]
const expectedSeedDigest = /ExpectedSeedInventoryDigest\s*=\s*\r?\n?\s*"([A-F0-9]{64})"/.exec(source)?.[1] || ''

check('source and executable exist', existsSync(sourcePath) && existsSync(exePath))
check('normalized source hash is compiled into the source contract',
  stampedSourceHash === sha(normalizedSource), `${stampedSourceHash} != ${sha(normalizedSource)}`)
check('compiled V37 seed profile has exactly 75 unique names and hashes',
  seedRows.length === 75 && new Set(seedRows.map((row) => row[1].toLowerCase())).size === 75 &&
  new Set(seedRows.map((row) => row[2])).size === 75)
const compiledSeedDigest = sha(seedRows
  .map((row) => [row[1], row[2]])
  .sort((left, right) => left[0].toUpperCase() < right[0].toUpperCase() ? -1 :
    left[0].toUpperCase() > right[0].toUpperCase() ? 1 : 0)
  .map((row) => `${row[0]}|${row[1]}\n`).join(''))
check('seed digest constant is independently recomputed from all 75 compiled rows',
  expectedSeedDigest === compiledSeedDigest, `${expectedSeedDigest} != ${compiledSeedDigest}`)
check('C# receipt keys exactly match the live shared stage-contract module',
  JSON.stringify(stringArray(source, 'SeedReceiptKeys')) ===
    JSON.stringify(sharedStageContract.NATIVE_SEED_RECEIPT_KEYS) &&
  JSON.stringify(stringArray(source, 'PhaseReceiptKeys')) ===
    JSON.stringify(sharedStageContract.NATIVE_RUNTIME_RECEIPT_KEYS),
  JSON.stringify({ csharpSeed: stringArray(source, 'SeedReceiptKeys'),
    sharedSeed: sharedStageContract.NATIVE_SEED_RECEIPT_KEYS,
    csharpRuntime: stringArray(source, 'PhaseReceiptKeys'),
    sharedRuntime: sharedStageContract.NATIVE_RUNTIME_RECEIPT_KEYS }))
check('C# evidence commitment excluded keys match the live shared module',
  JSON.stringify(stringArray(source, 'EvidenceCommitmentExcludedKeys')) ===
    JSON.stringify(sharedStageContract.NATIVE_EVIDENCE_COMMITMENT_EXCLUDED_KEYS))
const contractGatePassed = contractGate(source)
check('receipt chain, recursive inventory, rollback, short authorization and traversal structure',
  contractGatePassed, JSON.stringify(contractGate.last || {}))
const baseHoleGatePassed = baseHoleGate(source)
check('base-hole moves only the four native bottom-plate knockout blocks and preserves its contracts',
  baseHoleGatePassed, JSON.stringify(baseHoleGate.last || {}))
const splitBodyLockGatePassed = splitBodyLockGate(source)
check('all width-dependent SplitBody parts are rebound locally and persist Locked(1)',
  splitBodyLockGatePassed, JSON.stringify(splitBodyLockGate.last || {}))
const assembliesGatePassed = assembliesGate(source)
check('assemblies use exactly twenty plans with isolated stabilization and read-only verification',
  assembliesGatePassed, JSON.stringify(assembliesGate.last || {}))
check('fixed R34 rule, evidence, receipt, part and authorization hashes are live',
  nativeRightInputs.every(([, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return existsSync(path) && sha(readFileSync(path)) === expected
  }), JSON.stringify(nativeRightInputs.map(([name, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return { name, exists: existsSync(path), expected,
      actual: existsSync(path) ? sha(readFileSync(path)) : '' }
  })))
const nativeRightGatePassed = nativeRightTopCoverGate(source)
check('only the right top-cover uses the R34-derived 888 native replacement route',
  nativeRightGatePassed, JSON.stringify(nativeRightTopCoverGate.last || {}))
check('fixed native frame-crossbar pilot and Boolean-equivalence inputs are live',
  nativeFrameCrossbarInputs.every(([, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return existsSync(path) && sha(readFileSync(path)) === expected
  }), JSON.stringify(nativeFrameCrossbarInputs.map(([name, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return { name, exists: existsSync(path), expected,
      actual: existsSync(path) ? sha(readFileSync(path)) : '' }
  })))
const nativeFrameCrossbarGatePassed = nativeFrameCrossbarGate(source)
check('right frame crossbar uses the zero-reference native replacement route',
  nativeFrameCrossbarGatePassed,
  JSON.stringify(nativeFrameCrossbarGate.last || {}))
check('fixed native right door-frame pilot and Boolean-equivalence inputs are live',
  nativeDoorFrameRightInputs.every(([, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return existsSync(path) && sha(readFileSync(path)) === expected
  }), JSON.stringify(nativeDoorFrameRightInputs.map(([name, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return { name, exists: existsSync(path), expected,
      actual: existsSync(path) ? sha(readFileSync(path)) : '' }
  })))
const nativeDoorFrameRightGatePassed = nativeDoorFrameRightGate(source)
check('right door frame uses the zero-reference native replacement route',
  nativeDoorFrameRightGatePassed,
  JSON.stringify(nativeDoorFrameRightGate.last || {}))
check('fixed native right cabinet-shelf pilot and Boolean-equivalence inputs are live',
  nativeCabinetShelfRightInputs.every(([, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return existsSync(path) && sha(readFileSync(path)) === expected
  }), JSON.stringify(nativeCabinetShelfRightInputs.map(([name, relativePath, expected]) => {
    const path = resolve(repositoryRoot, relativePath)
    return { name, exists: existsSync(path), expected,
      actual: existsSync(path) ? sha(readFileSync(path)) : '' }
  })))
const nativeCabinetShelfRightGatePassed = nativeCabinetShelfRightGate(source)
check('right cabinet shelf uses the zero-reference native replacement route',
  nativeCabinetShelfRightGatePassed,
  JSON.stringify(nativeCabinetShelfRightGate.last || {}))

const negativeMutations = [
  ['remove predecessor hash binding', 'predecessorReceiptSha = Sha256(receiptPath)', 'predecessorReceiptSha = ""'],
  ['restore historical-current inventory bug', '!receipt.includes', '!receipt.includes'],
  ['remove single-link CAD gate', 'FileLinkCount(path) == 1, status', 'FileLinkCount(path) >= 1, status'],
  ['expand authorization beyond short lifetime', 'TimeSpan.FromMinutes(30)', 'TimeSpan.FromMinutes(300)'],
  ['remove lease expiry ordering', 'expires > leaseExpires', 'expires < leaseExpires'],
  ['remove rollback audit call', 'AssertRollbackInventory(result);', '/* rollback audit removed */'],
  ['restore unsafe hardlink overwrite', 'File.Copy(backup, target, false)', 'File.Copy(backup, target, true)'],
  ['remove exact authorization key gate', 'ExactKeys(authorization, AuthorizationTopLevelKeys)', 'authorization != null'],
  ['remove part traversal fail-closed gate', '!snapshot.traversal_complete || snapshot.error_feature_count', 'snapshot.error_feature_count'],
  ['restore default JSON evidence length limit', 'MaxJsonLength = int.MaxValue,', 'MaxJsonLength = 2097152,'],
]
const mutationResults = negativeMutations.map(([name, find, replacement]) => {
  if (name === 'restore historical-current inventory bug') {
    const receiptBody = method(source, 'ValidatePhaseReceipt')
    const mutated = source.replace(receiptBody,
      receiptBody.replace('IsSha256(receiptPostInventoryDigest)',
        'IsSha256(receiptPostInventoryDigest) && InventoryDigest(CadInventory(result.cad_directory)).Length == 64'))
    return { name, rejected: !contractGate(mutated) }
  }
  let mutated
  if (name === 'restore default JSON evidence length limit') {
    const readJson = method(source, 'ReadJsonObject')
    mutated = source.replace(readJson, readJson.replace(find, replacement))
  } else mutated = source.replace(find, replacement)
  return { name, rejected: mutated !== source && !contractGate(mutated) }
})
check('negative controls are all rejected by structural verifier',
  mutationResults.every((row) => row.rejected), JSON.stringify(mutationResults))

const baseHoleNegativeMutations = [
  ['change target knockout X coordinate', '',
    'BaseHoleTargetAbsXMm = 369.0', 'BaseHoleTargetAbsXMm = 368.0'],
  ['widen base-hole write allowlist', 'PhaseAllowlist',
    '? new[] { "底座底板.sldprt" }', '? new[] { "底座底板.sldprt", "底座外框.sldprt" }'],
  ['accept fewer moved knockout blocks', 'RunBaseHoleEdit',
    'record.block_positions_set == 4', 'record.block_positions_set >= 3'],
  ['remove reopened relation equality', 'RunBaseHoleVerify',
    'BaseHoleRelationsEqual(record.relations_before, record.relations_reopen)', 'true'],
  ['remove reopened consumer-feature health gate', 'RunBaseHoleVerify',
    'BaseHoleConsumerFeatureGate(record.consumer_reopen)', 'true'],
  ['remove base-hole read-only hash equality', 'RunBaseHoleVerify',
    'string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase)', 'true'],
  ['remove edit-session source context preload', 'RunBaseHoleEdit',
    'sourceModel = OpenDocument(sw, record.source_path,',
    'sourceModel = OpenDocument(sw, record.source_path + ".missing",'],
  ['remove verify-session source context preload', 'RunBaseHoleVerify',
    'sourceModel = OpenDocument(sw, record.source_path,',
    'sourceModel = OpenDocument(sw, record.source_path + ".missing",'],
]
const baseHoleMutationResults = baseHoleNegativeMutations.map(([name, methodName, find, replacement]) => {
  if (!methodName) {
    const mutated = source.replace(find, replacement)
    return { name, rejected: mutated !== source && !baseHoleGate(mutated) }
  }
  const body = method(source, methodName)
  const mutatedBody = body.replace(find, replacement)
  const mutated = source.replace(body, mutatedBody)
  return { name, rejected: mutatedBody !== body && !baseHoleGate(mutated) }
})
check('base-hole negative controls are all rejected',
  baseHoleMutationResults.every((row) => row.rejected), JSON.stringify(baseHoleMutationResults))

const splitBodyLockNegativeMutations = [
  ['remove invariant left-partition stabilization plan', 'DerivedPlans',
    'P("箱体竖隔板L.sldprt", master, "SplitBody",',
    'P("箱体竖隔板L-removed.sldprt", master, "SplitBody",'],
  ['disable default post-refresh locking', 'P',
    'lock_after_refresh = true', 'lock_after_refresh = false'],
  ['remove SplitBody lock action', 'RunDerivedEdit',
    'swExternalFileReferencesUpdate_e.swExternalFileReferencesLockAll',
    'swExternalFileReferencesUpdate_e.swExternalFileReferencesUpdateNone'],
  ['remove final locked snapshot expectation', 'RunDerivedEdit',
    'CaptureLinkSnapshot(model, plan, cadDir, true)',
    'CaptureLinkSnapshot(model, plan, cadDir, false)'],
  ['remove locked reopen enforcement', 'LinkGate',
    'plan.lock_after_refresh && !snapshot.all_primary_locked', 'false'],
]
const splitBodyLockMutationResults = splitBodyLockNegativeMutations
  .map(([name, methodName, find, replacement]) => {
    const body = method(source, methodName)
    const mutatedBody = body.replace(find, replacement)
    const mutated = source.replace(body, mutatedBody)
    return { name, rejected: mutatedBody !== body && !splitBodyLockGate(mutated) }
  })
check('SplitBody lock negative controls are all rejected',
  splitBodyLockMutationResults.every((row) => row.rejected),
  JSON.stringify(splitBodyLockMutationResults))

const assembliesNegativeMutations = [
  ['change root right-door target X', 'AssemblyPlans',
    'T("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", 198.5, 230.5)',
    'T("储物柜门装配_R6-2", "储物柜门装配_R6.SLDASM", 198.5, 231.5)'],
  ['change right-side reinforcement target Z', 'AssemblyPlans',
    '72.6, -170.0, 104.6, -106.0', '72.6, -170.0, 104.6, -105.0'],
  ['remove top-cover assembly IdMismatch baseline', 'AssemblyPlans',
    'new AssemblyPlan("上盖焊接.SLDASM", false, 1)',
    'new AssemblyPlan("上盖焊接.SLDASM", false)'],
  ['remove stabilization drift rejection', 'RunAssemblyEditPlan',
    'Require(!changed, "ASSEMBLY_STABILIZATION_TARGET_DRIFT"',
    'Require(true, "ASSEMBLY_STABILIZATION_TARGET_DRIFT"'],
  ['accept twenty-one assembly plans', 'RunAssembliesVerifyIsolated',
    'plans.Count == 22', 'plans.Count == 21'],
  ['remove non-target reopen equality', 'RunAssemblyVerifyPlan',
    'record.non_target_reopen_unchanged = NonTargetStatesEqual(',
    'record.non_target_reopen_unchanged = true || NonTargetStatesEqual('],
  ['expand exact NeedsRegen whitelist', 'AssemblyNeedsRegenWarningAllowlist',
    '"上盖焊接.SLDASM"', '"上盖焊接.SLDASM", "未知焊接.SLDASM"'],
  ['restore a root issue whitelist', 'AssemblyHealthGate',
    'return snapshot.issues.Count == 0', 'return snapshot.issues.Count <= 1'],
  ['remove root shelf reopen count', 'RunAssemblyVerifyPlan',
    'record.reopen_left_shelf_root_instance_count == 2',
    'record.reopen_left_shelf_root_instance_count >= 0'],
  ['remove assembly read-only hash equality', 'RunAssemblyVerifyPlan',
    'string.Equals(file.sha_after_save, hash, StringComparison.OrdinalIgnoreCase)', 'true'],
]
const assembliesMutationResults = assembliesNegativeMutations.map(([name, methodName, find, replacement]) => {
  const body = method(source, methodName)
  const mutatedBody = body.replace(find, replacement)
  const mutated = source.replace(body, mutatedBody)
  return { name, rejected: mutatedBody !== body && !assembliesGate(mutated) }
})
check('assemblies negative controls are all rejected',
  assembliesMutationResults.every((row) => row.rejected), JSON.stringify(assembliesMutationResults))

const nativeRightNegativeMutations = [
  ['remove R34 evidence identity',
    'E23D31674308456A341D4100C4EA833911193367389DF281F327B2D26BC819BE',
    '0'.repeat(64)],
  ['remove 64 mm sketch translation',
    '(modelPointMm.x + NativeRightDeltaXMm) / 1000.0',
    'modelPointMm.x / 1000.0'],
  ['restore auto-relations in translated hole sketches',
    'model.SetAddToDB(true);', 'model.SetAddToDB(false);'],
  ['restore auto-relations in PEM boss sketches',
    'model.SetAddToDB(true);', 'model.SetAddToDB(false);'],
  ['disable the unique native plan flag',
    'native_right_topcover = true', 'native_right_topcover = false'],
  ['remove live zero-external-reference gate',
    'record.native_right_external_reference_count == 0',
    'record.native_right_external_reference_count >= 0'],
  ['remove read-only reopen hash equality',
    'NATIVE_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,',
    'NATIVE_RIGHT_READONLY_REOPEN_HASH_GATE_REMOVED", 41,'],
]
const nativeRightMutationResults = nativeRightNegativeMutations.map(([name, find, replacement]) => {
  let mutated
  if (name === 'restore auto-relations in translated hole sketches') {
    const createHoles = method(source, 'NativeRightCreateThroughHolesOnSelectedFace')
    mutated = source.replace(createHoles, createHoles.replace(find, replacement))
  } else if (name === 'restore auto-relations in PEM boss sketches') {
    const createPem = method(source, 'NativeRightCreatePemBossesOnSelectedFace')
    mutated = source.replace(createPem, createPem.replace(find, replacement))
  } else mutated = source.replace(find, replacement)
  return { name, rejected: mutated !== source && !nativeRightTopCoverGate(mutated) }
})
check('native right top-cover negative controls are all rejected',
  nativeRightMutationResults.every((row) => row.rejected),
  JSON.stringify(nativeRightMutationResults))

const nativeFrameCrossbarNegativeMutations = [
  ['remove Boolean-equivalence evidence identity',
    '1F5C34D8B9F2588FE22E189B2AE543B2F545A020B986DE4472264AF4E12FA5B0',
    '0'.repeat(64)],
  ['restore frame-crossbar MirrorStock plan',
    'PNativeFrameCrossbarRight("门框 横隔板R.SLDPRT", "门框 横隔板.sldprt",',
    'P("门框 横隔板R.SLDPRT", "门框 横隔板.sldprt", "MirrorStock",'],
  ['disable frame-crossbar native plan flag',
    'native_frame_crossbar_right = true',
    'native_frame_crossbar_right = false'],
  ['inject an incorrect 64 mm translation into frame-crossbar sketches',
    'modelPointMm.x / 1000.0',
    '(modelPointMm.x + NativeRightDeltaXMm) / 1000.0'],
  ['remove the proven through-section-before-trim ordering anchor',
    '"FRAME_CROSSBAR_THROUGH_SECTION_END_DETAILS"',
    '"FRAME_CROSSBAR_ORDER_ANCHOR_REMOVED"'],
  ['remove Boolean-difference geometry corrections',
    'FrameCrossbarApplyBooleanDifferenceCorrections(model, math);',
    'model.ForceRebuild3(false);'],
  ['remove frame-crossbar zero-reference gate',
    'record.native_frame_crossbar_external_reference_count == 0',
    'record.native_frame_crossbar_external_reference_count >= 0'],
  ['remove frame-crossbar read-only hash equality',
    'NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_CHANGED_HASH", 41,',
    'NATIVE_FRAME_CROSSBAR_READONLY_REOPEN_HASH_GATE_REMOVED", 41,'],
]
const nativeFrameCrossbarMutationResults = nativeFrameCrossbarNegativeMutations
  .map(([name, find, replacement]) => {
    const mutated = source.replace(find, replacement)
    return { name, rejected: mutated !== source && !nativeFrameCrossbarGate(mutated) }
  })
check('native frame-crossbar negative controls are all rejected',
  nativeFrameCrossbarMutationResults.every((row) => row.rejected),
  JSON.stringify(nativeFrameCrossbarMutationResults))

const nativeDoorFrameRightNegativeMutations = [
  ['remove door-frame Boolean-equivalence evidence identity',
    '91C6BD7411B4CFDA007C599E8C4E708F168D33887E4DBDAC80D7B8D7789DAAC7',
    '0'.repeat(64)],
  ['restore right door-frame MirrorStock plan',
    'PNativeDoorFrameRight("门框 右.sldprt", "门框 左.sldprt",',
    'P("门框 右.sldprt", "门框 左.sldprt", "MirrorStock",'],
  ['disable right door-frame native plan flag',
    'native_door_frame_right = true',
    'native_door_frame_right = false'],
  ['change right door-frame repeated-hole count',
    'for (int index = 0; index < 11; index++)',
    'for (int index = 0; index < 10; index++)'],
  ['remove right door-frame split-line topology restoration',
    'DoorFrameRightApplyReferenceFaceSplits(model, math);',
    'model.ForceRebuild3(false);'],
  ['remove right door-frame zero-reference gate',
    'record.native_door_frame_right_external_reference_count == 0',
    'record.native_door_frame_right_external_reference_count >= 0'],
  ['remove right door-frame read-only hash equality',
    'NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,',
    'NATIVE_DOOR_FRAME_RIGHT_READONLY_REOPEN_HASH_GATE_REMOVED", 41,'],
  ['remove right door-frame Boolean input path binding',
    'SamePath(TextValue(difference, "candidate_path"), pilotPartPath)',
    'true'],
]
const nativeDoorFrameRightMutationResults = nativeDoorFrameRightNegativeMutations
  .map(([name, find, replacement]) => {
    let mutated
    if (name === 'change right door-frame repeated-hole count') {
      const cutDetails = method(source, 'DoorFrameRightCreateCutDetails')
      mutated = source.replace(cutDetails, cutDetails.replace(find, replacement))
    } else mutated = source.replace(find, replacement)
    return { name, rejected: mutated !== source && !nativeDoorFrameRightGate(mutated) }
  })
check('native right door-frame negative controls are all rejected',
  nativeDoorFrameRightMutationResults.every((row) => row.rejected),
  JSON.stringify(nativeDoorFrameRightMutationResults))

const nativeCabinetShelfRightNegativeMutations = [
  ['remove cabinet-shelf Boolean-equivalence evidence identity',
    '4475C4C99DE90C1D713286D12E1586D32C2A107512A58676E26D096045352CC7',
    '0'.repeat(64)],
  ['restore right cabinet-shelf MirrorStock plan',
    'PNativeCabinetShelfRight("箱体横层板R.SLDPRT", "箱体横层板L.sldprt",',
    'P("箱体横层板R.SLDPRT", "箱体横层板L.sldprt", "MirrorStock",'],
  ['disable right cabinet-shelf native plan flag',
    'native_cabinet_shelf_right = true',
    'native_cabinet_shelf_right = false'],
  ['change right cabinet-shelf relief ratio',
    '0.0, true, 0.5, true',
    '0.0, true, 0.333333, true'],
  ['remove exact cabinet-shelf Boolean corrections',
    'CabinetShelfRightApplyExactBooleanCorrections(model, math);',
    'model.ForceRebuild3(false);'],
  ['remove cabinet-shelf reference face splits',
    'CabinetShelfRightApplyReferenceFaceSplits(model, math);',
    'model.ForceRebuild3(false);'],
  ['remove cabinet-shelf zero-reference gate',
    'record.native_cabinet_shelf_right_external_reference_count == 0',
    'record.native_cabinet_shelf_right_external_reference_count >= 0'],
  ['remove cabinet-shelf read-only hash equality',
    'NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_CHANGED_HASH", 41,',
    'NATIVE_CABINET_SHELF_RIGHT_READONLY_REOPEN_HASH_GATE_REMOVED", 41,'],
  ['remove cabinet-shelf Boolean input path binding',
    'SamePath(TextValue(difference, "candidate_path"), pilotPartPath)',
    'true'],
]
const nativeCabinetShelfRightMutationResults = nativeCabinetShelfRightNegativeMutations
  .map(([name, find, replacement]) => {
    let mutated
    if (name === 'remove cabinet-shelf Boolean input path binding') {
      const validation = method(source, 'ValidateNativeCabinetShelfRightEvidenceInputs')
      mutated = source.replace(validation, validation.replace(find, replacement))
    } else mutated = source.replace(find, replacement)
    return { name, rejected: mutated !== source && !nativeCabinetShelfRightGate(mutated) }
  })
check('native right cabinet-shelf negative controls are all rejected',
  nativeCabinetShelfRightMutationResults.every((row) => row.rejected),
  JSON.stringify(nativeCabinetShelfRightMutationResults))

check('fixed planning-only recipe and stage-specific authorization path remain enforced',
  source.includes('BoolValue(qualityBoundary, "planningOnly")') &&
  source.includes('!BoolValue(executionBoundary, "executorImplemented")') &&
  source.includes('WINNSEN_NATIVE_EXECUTION_AUTH_PATH') &&
  source.includes('execution_authorizations') && source.includes('ExpectedToolId + ".json"') &&
  method(source, 'ValidateStageContracts').includes('native_seed_pack_888x14_v1') &&
  method(source, 'ValidateStageContracts').includes('native_width_888_v1') &&
  !method(source, 'ValidateStageContracts').includes('native_pack_and_go_v1'))
check('shared stage receipt and exact six-manifest plan contracts remain enforced',
  method(source, 'ValidateStageReceiptContracts').includes('clone_native_seed') &&
  method(source, 'ValidateStageReceiptContracts').includes('final_pack_and_relocated_reopen') &&
  method(source, 'ValidateToolchainManifest').includes('NormalizedToolSourceSha256') &&
  source.includes('TrustedToolchainManifestKeys') &&
  source.includes('seed_pack_888_native_v1/toolchain_manifest.json') &&
  source.includes('width_888_native_v1/toolchain_manifest.json'))
check('SolidWorks 2020 exact owned-process boundary remains enforced',
  source.includes('SldWorks.Application.28') && source.includes('ExpectedSolidWorksVersionPrefix = "28."') &&
  source.includes('ExpectedSolidWorksExeSha256') && source.includes('ExactProcessIdentityExists') &&
  source.includes('ExactOwnedMonitor'))
check('NeedsRegen warning is limited to the exact twenty-two assembly plans',
  assembliesGatePassed && assembliesGate.last?.warningGate === true &&
  method(source, 'AssemblyReopenGate').includes('AssemblyNeedsRegenWarningAllowlist().Contains(plan.file_name)'))
check('no legacy generator, STEP, FreeCAD, or completion-grade wording',
  !/FreeCAD|\.step\b|\.stp\b|legacy[_ -]?generator|production|release_ready|engineer[-_ ]?ready/i.test(source))

const before = processRows()
const identity = spawnSync(exePath, ['--identity'], { encoding: 'utf8', windowsHide: true })
const usage = spawnSync(exePath, [], { encoding: 'utf8', windowsHide: true })
const behavior = behaviorSelfTest()
const commitmentBehavior = evidenceCommitmentBehaviorTest()
const canonicalWidthEvidenceBehavior = canonicalWidthEvidenceBehaviorTest()
const after = processRows()
const executableHash = sha(readFileSync(exePath))
const identityText = `${identity.stdout || ''}${identity.stderr || ''}`
check('identity and usage paths start no CAD process', identity.status === 0 && usage.status === 2 &&
  before === '' && after === '',
  `identity=${identity.status}; usage=${usage.status}; CAD ${before}->${after}`)
check('preflight-only path is explicit and cannot enter CAD execution',
  source.includes('args[0], "--preflight-only"') &&
  source.includes('PREFLIGHT_PASS_CAD_NOT_STARTED') &&
  source.indexOf('AssertTrustedAttemptDirectory(preflightCadDir, preflightResult)') <
    source.indexOf('if (args.Length != 3)') &&
  source.indexOf('AssertInitialInventory(preflightCadDir, preflightResult, preflightPhase)') <
    source.indexOf('if (args.Length != 3)') &&
  source.indexOf('ValidatePhaseSequence(preflightResult, preflightPhase)') <
    source.indexOf('if (args.Length != 3)') &&
  source.indexOf('AssertAuthorizationStillValid(preflightResult,') <
    source.indexOf('if (args.Length != 3)') &&
  !method(source, 'AssertTrustedAttemptDirectory').includes('StartOwnedSession('))
check('width stage accepts the exact seed relocated-reopen warning contract',
  method(source, 'ValidateSeedReceipt').includes('ExactNumber(receipt, "reopenWarnings", 32.0) ||') &&
  method(source, 'ValidateSeedReceipt').includes('ExactNumber(receipt, "reopenWarnings", 96.0)') &&
  method(source, 'ValidateSeedReceipt').includes('NumberValue(receipt, "reopenWarnings")) & 1'))
check('dimensions binds the stabilized root from the consumed seed receipt',
  method(source, 'AssertPackInventoryAndDependencies').includes('result.seed_root_sha_after_stable') &&
  method(source, 'AssertPackInventoryAndDependencies').includes('STABILIZED_SEED_ROOT_SHA_DRIFT') &&
  !method(source, 'AssertPackInventoryAndDependencies').includes(
    'result.seed_root_sha256, ExpectedSeedRootSha256'))
check('compiled executable reports exact source and executable identity',
  identityText.includes(`sourceNormalizedSha256=${stampedSourceHash}`) &&
  identityText.includes(`executableSha256=${executableHash}`) &&
  identityText.includes('compiledSeedCount=75') &&
  identityText.includes(`compiledSeedInventoryDigest=${expectedSeedDigest}`))
check('private C# behavior gates reject malformed authorization and receipt fixtures and safely restore a hardlink',
  behavior.ok, JSON.stringify({ status: behavior.status, stdout: behavior.stdout, stderr: behavior.stderr }))
check('compiled C# evidence commitment digest matches shared JavaScript behavior',
  commitmentBehavior.ok, JSON.stringify(commitmentBehavior))
check('canonical width evidence removes binary float artifacts before shared commitment',
  canonicalWidthEvidenceBehavior.ok, JSON.stringify(canonicalWidthEvidenceBehavior))

const failures = checks.filter((item) => !item.ok)
let manifestSha256 = ''
if (failures.length === 0) {
  const manifest = {
    schema: 'winnsen.16029.native_toolchain_manifest.v1',
    generatedBy: verifierRelativePath,
    tool: {
      id: 'native_width_888_v1',
      sourcePath: sourceRelativePath,
      sourceNormalizedSha256: stampedSourceHash,
      executablePath: executableRelativePath,
      executableSha256: executableHash,
      verifierPath: verifierRelativePath,
      verifierSha256: sha(readFileSync(verifierPath)),
    },
  }
  const serializedManifest = `${JSON.stringify(manifest, null, 2)}\n`
  writeFileSync(manifestPath, serializedManifest, { encoding: 'utf8', flag: 'w' })
  manifestSha256 = sha(Buffer.from(serializedManifest, 'utf8'))
}
console.log(JSON.stringify({
  schema: 'winnsen.native_width_888.static_verification.v3',
  status: failures.length ? 'NO_GO' : 'STATIC_PASS_RUNTIME_NOT_RUN',
  solidWorksExecuted: false,
  checks: checks.length,
  passed: checks.length - failures.length,
  failed: failures.length,
  failures,
  negativeControls: mutationResults,
  baseHoleNegativeControls: baseHoleMutationResults,
  splitBodyLockNegativeControls: splitBodyLockMutationResults,
  assembliesNegativeControls: assembliesMutationResults,
  nativeRightNegativeControls: nativeRightMutationResults,
  nativeFrameCrossbarNegativeControls: nativeFrameCrossbarMutationResults,
  nativeDoorFrameRightNegativeControls: nativeDoorFrameRightMutationResults,
  nativeCabinetShelfRightNegativeControls: nativeCabinetShelfRightMutationResults,
  behaviorSelfTest: behavior.parsed,
  evidenceCommitmentBehaviorTest: commitmentBehavior,
  canonicalWidthEvidenceBehaviorTest: canonicalWidthEvidenceBehavior,
  sourceNormalizedSha256: stampedSourceHash,
  executableSha256: executableHash,
  toolchainManifestPath: manifestPath,
  toolchainManifestSha256: manifestSha256,
  repositoryRoot,
}, null, 2))
process.exit(failures.length ? 1 : 0)
