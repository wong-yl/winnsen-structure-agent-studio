import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(MODULE_DIR, '..')

const DEFAULT_SUMMARY = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/solidworks_2020_native_generation_summary.json'
const DEFAULT_TARGET_SPEC = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first/door_panel_2_12_right_v43/review_single_door_panel_W307_H298_sheetmetal_flatpattern_target_spec.json'

function parseArgs(argv) {
  const options = {
    summary: DEFAULT_SUMMARY,
    targetSpec: DEFAULT_TARGET_SPEC,
  }
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index]
    const next = () => argv[++index] || ''
    if (arg === '--summary') options.summary = next()
    else if (arg === '--target-spec') options.targetSpec = next()
  }
  return options
}

function readJson(path) {
  return JSON.parse(readFileSync(resolve(ROOT, path), 'utf8').replace(/^\uFEFF/, ''))
}

function roundMm(value) {
  return Math.round(Number(value) * 1000) / 1000
}

function distancePair(sizeMm, coordinateFromCenterMm) {
  const half = Number(sizeMm) / 2
  return {
    min: roundMm(Number(coordinateFromCenterMm) + half),
    max: roundMm(half - Number(coordinateFromCenterMm)),
  }
}

function main() {
  const options = parseArgs(process.argv.slice(2))
  const summaryPath = resolve(ROOT, options.summary)
  const summary = readJson(options.summary)
  const targetSpec = readJson(options.targetSpec)
  const target = targetSpec.target || {}
  const targetDatums = Array.isArray(targetSpec.target_hole_datums) ? targetSpec.target_hole_datums : []
  const summaryDatums = Array.isArray(summary.sheetMetalHoleDatums) ? summary.sheetMetalHoleDatums : []
  if (summaryDatums.length !== targetDatums.length) {
    throw new Error(`datum count mismatch: summary=${summaryDatums.length}, target=${targetDatums.length}`)
  }

  const previous = summaryDatums.map((datum) => ({
    role: datum.role,
    generatedFlatCxFromCenterMm: datum.generatedFlatCxFromCenterMm ?? null,
    generatedFlatCyFromCenterMm: datum.generatedFlatCyFromCenterMm ?? null,
    distanceToLeftMm: datum.distanceToLeftMm ?? null,
    distanceToRightMm: datum.distanceToRightMm ?? null,
    distanceToBottomMm: datum.distanceToBottomMm ?? null,
    distanceToTopMm: datum.distanceToTopMm ?? null,
    handednessMirror: datum.handednessMirror ?? null,
  }))

  summary.sheetMetalHoleDatums = summaryDatums.map((datum, index) => {
    const targetDatum = targetDatums[index]
    const xDistances = distancePair(target.flat_width_mm, targetDatum.target_cx_from_center_mm)
    const yDistances = distancePair(target.flat_height_mm, targetDatum.target_cy_from_center_mm)
    return {
      ...datum,
      sourceDistanceToLeftMm: datum.sourceDistanceToLeftMm ?? datum.distanceToLeftMm ?? null,
      sourceDistanceToRightMm: datum.sourceDistanceToRightMm ?? datum.distanceToRightMm ?? null,
      sourceDistanceToBottomMm: datum.sourceDistanceToBottomMm ?? datum.distanceToBottomMm ?? null,
      sourceDistanceToTopMm: datum.sourceDistanceToTopMm ?? datum.distanceToTopMm ?? null,
      generatedFlatCxFromCenterMm: targetDatum.target_cx_from_center_mm,
      generatedFlatCyFromCenterMm: targetDatum.target_cy_from_center_mm,
      horizontalAnchor: targetDatum.horizontal_anchor,
      horizontalOffsetFromAnchorMm: targetDatum.horizontal_offset_from_anchor_mm,
      verticalAnchor: targetDatum.vertical_anchor,
      verticalOffsetFromAnchorMm: targetDatum.vertical_offset_from_anchor_mm,
      distanceToLeftMm: xDistances.min,
      distanceToRightMm: xDistances.max,
      distanceToBottomMm: yDistances.min,
      distanceToTopMm: yDistances.max,
      generatedFlatWidthMm: target.flat_width_mm,
      generatedFlatHeightMm: target.flat_height_mm,
      handednessMirror: 'not_applied_edge_anchored_to_target_blank',
      handednessHandling: `edge_anchored_${summary.handedness || target.handedness || 'unknown'}_without_coordinate_mirror`,
      sourceDatumMapping: 'nearest_source_edge_offset_preserved_on_generated_flat',
      featureImplementationStatus: datum.featureImplementationStatus || 'datum_ready_for_solidworks_cut_feature',
    }
  })

  summary.sheetMetalHoleDatumMappingStatus = 'edge_anchored_target_spec_aligned'
  summary.sheetMetalHoleDatumTargetSpec = options.targetSpec
  summary.sheetMetalHoleDatumCorrection = {
    status: 'corrected_metadata_only_geometry_still_release_blocked',
    updatedAt: new Date().toISOString(),
    previousIssue: 'right-hand mirror datum mapping overflowed the generated 343.4 x 334.4 mm target blank',
    previous,
  }

  writeFileSync(summaryPath, `${JSON.stringify(summary, null, 2)}\n`, 'utf8')
  process.stdout.write(`${JSON.stringify({
    summary: options.summary,
    target_spec: options.targetSpec,
    status: summary.sheetMetalHoleDatumMappingStatus,
    datum_count: summary.sheetMetalHoleDatums.length,
  }, null, 2)}\n`)
}

const currentFile = fileURLToPath(import.meta.url)
const entryFile = process.argv[1] ? fileURLToPath(pathToFileURL(resolve(process.argv[1])).href) : ''
if (currentFile === entryFile) {
  main()
}
