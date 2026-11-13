import { readFileSync, writeFileSync, mkdirSync, mkdtempSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'

const runnerPath = fileURLToPath(import.meta.url)
const folder = dirname(runnerPath)
const repository = resolve(folder, '../../../../..')
const sourcePath = join(folder, 'source/NativeParametricDoorPlacement.cs')
const assemblyPath = join(folder, 'source/NativeParametricAssembly.cs')
const contextPath = join(folder, 'source/ParametricContext.cs')
const templatePath = join(folder, 'tests/RearWidthHarness.cs')
const fixturePaths = {
  rear: resolve(repository, 'data/indoor_workbench_repair_20261109/rear_hole_820_probe/geometry.json'),
  master: resolve(repository, 'data/indoor_workbench_repair_20261109/master_820_probe/geometry.json'),
  skin: resolve(repository, 'data/indoor_workbench_repair_20261109/skin_820_probe/geometry.json'),
  wide: resolve(repository, 'data/parametric_attempts/PARAM-20261109122246471-03695E/evidence/repair-engineer-feedback.json')
}
const compilerPath = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
const expectedCaseCount = 92
const startedAt = new Date().toISOString()
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase()
const snapshot = path => ({ path, bytes: readFileSync(path) })
const sourceFiles = [sourcePath, assemblyPath, contextPath, templatePath, runnerPath].map(snapshot)
const fixtureFiles = Object.values(fixturePaths).map(snapshot)
const source = sourceFiles[0].bytes.toString('utf8')
const assembly = sourceFiles[1].bytes.toString('utf8')
const context = sourceFiles[2].bytes.toString('utf8')
const parseFixture = key => JSON.parse(fixtureFiles.find(file => file.path === fixturePaths[key]).bytes.toString('utf8').replace(/^\uFEFF/, ''))
const extracted = []

function uniqueIndex(text, token) {
  const index = text.indexOf(token)
  if (index < 0 || text.indexOf(token, index + token.length) >= 0) throw new Error('source marker missing or ambiguous: ' + token)
  return index
}

function capture(text, path, start, end, name) {
  const value = text.slice(start, end)
  extracted.push({ name, path, startLine: text.slice(0, start).split('\n').length, endLine: text.slice(0, end).split('\n').length, sha256: hash(value) })
  return value
}

function method(text, path, signature) {
  const start = uniqueIndex(text, signature)
  const open = text.indexOf('{', start + signature.length)
  let depth = 0, state = 'code'
  for (let index = open; index < text.length; index++) {
    const char = text[index], next = text[index + 1]
    if (state === 'line') { if (char === '\n') state = 'code'; continue }
    if (state === 'block') { if (char === '*' && next === '/') { state = 'code'; index++ }; continue }
    if (state === 'string' || state === 'char') {
      if (char === '\\') { index++; continue }
      if (char === (state === 'string' ? '"' : "'")) state = 'code'
      continue
    }
    if (state === 'verbatim') {
      if (char === '"' && next === '"') { index++; continue }
      if (char === '"') state = 'code'
      continue
    }
    if (char === '/' && next === '/') { state = 'line'; index++; continue }
    if (char === '/' && next === '*') { state = 'block'; index++; continue }
    if (char === '@' && next === '"') { state = 'verbatim'; index++; continue }
    if (char === '"') { state = 'string'; continue }
    if (char === "'") { state = 'char'; continue }
    if (char === '{') depth++
    if (char === '}' && --depth === 0) return capture(text, path, start, index + 1, signature)
  }
  throw new Error('unclosed source method: ' + signature)
}

const rear = parseFixture('rear').events.find(event => event.inspection === 'rear_geometry')
const skins = parseFixture('skin').events.find(event => event.inspection === 'rear_skin_boundaries').skins
const master = parseFixture('master').events.find(event => event.inspection === 'master_hole_sketches').sketches.find(sketch => sketch.name === '草图123')
const wideRows = parseFixture('wide').rows
const widePair = wideRows.find(row => row.inspection === 'feedback_rear_hole_candidates')
const wideRetained = wideRows.find(row => row.inspection === 'feedback_retained_rear_holes')
if (rear.bodies.length !== 1 || rear.faces.length !== 137 || skins.length !== 2 || widePair.count !== 2) throw new Error('native fixture shape changed')
const matchedSkins = new Set()
const faces = rear.faces.map((face, index) => {
  const skinIndex = skins.findIndex(skin => face.box.every((value, axis) => Math.abs(value - skin.box[axis]) < 1e-9))
  if (skinIndex >= 0) {
    if (matchedSkins.has(skinIndex)) throw new Error('skin fixture maps to multiple original faces')
    matchedSkins.add(skinIndex)
  }
  return {
    name: 'native-820-face-' + index, cylinder: face.isCylinder, parameters: face.cylinder,
    plane: !face.isCylinder && Math.abs(face.box[5] - face.box[2]) < 1e-9,
    box: face.box, area: face.area, loops: skinIndex >= 0 ? skins[skinIndex].loops : null
  }
})
if (matchedSkins.size !== 2) throw new Error('skin fixture missing from complete rear face fixture')
const fixture = {
  bodyBoxes: rear.bodies.map(body => body.box), faces,
  widePair: widePair.cylinders, wideRetained: wideRetained.before,
  sourceHoleCenters: master.arcs.map(arc => arc.centerModel), sourceHoleRadii: master.arcs.map(arc => arc.radius)
}

const methodNames = ['FeedbackRearHoles', 'FeedbackRetainedRearHoles', 'FeedbackRearHolesOutsideWidth', 'FeedbackRetainedHolesMatch']
const declarations = [...source.matchAll(/private\s+static\s+[^\r\n{;]+?\s+(Feedback\w+)\s*\(/g)].map(match => ({ name: match[1], signature: match[0] }))
const methods = new Map()
function addMethod(name) {
  if (methods.has(name)) return
  const declaration = declarations.find(item => item.name === name)
  if (!declaration) throw new Error('required production helper missing: ' + name)
  const value = method(source, sourcePath, declaration.signature)
  methods.set(name, value)
  for (const helper of declarations) if (helper.name !== name && new RegExp('\\b' + helper.name + '\\s*\\(').test(value)) addMethod(helper.name)
}
for (const name of methodNames) addMethod(name)
const gateStart = uniqueIndex(source, 'ParametricContext.Require(holes.Count==(alreadyRepaired||outsideWidth?0:2),"FEEDBACK_REAR_HOLE_COUNT_DRIFT");')
const gateEnd = source.indexOf('if(!alreadyRepaired&&!outsideWidth) {', gateStart)
if (gateEnd < gateStart) throw new Error('production pre-deletion gate end missing')
const pairGate = capture(source, sourcePath, gateStart, gateEnd, 'production_count_full_circle_marker_signed_pair_gates')
for (const helper of declarations) if (new RegExp('\\b' + helper.name + '\\s*\\(').test(pairGate)) addMethod(helper.name)
function statement(token, name) {
  const start = uniqueIndex(source, token)
  const end = source.indexOf(';', start)
  if (end < 0) throw new Error('source statement end missing: ' + name)
  return capture(source, sourcePath, start, end + 1, name)
}
const replacements = {
  METHODS: [...methods.values()].join('\n\n') + '\n\n' + method(assembly, assemblyPath, 'internal static double[] BodyBox('),
  PAIR_GATE: pairGate,
  PACKAGE_MARKER_GATE: statement('ParametricContext.Require((outsideWidth?!repairedMarker:repairedMarker)&&FeedbackRearHoles(rear).Count==0,', 'production_package_marker_and_zero_target_gate'),
  PACKAGE_ACTION_GATE: statement('ParametricContext.Require(outsideWidth?repairAction==', 'production_package_action_count_tuple_gate'),
  REPAIR_RECORD: statement('records.Add(new{inspection="feedback_rear_hole_repair",', 'production_repair_action_evidence_record'),
  REQUIRE: method(context, contextPath, 'internal static void Require(')
}
let generated = sourceFiles[3].bytes.toString('utf8')
for (const [name, value] of Object.entries(replacements)) {
  const token = '/*__' + name + '__*/'
  if (generated.split(token).length !== 2) throw new Error('template token missing or ambiguous: ' + token)
  generated = generated.replace(token, () => value)
}
if (/\/\*__[A-Z_]+__\*\//.test(generated)) throw new Error('unfilled harness template token')

const evidenceRoot = resolve(repository, 'data/indoor_workbench_repair_20261109')
mkdirSync(evidenceRoot, { recursive: true })
const evidenceDirectory = mkdtempSync(join(evidenceRoot, 'rearwidth-tests-' + startedAt.replace(/[-:.]/g, '') + '-'))
const generatedPath = join(evidenceDirectory, 'RearWidthHarness.generated.cs')
const fixturePath = join(evidenceDirectory, 'fixtures.generated.json')
const executablePath = join(evidenceDirectory, 'RearWidthHarness.exe')
const evidencePath = join(evidenceDirectory, 'report.json')
writeFileSync(generatedPath, generated, { encoding: 'utf8', flag: 'wx' })
writeFileSync(fixturePath, JSON.stringify(fixture, null, 2) + '\n', { encoding: 'utf8', flag: 'wx' })
const args = ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', '/utf8output', '/reference:System.Web.Extensions.dll', '/out:' + executablePath, generatedPath]
const report = {
  schema: 'winnsen.rear_width_behavior_tests.v1', pass: false, startedAt,
  scope: 'Unmodified extracted production rear-hole selection, retained-hole matching, narrow-width geometry/skin guards, BodyBox and pre-deletion gates, compiled against in-process geometric API doubles.',
  sourceSha256: hash(sourceFiles[0].bytes),
  sourceArtifacts: sourceFiles.map(file => ({ path: file.path, sha256: hash(file.bytes) })),
  fixtureArtifacts: fixtureFiles.map(file => ({ path: file.path, sha256: hash(file.bytes) })),
  fixtureCoverage: { native820FaceCount: faces.length, native820BodyCount: rear.bodies.length, mappedSkinFaces: faces.map((face, index) => face.loops ? index : null).filter(index => index !== null), sourceRearSketchArcCount: master.arcs.length, wideNativeCandidateCount: widePair.count, wideNativeRetainedCount: wideRetained.before.length },
  extracted, extractedMethods: [...methods.keys()],
  generated: { path: generatedPath, sha256: hash(generated) },
  normalizedFixture: { path: fixturePath, sha256: hash(readFileSync(fixturePath)) },
  compiler: { path: compilerPath, sha256: hash(readFileSync(compilerPath)), args },
  limitations: [
    'This test does not open CAD, use COM, inspect or manage CAD processes, edit native files, select/delete faces, rebuild, save, or reopen a package.',
    'All 137 native 820 faces are retained. The two matching native rear-skin faces receive loops from the separate native skin probe; no substitute contour is generated for that fixture.',
    'Surface.IsPlane is inferred for noncylindrical faces whose native box has constant Z; Body.GetExtremePoint returns the corresponding recorded native body-box extent.',
    'The 1000 candidate parameters and boxes and eight retained rows are native evidence. Candidate cylinder area is synthesized as the full 0.8 mm by 2.75 mm cylinder because that evidence does not store candidate areas.',
    'Resized boundary fixtures and invalid geometries are synthetic mutations used only to exercise the unmodified production guards. They do not establish native CAD generation acceptance at those widths.'
  ]
}
const compiled = spawnSync(compilerPath, args, { cwd: evidenceDirectory, encoding: 'utf8', windowsHide: true, timeout: 30000 })
report.compile = { exitCode: compiled.status, stdout: compiled.stdout || '', stderr: compiled.stderr || '', error: compiled.error ? String(compiled.error) : null }
if (compiled.status === 0) {
  report.executable = { path: executablePath, sha256: hash(readFileSync(executablePath)) }
  const result = spawnSync(executablePath, [fixturePath], { cwd: evidenceDirectory, encoding: 'utf8', windowsHide: true, timeout: 30000 })
  report.execution = { command: [executablePath, fixturePath], exitCode: result.status, stdout: result.stdout || '', stderr: result.stderr || '', error: result.error ? String(result.error) : null }
  const marker = 'REAR_WIDTH_TEST_RESULT '
  const line = (result.stdout || '').split(/\r?\n/).find(value => value.startsWith(marker))
  if (line) report.behavior = JSON.parse(line.slice(marker.length))
}
report.sourcesUnchangedDuringRun = sourceFiles.every(file => hash(readFileSync(file.path)) === hash(file.bytes))
report.fixturesUnchangedDuringRun = fixtureFiles.every(file => hash(readFileSync(file.path)) === hash(file.bytes))
report.expectedCaseCount = expectedCaseCount
report.caseInventoryComplete = report.behavior?.cases?.length === expectedCaseCount && new Set(report.behavior.cases.map(value => value.name)).size === expectedCaseCount && report.behavior.cases.every(value => value.status === 'PASS')
report.finishedAt = new Date().toISOString()
report.pass = report.compile.exitCode === 0 && report.execution?.exitCode === 0 && report.behavior?.failed === 0 && report.behavior?.passed === expectedCaseCount && report.caseInventoryComplete && report.sourcesUnchangedDuringRun && report.fixturesUnchangedDuringRun
report.status = report.pass ? 'PASS' : 'FAIL'
writeFileSync(evidencePath, JSON.stringify(report, null, 2) + '\n', { encoding: 'utf8', flag: 'wx' })
process.stdout.write(JSON.stringify({ pass: report.pass, status: report.status, passed: report.behavior?.passed || 0, failed: report.behavior?.failed || 0, sourcesUnchangedDuringRun: report.sourcesUnchangedDuringRun, fixturesUnchangedDuringRun: report.fixturesUnchangedDuringRun, sourceSha256: report.sourceSha256, evidencePath }) + '\n')
if (!report.pass) {
  process.stderr.write(report.compile.stdout + report.compile.stderr + (report.execution?.stdout || '') + (report.execution?.stderr || ''))
  process.exitCode = 1
}
