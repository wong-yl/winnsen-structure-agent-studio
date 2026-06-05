import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const QUEUE_WORKER_PATH = resolve(ROOT, 'tools/process_16029_review_generation_queue.mjs')
const POLICY_PATH = resolve(ROOT, 'tools/locker_16029_controlled_generation_policy.mjs')
const source = readFileSync(PORTAL_PATH, 'utf8').replace(/^\uFEFF/, '')
const workerSource = readFileSync(QUEUE_WORKER_PATH, 'utf8').replace(/^\uFEFF/, '')
const policySource = readFileSync(POLICY_PATH, 'utf8').replace(/^\uFEFF/, '')

const checks = [
  {
    name: 'submit_does_not_auto_start_worker',
    ok: !source.includes('startGenerationQueueWorker(requestRecord.id)'),
    expected: 'POST /generation-request saves only',
  },
  {
    name: 'new_requests_wait_for_manual_start',
    ok: source.includes("status: 'manual_start_required'") && source.includes('等待手动开始'),
    expected: 'manual_start_required status and copy',
  },
  {
    name: 'submit_response_marks_not_auto_started',
    ok: source.includes('autoStarted: false'),
    expected: 'autoStarted false in submit response',
  },
  {
    name: 'start_endpoint_exists',
    ok: source.includes("generationActionMatch") &&
      source.includes("action === 'start'") &&
      source.includes('startGenerationRequest') &&
      source.includes('startGenerationQueueWorker(requestId)'),
    expected: 'explicit start endpoint triggers worker by request id',
  },
  {
    name: 'delete_endpoint_exists',
    ok: source.includes("(start|delete)") &&
      source.includes('deleteGenerationRequest') &&
      source.includes('preservedGeneratedFiles'),
    expected: 'delete endpoint removes task record without deleting generated files',
  },
  {
    name: 'start_delete_buttons_exist',
    ok: source.includes('generation-start-button') &&
      source.includes('generation-delete-button') &&
      source.includes('删除'),
    expected: 'task cards expose Start and Delete controls',
  },
  {
    name: 'manual_tasks_do_not_poll_forever',
    ok: source.includes('isPollableGenerationRequestForClient') &&
      source.includes("status.includes('queued_by_user')"),
    expected: 'polling is limited to explicitly started/running tasks',
  },
  {
    name: 'canceled_tasks_are_not_startable',
    ok: source.includes("status.includes('canceled')") &&
      source.includes("status.includes('cancelled')"),
    expected: 'canceled/interrupted tasks must be deleted and recreated, not restarted',
  },
  {
    name: 'controlled_generation_policy_module_exists',
    ok: policySource.includes('validateControlled16029GenerationRequest') &&
      policySource.includes('950w-400d-14-door-derived-review') &&
      policySource.includes('v43-width-derived-l642-r246') &&
      policySource.includes('isDownloadBlockedByControlled16029Gate'),
    expected: 'shared controlled-generation policy covers v43 width-derived 738-class models, whitelisted 950/400/14, and download blockers',
  },
  {
    name: 'submit_validates_controlled_generation_policy',
    ok: source.includes('validateControlled16029GenerationRequest(normalized)') &&
      source.includes('blocked_controlled_generation_policy') &&
      source.includes('controlledGenerationPolicy'),
    expected: 'portal submit validates whitelist before saving request',
  },
  {
    name: 'start_revalidates_controlled_generation_policy',
    ok: source.includes('validateControlled16029GenerationRequest(item)') &&
      source.includes('queued_by_user_for_background_generation') &&
      source.includes('controlledGenerationPolicy'),
    expected: 'manual Start revalidates policy before launching worker',
  },
  {
    name: 'download_blocks_box_regression_packages',
    ok: source.includes('isDownloadBlockedByControlled16029Gate(item)') &&
      source.includes('controlled generation gate blocked this download') &&
      source.includes('controlledGenerationBlockersForClient'),
    expected: 'generated ZIP download is blocked when body/door box or electrical/electric-lock regressions are detected',
  },
  {
    name: 'queue_worker_only_runs_user_started_tasks',
    ok: workerSource.includes("status.includes('queued_by_user')") &&
      workerSource.includes('validateControlled16029GenerationRequest(request)') &&
      workerSource.includes('controlledReleaseState'),
    expected: 'queue worker processes only user-started tasks and applies controlled policy internally',
  },
  {
    name: 'non_v43_full_assembly_has_derived_sheetmetal_review_state',
    ok: source.includes('isNonNativeFullAssemblyRequest') &&
      source.includes('isNonNativeFullAssemblyForClient') &&
      source.includes('isDerivedSheetMetalReviewModel') &&
      source.includes('下载派生钣金模型') &&
      source.includes('派生钣金复核模型') &&
      source.includes('下载非交付结构证据包') &&
      source.includes('非 v43 原生模板完整装配体'),
    expected: 'non-native full assemblies distinguish gold-gated derived sheet-metal review models from non-delivery evidence packages',
  },
]

const failed = checks.filter((item) => !item.ok)
const result = {
  schema: 'winnsen.locker16029.review_portal_manual_generation_control.v1',
  status: failed.length ? 'FAIL' : 'PASS',
  checks_total: checks.length,
  checks_failed: failed.length,
  checks,
}

process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
if (failed.length) process.exit(1)
