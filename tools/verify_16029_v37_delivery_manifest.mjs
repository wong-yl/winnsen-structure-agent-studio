import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const MANIFEST_PATH = resolve(ROOT, 'data/locker_16029_v37_engineering_assistance_delivery.json')
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const NATIVE_GENERATOR_PATH = resolve(ROOT, 'tools/locker_16029_native_generator.mjs')
const EXPECTED_REQUEST_ID = 'v43-int-v37-760w-six-door-l642-r246-r1'
const EXPECTED_ROUND = '16029-v43-v37-760w-six-door-engineering-assistance-20260811'
const EXPECTED_ASSET_ID = '16029-v43-v37-760w-six-door-engineering-assistance-zip'
const EXPECTED_ZIP_NAME = '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'

const checks = []

function add(name, ok, actual, expected) {
  checks.push({ name, ok: Boolean(ok), actual, expected })
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
}

function exactReferenceGate(referenceState, mode) {
  const statuses = Array.isArray(referenceState?.statuses) ? referenceState.statuses.map(Number) : []
  const records = Array.isArray(referenceState?.records) ? referenceState.records : []
  return referenceState?.gate === true &&
    referenceState?.mode === mode &&
    Number(referenceState?.count) === 7 &&
    statuses.length === 7 && statuses.every((status) => status === 0) &&
    records.length === 7 && records.every((record) => Number(record.status) === 0)
}

add('manifest_exists', existsSync(MANIFEST_PATH), MANIFEST_PATH, 'existing file')

