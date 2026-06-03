import { readFileSync, writeFileSync } from 'node:fs'
import { basename, extname } from 'node:path'
import { pathToFileURL } from 'node:url'

export const STRUCTURE_FEEDBACK_SCHEMA = 'winnsen.locker16029.structure_feedback.v1'

export const GOLD_SOURCE_STRUCTURE_CONTRACT = Object.freeze({
  source: '参数化模板素材_U盘原始_20260526/16029 寄存柜(标准组合式 1917x1000x550)/1.工程图',
  cabinetBodyModules: [
    '箱体左侧板焊接',
    '箱体右侧板焊接',
    '箱体横层板L焊接',
    '箱体横层板R焊接',
    '箱体竖隔板L焊接',
    '箱体竖隔板R焊接',
    '门框焊接',
    '底座焊接',
    '上盖焊接',
  ],
  doorAssemblyLayers: [
    '储物柜门{ratio}装配',
    '储物柜门{ratio}焊接',
    '储物柜门板{ratio}',
    '柜门加强筋{ratio}',
    '插销固定板',
    'U型锁钩垫板',
    '锁舌',
    '门轴销',
    '塑料轴套',
    '开口挡圈',
  ],
})

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function textOf(value) {
  return String(value ?? '')
}

function hasLatin(value) {
  return /[A-Za-z]/.test(textOf(value))
}

function hasCjk(value) {
  return /[\u3400-\u9fff]/.test(textOf(value))
}

function extensionOf(component) {
  return extname(textOf(component?.path)).toLowerCase()
}

function componentBase(component) {
  return basename(textOf(component?.path))
}

function normalizeName(name) {
  return textOf(name).replace(/-\d+(?:\/.*)?$/, '')
}

function topLevelComponents(structureRecord) {
  return asArray(structureRecord?.components).filter((component) => Number(component.depth) === 0)
}

function visibleComponents(structureRecord) {
  return asArray(structureRecord?.components).filter((component) => !component.is_hidden && !component.is_suppressed)
}

function componentText(component) {
  const componentPath = textOf(component?.path)
  return `${textOf(component?.name)} ${basename(componentPath)}`
}

function hasVisibleComponentMatching(components, pattern) {
  return components.some((component) => pattern.test(componentText(component)))
}

function countVisibleComponentMatching(components, pattern) {
  return components.filter((component) => pattern.test(componentText(component))).length
}

function isParametricScaffoldComponent(component) {
  return /(?:\u53c2\u6570\u5316|_parametric\b|cabinet_(?:left|right)_(?:side|partition|shelf)_weldment|front_frame_weldment|base_weldment|top_cover_weldment|lock_(?:control_strip|mounting_hole_datum)|leveling_foot|partition_stiffener|shelf_locating|front_frame_locating)/i.test(componentText(component))
}

function isRearOverlayBackPanelComponent(component) {
  return /\u540e\u80cc\u677f\u4e2d\u5fc3\u63a5\u7f1d|centered[_ -]?back[_ -]?panel|back[_ -]?seam[_ -]?center/i.test(componentText(component))
}

function isRoleNamedInternalSheetMetalComponent(component) {
  const text = componentText(component)
  return GOLD_SOURCE_STRUCTURE_CONTRACT.cabinetBodyModules.some((role) => role && text.includes(role)) ||
    /\u9501\u5b54\u57fa\u51c6|\u5c42\u677f\u5b9a\u4f4d\u811a|\u95e8\u6846\u5b9a\u4f4d\u7f3a\u53e3\u57fa\u51c6|\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f|\u8c03\u6574\u811a/i.test(text)
}

function hasParametricScaffoldComponentMatching(components, pattern) {
  return components.some((component) =>
    isParametricScaffoldComponent(component) && pattern.test(componentText(component)),
  )
}

function requiredShelfBoundaryY(plan) {
  const values = []
  for (const column of asArray(plan?.columns)) {
    const rows = asArray(column.rows)
    for (const row of rows.slice(0, -1)) {
      const top = Number(row.topYmm)
      if (Number.isFinite(top)) values.push(top)
    }
  }
  return [...new Set(values.map((value) => Math.round(value * 1000) / 1000))].sort((a, b) => a - b)
}

function nearAny(value, candidates, tolerance) {
  return candidates.some((candidate) => Math.abs(value - candidate) <= tolerance)
}

