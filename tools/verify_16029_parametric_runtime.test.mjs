import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { copyFileSync, existsSync, linkSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'
import { run16029ParametricRuntime } from './lib/locker_16029_parametric_runtime.mjs'
import { INDOOR_16029_PRODUCTION_PROFILE } from './lib/locker_16029_production_profile.mjs'
import { run16029ParametricPortalTask } from './run_16029_parametric_portal_worker.mjs'

const BASELINE = 'engineer-feedback-20261008-r4'
const REPAIR = 'readonly-open-20261105-r4'
const REARWIDTH_REPAIR = 'readonly-open-20261109-r4-rearwidth1'
const REARGEOMETRY_REPAIR = 'readonly-open-20261110-r4-reargeometry1'
const DOOR_PRECISION_REPAIR = 'readonly-open-20261110-r4-doorprecision1'
const DOOR_PRECISION_REPAIR2 = 'readonly-open-20261110-r4-doorprecision2'
const DOOR_PRECISION_REPAIR3 = 'readonly-open-20261110-r4-doorprecision3'
const DOOR_PRECISION_REPAIRS = [DOOR_PRECISION_REPAIR, DOOR_PRECISION_REPAIR2, DOOR_PRECISION_REPAIR3]
const REPAIR_PROFILES = [REPAIR, REARWIDTH_REPAIR, REARGEOMETRY_REPAIR, ...DOOR_PRECISION_REPAIRS]
const repairLabel = tag => tag === REPAIR ? '' : tag === REARWIDTH_REPAIR ? ' rear-width repair' : tag === REARGEOMETRY_REPAIR ? ' rear-geometry repair' : ' ' + tag
const RECORDED_ATTEMPT = resolve(dirname(fileURLToPath(import.meta.url)), '../data/parametric_attempts/PARAM-20261109111020502-DAEB5A')
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase()
const hash = path => sha(readFileSync(path))
const read = path => JSON.parse(readFileSync(path, 'utf8'))
const write = (path, value) => {
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, JSON.stringify(value, null, 2) + '\n')
}
const update = (path, change) => {
  const value = read(path)
  change(value)
  write(path, value)
}
const requestFor = (heightMm = 1917, depthMm = 550) => ({
  cabinet: { widthMm: 850, heightMm, depthMm },
  columns: ['L', 'R'].map(side => ({ side, doors: [{ heightUnits: 1 }, { heightUnits: 2 }] })),
})
const prefixFor = (request, doors) => [
  'clone', 'probe', 'dimensions', 'derived', 'base-hole', 'assemblies',
  ...(request.cabinet.heightMm !== 1917 || request.cabinet.depthMm !== 550 ? ['envelope'] : []),
  ...doors, 'repair-door-placements', 'repair-interfaces', 'repair-base-foot', 'locks', 'locks-verify',
  'repair-partition-mates', 'root', 'repair-frame-slots',
]
const suffixFor = request => [
  'clone', ...(request.cabinet.heightMm !== 1917 ? ['repair-height-braces'] : []),
  ...(request.cabinet.depthMm !== 550 ? ['repair-depth-placements'] : []),
  'repair-base-mates', 'repair-engineer-feedback', 'verify', 'package', 'verify-package',
  'verify-relocated', 'physical-audit',
]

function bindReceipts(result, evidenceByPhase = new Map()) {
  const planPath = resolve(result.attempt, 'parametric_execution.json'), plan = read(planPath)
  const heights = [...plan.contract.rowsByColumn.L, ...plan.contract.rowsByColumn.R].map(row => row.doorHeightMm)
  const planSha256 = hash(planPath), executableSha256 = hash(resolve(result.attempt, 'tools/NativeParametricAssembly.exe'))
  let predecessor = ''
  for (const phase of result.stages) {
    const authorizationPath = resolve(result.attempt, 'authorizations', phase + '.json')
    const evidencePath = resolve(result.attempt, 'evidence', phase + '.json')
    const receiptPath = resolve(result.attempt, 'receipts', phase + '.json')
    write(authorizationPath, { phase, taskId: plan.taskId, planSha256, executableSha256 })
    write(evidencePath, evidenceByPhase.get(phase) || (phase.startsWith('door-')
      ? { success: true, heightMm: heights.find(height => 'door-' + Number(height.toFixed(6)).toString().replace('.', 'p') === phase), result: { success: false } }
      : { phase, success: true }))
    write(receiptPath, { phase, taskId: plan.taskId, planSha256, authorizationSha256: hash(authorizationPath),
      evidenceSha256: hash(evidencePath), protectedSourcesUnchanged: true, predecessor })
    predecessor = hash(receiptPath)
  }
}

function rebindReceiptChain(result) {
  let predecessor = ''
  for (const phase of result.stages) {
    const receiptPath = resolve(result.attempt, 'receipts', phase + '.json')
    update(receiptPath, receipt => {
      receipt.evidenceSha256 = hash(resolve(result.attempt, 'evidence', phase + '.json'))
      receipt.predecessor = predecessor
    })
    predecessor = hash(receiptPath)
  }
}

