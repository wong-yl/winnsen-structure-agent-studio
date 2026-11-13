import assert from 'node:assert/strict'
import test from 'node:test'
import { filterCompleted16029ResumePhases, run16029ParametricModel } from './run_16029_parametric_model.mjs'

test('resume skips receipt-bound completed stages but repeats clone for the isolated copy', () => {
  const planned = ['clone', 'probe', 'door-130', 'repair-door-placements', 'root', 'repair-frame-slots', 'verify']
  const completed = new Set(['clone', 'probe', 'door-130', 'repair-door-placements', 'root'])

  assert.deepEqual(filterCompleted16029ResumePhases(planned, completed, true), ['clone', 'repair-frame-slots', 'verify'])
})

test('ordinary and explicitly selected phase runs are unchanged', () => {
  const planned = ['clone', 'repair-door-placements', 'repair-frame-slots']
  const completed = new Set(['repair-door-placements'])

  assert.equal(filterCompleted16029ResumePhases(planned, completed, false), planned)
})

test('distinct door heights sharing a module identifier are rejected before build selection or CAD', async () => {
  const request = { cabinet: { widthMm:900, heightMm:2000, depthMm:500 },
    columns:['L','R'].map(side => ({ side, doors:[100.0000001,100.0000002,1691.9999997].map(heightMm => ({ heightMm })) })) }
  await assert.rejects(run16029ParametricModel(request), /Distinct contract door heights map to the same door module identifier/)
})