function analyzeBodyProbe(plan, bodyProbe) {
  const requiredY = requiredShelfBoundaryY(plan)
  if (!bodyProbe || !requiredY.length) {
    return { requiredShelfBoundaryY: requiredY, unusedShelfBandCandidates: [] }
  }

  const candidates = []
  for (const body of asArray(bodyProbe.bodies)) {
    const xLen = Number(body.x_len_mm)
    const yLen = Number(body.y_len_mm)
    const zMin = Number(body.z_min_mm)
    const yMid = (Number(body.y_min_mm) + Number(body.y_max_mm)) / 2
    if (!Number.isFinite(xLen) || !Number.isFinite(yLen) || !Number.isFinite(zMin) || !Number.isFinite(yMid)) continue
    const shelfLike = yLen <= 45 && xLen >= 100 && zMin <= -100
    if (!shelfLike || nearAny(yMid, requiredY, 45)) continue
    candidates.push({
      index: body.index,
      name: body.name,
      yMidMm: Math.round(yMid * 1000) / 1000,
      xLenMm: xLen,
      yLenMm: yLen,
      zMinMm: zMin,
      zMaxMm: body.z_max_mm,
    })
  }
  return { requiredShelfBoundaryY: requiredY, unusedShelfBandCandidates: candidates }
}

function issue(id, severity, title, details = {}) {
  return { id, severity, title, ...details }
}

