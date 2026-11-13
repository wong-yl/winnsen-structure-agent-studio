import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, mkdirSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'
import { bound16029ProductionDoorHeight, production16029Profile } from './lib/locker_16029_production_evidence.mjs'
import { normalize16029NativeModelRequest } from './locker_16029_native_generator.mjs'
import { verify16029ParametricBuild } from './lib/locker_16029_parametric_build.mjs'
import { current16029ProcessReviewSources } from '../workers/native_model_requests/parametric_v1/process_review_source.mjs'
import { INDOOR_16029_PROCESS_REVIEW_PROFILE as PROFILE, INDOOR_16029_PROCESS_REVIEW_BUILD as BUILD,
  INDOOR_16029_PROCESS_REVIEW_BASE as BASE, read16029ProcessReviewSelection } from './lib/locker_16029_production_profile.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const request = { cabinet: { widthMm: 850, heightMm: 1917, depthMm: 485 },
  columns: [{ side: 'L', doors: Array.from({ length: 7 }, () => ({ heightUnits: 1 })) }, { side: 'R', doors: [{ heightUnits: 1 }, { heightUnits: 1 }] }], productionProfile: PROFILE }

test('process profile preserves full equal-division door heights and rejects altered module maps', () => {
  const contract = build16029ParametricContract(request)
  const heights = [...new Set([...contract.rowsByColumn.L, ...contract.rowsByColumn.R].map(row => row.doorHeightMm))]
  const plan = { contract, doorModules: heights.map(heightMm => ({ token: Number(heightMm.toFixed(6)).toString().replace('.', 'p'), heightMm })) }
  assert.equal(bound16029ProductionDoorHeight(plan, '254p428571'), 1830 / 7 - 7)
  assert.notEqual(bound16029ProductionDoorHeight(plan, '254p428571'), 254.428571)
  assert.equal(bound16029ProductionDoorHeight(plan, '908'), 908)
  for (const change of [p => { p.doorModules[0].heightMm = 254.428571 }, p => { delete p.doorModules }, p => { p.doorModules.push(p.doorModules[0]) }, p => { p.doorModules[0].token = '254p429' }]) {
    const bad = structuredClone(plan); change(bad)
    assert.throws(() => bound16029ProductionDoorHeight(bad, '254p428571'))
  }
  assert.throws(() => bound16029ProductionDoorHeight(plan, '254p429'))
})

test('new process request uses an independent recognized identity and integer cabinet inputs', () => {
  assert.equal(production16029Profile(request), PROFILE)
  assert.throws(() => production16029Profile({ productionProfile: 'unrecognized-process' }))
  const payload = { customerRequirementReference: '加工工艺版精度验收', customerParameterFlow: 'v1', parametricRequest: request, productionProfile: PROFILE }
  const result = normalize16029NativeModelRequest(payload)
  assert.equal(result.ok, true, result.message)
  assert.deepEqual(result.normalizedRequest.parametricRequest.columns, request.columns)
  assert.equal(result.normalizedRequest.productionProfile, PROFILE)
  assert.equal(normalize16029NativeModelRequest({ ...payload, productionProfile: 'indoor-weld-locators-20261029-r1' }).reason, 'unsupported_production_profile')
  for (const key of ['widthMm', 'heightMm', 'depthMm']) {
    const fractional = structuredClone(request); fractional.cabinet[key] += .5
    const rejected = normalize16029NativeModelRequest({ ...payload, parametricRequest: fractional })
    assert.equal(rejected.ok, false)
    assert.match(rejected.message, /整数 mm/)
  }
})

test('global legacy selection cannot enable the independent process review entry', t => {
  const folder = mkdtempSync(resolve(tmpdir(), '16029-process-profile-'))
  t.after(() => rmSync(folder, { recursive: true, force: true }))
  const profiles = resolve(folder, 'workers/native_model_requests/parametric_v1'); mkdirSync(profiles, { recursive: true })
  writeFileSync(resolve(profiles, 'build-selection.json'), JSON.stringify({ verified: true, productionProfile: 'indoor-weld-locators-20261029-r1' }))
  assert.equal(read16029ProcessReviewSelection(folder).enabled, false)
  const selection = { schema: 'winnsen.locker16029.process_review_selection.v1', verified: true, productionProfile: PROFILE, buildTag: BUILD }
  writeFileSync(resolve(profiles, 'process-review-selection.json'), JSON.stringify(selection))
  assert.equal(read16029ProcessReviewSelection(folder).enabled, true)
  writeFileSync(resolve(profiles, 'process-review-selection.json'), JSON.stringify({ ...selection, buildTag: BASE }))
  assert.equal(read16029ProcessReviewSelection(folder).enabled, false)
})

test('compiled process build binds today accepted base and preserves protected generation sources', () => {
  const build = verify16029ParametricBuild(BUILD)
  assert.equal(build.processReviewSource.baseBuildTag, BASE)
  assert.equal(build.processReviewSource.baseExecutableSha256, 'F8A2FE41AB74C0794C7728767D8640AD0D845689C18866103423D48F83BCC783')
  assert.equal(build.manifest.productionProfile, PROFILE)
  assert.equal(build.processReviewSource.productionRelease, false)
  const sources = current16029ProcessReviewSources(root)
  const normalized = value => value.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
  for (const name of ['NativeParametricWidth.cs', 'NativeParametricDoor.cs', 'NativeParametricLockBase.cs', 'ParametricContext.cs', 'NativeParametricLocks.cs', 'NativeParametricEnvelope.cs']) {
    assert.equal(sources.get(name), normalized(readFileSync(resolve(root, 'workers/native_model_requests/parametric_v1/repairs', BASE, 'source', name), 'utf8')), name)
  }
  const placementBase = normalized(readFileSync(resolve(root, 'workers/native_model_requests/parametric_v1/repairs', BASE, 'source/NativeParametricDoorPlacement.cs'), 'utf8'))
  assert.equal(sources.get('NativeParametricDoorPlacement.cs').replace('\n            PersistProcessReviewSideOwners(sw,records);', ''), placementBase)
  assert.match(sources.get('NativeParametricAssembly.cs'), /RepairEngineerFeedback\(sw, rows\); LocalizeProductionDoorPack\(sw, rows\);/)
  assert.doesNotMatch(sources.get('NativeParametricAssembly.cs'), /RepairProduction(?:BaseFlatPattern|BottomPlateFlatPattern|OrderInterfaces)\(sw, rows\)/)
  assert.doesNotMatch(sources.get('NativeParametricProductionGeometry.cs'), /校形前/)
  assert.match(sources.get('NativeParametricProductionGeometry.cs'), /装配不采用校形/)
  assert.match(sources.get('NativeParametricProductionMarks.cs'), /PMSideStandOff = .0002/)
  assert.match(sources.get('NativeParametricProductionMarks.cs'), /PMPartitionStandOff = .0005/)
})
