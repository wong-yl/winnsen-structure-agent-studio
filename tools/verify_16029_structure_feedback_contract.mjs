import assert from 'node:assert/strict'
import {
  analyzeStructureFeedback,
  GOLD_SOURCE_STRUCTURE_CONTRACT,
} from './locker_16029_structure_feedback.mjs'

function component(name, depth = 0) {
  return {
    depth,
    name,
    path: `D:\\synthetic\\${name}.SLDASM`,
    is_hidden: false,
    is_suppressed: false,
  }
}

function partComponent(name, depth = 0) {
  return {
    ...component(name, depth),
    path: `D:\\synthetic\\${name}.SLDPRT`,
  }
}

function boxedPartComponent(name, box, depth = 0) {
  return {
    ...partComponent(name, depth),
    box,
  }
}

function baselineComponents(extra = []) {
  return [
    ...GOLD_SOURCE_STRUCTURE_CONTRACT.cabinetBodyModules.map((name) => component(`${name}-1`)),
    component('储物柜门3╱12装配_左-1'),
    component('锁舌-1'),
    component('锁舌-2'),
    component('锁舌-3'),
    component('锁舌-4'),
    component('锁舌-5'),
    component('锁舌-6'),
    component('底座焊接-1'),
    component('\u9501\u63a7\u7ef4\u62a4\u6761\u94a3\u91d1-1'),
    component('\u9876\u90e8\u5e26\u9501\u76d6\u677f\u94a3\u91d1-1'),
    ...extra,
  ]
}

const nonTemplatePlan = {
  requested: { cabinetWidthMm: 1100, doorCount: 6 },
  derived: { compatibleWithNativeTemplate: false, doorCount: 6 },
  columns: [
    {
      rows: [
        { topYmm: 482.5 },
        { topYmm: 940 },
        { topYmm: 1397.5 },
        { topYmm: 1855 },
      ],
    },
  ],
}

const nonTemplateFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 20,
    components: baselineComponents(),
  },
})

const issueIds = new Set(nonTemplateFeedback.issues.map((item) => item.id))
assert.equal(nonTemplateFeedback.status, 'issues_found')
assert.equal(nonTemplateFeedback.derived.p0Count, 5)
assert.ok(issueIds.has('non_template_cabinet_width_not_parameterized'))
assert.ok(issueIds.has('missing_lock_mounting_hole_datum'))
assert.ok(issueIds.has('missing_shelf_front_frame_locating_interface'))
assert.ok(issueIds.has('missing_inner_vertical_partition_stiffeners'))
assert.ok(issueIds.has('missing_leveling_feet'))

const placeholderOnlyFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 21,
    components: baselineComponents([
      component('parametric_cabinet_envelope_placeholder'),
      component('lock_control_strip_left'),
      component('leveling_foot_left_front'),
    ]),
  },
})

const placeholderIssueIds = new Set(placeholderOnlyFeedback.issues.map((item) => item.id))
assert.equal(placeholderIssueIds.has('non_template_cabinet_width_not_parameterized'), true)
assert.equal(placeholderIssueIds.has('parametric_scaffold_requires_engineering_validation'), true)
assert.equal(placeholderIssueIds.has('missing_lock_mounting_hole_datum'), true)
assert.equal(placeholderIssueIds.has('missing_shelf_front_frame_locating_interface'), true)
assert.equal(placeholderIssueIds.has('missing_inner_vertical_partition_stiffeners'), true)
assert.equal(placeholderIssueIds.has('missing_leveling_feet'), false)

const parametricScaffoldFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 27,
    components: baselineComponents([
      component('cabinet_left_side_weldment_parametric'),
      component('cabinet_right_side_weldment_parametric'),
      component('cabinet_left_partition_weldment_parametric'),
      component('cabinet_left_shelf_weldment_parametric'),
      component('front_frame_weldment_parametric'),
      component('base_weldment_parametric'),
      component('top_cover_weldment_parametric'),
      component('lock_mounting_hole_datum_left'),
      component('lock_mounting_hole_datum_left_row02'),
      component('lock_mounting_hole_datum_left_row03'),
      component('lock_mounting_hole_datum_right_row01'),
      component('lock_mounting_hole_datum_right_row02'),
      component('lock_mounting_hole_datum_right_row03'),
      component('shelf_locating_foot_left_row01'),
      component('front_frame_locating_notch_left_row01'),
      component('shelf_locating_foot_left_row02'),
      component('front_frame_locating_notch_left_row02'),
      component('shelf_locating_foot_left_row03'),
      component('front_frame_locating_notch_left_row03'),
      component('partition_stiffener_left_front'),
      component('partition_stiffener_left_rear'),
      component('partition_stiffener_right_front'),
      component('partition_stiffener_right_rear'),
      component('leveling_foot_left_front'),
    ]),
  },
})