export function analyzeStructureFeedback({ plan, structureRecord, bodyProbe = null, moduleTargets = null } = {}) {
  const topLevel = topLevelComponents(structureRecord)
  const visible = visibleComponents(structureRecord)
  const issues = []
  const expectedCabinetTargets = asArray(moduleTargets?.targets)
  const compatibleWithNativeTemplate = plan?.derived?.compatibleWithNativeTemplate === true
  const requestedWidthMm = Number(plan?.requested?.cabinetWidthMm)
  const sourceWidthMm = 1000
  const sourceHeightMm = 1917
  const sourceDepthMm = 550

  const shellBlackBox = topLevel.find((component) => {
    const text = componentText(component)
    return extensionOf(component) === '.sldprt' &&
      !isParametricScaffoldComponent(component) &&
      !isRoleNamedInternalSheetMetalComponent(component) &&
      /gold[_ -]?shell|shell[_ -]?frame|frame[_ -]?shelf|shelf[_ -]?only|cabinet[_ -]?(?:shell|envelope|body)|\u7bb1\u4f53(?:\u603b\u6210|\u6574\u4f53|\u5916\u58f3|\u6846\u67b6)/i.test(text)
  })
  if (shellBlackBox) {
    issues.push(issue(
      'cabinet_body_black_box_part',
      'P0',
      '箱体仍是顶层零件，金标准要求拆成箱体子装配',
      { component: shellBlackBox.name, file: componentBase(shellBlackBox) },
    ))
  }

  const expectedCabinetModules = expectedCabinetTargets.length
    ? expectedCabinetTargets.map((target) => ({ key: target.key, role: target.role, binding: target.binding }))
    : GOLD_SOURCE_STRUCTURE_CONTRACT.cabinetBodyModules.map((role) => ({ key: '', role, binding: null }))
  const missingCabinetModules = expectedCabinetModules.filter((target) =>
    !topLevel.some((component) => {
      const text = componentText(component)
      const role = textOf(target.role)
      const key = textOf(target.key)
      return (role && text.includes(role)) || (key && text.toLowerCase().includes(key.toLowerCase()))
    }),
  )
  if (missingCabinetModules.length) {
    issues.push(issue(
      'missing_gold_cabinet_body_modules',
      'P0',
      '当前顶层装配缺少金标准箱体模块',
      {
        missing: missingCabinetModules.map((target) => target.role),
        missingTargets: missingCabinetModules,
        expectedTargetCount: expectedCabinetModules.length,
      },
    ))
  }

  const parametricCabinetSignals = {
    sidePanels: hasParametricScaffoldComponentMatching(visible, /cabinet_(?:left|right)_side_weldment|\u7bb1\u4f53.*\u4fa7\u677f.*\u53c2\u6570\u5316/i),
    partitionsOrShelves: hasParametricScaffoldComponentMatching(visible, /cabinet_(?:left|right)_(?:partition|shelf)_weldment|\u7bb1\u4f53.*(?:\u7ad6\u9694\u677f|\u6a2a\u5c42\u677f).*\u53c2\u6570\u5316/i),
    frontFrame: hasParametricScaffoldComponentMatching(visible, /front_frame_weldment|\u95e8\u6846.*\u53c2\u6570\u5316/i),
    base: hasParametricScaffoldComponentMatching(visible, /base_weldment|\u5e95\u5ea7.*\u53c2\u6570\u5316/i),
    topCover: hasParametricScaffoldComponentMatching(visible, /top_cover_weldment|\u4e0a\u76d6.*\u53c2\u6570\u5316/i),
  }
  const hasParameterizedCabinetEnvelope = Object.values(parametricCabinetSignals).every(Boolean)
  if (!compatibleWithNativeTemplate && Number.isFinite(requestedWidthMm) && !hasParameterizedCabinetEnvelope) {
    issues.push(issue(
      'non_template_cabinet_width_not_parameterized',
      'P0',
      'Non-template width still falls short of the 1000W gold/source structural reference; cabinet body, front frame, base, and top cover must be parameterized before engineer handoff.',
      {
        requestedWidthMm,
        sourceEnvelopeMm: {
          width: sourceWidthMm,
          height: sourceHeightMm,
          depth: sourceDepthMm,
        },
        parametricCabinetSignals,
        requiredAction: 'Compare against the 1000W gold/source reference, replace fixed/provisional cabinet envelope modules with width-aware SolidWorks modules, then rerun Pack-and-Go.',
      },
    ))
  }

  const parametricScaffoldTopLevel = topLevel.filter(isParametricScaffoldComponent)
  if (parametricScaffoldTopLevel.length) {
    issues.push(issue(
      'parametric_scaffold_requires_engineering_validation',
      'P1',
      'Parametric scaffold components are provisional geometry; the model must remain an engineering-validation package until compared against the 1000W gold/source reference.',
      {
        count: parametricScaffoldTopLevel.length,
        compatibleWithNativeTemplate,
        examples: parametricScaffoldTopLevel.slice(0, 12).map((component) => component.name),
        requiredAction: 'Do not mark this output clean or engineer-ready. Review scaffold cabinet modules, lock-hole datums, and leveling feet against the 1000W gold/source model.',
      },
    ))
  }

  const rearOverlayPanels = visible.filter(isRearOverlayBackPanelComponent)
  if (rearOverlayPanels.length) {
    issues.push(issue(
      'rear_back_panel_overlay_box_components',
      'P0',
      'Rear back seam was created with standalone overlay panels; it must be repaired through cabinet side-panel sheet metal instead.',
      {
        count: rearOverlayPanels.length,
        examples: rearOverlayPanels.slice(0, 6).map((component) => component.name),
        requiredAction: 'Remove the overlay back-panel parts and trim/rebuild the copied left/right cabinet side-panel sheet metal to the centered rear seam datum.',
      },
    ))
  }

  const hasElectricalOrElectricLock = hasVisibleComponentMatching(
    visible,
    /\u7535\u63a7\u9501\u4f53|\u7535\u63a7U\u578b\u9501\u94a9|\u9501\u63a7\u677f\u88c5\u914d\u7ec4\u4ef6|\u7535\u8def\u677f\u652f\u67b6|ZJA-S500|LK-4-6|M9 V1\.1|electric[_ -]?lock[_ -]?(?:body|hook)|lock[_ -]?control[_ -]?board/i,
  )
  if (hasElectricalOrElectricLock) {
    issues.push(issue(
      'electrical_or_electric_lock_components_present',
      'P1',
      'Generated structure package contains electrical or electric-lock components; generated review models should keep only sheet-metal structure and hole/interface datums.',
      {
        requiredAction: 'Remove lock-control boards, circuit-board brackets, electric lock bodies, and electric lock hooks from the generated model. Preserve the corresponding lock/electrical mounting holes as structural datums.',
      },
    ))
  }

  const hasDoorModules = hasVisibleComponentMatching(visible, /\u50a8\u7269\u67dc\u95e8|door[_ -]?module/i)
  const expectedDoorCount = Number(plan?.derived?.doorCount || plan?.requested?.doorCount || 0)
  const lockHoleDatumCount = countVisibleComponentMatching(visible, /\u9501\u5b54\u57fa\u51c6|\u9501\u5b89\u88c5\u5b54\u4f4d|lock[_ -]?(?:mounting[_ -]?)?hole[_ -]?datum/i)
  const hasLockHoleDatum = lockHoleDatumCount > 0
  if (hasDoorModules && !hasLockHoleDatum) {
    issues.push(issue(
      'missing_lock_mounting_hole_datum',
      'P0',
      'Door modules are present, but the lock/electrical mounting hole datum is missing from the generated structure package.',
      {
        requiredAction: 'Add lock-hole datum geometry tied to the row plan and 1000W gold/source reference positions; do not add the electrical or electric-lock components themselves.',
      },
    ))
  } else if (hasDoorModules && expectedDoorCount > 0 && lockHoleDatumCount < expectedDoorCount) {
    issues.push(issue(
      'lock_mounting_hole_datum_count_below_door_rows',
      'P0',
      'Lock/electrical mounting hole datums must follow the door row plan one-for-one.',
      {
        actual: lockHoleDatumCount,
        expectedMinimum: expectedDoorCount,
        requiredAction: 'Place row-specific lock-hole datum geometry at the same datum used by the lock-side mounting interface; do not add electric-lock bodies or hooks.',
      },
    ))
  }

  const doorLockTongueCount = countVisibleComponentMatching(visible, /\u9501\u820c|lock[_ -]?tongue/i)
  if (hasDoorModules && expectedDoorCount > 0 && doorLockTongueCount < expectedDoorCount) {
    issues.push(issue(
      'missing_door_lock_tongue',
      'P0',
      'Door modules are present, but the mechanical door lock tongue geometry is missing or below the row count.',
      {
        actual: doorLockTongueCount,
        expectedMinimum: expectedDoorCount,
        requiredAction: 'Restore the frozen v43 door-route lock tongue geometry for each door module. Do not add cabinet-side electric lock bodies, lock-control boards, or electric-lock hook named components.',
      },
    ))
  }

  const shelfLocatorCount = countVisibleComponentMatching(
    visible,
    /\u5c42\u677f\u5b9a\u4f4d\u811a|\u95e8\u6846\u5b9a\u4f4d\u7f3a\u53e3|shelf[_ -]?locating|front[_ -]?frame[_ -]?locating[_ -]?notch/i,
  )
  const expectedShelfLocatorCount = Math.max(1, requiredShelfBoundaryY(plan).length)
  if (hasDoorModules && shelfLocatorCount < expectedShelfLocatorCount) {
    issues.push(issue(
      'missing_shelf_front_frame_locating_interface',
      'P0',
      'Shelf/front-frame locating interface is incomplete; gold feedback requires locating feet and matching notches.',
      {
        actual: shelfLocatorCount,
        expectedMinimum: expectedShelfLocatorCount,
        requiredAction: 'Add shelf locating feet and matching front-frame locating notches at the requested row boundaries; remove unused long slots.',
      },
    ))
  }

  const partitionStiffenerCount = countVisibleComponentMatching(
    visible,
    /\u5185\u4fa7\u7ad6\u9694\u677f\u52a0\u5f3a\u677f|partition[_ -]?stiffener/i,
  )
  if (hasDoorModules && partitionStiffenerCount < 4) {
    issues.push(issue(
      'missing_inner_vertical_partition_stiffeners',
      'P0',
      'Inner vertical partitions are missing the left/right reinforcement plates requested by engineering feedback.',
      {
        actual: partitionStiffenerCount,
        expectedMinimum: 4,
        requiredAction: 'Add left/right front/rear inner vertical partition reinforcement plates and keep them visible in the structure tree.',
      },
    ))
  }

  const externalThroughHoleCandidateCount = countVisibleComponentMatching(
    visible,
    /\u5916\u4fa7.*\u8d2f\u7a7f\u5b54|external[_ -]?through[_ -]?hole|outside[_ -]?face[_ -]?hole/i,
  )
  if (externalThroughHoleCandidateCount > 0) {
    issues.push(issue(
      'external_through_hole_candidates_present',
      'P0',
      'Exterior top/side/rear faces still expose through-hole candidates that should be internal-only.',
      {
        count: externalThroughHoleCandidateCount,
        requiredAction: 'Move or suppress these holes so only the internal top/side interface carries the datum.',
      },
    ))
  }

  const hasBaseOrNut = hasVisibleComponentMatching(visible, /\u5e95\u5ea7|\u87ba\u6bcdM12|M12/i)
  const hasLevelingFoot = hasVisibleComponentMatching(
    visible,
    /\u8c03\u8282\u811a|\u5730\u811a|\u811a\u676f|level(?:l)?ing[_ -]?foot|level(?:l)?ing[_ -]?feet|adjust(?:able)?[_ -]?foot|adjust(?:able)?[_ -]?feet/i,
  )
  if (hasBaseOrNut && !hasLevelingFoot) {
    issues.push(issue(
      'missing_leveling_feet',
      'P0',
      'Base weldment is present, but leveling feet are missing; M12 nuts alone are not a complete base module.',
      {
        requiredAction: 'Add adjustable leveling feet as visible base components and bind their placement to the base datum.',
      },
    ))
  }

  const rightMirrorPanels = visible.filter((component) =>
    /right_mirror_ordinary_panel/i.test(componentText(component)),
  )
  if (rightMirrorPanels.length) {
    issues.push(issue(
      'right_door_panel_import_body_name',
      'P1',
      '右侧门板仍暴露为输入/镜像实体命名，需与左侧门板统一为钣金门板表达',
      { examples: rightMirrorPanels.slice(0, 6).map((component) => component.name) },
    ))
  }

  const visibleLatinNames = visible
    .filter((component) => hasLatin(component.name) && !hasCjk(component.name))
    .map((component) => component.name)
  if (visibleLatinNames.length) {
    issues.push(issue(
      'engineer_visible_english_names',
      'P2',
      '工程师可见组件仍有英文命名，需按中文命名规则收敛',
      { count: visibleLatinNames.length, examples: visibleLatinNames.slice(0, 12) },
    ))
  }

  const bodyProbeSummary = analyzeBodyProbe(plan, bodyProbe)
  if (bodyProbeSummary.unusedShelfBandCandidates.length) {
    issues.push(issue(
      'unused_shelf_band_candidates',
      'P1',
      '箱体内存在不贴合当前门高比例的层板候选，需要删除或抑制',
      {
        requiredShelfBoundaryY: bodyProbeSummary.requiredShelfBoundaryY,
        candidates: bodyProbeSummary.unusedShelfBandCandidates.slice(0, 20),
        candidateCount: bodyProbeSummary.unusedShelfBandCandidates.length,
      },
    ))
  }

  return {
    schema: STRUCTURE_FEEDBACK_SCHEMA,
    status: issues.length ? 'issues_found' : 'clean',
    contract: GOLD_SOURCE_STRUCTURE_CONTRACT,
    generated: {
      structureRecord: structureRecord?.assembly_path ?? '',
      componentCount: structureRecord?.component_count ?? 0,
      topLevelCount: topLevel.length,
      topLevelNames: topLevel.map((component) => component.name),
    },
    derived: {
      requestedShelfBoundaryY: bodyProbeSummary.requiredShelfBoundaryY,
      goldModuleTargetCount: expectedCabinetTargets.length,
      goldShelfModuleTargetCount: Number(moduleTargets?.derived?.shelfModuleCount || 0),
      goldTargetShelfBoundaryY: asArray(moduleTargets?.derived?.requestedUniqueShelfBoundaryY),
      lockMountingHoleDatumCount: lockHoleDatumCount,
      doorLockTongueCount,
      shelfFrontFrameLocatingInterfaceCount: shelfLocatorCount,
      innerVerticalPartitionStiffenerCount: partitionStiffenerCount,
      externalThroughHoleCandidateCount,
      issueCount: issues.length,
      p0Count: issues.filter((item) => item.severity === 'P0').length,
      p1Count: issues.filter((item) => item.severity === 'P1').length,
      p2Count: issues.filter((item) => item.severity === 'P2').length,
    },
    issues,
  }
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    args[item.slice(2)] = argv[index + 1]
    index += 1
  }
  return args
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  if (!args.plan || !args.structure) {
    throw new Error('Usage: node tools/locker_16029_structure_feedback.mjs --plan <plan.json> --structure <record.json> [--module-targets <targets.json>] [--body-probe <bodies.json>] [--out <feedback.json>]')
  }

  const feedback = analyzeStructureFeedback({
    plan: readJson(args.plan),
    structureRecord: readJson(args.structure),
    bodyProbe: args['body-probe'] ? readJson(args['body-probe']) : null,
    moduleTargets: args['module-targets'] ? readJson(args['module-targets']) : null,
  })
  const text = `${JSON.stringify(feedback, null, 2)}\n`
  if (args.out) {
    writeFileSync(args.out, text, 'utf8')
  } else {
    process.stdout.write(text)
  }
  return feedback
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
