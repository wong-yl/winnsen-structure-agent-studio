import test from 'node:test'
import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import {
  auditFlatDxfDirectory,
  extractInstalledPartSummary,
} from './portal_pipeline.mjs'

function makeValidFlatDxf(width = 100, height = 50, { duplicateCircle = false, insunits = 4 } = {}) {
  const lines = [
    '0', 'SECTION', '2', 'HEADER',
    '9', '$INSUNITS', '70', String(insunits),
    '0', 'ENDSEC',
    '0', 'SECTION', '2', 'TABLES',
    '0', 'TABLE', '2', 'LAYER', '70', '3',
    '0', 'LAYER', '2', 'CUT', '70', '0', '62', '7', '6', 'CONTINUOUS',
    '0', 'LAYER', '2', 'BEND', '70', '0', '62', '3', '6', 'CONTINUOUS',
    '0', 'LAYER', '2', 'MARK', '70', '0', '62', '5', '6', 'CONTINUOUS',
    '0', 'ENDTAB',
    '0', 'ENDSEC',
    '0', 'SECTION', '2', 'ENTITIES',
    '0', 'LINE', '8', 'CUT', '10', '0', '20', '0', '11', String(width), '21', '0',
    '0', 'LINE', '8', 'CUT', '10', String(width), '20', '0', '11', String(width), '21', String(height),
    '0', 'LINE', '8', 'CUT', '10', String(width), '20', String(height), '11', '0', '21', String(height),
    '0', 'LINE', '8', 'CUT', '10', '0', '20', String(height), '11', '0', '21', '0',
    '0', 'CIRCLE', '8', 'CUT', '10', String(width / 2), '20', String(height / 2), '40', '4.4',
    '0', 'LINE', '8', 'BEND', '10', '10', '20', '0', '11', '10', '21', String(height),
    '0', 'LINE', '8', 'MARK', '10', '20', '20', '15', '11', '30', '21', '15',
  ]
  if (duplicateCircle) {
    lines.push('0', 'CIRCLE', '8', 'CUT', '10', String(width / 2), '20', String(height / 2), '40', '4.4')
  }
  lines.push('0', 'ENDSEC', '0', 'EOF')
  return lines.join('\r\n') + '\r\n'
}

function writeDxf(rootDir, relPath, content) {
  const full = join(rootDir, relPath)
  mkdirSync(dirname(full), { recursive: true })
  writeFileSync(full, content, 'utf8')
  const sha = createHash('sha256').update(readFileSync(full)).digest('hex').toUpperCase()
  return { full, sha }
}

