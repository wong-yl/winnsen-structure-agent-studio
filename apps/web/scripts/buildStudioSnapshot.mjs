import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const appRoot = path.resolve(__dirname, '..')
const repoRoot = path.resolve(appRoot, '..', '..')
const cadWorkspace = 'D:\\机械结构工程师智能体'

const sources = {
  intakeStatus: path.join(cadWorkspace, 'outputs\\intake\\intake_pipeline_status_v1.md'),
  outdoorStatus: path.join(cadWorkspace, 'outputs\\intake\\outdoor_courier_family_intake_status.md'),
  strictQueueCsv: path.join(cadWorkspace, 'outputs\\freecad\\locker_16029_strict_geometry_upgrade_queue.csv'),
  strictQueueMd: path.join(cadWorkspace, 'outputs\\freecad\\locker_16029_strict_geometry_upgrade_queue.md'),
  outdoorGateCsv: path.join(cadWorkspace, 'outputs\\freecad\\outdoor_courier_production_gate_queue.csv'),
  outdoorGateMd: path.join(cadWorkspace, 'outputs\\freecad\\outdoor_courier_production_gate_queue.md'),
  outdoorSingleDoorAudit: path.join(
    cadWorkspace,
    'outputs\\freecad\\outdoor_courier_single_door\\outdoor_courier_waterproof_door_1_12_R_single_panel_v1_source_signature_audit.md',
  ),
}

function numberAfter(text, label) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = text.match(new RegExp(`${escaped}:\\s*` + '`?([0-9,]+)', 'u'))
  return match ? Number(match[1].replaceAll(',', '')) : 0
}

function ratioAfter(text, label) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = text.match(new RegExp(`${escaped}:\\s*` + '`?([0-9,]+)\\s*/\\s*([0-9,]+)', 'u'))
  return match
    ? { done: Number(match[1].replaceAll(',', '')), total: Number(match[2].replaceAll(',', '')) }
    : { done: 0, total: 0 }
}

function firstCodeAfter(text, label) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = text.match(new RegExp(`${escaped}:\\s*` + '`([^`]+)`', 'u'))
  return match?.[1] ?? ''
}

function tableNumber(text, label) {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = text.match(new RegExp(`\\|\\s*${escaped}\\s*\\|\\s*([0-9,]+)\\s*\\|`, 'u'))
  return match ? Number(match[1].replaceAll(',', '')) : 0
}

function parseCsvLine(line) {
  const cells = []
  let cell = ''
  let quoted = false

  for (let index = 0; index < line.length; index += 1) {
    const char = line[index]
    const next = line[index + 1]
    if (char === '"' && quoted && next === '"') {
      cell += '"'
      index += 1
    } else if (char === '"') {
      quoted = !quoted
    } else if (char === ',' && !quoted) {
      cells.push(cell)
      cell = ''
    } else {
      cell += char
    }
  }

  cells.push(cell)
  return cells
}

function parseCsv(text) {
  const lines = text.replace(/^\uFEFF/u, '').trim().split(/\r?\n/u)
  if (lines.length < 2) return []
  const headers = parseCsvLine(lines[0])
  return lines.slice(1).map((line) => {
    const values = parseCsvLine(line)
    return Object.fromEntries(headers.map((header, index) => [header, values[index] ?? '']))
  })
}

function countBy(rows, field) {
  return rows.reduce((acc, row) => {
    const value = row[field] || 'unknown'
    acc[value] = (acc[value] ?? 0) + 1
    return acc
  }, {})
}

async function readText(filePath) {
  return fs.readFile(filePath, 'utf8')
}

async function sourceInfo(filePath) {
  const stat = await fs.stat(filePath)
  return {
    path: filePath,
    updatedAt: stat.mtime.toISOString(),
    bytes: stat.size,
  }
}

const [intakeStatus, outdoorStatus, strictRowsText, outdoorRowsText, outdoorSingleDoorAudit] = await Promise.all([
  readText(sources.intakeStatus),
  readText(sources.outdoorStatus),
  readText(sources.strictQueueCsv),
  readText(sources.outdoorGateCsv),
  readText(sources.outdoorSingleDoorAudit),
])

