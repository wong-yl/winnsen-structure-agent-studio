const FULL_ASSEMBLY_COMBOS = [
  {
    key: '740w-v43-l642-r246',
    width: 740,
    height: 1917,
    depth: 550,
    columns: 2,
    doorCount: 6,
    rowSequences: ['L642-R246'],
    releaseLevel: 'current_v43_template_seed',
    message: '740W / L642-R246 / v43 route is the frozen verified door/template seed.',
  },
  {
    key: '950w-400d-14-door-derived-review',
    width: 950,
    height: 1917,
    depth: 400,
    columns: 2,
    doorCount: 14,
    rowSequences: ['', 'L1111111-R1111111'],
    releaseLevel: 'derived_sheetmetal_review',
    message: '950W / 400D / 14-door is allowed only as a controlled gold-rule derived sheet-metal review model.',
  },
  {
    key: '1000w-gold-10-door-reference',
    width: 1000,
    height: 1917,
    depth: 550,
    columns: 2,
    doorCount: 10,
    rowSequences: ['', 'L11111-R11111'],
    releaseLevel: 'gold_source_reference',
    message: '1000W / 10-door stays a gold/source reference family, not a random generation template.',
  },
  {
    key: '1000w-gold-12-door-reference',
    width: 1000,
    height: 1917,
    depth: 550,
    columns: 2,
    doorCount: 12,
    rowSequences: ['', 'L111111-R111111'],
    releaseLevel: 'gold_source_reference',
    message: '1000W / 12-door stays a gold/source reference family, not a random generation template.',
  },
  {
    key: '1000w-gold-14-door-reference',
    width: 1000,
    height: 1917,
    depth: 550,
    columns: 2,
    doorCount: 14,
    rowSequences: ['', 'L1111111-R1111111'],
    releaseLevel: 'gold_source_reference',
    message: '1000W / 14-door stays a gold/source reference family, not a random generation template.',
  },
]

const FULL_ASSEMBLY_RULES = [
  {
    key: 'v43-width-derived-l642-r246',
    minWidth: 700,
    maxWidth: 780,
    height: 1917,
    depths: [550],
    columns: 2,
    doorCounts: [6],
    rowSequences: ['', 'L642-R246'],
    releaseLevel: 'v43_width_derived_sheetmetal_review',
    message: 'V43 L642-R246 width-derived sheet-metal review is allowed for 700-780W / 550D / 6-door requests.',
  },
  {
    key: 'gold-equal-row-derived-10-12-14',
    minWidth: 740,
    maxWidth: 1100,
    height: 1917,
    minDepth: 350,
    maxDepth: 600,
    columns: 2,
    doorCounts: [10, 12, 14],
    rowSequences: ['', 'L11111-R11111', 'L111111-R111111', 'L1111111-R1111111'],
    releaseLevel: 'gold_rule_derived_sheetmetal_review',
    message: 'Gold-rule equal-row derived sheet-metal review is allowed for 740-1100W / 350-600D / 10-12-14 door requests.',
  },
]

const SINGLE_DOOR_POLICY = {
  key: 'ordinary-single-door-panel',
  minDoorWidth: 120,
  maxDoorWidth: 600,
  minDoorHeight: 180,
  maxDoorHeight: 1200,
  releaseLevel: 'single_door_sheetmetal_review',
}

const NEGATIVE_HARDWARE_WORDS = [
  'no',
  'without',
  'exclude',
  'excluded',
  'excluding',
  'do not include',
  'not include',
  'none',
  'remove',
  'removed',
  '不要',
  '不需要',
  '不生成',
  '不包含',
  '排除',
  '去除',
  '无',
]

const BANNED_HARDWARE_TERMS = [
  'electrical board',
  'electric board',
  'control board',
  'lock-control board',
  'lock control board',
  'cabinet-side electric lock',
  'electric lock body',
  'electric lock hook',
  'electric-lock body',
  'electric-lock hook',
  'pcb',
  '电器板',
  '电控锁',
  '电锁',
  '锁钩',
  '控制板',
  '主板',
]

function text(value) {
  return String(value ?? '').trim()
}

