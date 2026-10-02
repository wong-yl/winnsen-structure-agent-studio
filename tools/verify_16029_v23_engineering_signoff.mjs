import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const REQUEST_ID = 'v43-int-v23-all-sources-isolated'
const CANDIDATE_ROOT = `workers/generated_models/review_generation_requests/${REQUEST_ID}/sw2020_full_740W_parametric_template`

function optionValue(name) {
  const index = process.argv.indexOf(name)
  return index >= 0 && process.argv[index + 1] ? process.argv[index + 1] : ''
}

const PATHS = {
  template: 'data/locker_16029_v23_engineering_signoff.template.json',
  signed: optionValue('--signed-file') || 'data/locker_16029_v23_engineering_signoff.json',
  gate: optionValue('--gate-file') || 'data/locker_16029_v23_engineering_signoff_gate.json',
  summary: `${CANDIDATE_ROOT}/solidworks_2020_full_assembly_generation_summary.json`,
  exactGate: `${CANDIDATE_ROOT}/evidence/v43_exact_structure_gate.json`,
  sourceIntegrity: `${CANDIDATE_ROOT}/evidence/controlled_source_integrity_result.json`,
  sourceProvenance: `${CANDIDATE_ROOT}/evidence/pre_signoff_review/source_provenance_report.json`,
  fullPackage: `workers/generation_logs/review_generation_${REQUEST_ID}_solidworks2020_full_assembly.zip`,
  preSignoffPackage: `workers/generation_logs/review_generation_${REQUEST_ID}_pre_signoff_review.zip`,
}

const REVIEW_ITEM_IDS = [
  'lock_common_datum_and_engagement',
  'shelf_front_frame_interface',
  'partition_reinforcement_and_weldability',
  'external_through_hole_disposition',
  'door_gap_sag_and_collision',
  'sheetmetal_process_and_tolerance',
  'touched_source_file_disposition',
]

function absolute(path) {
  return resolve(ROOT, path)
}

function loadJson(path) {
  const fullPath = absolute(path)
  if (!existsSync(fullPath)) return { ok: false, value: null, error: `missing: ${path}` }
  try {
    return { ok: true, value: JSON.parse(readFileSync(fullPath, 'utf8').replace(/^\uFEFF/, '')), error: '' }
  } catch (error) {
    return { ok: false, value: null, error: `invalid JSON ${path}: ${error.message}` }
  }
}

function fileRecord(path) {
  const fullPath = absolute(path)
  if (!existsSync(fullPath)) return null
  const buffer = readFileSync(fullPath)
  return {
    path,
    length: statSync(fullPath).size,
    sha256: createHash('sha256').update(buffer).digest('hex').toUpperCase(),
  }
}

function sameJson(a, b) {
  return JSON.stringify(a) === JSON.stringify(b)
}

const checks = []
function check(phase, name, ok, actual, expected) {
  checks.push({ phase, name, ok: Boolean(ok), actual, expected })
}

const summaryLoaded = loadJson(PATHS.summary)
const exactGateLoaded = loadJson(PATHS.exactGate)
const sourceIntegrityLoaded = loadJson(PATHS.sourceIntegrity)
const sourceProvenanceLoaded = loadJson(PATHS.sourceProvenance)
const templateLoaded = loadJson(PATHS.template)
const signedLoaded = loadJson(PATHS.signed)

for (const [name, loaded, path] of [
  ['summary_json', summaryLoaded, PATHS.summary],
  ['exact_gate_json', exactGateLoaded, PATHS.exactGate],
  ['source_integrity_json', sourceIntegrityLoaded, PATHS.sourceIntegrity],
  ['source_provenance_json', sourceProvenanceLoaded, PATHS.sourceProvenance],
]) {
  check('automatic_evidence', name, loaded.ok, loaded.error || path, 'existing valid JSON')
}

const summary = summaryLoaded.value || {}
const exactGate = exactGateLoaded.value || {}
const sourceIntegrity = sourceIntegrityLoaded.value || {}
const sourceProvenance = sourceProvenanceLoaded.value || {}