const strictRows = parseCsv(strictRowsText)
const outdoorGateRows = parseCsv(outdoorRowsText)
const outdoorDxfParsed = ratioAfter(outdoorStatus, 'all `283/283` outdoor DXF files parsed successfully')

const lockerDxfParsed = ratioAfter(intakeStatus, '- DXF files parsed')
const lockerBomMapped = ratioAfter(intakeStatus, '- BOM rows mapped')
const outdoorReleaseCounts = countBy(outdoorGateRows, 'release_status')
const outdoorGateCounts = countBy(outdoorGateRows, 'gate')
const strictPriorityCounts = countBy(strictRows, 'priority')
const strictModuleCounts = countBy(strictRows, 'inferred_module')
const normalizedStrictPriorityCounts = {
  P0: strictPriorityCounts.P0 ?? 0,
  P1: strictPriorityCounts.P1 ?? 0,
  P2: strictPriorityCounts.P2 ?? 0,
  P3: strictPriorityCounts.P3 ?? 0,
}

const snapshot = {
  generatedAt: new Date().toISOString(),
  sourcePaths: {
    appRoot: repoRoot,
    cadWorkspace,
    ...sources,
  },
  sources: await Promise.all(Object.values(sources).map(sourceInfo)),
  summary: {
    projectSlots: 6,
    activeProjects: 2,
    futureSlots: 2,
    reservedFamilies: 2,
    cadEvidenceAssetsLabel: '2,000+',
    productionCandidateRows: 0,
  },
  projects: {
    locker16029: {
      indexedFiles: numberAfter(intakeStatus, '- Indexed files'),
      sourceAssemblies: numberAfter(intakeStatus, '- Source assemblies'),
      sourceParts: numberAfter(intakeStatus, '- Source parts'),
      sourceDrawings: numberAfter(intakeStatus, '- Source drawings'),
      dxfFiles: numberAfter(intakeStatus, '- DXF files'),
      dxfParsed: lockerDxfParsed.done,
      bomRows: numberAfter(intakeStatus, '- BOM rows'),
      bomMapped: lockerBomMapped.done,
      swEvidenceRows: numberAfter(intakeStatus, '- SolidWorks API evidence rows'),
      strictRows: strictRows.length,
      strictPriorityCounts: normalizedStrictPriorityCounts,
      strictModuleCounts,
      regressionStatus: firstCodeAfter(intakeStatus, '- Latest regression status'),
      verifiedCases: ['8door', '12door', '14door', '18door_W784'],
    },
    outdoorCourier: {
      sourceRoot: firstCodeAfter(outdoorStatus, '- source_root'),
      activeProjectCount: 4,
      dxfParsed: 283,
      dxfTotal: 283,
      bomMapped: 611,
      bomRows: 611,
      familyRuleCandidates: numberAfter(outdoorStatus, '- Candidate rows'),
      familyRuleFamilies: 25,
      stepQueueRows: tableNumber(outdoorStatus, 'SolidWorks STEP export queue rows'),
      stepExportedOrExisting: tableNumber(outdoorStatus, 'STEP exported or existing'),
      stepExportFailed: tableNumber(outdoorStatus, 'STEP export failed'),
      measuredValidStepRows: tableNumber(outdoorStatus, 'FreeCAD measured valid STEP rows'),
      formedBboxMeasuredRows: tableNumber(outdoorStatus, 'release rows with formed STEP bbox evidence'),
      productionGateRows: outdoorGateRows.length,
      releaseStatusCounts: outdoorReleaseCounts,
      gateCounts: outdoorGateCounts,
      p0Blockers:
        (outdoorGateCounts.p0_step_export_blocker_door_panel ?? 0) +
        (outdoorGateCounts.p0_step_export_blocker_lock_or_water_guide ?? 0),
      singleDoorAuditStatus: firstCodeAfter(outdoorSingleDoorAudit, '- status'),
    },
  },
}

const outPath = path.join(appRoot, 'src\\data\\generatedSnapshot.ts')
const file = `// This file is generated by scripts/buildStudioSnapshot.mjs.\n// Do not edit by hand; update the CAD status source files or the snapshot builder.\n\nexport const generatedSnapshot = ${JSON.stringify(snapshot, null, 2)} as const\n`

await fs.writeFile(outPath, file, 'utf8')
console.log(`Generated ${outPath}`)
