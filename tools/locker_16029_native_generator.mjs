import { createHash } from 'node:crypto'
import { build16029ParametricContract } from './lib/locker_16029_parametric_contract.mjs'

const FIXED_HEIGHT_MM = 1917
const FIXED_COLUMNS = 2
const CURRENT_WIDTH_ALLOWANCE_MM = 126
const MIN_NATIVE_TASK_DOOR_COUNT = 2
const MAX_NATIVE_TASK_DOOR_COUNT = 48
const MIN_REQUESTED_WIDTH_MM = 120
const MAX_REQUESTED_WIDTH_MM = 2000
const MIN_CABINET_DEPTH_MM = 300
const MAX_CABINET_DEPTH_MM = 800

export const NATIVE_16029_GENERATOR = Object.freeze({
  id: '16029-solidworks-native-generator-v1',
  cad: 'SolidWorks 2020',
  purpose: 'structure_engineering_assistance',
  sourcePolicy: 'verified_native_seeds_only',
  supportedDoorCounts: Object.freeze([4, 6]),
  nativeTaskDoorCountRange: Object.freeze({
    min: MIN_NATIVE_TASK_DOOR_COUNT,
    max: MAX_NATIVE_TASK_DOOR_COUNT,
    step: 2,
  }),
  unmatchedRequestPolicy: 'create_native_build_task',
})

export const NATIVE_16029_SEEDS = Object.freeze([
  Object.freeze({
    id: 'v37-760w-six-door-l642-r246',
    version: 'V37',
    label: 'V37 760宽六门 L642-R246 结构工程辅助基础模型',
    assetId: '16029-v43-v37-760w-six-door-engineering-assistance-zip',
    cabinetWidthMm: 760,
    cabinetHeightMm: FIXED_HEIGHT_MM,
    cabinetDepthMm: 550,
    columns: FIXED_COLUMNS,
    doorCount: 6,
    installedDoorPanelWidthMm: 317,
    referenceDoorHeightMm: 908,
    rowSequence: 'L642-R246',
    zipSizeBytes: 24535798,
    zipSha256: '6B9F62E4F697B96909131D81EAD0FA341E059530204DB76480148460883CD1C0',
    validatedCapabilities: Object.freeze([
      'native_width_760',
      'six_door_mixed_height_layout',
      'one_door_one_lock_opening',
      'local_reference_closure',
      'rebuild_save_reopen',
    ]),
  }),
  Object.freeze({
    id: 'v36-740w-four-door-l66-r66',
    version: 'V36',
    label: 'V36 740宽四门 L66-R66 结构工程辅助基础模型',
    assetId: '16029-v43-v36-four-door-engineering-assistance-zip',
    cabinetWidthMm: 740,
    cabinetHeightMm: FIXED_HEIGHT_MM,
    cabinetDepthMm: 550,
    columns: FIXED_COLUMNS,
    doorCount: 4,
    installedDoorPanelWidthMm: 307,
    referenceDoorHeightMm: 908,
    rowSequence: 'L66-R66',
    zipSizeBytes: 25096688,
    zipSha256: 'DEF241BB9B0D530EB73130C5A3FCBDC22F206B6A59C65EBC5F6CE16B985C3AA4',
    validatedCapabilities: Object.freeze([
      'native_width_740',
      'four_door_equal_height_layout',
      'one_door_one_lock_opening',
      'local_reference_closure',
      'rebuild_save_reopen',
    ]),
  }),
  Object.freeze({
    id: 'v35-740w-six-door-l642-r246',
    version: 'V35',
    label: 'V35 740宽六门 L642-R246 结构工程辅助基础模型',
    assetId: '16029-v43-v35-one-door-one-lock-hole-rereview-zip',
    cabinetWidthMm: 740,
    cabinetHeightMm: FIXED_HEIGHT_MM,
    cabinetDepthMm: 550,
    columns: FIXED_COLUMNS,
    doorCount: 6,
    installedDoorPanelWidthMm: 307,
    referenceDoorHeightMm: 908,
    rowSequence: 'L642-R246',
    zipSizeBytes: 24741785,
    zipSha256: '440596F0C9E321D0FF04EC978EEBAB31DDFA6D542E337BDA00C5A3B9701963FA',
    validatedCapabilities: Object.freeze([
      'native_width_740',
      'six_door_mixed_height_layout',
      'one_door_one_lock_opening',
      'local_reference_closure',
      'rebuild_save_reopen',
    ]),
  }),
])

