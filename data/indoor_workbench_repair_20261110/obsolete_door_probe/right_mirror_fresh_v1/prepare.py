"""Throwaway preparation. Writes only this owned directory; never runs native CAD."""
import sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import datetime, hashlib, json, re
from pathlib import Path

ROOT = Path(r'D:\Winnsen_Structure_Agent_Studio')
HERE = Path(__file__).resolve().parent
PROBE = HERE.parent
PRIOR = PROBE / 'right_identity_v1'
SOURCE = ROOT / 'workers/native_model_requests/parametric_v1/repairs/readonly-open-20261110-r4-doorprecision3/source'
ATTEMPT = ROOT / 'data/parametric_attempts/PARAM-20261110111643911-CE439B'
assert HERE == PROBE / 'right_mirror_fresh_v1'
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()
def load(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def pin(path): return dict(path=str(path), sha256=sha(path), size=Path(path).stat().st_size)
def write(name, text):
    with (HERE / name).open('x', encoding='utf-8', newline='') as stream: stream.write(text)
def put(name, value): write(name, json.dumps(value, ensure_ascii=False, indent=2) + '\n')
def text(path): return Path(path).read_bytes().decode('utf-8-sig')
def change(value, old, new):
    assert old in value, old
    return value.replace(old, new)
pins = {'NativeParametricLocks.cs': '467D217D798F02ACB9FE5CFA8584DD272444E7F210F1B58A03D933FE0CB56EC6', 'NativeParametricLockBase.cs': 'C3EC81564C536E341053A97FDAD6088E6BC8256FAF16A78933733E630AD09586', 'NativeParametricAssembly.cs': '751C2BF8718AA087CB527E99834042E82D87A5F027D6A23AA04703FB7B52E937'}
for name, expected in pins.items(): assert sha(SOURCE / name) == expected, name
chunks = []
def block(path, token, end='        }'):
    value = text(path); lines = value.splitlines(keepends=True)
    starts = [i for i, line in enumerate(lines) if token in line]
    assert len(starts) == 1, token
    start = starts[0]
    finish = next(i for i in range(start + 1, len(lines)) if lines[i].rstrip('\r\n') == end)
    raw = ''.join(lines[start:finish+1])
    chunks.append(dict(source=pin(path), token=token, firstLine=start+1, lastLine=finish+1, textSha256=hashlib.sha256(raw.encode('utf-8')).hexdigest().upper(), exactText=raw))
    return raw
base = SOURCE / 'NativeParametricLockBase.cs'
methods = ['CaptureGeometry(', 'CaptureFeatureHealth(', 'AnalyzeGeometry(', 'AnalyzeSlot(', 'AnalyzeCircle(', 'GeometryGate(', 'BasicPartGate(', 'MirrorGeometryGate(', 'EdgeMatches(', 'DerivedRows(', 'CircleMatches(', 'NormalizedEdgeSignature(', 'PointSignature(', 'NormalizeVertex(', 'PointMatches(', 'DistinctRows(', 'VertexMm(', 'ToPointMm(', 'ToNumbers(', 'RowsEqual(', 'T(string', 'Round(', 'Close(', 'Sha256Text(', 'Sha256Bytes(', 'BytesToHex(', 'Safe<T>(', 'Release(object', 'SelectRightPlane(']
body = []
for token in methods:
    signature = next(line.strip() for line in text(base).splitlines() if line.startswith('        private static ') and token in line)
    body.append(block(base, '        ' + signature))
for name in ['SlotTemplate', 'EdgeRow', 'SlotAnalysis', 'CircleAnalysis', 'GeometrySnapshot']:
    body.append(block(base, '        public sealed class ' + name))
for name in ['RowToleranceMm', 'CoordinateToleranceMm', 'LengthToleranceMm', 'CircleLengthMm']:
    body.insert(0, next(line for line in text(base).splitlines(keepends=True) if 'private const double ' + name + ' ' in line))
for token in ['private static double[] ExpectedSlots', 'private static double[] ExpectedCircles']:
    body.insert(0, next(line for line in text(base).splitlines(keepends=True) if token in line))
body.insert(0, block(base, '        private static readonly SlotTemplate[] SlotTemplates =', '        };'))
wrappers = '''
        private static double[] ActiveRows;
        internal static GeometrySnapshot ResearchCapture(ModelDoc2 model, string side, double[] rows) { ActiveRows = rows; return CaptureGeometry(model, side); }
        internal static bool ResearchGeometryGate(GeometrySnapshot value, string side, double[] rows) { ActiveRows = rows; return GeometryGate(value, side); }
        internal static bool ResearchMirrorGeometryGate(GeometrySnapshot value, double[] rows) { ActiveRows = rows; return MirrorGeometryGate(value, "R"); }
        internal static bool ResearchSelectRightPlane(ModelDoc2 model) { return SelectRightPlane(model); }
'''
original = 'using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Globalization;\nusing System.Runtime.InteropServices;\nusing System.Security.Cryptography;\nusing System.Text;\nusing SolidWorks.Interop.sldworks;\nusing SolidWorks.Interop.swconst;\nnamespace Winnsen.StructureAgent.Native888 { internal static partial class NativeParametricLocks {\n' + ''.join(body) + wrappers + '\n} }\n'
reflected = block(SOURCE / 'NativeParametricAssembly.cs', '        internal static void VerifyReflectedVertices(')
original += 'namespace Winnsen.StructureAgent.NativeDoorModule888x14 { internal static class NativeParametricDoor {\n' + reflected + '\n        private static void Require(bool condition, string code, int exitCode, string message) { if (!condition) throw new InvalidOperationException(code + " " + message); }\n} }\n'
write('OriginalGeometry.cs', original)
put('extraction.json', dict(schema='right-mirror-original-methods-v1', sources=[pin(SOURCE/name) for name in pins], chunks=chunks, generated=pin(HERE/'OriginalGeometry.cs'), nativeCadExecuted=False, modelAccepted=False))
binding = load(PRIOR/'source-binding.json')
binding['schema'] = 'right-mirror-fresh-source-binding-v1'
binding['generatedAtUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
binding['originalSourcePins'] = [pin(SOURCE/name) for name in pins]
binding['plan'] = pin(ATTEMPT/'parametric_execution.json')
evidence = load(binding['locksEvidence']['path'])
bend = [row for row in evidence['rows'] if 'v35BendRadiusMm' in row]
assert len(bend) == 1 and bend[0]['side'] == 'R'
binding['bend'] = bend[0]
binding['rightRows'] = [row['centerYmm'] for row in load(binding['plan']['path'])['contract']['rowsByColumn']['R']]
assert len(binding['rightRows']) == 9
binding['templatePins'] += [pin(PRIOR/name) for name in ['RightIdentityProbe.cs','run-right-identity.mjs','observe-right-identity.py','static-check.ps1','tests-offline.mjs','freeze-manifest.json']]
put('source-binding.json', binding)
probe = text(PRIOR/'RightIdentityProbe.cs')
probe = change(probe, 'RightIdentityProbe', 'RightMirrorFreshProbe')
probe = change(probe, 'right_identity_v1', 'right_mirror_fresh_v1')
probe = change(probe, 'right-identity', 'right-mirror-fresh')
probe = change(probe, '"right-only", "parameter-source-only", "left-close-right"', '"mirror-create", "temporary-cold", "final-cold"')
probe = change(probe, 'public sealed class Binding { public string schema, sourceAttempt; public FilePin locksEvidence, right, parameterSource, left; public FilePin[] inventory; }', 'public sealed class Bend { public double v35BendRadiusMm, v35ThicknessMm, v35KFactor, v35BendAllowance, v35ReliefRatio; public bool v35AutoRelief; public double[] v35FixedPlane; }\n    public sealed class Binding { public string schema, sourceAttempt; public FilePin locksEvidence, right, parameterSource, left, plan; public FilePin[] inventory, originalSourcePins; public double[] rightRows; public Bend bend; }\n    private static string Candidate, CandidateSha;')
probe = change(probe, '"OUTPUT_NOT_NEW_OWNED_RUN"', '"OUTPUT_NOT_NEW_OWNED_RUN"')
probe = change(probe, 'private static bool Bound(string path) { try { return Known.ContainsKey(Full(path)); } catch { return false; } }', 'private static bool Bound(string path) { try { return Known.ContainsKey(Full(path)) || (Candidate != null && Same(path, Candidate)); } catch { return false; } }')
probe = change(probe, 'rows.Add(Identity(value as ModelDoc2));', 'rows.Add(Identity(value as ModelDoc2));')
probe = change(probe, '"unknownPathsAccepted", false));\n    }\n    private static object References', '"unknownPathsAccepted", false));\n        foreach (object value in Values(raw)) { ModelDoc2 doc = value as ModelDoc2; Require(doc != null && Bound(doc.GetPathName()), "LOADED_DOCUMENT_OUTSIDE_FIXED_INVENTORY_OR_CANDIDATE"); }\n    }\n    private static object References')
probe = change(probe, 'Require(model != null && errors == 0 && model.IsOpenedReadOnly(), "READ_ONLY_OPEN_FAILED " + stage);', 'Require(model != null && errors == 0 && warnings == 0 && model.IsOpenedReadOnly() && Same(model.GetPathName(), path) && !model.GetSaveFlag(), "STRICT_COLD_OPEN_FAILED " + stage + " " + errors + "/" + warnings);')
start = probe.index('    private static string[] ArmSequence(')
finish = probe.index('    private static void CheckPin(', start)
probe = probe[:start] + probe[finish:]
probe = change(probe, 'Require(Hash(Input.locksEvidence.path) == LocksSha, "LOCKS_EVIDENCE_CHANGED");', 'Require(Hash(Input.locksEvidence.path) == LocksSha, "LOCKS_EVIDENCE_CHANGED");\n        foreach (FilePin pin in Input.originalSourcePins) Require(Hash(pin.path) == pin.sha256, "ORIGINAL_SOURCE_CHANGED " + pin.path);\n        Require(Hash(Input.plan.path) == Input.plan.sha256 && Input.rightRows.Length == 9 && Input.bend != null && Input.bend.v35FixedPlane.Length == 6, "FROZEN_PLAN_OR_BEND_BINDING_CHANGED");')
probe = change(probe, 'args.Length != 4', 'args.Length != 6')
probe = change(probe, '<right-only|parameter-source-only|left-close-right> <newOutput> <source-binding.json> <bindingSha>', '<mirror-create|temporary-cold|final-cold> <newOutput> <source-binding.json> <bindingSha> <candidate> <candidateShaOrNew>')
probe = change(probe, 'LoadBindings(args[2], args[3]); Require', 'LoadBindings(args[2], args[3]); Candidate = Full(args[4]); CandidateSha = args[5]; Require(Inside(Path.Combine(OwnedRoot, "runs"), Candidate) && Directory.Exists(Path.GetDirectoryName(Candidate)) && !Known.ContainsKey(Candidate), "CANDIDATE_NOT_OWNED_NEW_RUN"); Plain(Candidate);\n            if (args[0] == "mirror-create") Require(CandidateSha == "new" && !File.Exists(Candidate), "MIRROR_TARGET_ALREADY_EXISTS"); else Require(CandidateSha.Length == 64 && Hash(Candidate) == CandidateSha, "COLD_CANDIDATE_PIN_CHANGED");\n            Require')
start = probe.index('            if (args[0] == "left-close-right")')
finish = probe.index('            Documents(sw, "after-diagnostics");', start)
probe = probe[:start] + '''            if (args[0] == "mirror-create") CreateMirror(sw);
            else {
                ModelDoc2 model = Open(sw, Candidate, args[0]);
                var geometry = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchCapture(model, "R", Input.rightRows);
                bool originalReadOnlyGate = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchGeometryGate(geometry, "R", Input.rightRows);
                bool mirrorGate = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchMirrorGeometryGate(geometry, Input.rightRows);
                Record(D("inspection", "original_locks_readonly_geometry_gate", "path", Candidate, "geometry", geometry, "originalReadOnlyGate", originalReadOnlyGate, "mirrorGate", mirrorGate, "saveFlag", model.GetSaveFlag(), "modelAccepted", false));
                Require(originalReadOnlyGate && mirrorGate && !model.GetSaveFlag(), "ORIGINAL_LOCKS_READONLY_OR_MIRROR_GATE_FAILED");
                Require(Hash(Candidate) == CandidateSha, "COLD_CANDIDATE_BYTES_CHANGED"); Dependencies(sw, Candidate); Release(model);
            }
''' + probe[finish:]
probe = change(probe, '"noSave", true, "noRebuild", true, "noReplace", true', '"sourceSaved", false, "sourceRebuilt", false, "candidateOnlySave", args[0] == "mirror-create", "noReplace", true')
# Insert the mirror creation algorithm copied from the frozen Locks source. Parameters come from pinned locks evidence.
locksLines = text(SOURCE/'NativeParametricLocks.cs').splitlines(keepends=True)
mirrorBody = ''.join(locksLines[131:152])
assert 'MirrorPart2(false' in mirrorBody and 'InsertBends2(bendRadius' in mirrorBody
create = '''    private static bool SelectRightPlane(ModelDoc2 model) { return Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchSelectRightPlane(model); }
    private static void Require(bool condition, string code, int exitCode, string message) { Require(condition, code + " " + message); }
    private static void CreateMirror(ISldWorks sw)
    {
        ModelDoc2 model = Open(sw, Input.parameterSource.path, "saved-parameter-source-before-mirror");
        Documents(sw, "before-mirror-source-only");
        double bendRadius = Input.bend.v35BendRadiusMm / 1000.0, kFactor = Input.bend.v35KFactor, bendAllowance = Input.bend.v35BendAllowance, reliefRatio = Input.bend.v35ReliefRatio;
        bool autoRelief = Input.bend.v35AutoRelief; double[] fixedPlane = Input.bend.v35FixedPlane; string side = "R"; int activateError = 0; ModelDoc2 mirrored = null;
        Record(D("inspection", "frozen_mirror_parameters", "locksEvidenceSha256", LocksSha, "bend", Input.bend, "rightRows", Input.rightRows, "breakLink", false, "modelAccepted", false));
        try {
''' + mirrorBody + '''
            Documents(sw, "after-mirror-and-bends");
            Inspect(sw, mirrored, Candidate, "new-mirror-before-save");
            var geometry = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchCapture(mirrored, "R", Input.rightRows);
            bool mirrorGate = Winnsen.StructureAgent.Native888.NativeParametricLocks.ResearchMirrorGeometryGate(geometry, Input.rightRows);
            object files, parts, features, types, statuses, entities, components;
            mirrored.ListExternalFileReferences2(out files, out parts, out features, out types, out statuses, out entities, out components);
            bool bound = Values(files).Length > 0 && Values(files).All(file => Same(Convert.ToString(file), Input.parameterSource.path));
            Record(D("inspection", "original_mirror_gate_before_save", "geometry", geometry, "mirrorGate", mirrorGate, "localSourceBound", bound, "referenceFiles", files, "referenceStatuses", statuses, "sourceIdentity", Identity(model), "modelAccepted", false));
            Require(mirrorGate && bound && model.IsOpenedReadOnly() && Hash(Input.parameterSource.path) == Input.parameterSource.sha256, "MIRROR_GEOMETRY_OR_SOURCE_PROTECTION_FAILED");
            int errors = 0, warnings = 0; bool saved = mirrored.Extension.SaveAs(Candidate, 0, 1, null, ref errors, ref warnings);
            Record(D("inspection", "candidate_save", "path", Candidate, "saved", saved, "errors", errors, "warnings", warnings, "actual", Identity(mirrored), "modelAccepted", false));
            Require(saved && errors == 0 && warnings == 0 && Same(mirrored.GetPathName(), Candidate) && !mirrored.GetSaveFlag(), "CANDIDATE_SAVE_FAILED");
            Documents(sw, "after-candidate-save"); Inspect(sw, mirrored, Candidate, "new-mirror-after-save");
            Record(D("inspection", "candidate_saved_sha256", "path", Candidate, "sha256", Hash(Candidate), "modelAccepted", false));
        } finally { if (mirrored != null) Release(mirrored); Release(model); }
    }
'''
probe = change(probe, 'using SolidWorks.Interop.sldworks;', 'using SolidWorks.Interop.sldworks;\nusing SolidWorks.Interop.swconst;')
probe = probe.replace('    private static void VerifyOwner(', create + '    private static void VerifyOwner(', 1)
# Unsaved mirrored part has no path yet; this exact live object is allowed only inside creation.
probe = change(probe, 'private static string Candidate, CandidateSha;', 'private static string Candidate, CandidateSha;\n    private static ModelDoc2 NewMirror;')
probe = change(probe, 'Bound(doc.GetPathName()), "LOADED_DOCUMENT', '(Bound(doc.GetPathName()) || (NewMirror != null && object.ReferenceEquals(doc, NewMirror) && string.IsNullOrEmpty(doc.GetPathName()))), "LOADED_DOCUMENT')
probe = change(probe, 'Documents(sw, "after-mirror-and-bends");', 'NewMirror = mirrored; Documents(sw, "after-mirror-and-bends");')
write('RightMirrorFreshProbe.cs', probe)
# Reuse the mature observer unchanged: its arm contract is generic and all protections remain in config.
write('observe-right-mirror.py', text(PRIOR/'observe-right-identity.py'))
runner = text(PRIOR/'run-right-identity.mjs')
runner = change(runner, 'right_identity_v1', 'right_mirror_fresh_v1')
runner = change(runner, 'right-identity', 'right-mirror-fresh')
runner = change(runner, "const BINDING = resolve(HERE, 'source-binding.json'), BINDING_SHA = '4445F5F8B60B0867F07D391959AB96670FDE53E6E8C85D802E4BF3C2C8623438'", "const BINDING = resolve(HERE, 'source-binding.json'), BINDING_SHA = '" + sha(HERE/'source-binding.json') + "'")
runner = change(runner, "['right-only', 'parameter-source-only', 'left-close-right']", "['mirror-create', 'temporary-cold', 'final-cold']")
runner = change(runner, '...binding.templatePins, binding.observer', '...binding.templatePins, binding.observer, ...binding.originalSourcePins, binding.plan')
runner = change(runner, "const out = resolve(HERE, 'runs', stamp() + '-' + process.pid), results = []", "const out = resolve(HERE, 'runs', stamp() + '-' + process.pid), results = []; let temporary = null, temporaryPin = null, finalCandidate = null, finalPin = null")
runner = change(runner, '      const armOut = resolve(out, arm);', '''      let candidate;
      if (arm === 'mirror-create') { const folder = resolve(out, 'candidate'); mkdirSync(folder); temporary = resolve(folder, 'lock_right_native.SLDPRT'); candidate = temporary }
      else if (arm === 'temporary-cold') { need(temporaryPin, 'TEMPORARY_CANDIDATE_EVIDENCE_MISSING'); unchanged([temporaryPin]); candidate = temporary }
      else { need(results.length === 2 && results[1].pass === true && temporaryPin, 'TEMPORARY_COLD_GATE_REQUIRED_BEFORE_REPLACE'); unchanged([temporaryPin]); const folder = resolve(out, 'final'); mkdirSync(folder); finalCandidate = resolve(folder, '箱体竖隔板R.SLDPRT'); copyFileSync(context.binding.right.path, finalCandidate, constants.COPYFILE_EXCL); const copiedOld = regular(finalCandidate); need(copiedOld.sha256 === context.binding.right.sha256, 'FINAL_OLD_RIGHT_COPY_DIFFERS'); const replaceScript = '$ErrorActionPreference=\'Stop\'; [IO.File]::Replace(\'' + temporary.replaceAll("'", "''") + '\',\'' + finalCandidate.replaceAll("'", "''") + '\',\'' + (finalCandidate + '.before-parametric-lock.bak').replaceAll("'", "''") + '\')'; const replaced = spawnSync('powershell.exe', ['-NoProfile','-NonInteractive','-Command',replaceScript], {encoding:'utf8',windowsHide:true,timeout:10000}); need(replaced.status === 0, 'CANDIDATE_FILE_REPLACE_FAILED ' + replaced.stderr); finalPin = regular(finalCandidate); need(finalPin.sha256 === temporaryPin.sha256 && hash(finalCandidate + '.before-parametric-lock.bak') === context.binding.right.sha256 && !existsSync(temporary), 'CANDIDATE_REPLACE_BYTES_DIFFER'); put(resolve(out, 'candidate-replace.json'), {api:'System.IO.File.Replace', temporaryBefore:temporaryPin, finalBefore:copiedOld, finalAfter:finalPin, backup:regular(finalCandidate + '.before-parametric-lock.bak'), modelAccepted:false}); candidate = finalCandidate }
      const armOut = resolve(out, arm);''')
runner = change(runner, "writeFileSync, lstatSync", "writeFileSync, copyFileSync, constants, lstatSync")
runner = change(runner, "immutable: context.protectedRows,", "immutable: [...context.protectedRows, ...(arm === 'temporary-cold' ? [temporaryPin] : arm === 'final-cold' ? [finalPin] : [])],")
runner = change(runner, "BINDING, BINDING_SHA]", "BINDING, BINDING_SHA, candidate, arm === 'mirror-create' ? 'new' : (arm === 'temporary-cold' ? temporaryPin.sha256 : finalPin.sha256)]")
runner = change(runner, 'observe-right-mirror-fresh.py', 'observe-right-mirror.py')
runner = change(runner, "'observe-right-mirror-fresh.py'", "'observe-right-mirror.py'") if "'observe-right-mirror-fresh.py'" in runner else runner
runner = change(runner, '      unchanged(context.protectedRows)\n    }', "      if (arm === 'mirror-create') { temporaryPin = regular(temporary); put(resolve(out, 'temporary-candidate.json'), {candidate:temporaryPin, modelAccepted:false}) }\n      unchanged(context.protectedRows)\n    }")
runner = change(runner, "state: 'diagnostic_observations_only'", "state: 'fresh_mirror_research_only', temporaryPin, finalPin")
write('run-right-mirror.mjs', runner)
print(json.dumps(dict(prepared=True,bindingSha256=sha(HERE/'source-binding.json'),exactMethodChunks=len(chunks),nativeCadExecuted=False,modelAccepted=False)))