function harness(t, request = requestFor(), repairTag = REPAIR) {
  const root = mkdtempSync(resolve(tmpdir(), '16029-runtime-test-'))
  t.after(() => {
    assert.equal(dirname(root), resolve(tmpdir()))
    assert.ok(basename(root).startsWith('16029-runtime-test-'))
    rmSync(root, { recursive: true, force: true })
  })
  const builds = new Map()
  for (const tag of [BASELINE, ...REPAIR_PROFILES]) {
    const folder = resolve(root, 'workers/native_model_requests/parametric_v1', 'bin-' + tag)
    mkdirSync(folder, { recursive: true })
    const executable = resolve(folder, 'NativeParametricAssembly.exe')
    writeFileSync(executable, 'fixture executable only: ' + tag + '\n')
    const manifest = { executable, executableSha256: hash(executable) }
    write(resolve(folder, 'build.json'), manifest)
    builds.set(tag, { tag, folder, executable, manifest })
  }
  let fixtureCount = 0
  const h = { root, request, builds, calls: [], events: [], repair: { buildTag: repairTag, repairProfile: repairTag },
    cad: { verified: true }, readRepairCalls: 0, verifyBuildCalls: [], inspectCadCalls: 0 }
  h.fixture = (input, options, { attempt = resolve(root, 'data/parametric_attempts', 'PARAM-fixture-' + ++fixtureCount), evidenceByPhase } = {}) => {
    mkdirSync(dirname(attempt), { recursive: true })
    mkdirSync(attempt)
    const taskId = basename(attempt), build = builds.get(options.buildTag)
    const stages = options.phases || suffixFor(input)
    const resumedEvidence = options.resumeSource ? { sourceAttempt: options.resumeSource, completedPhase: options.resumeStage,
      receiptSha256: hash(resolve(options.resumeSource, 'receipts/repair-frame-slots.json')) } : null
    const contract = build16029ParametricContract(input)
    const doorModules = DOOR_PRECISION_REPAIRS.includes(options.buildTag)
      ? [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => row.doorHeightMm))]
        .map(heightMm => ({ token: Number(heightMm.toFixed(6)).toString().replace('.', 'p'), heightMm })) : undefined
    write(resolve(attempt, 'parametric_execution.json'), { taskId, contract, resumedEvidence, ...(doorModules ? { doorModules } : {}) })
    mkdirSync(resolve(attempt, 'tools'))
    copyFileSync(build.executable, resolve(attempt, 'tools/NativeParametricAssembly.exe'))
    copyFileSync(resolve(build.folder, 'build.json'), resolve(attempt, 'tools/build.json'))
    const result = { attempt, taskId, stages: [...stages], state: 'candidate_requires_validation', modelReady: false }
    bindReceipts(result, evidenceByPhase)
    return result
  }
  h.respond = (input, options) => h.fixture(input, options)
  h.dependencies = {
    root,
    readRepair: options => { h.readRepairCalls++; h.events.push('readRepair'); assert.equal(options.root, root); return h.repair },
    verifyBuild: (tag, options) => {
      h.verifyBuildCalls.push({ tag, options }); h.events.push('verifyBuild:' + (tag || 'default'))
      return builds.get(tag || BASELINE)
    },
    inspectCad: () => { h.inspectCadCalls++; h.events.push('inspectCad'); return h.cad },
    runModel: async (input, options) => {
      h.calls.push({ request: input, options }); h.events.push('runModel:' + h.calls.length)
      return h.respond(input, options, h.calls.length)
    },
  }
  h.run = (options = {}) => run16029ParametricRuntime(request, options, h.dependencies)
  return h
}

test('runtime bridge pins two fresh segments and uses requested height/depth stages', async t => {
  for (const repairTag of REPAIR_PROFILES) for (const [height, depth] of [[1917, 550], [2000, 550], [1917, 600], [2000, 600]]) {
    await t.test(height + 'H/' + depth + 'D' + repairLabel(repairTag), async t => {
      const h = harness(t, requestFor(height, depth), repairTag), controller = new AbortController()
      const progress = () => {}, beforeResume = async () => { h.events.push('beforeResume'); await Promise.resolve() }
      const result = await h.run({ signal: controller.signal, onProgress: progress, beforeResume, phaseTimeoutMs: 123456 })
      assert.equal(h.calls.length, 2)
      const [first, second] = h.calls
      const doors = height === 1917 ? ['door-603', 'door-1213'] : ['door-630p666667', 'door-1268p333333']
      assert.deepEqual(first.options.phases, prefixFor(h.request, doors))
      assert.equal(first.options.buildTag, DOOR_PRECISION_REPAIRS.includes(repairTag) ? repairTag : BASELINE)
      assert.equal(first.options.repairProfile, DOOR_PRECISION_REPAIRS.includes(repairTag) ? repairTag : '')
      assert.equal(second.options.buildTag, repairTag)
      assert.equal(second.options.repairProfile, repairTag)
      assert.equal(second.options.resumeSource, resolve(h.root, 'data/parametric_attempts/PARAM-fixture-1'))
      assert.equal(second.options.resumeStage, 'repair-frame-slots')
      assert.equal(second.options.phases, undefined)
      assert.equal(second.options.resumeBackupPhase, undefined)
      assert.deepEqual(result.stages, suffixFor(h.request))
      assert.equal(result.state, 'candidate_requires_validation')
      assert.equal(result.modelReady, false)
      assert.notEqual(result.attempt, second.options.resumeSource)
      for (const call of h.calls) {
        assert.equal(call.request, h.request)
        assert.equal(call.options.signal, controller.signal)
        assert.equal(call.options.onProgress, progress)
        assert.equal(call.options.phaseTimeoutMs, 123456)
      }
      assert.deepEqual(h.verifyBuildCalls, [{ tag: undefined, options: { root: h.root } },
        { tag: repairTag, options: { root: h.root, repairProfile: repairTag } }])
      assert.deepEqual(h.events, ['readRepair', 'verifyBuild:default', 'verifyBuild:' + repairTag,
        'runModel:1', 'beforeResume', 'inspectCad', 'runModel:2'])
    })
  }
})