export const NATIVE_16029_PENDING_RECIPES = Object.freeze([
  Object.freeze({
    id: 'v38-760w-four-door-l66-r66',
    label: '760宽四门 L66-R66 原生组合配方',
    cabinetWidthMm: 760,
    cabinetHeightMm: FIXED_HEIGHT_MM,
    cabinetDepthMm: 550,
    columns: FIXED_COLUMNS,
    doorCount: 4,
    installedDoorPanelWidthMm: 317,
    referenceDoorHeightMm: 908,
    rowSequence: 'L66-R66',
    sourceSeedIds: Object.freeze([
      'v37-760w-six-door-l642-r246',
      'v36-740w-four-door-l66-r66',
    ]),
    requiredValidation: Object.freeze([
      'native_solidworks_build',
      'one_door_one_lock_opening',
      'shelf_and_crossbar_topology',
      'local_reference_closure',
      'rebuild_save_reopen',
      'relocated_copy_reopen',
    ]),
  }),
])

function text(value) {
  return String(value ?? '').trim()
}

function number(value, fallback = null) {
  if (value === undefined || value === null || text(value) === '') return fallback
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

function positiveInteger(value) {
  const parsed = number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function sameMm(left, right) {
  return Math.abs(Number(left) - Number(right)) < 0.001
}

function rowSequenceForDoorCount(doorCount) {
  if (doorCount === 4) return 'L66-R66'
  if (doorCount === 6) return 'L642-R246'
  const rowsPerColumn = doorCount / FIXED_COLUMNS
  const equalHeightCode = '1'.repeat(rowsPerColumn)
  return `L${equalHeightCode}-R${equalHeightCode}`
}

function referenceDoorHeightForDoorCount(doorCount) {
  if (doorCount === 4 || doorCount === 6) return 908
  return null
}

function estimatedDoorHeightForDoorCount(doorCount) {
  if (doorCount === 4 || doorCount === 6) return 908
  const rowsPerColumn = doorCount / FIXED_COLUMNS
  return roundMm((12 / rowsPerColumn) * 152.5 - 7)
}

function hasUnsupportedHardwareIntent(request) {
  const structuredSources = [
    request.lockType,
    request.latchType,
    request.doorType,
    request.openings,
  ].map((value) => text(value).toLowerCase()).filter(Boolean)
  const promptSource = text(request.prompt).toLowerCase()
  const electricalKeyword = /(?:electric(?:al)?\s+lock|electric(?:al)?\s+board|pcb|control\s+board|电控锁|电器板|控制板|电锁|锁钩)/i
  const explicitExclusion = /(?:(?:no|without|exclude|remove|omit|不要|不含|排除|去除|无).{0,40}(?:electric(?:al)?\s+lock|electric(?:al)?\s+board|pcb|control\s+board|电控锁|电器板|控制板|电锁|锁钩)|(?:electric(?:al)?\s+lock|electric(?:al)?\s+board|pcb|control\s+board|电控锁|电器板|控制板|电锁|锁钩).{0,20}(?:not\s+required|excluded|removed|不需要|不含|排除|去除))/i
  const explicitPositivePrompt = /(?:(?:add|include|restore|with).{0,40}(?:electric(?:al)?\s+lock|electric(?:al)?\s+board|pcb|control\s+board)|(?:增加|包含|恢复|带).{0,24}(?:电控锁|电器板|控制板|电锁|锁钩))/i
  const structuredElectricalRequest = structuredSources.some((source) => (
    electricalKeyword.test(source) && !explicitExclusion.test(source)
  ))
  const promptedElectricalRequest = explicitPositivePrompt.test(promptSource) ||
    (electricalKeyword.test(promptSource) && !explicitExclusion.test(promptSource))
  return structuredElectricalRequest || promptedElectricalRequest
}

function requestGeometry(request) {
  if (request.parametricRequest !== undefined) {
    try {
      const supplied = request.parametricRequest
      if (!supplied || supplied.envelopeProbe) throw new Error('诊断请求不能进入模型生成任务。')
      const canonical = { cabinet: { ...supplied.cabinet }, columns: supplied.columns }
      const contract = build16029ParametricContract(canonical)
      const cabinet = contract.geometry.cabinet
      if (cabinet.widthMm < 700 || cabinet.widthMm > 1200 || cabinet.heightMm < 1700 || cabinet.heightMm > 2200 || cabinet.depthMm < 250 || cabinet.depthMm > 650) throw new Error('当前参数化任务仅开放700–1200宽、1700–2200高、250–650深；每个新规格均需原生验收。')
      const rowSequence = ['L', 'R'].map(side => side + '[' + contract.rowsByColumn[side].map(row => row.doorHeightMm.toFixed(6)).join(',') + ']').join('-')
      return { ok: true, geometry: {
        widthInputMode: 'cabinet_outer_width', requestedWidthMm: cabinet.widthMm,
        cabinetWidthMm: cabinet.widthMm, cabinetHeightMm: cabinet.heightMm, cabinetDepthMm: cabinet.depthMm,
        columns: 2, doorCount: contract.geometry.doorCount, columnDoorCounts: contract.geometry.columnDoorCounts,
        installedDoorPanelWidthMm: contract.geometry.doorLeafWidthMm,
        referenceDoorHeightMm: null, estimatedDoorHeightMm: Math.max(...contract.rowsByColumn.L.concat(contract.rowsByColumn.R).map(row => row.doorHeightMm)),
        rowSequence, parametricRequest: contract.input,
      } }
    } catch (error) { return { ok: false, reason: 'invalid_parametric_request', message: error.message } }
  }
  const widthInputMode = text(request.widthInputMode || 'cabinet_outer_width')
  if (request.cabinetHeight !== undefined && number(request.cabinetHeight) !== FIXED_HEIGHT_MM) return { ok: false, reason: 'parametric_height_request_required', message: '变高度请通过完整参数化请求提交，不能使用固定规格入口。' }
  if (!['cabinet_outer_width', 'installed_door_panel_width'].includes(widthInputMode)) {
    return { ok: false, reason: 'unsupported_width_input_mode', message: '宽度口径只能选择柜体成品外宽或单扇门板成品外宽。' }
  }

  const requestedWidthMm = number(request.requestedWidthMm ?? request.cabinetWidth ?? request.doorWidth)
  const cabinetDepthMm = number(request.cabinetDepth, 550)
  const columns = positiveInteger(request.columns ?? FIXED_COLUMNS)
  const doorCount = positiveInteger(request.doorCount)
  if (requestedWidthMm === null || requestedWidthMm < MIN_REQUESTED_WIDTH_MM || requestedWidthMm > MAX_REQUESTED_WIDTH_MM) {
    return { ok: false, reason: 'invalid_requested_width', message: `目标宽度必须在 ${MIN_REQUESTED_WIDTH_MM}-${MAX_REQUESTED_WIDTH_MM} mm 范围内。` }
  }
  if (cabinetDepthMm === null || cabinetDepthMm < MIN_CABINET_DEPTH_MM || cabinetDepthMm > MAX_CABINET_DEPTH_MM) {
    return { ok: false, reason: 'invalid_cabinet_depth', message: `柜深必须在 ${MIN_CABINET_DEPTH_MM}-${MAX_CABINET_DEPTH_MM} mm 范围内。` }
  }
  if (columns !== FIXED_COLUMNS) {
    return { ok: false, reason: 'unsupported_column_count', message: '当前原生任务只接收两列柜结构。' }
  }
  if (doorCount === null) {
    return { ok: false, reason: 'invalid_door_count', message: '总门数必须是正整数。' }
  }
  if (doorCount < MIN_NATIVE_TASK_DOOR_COUNT || doorCount > MAX_NATIVE_TASK_DOOR_COUNT) {
    return {
      ok: false,
      reason: 'door_count_out_of_range',
      message: `当前两列原生任务接收 ${MIN_NATIVE_TASK_DOOR_COUNT}-${MAX_NATIVE_TASK_DOOR_COUNT} 扇门。`,
    }
  }
  if (doorCount % FIXED_COLUMNS !== 0) {
    return {
      ok: false,
      reason: 'uneven_two_column_door_count',
      message: '两列等门数布局的总门数必须是偶数。',
    }
  }

  const cabinetWidthMm = roundMm(widthInputMode === 'cabinet_outer_width'
    ? requestedWidthMm
    : requestedWidthMm * FIXED_COLUMNS + CURRENT_WIDTH_ALLOWANCE_MM)
  const installedDoorPanelWidthMm = roundMm(widthInputMode === 'installed_door_panel_width'
    ? requestedWidthMm
    : (requestedWidthMm - CURRENT_WIDTH_ALLOWANCE_MM) / FIXED_COLUMNS)
  if (installedDoorPanelWidthMm <= 0) {
    return { ok: false, reason: 'invalid_derived_door_width', message: '按当前两列结构换算后的门板成品外宽无效。' }
  }

  return {
    ok: true,
    geometry: {
      widthInputMode,
      requestedWidthMm: roundMm(requestedWidthMm),
      cabinetWidthMm,
      cabinetHeightMm: FIXED_HEIGHT_MM,
      cabinetDepthMm: roundMm(cabinetDepthMm),
      columns,
      doorCount,
      columnDoorCounts: [doorCount / columns, doorCount / columns],
      installedDoorPanelWidthMm,
      referenceDoorHeightMm: referenceDoorHeightForDoorCount(doorCount),
      estimatedDoorHeightMm: estimatedDoorHeightForDoorCount(doorCount),
      rowSequence: rowSequenceForDoorCount(doorCount),
    },
  }
}

function requestFingerprintForGeometry(customerRequirementReference, geometry) {
  const canonicalRequest = {
    customerRequirementReference,
    cabinetWidthMm: geometry.cabinetWidthMm,
    cabinetHeightMm: geometry.cabinetHeightMm,
    cabinetDepthMm: geometry.cabinetDepthMm,
    columns: geometry.columns,
    doorCount: geometry.doorCount,
    columnDoorCounts: geometry.columnDoorCounts,
    rowSequence: geometry.rowSequence,
    ...(geometry.parametricRequest ? { parametricRequest: geometry.parametricRequest } : {}),
  }
  return createHash('sha256').update(JSON.stringify(canonicalRequest)).digest('hex')
}

function geometryMatches(candidate, expected) {
  return sameMm(candidate.cabinetWidthMm, expected.cabinetWidthMm) &&
    sameMm(candidate.cabinetHeightMm, expected.cabinetHeightMm) &&
    sameMm(candidate.cabinetDepthMm, expected.cabinetDepthMm) &&
    candidate.columns === expected.columns &&
    candidate.doorCount === expected.doorCount &&
    sameMm(candidate.installedDoorPanelWidthMm, expected.installedDoorPanelWidthMm) &&
    candidate.rowSequence === expected.rowSequence
}

export function native16029SeedByAssetId(assetId) {
  return NATIVE_16029_SEEDS.find((seed) => seed.assetId === text(assetId)) || null
}

export function native16029RequestFingerprint(request = {}) {
  const customerRequirementReference = text(
    request.customerRequirementReference || request.customerTaskReference,
  ).slice(0, 200)
  if (!customerRequirementReference) return ''
  const geometryResult = requestGeometry(request)
  if (!geometryResult.ok) return ''
  return requestFingerprintForGeometry(customerRequirementReference, geometryResult.geometry)
}

export function normalize16029NativeModelRequest(request = {}) {
  const customerRequirementReference = text(
    request.customerRequirementReference || request.customerTaskReference,
  ).slice(0, 200)
  if (!customerRequirementReference) {
    return {
      ok: false,
      reason: 'missing_customer_requirement_reference',
      message: '请填写脱敏客户需求代号或任务名称。',
    }
  }
  if (hasUnsupportedHardwareIntent(request)) {
    return {
      ok: false,
      reason: 'unsupported_electrical_hardware_request',
      message: '当前原生模型族只处理机械锁舌结构，不自动加入电器板或电控锁实体。',
    }
  }

  const geometryResult = requestGeometry(request)
  if (!geometryResult.ok) return geometryResult
  const geometry = geometryResult.geometry
  const requestFingerprint = requestFingerprintForGeometry(customerRequirementReference, geometry)
  const seed = NATIVE_16029_SEEDS.find((item) => geometryMatches(geometry, item)) || null
  if (seed) {
    return {
      ok: true,
      status: 'native_assistance_model_ready',
      matchType: 'verified_native_seed',
      seed,
      normalizedRequest: {
        schema: 'winnsen.native_16029_request.v1',
        generatorId: NATIVE_16029_GENERATOR.id,
        purpose: NATIVE_16029_GENERATOR.purpose,
        customerRequirementReference,
        ...geometry,
        cabinetWidth: geometry.cabinetWidthMm,
        cabinetHeight: geometry.cabinetHeightMm,
        cabinetDepth: geometry.cabinetDepthMm,
        doorWidth: geometry.installedDoorPanelWidthMm,
        doorHeight: geometry.referenceDoorHeightMm,
        generationTemplateId: seed.id,
        generationTemplateLabel: seed.label,
        sourceBaselineAssetId: seed.assetId,
        requestFingerprint,
        resultKind: 'solidworks2020_native_structure_assistance_model',
        deliveryMode: 'verified_native_model',
        engineeringAssistanceReady: true,
        engineeringDeepeningRequired: true,
        material: text(request.material || '由结构工程师在项目深化中确定'),
        thickness: text(request.thickness || '由结构工程师在项目深化中确定'),
      },
    }
  }

  const pendingRecipe = NATIVE_16029_PENDING_RECIPES.find((item) => geometryMatches(geometry, item)) || null
  const estimatedDoorPanelWidthMm = geometry.installedDoorPanelWidthMm
  const estimatedDoorHeightMm = geometry.estimatedDoorHeightMm
  const preview = {
    widthInputMode: geometry.widthInputMode,
    requestedWidthMm: geometry.requestedWidthMm,
    cabinetWidthMm: geometry.cabinetWidthMm,
    cabinetHeightMm: geometry.cabinetHeightMm,
    cabinetDepthMm: geometry.cabinetDepthMm,
    columns: geometry.columns,
    doorCount: geometry.doorCount,
    columnDoorCounts: geometry.columnDoorCounts,
    rowSequence: geometry.rowSequence,
    estimatedDoorPanelWidthMm,
    estimatedDoorHeightMm,
    previewDimensionsValidated: false,
  }
  return {
    ok: true,
    status: 'native_task_created',
    matchType: 'native_build_task',
    message: pendingRecipe
      ? `这组参数对应${pendingRecipe.label}，已建立原生新任务；完成原生构建与验证后再提供模型。`
      : `已为 ${geometry.cabinetWidthMm}宽、${geometry.doorCount}门、${geometry.cabinetDepthMm}深建立原生新任务。`,
    preview,
    pendingRecipe,
    normalizedRequest: {
      schema: 'winnsen.native_16029_request.v1',
      generatorId: NATIVE_16029_GENERATOR.id,
      purpose: NATIVE_16029_GENERATOR.purpose,
      customerRequirementReference,
      widthInputMode: geometry.widthInputMode,
      requestedWidthMm: geometry.requestedWidthMm,
      cabinetWidthMm: geometry.cabinetWidthMm,
      cabinetHeightMm: geometry.cabinetHeightMm,
      cabinetDepthMm: geometry.cabinetDepthMm,
      columns: geometry.columns,
      doorCount: geometry.doorCount,
      columnDoorCounts: geometry.columnDoorCounts,
      rowSequence: geometry.rowSequence,
      cabinetWidth: geometry.cabinetWidthMm,
      ...(geometry.parametricRequest ? { parametricRequest: geometry.parametricRequest } : {}),
      cabinetHeight: geometry.cabinetHeightMm,
      cabinetDepth: geometry.cabinetDepthMm,
      estimatedDoorPanelWidthMm,
      estimatedDoorHeightMm,
      doorWidth: estimatedDoorPanelWidthMm,
      doorHeight: estimatedDoorHeightMm,
      previewDimensionsValidated: false,
      requestFingerprint,
      candidateRecipeId: pendingRecipe?.id || '',
      validationRequired: ['one_door_one_lock_opening'],
      resultKind: 'solidworks2020_native_build_task',
      deliveryMode: 'native_task_pending',
      taskType: 'native_solidworks_build_task',
      engineeringAssistanceReady: false,
      engineeringDeepeningRequired: true,
      legacyFallbackUsed: false,
      material: text(request.material || '由结构工程师在项目深化中确定'),
      thickness: text(request.thickness || '由结构工程师在项目深化中确定'),
    },
  }
}

export function is16029StructureAssistanceReady(item = {}) {
  const seed = native16029SeedByAssetId(item.sourceBaselineAssetId)
  if (!seed) return false
  const numericMatch = (value, expected) => Number.isFinite(Number(value)) && Number(value) === expected
  return Boolean(
    item.status === 'native_assistance_model_ready' &&
    item.taskType === 'verified_native_structure_assistance_model' &&
    item.modelReady === true &&
    item.engineeringAssistanceReady === true &&
    item.deliveryMode === 'verified_native_model' &&
    item.verifiedRecipeMatched === true &&
    item.generationTemplateId === seed.id &&
    item.downloadUrl === `/download/${seed.assetId}` &&
    numericMatch(item.cabinetWidth ?? item.cabinetWidthMm, seed.cabinetWidthMm) &&
    numericMatch(item.cabinetHeight ?? item.cabinetHeightMm, seed.cabinetHeightMm) &&
    numericMatch(item.cabinetDepth ?? item.cabinetDepthMm, seed.cabinetDepthMm) &&
    numericMatch(item.columns, seed.columns) &&
    numericMatch(item.doorCount, seed.doorCount) &&
    numericMatch(item.doorWidth ?? item.installedDoorPanelWidthMm, seed.installedDoorPanelWidthMm) &&
    item.rowSequence === seed.rowSequence,
  )
}
