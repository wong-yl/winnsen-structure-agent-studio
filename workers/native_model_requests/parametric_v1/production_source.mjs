import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { adaptWidthSource, WIDTH_SOURCE_PATH } from './width_adapter.mjs'
import { adaptDoorSource, DOOR_SOURCE_PATH } from './door_adapter.mjs'
import { adaptLockSource, LOCK_SOURCE_PATH } from './lock_adapter.mjs'

export const PRODUCTION_PROCESS_VERSION = 'indoor-weld-locators-20261029-r1'
const folder = 'workers/native_model_requests/parametric_v1'
const nativeNames = ['ParametricContext.cs', 'NativeParametricAssembly.cs', 'NativeParametricVerification.cs', 'NativeParametricLocks.cs', 'NativeParametricPhysicalAudit.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricEnvelope.cs', 'NativeParametricProduction.cs', 'NativeParametricProductionGeometry.cs', 'NativeParametricProductionMarks.cs', 'NativeParametricOrderInterfaces.cs']
function once(text, before, after) {
  if (text.split(before).length !== 2) throw new Error('production source anchor changed: ' + before)
  return text.replace(before, after)
}
export function current16029ProductionSources(root) {
  const sources = new Map([
    ['NativeParametricWidth.cs', adaptWidthSource(readFileSync(resolve(root, WIDTH_SOURCE_PATH), 'utf8'))],
    ['NativeParametricDoor.cs', adaptDoorSource(readFileSync(resolve(root, DOOR_SOURCE_PATH), 'utf8'), 'A07174DEDF4F9F0958A731E679655B742E53BEDA063709F5F2F10BF2376572B0')],
    ['NativeParametricLockBase.cs', adaptLockSource(readFileSync(resolve(root, LOCK_SOURCE_PATH), 'utf8'))],
    ...nativeNames.map(name => [name, readFileSync(resolve(root, folder, name), 'utf8')]),
  ])
  let main = sources.get('NativeParametricAssembly.cs').replaceAll('\r\n', '\n')
  main = once(main, '"locks-verify", "root", "repair-engineer-feedback",', '"production-probe", "production-apply", "production-verify", "locks-verify", "root", "repair-engineer-feedback",')
  main = once(main, 'else if (phase == "physical-audit") PhysicalAudit(sw, rows);', 'else if (phase == "production-probe") ProductionProbe(sw, rows);\n                    else if (phase == "production-apply") ApplyProductionProcess(sw, rows);\n                    else if (phase == "production-verify") VerifyProductionProcess(sw, rows);\n                    else if (phase == "physical-audit") PhysicalAudit(sw, rows);')
  main = once(main, 'else if (phase == "repair-engineer-feedback") RepairEngineerFeedback(sw, rows);', 'else if (phase == "repair-engineer-feedback") { RepairEngineerFeedback(sw, rows); LocalizeProductionDoorPack(sw, rows); RepairProductionBaseFlatPattern(sw, rows); RepairProductionBottomPlateFlatPattern(sw, rows); RepairProductionOrderInterfaces(sw, rows); }')
  sources.set('NativeParametricAssembly.cs', main)
  sources.set('NativeParametricVerification.cs', '#define WINNSEN_PRODUCTION_PACKAGE\n' + sources.get('NativeParametricVerification.cs'))
  return sources
}