test('dynamic door phases use all distinct measured heights in column order', async t => {
  const request = requestFor()
  request.columns = [
    { side: 'L', doors: [130, 316, 316, 316, 717].map(heightMm => ({ heightMm })) },
    { side: 'R', doors: [603, 603, 603].map(heightMm => ({ heightMm })) },
  ]
  const h = harness(t, request)
  await h.run()
  assert.deepEqual(h.calls[0].options.phases, prefixFor(request, ['door-130', 'door-316', 'door-717', 'door-603']))
  for (const phase of ['door-130', 'door-316', 'door-717', 'door-603']) {
    const evidence = read(resolve(h.calls[1].options.resumeSource, 'evidence', phase + '.json'))
    assert.deepEqual(Object.keys(evidence).sort(), ['heightMm', 'result', 'success'])
    assert.equal(evidence.success, true)
    assert.equal(evidence.result.success, false)
  }
})

for (const repairTag of DOOR_PRECISION_REPAIRS) test('door-precision prefix uses the candidate executable and preserves complete equal-division heights: ' + repairTag, async t => {
  const request = { cabinet: { widthMm: 900, heightMm: 2000, depthMm: 500 },
    columns: [{ side: 'L', doors: Array.from({ length: 4 }, () => ({ heightUnits: 1 })) },
      { side: 'R', doors: Array.from({ length: 9 }, () => ({ heightUnits: 1 })) }] }
  const h = harness(t, request, repairTag)
  await h.run()
  const [first, second] = h.calls
  assert.equal(first.options.buildTag, repairTag)
  assert.equal(first.options.repairProfile, repairTag)
  assert.deepEqual(first.options.phases.filter(phase => phase.startsWith('door-')), ['door-471p25', 'door-205p555556'])
  const evidence = read(resolve(second.options.resumeSource, 'evidence/door-205p555556.json'))
  assert.equal(evidence.heightMm, 1913 / 9 - 7)
  assert.notEqual(evidence.heightMm, 205.555556)
  assert.equal(hash(resolve(second.options.resumeSource, 'tools/NativeParametricAssembly.exe')),
    h.builds.get(repairTag).manifest.executableSha256)
})

test('door-precision accepts historical explicit heights without redistributing them', async t => {
  const request = { cabinet: { widthMm: 900, heightMm: 2000, depthMm: 500 },
    columns: [{ side: 'L', doors: Array.from({ length: 4 }, () => ({ heightMm: 471.25 })) },
      { side: 'R', doors: [...Array(8).fill(205.555), 205.56].map(heightMm => ({ heightMm })) }] }
  const before = JSON.stringify(request), h = harness(t, request, DOOR_PRECISION_REPAIR)
  await h.run()
  assert.equal(JSON.stringify(request), before)
  assert.deepEqual(h.calls[0].options.phases.filter(phase => phase.startsWith('door-')),
    ['door-471p25', 'door-205p555', 'door-205p56'])
  for (const height of [205.555, 205.56]) {
    const phase = 'door-' + String(height).replace('.', 'p')
    assert.equal(read(resolve(h.calls[1].options.resumeSource, 'evidence', phase + '.json')).heightMm, height)
  }
})

test('distinct exact heights sharing one six-decimal identifier stop before generation', async t => {
  for (const repairTag of REPAIR_PROFILES) await t.test(repairTag, async t => {
    const request = requestFor()
    request.columns = [{ side: 'L', doors: [603.0000001, 1212.9999999].map(heightMm => ({ heightMm })) },
      { side: 'R', doors: [603.0000002, 1212.9999998].map(heightMm => ({ heightMm })) }]
    const h = harness(t, request, repairTag)
    await assert.rejects(h.run(), /Distinct contract door heights map to the same door module identifier/)
    assert.equal(h.calls.length, 0)
    assert.equal(h.inspectCadCalls, 0)
  })
})

for (const repairTag of DOOR_PRECISION_REPAIRS) test('door-precision rejects rounded door evidence even when its identifier and receipt chain match: ' + repairTag, async t => {
  const h = harness(t, requestFor(2000), repairTag)
  h.respond = (input, options, index) => {
    const result = h.fixture(input, options)
    if (index === 1) {
      const phase = result.stages.find(phase => phase.startsWith('door-'))
      update(resolve(result.attempt, 'evidence', phase + '.json'), evidence => { evidence.heightMm = Number(evidence.heightMm.toFixed(6)) })
      rebindReceiptChain(result)
    }
    return result
  }
  await assert.rejects(h.run(), /runtime bridge receipt binding failed: door-/)
  assert.equal(h.calls.length, 1)
  assert.equal(h.inspectCadCalls, 0)
})

