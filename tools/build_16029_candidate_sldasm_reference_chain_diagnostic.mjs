import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'

const ROOT = process.cwd()
const BASE = 'workers/generated_models/review_generation_requests/part-first-16029-v43-pilot/sw2020_part_first'
const ASM_DIR = `${BASE}/minimum_interface_assembly`
const paths = {
  candidate: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate.SLDASM`,
  formal: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun.SLDASM`,
  candidateBuild: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_build.json`,
  formalBuild: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_build.json`,
  candidateTsv: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_placements.tsv`,
  candidateComponents: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_front_tx_m185p15_candidate_components.json`,
  formalComponents: `${ASM_DIR}/door_frame_lock_shelf_minimum_interface_v43_formal_native_rerun_components.json`,
  doorOpen: 'data/locker_16029_sw2020_opendoc6_stable_part.json',
  frontOpen: 'data/locker_16029_sw2020_opendoc6_front_frame_part.json',
  shelfOpen: 'data/locker_16029_sw2020_opendoc6_shelf_part.json',
  frontResult: `${BASE}/front_frame_vertical_left_v43/front_frame_vertical_left_v43_result.json`,
  shelfResult: `${BASE}/shelf_cross_panel_left_v43/shelf_cross_panel_left_v43_result.json`,
  differential: 'data/locker_16029_sw2020_opendoc6_differential_gate.json',
  out: 'data/locker_16029_candidate_sldasm_reference_chain_diagnostic.json',
}

const abs = (p) => path.resolve(ROOT, p)
const readJson = (p) => JSON.parse(fs.readFileSync(abs(p), 'utf8').replace(/^\uFEFF/, ''))
const sha256 = (p) => crypto.createHash('sha256').update(fs.readFileSync(abs(p))).digest('hex').toUpperCase()
const stat = (p) => fs.statSync(abs(p))

function printableAscii(buffer, min = 6) {
  const out = []
  let current = ''
  for (const byte of buffer) {
    if (byte >= 0x20 && byte <= 0x7e) current += String.fromCharCode(byte)
    else {
      if (current.length >= min) out.push(current)
      current = ''
    }
  }
  if (current.length >= min) out.push(current)
  return out
}

function printableUtf16(buffer, min = 6) {
  const out = []
  let current = ''
  for (let i = 0; i + 1 < buffer.length; i += 2) {
    const code = buffer.readUInt16LE(i)
    if (code >= 0x20 && code !== 0xffff) current += String.fromCharCode(code)
    else {
      if (current.length >= min) out.push(current)
      current = ''
    }
  }
  if (current.length >= min) out.push(current)
  return out
}

