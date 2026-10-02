import { createHash } from 'node:crypto'
import { mkdirSync } from 'node:fs'
import { resolve } from 'node:path'
import { createNativeTaskStore } from '../../../../../tools/lib/locker_16029_native_task_store.mjs'
import { runNativeWorkerOnce } from '../../../../../tools/run_16029_native_worker.mjs'

const DEVELOPMENT_ROOT = resolve('workers/native_model_requests/development/v1')
const DATA_DIR = resolve(DEVELOPMENT_ROOT, 'runtime_data')
const TASK_ROOT = resolve(DATA_DIR, 'native_model_requests')
const TASK_ID = 'NATIVE-88814-DEV-20260812'
const CREATED_AT = '2026-08-12T09:45:00.000Z'

function sha256(value) {
  return createHash('sha256').update(value, 'utf8').digest('hex').toUpperCase()
}

const task = {
  id: TASK_ID,
  schema: 'winnsen.native_16029_request.v1',
  createdAt: CREATED_AT,
  updatedAt: CREATED_AT,
  username: 'native-development',
  requestFingerprint: sha256('16029|888|1917|550|2|14|7,7|L1111111-R1111111|381|mechanical'),
  customerRequirementReference: '888宽14门原生结构辅助模型开发验证',
  purpose: 'structure_engineering_assistance',
  customerParameterFlow: 'v1',
  inputMode: 'cabinet_outer_width',
  widthInputMode: 'cabinet_outer_width',
  candidateRecipeId: '',
  cabinetWidth: 888,
  cabinetHeight: 1917,
  cabinetDepth: 550,
  columns: 2,
  doorCount: 14,
  columnDoorCounts: [7, 7],
  rowSequence: 'L1111111-R1111111',
  doorWidth: 381,
  doorType: 'plain_native_sheet_metal',
  lockType: 'mechanical_lock_tongue',
  hingeType: 'native_hinge_pin',
  latchType: 'mechanical',
  reinforcement: 'native_sheet_metal',
  openings: 'one_door_one_lock_opening',
  material: '',
  thickness: '',
  previewDimensionsValidated: false,
  storageMode: 'task_file_source',
  status: 'native_task_created',
  taskType: 'native_solidworks_build_task',
  deliveryMode: 'native_task_pending',
  modelReady: false,
  engineeringAssistanceReady: false,
  legacyFallbackUsed: false,
  nativeBuild: {
    schema: 'winnsen.native_solidworks_build_task.v1',
    state: 'created',
    attempt: 0,
    createdAt: CREATED_AT,
    executionStarted: false,
    modelReady: false,
    legacyFallbackUsed: false,
    statusHistory: [{ state: 'created', at: CREATED_AT }],
  },
}

mkdirSync(DATA_DIR, { recursive: true })
const store = createNativeTaskStore({ dataDir: TASK_ROOT })
const persisted = await store.createOrImport(task)
const workerResult = await runNativeWorkerOnce({
  dataDir: DATA_DIR,
  workerId: 'native-development-planner',
})
const current = await store.read(TASK_ID)

process.stdout.write(`${JSON.stringify({
  taskId: TASK_ID,
  dataDir: DATA_DIR,
  taskRoot: TASK_ROOT,
  persistedStatus: persisted.status,
  workerStatus: workerResult.status,
  workerCode: workerResult.code || '',
  planPath: workerResult.planPath || resolve(
    TASK_ROOT,
    'attempts',
    TASK_ID,
    'attempt-0001',
    'native_build_plan.json',
  ),
  planFileSha256: current.nativeBuild?.planArtifact?.fileSha256 || '',
  recipeId: current.nativeBuild?.planArtifact?.recipeId || '',
  currentStatus: current.status,
}, null, 2)}\n`)