for (const repairTag of DOOR_PRECISION_REPAIRS) test('door-precision rejects the observed CLR decimal conversion drift with a valid identifier and rebound receipts: ' + repairTag, async t => {
  const request = { cabinet: { widthMm: 900, heightMm: 2000, depthMm: 500 },
    columns: [{ side: 'L', doors: Array.from({ length: 4 }, () => ({ heightUnits: 1 })) },
      { side: 'R', doors: Array.from({ length: 9 }, () => ({ heightUnits: 1 })) }] }
  const h = harness(t, request, repairTag)
  h.respond = (input, options, index) => {
    const result = h.fixture(input, options)
    if (index === 1) {
      update(resolve(result.attempt, 'evidence/door-205p555556.json'), evidence => {
        assert.equal(evidence.heightMm, 205.55555555555554)
        evidence.heightMm = 205.55555555555551
      })
      rebindReceiptChain(result)
    }
    return result
  }
  await assert.rejects(h.run(), /runtime bridge receipt binding failed: door-205p555556/)
  assert.equal(h.calls.length, 1)
  assert.equal(h.inspectCadCalls, 0)
})

test('door-precision keeps the JS midpoint identifier and the full explicitly supplied height', async t => {
  const request = requestFor()
  request.columns = ['L', 'R'].map(side => ({ side, doors: [100.0000005, 1715.9999995].map(heightMm => ({ heightMm })) }))
  const h = harness(t, request, DOOR_PRECISION_REPAIR)
  await h.run()
  assert.deepEqual(h.calls[0].options.phases.filter(phase => phase.startsWith('door-')), ['door-100', 'door-1716'])
  const plan = read(resolve(h.calls[1].options.resumeSource, 'parametric_execution.json'))
  assert.deepEqual(plan.doorModules, [{ token: '100', heightMm: 100.0000005 }, { token: '1716', heightMm: 1715.9999995 }])
  assert.equal(read(resolve(h.calls[1].options.resumeSource, 'evidence/door-100.json')).heightMm, 100.0000005)
})

for (const repairTag of DOOR_PRECISION_REPAIRS) test('door-precision binds both segment module maps to the exact requested contract: ' + repairTag, async t => {
  const cases = [
    ['missing map', plan => { delete plan.doorModules }],
    ['rounded height', plan => { plan.doorModules[0].heightMm = Number(plan.doorModules[0].heightMm.toFixed(6)) }],
    ['changed identifier', plan => { plan.doorModules[0].token = '630p666666' }],
    ['duplicate module', plan => { plan.doorModules.push({ ...plan.doorModules[0] }) }],
    ['missing module', plan => { plan.doorModules.pop() }],
    ['reordered modules', plan => { plan.doorModules.reverse() }],
  ]
  for (const segment of [1, 2]) for (const [label, mutate] of cases) await t.test('segment ' + segment + ': ' + label, async t => {
    const h = harness(t, requestFor(2000), repairTag)
    h.respond = (input, options, index) => {
      const result = h.fixture(input, options)
      if (index === segment) update(resolve(result.attempt, 'parametric_execution.json'), mutate)
      return result
    }
    await assert.rejects(h.run(), /runtime bridge door module map differs from the request contract/)
    assert.equal(h.calls.length, segment)
  })
})

test('native door evidence rejects missing, wrong or nonnumeric heights and unsuccessful outer results', async t => {
  const edit = mutate => (path, phase) => update(path, evidence => mutate(evidence, phase))
  const cases = [
    ['missing height', edit(evidence => { delete evidence.heightMm }), [false, true]],
    ['wrong height', edit(evidence => { evidence.heightMm++ }), [false, true]],
    ['missing height with synthetic phase', edit((evidence, phase) => { delete evidence.heightMm; evidence.phase = phase })],
    ['wrong height with synthetic phase', edit((evidence, phase) => { evidence.heightMm++; evidence.phase = phase })],
    ['numeric string height', edit(evidence => { evidence.heightMm = String(evidence.heightMm) })],
    ['null height', edit(evidence => { evidence.heightMm = null })],
    ['boolean height', edit(evidence => { evidence.heightMm = true })],
    ['object height', edit(evidence => { evidence.heightMm = { value: evidence.heightMm } })],
    ['non-finite numeric height', path => writeFileSync(path, '{"success":true,"heightMm":1e400,"result":{"success":false}}\n')],
    ['failed outer result with successful nested result', edit(evidence => { evidence.success = false; evidence.result.success = true })],
    ['missing outer success', edit(evidence => { delete evidence.success; evidence.result.success = true })],
    ['truthy nonboolean outer success', edit(evidence => { evidence.success = 'true' })],
  ]
  for (const [label, mutate, rebindings = [true]] of cases) for (const rebound of rebindings) {
    await t.test(label + (rebound ? ' with rebound receipt chain' : ' without rebound receipt'), async t => {
      const h = harness(t); let doorPhase, boundaryCalls = 0
      h.respond = (input, options, index) => {
        const result = h.fixture(input, options)
        if (index === 1) {
          doorPhase = result.stages.find(phase => phase.startsWith('door-'))
          mutate(resolve(result.attempt, 'evidence', doorPhase + '.json'), doorPhase)
          if (rebound) rebindReceiptChain(result)
        }
        return result
      }
      await assert.rejects(h.run({ beforeResume: () => { boundaryCalls++ } }),
        error => error.message === 'runtime bridge receipt binding failed: ' + doorPhase)
      assert.equal(h.calls.length, 1)
      assert.equal(h.inspectCadCalls, 0)
      assert.equal(boundaryCalls, 0)
    })
  }
})

