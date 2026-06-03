import { existsSync, readFileSync, statSync } from 'node:fs'
import { basename, resolve } from 'node:path'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const MANIFEST_PATH = resolve(ROOT, 'data/locker_16029_v43_internal_sheetmetal_delivery.json')

function readText(path) {
  return readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
}

function readJson(path) {
  return JSON.parse(readText(path))
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

const checks = []

function check(name, ok, actual, expected) {
  checks.push({ name, ok: Boolean(ok), actual, expected })
}

function checkFile(name, path) {
  const ok = existsSync(path)
  check(name, ok, path, 'file or directory exists')
  return ok
}

const manifest = readJson(MANIFEST_PATH)
const requestId = manifest.current_request_id
const delivery = manifest.delivery || {}
const evidence = manifest.evidence || {}
const verified = manifest.verified || {}

check('manifest_status', manifest.status === 'sw2020_review_ready', manifest.status, 'sw2020_review_ready')
check('manifest_current_request_id', requestId === 'v43-int-v18-lockfix', requestId, 'v43-int-v18-lockfix')
check('manifest_route_width', asNumber(manifest.route?.cabinet_width_mm) === 740, manifest.route?.cabinet_width_mm, 740)
check('manifest_route_sequence', manifest.route?.row_sequence === 'L642-R246', manifest.route?.row_sequence, 'L642-R246')
check('manifest_cad_mainline', manifest.route?.cad_mainline === 'SolidWorks 2020', manifest.route?.cad_mainline, 'SolidWorks 2020')
check('manifest_user_visual_confirmed', verified.user_visual_confirmed === true, verified.user_visual_confirmed, true)

for (const [key, path] of Object.entries(delivery)) {
  if (key.endsWith('_url') || key.endsWith('_id')) continue
  if (key === 'screenshot_folder') continue
  checkFile(`delivery_path_exists:${key}`, path)
}
check('delivery_screenshot_folder_recorded', Boolean(delivery.screenshot_folder), delivery.screenshot_folder, 'screenshot folder path recorded')

for (const [key, path] of Object.entries(evidence)) {
  checkFile(`evidence_path_exists:${key}`, path)
}

const zipStat = existsSync(delivery.zip_path) ? statSync(delivery.zip_path) : null
check('zip_size_nontrivial', Boolean(zipStat && zipStat.size > 95 * 1024 * 1024), zipStat?.size ?? 0, '> 95 MB')

const summary = readJson(evidence.summary)
check('summary_status', summary.status === verified.generation_status, summary.status, verified.generation_status)
check('summary_request_id', summary.requestId === requestId, summary.requestId, requestId)
check('summary_gold_gate_status', summary.goldStructureGateStatus === 'PASS', summary.goldStructureGateStatus, 'PASS')
check('summary_structure_feedback_status', summary.structureFeedbackStatus === 'clean', summary.structureFeedbackStatus, 'clean')
check('summary_door_lock_tongue_count', asNumber(summary.doorLockTongueCount) === 6, summary.doorLockTongueCount, 6)
check('summary_door_lock_tongue_restore_added', asNumber(summary.doorLockTongueRestoreAddedCount) === 6, summary.doorLockTongueRestoreAddedCount, 6)
check('summary_door_lock_tongue_restore_failed_zero', asNumber(summary.doorLockTongueRestoreFailedCount) === 0, summary.doorLockTongueRestoreFailedCount, 0)
check('summary_electric_components_zero', asNumber(summary.electricalOrElectricLockComponentCount) === 0, summary.electricalOrElectricLockComponentCount, 0)
check('summary_back_seam_centered', summary.backSheetMetalRepairStatus === verified.back_seam_status, summary.backSheetMetalRepairStatus, verified.back_seam_status)

const gate = readJson(evidence.gold_structure_gate)
check('gold_structure_gate_pass', gate.status === 'PASS', gate.status, 'PASS')
check('gold_structure_gate_issue_count_zero', (gate.issues || []).length === 0, (gate.issues || []).length, 0)

const feedback = readJson(evidence.structure_feedback)
check('structure_feedback_clean', feedback.status === 'clean', feedback.status, 'clean')
check('structure_feedback_issue_count_zero', (feedback.issues || []).length === 0, (feedback.issues || []).length, 0)
check('structure_feedback_door_lock_tongue_count', asNumber(feedback.derived?.doorLockTongueCount) === 6, feedback.derived?.doorLockTongueCount, 6)

const restore = readJson(evidence.door_lock_tongue_restore)
check('door_lock_tongue_restore_status', restore.status === 'restored', restore.status, 'restored')
check('door_lock_tongue_restore_added_count', asNumber(restore.addedCount ?? restore.added_count) === 6, restore.addedCount ?? restore.added_count, 6)
check('door_lock_tongue_restore_failed_zero', asNumber(restore.failedCount ?? restore.failed_count) === 0, restore.failedCount ?? restore.failed_count, 0)
check('door_lock_tongue_restore_items_count', Array.isArray(restore.items) && restore.items.length === 6, restore.items?.length ?? 0, 6)
check('door_lock_tongue_restore_items_saved', (restore.items || []).every((item) => item.added === true && item.saved === true && !item.error), restore.items || [], 'all added and saved')

const requestPath = resolve(ROOT, `data/review_generation_requests/${requestId}.json`)
checkFile('request_record_exists', requestPath)
const requestRecord = readJson(requestPath)
check('request_status_ready', requestRecord.status === 'solidworks_2020_full_assembly_ready', requestRecord.status, 'solidworks_2020_full_assembly_ready')
check('request_download_url', requestRecord.downloadUrl === delivery.download_url, requestRecord.downloadUrl, delivery.download_url)
check('request_zip_path', requestRecord.zipPath === delivery.zip_path, requestRecord.zipPath, delivery.zip_path)
check('request_door_lock_tongue_count', asNumber(requestRecord.doorLockTongueCount) === 6, requestRecord.doorLockTongueCount, 6)
check('request_electric_components_zero', asNumber(requestRecord.electricalOrElectricLockComponentCount) === 0, requestRecord.electricalOrElectricLockComponentCount, 0)

const indexPath = resolve(ROOT, 'data/review_generation_requests/generation_request_index.json')
checkFile('generation_index_exists', indexPath)
const index = readJson(indexPath)
const requests = Array.isArray(index.requests) ? index.requests : []
const currentIndexRecord = requests.find((item) => item.id === requestId)
check('generation_index_current_record_present', Boolean(currentIndexRecord), currentIndexRecord?.id ?? '', requestId)
check('generation_index_current_record_download_url', currentIndexRecord?.downloadUrl === delivery.download_url, currentIndexRecord?.downloadUrl ?? '', delivery.download_url)
check('generation_index_current_record_zip_path', currentIndexRecord?.zipPath === delivery.zip_path, currentIndexRecord?.zipPath ?? '', delivery.zip_path)

const apiText = readText(resolve(ROOT, 'services/api/app/main.py'))
check('api_catalog_current_asset_id', apiText.includes(delivery.api_download_asset_id), delivery.api_download_asset_id, 'listed in FastAPI review_download_catalog')
check('api_catalog_current_zip', apiText.includes(basename(delivery.zip_path)), basename(delivery.zip_path), 'listed in FastAPI review_download_catalog')
check('api_current_scope_v43', apiText.includes('16029 740W / L642-R246 / v43'), 'FastAPI current route text', 'v43 current route')

const portalText = readText(resolve(ROOT, 'tools/serve_16029_review_downloads.mjs'))
check('portal_current_request_id', portalText.includes(requestId), requestId, 'listed in review portal')
check('portal_manifest_filter', portalText.includes('readCurrentDeliveryManifest') && portalText.includes('isVisibleCurrentGenerationRequest'), 'portal current request filtering', 'manifest-based current list')
check('portal_default_prompt_no_electric_lock_body', portalText.includes('no electrical board') && portalText.includes('no cabinet-side electric lock body'), 'portal default prompt', 'electrical hardware exclusion text')
check('portal_not_800w_current_round', !portalText.includes('<strong>16029 800W gold-variable</strong>'), 'old 800W current round label absent', 'not current')

const appText = readText(resolve(ROOT, 'apps/web/src/App.tsx'))
check('app_handoff_v43_copy', appText.includes('16029 740W / L642-R246 / v43'), 'App.tsx handoff copy', 'v43 current copy')
check('app_current_asset_good_status', appText.includes("asset.id.includes('v43-internal-sheetmetal')"), 'App.tsx asset status tone', 'current v43 asset highlighted')

const studioDataText = readText(resolve(ROOT, 'apps/web/src/data/studioData.ts'))
check('studio_data_current_v43_capability', studioDataText.includes("id: 'locker_16029_v43_internal_sheetmetal_current'"), 'studioData.ts capability', 'v43 current capability exists')
check('studio_data_current_metric', studioDataText.includes("value: '740W v43'"), 'studioData.ts metric', '740W v43 current metric')

const failed = checks.filter((item) => !item.ok)
const report = {
  schema: 'winnsen.locker16029.v43_delivery_manifest_gate.v1',
  status: failed.length ? 'FAIL' : 'PASS',
  manifest: MANIFEST_PATH,
  checks_total: checks.length,
  checks_failed: failed.length,
  checks,
}

console.log(JSON.stringify(report, null, 2))
if (failed.length) process.exit(1)
