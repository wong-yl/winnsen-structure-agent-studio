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