test('recorded native 16-stage prefix evidence replays in a newly created temporary attempt', {
  skip: !existsSync(resolve(RECORDED_ATTEMPT, 'parametric_execution.json')) && 'recorded native prefix is unavailable on this machine',
}, async t => {
  const planPath = resolve(RECORDED_ATTEMPT, 'parametric_execution.json'), plan = read(planPath)
  assert.equal(plan.taskId, 'PARAM-20261109111020502-DAEB5A')
  const phases = prefixFor(plan.contract.input, ['door-471p25'])
  assert.equal(phases.length, 16)
  const evidenceByPhase = new Map(phases.map(phase => [phase, read(resolve(RECORDED_ATTEMPT, 'evidence', phase + '.json'))]))
  const recordedPaths = [planPath, ...phases.map(phase => resolve(RECORDED_ATTEMPT, 'evidence', phase + '.json'))]
  const recordedHashes = new Map(recordedPaths.map(path => [path, hash(path)]))
  t.after(() => { for (const [path, digest] of recordedHashes) assert.equal(hash(path), digest, 'recorded source must remain unchanged: ' + path) })
  const nativeDoor = evidenceByPhase.get('door-471p25')
  assert.deepEqual(Object.keys(nativeDoor).sort(), ['heightMm', 'result', 'success'])
  assert.equal(nativeDoor.heightMm, 471.25)
  assert.equal(nativeDoor.success, true)
  assert.equal(nativeDoor.result.success, false)
  const h = harness(t, plan.contract.input)
  h.respond = (input, options, index) => h.fixture(input, options, { evidenceByPhase: index === 1 ? evidenceByPhase : undefined })
  const result = await h.run()
  assert.equal(h.calls.length, 2)
  assert.deepEqual(h.calls[0].options.phases, phases)
  const freshSource = h.calls[1].options.resumeSource
  assert.notEqual(freshSource, RECORDED_ATTEMPT)
  assert.equal(dirname(freshSource), resolve(h.root, 'data/parametric_attempts'))
  for (const [phase, evidence] of evidenceByPhase) assert.deepEqual(read(resolve(freshSource, 'evidence', phase + '.json')), evidence)
  assert.deepEqual(result.stages, suffixFor(plan.contract.input))
  assert.equal(result.state, 'candidate_requires_validation')
  assert.equal(result.modelReady, false)
})

test('first-segment failure propagates without checking CAD or attempting resume', async t => {
  const h = harness(t), failure = new Error('prefix lock failure')
  h.respond = () => { throw failure }
  await assert.rejects(h.run({ beforeResume: () => assert.fail('resume hook must not run') }), error => error === failure)
  assert.equal(h.calls.length, 1)
  assert.equal(h.inspectCadCalls, 0)
})

test('atomic-stage cancellation returns either segment immediately without validating its incomplete fixtures', async t => {
  for (const repairTag of REPAIR_PROFILES) for (const segment of [1, 2]) await t.test('segment ' + segment + repairLabel(repairTag), async t => {
    const h = harness(t, requestFor(), repairTag), cancelled = { state: 'cancelled_after_atomic_stage', attempt: 'unfinished', stages: [] }
    h.respond = (input, options, index) => index === segment ? cancelled : h.fixture(input, options)
    assert.equal(await h.run(), cancelled)
    assert.equal(h.calls.length, segment)
    assert.equal(h.inspectCadCalls, segment - 1)
  })
})

test('abort after prefix prevents the resume call', async t => {
  const h = harness(t), controller = new AbortController()
  h.respond = (input, options) => { const result = h.fixture(input, options); controller.abort(); return result }
  await assert.rejects(h.run({ signal: controller.signal }), /cancel/i)
  assert.equal(h.calls.length, 1)
  assert.equal(h.inspectCadCalls, 0)
})

test('boundary cancellation and lease failure propagate without a second model call', async t => {
  for (const message of ['Portal task cancelled before checkpoint resume', 'portal worker lease changed']) {
    await t.test(message, async t => {
      const h = harness(t), failure = new Error(message)
      await assert.rejects(h.run({ beforeResume: async () => { await Promise.resolve(); throw failure } }), error => error === failure)
      assert.equal(h.calls.length, 1)
      assert.equal(h.inspectCadCalls, 0)
    })
  }
})

test('CAD must be verified idle after the asynchronous resume hook', async t => {
  const h = harness(t)
  await assert.rejects(h.run({ beforeResume: async () => {
    await Promise.resolve()
    h.cad = { verified: false, pids: [12345] }
  } }), /CAD processes|unverified/)
  assert.equal(h.calls.length, 1)
  assert.equal(h.inspectCadCalls, 1)
})

test('missing CAD verification and abort during CAD inspection each stop resume', async t => {
  await t.test('missing verification', async t => {
    const h = harness(t); h.cad = {}
    await assert.rejects(h.run(), /CAD processes|unverified/)
    assert.equal(h.calls.length, 1)
  })
  await t.test('abort during inspection', async t => {
    const h = harness(t), controller = new AbortController()
    h.dependencies.inspectCad = () => { controller.abort(); return { verified: true } }
    await assert.rejects(h.run({ signal: controller.signal }), /cancel/i)
    assert.equal(h.calls.length, 1)
  })
})