check('automatic_evidence', 'candidate_request_id', summary.requestId === REQUEST_ID, summary.requestId, REQUEST_ID)
check(
  'automatic_evidence',
  'candidate_classification',
  summary.status === 'solidworks_2020_controlled_candidate_pass',
  summary.status,
  'solidworks_2020_controlled_candidate_pass',
)
check('automatic_evidence', 'candidate_not_release_eligible', summary.releaseEligible === false, summary.releaseEligible, false)
check('automatic_evidence', 'exact_structure_gate_pass', exactGate.status === 'PASS', exactGate.status, 'PASS')
check(
  'automatic_evidence',
  'exact_structure_checks_failed_zero',
  Number(exactGate?.summary?.checksFailed) === 0,
  exactGate?.summary?.checksFailed,
  0,
)
check(
  'automatic_evidence',
  'controlled_source_integrity_pass',
  sourceIntegrity.status === 'PASS',
  sourceIntegrity.status,
  'PASS',
)
check(
  'automatic_evidence',
  'controlled_source_file_count_302',
  Number(sourceIntegrity.sourceFileCount) === 302,
  sourceIntegrity.sourceFileCount,
  302,
)
check(
  'automatic_evidence',
  'controlled_source_changed_zero',
  Number(sourceIntegrity.changedFileCount) === 0,
  sourceIntegrity.changedFileCount,
  0,
)
check(
  'automatic_evidence',
  'all_placement_sources_localized',
  Number(summary.localizedModuleTargetPlacementCount) === 39 && Number(summary.localizedRestoredV43PlacementCount) === 7,
  {
    module: summary.localizedModuleTargetPlacementCount,
    restored_v43: summary.localizedRestoredV43PlacementCount,
  },
  { module: 39, restored_v43: 7 },
)
check(
  'automatic_evidence',
  'final_assembly_external_references_zero',
  Number(summary.structureRecordExternalReferenceCount) === 0,
  summary.structureRecordExternalReferenceCount,
  0,
)
check(
  'automatic_evidence',
  'historical_source_provenance_partial',
  sourceProvenance.status === 'EVIDENCE_COLLECTED_HISTORICAL_PROVENANCE_PARTIAL',
  sourceProvenance.status,
  'EVIDENCE_COLLECTED_HISTORICAL_PROVENANCE_PARTIAL',
)
check(
  'automatic_evidence',
  'automatic_source_restore_forbidden',
  sourceProvenance.automatic_restore_allowed === false &&
    sourceProvenance.byte_identical_pre_v21_source_proven_for_all_touched_files === false,
  {
    automatic_restore_allowed: sourceProvenance.automatic_restore_allowed,
    byte_identity_proven: sourceProvenance.byte_identical_pre_v21_source_proven_for_all_touched_files,
  },
  { automatic_restore_allowed: false, byte_identity_proven: false },
)
check(
  'automatic_evidence',
  'source_provenance_review_changed_zero',
  Number(sourceProvenance?.source_inspection_guard?.source_changed_count) === 0,
  sourceProvenance?.source_inspection_guard?.source_changed_count,
  0,
)

const bindingPaths = [
  PATHS.summary,
  PATHS.exactGate,
  PATHS.sourceIntegrity,
  PATHS.sourceProvenance,
  PATHS.fullPackage,
  PATHS.preSignoffPackage,
]
const actualBinding = bindingPaths.map(fileRecord)
for (let index = 0; index < actualBinding.length; index += 1) {
  check(
    'automatic_evidence',
    `evidence_binding_file_${index + 1}`,
    actualBinding[index] !== null,
    actualBinding[index]?.path || `missing: ${bindingPaths[index]}`,
    'existing file with SHA256',
  )
}

check('template', 'template_json', templateLoaded.ok, templateLoaded.error || PATHS.template, 'existing valid JSON')
const template = templateLoaded.value || {}
check(
  'template',
  'template_schema',
  template.schema === 'winnsen.locker16029.v43_engineering_signoff.v1',
  template.schema,
  'winnsen.locker16029.v43_engineering_signoff.v1',
)
check('template', 'template_status', template.status === 'TEMPLATE_NOT_SIGNED', template.status, 'TEMPLATE_NOT_SIGNED')
check('template', 'template_request_id', template.candidate_request_id === REQUEST_ID, template.candidate_request_id, REQUEST_ID)
check('template', 'template_decision_unreviewed', template.decision === 'UNREVIEWED', template.decision, 'UNREVIEWED')
check(
  'template',
  'template_not_accepted',
  template.accepted_for_prototype === false && template.production_release_eligible === false,
  {
    accepted_for_prototype: template.accepted_for_prototype,
    production_release_eligible: template.production_release_eligible,
  },
  { accepted_for_prototype: false, production_release_eligible: false },
)
check(
  'template',
  'template_evidence_binding_current',
  actualBinding.every(Boolean) && sameJson(template.evidence_binding, actualBinding),
  template.evidence_binding,
  actualBinding,
)
const templateReviewIds = Array.isArray(template.review_items) ? template.review_items.map((item) => item.id) : []
check(
  'template',
  'template_review_items_exact',
  sameJson(templateReviewIds, REVIEW_ITEM_IDS),
  templateReviewIds,
  REVIEW_ITEM_IDS,
)
check(
  'template',
  'template_signer_blank',
  !String(template?.signer?.structure_engineer || '').trim() && !String(template?.signer?.signed_at || '').trim(),
  template.signer,
  'blank structure engineer and signed_at',
)

