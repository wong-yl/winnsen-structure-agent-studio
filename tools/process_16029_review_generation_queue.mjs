import { existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import { planTemplateFullAssemblyRequest } from './locker_16029_template_rules.mjs'

const ROOT = resolve('D:/Winnsen_Structure_Agent_Studio')
const DATA_DIR = resolve(ROOT, 'data')
const REQUEST_DIR = resolve(DATA_DIR, 'review_generation_requests')
const INDEX_PATH = resolve(REQUEST_DIR, 'generation_request_index.json')
const OUTPUT_ROOT = resolve(ROOT, 'workers/generated_models/review_generation_requests')
const LOG_ROOT = resolve(ROOT, 'workers/generation_logs')
const NODE_DIR = dirname(process.execPath)

function readJson(path, fallback) {
  try {
    return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
  } catch {
    return fallback
  }
}

function writeJson(path, value) {
  mkdirSync(dirname(path), { recursive: true })
  const tempPath = `${path}.tmp`
  writeFileSync(tempPath, JSON.stringify(value, null, 2), 'utf8')
  renameSync(tempPath, path)
}

function nowIso() {
  return new Date().toISOString()
}

function parseArgs(argv) {
  const options = { id: '', limit: 1, force: false, dryRun: false, emitWorkerPayload: false }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    if (arg === '--force') options.force = true
    else if (arg === '--dry-run') options.dryRun = true
    else if (arg === '--emit-worker-payload') options.emitWorkerPayload = true
    else if (arg.startsWith('--id=')) options.id = arg.slice('--id='.length)
    else if (arg === '--id') options.id = argv[++index] || ''
    else if (arg.startsWith('--limit=')) options.limit = Math.max(1, Number(arg.slice('--limit='.length)) || 1)
    else if (arg === '--limit') options.limit = Math.max(1, Number(argv[++index]) || 1)
    else if (arg === '--all') options.limit = 999
  }
  return options
}

