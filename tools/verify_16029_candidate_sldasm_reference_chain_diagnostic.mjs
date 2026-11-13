import fs from 'node:fs'
import path from 'node:path'

const reportPath = path.resolve(process.argv[2] || 'data/locker_16029_candidate_sldasm_reference_chain_diagnostic.json')
const gatePath = path.resolve(process.argv[3] || 'data/locker_16029_candidate_sldasm_reference_chain_gate.json')
const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'))
const allowed = [
  'BROKEN_CHILD_REFERENCE',
  'CANDIDATE_SLDASM_CONTAINER_CORRUPT_OR_INCOMPATIBLE',
  'ASSEMBLY_SESSION_LOAD_DEFECT',
  'REFERENCE_CHAIN_HEALTHY_REBUILD_CANDIDATE',
]
const checks = []
const check = (name, ok, actual, expected) => checks.push({ name, ok: Boolean(ok), actual, expected })

check('schema', report.schema === 'winnsen.locker16029.candidate_sldasm_reference_chain_diagnostic.v1', report.schema, 'reference chain schema')
check('classification_unique', allowed.includes(report.classification), report.classification, allowed)
check('candidate_metadata', /^[0-9A-F]{64}$/.test(report.candidate_container?.sha256 || '') && report.candidate_container?.size_bytes > 0, report.candidate_container, 'hash/size/time')
check('three_references', report.reference_chain_checks?.actual_count === 3, report.reference_chain_checks?.actual_count, 3)
check('references_exist', report.reference_chain_checks?.all_exist === true, report.reference_chain_checks?.all_exist, true)
check('references_are_parts', report.reference_chain_checks?.all_sldprt === true, report.reference_chain_checks?.all_sldprt, true)
check('no_duplicates', report.reference_chain_checks?.duplicate_paths?.length === 0, report.reference_chain_checks?.duplicate_paths, [])
check('no_cycles', report.reference_chain_checks?.cycle_references?.length === 0, report.reference_chain_checks?.cycle_references, [])
check('build_complete', report.build_evidence?.candidate?.placement_count === 3 && report.build_evidence?.candidate?.reference_count === 3 && report.build_evidence?.candidate?.components_added === 3 && report.build_evidence?.candidate?.saved === true, report.build_evidence?.candidate, '3/3/3/saved')
check('child_evidence', report.references?.every((item) =>
  item.independent_or_recent_open_evidence?.same_hash === true &&
  (
    item.independent_or_recent_open_evidence?.opened === true ||
    item.independent_or_recent_open_evidence?.prior_candidate_assembly_loaded_same_unchanged_path === true
  )
), report.references?.map((item) => item.independent_or_recent_open_evidence), 'same-hash current test plus successful unchanged-path history')
check('formal_comparison', report.structural_comparison?.reference_paths_equivalent === true && report.structural_comparison?.configurations_equal === true && report.structural_comparison?.suppression_equal === true, report.structural_comparison, 'same references/config/suppression')
check('prior_candidate_opened', report.prior_candidate_native_evidence?.same_candidate_sha256 === true && report.prior_candidate_native_evidence?.opened === true && report.prior_candidate_native_evidence?.rebuilt === true, report.prior_candidate_native_evidence, 'same hash opened/rebuilt')
check('read_only_boundary', report.release_boundary && Object.values(report.release_boundary).every((value) => value === false), report.release_boundary, 'all false')

const failed = checks.filter((item) => !item.ok)
const gate = {
  schema: 'winnsen.locker16029.candidate_sldasm_reference_chain_gate.v1',
  generated_at: new Date().toISOString(),
  status: failed.length === 0 ? 'PASS' : 'INCOMPLETE',
  classification: report.classification,
  checks_total: checks.length,
  checks_failed: failed.length,
  blocking_reasons: failed.map((item) => item.name),
  exact_api_resume_allowed: false,
  minimum_repair: report.minimum_repair,
  release_boundary: report.release_boundary,
  checks,
}

fs.writeFileSync(gatePath, `${JSON.stringify(gate, null, 2)}\n`)
console.log(`${gate.status} ${gate.classification}: ${path.relative(process.cwd(), gatePath)}`)
process.exitCode = failed.length === 0 ? 0 : 1