const signedFileExists = existsSync(absolute(PATHS.signed))
const signed = signedLoaded.value || {}
if (signedFileExists) {
  check('signed_contract', 'signed_json_valid', signedLoaded.ok, signedLoaded.error || PATHS.signed, 'valid JSON')
  check(
    'signed_contract',
    'signed_schema',
    signed.schema === 'winnsen.locker16029.v43_engineering_signoff.v1',
    signed.schema,
    'winnsen.locker16029.v43_engineering_signoff.v1',
  )
  check('signed_contract', 'signed_status', signed.status === 'SIGNED', signed.status, 'SIGNED')
  check('signed_contract', 'signed_request_id', signed.candidate_request_id === REQUEST_ID, signed.candidate_request_id, REQUEST_ID)
  check(
    'signed_contract',
    'signed_evidence_binding_unchanged',
    sameJson(signed.evidence_binding, actualBinding),
    signed.evidence_binding,
    actualBinding,
  )
  check(
    'signed_contract',
    'signed_production_release_forbidden',
    signed.production_release_eligible === false,
    signed.production_release_eligible,
    false,
  )
  check(
    'signed_contract',
    'historical_source_boundary_acknowledged',
    signed.historical_source_provenance_acknowledged === true,
    signed.historical_source_provenance_acknowledged,
    true,
  )
  check(
    'signed_contract',
    'prototype_boundary_acknowledged',
    signed.prototype_boundary_acknowledged === true,
    signed.prototype_boundary_acknowledged,
    true,
  )

  const reviewItems = Array.isArray(signed.review_items) ? signed.review_items : []
  const reviewIds = reviewItems.map((item) => item.id)
  const reviewResults = reviewItems.map((item) => item.result)
  const allResultsValid = reviewResults.every((result) => result === 'PASS' || result === 'FAIL')
  const allEvidenceAttached = reviewItems.every((item) => String(item.evidence || '').trim().length > 0)
  check('signed_contract', 'signed_review_items_exact', sameJson(reviewIds, REVIEW_ITEM_IDS), reviewIds, REVIEW_ITEM_IDS)
  check('signed_contract', 'signed_review_results_complete', allResultsValid, reviewResults, 'PASS or FAIL for every item')
  check('signed_contract', 'signed_review_evidence_complete', allEvidenceAttached, reviewItems.map((item) => item.evidence), 'non-empty evidence for every item')

  const decisionAllowed = signed.decision === 'ACCEPT_FOR_PROTOTYPE' || signed.decision === 'RETURN_FOR_STRUCTURE_REVISION'
  const allPass = reviewResults.length === REVIEW_ITEM_IDS.length && reviewResults.every((result) => result === 'PASS')
  const hasFail = reviewResults.some((result) => result === 'FAIL')
  const decisionConsistent =
    (signed.decision === 'ACCEPT_FOR_PROTOTYPE' && signed.accepted_for_prototype === true && allPass) ||
    (signed.decision === 'RETURN_FOR_STRUCTURE_REVISION' && signed.accepted_for_prototype === false && hasFail)
  check(
    'signed_contract',
    'signed_decision_allowed',
    decisionAllowed,
    signed.decision,
    ['ACCEPT_FOR_PROTOTYPE', 'RETURN_FOR_STRUCTURE_REVISION'],
  )
  check(
    'signed_contract',
    'signed_decision_consistent_with_results',
    decisionConsistent,
    { decision: signed.decision, accepted_for_prototype: signed.accepted_for_prototype, review_results: reviewResults },
    'accept only when all PASS; return for revision when at least one FAIL',
  )
  check(
    'signed_contract',
    'structure_engineer_named',
    String(signed?.signer?.structure_engineer || '').trim().length > 0,
    signed?.signer?.structure_engineer,
    'non-empty structure engineer',
  )
  check(
    'signed_contract',
    'signed_at_iso_timestamp',
    Number.isFinite(Date.parse(String(signed?.signer?.signed_at || ''))),
    signed?.signer?.signed_at,
    'ISO-8601 timestamp',
  )
}

