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
    '门轴销',
    '塑料轴套',
    '开口挡圈',
    '电控U型锁钩ZJA-S500',
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

  const shellBlackBox = topLevel.find((component) => {
    const name = textOf(component.name)
    return extensionOf(component) === '.sldprt' && /shell|frame|shelf|箱体/i.test(name)
  })
  if (shellBlackBox) {
    issues.push(issue(
      'cabinet_body_black_box_part',
      'P0',
      '箱体仍是顶层零件，金标准要求拆成箱体子装配',
      { component: shellBlackBox.name, file: componentBase(shellBlackBox) },
    ))
  }

  const topNames = topLevel.map((component) => normalizeName(component.name))
  const expectedCabinetModules = expectedCabinetTargets.length
    ? expectedCabinetTargets.map((target) => ({ key: target.key, role: target.role, binding: target.binding }))
    : GOLD_SOURCE_STRUCTURE_CONTRACT.cabinetBodyModules.map((role) => ({ key: '', role, binding: null }))
  const missingCabinetModules = expectedCabinetModules.filter((target) =>
    !topNames.some((name) => textOf(name).includes(textOf(target.role))),
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

  const rightMirrorPanels = visible.filter((component) =>
    /right_mirror_ordinary_panel/i.test(`${component.name} ${component.path}`),
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