function asNumber(value, fallback) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function safeText(value) {
  return String(value ?? '').replace(/[<>&]/g, (char) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;' }[char]))
}

function csv(value) {
  const text = String(value ?? '')
  return /[",\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

function psSingleQuote(value) {
  return `'${String(value).replace(/'/g, "''")}'`
}

function powershellExe() {
  const systemRoot = process.env.SystemRoot || 'C:\\Windows'
  const candidate = resolve(systemRoot, 'System32/WindowsPowerShell/v1.0/powershell.exe')
  return existsSync(candidate) ? candidate : 'powershell.exe'
}

function childProcessEnv(extra = {}) {
  const pathValue = [
    NODE_DIR,
    'C:\\Program Files\\nodejs',
    process.env.Path || process.env.PATH || '',
    'C:\\Windows\\System32',
    'C:\\Windows',
    'C:\\Windows\\System32\\WindowsPowerShell\\v1.0',
  ].filter(Boolean).join(';')
  return {
    ...process.env,
    Path: pathValue,
    PATH: pathValue,
    STUDIO_NODE_EXE: process.execPath,
    ...extra,
  }
}

function compressPackage(sourceDir, zipPath) {
  if (existsSync(zipPath)) rmSync(zipPath, { force: true })
  mkdirSync(dirname(zipPath), { recursive: true })
  const command = [
    '$ErrorActionPreference = "Stop"',
    `$items = Get-ChildItem -LiteralPath ${psSingleQuote(sourceDir)} -Force | Where-Object { -not $_.Name.StartsWith('~$') }`,
    'if (-not $items) { throw "no files to compress" }',
    `Compress-Archive -LiteralPath $items.FullName -DestinationPath ${psSingleQuote(zipPath)} -Force`,
  ].join('; ')
  const completed = spawnSync(powershellExe(), ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', command], {
    cwd: ROOT,
    encoding: 'utf8',
    windowsHide: true,
    env: childProcessEnv(),
  })
  if (completed.status !== 0) {
    throw new Error(`Compress-Archive failed: ${completed.stderr || completed.stdout || completed.error?.message || 'unknown error'}`)
  }
}

function runPowerShellScript(scriptPath, args, label) {
  const completed = spawnSync(
    powershellExe(),
    ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath, ...args],
    {
      cwd: ROOT,
      encoding: 'utf8',
      windowsHide: true,
      timeout: 20 * 60 * 1000,
      env: childProcessEnv(),
    },
  )
  if (completed.status !== 0) {
    throw new Error(`${label} failed: ${completed.stderr || completed.stdout || completed.error?.message || 'unknown error'}`)
  }
  return `${completed.stdout || ''}${completed.stderr ? `\n${completed.stderr}` : ''}`
}

function lastSummaryPath(output, fileName = 'solidworks_2020_native_generation_summary.json') {
  return String(output || '')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .reverse()
    .find((line) => line.toLowerCase().endsWith(fileName.toLowerCase()))
}

function readIndex() {
  const index = readJson(INDEX_PATH, { requests: [] })
  return Array.isArray(index.requests) ? index : { requests: [] }
}

function requestFilePath(id) {
  return resolve(REQUEST_DIR, `${id}.json`)
}

function updateRequest(id, patch) {
  const index = readIndex()
  let updated = null
  index.requests = index.requests.map((item) => {
    if (item.id !== id) return item
    updated = { ...item, ...patch }
    return updated
  })
  if (!updated) throw new Error(`request not found: ${id}`)
  writeJson(INDEX_PATH, index)
  writeJson(requestFilePath(id), updated)
  return updated
}

function candidateRequests(options) {
  const index = readIndex()
  const pending = index.requests.filter((item) => {
    if (options.id && item.id !== options.id) return false
    if (options.force) return true
    if (item.downloadUrl || item.zipPath) return false
    const status = String(item.status || '')
    return !status.includes('failed') && status !== 'cad_worker_payload_ready'
  })
  return pending.slice(0, options.limit)
}

function shouldGenerateSolidWorksSingleDoor(request) {
  if (request.taskMode !== 'single_model') return false
  const doorType = String(request.doorType || 'ordinary_door_panel').toLowerCase()
  return doorType.includes('ordinary') || doorType.includes('storage') || doorType.includes('door')
}

function isFullAssemblyRequest(request) {
  return String(request.taskMode || 'full_assembly') !== 'single_model'
}

function fullAssemblyTemplatePlanFor(request) {
  if (!isFullAssemblyRequest(request)) return null
  try {
    return planTemplateFullAssemblyRequest({
      cabinetWidth: request.cabinetWidth,
      cabinetHeight: request.cabinetHeight,
      cabinetDepth: request.cabinetDepth,
      columns: request.columns,
      doorCount: request.doorCount,
      doorWidth: request.doorWidth,
      doorHeight: request.doorHeight,
      rowSequence: request.rowSequence,
      prompt: request.prompt,
    })
  } catch {
    return null
  }
}

function shouldGenerateSolidWorksTemplateFullAssembly(request) {
  return Boolean(fullAssemblyTemplatePlanFor(request))
}

function processSolidWorksSingleDoorRequest(request, options) {
  if (options.dryRun) {
    console.log(`${request.id}\t${request.status}\tsolidworks2020_single_door`)
    return request
  }

  const startedAt = nowIso()
  updateRequest(request.id, {
    status: 'running_solidworks2020_native',
    queueStartedAt: startedAt,
    message: 'SolidWorks 2020 native sheet-metal model is generating.',
  })

  try {
    const doorWidth = asNumber(request.doorWidth, asNumber(request.cabinetWidth, 300))
    const doorHeight = asNumber(request.doorHeight, asNumber(request.cabinetHeight, 1917))
    const generator = resolve(ROOT, 'tools/generate_review_solidworks_single_door.ps1')
    if (!existsSync(generator)) throw new Error(`SolidWorks generator was not found: ${generator}`)

    const output = runPowerShellScript(
      generator,
      [
        '-RequestId',
        request.id,
        '-DoorWidthMm',
        String(doorWidth),
        '-DoorHeightMm',
        String(doorHeight),
        '-Handedness',
        'left',
      ],
      'SolidWorks 2020 sheet-metal door generation',
    )
    const summaryPath = lastSummaryPath(output)
    if (!summaryPath || !existsSync(summaryPath)) {
      throw new Error(`SolidWorks generation summary was not found. Output:\n${output}`)
    }
    const summary = readJson(summaryPath, null)
    if (!summary || summary.status !== 'solidworks_2020_native_ready') {
      throw new Error(`SolidWorks generation did not report ready status: ${summaryPath}`)
    }

    const outputDir = summary.outputDir
    const zipPath = resolve(LOG_ROOT, `review_generation_${request.id}_solidworks2020_sheetmetal_door.zip`)
    compressPackage(outputDir, zipPath)

    const completedAt = nowIso()
    const result = updateRequest(request.id, {
      status: 'solidworks2020_native_ready',
      resultKind: 'solidworks2020_sheetmetal_model',
      outputDir,
      zipPath,
      downloadUrl: `/generation-download/${request.id}`,
      processedAt: completedAt,
      queueFinishedAt: completedAt,
      modelGeneratedAt: completedAt,
      primaryAssembly: summary.primaryAssembly,
      sheetMetalPanelPart: summary.sheetMetalPanelPart,
      sheetMetalStiffenerPart: summary.sheetMetalStiffenerPart,
      outputFiles: Array.isArray(summary.outputFiles) ? summary.outputFiles : [],
      error: '',
      message: 'SolidWorks 2020 native sheet-metal door model generated. Download contains .SLDPRT, .SLDASM, STEP, and evidence JSON.',
    })
    console.log(`processed\t${request.id}\t${zipPath}`)
    return result
  } catch (error) {
    const failedAt = nowIso()
    const result = updateRequest(request.id, {
      status: 'failed_solidworks2020_native',
      queueFinishedAt: failedAt,
      error: error instanceof Error ? error.message : String(error),
      message: 'SolidWorks 2020 native sheet-metal generation failed; check workers/generation_logs and queue stdout/stderr.',
    })
    console.error(`failed\t${request.id}\t${result.error}`)
    return result
  }
}

function processSolidWorksTemplateFullAssemblyRequest(request, options) {
  if (options.dryRun) {
    console.log(`${request.id}\t${request.status}\tsolidworks2020_template_full_assembly`)
    return request
  }

  const startedAt = nowIso()
  updateRequest(request.id, {
    status: 'running_solidworks2020_full_assembly_pack_and_go',
    queueStartedAt: startedAt,
    message: 'SolidWorks 2020 template-backed full assembly package is generating.',
  })

  try {
    const cabinetWidth = asNumber(request.cabinetWidth, 740)
    const cabinetHeight = asNumber(request.cabinetHeight, 1917)
    const cabinetDepth = asNumber(request.cabinetDepth, 550)
    const columns = Math.max(1, Math.round(asNumber(request.columns, 2)))
    const doorCount = Math.max(1, Math.round(asNumber(request.doorCount, 6)))
    const doorWidth = asNumber(request.doorWidth, 0)
    const doorHeight = asNumber(request.doorHeight, 0)
    const generator = resolve(ROOT, 'tools/generate_review_solidworks_full_assembly.ps1')
    if (!existsSync(generator)) throw new Error(`SolidWorks full assembly generator was not found: ${generator}`)

    const generatorArgs = [
      '-RequestId',
      request.id,
      '-CabinetWidthMm',
      String(cabinetWidth),
      '-CabinetHeightMm',
      String(cabinetHeight),
      '-CabinetDepthMm',
      String(cabinetDepth),
      '-Columns',
      String(columns),
      '-DoorCount',
      String(doorCount),
      '-RowSequence',
      request.rowSequence || '',
      '-Prompt',
      request.prompt || '',
    ]
    if (doorWidth > 0) generatorArgs.push('-DoorWidthMm', String(doorWidth))
    if (doorHeight > 0) generatorArgs.push('-DoorHeightMm', String(doorHeight))

    const output = runPowerShellScript(generator, generatorArgs, 'SolidWorks 2020 template full assembly generation')
    const summaryPath = lastSummaryPath(output, 'solidworks_2020_full_assembly_generation_summary.json')
    if (!summaryPath || !existsSync(summaryPath)) {
      throw new Error(`SolidWorks full assembly generation summary was not found. Output:\n${output}`)
    }
    const summary = readJson(summaryPath, null)
    const readyStatuses = new Set([
      'solidworks_2020_full_assembly_ready',
      'solidworks_2020_template_rule_package_ready',
      'solidworks_2020_template_rule_needs_native_door_generation',
      'solidworks_2020_full_assembly_needs_structure_revision',
    ])
    if (!summary || !readyStatuses.has(summary.status)) {
      throw new Error(`SolidWorks full assembly generation did not report ready status: ${summaryPath}`)
    }

    const outputDir = summary.outputDir
    const zipPath = resolve(LOG_ROOT, `review_generation_${request.id}_solidworks2020_full_assembly.zip`)
    compressPackage(outputDir, zipPath)

    const completedAt = nowIso()
    const structureFeedbackIssueCount = Number(summary.structureFeedbackIssueCount || 0)
    const structureFeedbackP0Count = Number(summary.structureFeedbackP0Count || 0)
    const structureFeedbackP1Count = Number(summary.structureFeedbackP1Count || 0)
    const structureFeedbackP2Count = Number(summary.structureFeedbackP2Count || 0)
    const structureNeedsRevision = structureFeedbackIssueCount > 0 ||
      String(summary.structureFeedbackStatus || '').toLowerCase() === 'issues_found' ||
      String(summary.handoffReadinessStatus || '').toLowerCase() === 'needs_structure_revision'
    const nativeDoorModuleNeedsGeneration = Boolean(summary.nativeDoorModuleNeedsGeneration) ||
      String(summary.handoffReadinessStatus || '').toLowerCase() === 'needs_native_door_generation'
    const missingNativeDoorModuleUnits = Array.isArray(summary.missingNativeDoorModuleUnits)
      ? summary.missingNativeDoorModuleUnits
      : []
    const generatedNativeDoorModuleCount = Number(summary.generatedNativeDoorModuleCount || 0)
    const result = updateRequest(request.id, {
      status: structureNeedsRevision
        ? 'solidworks2020_full_assembly_needs_structure_revision'
        : nativeDoorModuleNeedsGeneration
        ? 'solidworks2020_template_rule_needs_native_door_generation'
        : 'solidworks2020_full_assembly_ready',
      resultKind: structureNeedsRevision ? 'solidworks2020_structure_revision_evidence_package' : summary.resultKind || 'solidworks2020_full_assembly_model',
      outputDir,
      zipPath,
      downloadUrl: `/generation-download/${request.id}`,
      processedAt: completedAt,
      queueFinishedAt: completedAt,
      modelGeneratedAt: completedAt,
      primaryAssembly: summary.primaryAssembly,
      packAndGoDir: summary.packAndGoDir,
      packAndGoChineseSaveNameMapCount: summary.packAndGoChineseSaveNameMapCount,
      packAndGoGotDocumentSaveToNames: summary.packAndGoGotDocumentSaveToNames,
      packAndGoSetDocumentSaveToNames: summary.packAndGoSetDocumentSaveToNames,
      templateRulePlan: summary.templateRulePlan,
      templateRuleMode: summary.templateRuleMode,
      compatibleWithNativeTemplate: summary.compatibleWithNativeTemplate,
      templateDoorModuleBindingStatus: summary.templateDoorModuleBindingStatus,
      nativeDoorModuleBindingStatus: summary.nativeDoorModuleBindingStatus,
      generatedNativeDoorModules: summary.generatedNativeDoorModules,
      generatedNativeDoorModuleCount,
      missingNativeDoorModuleUnits,
      nativeDoorModuleNeedsGeneration,
      goldSourceModuleTargets: summary.goldSourceModuleTargets,
      goldSourceModuleTargetsTsv: summary.goldSourceModuleTargetsTsv,
      goldSourceModuleRebuildPlanTsv: summary.goldSourceModuleRebuildPlanTsv,
      goldSourceFixedCabinetModulePlacementsTsv: summary.goldSourceFixedCabinetModulePlacementsTsv,
      goldSourceShelfCandidatePlacementsTsv: summary.goldSourceShelfCandidatePlacementsTsv,
      goldSourceCabinetCandidatePlacementsTsv: summary.goldSourceCabinetCandidatePlacementsTsv,
      goldSourceFullAssemblyCandidatePlacementsTsv: summary.goldSourceFullAssemblyCandidatePlacementsTsv,
      goldSourceModuleTargetCount: summary.goldSourceModuleTargetCount,
      goldSourceShelfModuleTargetCount: summary.goldSourceShelfModuleTargetCount,
      goldSourceShelfCandidateTargetCount: summary.goldSourceShelfCandidateTargetCount,
      goldSourceCabinetCandidateTargetCount: summary.goldSourceCabinetCandidateTargetCount,
      goldSourceModuleBuildReadyTargetCount: summary.goldSourceModuleBuildReadyTargetCount,
      goldSourceModuleNeedsBindingTargetCount: summary.goldSourceModuleNeedsBindingTargetCount,
      goldSourceModuleTargetsSourceStructureBound: summary.goldSourceModuleTargetsSourceStructureBound,
      fixedCabinetSourceReferenceAssembly: summary.fixedCabinetSourceReferenceAssembly,
      fixedCabinetSourceReferenceBuild: summary.fixedCabinetSourceReferenceBuild,
      fixedCabinetSourceReferenceStructure: summary.fixedCabinetSourceReferenceStructure,
      fixedCabinetSourceReferenceComponentCount: summary.fixedCabinetSourceReferenceComponentCount,
      fixedCabinetSourceReferenceTopLevelCount: summary.fixedCabinetSourceReferenceTopLevelCount,
      shelfBindingCandidateStatus: summary.shelfBindingCandidateStatus,
      shelfBindingCandidateAssembly: summary.shelfBindingCandidateAssembly,
      shelfBindingCandidateBuild: summary.shelfBindingCandidateBuild,
      shelfBindingCandidateStructure: summary.shelfBindingCandidateStructure,
      shelfBindingCandidateComponentCount: summary.shelfBindingCandidateComponentCount,
      shelfBindingCandidateTopLevelCount: summary.shelfBindingCandidateTopLevelCount,
      cabinetBodyCandidateStatus: summary.cabinetBodyCandidateStatus,
      cabinetBodyCandidateAssembly: summary.cabinetBodyCandidateAssembly,
      cabinetBodyCandidateBuild: summary.cabinetBodyCandidateBuild,
      cabinetBodyCandidateStructure: summary.cabinetBodyCandidateStructure,
      cabinetBodyCandidateComponentCount: summary.cabinetBodyCandidateComponentCount,
      cabinetBodyCandidateTopLevelCount: summary.cabinetBodyCandidateTopLevelCount,
      cabinetBodyCandidateBboxStatus: summary.cabinetBodyCandidateBboxStatus,
      cabinetBodyCandidateShelfInsideBodyEnvelope: summary.cabinetBodyCandidateShelfInsideBodyEnvelope,
      cabinetBodyCandidateShelfBboxCount: summary.cabinetBodyCandidateShelfBboxCount,
      cabinetBodyCandidateFixedBodyBboxCount: summary.cabinetBodyCandidateFixedBodyBboxCount,
      cabinetBodyCandidateBodyEnvelope: summary.cabinetBodyCandidateBodyEnvelope,
      fullAssemblyCandidateStatus: summary.fullAssemblyCandidateStatus,
      fullAssemblyCandidateAssembly: summary.fullAssemblyCandidateAssembly,
      fullAssemblyCandidateBuild: summary.fullAssemblyCandidateBuild,
      fullAssemblyCandidateStructure: summary.fullAssemblyCandidateStructure,
      fullAssemblyCandidateComponentCount: summary.fullAssemblyCandidateComponentCount,
      fullAssemblyCandidateTopLevelCount: summary.fullAssemblyCandidateTopLevelCount,
      packSourceAssembly: summary.packSourceAssembly,
      structureRecord: summary.structureRecord,
      structureFeedback: summary.structureFeedback,
      structureFeedbackStatus: summary.structureFeedbackStatus,
      structureFeedbackIssueCount: summary.structureFeedbackIssueCount,
      structureFeedbackP0Count: summary.structureFeedbackP0Count,
      structureFeedbackP1Count: summary.structureFeedbackP1Count,
      structureFeedbackP2Count: summary.structureFeedbackP2Count,
      handoffReadinessStatus: summary.handoffReadinessStatus,
      componentRenameAttemptCount: summary.componentRenameAttemptCount,
      componentRenameAssignedCount: summary.componentRenameAssignedCount,
      componentRenameVerifiedCount: summary.componentRenameVerifiedCount,
      componentRenameRunStatus: summary.componentRenameRunStatus,
      componentRenameError: summary.componentRenameError,
      componentRenameTimeoutSeconds: summary.componentRenameTimeoutSeconds,
      componentNamingStatus: summary.componentNamingStatus,
      outputFiles: Array.isArray(summary.outputFiles) ? summary.outputFiles : [],
      error: '',
      message: structureNeedsRevision
        ? `SolidWorks 2020 package generated as a structure-revision evidence package. Gold-source feedback still has ${structureFeedbackIssueCount} issue(s): P0=${structureFeedbackP0Count}, P1=${structureFeedbackP1Count}, P2=${structureFeedbackP2Count}; do not treat it as engineer-ready.`
        : nativeDoorModuleNeedsGeneration
        ? `SolidWorks 2020 template-rule package generated, but native door modules still need generation for ratio unit(s): ${missingNativeDoorModuleUnits.join(', ') || 'unknown'}. Download is evidence/parameter package, not engineer-ready Pack-and-Go.`
        : summary.compatibleWithNativeTemplate
        ? 'SolidWorks 2020 full assembly Pack-and-Go package generated. Download contains .SLDASM, .SLDPRT, evidence JSON, and review captures.'
        : generatedNativeDoorModuleCount > 0
        ? `SolidWorks 2020 template-rule package generated with ${generatedNativeDoorModuleCount} generated native door module(s). Download contains Pack-and-Go plus parameter and evidence files.`
        : 'SolidWorks 2020 template-rule package generated. Download contains the verified template Pack-and-Go plus a parameterized rule plan for engineer review.',
    })
    console.log(`processed\t${request.id}\t${zipPath}`)
    return result
  } catch (error) {
    const failedAt = nowIso()
    const result = updateRequest(request.id, {
      status: 'failed_solidworks2020_full_assembly',
      queueFinishedAt: failedAt,
      error: error instanceof Error ? error.message : String(error),
      message: 'SolidWorks 2020 full assembly generation failed; check workers/generation_logs and queue stdout/stderr.',
    })
    console.error(`failed\t${request.id}\t${result.error}`)
    return result
  }
}

function buildPayload(request) {
  const cabinetWidth = asNumber(request.cabinetWidth, 800)
  const cabinetHeight = asNumber(request.cabinetHeight, 1917)
  const cabinetDepth = asNumber(request.cabinetDepth, 550)
  const columns = Math.max(1, Math.round(asNumber(request.columns, request.taskMode === 'single_model' ? 1 : 2)))
  const doorCount = Math.max(1, Math.round(asNumber(request.doorCount, request.taskMode === 'single_model' ? 1 : columns)))
  const doorWidth = asNumber(request.doorWidth, request.taskMode === 'single_model' ? cabinetWidth : Math.round((cabinetWidth - 126) / columns))
  const doorHeight = asNumber(request.doorHeight, request.taskMode === 'single_model' ? cabinetHeight : 908)
  return {
    schema: 'winnsen.review_generation.cad_worker_payload.v1',
    sourceRequestId: request.id,
    taskMode: request.taskMode === 'single_model' ? 'single_model' : 'full_assembly',
    cadMainline: 'SolidWorks 2020',
    fallbackInternalEvidenceTool: 'FreeCAD only for internal parameter/evidence generation',
    modelBoundary: {
      cabinetWidthMm: cabinetWidth,
      cabinetHeightMm: cabinetHeight,
      cabinetDepthMm: cabinetDepth,
      columns,
      doorCount,
      doorWidthMm: doorWidth,
      doorHeightMm: doorHeight,
      rowSequence: request.rowSequence || '',
    },
    doorModule: {
      doorType: request.doorType || 'ordinary_door_panel',
      lockType: request.lockType || '待确认',
      hingeType: request.hingeType || '待确认',
      latchType: request.latchType || '待确认',
      reinforcement: request.reinforcement || '待确认',
      openings: request.openings || '待确认',
      material: request.material || '待确认',
      thickness: request.thickness || '待确认',
    },
    requiredEvidenceBeforeProductionRelease: [
      'SolidWorks 2020 open evidence',
      'component tree / assembly relationship check',
      'door gap and clearance check',
      'hinge hole pattern check',
      'lock and latch engagement check',
      'reinforcement rib clearance check',
      'material thickness and bend rule check',
      'engineer signoff before production drawings',
    ],
    boundary:
      'This package is a CAD worker task payload and review checklist. It is not a production drawing release package.',
  }
}

function writePreviewSvg(path, payload) {
  const w = payload.modelBoundary.cabinetWidthMm
  const h = payload.modelBoundary.cabinetHeightMm
  const d = payload.modelBoundary.cabinetDepthMm
  const columns = payload.modelBoundary.columns
  const doorCount = payload.modelBoundary.doorCount
  const rows = Math.max(1, Math.ceil(doorCount / columns))
  const bodyX = 90
  const bodyY = 40
  const bodyW = 430
  const bodyH = 760
  const gap = 6
  const header = 38
  const doorAreaY = bodyY + header
  const doorAreaH = bodyH - header - 28
  const cellW = (bodyW - gap * (columns + 1)) / columns
  const cellH = (doorAreaH - gap * (rows + 1)) / rows
  const doors = []
  for (let row = 0; row < rows; row += 1) {
    for (let col = 0; col < columns; col += 1) {
      const index = row * columns + col
      if (index >= doorCount) continue
      const x = bodyX + gap + col * (cellW + gap)
      const y = doorAreaY + gap + row * (cellH + gap)
      doors.push(`<rect x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="${cellW.toFixed(2)}" height="${cellH.toFixed(2)}" rx="2" fill="#f7f9fc" stroke="#1f3585" stroke-width="2"/>`)
      doors.push(`<circle cx="${(x + cellW - 18).toFixed(2)}" cy="${(y + cellH / 2).toFixed(2)}" r="4" fill="#f04a12"/>`)
      doors.push(`<line x1="${(x + 12).toFixed(2)}" y1="${(y + 18).toFixed(2)}" x2="${(x + 12).toFixed(2)}" y2="${(y + cellH - 18).toFixed(2)}" stroke="#67758a" stroke-width="3"/>`)
    }
  }
  const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="640" height="900" viewBox="0 0 640 900">
  <rect width="640" height="900" fill="#eef3fa"/>
  <rect x="${bodyX}" y="${bodyY}" width="${bodyW}" height="${bodyH}" rx="4" fill="#dce6f2" stroke="#56657c" stroke-width="3"/>
  <polygon points="${bodyX + bodyW},${bodyY} ${bodyX + bodyW + 58},${bodyY + 28} ${bodyX + bodyW + 58},${bodyY + bodyH + 28} ${bodyX + bodyW},${bodyY + bodyH}" fill="#c9d7e8" stroke="#56657c" stroke-width="2"/>
  <rect x="${bodyX + 8}" y="${bodyY + 8}" width="${bodyW - 16}" height="${header - 12}" fill="#f8fafc" stroke="#9aabc2"/>
  ${doors.join('\n  ')}
  <text x="320" y="835" text-anchor="middle" font-family="Arial, sans-serif" font-size="24" font-weight="700" fill="#1f3585">${safeText(payload.taskMode)} / ${w}W x ${h}H x ${d}D</text>
  <text x="320" y="866" text-anchor="middle" font-family="Arial, sans-serif" font-size="17" fill="#56657c">${columns} columns / ${doorCount} doors / door ${payload.modelBoundary.doorWidthMm}W x ${payload.modelBoundary.doorHeightMm}H</text>
</svg>
`
  writeFileSync(path, svg, 'utf8')
}

function writeChecklist(path, request, payload) {
  const rows = [
    ['check', 'status', 'note'],
    ['request_saved', 'PASS', request.id],
    ['cad_mainline', 'PENDING_ENGINEER_CAD', payload.cadMainline],
    ['dimension_payload', 'READY_FOR_WORKER', `${payload.modelBoundary.cabinetWidthMm}W x ${payload.modelBoundary.cabinetHeightMm}H x ${payload.modelBoundary.cabinetDepthMm}D`],
    ['door_payload', 'READY_FOR_WORKER', `${payload.modelBoundary.doorCount} doors; ${payload.modelBoundary.doorWidthMm}W x ${payload.modelBoundary.doorHeightMm}H`],
    ['lock_hinge_latch', 'REQUIRES_ENGINEER_CHECK', `${payload.doorModule.lockType} / ${payload.doorModule.hingeType} / ${payload.doorModule.latchType}`],
    ['production_release', 'BLOCKED_BY_SIGNOFF', 'Needs SolidWorks 2020 open evidence and structural engineer approval.'],
  ]
  writeFileSync(path, rows.map((row) => row.map(csv).join(',')).join('\n') + '\n', 'utf8')
}

function writeReadme(path, request, payload) {
  const lines = [
    `# ${request.id} CAD Worker Package`,
    '',
    'This package is generated from the review login page queue.',
    '',
    '## Boundary',
    '',
    '- This is a CAD worker task payload package.',
    '- It is not a production drawing release package.',
    '- SolidWorks 2020 remains the engineer-review CAD mainline.',
    '- FreeCAD may be used internally only for parameter/evidence generation.',
    '',
    '## Request',
    '',
    `- User: ${request.username || '-'}`,
    `- Task mode: ${payload.taskMode}`,
    `- Prompt: ${request.prompt || '-'}`,
    `- Cabinet: ${payload.modelBoundary.cabinetWidthMm}W x ${payload.modelBoundary.cabinetHeightMm}H x ${payload.modelBoundary.cabinetDepthMm}D`,
    `- Door: ${payload.modelBoundary.doorCount} door(s), ${payload.modelBoundary.doorWidthMm}W x ${payload.modelBoundary.doorHeightMm}H`,
    `- Row sequence: ${payload.modelBoundary.rowSequence || '-'}`,
    '',
    '## Package Files',
    '',
    '- `cad_worker_payload.json`: normalized task schema for CAD generation.',
    '- `source_request.json`: original review request snapshot.',
    '- `model_preview.svg`: browser-side reference preview, not true CAD geometry.',
    '- `validation_checklist.csv`: required checks before production release.',
    '- `README.md`: this boundary note.',
  ]
  writeFileSync(path, lines.join('\n') + '\n', 'utf8')
}

