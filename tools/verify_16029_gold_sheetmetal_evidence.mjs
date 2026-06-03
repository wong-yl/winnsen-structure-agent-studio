import { readFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'

export const GOLD_SHEETMETAL_EVIDENCE_SCHEMA = 'winnsen.locker16029.gold_sheetmetal_evidence.v1'

const REQUIRED_ROLES = Object.freeze([
  'door_panel',
  'door_stiffener_or_press_strip',
  'shelf_or_horizontal_layer',
  'vertical_partition',
  'side_panel',
  'base_or_bottom',
  'top_cover',
  'lock_or_hinge_interface',
])

const REQUIRED_DOOR_CLASSES = Object.freeze(['1/12', '2/12', '3/12', '4/12', '5/12', '6/12'])

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''))
}

function issue(id, severity, message, details = {}) {
  return { id, severity, message, ...details }
}

function asArray(value) {
  return Array.isArray(value) ? value : []
}

function asNumber(value) {
  const number = Number(value)
  return Number.isFinite(number) ? number : 0
}

export function analyzeGoldSheetmetalEvidence(report) {
  const issues = []
  const summary = report?.summary || {}
  const sourceBuckets = summary.source_bucket_counts || {}
  const roleCoverage = summary.required_roles || {}
  const doorClassCoverage = summary.door_class_coverage_1_to_6 || {}
  const dxfFiles = asArray(report?.dxf_files)

  if (report?.schema !== GOLD_SHEETMETAL_EVIDENCE_SCHEMA) {
    issues.push(issue('schema_mismatch', 'P0', 'Gold sheet-metal evidence schema is not recognized.', {
      actual: report?.schema,
      expected: GOLD_SHEETMETAL_EVIDENCE_SCHEMA,
    }))
  }

  if (asNumber(summary.dxf_count) < 120) {
    issues.push(issue('too_few_dxf_files', 'P0', '1000W gold/source evidence has too few DXF flat-pattern files.', {
      actual: asNumber(summary.dxf_count),
      expectedMinimum: 120,
    }))
  }

  if (asNumber(summary.parsed_dxf_count) !== asNumber(summary.dxf_count)) {
    issues.push(issue('dxf_parse_incomplete', 'P0', 'Not all 1000W gold/source DXF files parsed successfully.', {
      parsed: asNumber(summary.parsed_dxf_count),
      total: asNumber(summary.dxf_count),
    }))
  }

  if (asNumber(sourceBuckets.current_flat_pattern) < 40) {
    issues.push(issue('too_few_current_flat_patterns', 'P0', 'Current 1000W flat-pattern folder does not provide enough direct DXF evidence.', {
      actual: asNumber(sourceBuckets.current_flat_pattern),
      expectedMinimum: 40,
    }))
  }

  for (const role of REQUIRED_ROLES) {
    const item = roleCoverage[role] || {}
    if (!item.covered || asNumber(item.count) <= 0) {
      issues.push(issue(`missing_role_${role}`, 'P0', `1000W gold/source sheet-metal evidence is missing required role ${role}.`, {
        actual: asNumber(item.count),
      }))
    }
  }

  for (const doorClass of REQUIRED_DOOR_CLASSES) {
    const item = doorClassCoverage[doorClass] || {}
    if (!item.covered || asNumber(item.count) <= 0) {
      issues.push(issue(`missing_door_class_${doorClass.replace('/', '_')}`, 'P0', `1000W gold/source sheet-metal evidence is missing door class ${doorClass}.`, {
        actual: asNumber(item.count),
      }))
    }
  }

  if (asNumber(summary.hole_candidate_count) < 1000) {
    issues.push(issue('too_few_hole_candidates', 'P1', 'DXF evidence has too few extracted hole candidates for lock/hinge/interface rules.', {
      actual: asNumber(summary.hole_candidate_count),
      expectedMinimum: 1000,
    }))
  }

  const currentDoorPanelCount = dxfFiles.filter((item) => item.source_bucket === 'current_flat_pattern' && asArray(item.roles).includes('door_panel')).length
  if (currentDoorPanelCount < 6) {
    issues.push(issue('too_few_current_door_panel_dxfs', 'P1', 'Current flat-pattern folder has too few door-panel DXF rows.', {
      actual: currentDoorPanelCount,
      expectedMinimum: 6,
    }))
  }

  const usableRows = dxfFiles.filter((item) => String(item?.dxf?.quality_status || '') === 'usable_rule_evidence')
  if (usableRows.length < 80) {
    issues.push(issue('too_few_usable_rule_rows', 'P1', 'Too many DXF rows are limited by missing bbox or parse quality.', {
      actual: usableRows.length,
      expectedMinimum: 80,
    }))
  }

  return {
    schema: 'winnsen.locker16029.gold_sheetmetal_evidence_gate.v1',
    status: issues.length ? 'FAIL' : 'PASS',
    summary: {
      dxfCount: asNumber(summary.dxf_count),
      parsedDxfCount: asNumber(summary.parsed_dxf_count),
      currentFlatPatternCount: asNumber(sourceBuckets.current_flat_pattern),
      backupReferenceCount: asNumber(sourceBuckets.backup_reference),
      holeCandidateCount: asNumber(summary.hole_candidate_count),
      currentDoorPanelCount,
      usableRuleEvidenceRows: usableRows.length,
      issueCount: issues.length,
      p0Count: issues.filter((item) => item.severity === 'P0').length,
      p1Count: issues.filter((item) => item.severity === 'P1').length,
    },
    issues,
  }
}

function parseArgs(argv) {
  const args = {}
  for (let index = 0; index < argv.length; index += 1) {
    const item = argv[index]
    if (!item.startsWith('--')) continue
    if (index + 1 < argv.length && !argv[index + 1].startsWith('--')) {
      args[item.slice(2)] = argv[index + 1]
      index += 1
    } else {
      args[item.slice(2)] = true
    }
  }
  return args
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv)
  const evidencePath = args.evidence || 'data/locker_16029_gold_sheetmetal_evidence.json'
  const result = analyzeGoldSheetmetalEvidence(readJson(evidencePath))
  if (args.json) {
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
  } else {
    process.stdout.write(`16029 gold sheet-metal evidence gate: ${result.status}\n`)
    process.stdout.write(`DXF: ${result.summary.parsedDxfCount}/${result.summary.dxfCount}; holes: ${result.summary.holeCandidateCount}; issues: ${result.summary.issueCount}\n`)
    for (const item of result.issues) {
      process.stdout.write(`- ${item.severity} ${item.id}: ${item.message}\n`)
    }
  }
  if (result.status !== 'PASS') process.exitCode = 1
  return result
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