const failedAutomatic = checks.filter((item) => item.phase === 'automatic_evidence' && !item.ok)
const failedTemplate = checks.filter((item) => item.phase === 'template' && !item.ok)
const failedSigned = checks.filter((item) => item.phase === 'signed_contract' && !item.ok)
const signedFileValid = signedFileExists && signedLoaded.ok && failedSigned.length === 0

let status = 'AWAITING_ENGINEERING_SIGNOFF'
if (failedAutomatic.length > 0) status = 'INVALID_AUTOMATIC_EVIDENCE'
else if (failedTemplate.length > 0) status = 'INVALID_TEMPLATE'
else if (signedFileExists && !signedFileValid) status = 'INVALID_ENGINEERING_SIGNOFF'
else if (signedFileValid && signed.decision === 'RETURN_FOR_STRUCTURE_REVISION') status = 'STRUCTURE_REVISION_REQUIRED'
else if (signedFileValid && signed.decision === 'ACCEPT_FOR_PROTOTYPE') status = 'READY_FOR_PROTOTYPE'

const nextActions = {
  AWAITING_ENGINEERING_SIGNOFF:
    '下载模板，完成 7 项结构审查后，将完整签字副本保存为 data/locker_16029_v23_engineering_signoff.json。',
  INVALID_AUTOMATIC_EVIDENCE: '先修复或重新生成 v23 自动证据，再请求结构工程签核。',
  INVALID_TEMPLATE: '刷新受控模板，使证据 SHA256 和审查项契约与当前 v23 候选一致。',
  INVALID_ENGINEERING_SIGNOFF: '修复签字 JSON；不得把部分填写或格式错误的文件当作工程决定。',
  STRUCTURE_REVISION_REQUIRED: '把失败审查项退回 SolidWorks 2020 结构修订路线，生成新的受控候选。',
  READY_FOR_PROTOTYPE: '冻结本次已审候选并安排样机；生产释放仍受样机和独立 release gate 阻挡。',
}

const reviewItems = signedFileValid && Array.isArray(signed.review_items) ? signed.review_items : []
const gate = {
  schema: 'winnsen.locker16029.v23_engineering_signoff_gate.v1',
  generated_at: new Date().toISOString(),
  candidate_request_id: REQUEST_ID,
  status,
  automatic_evidence_pass: failedAutomatic.length === 0,
  template_valid: failedTemplate.length === 0,
  signed_file_exists: signedFileExists,
  signed_file_valid: signedFileValid,
  engineering_review_accepted: status === 'READY_FOR_PROTOTYPE',
  ready_for_prototype: status === 'READY_FOR_PROTOTYPE',
  prototype_validation_status: 'NOT_RUN_REQUIRED_BEFORE_RELEASE',
  production_release_eligible: false,
  review_decision: signedFileValid ? signed.decision : 'UNREVIEWED',
  failed_review_item_ids: reviewItems.filter((item) => item.result === 'FAIL').map((item) => item.id),
  checks_total: checks.length,
  checks_failed: checks.filter((item) => !item.ok).length,
  validation_errors: checks.filter((item) => !item.ok).map((item) => `${item.phase}:${item.name}`),
  checks,
  paths: {
    template: PATHS.template,
    signed_file: PATHS.signed,
    template_download_url: '/api/review-downloads/16029-v43-internal-sheetmetal-v23-engineering-signoff-template',
    gate: PATHS.gate,
  },
  next_action: nextActions[status],
  status_note:
    'READY_FOR_PROTOTYPE 只表示工程预签核允许进入样机，不释放图纸、DXF、BOM、供应商工艺、载荷、涂层、公差链或生产。',
}

mkdirSync(dirname(absolute(PATHS.gate)), { recursive: true })
writeFileSync(absolute(PATHS.gate), `${JSON.stringify(gate, null, 2)}\n`, 'utf8')
console.log(JSON.stringify(gate, null, 2))

const strictSigned = process.argv.includes('--strict-signed')
if (status.startsWith('INVALID_')) process.exit(1)
if (strictSigned && status !== 'READY_FOR_PROTOTYPE') process.exit(2)
