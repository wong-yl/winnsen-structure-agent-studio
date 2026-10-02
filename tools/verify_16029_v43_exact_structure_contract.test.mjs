import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { analyzeV43ExactStructureContract } from './verify_16029_v43_exact_structure_contract.mjs'

const contract = JSON.parse(readFileSync(new URL('../configs/locker_16029_v43_exact_structure_contract.json', import.meta.url), 'utf8'))

function component(name, depth = 0, box = null, path = '') {
  return {
    name,
    path: path || `C:\\candidate\\${name.replace(/-\d+$/, '')}.SLDPRT`,
    depth,
    is_hidden: false,
    is_suppressed: false,
    box,
  }
}

function shelf(name, centerYmm) {
  return component(name, 0, { ymin_mm: centerYmm - 5, ymax_mm: centerYmm + 5 })
}

function validRecord() {
  const components = [
    ...contract.expected.topLevelDoorModules.names.map((name) => component(`${name}-1`, 0, null, `C:\\candidate\\${name}.SLDASM`)),
    ...contract.expected.topLevelDoorModules.names.map((name) => component(`${name}-1/锁舌-1`, 1, null, 'C:\\candidate\\锁舌.SLDPRT')),
    ...Array.from({ length: 6 }, (_, index) => component(`锁孔基准_row_${index + 1}-1`)),
    component('锁控维护条_源钣金-1'),
    component('箱体左侧板焊接-1'),
    component('箱体右侧板焊接-1'),
    component('箱体竖隔板L焊接-1'),
    component('箱体竖隔板R焊接-1'),
    shelf('箱体横层板L焊接_a-1', 948),
    shelf('箱体横层板L焊接_b-1', 1558),
    shelf('箱体横层板R焊接_a-1', 338),
    shelf('箱体横层板R焊接_b-1', 948),
  ]
  return { assembly_path: 'C:\\candidate\\full.SLDASM', component_count: components.length, components }
}

const passResult = analyzeV43ExactStructureContract({ structureRecord: validRecord(), contract })
assert.equal(passResult.status, 'PASS')
assert.equal(passResult.classification, 'controlled_candidate_pass')
assert.equal(passResult.releaseEligible, false)

const duplicateDatumRecord = validRecord()
duplicateDatumRecord.components.push(component('锁孔基准_legacy-1'))
const duplicateDatumResult = analyzeV43ExactStructureContract({ structureRecord: duplicateDatumRecord, contract })
assert.equal(duplicateDatumResult.status, 'FAIL')
assert.ok(duplicateDatumResult.failedChecks.some((item) => item.id === 'lock_hole_datums_exact'))

const historicalShelfRecord = validRecord()
historicalShelfRecord.components.push(shelf('箱体横层板L焊接_historical-1', 648))
const historicalShelfResult = analyzeV43ExactStructureContract({ structureRecord: historicalShelfRecord, contract })
assert.equal(historicalShelfResult.status, 'FAIL')
assert.ok(historicalShelfResult.failedChecks.some((item) => item.id === 'shelf_bands_l_exact'))

console.log('verify_16029_v43_exact_structure_contract tests: PASS')
