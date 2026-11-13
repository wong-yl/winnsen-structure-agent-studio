import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { INDOOR_16029_PROCESS_REVIEW_PROFILE, INDOOR_16029_PROCESS_REVIEW_BASE } from '../../../tools/lib/locker_16029_production_profile.mjs'

const folder = 'workers/native_model_requests/parametric_v1'
export const PROCESS_REVIEW_BASE_FILES = Object.freeze(['NativeParametricWidth.cs', 'NativeParametricDoor.cs', 'NativeParametricLockBase.cs',
  'ParametricContext.cs', 'NativeParametricAssembly.cs', 'NativeParametricVerification.cs', 'NativeParametricLocks.cs',
  'NativeParametricPhysicalAudit.cs', 'NativeParametricDoorPlacement.cs', 'NativeParametricEnvelope.cs'])
const normalize = source => source.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n')
function once(source, before, after) {
  if (source.split(before).length !== 2) throw new Error('process review source anchor changed: ' + before)
  return source.replace(before, after)
}

export function current16029ProcessReviewSources(root) {
  const read = name => normalize(readFileSync(resolve(root, folder, name), 'utf8'))
  const sources = new Map(PROCESS_REVIEW_BASE_FILES.map(name => [name, read('repairs/' + INDOOR_16029_PROCESS_REVIEW_BASE + '/source/' + name)]))
  let main = sources.get('NativeParametricAssembly.cs')
  main = once(main, '"locks-verify", "root", "repair-engineer-feedback",', '"production-probe", "production-apply", "production-verify", "locks-verify", "root", "repair-engineer-feedback",')
  main = once(main, 'else if (phase == "physical-audit") PhysicalAudit(sw, rows);', 'else if (phase == "production-probe") ProductionProbe(sw, rows);\n                    else if (phase == "production-apply") ApplyProductionProcess(sw, rows);\n                    else if (phase == "production-verify") VerifyProductionProcess(sw, rows);\n                    else if (phase == "physical-audit") PhysicalAudit(sw, rows);')
  main = once(main, 'else if (phase == "repair-engineer-feedback") RepairEngineerFeedback(sw, rows);', 'else if (phase == "repair-engineer-feedback") { RepairEngineerFeedback(sw, rows); LocalizeProductionDoorPack(sw, rows); }')
  main = once(main, 'ReadOnlyInspection = new[] { "verify-package", "verify-relocated", "physical-audit" }', 'ReadOnlyInspection = new[] { "production-probe", "production-verify", "verify-package", "verify-relocated", "physical-audit" }')
  const shelfSource = read('NativeParametricAssembly.cs')
  const shelfStart = shelfSource.indexOf('        internal static object ProductionShelfMirrorLink(')
  const shelfEnd = shelfSource.indexOf('        internal static void RepairMaintenanceStrip(', shelfStart)
  if (shelfStart < 0 || shelfEnd <= shelfStart) throw new Error('process shelf mirror proof source missing')
  main = once(main, '    internal static partial class NativeParametricWidth\n    {', '    internal static partial class NativeParametricWidth\n    {\n' + shelfSource.slice(shelfStart, shelfEnd))
  sources.set('NativeParametricAssembly.cs', main)
  sources.set('NativeParametricDoorPlacement.cs', once(sources.get('NativeParametricDoorPlacement.cs'),
    '            if(ParametricContext.DepthMm<450)RepairShallowShelfBraces(sw,records);',
    '            if(ParametricContext.DepthMm<450)RepairShallowShelfBraces(sw,records);\n            PersistProcessReviewSideOwners(sw,records);'))
  sources.set('NativeParametricProcessReviewPersistence.cs', read('NativeParametricProcessReviewPersistence.cs'))

  // Reuse the isolated package writer and its provenance checks; retain today's read-only session cleanup.
  let verification = '#define WINNSEN_PRODUCTION_PACKAGE\n' + read('NativeParametricVerification.cs')
  sources.set('NativeParametricVerification.cs', verification)
  let audit = read('NativeParametricPhysicalAudit.cs')
  audit = once(audit, '            var doorProofPaths=VerifiedDoorProofPaths().ToList();', '            CloseReadOnlyInspectionDocuments(sw, purpose + "-coverage-start");\n            var doorProofPaths=VerifiedDoorProofPaths().ToList();')
  audit = once(audit, '                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }\n            }\n            var partEdits', '                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); CloseReadOnlyInspectionDocuments(sw, purpose + "-coverage-" + Path.GetFileName(path)); }\n            }\n            var partEdits')
  audit = once(audit, '                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); }\n            }\n            foreach (var item in hashes)', '                } finally { sw.CloseDoc(model.GetTitle()); Marshal.ReleaseComObject(model); CloseReadOnlyInspectionDocuments(sw, "physical-flat-" + part.file); }\n            }\n            foreach (var item in hashes)')
  sources.set('NativeParametricPhysicalAudit.cs', audit.replaceAll('indoor-weld-locators-20261029-r1', INDOOR_16029_PROCESS_REVIEW_PROFILE))
  for (const name of ['NativeParametricProduction.cs', 'NativeParametricProductionGeometry.cs', 'NativeParametricProductionMarks.cs']) {
    let source = read(name).replaceAll('indoor-weld-locators-20261029-r1', INDOOR_16029_PROCESS_REVIEW_PROFILE)
    source = source.replace('校形前加工形状；实际校形、装配力及生产效果未验证', '装配不采用校形；上孔内侧预偏0.5mm；实际装配力及生产效果未验证')
    sources.set(name, source)
  }
  return sources
}
