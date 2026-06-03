import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const PORTAL_PATH = resolve(ROOT, 'tools/serve_16029_review_downloads.mjs')
const source = readFileSync(PORTAL_PATH, 'utf8').replace(/^\uFEFF/, '')

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
