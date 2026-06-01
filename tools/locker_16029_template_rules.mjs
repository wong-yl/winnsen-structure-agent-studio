import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { mkdirSync, writeFileSync } from 'node:fs'
import { GOLD_DOOR_MODULES, GOLD_SOURCE_MODULES } from './locker_16029_gold_source_manifest.mjs'

export const TEMPLATE_RULE_SCHEMA = 'winnsen.locker16029.template_full_assembly_plan.v1'

export const TEMPLATE_RULE = Object.freeze({
  id: '740W_L642_R246_v43_hidden_lock_body_restored_tongue',
  sourceWidthMm: 740,
  sourceHeightMm: 1917,
  sourceDepthMm: 550,
  columns: 2,
  unitHeightMm: 152.5,
  doorGapMm: 7,
  doorBottomMarginMm: 32,
  fixedSideClearanceMm: 126,
  columnCenterInsetMm: 40,
  lockBodyAbsXmm: 55.2,
  lockBodyZmm: -101.5,
  leftUnits: [6, 4, 2],
  rightUnits: [2, 4, 6],
})

function asNumber(value, fallback) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function asInteger(value, fallback) {
  const parsed = Math.round(asNumber(value, fallback))
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function almostEqual(a, b, tolerance = 0.001) {
  return Math.abs(Number(a) - Number(b)) <= tolerance
}

function arraysAlmostEqual(a, b, tolerance = 0.001) {
  return a.length === b.length && a.every((value, index) => almostEqual(value, b[index], tolerance))
}

function roundMm(value) {
  return Math.round(value * 1000) / 1000
}

function compactNumber(value) {
  return Number(value).toFixed(3).replace(/\.?0+$/, '')
}

function slugNumber(value) {
  return compactNumber(value).replace('.', 'p')
}

function codeForUnits(units) {
  return units.map((unit) => compactNumber(unit).replace('.', 'p')).join('-')
}

function goldDoorModulesForUnit(unit) {
  const token = `${compactNumber(unit)}_12`
  return GOLD_DOOR_MODULES
    .filter((module) => module.key.includes(token))
    .map((module) => ({
      key: module.key,
      role: module.role,
      file: module.file,
      requiredChildren: module.requiredChildren,
    }))
}

function doorUnitsForPlan(columnsPlan) {
  return [...new Set(columnsPlan.flatMap((column) => column.units.map((unit) => compactNumber(unit))))]
}

function doorModuleBindingForPlan(columnsPlan) {
  const requestedUnits = doorUnitsForPlan(columnsPlan)
  const bindings = requestedUnits.map((unit) => {
    const modules = goldDoorModulesForUnit(Number(unit))
    return {
      unit,
      moduleCount: modules.length,
      status: modules.length ? 'ready_from_template_modules' : 'needs_native_door_generation',
    }
  })
  const missingUnits = bindings.filter((binding) => binding.moduleCount === 0).map((binding) => binding.unit)
  return {
    status: missingUnits.length ? 'needs_native_door_generation' : 'ready_from_template_modules',
    requestedUnits,
    availableUnits: bindings.filter((binding) => binding.moduleCount > 0).map((binding) => binding.unit),
    missingUnits,
    availableDoorModuleCount: bindings.reduce((total, binding) => total + binding.moduleCount, 0),
    missingDoorModuleCount: missingUnits.length,
    bindings,
  }
}

function targetAssemblyForPlan(columnsPlan) {
  const doorUnits = doorUnitsForPlan(columnsPlan)
  return {
    source: 'gold_source_manifest',
    cabinetBodyModules: GOLD_SOURCE_MODULES.map((module) => ({
      key: module.key,
      role: module.role,
      file: module.file,
      requiredChildren: module.requiredChildren,
      hiddenReferenceChildren: module.hiddenReferenceChildren || [],
    })),
    doorModulesByUnit: Object.fromEntries(doorUnits.map((unit) => [unit, goldDoorModulesForUnit(Number(unit))])),
    engineerVisibleNaming: 'Use Chinese SolidWorks component names from the gold source for engineer-facing assemblies; keep ASCII only for internal automation ids.',
    currentTemplateKnownGaps: [
      'cabinet body is still a top-level multi-body SLDPRT until the modular SolidWorks rebuild pass replaces it',
      'right-side door panels still expose imported/mirrored solid-body names until the door sheet-metal normalization pass replaces them',
    ],
  }
}

function parseCompactSideCode(text) {
  const source = String(text || '')
  const match = source.match(/\bL\s*([0-9]+(?:[.-][0-9]+)*)\s*[-_/]\s*R\s*([0-9]+(?:[.-][0-9]+)*)\b/i)
  if (!match) return null
  return [digitsToUnits(match[1]), digitsToUnits(match[2])]
}

function digitsToUnits(value) {
  const text = String(value || '').replace(/[^0-9.]/g, '')
  if (!text) return []
  if (text.includes('.')) return text.split('.').map(Number).filter((item) => Number.isFinite(item) && item > 0)
  return Array.from(text, (char) => Number(char)).filter((item) => Number.isFinite(item) && item > 0)
}

function extractBracketedSequence(text, labelPattern) {
  const source = String(text || '')
  const bracketed = new RegExp(`(?:${labelPattern})\\s*(?:column)?\\s*[:=]?\\s*[\\[(]\\s*([^\\])]+)\\s*[\\])]`, 'i')
  const bracketMatch = source.match(bracketed)
  if (bracketMatch?.[1]) return bracketMatch[1]

  const inline = new RegExp(`(?:${labelPattern})\\s*(?:column)?\\s*[:=]?\\s*((?:[0-9]+(?:\\.[0-9]+)?\\s*(?:/\\s*12)?\\s*[,;\\s-]+){1,8}[0-9]+(?:\\.[0-9]+)?\\s*(?:/\\s*12)?)`, 'i')
  const inlineMatch = source.match(inline)
  return inlineMatch?.[1] ?? ''
}

function parseUnitList(raw, allowPlainNumbers = true) {
  const source = String(raw || '')
  const fractionMatches = Array.from(source.matchAll(/([0-9]+(?:\.[0-9]+)?)\s*\/\s*12/g), (match) => Number(match[1]))
    .filter((value) => Number.isFinite(value) && value > 0)
  if (fractionMatches.length) return fractionMatches

  if (!allowPlainNumbers) return []
  const plainMatches = Array.from(source.matchAll(/(?<![0-9.])([0-9]+(?:\.[0-9]+)?)(?![0-9.])/g), (match) => Number(match[1]))
    .filter((value) => Number.isFinite(value) && value > 0 && value <= 12)
  return plainMatches
}

function parseSharedUnits(sequenceText, promptText) {
  const sequence = String(sequenceText || '').trim()
  const prompt = String(promptText || '').trim()
  const combined = `${sequence} ${prompt}`

  if (/\bLMS\b/i.test(combined)) return [6, 4, 2]
  if (/\bSML\b/i.test(combined)) return [2, 4, 6]

  if (sequence && !/^equal\s*rows?$/i.test(sequence) && !/^single\s*panel$/i.test(sequence)) {
    const units = parseUnitList(sequence, true)
    if (units.length) return units
  }

  const rowUnits = prompt.match(/row\s*units?\s*[:=]?\s*([^.;\n]+)/i)
  if (rowUnits?.[1]) {
    const units = parseUnitList(rowUnits[1], true)
    if (units.length) return units
  }

  const fractions = parseUnitList(prompt, false)
  return fractions.length ? fractions : []
}

function parseColumnUnits(sequenceText, promptText, columns) {
  const texts = [sequenceText, promptText].map((value) => String(value || '').trim()).filter(Boolean)
  for (const text of texts) {
    const compact = parseCompactSideCode(text)
    if (compact && compact.every((items) => items.length)) return compact.slice(0, columns)

    const leftRaw = extractBracketedSequence(text, 'left|\\bL\\b')
    const rightRaw = extractBracketedSequence(text, 'right|\\bR\\b')
    if (leftRaw || rightRaw) {
      const left = parseUnitList(leftRaw, true)
      const right = parseUnitList(rightRaw, true)
      if (left.length && right.length) return [left, right].slice(0, columns)
    }
  }

  const shared = parseSharedUnits(sequenceText, promptText)
  return shared.length ? Array.from({ length: columns }, () => [...shared]) : []
}

function normalizeColumnUnits(columnUnits, warnings) {
  return columnUnits.map((units, index) => {
    const sum = units.reduce((total, value) => total + value, 0)
    if (sum <= 0) throw new Error(`column ${index + 1} has no positive door-height ratio units`)
    if (almostEqual(sum, 12, 0.01)) return units.map(roundMm)
    warnings.push(`column ${index + 1} height ratios sum to ${compactNumber(sum)}; normalized to a 12-unit template height.`)
    return units.map((value) => roundMm((value / sum) * 12))
  })
}

function equalUnitsForDoorCount(doorCount, columns) {
  const rowsPerColumn = Math.max(1, Math.ceil(doorCount / columns))
  const unit = 12 / rowsPerColumn
  return Array.from({ length: columns }, () => Array.from({ length: rowsPerColumn }, () => roundMm(unit)))
}

function rowsForColumn(units, side, xMm) {
  let bottom = TEMPLATE_RULE.doorBottomMarginMm
  return units.map((unit, index) => {
    const heightMm = roundMm(unit * TEMPLATE_RULE.unitHeightMm - TEMPLATE_RULE.doorGapMm)
    if (heightMm <= 50) {
      throw new Error(`door row ${side}${index + 1} is too short after ratio normalization: ${heightMm}mm`)
    }
    const topMm = roundMm(bottom + heightMm)
    const centerYmm = roundMm(bottom + heightMm / 2)
    const row = {
      index: index + 1,
      side,
      unit,
      label: `${compactNumber(unit)}/12`,
      heightMm,
      centerXmm: xMm,
      centerYmm,
      bottomYmm: roundMm(bottom),
      topYmm: topMm,
    }
    bottom = topMm + TEMPLATE_RULE.doorGapMm
    return row
  })
}

export function planTemplateFullAssemblyRequest(input = {}) {
  const warnings = []
  const cabinetWidthMm = asNumber(input.cabinetWidthMm ?? input.cabinetWidth, TEMPLATE_RULE.sourceWidthMm)
  const cabinetHeightMm = asNumber(input.cabinetHeightMm ?? input.cabinetHeight, TEMPLATE_RULE.sourceHeightMm)
  const cabinetDepthMm = asNumber(input.cabinetDepthMm ?? input.cabinetDepth, TEMPLATE_RULE.sourceDepthMm)
  const columns = asInteger(input.columns, TEMPLATE_RULE.columns)
  if (columns !== TEMPLATE_RULE.columns) {
    throw new Error(`template rule ${TEMPLATE_RULE.id} currently supports exactly ${TEMPLATE_RULE.columns} columns; requested ${columns}`)
  }

  const requestedDoorCount = asInteger(input.doorCount, 0)
  const parsedColumnUnits = parseColumnUnits(input.rowSequence, input.prompt, columns)
  const rawColumnUnits = parsedColumnUnits.length
    ? parsedColumnUnits
    : equalUnitsForDoorCount(requestedDoorCount || 6, columns)
  const columnUnits = normalizeColumnUnits(rawColumnUnits, warnings)
  const derivedDoorCount = columnUnits.reduce((total, units) => total + units.length, 0)
  if (requestedDoorCount && requestedDoorCount !== derivedDoorCount) {
    throw new Error(`doorCount ${requestedDoorCount} does not match row sequences (${derivedDoorCount} doors)`)
  }

  const doorWidthMm = roundMm(asNumber(input.doorWidthMm ?? input.doorWidth, (cabinetWidthMm - TEMPLATE_RULE.fixedSideClearanceMm) / columns))
  const columnCenterAbsXmm = roundMm(doorWidthMm / 2 + TEMPLATE_RULE.columnCenterInsetMm)
  const columnsPlan = [
    { side: 'L', units: columnUnits[0], xMm: -columnCenterAbsXmm },
    { side: 'R', units: columnUnits[1], xMm: columnCenterAbsXmm },
  ].map((column) => ({
    side: column.side,
    units: column.units,
    rows: rowsForColumn(column.units, column.side, column.xMm),
  }))

  const placements = [
    { role: 'gold_source_shell_frame_shelf', txMm: 0, tyMm: 0, tzMm: 0, source: 'template_shell_frame_shelf' },
    { role: 'gold_electronics_module', txMm: 0, tyMm: 0, tzMm: 0, source: 'template_electronics_module' },
  ]
  for (const column of columnsPlan) {
    for (const row of column.rows) {
      placements.push({
        role: `${row.side}_row${String(row.index).padStart(2, '0')}_${codeForUnits([row.unit])}_door_module`,
        txMm: row.centerXmm,
        tyMm: row.centerYmm,
        tzMm: 0,
        source: `template_${row.side}_door_module_by_height_ratio`,
      })
      placements.push({
        role: `cabinet_lock_body_${row.side}_row${String(row.index).padStart(2, '0')}`,
        txMm: row.side === 'L' ? -TEMPLATE_RULE.lockBodyAbsXmm : TEMPLATE_RULE.lockBodyAbsXmm,
        tyMm: row.centerYmm,
        tzMm: TEMPLATE_RULE.lockBodyZmm,
        source: 'template_electric_lock_body',
      })
    }
  }

  const compatibleWithNativeTemplate = (
    almostEqual(cabinetWidthMm, TEMPLATE_RULE.sourceWidthMm, 0.5) &&
    almostEqual(cabinetHeightMm, TEMPLATE_RULE.sourceHeightMm, 0.5) &&
    almostEqual(cabinetDepthMm, TEMPLATE_RULE.sourceDepthMm, 0.5) &&
    arraysAlmostEqual(columnUnits[0], TEMPLATE_RULE.leftUnits, 0.001) &&
    arraysAlmostEqual(columnUnits[1], TEMPLATE_RULE.rightUnits, 0.001)
  )
  const doorModuleBinding = doorModuleBindingForPlan(columnsPlan)
  if (doorModuleBinding.missingUnits.length) {
    warnings.push(`door height ratios ${doorModuleBinding.missingUnits.map((unit) => `${unit}/12`).join(', ')} need generated native SolidWorks door modules before engineer-ready Pack-and-Go.`)
  }

  const outputToken = [
    `${slugNumber(cabinetWidthMm)}W`,
    `L${codeForUnits(columnUnits[0])}`,
    `R${codeForUnits(columnUnits[1])}`,
  ].join('_')

  return {
    schema: TEMPLATE_RULE_SCHEMA,
    status: 'ready',
    cadMainline: 'SolidWorks 2020',
    templateRuleVersion: TEMPLATE_RULE.id,
    route: {
      mode: compatibleWithNativeTemplate ? 'native_template_pack_and_go' : 'template_rule_parameter_plan',
      directAssemblyInsertion: false,
      sourceTemplate: 'verified 740W / L642-R246 / v43 full assembly',
      freeCadRole: 'internal evidence and parameter assistance only',
    },
    requested: {
      cabinetWidthMm,
      cabinetHeightMm,
      cabinetDepthMm,
      columns,
      doorCount: derivedDoorCount,
      doorWidthMm,
      rowSequence: String(input.rowSequence || ''),
      prompt: String(input.prompt || ''),
    },
    derived: {
      doorCount: derivedDoorCount,
      doorWidthMm,
      columnCenterAbsXmm,
      doorGapMm: TEMPLATE_RULE.doorGapMm,
      unitHeightMm: TEMPLATE_RULE.unitHeightMm,
      lockBodyAbsXmm: TEMPLATE_RULE.lockBodyAbsXmm,
      lockBodyZmm: TEMPLATE_RULE.lockBodyZmm,
      outputToken,
      compatibleWithNativeTemplate,
      doorModuleBinding,
    },
    columns: columnsPlan,
    targetAssembly: targetAssemblyForPlan(columnsPlan),
    placements,
    warnings,
    boundary: compatibleWithNativeTemplate
      ? 'Exact verified native template; Pack-and-Go output is the SolidWorks 2020 assembly package.'
      : 'Template-rule package; derived dimensions and placement plan are included for review, while native geometry mutation still requires the next SolidWorks rule-binding pass.',
  }
}

function parseArgs(argv) {
  const options = {}
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    const readValue = () => argv[++index] ?? ''
    if (arg === '--out') options.out = readValue()
    else if (arg === '--cabinet-width') options.cabinetWidthMm = readValue()
    else if (arg === '--cabinet-height') options.cabinetHeightMm = readValue()
    else if (arg === '--cabinet-depth') options.cabinetDepthMm = readValue()
    else if (arg === '--columns') options.columns = readValue()
    else if (arg === '--door-count') options.doorCount = readValue()
    else if (arg === '--door-width') options.doorWidthMm = readValue()
    else if (arg === '--door-height') options.doorHeightMm = readValue()
    else if (arg === '--row-sequence') options.rowSequence = readValue()
    else if (arg === '--prompt') options.prompt = readValue()
    else if (arg.startsWith('--')) options[arg.slice(2)] = readValue()
  }
  return options
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  const plan = planTemplateFullAssemblyRequest(options)
  const text = `${JSON.stringify(plan, null, 2)}\n`
  if (options.out) {
    const outPath = resolve(options.out)
    mkdirSync(dirname(outPath), { recursive: true })
    writeFileSync(outPath, text, 'utf8')
    console.log(outPath)
  } else {
    process.stdout.write(text)
  }
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (entryFile && currentFile === entryFile) {
  main()
}