test('second-segment lock conflict is propagated once without retry', async t => {
  const h = harness(t), failure = Object.assign(new Error('native worker instance is already active'), { code: 'LOCK_CONFLICT' })
  h.respond = (input, options, index) => { if (index === 2) throw failure; return h.fixture(input, options) }
  await assert.rejects(h.run(), error => error === failure)
  assert.equal(h.calls.length, 2)
  assert.equal(h.calls[1].options.buildTag, REPAIR)
  assert.equal(h.calls[1].options.resumeStage, 'repair-frame-slots')
})

test('manual, production and envelope requests bypass automatic runtime repair selection', async t => {
  const cases = [
    ['build tag', { buildTag: BASELINE }], ['repair profile', { repairProfile: REPAIR }],
    ['selected phases', { phases: ['clone'] }], ['empty phases', { phases: [] }],
    ['resume source', { resumeSource: 'manual-checkpoint' }], ['resume stage', { resumeStage: 'repair-frame-slots' }],
    ['resume backup', { resumeBackupPhase: 'repair-height-braces' }],
    ['production profile', {}, { productionProfile: INDOOR_16029_PRODUCTION_PROFILE }],
    ['envelope probe', {}, { envelopeProbe: true }],
  ]
  for (const repairTag of REPAIR_PROFILES) for (const [label, options, extra = {}] of cases) await t.test(label + repairLabel(repairTag), async t => {
    const h = harness(t, { ...requestFor(), ...extra }, repairTag), sentinel = { state: 'manual-fixture' }
    h.respond = () => sentinel
    h.dependencies.readRepair = () => assert.fail('explicit requests must not read automatic selection')
    assert.equal(await h.run(options), sentinel)
    assert.equal(h.calls.length, 1)
    assert.equal(h.calls[0].request, h.request)
    assert.equal(h.calls[0].options, options)
    assert.equal(h.verifyBuildCalls.length, 0)
    assert.equal(h.inspectCadCalls, 0)
  })
})

test('absent selection uses one unmodified model call, while selection-read failure propagates', async t => {
  await t.test('no selection', async t => {
    const h = harness(t), options = { phases: null }, sentinel = { state: 'ordinary-fixture' }
    h.repair = {}; h.respond = () => sentinel
    assert.equal(await h.run(options), sentinel)
    assert.equal(h.calls.length, 1)
    assert.equal(h.calls[0].options, options)
    assert.equal(h.readRepairCalls, 1)
    assert.equal(h.verifyBuildCalls.length, 0)
  })
  await t.test('selection read failure', async t => {
    const h = harness(t), failure = new Error('runtime repair acceptance evidence changed')
    h.dependencies.readRepair = () => { throw failure }
    await assert.rejects(h.run(), error => error === failure)
    assert.equal(h.calls.length, 0)
  })
})

test('unsupported repair selection and changed default baseline stop before model generation', async t => {
  await t.test('unsupported repair', async t => {
    const h = harness(t); h.repair = { buildTag: 'readonly-open-20261105-r5', repairProfile: 'readonly-open-20261105-r5' }
    await assert.rejects(h.run(), /selected repair profile/)
    assert.equal(h.calls.length, 0)
  })
  await t.test('changed default baseline', async t => {
    const h = harness(t); h.builds.get(BASELINE).tag = 'different-default'
    await assert.rejects(h.run(), /baseline selection/)
    assert.equal(h.calls.length, 0)
  })
  for (const [label, buildTag, repairProfile] of [
    ['unknown repair', 'readonly-open-20261109-r4-rearwidth2', 'readonly-open-20261109-r4-rearwidth2'],
    ['r4 build with rear-width profile', REPAIR, REARWIDTH_REPAIR],
    ['rear-width build with r4 profile', REARWIDTH_REPAIR, REPAIR],
    ['rear-width build with missing profile', REARWIDTH_REPAIR, undefined],
    ['rear-width build with r5 profile', REARWIDTH_REPAIR, 'readonly-open-20261105-r5'],
    ['rear-geometry build with r4 profile', REARGEOMETRY_REPAIR, REPAIR],
    ['rear-geometry build with rear-width profile', REARGEOMETRY_REPAIR, REARWIDTH_REPAIR],
    ['rear-geometry build with missing profile', REARGEOMETRY_REPAIR, undefined],
    ['r4 build with rear-geometry profile', REPAIR, REARGEOMETRY_REPAIR],
    ['rear-width build with rear-geometry profile', REARWIDTH_REPAIR, REARGEOMETRY_REPAIR],
  ]) await t.test(label, async t => {
    const h = harness(t); h.repair = { buildTag, repairProfile }
    await assert.rejects(h.run(), /selected repair profile/)
    assert.equal(h.calls.length, 0)
    assert.equal(h.verifyBuildCalls.length, 0)
    assert.equal(h.inspectCadCalls, 0)
  })
})