function processRequest(request, options) {
  if (shouldGenerateSolidWorksSingleDoor(request)) {
    return processSolidWorksSingleDoorRequest(request, options)
  }
  if (shouldGenerateSolidWorksTemplateFullAssembly(request)) {
    return processSolidWorksTemplateFullAssemblyRequest(request, options)
  }
  if (!options.emitWorkerPayload) {
    if (options.dryRun) {
      console.log(`${request.id}\t${request.status}\twaiting_solidworks2020_native_worker`)
      return request
    }
    return updateRequest(request.id, {
      status: 'waiting_solidworks2020_native_worker',
      queueStartedAt: nowIso(),
      queueFinishedAt: nowIso(),
      downloadUrl: '',
      zipPath: '',
      resultKind: '',
      outputFiles: [],
      error: '',
      message: isFullAssemblyRequest(request)
        ? 'This full assembly request needs a two-column row-ratio plan before the SolidWorks 2020 template worker can package it.'
        : 'This request is queued for a SolidWorks 2020 native model worker; no CAD task package was emitted.',
    })
  }

  if (options.dryRun) {
    console.log(`${request.id}\t${request.status}\t${request.taskMode}`)
    return request
  }

  const startedAt = nowIso()
  updateRequest(request.id, {
    status: 'running_cad_worker_payload',
    queueStartedAt: startedAt,
    message: '正在生成 CAD worker 任务包。',
  })

  try {
    const outputDir = resolve(OUTPUT_ROOT, request.id)
    mkdirSync(outputDir, { recursive: true })
    const payload = buildPayload(request)
    writeJson(resolve(outputDir, 'source_request.json'), request)
    writeJson(resolve(outputDir, 'cad_worker_payload.json'), payload)
    writeChecklist(resolve(outputDir, 'validation_checklist.csv'), request, payload)
    writePreviewSvg(resolve(outputDir, 'model_preview.svg'), payload)
    writeReadme(resolve(outputDir, 'README.md'), request, payload)

    const zipPath = resolve(LOG_ROOT, `review_generation_${request.id}_cad_worker_package.zip`)
    compressPackage(outputDir, zipPath)
    const completedAt = nowIso()
    const result = updateRequest(request.id, {
      status: 'cad_worker_payload_ready',
      resultKind: 'cad_worker_payload_package',
      outputDir,
      zipPath,
      downloadUrl: `/generation-download/${request.id}`,
      processedAt: completedAt,
      queueFinishedAt: completedAt,
      outputFiles: [
        relative(outputDir, resolve(outputDir, 'source_request.json')),
        relative(outputDir, resolve(outputDir, 'cad_worker_payload.json')),
        relative(outputDir, resolve(outputDir, 'validation_checklist.csv')),
        relative(outputDir, resolve(outputDir, 'model_preview.svg')),
        relative(outputDir, resolve(outputDir, 'README.md')),
      ],
      message: 'CAD worker 任务包已生成；正式 CAD 模型仍需 SolidWorks/FreeCAD worker 按规则执行并由工程师审核。',
    })
    console.log(`processed\t${request.id}\t${zipPath}`)
    return result
  } catch (error) {
    const failedAt = nowIso()
    const result = updateRequest(request.id, {
      status: 'failed_cad_worker_payload',
      queueFinishedAt: failedAt,
      error: error instanceof Error ? error.message : String(error),
      message: 'CAD worker 任务包生成失败，请查看 workers/generation_logs。',
    })
    console.error(`failed\t${request.id}\t${result.error}`)
    return result
  }
}

function main() {
  mkdirSync(REQUEST_DIR, { recursive: true })
  mkdirSync(OUTPUT_ROOT, { recursive: true })
  mkdirSync(LOG_ROOT, { recursive: true })
  const options = parseArgs(process.argv.slice(2))
  const requests = candidateRequests(options)
  if (!requests.length) {
    console.log('no pending review generation requests')
    return
  }
  for (const request of requests) processRequest(request, options)
}

main()
