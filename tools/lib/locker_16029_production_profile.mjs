import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

export const INDOOR_16029_PRODUCTION_PROFILE = 'indoor-weld-locators-20261029-r1'
export const INDOOR_16029_PRODUCTION_PROFILE_LABEL = '2026-10-29，焊接定位/左右缺口/门销预偏'
export const INDOOR_16029_PROCESS_REVIEW_PROFILE = 'indoor-process-review-20261111-r1'
export const INDOOR_16029_PROCESS_REVIEW_LABEL = '室内柜·加工工艺版（工程审阅）'
export const INDOOR_16029_PROCESS_REVIEW_BUILD = 'process-review-20261111-r8'
export const INDOOR_16029_PROCESS_REVIEW_BASE = 'readonly-open-20261111-r4-rightowner1'

export function is16029ProcessReview(request) {
  return request?.productionProfile === INDOOR_16029_PROCESS_REVIEW_PROFILE
}

export function read16029ProcessReviewSelection(root) {
  try {
    const selection = JSON.parse(readFileSync(resolve(root, 'workers/native_model_requests/parametric_v1/process-review-selection.json'), 'utf8'))
    if (selection.schema === 'winnsen.locker16029.process_review_selection.v1' && selection.verified === true &&
        selection.productionProfile === INDOOR_16029_PROCESS_REVIEW_PROFILE && selection.buildTag === INDOOR_16029_PROCESS_REVIEW_BUILD) {
      return { enabled: true, productionProfile: selection.productionProfile, productionProfileLabel: INDOOR_16029_PROCESS_REVIEW_LABEL, buildTag: selection.buildTag }
    }
  } catch {}
  return { enabled: false, productionProfile: '', productionProfileLabel: '' }
}

export function read16029ProductionSelection(root) {
  try {
    const selection = JSON.parse(readFileSync(resolve(root, 'workers/native_model_requests/parametric_v1/build-selection.json'), 'utf8'))
    if (selection.verified === true && selection.productionProfile === INDOOR_16029_PRODUCTION_PROFILE) {
      return { enabled: true, productionProfile: INDOOR_16029_PRODUCTION_PROFILE, productionProfileLabel: INDOOR_16029_PRODUCTION_PROFILE_LABEL }
    }
  } catch {}
  return { enabled: false, productionProfile: '', productionProfileLabel: '' }
}