test('results must have the exact ordered stages, task identity and request contract in each segment', async t => {
  const cases = [
    ['wrong state', result => { result.state = 'ready' }],
    ['extra stage', result => { result.stages.push('repair-height-braces') }],
    ['reordered stages', result => { [result.stages[0], result.stages[1]] = [result.stages[1], result.stages[0]] }],
    ['wrong result task', result => { result.taskId = 'PARAM-foreign' }],
    ['wrong plan task', result => update(resolve(result.attempt, 'parametric_execution.json'), plan => { plan.taskId = 'PARAM-foreign' })],
    ['changed request', result => update(resolve(result.attempt, 'parametric_execution.json'), plan => { plan.contract.input.cabinet.widthMm++ })],
    ['changed derived contract', result => update(resolve(result.attempt, 'parametric_execution.json'), plan => { plan.contract.geometry.doorLeafWidthMm++ })],
  ]
  for (const repairTag of REPAIR_PROFILES) for (const segment of [1, 2]) for (const [label, mutate] of cases) await t.test('segment ' + segment + ': ' + label + repairLabel(repairTag), async t => {
    const h = harness(t, requestFor(), repairTag)
    h.respond = (input, options, index) => { const result = h.fixture(input, options); if (index === segment) mutate(result); return result }
    await assert.rejects(h.run(), /runtime bridge/)
    assert.equal(h.calls.length, segment)
  })
})

test('first segment cannot use an ancestor, a historical attempt, or a directory outside the attempts root', async t => {
  await t.test('ancestor', async t => {
    const h = harness(t)
    h.respond = (input, options) => {
      const result = h.fixture(input, options)
      update(resolve(result.attempt, 'parametric_execution.json'), plan => { plan.resumedEvidence = { sourceAttempt: 'history' } })
      return result
    }
    await assert.rejects(h.run(), /first segment cannot have an ancestor/)
    assert.equal(h.calls.length, 1)
  })
  await t.test('historical attempt', async t => {
    const h = harness(t)
    const historical = h.fixture(h.request, { buildTag: BASELINE, phases: prefixFor(h.request, ['door-603', 'door-1213']) })
    h.respond = () => historical
    await assert.rejects(h.run(), /newly created isolated attempt/)
    assert.equal(h.calls.length, 1)
  })
  await t.test('outside attempts root', async t => {
    const h = harness(t)
    h.respond = (input, options) => h.fixture(input, options, { attempt: resolve(h.root, 'elsewhere/PARAM-escaped') })
    await assert.rejects(h.run(), /newly created isolated attempt/)
    assert.equal(h.calls.length, 1)
  })
})

test('second segment must use this request checkpoint without a backup or snapshot override', async t => {
  const cases = [
    ['wrong source', resumed => { resumed.sourceAttempt += '-foreign' }],
    ['wrong completed phase', resumed => { resumed.completedPhase = 'root' }],
    ['wrong checkpoint receipt', resumed => { resumed.receiptSha256 = 'A'.repeat(64) }],
    ['backup override', resumed => { resumed.backupPhase = 'repair-height-braces' }],
    ['snapshot override', resumed => { resumed.snapshotSource = 'foreign-snapshot' }],
  ]
  for (const repairTag of REPAIR_PROFILES) for (const [label, mutate] of cases) await t.test(label + repairLabel(repairTag), async t => {
    const h = harness(t, requestFor(), repairTag)
    h.respond = (input, options, index) => {
      const result = h.fixture(input, options)
      if (index === 2) update(resolve(result.attempt, 'parametric_execution.json'), plan => mutate(plan.resumedEvidence))
      return result
    }
    await assert.rejects(h.run(), /checkpoint differs/)
    assert.equal(h.calls.length, 2)
  })
  await t.test('cannot reuse first segment directory', async t => {
    const h = harness(t); let first
    h.respond = (input, options, index) => index === 1 ? (first = h.fixture(input, options)) : { ...first, stages: suffixFor(input) }
    await assert.rejects(h.run(), /newly created isolated attempt/)
    assert.equal(h.calls.length, 2)
  })
})

test('both segments require pinned program bytes, exact receipt coverage and a bound receipt chain', async t => {
  const receiptPath = result => resolve(result.attempt, 'receipts', result.stages[1] + '.json')
  const evidencePath = result => resolve(result.attempt, 'evidence', result.stages[1] + '.json')
  const authorizationPath = result => resolve(result.attempt, 'authorizations', result.stages[1] + '.json')
  const cases = [
    ['changed executable', result => writeFileSync(resolve(result.attempt, 'tools/NativeParametricAssembly.exe'), 'different bytes')],
    ['changed manifest', result => update(resolve(result.attempt, 'tools/build.json'), manifest => { manifest.extra = true })],
    ['hardlinked executable', result => linkSync(resolve(result.attempt, 'tools/NativeParametricAssembly.exe'), resolve(result.attempt, 'linked.exe'))],
    ['missing receipt', result => rmSync(receiptPath(result))],
    ['extra receipt', result => write(resolve(result.attempt, 'receipts/foreign-stage.json'), {})],
    ['broken predecessor', result => update(receiptPath(result), receipt => { receipt.predecessor = '' })],
    ['unprotected source', result => update(receiptPath(result), receipt => { receipt.protectedSourcesUnchanged = false })],
    ['changed plan binding', result => update(receiptPath(result), receipt => { receipt.planSha256 = 'A'.repeat(64) })],
    ['failed evidence with rebound digest', result => {
      update(evidencePath(result), evidence => { evidence.success = false })
      update(receiptPath(result), receipt => { receipt.evidenceSha256 = hash(evidencePath(result)) })
    }],
    ['wrong authorized program with rebound digest', result => {
      update(authorizationPath(result), authorization => { authorization.executableSha256 = 'A'.repeat(64) })
      update(receiptPath(result), receipt => { receipt.authorizationSha256 = hash(authorizationPath(result)) })
    }],
  ]
  for (const repairTag of REPAIR_PROFILES) for (const segment of [1, 2]) for (const [label, mutate] of cases) await t.test('segment ' + segment + ': ' + label + repairLabel(repairTag), async t => {
    const h = harness(t, requestFor(), repairTag)
    h.respond = (input, options, index) => { const result = h.fixture(input, options); if (index === segment) mutate(result); return result }
    await assert.rejects(h.run(), /runtime bridge/)
    assert.equal(h.calls.length, segment)
  })
})

