import assert from 'node:assert/strict'
import { analyzeGoldStructureGate } from './verify_16029_gold_structure_gate.mjs'

function component(name, depth = 0) {
  const item = {
    depth,
    name,
    path: `D:\\synthetic\\${name}.SLDASM`,
    is_hidden: false,
    is_suppressed: false,
  }
  if (name.includes('\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5')) {
    item.box = { xmin_mm: -370, xmax_mm: 14.7, ymin_mm: 23.8, ymax_mm: 1858.2, zmin_mm: -550, zmax_mm: -19.5, xlen_mm: 384.7, ylen_mm: 1834.4, zlen_mm: 530.5 }
  } else if (name.includes('\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5')) {
    item.box = { xmin_mm: 0.5, xmax_mm: 370, ymin_mm: 23.8, ymax_mm: 1858.2, zmin_mm: -550, zmax_mm: -19.5, xlen_mm: 369.5, ylen_mm: 1834.4, zlen_mm: 530.5 }
  } else if (name.includes('centered_back_panel_left') || name.includes('\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d_centered_left')) {
    item.box = { xmin_mm: -370, xmax_mm: -0.5, ymin_mm: 26.8, ymax_mm: 1839.2, zmin_mm: -551.2, zmax_mm: -550 }
  } else if (name.includes('centered_back_panel_right') || name.includes('\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d_centered_right')) {
    item.box = { xmin_mm: 0.5, xmax_mm: 370, ymin_mm: 26.8, ymax_mm: 1839.2, zmin_mm: -551.2, zmax_mm: -550 }
  }
  return item
}

function record({ componentCount, topLevelCount, maxDepth, names }) {
  const components = []
  for (const name of names) components.push(component(name, 0))
  while (components.filter((item) => item.depth === 0).length < topLevelCount) {
    components.push(component(`top_level_fill_${components.length}`, 0))
  }
  while (components.length < componentCount) {
    components.push(component(`nested_fill_${components.length}`, components.length % maxDepth === 0 ? maxDepth : 1))
  }
  return {
    opened: true,
    component_count: componentCount,
    components,
  }
}

const requiredGoldRoleNames = [
  '\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5-1',
  '\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5-1',
  '\u7bb1\u4f53\u6a2a\u5c42\u677fL\u710a\u63a5-1',
  '\u7bb1\u4f53\u6a2a\u5c42\u677fR\u710a\u63a5-1',
  '\u7bb1\u4f53\u7ad6\u9694\u677fL\u710a\u63a5-1',
  '\u7bb1\u4f53\u7ad6\u9694\u677fR\u710a\u63a5-1',
  '\u95e8\u6846\u710a\u63a5-1',
  '\u5e95\u5ea7\u710a\u63a5-1',
  '\u4e0a\u76d6\u710a\u63a5-1',
  '\u9501\u63a7\u7ef4\u62a4\u6761\u94a3\u91d1-1',
  '\u9876\u90e8\u5e26\u9501\u76d6\u677f\u94a3\u91d1-1',
  '\u9501\u5b54\u57fa\u51c6\u5de6-1',
  '\u9501\u5b54\u57fa\u51c6\u5de6-2',
  '\u9501\u5b54\u57fa\u51c6\u5de6-3',
  '\u9501\u5b54\u57fa\u51c6\u53f3-1',
  '\u9501\u5b54\u57fa\u51c6\u53f3-2',
  '\u9501\u5b54\u57fa\u51c6\u53f3-3',
  '\u9501\u5b54\u57fa\u51c6\u53f3-4',
  '\u9501\u820c\u5de6-1',
  '\u9501\u820c\u5de6-2',
  '\u9501\u820c\u5de6-3',
  '\u9501\u820c\u53f3-1',
  '\u9501\u820c\u53f3-2',
  '\u9501\u820c\u53f3-3',
  '\u9501\u820c\u53f3-4',
  '\u5c42\u677f\u5b9a\u4f4d\u811a-1',
  '\u95e8\u6846\u5b9a\u4f4d\u7f3a\u53e3\u57fa\u51c6-1',
  '\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u5de6\u524d-1',
  '\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u5de6\u540e-1',
  '\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u53f3\u524d-1',
  '\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f\u53f3\u540e-1',
  '\u8c03\u6574\u811a M12X60(\u6a21\u578b)-1',
  '\u8c03\u6574\u811a M12X60(\u6a21\u578b)-2',
  '\u8c03\u6574\u811a M12X60(\u6a21\u578b)-3',
  '\u8c03\u6574\u811a M12X60(\u6a21\u578b)-4',
  '\u50a8\u7269\u67dc\u95e82\u257112\u88c5\u914d-1',
  '\u95e8\u8f74\u9500(\u77ed)-1',
  '\u5e94\u6025\u7ef4\u62a4\u95e8\u710a\u63a5-1',
]

const goldLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: requiredGoldRoleNames,
})

const goldLikeResult = analyzeGoldStructureGate({ candidateRecord: goldLike, expectedDoorCount: 7 })
assert.equal(goldLikeResult.status, 'PASS')
assert.equal(goldLikeResult.summary.componentCount, 320)
assert.equal(goldLikeResult.summary.lockMountingHoleDatumCount, 7)
assert.equal(goldLikeResult.summary.doorLockTongueCount, 7)
assert.ok(goldLikeResult.summary.centerLockMaintenanceStripCount >= 1)
assert.ok(goldLikeResult.summary.topLockCoverSheetMetalCount >= 1)
assert.equal(goldLikeResult.summary.shelfFrontFrameLocatingInterfaceCount, 2)
assert.equal(goldLikeResult.summary.innerVerticalPartitionStiffenerCount, 4)
assert.equal(goldLikeResult.summary.issueCount, 0)
assert.equal(goldLikeResult.summary.centeredBackSeam.centered, true)
assert.equal(goldLikeResult.summary.centeredBackSeam.overlayPanelCount, 0)
assert.equal(goldLikeResult.baseline.componentCountComparisonMode, 'door_count_scaled_gold_floor')
assert.equal(goldLikeResult.baseline.minComponentCount, 147)

const sixDoorRoleComplete = record({
  componentCount: 195,
  topLevelCount: 36,
  maxDepth: 2,
  names: requiredGoldRoleNames.slice(0, -1),
})
const sixDoorRoleCompleteResult = analyzeGoldStructureGate({ candidateRecord: sixDoorRoleComplete, expectedDoorCount: 6 })
assert.equal(sixDoorRoleCompleteResult.status, 'PASS')
assert.equal(sixDoorRoleCompleteResult.baseline.componentCountComparisonMode, 'door_count_scaled_gold_floor')
assert.equal(sixDoorRoleCompleteResult.baseline.minComponentCount, 126)
assert.equal(sixDoorRoleCompleteResult.summary.warningCount, 0)
assert.equal(sixDoorRoleCompleteResult.summary.serviceAccessDoorPolicy, 'not_applicable')

const missingDoorLockTongueLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: requiredGoldRoleNames.filter((name) => !name.includes('\u9501\u820c')),
})
const missingDoorLockTongueResult = analyzeGoldStructureGate({ candidateRecord: missingDoorLockTongueLike, expectedDoorCount: 6 })
const missingDoorLockTongueIssueIds = new Set(missingDoorLockTongueResult.issues.map((item) => item.id))
assert.equal(missingDoorLockTongueResult.status, 'FAIL')
assert.equal(missingDoorLockTongueResult.summary.doorLockTongueCount, 0)
assert.equal(missingDoorLockTongueIssueIds.has('missing_door_lock_tongue'), true)

const electricHookAsLockTongueLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: [
    ...requiredGoldRoleNames.filter((name) => !name.includes('\u9501\u820c')),
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-1',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-2',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-3',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-4',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-5',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-6',
    '\u9501\u63a7\u6761\u5de6-1',
    '\u9501\u63a7\u6761\u53f3-1',
  ],
})
const electricHookAsLockTongueResult = analyzeGoldStructureGate({
  candidateRecord: electricHookAsLockTongueLike,
  expectedDoorCount: 6,
  allowElectricalLockHardware: true,
})
assert.equal(electricHookAsLockTongueResult.status, 'PASS')
assert.equal(electricHookAsLockTongueResult.summary.doorLockTongueCount, 6)
assert.equal(electricHookAsLockTongueResult.summary.lockControlStripPlaceholderCount, 0)
assert.equal(electricHookAsLockTongueResult.summary.allowElectricalLockHardware, true)

const parametricDirectoryOnly = JSON.parse(JSON.stringify(sixDoorRoleComplete))
parametricDirectoryOnly.components = parametricDirectoryOnly.components.map((item) => ({
  ...item,
  path: `D:\\synthetic\\sw2020_full_740W_parametric_template\\pack_and_go\\${item.name}.SLDPRT`,
}))
const parametricDirectoryOnlyResult = analyzeGoldStructureGate({
  candidateRecord: parametricDirectoryOnly,
  expectedDoorCount: 6,
})
assert.equal(parametricDirectoryOnlyResult.status, 'PASS')
assert.equal(parametricDirectoryOnlyResult.summary.provisionalParametricScaffoldCount, 0)

const optionalServiceDoorResult = analyzeGoldStructureGate({
  candidateRecord: sixDoorRoleComplete,
  expectedDoorCount: 6,
  serviceAccessDoorPolicy: 'optional',
})
assert.equal(optionalServiceDoorResult.status, 'PASS')
assert.equal(optionalServiceDoorResult.summary.warningCount, 1)
assert.equal(optionalServiceDoorResult.warnings[0].id, 'missing_optional_service_access_door')

const requiredServiceDoorResult = analyzeGoldStructureGate({
  candidateRecord: sixDoorRoleComplete,
  expectedDoorCount: 6,
  serviceAccessDoorPolicy: 'required',
})
const requiredServiceDoorIssueIds = new Set(requiredServiceDoorResult.issues.map((item) => item.id))
assert.equal(requiredServiceDoorResult.status, 'FAIL')
assert.equal(requiredServiceDoorIssueIds.has('missing_required_service_access_door'), true)

const overlayBackPanelLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: [
    ...requiredGoldRoleNames,
    '\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d_centered_left-1',
    '\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d_centered_right-1',
  ],
})
const overlayBackPanelResult = analyzeGoldStructureGate({ candidateRecord: overlayBackPanelLike, expectedDoorCount: 6 })
const overlayBackPanelIssueIds = new Set(overlayBackPanelResult.issues.map((item) => item.id))
assert.equal(overlayBackPanelResult.status, 'FAIL')
assert.equal(overlayBackPanelResult.summary.centeredBackSeam.overlayPanelCount, 2)
assert.equal(overlayBackPanelIssueIds.has('back_panel_seam_not_centered'), true)

const absoluteFloorResult = analyzeGoldStructureGate({ candidateRecord: sixDoorRoleComplete })
const absoluteFloorIssueIds = new Set(absoluteFloorResult.issues.map((item) => item.id))
assert.equal(absoluteFloorResult.status, 'FAIL')
assert.equal(absoluteFloorResult.baseline.componentCountComparisonMode, 'absolute_gold_floor')
assert.equal(absoluteFloorResult.baseline.minComponentCount, 253)
assert.equal(absoluteFloorIssueIds.has('component_count_below_gold_floor'), true)

const scaffoldLike = record({
  componentCount: 117,
  topLevelCount: 33,
  maxDepth: 2,
  names: [
    '\u7bb1\u4f53\u5de6\u4fa7\u677f\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u7bb1\u4f53\u53f3\u4fa7\u677f\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u7bb1\u4f53\u6a2a\u5c42\u677fL\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u7bb1\u4f53\u6a2a\u5c42\u677fR\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u7bb1\u4f53\u7ad6\u9694\u677fL\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u7bb1\u4f53\u7ad6\u9694\u677fR\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u95e8\u6846\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u5e95\u5ea7\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u4e0a\u76d6\u710a\u63a5_\u53c2\u6570\u5316-1',
    '\u9501\u63a7\u6761\u5de6-1',
    '\u9501\u63a7\u6761\u53f3-1',
    '\u8c03\u8282\u811a-1',
    '\u8c03\u8282\u811a-2',
    '\u8c03\u8282\u811a-3',
    '\u8c03\u8282\u811a-4',
    '\u50a8\u7269\u67dc\u95e83\u257112\u88c5\u914d_\u5de6-1',
    '\u95e8\u8f74\u9500-1',
  ],
})