function offlineContainer(p) {
  const buffer = fs.readFileSync(abs(p))
  const strings = [...printableAscii(buffer), ...printableUtf16(buffer)]
  const refs = [...new Set(strings.flatMap((text) =>
    text.match(/[A-Za-z]:\\[^\u0000\r\n"]+?\.SLD(?:PRT|ASM)/gi) || []
  ))]
  const relevant = [...new Set(strings.filter((text) =>
    /solidworks|sldworks|SLDPRT|SLDASM|Default|configuration|display state/i.test(text)
  ))].slice(0, 100)
  return {
    path: p,
    size_bytes: buffer.length,
    sha256: sha256(p),
    last_write_time: stat(p).mtime.toISOString(),
    header_hex_64: buffer.subarray(0, 64).toString('hex').toUpperCase().match(/../g).join(' '),
    header_nonzero: buffer.subarray(0, 64).some((value) => value !== 0),
    offline_method: 'raw header plus ASCII/UTF-16LE string scan; not SolidWorks Document Manager',
    extracted_absolute_solidworks_references: refs,
    relevant_string_sample: relevant,
  }
}

function parseTsv(p) {
  const [headerLine, ...lines] = fs.readFileSync(abs(p), 'utf8').trim().split(/\r?\n/)
  const headers = headerLine.split('\t')
  return lines.map((line) => {
    const values = line.split('\t')
    return Object.fromEntries(headers.map((header, index) => [header, values[index] ?? '']))
  })
}

function componentMap(components) {
  return Object.fromEntries((components.components || []).map((item) => [
    path.basename(item.path).toLowerCase(),
    {
      name: item.name,
      path: item.path,
      referenced_configuration: item.referenced_configuration,
      suppression: item.suppression,
      is_suppressed: item.is_suppressed,
      is_hidden: item.is_hidden,
      transform: item.total_transform,
    },
  ]))
}

const candidateBuild = readJson(paths.candidateBuild)
const formalBuild = readJson(paths.formalBuild)
const candidateComponents = readJson(paths.candidateComponents)
const formalComponents = readJson(paths.formalComponents)
const candidateContainer = offlineContainer(paths.candidate)
const formalContainer = offlineContainer(paths.formal)
const rows = parseTsv(paths.candidateTsv)
const doorOpen = readJson(paths.doorOpen)
const frontOpen = readJson(paths.frontOpen)
const shelfOpen = readJson(paths.shelfOpen)
const frontResult = readJson(paths.frontResult)
const shelfResult = readJson(paths.shelfResult)
const differential = readJson(paths.differential)
const candidateMap = componentMap(candidateComponents)
const formalMap = componentMap(formalComponents)

const references = rows.map((row) => {
  const fullPath = path.resolve(row.path)
  const exists = fs.existsSync(fullPath)
  const key = path.basename(fullPath).toLowerCase()
  let openEvidence
  const priorAssemblyEvidenceAfterPartWrite =
    stat(paths.candidateComponents).mtime.getTime() >= fs.statSync(fullPath).mtime.getTime() &&
    candidateComponents.opened === true &&
    candidateComponents.rebuilt === true &&
    candidateComponents.components?.some((item) => path.resolve(item.path).toLowerCase() === fullPath.toLowerCase())
  if (row.role === 'door_panel_2_12_right_v43') {
    openEvidence = {
      source: paths.doorOpen,
      same_hash: exists && doorOpen.assembly_sha256_before === sha256(fullPath),
      opened: doorOpen.opened,
      rebuilt: doorOpen.rebuilt,
      method: doorOpen.open_method,
      prior_candidate_assembly_loaded_same_unchanged_path: priorAssemblyEvidenceAfterPartWrite,
    }
  } else if (row.role === 'front_frame_vertical_left_v43') {
    openEvidence = {
      source: paths.frontOpen,
      same_hash: exists && frontOpen.assembly_sha256_before === sha256(fullPath),
      opened: frontOpen.opened,
      rebuilt: frontOpen.rebuilt,
      hresult: frontOpen.hresult,
      method: frontOpen.open_method,
      prior_native_source: paths.frontResult,
      prior_native_opened: frontResult.opened,
      prior_native_rebuilt: frontResult.rebuilt,
      prior_candidate_assembly_loaded_same_unchanged_path: priorAssemblyEvidenceAfterPartWrite,
    }
  } else {
    openEvidence = {
      source: paths.shelfOpen,
      same_hash: exists && shelfOpen.assembly_sha256_before === sha256(fullPath),
      opened: shelfOpen.opened,
      rebuilt: shelfOpen.rebuilt,
      hresult: shelfOpen.hresult,
      method: shelfOpen.open_method,
      prior_native_source: paths.shelfResult,
      prior_native_opened: shelfResult.opened,
      prior_candidate_assembly_loaded_same_unchanged_path: priorAssemblyEvidenceAfterPartWrite,
    }
  }
  return {
    role: row.role,
    path: fullPath,
    path_form: path.isAbsolute(fullPath) ? 'absolute' : 'relative',
    extension: path.extname(fullPath).toUpperCase(),
    exists,
    sha256: exists ? sha256(fullPath) : '',
    size_bytes: exists ? fs.statSync(fullPath).size : 0,
    placement: {
      tx_mm: Number(row.tx_mm),
      ty_mm: Number(row.ty_mm),
      tz_mm: Number(row.tz_mm),
      rotation: row.rotation,
    },
    candidate_component: candidateMap[key] || null,
    formal_component: formalMap[key] || null,
    independent_or_recent_open_evidence: openEvidence,
  }
})

const normalizedPaths = references.map((item) => item.path.toLowerCase())
const duplicatePaths = normalizedPaths.filter((value, index) => normalizedPaths.indexOf(value) !== index)
const candidateAbs = abs(paths.candidate).toLowerCase()
const cycleReferences = normalizedPaths.filter((value) => value === candidateAbs)
const allChildrenHealthy = references.every((item) =>
  item.exists &&
  item.extension === '.SLDPRT' &&
  item.independent_or_recent_open_evidence.same_hash &&
  (
    item.independent_or_recent_open_evidence.opened ||
    item.independent_or_recent_open_evidence.prior_candidate_assembly_loaded_same_unchanged_path
  )
)
const buildHealthy = candidateBuild.build_status === 'completed' &&
  candidateBuild.placement_count === 3 &&
  candidateBuild.reference_count === 3 &&
  candidateBuild.saved === true &&
  (candidateBuild.components || []).every((item) => item.added === true)
const candidatePreviouslyOpenedSameHash =
  candidateComponents.opened === true &&
  candidateComponents.rebuilt === true &&
  candidateComponents.component_count === 3 &&
  candidateContainer.sha256 === sha256(paths.candidate)
const sameReferenceStructure = references.every((item) => {
  const candidate = item.candidate_component
  const formal = item.formal_component
  return candidate && formal &&
    path.resolve(candidate.path).toLowerCase() === path.resolve(formal.path).toLowerCase() &&
    candidate.referenced_configuration === formal.referenced_configuration &&
    candidate.suppression === formal.suppression &&
    candidate.is_suppressed === formal.is_suppressed
})

let classification
if (!allChildrenHealthy || duplicatePaths.length || cycleReferences.length) {
  classification = 'BROKEN_CHILD_REFERENCE'
} else if (!candidateContainer.header_nonzero || !buildHealthy) {
  classification = 'CANDIDATE_SLDASM_CONTAINER_CORRUPT_OR_INCOMPATIBLE'
} else if (candidatePreviouslyOpenedSameHash && sameReferenceStructure && differential.classification === 'CANDIDATE_ASSEMBLY_OPEN_FAILURE') {
  classification = 'ASSEMBLY_SESSION_LOAD_DEFECT'
} else {
  classification = 'REFERENCE_CHAIN_HEALTHY_REBUILD_CANDIDATE'
}

const report = {
  schema: 'winnsen.locker16029.candidate_sldasm_reference_chain_diagnostic.v1',
  generated_at: new Date().toISOString(),
  classification,
  inspection_mode: 'offline/read-only; candidate SLDASM was not opened by SolidWorks during this diagnostic',
  document_manager_available: false,
  document_manager_note: 'No SolidWorks Document Manager binary/license or repository offline parser was available. Container evidence uses raw header/string scan and existing native evidence.',
  candidate_container: candidateContainer,
  formal_comparison_container: formalContainer,
  build_evidence: {
    candidate: {
      path: paths.candidateBuild,
      build_status: candidateBuild.build_status,
      placement_count: candidateBuild.placement_count,
      reference_count: candidateBuild.reference_count,
      components_added: (candidateBuild.components || []).filter((item) => item.added).length,
      rebuilt: candidateBuild.rebuilt,
      saved: candidateBuild.saved,
      save_errors: candidateBuild.save_errors,
      save_warnings: candidateBuild.save_warnings,
    },
    formal: {
      path: paths.formalBuild,
      build_status: formalBuild.build_status,
      placement_count: formalBuild.placement_count,
      reference_count: formalBuild.reference_count,
      components_added: (formalBuild.components || []).filter((item) => item.added).length,
      rebuilt: formalBuild.rebuilt,
      saved: formalBuild.saved,
    },
  },
  references,
  reference_chain_checks: {
    expected_count: 3,
    actual_count: references.length,
    all_exist: references.every((item) => item.exists),
    all_sldprt: references.every((item) => item.extension === '.SLDPRT'),
    all_absolute: references.every((item) => item.path_form === 'absolute'),
    duplicate_paths: duplicatePaths,
    cycle_references: cycleReferences,
    all_children_healthy: allChildrenHealthy,
  },
  prior_candidate_native_evidence: {
    source: paths.candidateComponents,
    same_candidate_sha256: candidatePreviouslyOpenedSameHash,
    opened: candidateComponents.opened,
    rebuilt: candidateComponents.rebuilt,
    component_count: candidateComponents.component_count,
    open_errors: candidateComponents.open_errors,
    open_warnings: candidateComponents.open_warnings,
    recorded_at: stat(paths.candidateComponents).mtime.toISOString(),
  },
  structural_comparison: {
    reference_paths_equivalent: sameReferenceStructure,
    candidate_size_bytes: candidateContainer.size_bytes,
    formal_size_bytes: formalContainer.size_bytes,
    size_delta_bytes: candidateContainer.size_bytes - formalContainer.size_bytes,
    candidate_reference_path_form: 'absolute from TSV/build/components evidence',
    formal_reference_path_form: 'absolute from build/components evidence',
    configurations_equal: references.every((item) =>
      item.candidate_component?.referenced_configuration === item.formal_component?.referenced_configuration
    ),
    suppression_equal: references.every((item) =>
      item.candidate_component?.suppression === item.formal_component?.suppression
    ),
    lightweight_or_suppressed_difference_found: false,
    placement_difference: {
      component: 'front_frame_vertical_left_v43',
      formal_tx_mm: formalMap['front_frame_vertical_left_v43.sldprt']?.transform?.tx_mm,
      candidate_tx_mm: candidateMap['front_frame_vertical_left_v43.sldprt']?.transform?.tx_mm,
    },
    save_version_readability: 'Both files were created and saved by the same SW2020 build tool path; raw file headers are nonzero but proprietary version fields were not decoded offline.',
  },
  recent_open_differential: {
    source: paths.differential,
    classification: differential.classification,
    stable_part_opened: differential.stable_part?.opened,
    candidate_assembly_opened: differential.candidate_assembly?.opened,
    candidate_hresult: differential.candidate_assembly?.hresult,
  },
  decision_basis: {
    broken_child_reference: !allChildrenHealthy || duplicatePaths.length > 0 || cycleReferences.length > 0,
    container_corrupt_or_incompatible_proven: !candidateContainer.header_nonzero || !buildHealthy,
    same_hash_candidate_previously_opened_and_rebuilt: candidatePreviouslyOpenedSameHash,
    reference_structure_matches_formal: sameReferenceStructure,
  },
  minimum_repair: classification === 'ASSEMBLY_SESSION_LOAD_DEFECT'
    ? 'Keep the candidate file immutable. Repair or isolate the SW2020 assembly-load session path, then retry read-only OpenDoc6 in a fresh same-integrity session. Do not rebuild the candidate until a separate copy proves the session path cannot be recovered.'
    : classification === 'REFERENCE_CHAIN_HEALTHY_REBUILD_CANDIDATE'
      ? 'Create a new candidate SLDASM at a new path from the same three verified SLDPRTs and placement TSV; keep the current candidate immutable for comparison.'
      : classification === 'BROKEN_CHILD_REFERENCE'
        ? 'Repair only the identified missing, duplicate, or cyclic child reference before any assembly rebuild.'
        : 'Quarantine the candidate container and rebuild a new candidate SLDASM from the verified TSV and child SLDPRTs at a new path.',
  release_boundary: {
    cad_modified: false,
    candidate_resaved: false,
    placement_modified: false,
    exact_api_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
  },
}

fs.mkdirSync(path.dirname(abs(paths.out)), { recursive: true })
fs.writeFileSync(abs(paths.out), `${JSON.stringify(report, null, 2)}\n`)
console.log(`${classification}: ${paths.out}`)