function asPositiveNumber(value, fallback = 0) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function asPositiveInteger(value, fallback = 0) {
  const parsed = Math.round(asPositiveNumber(value, fallback))
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function normalizeRowSequence(value) {
  return text(value).toUpperCase().replace(/\s+/g, '')
}

function taskModeFor(request) {
  return text(request.taskMode || request.task_mode || request.mode).toLowerCase() === 'single_model'
    ? 'single_model'
    : 'full_assembly'
}

function hardwarePhraseIsNegative(source, termIndex) {
  const before = source.slice(Math.max(0, termIndex - 56), termIndex).toLowerCase()
  return NEGATIVE_HARDWARE_WORDS.some((word) => before.includes(word.toLowerCase()))
}

function hasPositiveBannedHardwareText(value) {
  const source = text(value)
  if (!source) return false
  const lower = source.toLowerCase()
  for (const term of BANNED_HARDWARE_TERMS) {
    const lowerTerm = term.toLowerCase()
    let fromIndex = 0
    while (true) {
      const index = lower.indexOf(lowerTerm, fromIndex)
      if (index < 0) break
      if (!hardwarePhraseIsNegative(source, index)) return true
      fromIndex = index + lowerTerm.length
    }
  }
  return false
}

function hasExplicitBannedHardwareExclusion(value) {
  const source = text(value)
  if (!source) return false
  const lower = source.toLowerCase()
  return BANNED_HARDWARE_TERMS.some((term) => {
    const lowerTerm = term.toLowerCase()
    let fromIndex = 0
    while (true) {
      const index = lower.indexOf(lowerTerm, fromIndex)
      if (index < 0) return false
      if (hardwarePhraseIsNegative(source, index)) return true
      fromIndex = index + lowerTerm.length
    }
  })
}

function hasPositiveBannedHardwareIntent(request) {
  const prompt = text(request.prompt)
  if (hasPositiveBannedHardwareText(prompt)) return true
  const promptExcludesBannedHardware = hasExplicitBannedHardwareExclusion(prompt)
  const fieldValues = promptExcludesBannedHardware
    ? []
    : [
        request.lockType,
        request.latchType,
        request.doorType,
        request.openings,
        request.reinforcement,
        request.material,
      ].map(text).filter(Boolean)
  for (const value of fieldValues) {
    if (hasPositiveBannedHardwareText(value)) return true
  }
  return false
}

export function wantsElectricalLockHardwareRestore(request = {}) {
  const source = [
    request.prompt,
    request.lockType,
    request.latchType,
    request.doorType,
  ].map(text).filter(Boolean).join(' ')
  if (!source) return false
  if (hasExplicitBannedHardwareExclusion(source)) return false
  const lower = source.toLowerCase()
  const hasHardwareTerm = /\belectrical\s+lock\s+hardware\b|\belectric[-\s]?lock\s+hardware\b|\belectric\s+lock\s+(?:body|hook)\b|\belectric-lock\s+(?:body|hook)\b/.test(lower) ||
    /电控锁|电锁|电控U型锁钩|电控硬件|电控锁钩/.test(source)
  if (!hasHardwareTerm) return false
  return /\b(?:include|with|restore|restored|add|keep)\b.{0,48}\b(?:electrical|electric[-\s]?lock|electric\s+lock)\b/.test(lower) ||
    /\b(?:electrical|electric[-\s]?lock|electric\s+lock)\b.{0,48}\b(?:include|with|restore|restored|add|keep)\b/.test(lower) ||
    /(?:包含|带|恢复|加上|加入|添加|保留).{0,24}(?:电控锁|电锁|电控U型锁钩|电控硬件|电控锁钩)/.test(source) ||
    /(?:电控锁|电锁|电控U型锁钩|电控硬件|电控锁钩).{0,24}(?:包含|带|恢复|加上|加入|添加|保留)/.test(source)
}

function normalizedFullAssemblyRequest(request) {
  const width = asPositiveInteger(request.cabinetWidth ?? request.cabinet_width ?? request.width)
  const height = asPositiveInteger(request.cabinetHeight ?? request.cabinet_height ?? request.height, 1917)
  const depth = asPositiveInteger(request.cabinetDepth ?? request.cabinet_depth ?? request.depth, 550)
  const columns = asPositiveInteger(request.columns, 2)
  const doorCount = asPositiveInteger(request.doorCount ?? request.door_count, 0)
  const rowSequence = normalizeRowSequence(request.rowSequence ?? request.row_sequence)
  return { width, height, depth, columns, doorCount, rowSequence }
}

function matchesCombo(normalized, combo) {
  return normalized.width === combo.width &&
    normalized.height === combo.height &&
    normalized.depth === combo.depth &&
    normalized.columns === combo.columns &&
    normalized.doorCount === combo.doorCount &&
    combo.rowSequences.includes(normalized.rowSequence)
}

function matchesRule(normalized, rule) {
  const depthMatches = Array.isArray(rule.depths)
    ? rule.depths.includes(normalized.depth)
    : normalized.depth >= rule.minDepth && normalized.depth <= rule.maxDepth
  return normalized.width >= rule.minWidth &&
    normalized.width <= rule.maxWidth &&
    normalized.height === rule.height &&
    depthMatches &&
    normalized.columns === rule.columns &&
    rule.doorCounts.includes(normalized.doorCount) &&
    rule.rowSequences.includes(normalized.rowSequence)
}

function validateFullAssemblyRequest(request) {
  const normalized = normalizedFullAssemblyRequest(request)
  const combo = FULL_ASSEMBLY_COMBOS.find((item) => matchesCombo(normalized, item))
  if (combo) {
    return {
      ok: true,
      status: 'controlled_generation_allowed',
      reason: 'allowed_full_assembly_combo',
      policyKey: combo.key,
      releaseLevel: combo.releaseLevel,
      message: combo.message,
      normalized,
    }
  }
  const rule = FULL_ASSEMBLY_RULES.find((item) => matchesRule(normalized, item))
  if (!rule) {
    return {
      ok: false,
      status: 'blocked_controlled_generation_policy',
      reason: 'unsupported_full_assembly_combo',
      message: 'Full assembly generation is rule-controlled: use v43 L642-R246 700-780W/550D/6-door, or gold-rule equal-row 740-1100W/350-600D/10-12-14-door requests.',
      normalized,
    }
  }
  return {
    ok: true,
    status: 'controlled_generation_allowed',
    reason: 'allowed_full_assembly_rule',
    policyKey: rule.key,
    releaseLevel: rule.releaseLevel,
    message: rule.message,
    normalized,
  }
}

function validateSingleDoorRequest(request) {
  const doorWidth = asPositiveInteger(request.doorWidth ?? request.door_width ?? request.cabinetWidth ?? request.width)
  const doorHeight = asPositiveInteger(request.doorHeight ?? request.door_height ?? request.cabinetHeight ?? request.height, 451)
  const doorType = text(request.doorType || request.door_type || 'ordinary_door_panel').toLowerCase()
  const ordinaryDoor = !doorType || doorType.includes('ordinary') || doorType.includes('storage') || doorType.includes('door')
  const inRange = doorWidth >= SINGLE_DOOR_POLICY.minDoorWidth &&
    doorWidth <= SINGLE_DOOR_POLICY.maxDoorWidth &&
    doorHeight >= SINGLE_DOOR_POLICY.minDoorHeight &&
    doorHeight <= SINGLE_DOOR_POLICY.maxDoorHeight
  if (!ordinaryDoor || !inRange) {
    return {
      ok: false,
      status: 'blocked_controlled_generation_policy',
      reason: 'unsupported_single_door',
      message: `Single-door generation is controlled: ordinary sheet-metal door panels only, ${SINGLE_DOOR_POLICY.minDoorWidth}-${SINGLE_DOOR_POLICY.maxDoorWidth}W and ${SINGLE_DOOR_POLICY.minDoorHeight}-${SINGLE_DOOR_POLICY.maxDoorHeight}H.`,
      normalized: { doorWidth, doorHeight, doorType },
    }
  }
  return {
    ok: true,
    status: 'controlled_generation_allowed',
    reason: 'allowed_single_door',
    policyKey: SINGLE_DOOR_POLICY.key,
    releaseLevel: SINGLE_DOOR_POLICY.releaseLevel,
    message: 'Ordinary single-door sheet-metal panel generation is allowed for review.',
    normalized: { doorWidth, doorHeight, doorType },
  }
}

export function validateControlled16029GenerationRequest(request = {}) {
  const allowElectricalLockHardware = wantsElectricalLockHardwareRestore(request)
  if (hasPositiveBannedHardwareIntent(request) && !allowElectricalLockHardware) {
    return {
      ok: false,
      status: 'blocked_controlled_generation_policy',
      reason: 'electrical_or_electric_lock_hardware_requested',
      message: 'Generation is blocked because electrical boards, cabinet-side electric locks, or electric-lock hooks were requested. Keep only holes, datums, lock tongue, and mounting interfaces.',
      normalized: {},
    }
  }

  return taskModeFor(request) === 'single_model'
    ? validateSingleDoorRequest(request)
    : {
        ...validateFullAssemblyRequest(request),
        allowElectricalLockHardware,
      }
}

export function controlled16029DownloadBlockers(item = {}) {
  const blockers = []
  const visibleCabinetBodyBoxScaffoldCount = Number(item.visibleCabinetBodyBoxScaffoldCount || 0)
  const generatedDoorPanelBoxEnvelopeCount = Number(item.generatedDoorPanelBoxEnvelopeCount || 0)
  const electricalOrElectricLockComponentCount = Number(item.electricalOrElectricLockComponentCount || 0)
  const allowElectricalLockHardware = item.allowElectricalLockHardware === true ||
    item.includeElectricalLockHardware === true ||
    item.controlledGenerationPolicy?.allowElectricalLockHardware === true
  if (visibleCabinetBodyBoxScaffoldCount > 0) {
    blockers.push({
      code: 'visible_cabinet_body_box_scaffold',
      count: visibleCabinetBodyBoxScaffoldCount,
      message: `visible cabinet body box scaffold count=${visibleCabinetBodyBoxScaffoldCount}`,
    })
  }
  if (generatedDoorPanelBoxEnvelopeCount > 0) {
    blockers.push({
      code: 'generated_door_panel_box_envelope',
      count: generatedDoorPanelBoxEnvelopeCount,
      message: `generated door panel box envelope count=${generatedDoorPanelBoxEnvelopeCount}`,
    })
  }
  if (electricalOrElectricLockComponentCount > 0 && !allowElectricalLockHardware) {
    blockers.push({
      code: 'electrical_or_electric_lock_component',
      count: electricalOrElectricLockComponentCount,
      message: `electrical/electric-lock component count=${electricalOrElectricLockComponentCount}`,
    })
  }
  return blockers
}

export function isDownloadBlockedByControlled16029Gate(item = {}) {
  return controlled16029DownloadBlockers(item).length > 0
}

export function controlled16029ReleaseState(item = {}) {
  if (isDownloadBlockedByControlled16029Gate(item)) return 'blocked_box_regression'
  const policy = item.controlledGenerationPolicy || {}
  const status = text(item.status).toLowerCase()
  const resultKind = text(item.resultKind).toLowerCase()
  if (policy.releaseLevel === 'current_v43_template_seed' && item.downloadUrl) return 'current_delivery'
  if (item.derivedSheetMetalModelReadyForReview === true || status.includes('derived_sheetmetal_review')) {
    return 'derived_sheetmetal_review'
  }
  if (
    status.includes('needs_structure_revision') ||
    status.includes('parametric_scaffold') ||
    resultKind.includes('structure_revision_evidence')
  ) {
    return 'structure_revision_evidence'
  }
  if (resultKind.includes('cad_worker_payload')) return 'non_delivery_evidence'
  return policy.releaseLevel || 'non_delivery_evidence'
}

export {
  FULL_ASSEMBLY_COMBOS as CONTROLLED_16029_FULL_ASSEMBLY_COMBOS,
  FULL_ASSEMBLY_RULES as CONTROLLED_16029_FULL_ASSEMBLY_RULES,
  SINGLE_DOOR_POLICY,
}