const scaffoldResult = analyzeGoldStructureGate({ candidateRecord: scaffoldLike, expectedDoorCount: 7 })
const scaffoldIssueIds = new Set(scaffoldResult.issues.map((item) => item.id))
assert.equal(scaffoldResult.status, 'FAIL')
assert.equal(scaffoldResult.summary.componentCount, 117)
assert.equal(scaffoldResult.baseline.minComponentCount, 147)
assert.equal(scaffoldResult.summary.lockMountingHoleDatumCount, 0)
assert.equal(scaffoldResult.summary.centerLockMaintenanceStripCount, 0)
assert.equal(scaffoldResult.summary.topLockCoverSheetMetalCount, 0)
assert.equal(scaffoldResult.summary.lockControlStripPlaceholderCount, 2)
assert.equal(scaffoldResult.summary.provisionalParametricScaffoldCount, 9)
assert.equal(scaffoldIssueIds.has('component_count_below_gold_floor'), true)
assert.equal(scaffoldIssueIds.has('missing_lock_mounting_hole_datum'), true)
assert.equal(scaffoldIssueIds.has('missing_center_lock_maintenance_sheetmetal_strip'), true)
assert.equal(scaffoldIssueIds.has('missing_top_lock_cover_sheetmetal_feature'), true)
assert.equal(scaffoldIssueIds.has('missing_shelf_front_frame_locating_interface'), true)
assert.equal(scaffoldIssueIds.has('missing_inner_vertical_partition_stiffeners'), true)
assert.equal(scaffoldIssueIds.has('provisional_parametric_scaffold_components'), true)
assert.equal(scaffoldIssueIds.has('legacy_lock_control_strip_placeholder'), true)

const electricalLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: [
    ...requiredGoldRoleNames,
    '\u9501\u63a7\u677f\u88c5\u914d\u7ec4\u4ef6-1',
    '\u7535\u63a7\u9501\u4f53ZJA-S500-1',
    '\u7535\u63a7U\u578b\u9501\u94a9ZJA-S500-1',
  ],
})
const electricalResult = analyzeGoldStructureGate({ candidateRecord: electricalLike, expectedDoorCount: 7 })
const electricalIssueIds = new Set(electricalResult.issues.map((item) => item.id))
assert.equal(electricalResult.status, 'FAIL')
assert.equal(electricalResult.summary.electricalOrElectricLockComponentCount, 3)
assert.equal(electricalIssueIds.has('electrical_or_electric_lock_components_present'), true)

const allowedElectricalResult = analyzeGoldStructureGate({
  candidateRecord: electricalLike,
  expectedDoorCount: 7,
  allowElectricalLockHardware: true,
})
assert.equal(allowedElectricalResult.status, 'PASS')
assert.equal(allowedElectricalResult.summary.electricalOrElectricLockComponentCount, 3)

const externalHoleLike = record({
  componentCount: 320,
  topLevelCount: 36,
  maxDepth: 2,
  names: [
    ...requiredGoldRoleNames,
    '\u5916\u4fa7\u9876\u677F\u8d2f\u7a7f\u5b54\u5019\u9009-1',
  ],
})
const externalHoleResult = analyzeGoldStructureGate({ candidateRecord: externalHoleLike, expectedDoorCount: 7 })
const externalHoleIssueIds = new Set(externalHoleResult.issues.map((item) => item.id))
assert.equal(externalHoleResult.status, 'FAIL')
assert.equal(externalHoleIssueIds.has('external_through_hole_candidates_present'), true)

console.log('16029 gold structure gate contract checks passed')