async function portalTask(h, id) {
  const dataDir = resolve(h.root, 'portal_tasks'), store = createNativeTaskStore({ dataDir })
  await store.createOrImport({ id, username: 'runtime-test', createdAt: new Date().toISOString(), status: 'native_task_created',
    taskType: 'native_solidworks_build_task', requestFingerprint: id, parametricRequest: h.request,
    nativeBuild: { state: 'created', attempt: 0, modelReady: false } })
  return { dataDir, store }
}

test('portal still rejects a two-segment candidate when publication validation fails', async t => {
  const h = harness(t), id = 'PARAMETRIC-RUNTIME-REJECTED', { dataDir, store } = await portalTask(h, id)
  let validationCalls = 0
  const result = await run16029ParametricPortalTask(id, { dataDir, onProgress: () => {},
    runModel: (request, options) => run16029ParametricRuntime(request, options, h.dependencies),
    validate: attempt => { validationCalls++; assert.equal(attempt, resolve(h.root, 'data/parametric_attempts/PARAM-fixture-2'))
      return { ok: false, reason: 'fixture physical acceptance failed' } },
  })
  assert.equal(result.state, 'failed', result.error)
  assert.equal(result.modelReady, false)
  assert.equal(h.calls.length, 2)
  assert.equal(validationCalls, 1)
  const task = await store.read(id)
  assert.equal(task.nativeBuild.result, undefined)
  assert.equal(task.nativeBuild.statusHistory.some(entry => entry.to === 'ready'), false)
})

test('actual portal beforeResume hook stops a cancelled task before any continuation or validation', async t => {
  const h = harness(t), id = 'PARAMETRIC-RUNTIME-CANCELLED', { dataDir, store } = await portalTask(h, id)
  let continuationCalls = 0, validationCalls = 0, hookError
  const result = await run16029ParametricPortalTask(id, { dataDir, onProgress: () => {},
    runModel: async (request, options) => {
      const task = await store.read(id)
      await store.cancel({ taskId: id, expectedRevision: task.revision, transitionId: id + '-user-cancel', reason: 'fixture boundary cancellation' })
      try { await options.beforeResume() } catch (error) { hookError = error; throw error }
      continuationCalls++
      return { state: 'candidate_requires_validation', attempt: 'must-not-be-returned' }
    },
    validate: () => { validationCalls++; return { ok: true } },
  })
  assert.match(hookError?.message || '', /cancel/i)
  assert.equal(result.state, 'cancelled', result.error)
  assert.equal(result.modelReady, false)
  assert.equal(continuationCalls, 0)
  assert.equal(validationCalls, 0)
  const task = await store.read(id)
  assert.equal(task.nativeBuild.state, 'cancelled')
  assert.equal(task.nativeBuild.result, undefined)
  assert.equal(task.nativeBuild.statusHistory.some(entry => entry.to === 'ready'), false)
})

test('actual portal beforeResume hook refuses changed or removed leases without validation', async t => {
  for (const mode of ['changed', 'removed']) await t.test(mode, async t => {
    const h = harness(t), id = 'PARAMETRIC-RUNTIME-LEASE-' + mode.toUpperCase(), { dataDir, store } = await portalTask(h, id)
    let continuationCalls = 0, validationCalls = 0, hookError
    const result = await run16029ParametricPortalTask(id, { dataDir, onProgress: () => {},
      runModel: async (request, options) => {
        const task = await store.read(id)
        if (mode === 'changed') {
          update(resolve(dataDir, id + '.json'), row => { row.nativeBuild.lease.id = 'foreign-fixture-lease' })
        } else {
          await store.fail({ taskId: id, expectedRevision: task.revision, workerId: task.nativeBuild.lease.workerId,
            leaseId: task.nativeBuild.lease.id, transitionId: id + '-lease-ended', code: 'FIXTURE_LEASE_ENDED',
            message: 'fixture lease removed before continuation', retryable: false })
        }
        try { await options.beforeResume() } catch (error) { hookError = error; throw error }
        continuationCalls++
        return { state: 'candidate_requires_validation', attempt: 'must-not-be-returned' }
      },
      validate: () => { validationCalls++; return { ok: true } },
    })
    assert.match(hookError?.message || '', /lease changed/)
    assert.notEqual(result.state, 'ready')
    assert.equal(result.modelReady, false)
    assert.equal(continuationCalls, 0)
    assert.equal(validationCalls, 0)
    const task = await store.read(id)
    assert.equal(task.nativeBuild.result, undefined)
    assert.equal(task.nativeBuild.statusHistory.some(entry => entry.to === 'ready'), false)
    if (mode === 'changed') assert.equal(task.nativeBuild.lease.id, 'foreign-fixture-lease')
    else assert.equal(task.nativeBuild.lease, null)
  })
})
