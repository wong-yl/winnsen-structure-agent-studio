import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve('.')
const scriptPath = resolve(root, 'tools/collect_16029_topcover_rebuild_dxf.py')
const source = readFileSync(scriptPath, 'utf8')

const processSnapshot = () => {
  const result = spawnSync('powershell.exe', ['-NoProfile', '-Command',
    "@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ','"],
  { cwd: root, encoding: 'utf8', windowsHide: true })
  assert.equal(result.status, 0)
  const text = result.stdout.trim()
  return text ? text.split(',') : []
}

const checks = {}
checks.cad_processes_absent_before = processSnapshot().length === 0
checks.collector_has_fixed_source_and_create_new_output =
  source.includes('16029_金标准原始素材_U盘_20260526') &&
  source.includes('os.O_EXCL') && source.includes('output.stat().st_nlink != 1')
checks.collector_pins_exact_five_hashes =
  (source.match(/[A-F0-9]{64}/g) ?? []).length >= 5 &&
  source.includes('EXPECTED_FILES') && source.includes('len(files) == 5')
checks.collector_preserves_lossless_raw_groups =
  source.includes('"rawGroups"') && source.includes('canonicalGeometrySha256')

const run = spawnSync('python', [scriptPath, '--verify-only'], {
  cwd: root,
  encoding: 'utf8',
  windowsHide: true,
  timeout: 30_000,
})
checks.verify_only_exits_zero = run.status === 0 && run.stderr.trim() === ''
const report = run.status === 0 ? JSON.parse(run.stdout) : {}
checks.verify_only_exact_five_complete = report.status === 'PASS' &&
  report.capturePass === true && report.fileCount === 5
checks.entity_counts_are_exact = JSON.stringify(report.entityCounts) === JSON.stringify({
  '上盖壳体左侧板展开图.DXF': 84,
  '上盖壳体右侧板展开图.DXF': 84,
  '上盖壳体前侧板展开图.DXF': 34,
  '上盖壳体后侧板展开图.DXF': 91,
  '上盖壳体底板展开图.DXF': 84,
})
checks.geometry_signatures_are_exact = JSON.stringify(report.canonicalGeometrySha256) ===
  JSON.stringify({
    '上盖壳体左侧板展开图.DXF': '3EB9D097FDEE6687CFD0E71AF48FFCCE55FA44039042539FB52844B814AB5F82',
    '上盖壳体右侧板展开图.DXF': '5F666F6826054FD4E3B2C498E9F579FA2FA7D64234F696825E179E03DDF5A5D4',
    '上盖壳体前侧板展开图.DXF': 'BE669CE428A3D7EAF629B7A54C91C416F0D8B79E658BBD60E9700C0E5120DF94',
    '上盖壳体后侧板展开图.DXF': '23C5D30F66C039AEE3948445B625699DCD20789E295238E7A0D912F03755DDEC',
    '上盖壳体底板展开图.DXF': '346F032C4CE8EE3F35B700286B4B1864DE906DE42EF19FECEC25EF5A2896A9CA',
  })
checks.cad_processes_absent_after = processSnapshot().length === 0

const failed = Object.entries(checks).filter(([, ok]) => !ok)
process.stdout.write(`${JSON.stringify({
  status: failed.length ? 'FAIL' : 'PASS',
  checksTotal: Object.keys(checks).length,
  checksFailed: failed.length,
  checks,
}, null, 2)}\n`)
if (failed.length) process.exitCode = 1