const parametricIssueIds = new Set(parametricScaffoldFeedback.issues.map((item) => item.id))
assert.equal(parametricIssueIds.has('non_template_cabinet_width_not_parameterized'), false)
assert.equal(parametricIssueIds.has('parametric_scaffold_requires_engineering_validation'), true)
assert.equal(parametricIssueIds.has('missing_lock_mounting_hole_datum'), false)
assert.equal(parametricIssueIds.has('lock_mounting_hole_datum_count_below_door_rows'), false)
assert.equal(parametricIssueIds.has('missing_shelf_front_frame_locating_interface'), false)
assert.equal(parametricIssueIds.has('missing_inner_vertical_partition_stiffeners'), false)
assert.equal(parametricIssueIds.has('missing_leveling_feet'), false)
assert.equal(parametricScaffoldFeedback.status, 'issues_found')
assert.equal(parametricScaffoldFeedback.derived.p1Count, 1)

const sourceSheetMetalSplitFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  moduleTargets: {
    targets: [
      { key: 'front_frame_weldment', role: '\u95e8\u6846\u710a\u63a5' },
    ],
  },
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 48,
    components: baselineComponents([
      component('\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5_\u6e90\u94a3\u91d1-1'),
      component('\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5_\u6e90\u94a3\u91d1-1'),
      component('\u7bb1\u4f53\u7ad6\u9694\u677fL\u710a\u63a5_\u6e90\u94a3\u91d1-1'),
      component('\u7bb1\u4f53\u6a2a\u5c42\u677fL\u710a\u63a5_\u6e90\u94a3\u91d1_Y940-1'),
      component('\u95e8\u6846\u710a\u63a5_\u6e90\u94a3\u91d1_split-1'),
      component('\u5e95\u5ea7\u710a\u63a5_\u6e90\u94a3\u91d1-1'),
      component('\u4e0a\u76d6\u710a\u63a5_\u6e90\u94a3\u91d1-1'),
      component('\u9501\u5b54\u57fa\u51c6\u5de6_row01-1'),
      component('\u9501\u5b54\u57fa\u51c6\u5de6_row02-1'),
      component('\u9501\u5b54\u57fa\u51c6\u5de6_row03-1'),
      component('\u9501\u5b54\u57fa\u51c6\u53f3_row01-1'),
      component('\u9501\u5b54\u57fa\u51c6\u53f3_row02-1'),
      component('\u9501\u5b54\u57fa\u51c6\u53f3_row03-1'),
      component('\u5c42\u677f\u5b9a\u4f4d\u811a_L_Y940-1'),
      component('\u95e8\u6846\u5b9a\u4f4d\u7f3a\u53e3\u57fa\u51c6_L_Y940-1'),
      component('\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u5de6\u524d-1'),
      component('\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u5de6\u540e-1'),
      component('\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u53f3\u524d-1'),
      component('\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u53f3\u540e-1'),
      component('\u8c03\u6574\u811a M12X60-1'),
    ]),
  },
})
const sourceSheetMetalSplitIssueIds = new Set(sourceSheetMetalSplitFeedback.issues.map((item) => item.id))
assert.equal(sourceSheetMetalSplitIssueIds.has('missing_gold_cabinet_body_modules'), false)
assert.equal(sourceSheetMetalSplitIssueIds.has('non_template_cabinet_width_not_parameterized'), false)
assert.equal(sourceSheetMetalSplitIssueIds.has('missing_leveling_feet'), false)

const allowedTechnicalNameFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 23,
    components: baselineComponents([
      component('front_frame_split-1'),
      component('閿佸瓟鍩哄噯宸?1'),
      component('灞傛澘瀹氫綅鑴?1'),
      component('闂ㄦ瀹氫綅缂哄彛鍩哄噯-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀悗-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲悗-1'),
      component('璋冭妭鑴?1'),
    ]),
  },
})
const allowedTechnicalNameIssueIds = new Set(allowedTechnicalNameFeedback.issues.map((item) => item.id))
assert.equal(allowedTechnicalNameIssueIds.has('engineer_visible_english_names'), false)

const derivedSheetMetalFeedback = analyzeStructureFeedback({
  plan: nonTemplatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 48,
    components: baselineComponents([
      component('箱体左侧板焊接_派生钣金-1'),
      component('箱体右侧板焊接_派生钣金-1'),
      component('箱体竖隔板L焊接_派生钣金-1'),
      component('箱体横层板L焊接_派生钣金_Y482p5-1'),
      component('门框焊接_派生钣金-1'),
      component('底座焊接_派生钣金-1'),
      component('上盖焊接_派生钣金-1'),
      component('锁孔基准左_派生钣金_row01-1'),
      component('锁孔基准左_派生钣金_row02-1'),
      component('锁孔基准左_派生钣金_row03-1'),
      component('锁孔基准右_派生钣金_row01-1'),
      component('锁孔基准右_派生钣金_row02-1'),
      component('锁孔基准右_派生钣金_row03-1'),
      component('层板定位脚_派生钣金_row01-1'),
      component('层板定位脚_派生钣金_row02-1'),
      component('层板定位脚_派生钣金_row03-1'),
      component('门框定位缺口基准_派生钣金_row01-1'),
      component('内侧竖隔板加强板左前_派生钣金-1'),
      component('内侧竖隔板加强板左后_派生钣金-1'),
      component('内侧竖隔板加强板右前_派生钣金-1'),
      component('内侧竖隔板加强板右后_派生钣金-1'),
      component('调节脚_派生钣金_left_front-1'),
      component('后背中缝钣金连接片_派生钣金_Y482p5-1'),
    ]),
  },
})

const derivedSheetMetalIssueIds = new Set(derivedSheetMetalFeedback.issues.map((item) => item.id))
assert.equal(derivedSheetMetalIssueIds.has('non_template_cabinet_width_not_parameterized'), false)
assert.equal(derivedSheetMetalIssueIds.has('parametric_scaffold_requires_engineering_validation'), false)
assert.equal(derivedSheetMetalIssueIds.has('visible_cabinet_body_box_scaffold_components'), true)
assert.equal(derivedSheetMetalFeedback.status, 'issues_found')
assert.equal(derivedSheetMetalFeedback.derived.visibleCabinetBodyBoxScaffoldCount > 0, true)
assert.equal(derivedSheetMetalFeedback.derived.p0Count, 1)

const templatePlan = {
  requested: { cabinetWidthMm: 740, doorCount: 1 },
  derived: { compatibleWithNativeTemplate: true, doorCount: 1 },
  columns: [
    {
      rows: [
        { topYmm: 1855 },
      ],
    },
  ],
}

const templateFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 22,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
    ]),
  },
})

assert.equal(templateFeedback.status, 'clean')
assert.equal(templateFeedback.derived.issueCount, 0)
assert.ok(templateFeedback.derived.centerLockMaintenanceStripCount >= 1)
assert.ok(templateFeedback.derived.topLockCoverSheetMetalCount >= 1)

const missingExternalSheetMetalFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 22,
    components: baselineComponents([
      component('閿佸瓟鍩哄噯宸?1'),
      component('灞傛澘瀹氫綅鑴?1'),
      component('闂ㄦ瀹氫綅缂哄彛鍩哄噯-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀悗-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲悗-1'),
      component('璋冭妭鑴?1'),
    ]).filter((item) =>
      !item.name.includes('\u9501\u63a7\u7ef4\u62a4\u6761\u94a3\u91d1') &&
      !item.name.includes('\u9876\u90e8\u5e26\u9501\u76d6\u677f\u94a3\u91d1') &&
      !item.name.includes('\u4e0a\u76d6\u710a\u63a5')
    ),
  },
})
const missingExternalSheetMetalIssueIds = new Set(missingExternalSheetMetalFeedback.issues.map((item) => item.id))
assert.equal(missingExternalSheetMetalFeedback.status, 'issues_found')
assert.equal(missingExternalSheetMetalIssueIds.has('missing_center_lock_maintenance_sheetmetal_strip'), true)
assert.equal(missingExternalSheetMetalIssueIds.has('missing_top_lock_cover_sheetmetal_feature'), true)

const missingDoorLockTongueFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 22,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
    ]).filter((item) => !item.name.includes('锁舌')),
  },
})
const missingDoorLockTongueFeedbackIssueIds = new Set(missingDoorLockTongueFeedback.issues.map((item) => item.id))
assert.equal(missingDoorLockTongueFeedback.status, 'issues_found')
assert.equal(missingDoorLockTongueFeedbackIssueIds.has('missing_door_lock_tongue'), true)

const hardwareRestoredFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  allowElectricalLockHardware: true,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 24,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
      component('电控锁体ZJA-S500-1'),
      component('电控U型锁钩ZJA-S500-1'),
      component('锁控条左-1'),
    ]).filter((item) => !item.name.includes('锁舌')),
  },
})
assert.equal(hardwareRestoredFeedback.status, 'clean')
assert.equal(hardwareRestoredFeedback.derived.doorLockTongueCount, 1)
assert.equal(hardwareRestoredFeedback.derived.allowElectricalLockHardware, true)

const generatedDoorPanelBoxFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 23,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
      boxedPartComponent('储物柜门板1.714╱12-1', { xlen_mm: 412, ylen_mm: 254.385, zlen_mm: 15 }),
    ]),
  },
})
const generatedDoorPanelBoxIssueIds = new Set(generatedDoorPanelBoxFeedback.issues.map((item) => item.id))
assert.equal(generatedDoorPanelBoxFeedback.status, 'issues_found')
assert.equal(generatedDoorPanelBoxIssueIds.has('generated_door_panel_box_envelope_components'), true)
assert.equal(generatedDoorPanelBoxFeedback.derived.generatedDoorPanelBoxEnvelopeCount, 1)

const roleNamedTopLevelSheetMetalFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 23,
    components: baselineComponents([
      component('閿佸瓟鍩哄噯宸?1'),
      component('灞傛澘瀹氫綅鑴?1'),
      component('闂ㄦ瀹氫綅缂哄彛鍩哄噯-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘宸﹀悗-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲墠-1'),
      component('鍐呬晶绔栭殧鏉垮姞寮烘澘鍙冲悗-1'),
      component('璋冭妭鑴?1'),
      partComponent(`${GOLD_SOURCE_STRUCTURE_CONTRACT.cabinetBodyModules[2]}_body000-1`),
    ]),
  },
})
const roleNamedTopLevelIssueIds = new Set(roleNamedTopLevelSheetMetalFeedback.issues.map((item) => item.id))
assert.equal(roleNamedTopLevelIssueIds.has('cabinet_body_black_box_part'), false)

const electricalFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 24,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
      component('电控锁体ZJA-S500-1'),
      component('锁控板装配组件-1'),
    ]),
  },
})
const electricalIssueIds = new Set(electricalFeedback.issues.map((item) => item.id))
assert.equal(electricalFeedback.status, 'issues_found')
assert.equal(electricalIssueIds.has('electrical_or_electric_lock_components_present'), true)

const externalHoleFeedback = analyzeStructureFeedback({
  plan: templatePlan,
  structureRecord: {
    assembly_path: 'D:\\synthetic\\full_candidate.SLDASM',
    component_count: 24,
    components: baselineComponents([
      component('锁孔基准左-1'),
      component('层板定位脚-1'),
      component('门框定位缺口基准-1'),
      component('内侧竖隔板加强板左前-1'),
      component('内侧竖隔板加强板左后-1'),
      component('内侧竖隔板加强板右前-1'),
      component('内侧竖隔板加强板右后-1'),
      component('调节脚-1'),
      component('外侧顶板贯穿孔候选-1'),
    ]),
  },
})
const externalHoleIssueIds = new Set(externalHoleFeedback.issues.map((item) => item.id))
assert.equal(externalHoleIssueIds.has('external_through_hole_candidates_present'), true)

console.log('16029 structure feedback contract checks passed')
