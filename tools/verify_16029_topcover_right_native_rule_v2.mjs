import { createHash } from 'node:crypto'
import { existsSync, readFileSync, statSync } from 'node:fs'
import { isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = resolve(fileURLToPath(new URL('..', import.meta.url)))
const rulePath = resolve(process.argv[2] ?? '')
const rule = JSON.parse(readFileSync(rulePath, 'utf8'))
const checks = []
const add = (name, pass, actual = undefined) => checks.push({
  name, pass:Boolean(pass), ...(actual === undefined ? {} : { actual }),
})
const hash = (value) => createHash('sha256').update(value).digest('hex').toUpperCase()
const fileHash = (path) => hash(readFileSync(path))
const near = (left, right, tolerance = 1e-6) =>
  Number.isFinite(left) && Number.isFinite(right) && Math.abs(left - right) <= tolerance
const exactKeys = (value, expected) => value && typeof value === 'object' &&
  !Array.isArray(value) && JSON.stringify(Object.keys(value).sort()) ===
    JSON.stringify([...expected].sort())

add('rule_is_regular_single_link_repository_file', existsSync(rulePath) &&
  statSync(rulePath).isFile() && Number(statSync(rulePath).nlink) === 1 &&
  !relative(repoRoot, rulePath).startsWith('..') &&
  !isAbsolute(relative(repoRoot, rulePath)))
add('schema_purpose_state_boundary',
  rule.schema === 'winnsen.16029.topcover_right_native_rule.v2' &&
  rule.purpose === 'structure_engineering_assistance' &&
  rule.partName === '上盖壳体右侧板.SLDPRT' &&
  rule.right760NativePilotContractReady === true &&
  rule.right888NativeBuildReady === false && rule.nativeCadCreated === false &&
  rule.unresolved?.length === 1 && /no right-side native CAD/.test(rule.qualityBoundary))

const expectedInputHashes = {
  ruleV1:'4F2BCA054CE4E91C38443D6604D2A7E23E7230D64769CBC903612980EB5E6012',
  physical:'68CF3723B540762C4991556A3571DB7B1127D1A7AE4B46A89CF38721BB0E58B5',
  dxf:'A018A121BF0CB31DB05CD3AE5D9FDE775FF151590F228EFC260A9DF327D6C319',
  readiness:'F5D12C9FF2EA07EEF6D852984407DB2DD4ED929198BFF6EF47D6FE1E4A4F7A5C',
  referenceV5:'BAEEF8AB4790F4650785605FC3C4A452753755558A931B9275977A1E88C32A98',
}
for (const [name, expected] of Object.entries(expectedInputHashes)) {
  const row = rule.inputs?.[name]
  const path = resolve(repoRoot, row?.path ?? '')
  add(`live_input_hash:${name}`, existsSync(path) && row.sha256 === expected &&
    fileHash(path) === expected)
}

add('width_relation_exact', rule.widthRelation?.localGeometryInvariant === true &&
  exactKeys(rule.widthRelation.xShiftFrom760Mm,
    ['width760','width888','width1000']) &&
  rule.widthRelation.xShiftFrom760Mm.width760 === 0 &&
  rule.widthRelation.xShiftFrom760Mm.width888 === 64 &&
  rule.widthRelation.xShiftFrom760Mm.width1000 === 120 &&
  rule.widthRelation.foldedAndFlatTranslationSignaturesProven === true)
add('dxf_to_model_transform_exact',
  rule.dxfToWidth760Model?.xFormula === 'modelX = dxfX - 539.8561797753' &&
  rule.dxfToWidth760Model?.yMm === 1917 &&
  rule.dxfToWidth760Model?.zFormula === 'modelZ = 320.3146067416 - dxfY')
add('sheet_metal_and_deduction_exact',
  near(rule.sheetMetal?.thicknessMm, 0.8) &&
  near(rule.sheetMetal?.defaultBendRadiusMm, 0.2) &&
  near(rule.sheetMetal?.kFactorReadback, 0.5) &&
  rule.sheetMetal?.customAllowanceType === 4 &&
  near(rule.sheetMetal?.bendDeductionMm, 1.4))

const flat = rule.manufacturingFlat760
const outer = flat?.baseSheet?.outerLoop
const knockouts = flat?.baseSheet?.knockoutCutLoops ?? []
const holes = flat?.baseSheet?.throughHoles ?? []
const bosses = flat?.pemBosses ?? []
add('outer_loop_22_and_closed', outer?.role === 'outer_profile' &&
  outer.points760ModelMm?.length === 22 && outer.sourceEntityIndexes?.length === 22 &&
  near(outer.areaMm2, 88915.94, 0.000001))
add('four_knockout_loops_exact', knockouts.length === 4 &&
  knockouts.every((loop) => loop.role === 'knockout_cut_loop' &&
    loop.points760ModelMm.length === 12 && loop.sourceEntityIndexes.length === 12 &&
    near(loop.areaMm2, 196, 0.000001)))
const vertexKey = (point) => point.map((value) => value.toFixed(6)).join(',')
add('all_line_loops_are_degree_two_closed_cycles', [outer, ...knockouts].every((loop) => {
  const degree = new Map()
  const points = loop.points760ModelMm
  for (let index = 0; index < points.length; index += 1) {
    const left = vertexKey(points[index])
    const right = vertexKey(points[(index + 1) % points.length])
    degree.set(left, (degree.get(left) ?? 0) + 1)
    degree.set(right, (degree.get(right) ?? 0) + 1)
  }
  return degree.size === points.length && [...degree.values()].every((value) => value === 2)
}))

const radiusCounts = new Map()
for (const hole of holes) {
  const key = hole.radiusMm.toFixed(2)
  radiusCounts.set(key, (radiusCounts.get(key) ?? 0) + 1)
}
add('eleven_true_holes_and_radius_multiset', holes.length === 11 &&
  JSON.stringify(Object.fromEntries([...radiusCounts].sort())) ===
    JSON.stringify({'1.00':2,'2.75':4,'3.25':1,'4.40':3,'5.00':1}) &&
  holes.every((hole) => hole.center760ModelMm?.length === 3 &&
    near(hole.center760ModelMm[1], 1917) && near(hole.diameterMm, hole.radiusMm * 2)))
add('three_pem_bosses_exact', bosses.length === 3 && bosses.every((boss) =>
  near(boss.sheetHoleRadiusMm, 4.4) && near(boss.outerRadiusMm, 5.5) &&
  near(boss.innerRadiusMm, 3) && near(boss.heightMm, 4) &&
  near(boss.fromYmm, 1916.2) && near(boss.toYmm, 1912.2) &&
  boss.mergeResult === true && holes.some((hole) =>
    near(hole.center760ModelMm[0], boss.center760ModelMm[0]) &&
    near(hole.center760ModelMm[2], boss.center760ModelMm[2]) &&
    near(hole.radiusMm, 4.4))))

const breakdown = flat.volumeBreakdownMm3
add('flat_volume_breakdown_recomputes',
  near(breakdown.baseSheetVolume,
    (outer.areaMm2 - knockouts.reduce((sum, loop) => sum + loop.areaMm2, 0) -
      holes.reduce((sum, hole) => sum + Math.PI * hole.radiusMm ** 2, 0)) * 0.8,
  0.000001) &&
  near(breakdown.pemVolume, bosses.reduce((sum, boss) => sum + Math.PI *
    (boss.outerRadiusMm ** 2 - boss.innerRadiusMm ** 2) * boss.heightMm, 0),
  0.000001) && near(breakdown.calculatedFlatVolume,
    breakdown.baseSheetVolume + breakdown.pemVolume, 0.000001) &&
  near(breakdown.calculatedFlatVolume, 70990.25576414, 0.0001))
add('flat_expected_brep_exact',
  flat.expected?.bboxMm?.every((value, index) => near(value,
    [299.8,1912.2,-549,480.6,1917,-47][index], 0.00001)) &&
  near(flat.expected.volumeMm3, 70990.25576414, 0.0001) &&
  near(flat.expected.massKg, 0.5572735077485, 1e-9) &&
  near(flat.expected.surfaceAreaMm2, 178225.56498146, 0.0001) &&
  JSON.stringify(flat.expected.topology) === JSON.stringify({faceCount:95,edgeCount:244,
    loopCount:148,loopEdgeReferenceCount:488,cylinderFaceCount:17,
    circleEdgeReferenceCount:68}) &&
  flat.expected.translationNormalizedDetailedGeometrySignature ===
    'D4D4F1E70FC1D94A16BCA7B41AB7CD0E353CA9039E1F826CA0AC640040CBD5FB')

const bends = rule.bends760 ?? []
add('five_bends_ordered_and_line_bound', bends.length === 5 &&
  JSON.stringify(bends.map((bend) => bend.order)) === JSON.stringify([1,2,3,4,5]) &&
  JSON.stringify(bends.map((bend) => bend.lineName)) ===
    JSON.stringify(['直线7','直线8','直线9','直线6','直线10']) &&
  bends.every((bend) => near(bend.angleRadians, Math.PI / 2, 1e-12) &&
    near(bend.radiusMm, 0.5) && bend.useDefaultRadius === false &&
    bend.bendPosition === 0 && bend.customAllowanceType === 4 &&
    near(bend.bendDeductionMm, 1.4) && near(bend.kFactorReadback, 0.5) &&
    bend.line760ModelMm.start.length === 3 && bend.line760ModelMm.end.length === 3))
add('bend_direction_contract_exact', bends[0]?.direction === 2 && bends[0]?.down === true &&
  bends[1]?.direction === 2 && bends[1]?.down === true &&
  bends[2]?.direction === 2 && bends[2]?.down === true &&
  bends[3]?.direction === 2 && bends[3]?.down === true &&
  bends[4]?.direction === 1 && bends[4]?.down === false)
add('folded_expected_brep_exact',
  rule.expectedFolded760?.bboxMm?.every((value, index) => near(value,
    [320.8,1839.2,-549,380,1917,-47][index], 0.00001)) &&
  near(rule.expectedFolded760.volumeMm3, 71396.31748296, 0.0001) &&
  near(rule.expectedFolded760.foldedMinusManufacturingFlatVolumeMm3,
    406.06171882, 0.0001) &&
  JSON.stringify(rule.expectedFolded760.topology) === JSON.stringify({faceCount:135,
    edgeCount:324,loopCount:188,loopEdgeReferenceCount:648,cylinderFaceCount:27,
    circleEdgeReferenceCount:108}) &&
  rule.expectedFolded760.translationNormalizedDetailedGeometrySignature ===
    '05461DA22A685F5995093CB2A8CC5D9C87E118284C5F7234F71EDFE2714FE2FB')
add('native_sequence_has_no_import_or_legacy_route',
  JSON.stringify(rule.nativeBuildSequence) === JSON.stringify([
    'native_base_flange_from_outer_loop',
    'single_through_all_cut_for_four_knockout_loops_and_eleven_holes',
    'three_native_merged_annular_pem_boss_extrusions',
    'five_native_sketched_bends_in_order_1_to_5',
    'single_save_then_fresh_read_only_full_brep_regression',
  ]) && !/(FreeCAD|STEP|FCStd|PackAndGo|BreakAll|Import)/i.test(
    JSON.stringify(rule.nativeBuildSequence)))

const passed = checks.filter((value) => value.pass).length
const report = {
  schema:'winnsen.16029.topcover_right_native_rule_v2_verification.v1',
  status:passed === checks.length ? 'PASS' : 'FAIL',
  checkCount:checks.length,
  passed,
  failed:checks.length-passed,
  ruleSha256:fileHash(rulePath),
  cadStarted:false,
  checks,
}
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
if (report.status !== 'PASS') process.exit(1)
