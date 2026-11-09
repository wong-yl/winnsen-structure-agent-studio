import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

export const INDOOR_16029_PRODUCTION_PROFILE = 'indoor-weld-locators-20261029-r1'
export const INDOOR_16029_PRODUCTION_PROFILE_LABEL = '2026-10-29，焊接定位/左右缺口/门销预偏'

export function read16029ProductionSelection(root) {
  try {
    const selection = JSON.parse(readFileSync(resolve(root, 'workers/native_model_requests/parametric_v1/build-selection.json'), 'utf8'))
    if (selection.verified === true && selection.productionProfile === INDOOR_16029_PRODUCTION_PROFILE) {
      return { enabled: true, productionProfile: INDOOR_16029_PRODUCTION_PROFILE, productionProfileLabel: INDOOR_16029_PRODUCTION_PROFILE_LABEL }
    }
  } catch {}
  return { enabled: false, productionProfile: '', productionProfileLabel: '' }
}