test('auditFlatDxfDirectory passes when installed sheet-metal set matches 1-to-1 and filters uninstalled DXFs', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-ok-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))
    const b = writeDxf(dir, 'structure\\上盖壳体底板.flat.dxf', makeValidFlatDxf(300, 150))
    const uninstalled = writeDxf(dir, 'structure\\uninstalled_seed.flat.dxf', makeValidFlatDxf(180, 90))

    const expectedParts = [
      { relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: a.full, sha256: a.sha },
      { relPath: 'structure\\上盖壳体底板.sldprt', classification: 'sheet_metal', flatPath: b.full, sha256: b.sha },
      { relPath: 'parts\\trial_circlip.SLDPRT', classification: 'non_sheet_metal', flatPath: '', sha256: '' },
      { relPath: 'structure\\uninstalled_seed.SLDPRT', classification: 'sheet_metal', flatPath: uninstalled.full, sha256: uninstalled.sha },
    ]
    const installedRelPaths = new Set([
      'parts\\L_panel.SLDPRT',
      'structure\\上盖壳体底板.sldprt',
      'parts\\trial_circlip.SLDPRT',
    ])

    const res = auditFlatDxfDirectory(dir, expectedParts, { installedRelPaths })
    assert.equal(res.pass, true)
    assert.equal(res.identityPass, true)
    assert.equal(res.expectedCountMatches, true)
    assert.equal(res.sheetMetalCount, 2)
    assert.equal(res.dxfFileCount, 2)
    assert.equal(res.dxfAuditPassCount, 2)
    assert.equal(res.dxfAuditFailCount, 0)
    assert.equal(res.identityMatchedCount, 2)
    assert.deepEqual(res.missingExportRecords, [])
    assert.deepEqual(res.missingDxfFiles, [])
    assert.deepEqual(res.unexpectedDxfFiles, [])
    assert.deepEqual(res.duplicateMappings, [])
    assert.deepEqual(res.shaMismatches, [])
    assert.deepEqual(res.excludedUninstalledFiles, ['structure\\uninstalled_seed.flat.dxf'])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('auditFlatDxfDirectory rejects same-count swap where one expected part is missing and another geometry-valid DXF is substituted', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-swap-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))
    // Expected part B is NOT written; instead an unexpected geometry-valid DXF for an installed non-sheet-metal part (or unrecorded part) is placed on disk so total count stays 2!
    const swapped = writeDxf(dir, 'parts\\trial_circlip.flat.dxf', makeValidFlatDxf(140, 80))
    const missingBSha = 'A'.repeat(64)

    const expectedParts = [
      { relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: a.full, sha256: a.sha },
      { relPath: 'structure\\上盖壳体底板.sldprt', classification: 'sheet_metal', flatPath: join(dir, 'structure\\上盖壳体底板.flat.dxf'), sha256: missingBSha },
      { relPath: 'parts\\trial_circlip.SLDPRT', classification: 'non_sheet_metal', flatPath: '', sha256: '' },
    ]
    const installedRelPaths = new Set([
      'parts\\L_panel.SLDPRT',
      'structure\\上盖壳体底板.sldprt',
      'parts\\trial_circlip.SLDPRT',
    ])

    const res = auditFlatDxfDirectory(dir, expectedParts, { installedRelPaths })
    assert.equal(res.dxfFileCount, 2, 'delivered count equals expected sheet-metal count (2)')
    assert.equal(res.sheetMetalCount, 2)
    assert.equal(res.pass, false, 'same-count swap must be rejected')
    assert.equal(res.identityPass, false)
    assert.equal(res.missingDxfFiles.length, 1)
    assert.equal(res.missingDxfFiles[0].relPath, 'structure\\上盖壳体底板.sldprt')
    assert.equal(res.unexpectedDxfFiles.length, 1)
    assert.equal(res.unexpectedDxfFiles[0].dxfRelPath, 'parts\\trial_circlip.flat.dxf')
    const swappedRow = res.results.find((r) => r.flatPath === swapped.full)
    assert.ok(swappedRow)
    assert.equal(swappedRow.geometryPassed, true, 'substituted DXF itself is geometrically valid')
    assert.equal(swappedRow.recordMatched, false)
    assert.equal(swappedRow.shaMatches, false, 'missing export record must NEVER mark shaMatches=true')
    assert.equal(swappedRow.auditPassed, false)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('auditFlatDxfDirectory rejects duplicate mappings in expected export records', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-dup-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))
    const expectedParts = [
      { relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: a.full, sha256: a.sha },
      { relPath: 'parts/l_panel.sldprt', classification: 'sheet_metal', flatPath: a.full, sha256: a.sha },
    ]

    const res = auditFlatDxfDirectory(dir, expectedParts)
    assert.equal(res.pass, false)
    assert.equal(res.identityPass, false)
    assert.ok(res.duplicateMappings.length >= 1)
    assert.equal(res.results[0].duplicateMapping, true)
    assert.equal(res.results[0].auditPassed, false)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('auditFlatDxfDirectory rejects SHA-256 mismatch even when path and geometry are valid', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-sha-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))
    const wrongSha = a.sha.startsWith('0') ? ('1' + a.sha.slice(1)) : ('0' + a.sha.slice(1))
    const expectedParts = [
      { relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: a.full, sha256: wrongSha },
    ]

    const res = auditFlatDxfDirectory(dir, expectedParts)
    assert.equal(res.pass, false)
    assert.equal(res.identityPass, false)
    assert.equal(res.shaMismatches.length, 1)
    assert.equal(res.results[0].geometryPassed, true)
    assert.equal(res.results[0].shaMatches, false)
    assert.equal(res.results[0].auditPassed, false)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('auditFlatDxfDirectory rejects missing export record for installed part and empty expected manifest', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-missing-rec-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))

    // Empty expected manifest must fail even for geometrically valid DXF
    const emptyRes = auditFlatDxfDirectory(dir, [])
    assert.equal(emptyRes.pass, false)
    assert.equal(emptyRes.identityPass, false)
    assert.ok(emptyRes.identityErrors.includes('EXPECTED_SHEET_METAL_MANIFEST_EMPTY'))

    // Installed part omitted from expectedParts must fail
    const missingRecRes = auditFlatDxfDirectory(
      dir,
      [{ relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: a.full, sha256: a.sha }],
      { installedRelPaths: new Set(['parts\\L_panel.SLDPRT', 'structure\\missing_record.sldprt']) },
    )
    assert.equal(missingRecRes.pass, false)
    assert.equal(missingRecRes.missingExportRecords.length, 1)
    assert.equal(missingRecRes.missingExportRecords[0].relPath, 'structure\\missing_record.sldprt')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('auditFlatDxfDirectory rejects record flatPath pointing to a different part identity and preserves geometry failure checks', () => {
  const dir = mkdtempSync(join(tmpdir(), 'vt-dxf-1to1-path-geom-'))
  try {
    const a = writeDxf(dir, 'parts\\L_panel.flat.dxf', makeValidFlatDxf(200, 120))
    const b = writeDxf(dir, 'parts\\R_panel.flat.dxf', makeValidFlatDxf(200, 120, { duplicateCircle: true }))

    const mismatchRes = auditFlatDxfDirectory(dir, [
      { relPath: 'parts\\L_panel.SLDPRT', classification: 'sheet_metal', flatPath: b.full, sha256: a.sha },
      { relPath: 'parts\\R_panel.SLDPRT', classification: 'sheet_metal', flatPath: b.full, sha256: b.sha },
    ])
    assert.equal(mismatchRes.pass, false)
    assert.equal(mismatchRes.pathIdentityMismatches.length, 1)
    const rowB = mismatchRes.results.find((r) => r.dxfRelPath === 'parts\\R_panel.flat.dxf')
    assert.equal(rowB.geometryPassed, false, 'duplicate circle geometry check must remain active')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('FE4372 prototype ZIP 34 DXFs pass strict 1-to-1 identity and geometry verification', (t) => {
  const zipPath = 'D:\\Winnsen_Structure_Agent_Studio\\data\\variable_trial_portal_tasks\\VT16029-20261117T112858Z-FE4372\\16029_全参数_860W_1800H_450D_L6-R5_3D与DXF_VT16029-20261117T112858Z-FE4372.zip'
  const reportPath = 'D:\\Winnsen_Structure_Agent_Studio\\data\\variable_trial_portal_tasks\\VT16029-20261117T112858Z-FE4372\\report.json'
  if (!existsSync(zipPath) || !existsSync(reportPath)) {
    t.skip('FE4372 local prototype archive not present in this environment')
    return
  }
  const taskReport = JSON.parse(readFileSync(reportPath, 'utf8'))
  const dxfNativePath = join(taskReport.dxfRunDir, 'native-result.json')
  if (!existsSync(dxfNativePath)) {
    t.skip('FE4372 dxfRunDir native-result.json not present')
    return
  }

  const tmpExtract = mkdtempSync(join(tmpdir(), 'vt-fe4372-dxf-verify-'))
  try {
    const unzip = spawnSync('powershell.exe', [
      '-NoProfile',
      '-Command',
      'Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory($env:VT_ZIP, $env:VT_OUT, [System.Text.Encoding]::UTF8)',
    ], {
      env: { ...process.env, VT_ZIP: zipPath, VT_OUT: tmpExtract },
      windowsHide: true,
      encoding: 'utf8',
    })
    assert.equal(unzip.status, 0, unzip.stderr)

    const asm = JSON.parse(readFileSync(join(tmpExtract, 'reports', 'assembly-evidence.json'), 'utf8'))
    const dxfNative = JSON.parse(readFileSync(dxfNativePath, 'utf8'))
    const dxfEvent = [...(dxfNative.events || [])].reverse().find((e) => e.inspection === 'assembly_flat_dxfs')
    const installedSummary = extractInstalledPartSummary(asm)

    const res = auditFlatDxfDirectory(
      join(tmpExtract, 'DXF_展开图'),
      dxfEvent.result.parts,
      { installedRelPaths: installedSummary.installedRelPaths },
    )
    assert.equal(res.pass, true, JSON.stringify(res.identityErrors))
    assert.equal(res.identityPass, true)
    assert.equal(res.sheetMetalCount, 34)
    assert.equal(res.dxfFileCount, 34)
    assert.equal(res.identityMatchedCount, 34)
    assert.equal(res.dxfAuditPassCount, 34)
    assert.equal(res.dxfAuditFailCount, 0)
    assert.deepEqual(res.missingExportRecords, [])
    assert.deepEqual(res.missingDxfFiles, [])
    assert.deepEqual(res.unexpectedDxfFiles, [])
    assert.deepEqual(res.duplicateMappings, [])
    assert.deepEqual(res.pathIdentityMismatches, [])
    assert.deepEqual(res.shaMismatches, [])
  } finally {
    rmSync(tmpExtract, { recursive: true, force: true })
  }
})