if (existsSync(MANIFEST_PATH)) {
  const manifest = readJson(MANIFEST_PATH)
  const zipPath = String(manifest.delivery?.zip_path || '')
  const receiptPath = String(manifest.delivery?.receipt_path || '')
  const packageManifestPath = String(manifest.evidence?.package_manifest || '')
  const validatorPath = String(manifest.evidence?.final_validator || '')
  const rootPath = String(manifest.delivery?.primary_assembly || '')
  const portalText = readFileSync(PORTAL_PATH, 'utf8')
  const nativeGeneratorText = readFileSync(NATIVE_GENERATOR_PATH, 'utf8')

  add('manifest_schema', manifest.schema === 'winnsen.locker16029.v37_engineering_assistance_delivery.v1', manifest.schema, 'winnsen.locker16029.v37_engineering_assistance_delivery.v1')
  add('request_round_asset_exact', manifest.current_request_id === EXPECTED_REQUEST_ID && manifest.review_round === EXPECTED_ROUND && manifest.asset_id === EXPECTED_ASSET_ID, { requestId: manifest.current_request_id, round: manifest.review_round, assetId: manifest.asset_id }, { requestId: EXPECTED_REQUEST_ID, round: EXPECTED_ROUND, assetId: EXPECTED_ASSET_ID })
  add('exact_recipe', Number(manifest.route?.cabinet_width_mm) === 760 && Number(manifest.route?.cabinet_height_mm) === 1917 && Number(manifest.route?.cabinet_depth_mm) === 550 && Number(manifest.route?.columns) === 2 && Number(manifest.route?.door_count) === 6 && Number(manifest.route?.door_leaf_width_mm) === 317 && manifest.route?.row_sequence === 'L642-R246', manifest.route, { width: 760, height: 1917, depth: 550, columns: 2, doorCount: 6, doorLeafWidth: 317, rowSequence: 'L642-R246' })
  add('honest_release_boundaries', manifest.verified?.engineering_use_eligible === true && manifest.verified?.production_release_eligible === false && manifest.verified?.release_ready === false && manifest.verified?.open_rebuild_required === true && manifest.verified?.warning_free === false && manifest.verified?.full_assembly_validation_all_green === false, manifest.verified, { engineeringUseEligible: true, productionReleaseEligible: false, releaseReady: false, openRebuildRequired: true, warningFree: false, fullAssemblyValidationAllGreen: false })
  add('known_issue_exact', manifest.known_issue?.is_only_known_non_green_item === true && manifest.known_issue?.component === '箱体右侧板焊接-1' && manifest.known_issue?.type === 'Reference' && Number(manifest.known_issue?.code) === 51 && manifest.known_issue?.warning === true, manifest.known_issue, { only: true, component: '箱体右侧板焊接-1', type: 'Reference', code: 51, warning: true })
  add('manifest_right_reference_semantics', Number(manifest.right_partition_external_references?.count) === 7 && manifest.right_partition_external_references?.all_broken_status_zero === true && manifest.right_partition_external_references?.baseline_provenance_mode === 'V35_DORMANT' && manifest.right_partition_external_references?.final_reopen_provenance_mode === 'FINAL_LOCAL_RESOLVED' && manifest.right_partition_external_references?.final_paths_all_package_local === true, manifest.right_partition_external_references, { count: 7, BrokenStatus: 0, baseline: 'V35_DORMANT', final: 'FINAL_LOCAL_RESOLVED', finalLocal: true })

  add('zip_exists', Boolean(zipPath) && existsSync(zipPath), zipPath, 'existing ZIP')
  add('receipt_exists', Boolean(receiptPath) && existsSync(receiptPath), receiptPath, 'existing detached receipt')
  add('package_manifest_exists', Boolean(packageManifestPath) && existsSync(packageManifestPath), packageManifestPath, 'existing package manifest')
  add('validator_exists', Boolean(validatorPath) && existsSync(validatorPath), validatorPath, 'existing final validator evidence')
  add('root_exists', Boolean(rootPath) && existsSync(rootPath), rootPath, 'existing final root assembly')

  if (zipPath && existsSync(zipPath)) {
    const zipSize = statSync(zipPath).size
    const zipSha = sha256(zipPath)
    add('zip_name_size_sha_pinned', zipPath.endsWith(EXPECTED_ZIP_NAME) && zipSize === Number(manifest.delivery.zip_size_bytes) && zipSha === String(manifest.delivery.zip_sha256 || '').toUpperCase(), { name: zipPath.split(/[\\/]/).at(-1), size: zipSize, sha256: zipSha }, { name: EXPECTED_ZIP_NAME, size: manifest.delivery.zip_size_bytes, sha256: manifest.delivery.zip_sha256 })
  }

  if (receiptPath && existsSync(receiptPath)) {
    const receipt = readJson(receiptPath)
    const stream = receipt.zipStreamVerification || {}
    add('receipt_sha_pinned', sha256(receiptPath) === String(manifest.delivery.receipt_sha256 || '').toUpperCase(), sha256(receiptPath), manifest.delivery.receipt_sha256)
    add('receipt_pass_and_matches_manifest', receipt.status === 'V37_ENGINEERING_ASSIST_PACKAGE_STREAM_VERIFIED' && receipt.requestId === EXPECTED_REQUEST_ID && receipt.reviewRound === EXPECTED_ROUND && receipt.zipFileName === EXPECTED_ZIP_NAME && Number(receipt.zipSizeBytes) === Number(manifest.delivery.zip_size_bytes) && String(receipt.zipSha256 || '').toUpperCase() === String(manifest.delivery.zip_sha256 || '').toUpperCase() && receipt.zipSingleRootDirectory === true && receipt.zipRootDirectory === 'review_package_v37_20260811' && stream.allEntriesReadToEnd === true && stream.allEntriesMatchedSource === true && (stream.missingEntries || []).length === 0 && (stream.extraEntries || []).length === 0 && (stream.mismatchedEntries || []).length === 0 && receipt.engineeringUseEligible === true && receipt.productionReleaseEligible === false && receipt.releaseReady === false && receipt.openRebuildRequired === true && receipt.warningFree === false && receipt.fullAssemblyValidationAllGreen === false, { status: receipt.status, requestId: receipt.requestId, reviewRound: receipt.reviewRound, zipFileName: receipt.zipFileName, zipSizeBytes: receipt.zipSizeBytes, zipSha256: receipt.zipSha256, singleRoot: receipt.zipSingleRootDirectory, rootDirectory: receipt.zipRootDirectory, stream, boundaries: { engineeringUseEligible: receipt.engineeringUseEligible, productionReleaseEligible: receipt.productionReleaseEligible, releaseReady: receipt.releaseReady, openRebuildRequired: receipt.openRebuildRequired, warningFree: receipt.warningFree, fullAssemblyValidationAllGreen: receipt.fullAssemblyValidationAllGreen } }, { status: 'V37_ENGINEERING_ASSIST_PACKAGE_STREAM_VERIFIED', exactRequestRoundZip: true, singleRootDirectory: true, streamVerified: true, honestBoundaries: true })
  }

  if (packageManifestPath && existsSync(packageManifestPath)) {
    const packageManifest = readJson(packageManifestPath)
    const knownIssues = Array.isArray(packageManifest.knownIssues) ? packageManifest.knownIssues : []
    const known = knownIssues[0] || {}
    add('package_manifest_identity_and_gate', packageManifest.reviewRound === EXPECTED_ROUND && packageManifest.assetId === EXPECTED_ASSET_ID && packageManifest.feedbackIdPrefix === 'V37-Q-' && packageManifest.validation?.status === 'PASS' && Number(packageManifest.validation?.checkCount) === 132 && Number(packageManifest.validation?.failedCheckCount) === 0 && packageManifest.engineeringUseEligible === true && packageManifest.productionReleaseEligible === false && packageManifest.releaseReady === false && packageManifest.openRebuildRequired === true && packageManifest.warningFree === false && packageManifest.fullAssemblyValidationAllGreen === false, { reviewRound: packageManifest.reviewRound, assetId: packageManifest.assetId, feedbackIdPrefix: packageManifest.feedbackIdPrefix, validation: packageManifest.validation, boundaries: { engineeringUseEligible: packageManifest.engineeringUseEligible, productionReleaseEligible: packageManifest.productionReleaseEligible, releaseReady: packageManifest.releaseReady, openRebuildRequired: packageManifest.openRebuildRequired, warningFree: packageManifest.warningFree, fullAssemblyValidationAllGreen: packageManifest.fullAssemblyValidationAllGreen } }, { exactIdentity: true, validator: 'PASS 132/132', honestBoundaries: true })
    add('package_manifest_known_issue_exact', Number(packageManifest.knownIssueCount) === 1 && knownIssues.length === 1 && known.component === '箱体右侧板焊接-1' && known.type === 'Reference' && Number(known.errorCode) === 51 && known.warning === true, { knownIssueCount: packageManifest.knownIssueCount, known }, { knownIssueCount: 1, component: '箱体右侧板焊接-1', type: 'Reference', code: 51, warning: true })
  }

  if (validatorPath && existsSync(validatorPath)) {
    const validator = readJson(validatorPath)
    const known = validator.allowedKnownIssue || {}
    add('final_validator_pass_132_of_132', validator.status === 'PASS' && Array.isArray(validator.checks) && validator.checks.length === 132 && Number(validator.failedCheckCount) === 0 && validator.engineeringUseEligible === true && validator.productionReleaseEligible === false, { status: validator.status, checks: validator.checks?.length, failed: validator.failedCheckCount, engineeringUseEligible: validator.engineeringUseEligible, productionReleaseEligible: validator.productionReleaseEligible }, { status: 'PASS', checks: 132, failed: 0, engineeringUseEligible: true, productionReleaseEligible: false })
    add('validator_model_and_root_hash_exact', Number(validator.model?.widthMm) === 760 && Number(validator.model?.heightMm) === 1917 && Number(validator.model?.depthMm) === 550 && Number(validator.model?.doorCount) === 6 && Number(validator.model?.doorLeafWidthMm) === 317 && validator.model?.layout === 'L642-R246' && rootPath === validator.model?.rootPath && (!existsSync(rootPath) || sha256(rootPath) === String(validator.model?.rootSha256 || '').toUpperCase()), validator.model, { exactRecipeAndRootHash: true })
    add('validator_known_issue_exact', known.component === '箱体右侧板焊接-1' && known.type === 'Reference' && Number(known.code) === 51 && known.warning === true, known, { component: '箱体右侧板焊接-1', type: 'Reference', code: 51, warning: true })
    add('validator_right_reference_provenance_transition', exactReferenceGate(validator.rightPartition?.readonlyReferences, 'V35_DORMANT') && exactReferenceGate(validator.rightPartition?.reopenReferences, 'FINAL_LOCAL_RESOLVED'), { readonly: { mode: validator.rightPartition?.readonlyReferences?.mode, count: validator.rightPartition?.readonlyReferences?.count, statuses: validator.rightPartition?.readonlyReferences?.statuses }, reopen: { mode: validator.rightPartition?.reopenReferences?.mode, count: validator.rightPartition?.reopenReferences?.count, statuses: validator.rightPartition?.reopenReferences?.statuses } }, { readonly: '7 x status0 V35_DORMANT', reopen: '7 x status0 FINAL_LOCAL_RESOLVED' })
  }

  add('portal_binding_exact', portalText.includes(`id: '${EXPECTED_ASSET_ID}'`) && portalText.includes(`CURRENT_REVIEW_ASSET_ID = '${EXPECTED_ASSET_ID}'`) && portalText.includes(`id: '${EXPECTED_ROUND}'`) && portalText.includes(`fileName: '${EXPECTED_ZIP_NAME}'`) && portalText.includes(`sha256: '${manifest.delivery?.zip_sha256}'`) && portalText.includes(`expectedSizeBytes: ${manifest.delivery?.zip_size_bytes}`) && portalText.includes('openRebuildRequired: true') && portalText.includes('warningFree: false') && portalText.includes('fullAssemblyValidationAllGreen: false'), 'portal source inspected', 'exact V37 round/asset/ZIP/hash/size and rebuild notes')
  add('native_generator_binding_exact_and_history_retained', nativeGeneratorText.includes("id: 'v37-760w-six-door-l642-r246'") && nativeGeneratorText.includes(`assetId: '${EXPECTED_ASSET_ID}'`) && nativeGeneratorText.includes("id: 'v36-740w-four-door-l66-r66'") && nativeGeneratorText.includes("id: 'v35-740w-six-door-l642-r246'") && nativeGeneratorText.includes("sourcePolicy: 'verified_native_seeds_only'"), 'native generator source inspected', 'V37 exact recipe plus V36/V35 native seeds')
}

const failed = checks.filter((check) => !check.ok)
const result = {
  schema: 'winnsen.locker16029.v37_engineering_assistance_delivery_gate.v1',
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: checks.length,
  checksFailed: failed.length,
  checks,
}

process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exitCode = 1
