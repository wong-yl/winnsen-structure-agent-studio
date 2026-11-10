from pathlib import Path
import datetime, difflib, hashlib, json, re

ROOT = Path(__file__).resolve().parent
ALLOWED = Path(r'D:\Winnsen_Structure_Agent_Studio\data\indoor_workbench_repair_20261110\obsolete_door_probe\v6')
assert ROOT == ALLOWED, 'STATIC_DIRECTORY_NOT_AUTHORIZED'
V5 = ROOT.parent / 'v5' / 'IdentityProbe.cs'
SOURCE = ROOT / 'IdentityProbe.cs'
BUILD = ROOT.parent / 'build-v6.json'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def sha_text(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest().upper()

old = V5.read_bytes().decode('utf-8-sig')
new = SOURCE.read_bytes().decode('utf-8-sig')
build = json.loads(BUILD.read_text(encoding='utf-8-sig'))
checks = []
def check(name, value, detail=''):
    checks.append(dict(name=name, passed=bool(value), detail=detail))

def block(text, name, class_block=False):
    pattern = (r'(?m)^    private sealed class ' + re.escape(name) + r'\b' if class_block else r'(?m)^    (?:private|public) static[^\r\n{=]*?\b' + re.escape(name) + r'\(')
    hits = list(re.finditer(pattern, text))
    assert len(hits) == 1, (name, len(hits))
    start = hits[0].start()
    opening = text.index('{', hits[0].end())
    depth, i, state = 0, opening, 'code'
    while i < len(text):
        ch = text[i]
        pair = text[i:i+2]
        if state == 'line':
            if ch == '\n': state = 'code'
        elif state == 'comment':
            if pair == '*/': state = 'code'; i += 1
        elif state == 'string':
            if ch == '\\': i += 1
            elif ch == '"': state = 'code'
        elif state == 'verbatim':
            if pair == '""': i += 1
            elif ch == '"': state = 'code'
        elif state == 'char':
            if ch == '\\': i += 1
            elif ch == "'": state = 'code'
        elif pair == '//': state = 'line'; i += 1
        elif pair == '/*': state = 'comment'; i += 1
        elif pair == '@"': state = 'verbatim'; i += 1
        elif ch == '"': state = 'string'
        elif ch == "'": state = 'char'
        elif ch == '{': depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0: return text[start:i+1]
        i += 1
    raise AssertionError('Unclosed block ' + name)

check('frozen v5 source SHA256', sha(V5) == '852CE11FBC7E75A73E70BB1FDF59B5684C27324D1B9EB73EF3C5D7A079416918', sha(V5))
check('source and executable match successful build', build['buildPassed'] is True and build['sourceSha256'] == sha(SOURCE) and build['executableSha256'] == sha(Path(build['executable'])), build['executable'])
protected = ['D','Require','Full','Same','Inside','Plain','Hash','ValidSha','CanonicalExecutable','Record','SafeRecord','ArrayValues','Numbers','Release','Pids','Files','Inventory','CompareFiles','EventFrom','LoadReferenceSourceWhitelist','BindPath','BindReferencePath','Persistent','NumberType','Equal','ObsoleteName','References','DocumentReferences','FeatureReferences','ComponentReferences','MateState','Features','FeatureSnapshot','Tree','Bodies','ActiveConfigurationName','ResolvedWitness','ComponentSnapshot','Snapshot','Loaded','DeleteOldDoors','RootHealthy','VerifyOwner']
method_hashes = []
for name in protected:
    a, b = block(old, name), block(new, name)
    method_hashes.append(dict(name=name, v5Sha256=sha_text(a), v6Sha256=sha_text(b), byteTextUnchanged=a == b))
    check('unchanged protected method ' + name, a == b, sha_text(b))
for name in ['IdentityScope','ComponentHandle','ComponentSet']:
    a, b = block(old, name, True), block(new, name, True)
    method_hashes.append(dict(name=name, v5Sha256=sha_text(a), v6Sha256=sha_text(b), byteTextUnchanged=a == b))
    check('unchanged protected class ' + name, a == b, sha_text(b))

main = block(new, 'Main')
oldmain = block(old, 'Main')
baseline = main[main.index('            if (Mode == "baseline")'):main.index('            else if (Mode == "edit")')]
diag = main[main.index('            else if (Mode == "diagnostic33")'):main.index('            else\n            {\n                Require(!model.GetSaveFlag()')]
verify = main[main.index('                Require(!model.GetSaveFlag(), "READONLY_ROOT_SAVE_FLAG_SET")'):main.index('            FrozenBindings(); Loaded(sw, "final-loaded-documents")')]
oldverify = oldmain[oldmain.index('                Require(!model.GetSaveFlag(), "READONLY_ROOT_SAVE_FLAG_SET")'):oldmain.index('            FrozenBindings(); Loaded(sw, "final-loaded-documents")')]
mutator = r'\.(?:DeleteSelection2|ForceRebuild3|Save3|ClearSelection2|Select4)\([^;\r\n]*'
check('CAD mutation API calls and arguments unchanged', re.findall(mutator, old) == re.findall(mutator, new), json.dumps(re.findall(mutator, new), ensure_ascii=False))
check('baseline branch only collects complete raw snapshot and root health', 'Snapshot(sw, model, "before-delete-baseline", true)' in baseline and 'RootHealthy(baseline)' in baseline and not re.search(r'DeleteOldDoors|\.(?:Save|ForceRebuild|Edit|Delete|ClearSelection|Select)', baseline), 'No mutation call in baseline branch')
check('diagnostic33 branch only collects raw after-save equality and root health', 'Equal(prior["state"], actual' in diag and 'RootHealthy(actual)' in diag and not re.search(r'DeleteOldDoors|GetSaveFlag|\.(?:Save|ForceRebuild|Edit|Delete|ClearSelection|Select)', diag), 'No save flag acceptance or mutation call in diagnostic branch')
check('strict verify branch is byte-text identical to v5', verify == oldverify, sha_text(verify))
check('four argument CLI has only four exact modes', 'args.Length != 4' in main and '<baseline|edit|diagnostic33|verify> <candidate_pack_v6>' in main and all(('args[0] != "'+mode+'"') in main for mode in ['baseline','edit','diagnostic33','verify']))
check('exact raw open gates retained for edit and verify', 'options = Mode == "verify" ? 35 : 33' in main and '(Mode == "verify" ? warnings == 0 : Mode == "edit" ? warnings == 32769 : true)' in main and 'model != null && errors == 0' in main and 'model.IsOpenedReadOnly() == (Mode == "verify")' in main)
check('independent baseline raw state is compared before deletion', main.index('Equal(IndependentBaselineState, before, "independent-cold-baseline-to-before-delete")') < main.index('DeleteOldDoors(model);'), 'Original Equal; no EntityParams normalization')
steps = ['DeleteOldDoors(model);', 'Snapshot(sw, model, "after-delete-before-rebuild", false)', 'Equal(before, afterDelete,', 'RootHealthy(afterDelete)', 'model.ForceRebuild3(true)', 'Snapshot(sw, model, "after-rebuild", false)', 'Equal(before, after,', 'RootHealthy(after)', 'model.Save3(1, ref saveErrors, ref saveWarnings)', 'Snapshot(sw, model, "after-save", false)', 'Equal(before, afterSave,', 'RootHealthy(afterSave)']
positions = [main.index(token) for token in steps]
check('edit deletion snapshot rebuild save snapshot order retained', positions == sorted(positions), steps)
check('one exact delete top-only rebuild and root-only save', len(re.findall(r'\.DeleteSelection2\(', new)) == 1 and len(re.findall(r'\.ForceRebuild3\(', new)) == 1 and len(re.findall(r'\.Save3\(', new)) == 1 and 'saveErrors == 0 && saveWarnings == 0 && !saveFlag' in main)
check('relink candidate hashes are distinct from source hashes in manifest', 'sha256, candidateSha256' in new and 'size, candidateSize' in new and 'ExpectedHashes.Add(Full(file.path), file.candidateSha256)' in new and '.candidateSha256, "ROOT_FILE_DID_NOT_CHANGE"' in main)
proof = block(new, 'ProofCandidateInventory')
check('proof candidate inventory binds all five fields and exact 75', all(('row["'+key+'"]') in proof for key in ['path','sha256','size','ticks','attributes']) and 'rows.Count == 75' in proof and 'row.sha256 == (editedRoot ? editedRootSha : file.candidateSha256)' in proof and 'row.size == file.candidateSize' in proof)
binding = block(new, 'ValidateManifest')
baseproof = block(new, 'BaselineProof')
check('relink proof exact completion flags are diagnostic only', '"relink"' in binding and all(('relink["'+key+'"], '+value+')') in binding for key,value in [('diagnosticCompleted','true'),('operationComplete','false'),('modelAccepted','false'),('strictColdAcceptancePassed','false')]))
check('independent baseline bound to same candidate and exact raw edit input', all(token in baseproof for token in ['"baseline"','completed["diagnosticCompleted"], true','raw["options"]) == 33','raw["errors"]) == 0','raw["warnings"]) == 32769','raw["actualReadOnly"], false','"before-delete-baseline"','"candidate-before"','"candidate-after"']))
check('every mode binds source evidence and relink proof', 'SOURCE_EVIDENCE_BINDING_FAILED' in binding and 'EvidenceBinding(Binding.relinkEvidence, "RELINK")' in binding)
check('baseline and edit proofs required in correct modes', 'if (Mode == "baseline") Require(Binding.baselineEvidence == null' in binding and 'else BaselineProof()' in binding and 'if (Mode == "verify" || Mode == "diagnostic33")' in binding and 'EvidenceBinding(Binding.editEvidence, "EDIT")' in binding)
frozen = block(new, 'FrozenBindings')
check('all frozen proof and current manifest hashes checked and logged', all(token in frozen for token in ['Hash(ManifestPath) == ManifestSha','Hash(ReceiptPath) == ReceiptSha256','Hash(EvidencePath) == EvidenceSha256','Hash(Binding.relinkEvidence.path) == Binding.relinkEvidence.sha256','Hash(Binding.baselineEvidence.path) == Binding.baselineEvidence.sha256','Hash(Binding.editEvidence.path) == Binding.editEvidence.sha256','Hash(proof.Key) == proof.Value','"manifestSha256"','"sourceEvidence"','"relinkEvidence"','"baselineEvidence"','"editEvidence"']))
check('session end file comparison uses session beginning inventory unchanged', 'CompareFiles(sourceBefore, sourceAfter, false, "all75-source-unchanged"); CompareFiles(packBefore, packAfter, Mode == "edit", Mode == "edit" ? "other74-candidate-unchanged" : "all75-candidate-unchanged");' in main)
check('candidate beginning inventory equals frozen preceding inventory', 'CompareFiles(CandidateInitialStates, packBefore, false, "candidate-start-matches-frozen-proof")' in main)
owner_start='            int[] cad = Pids("SLDWORKS")'
owner_end='            int errors = 0, warnings = 0, options ='
check('cold process ownership and activation unchanged', oldmain[oldmain.index(owner_start):oldmain.index(owner_end)] == main[main.index(owner_start):main.index(owner_end)])
close_start='            try { Release(model); }'
close_end='                passed = operationChecked && owned && closed && exited'
check('natural close process observation and final inventories unchanged', oldmain[oldmain.index(close_start):oldmain.index(close_end)] == main[main.index(close_start):main.index(close_end)])
check('no forced termination or forbidden CAD APIs', not re.search(r'\.(?:Kill|Terminate|ReplaceComponents|ReplaceReferencedDocument|ShowConfiguration2|SetUserPreference|SaveAs|SetSuppression2)\s*\(', new))
check('diagnostic completion never claims operation or model acceptance', '"operationComplete", (Mode == "edit" || Mode == "verify") && passed' in main and '"diagnosticCompleted", (Mode == "baseline" || Mode == "diagnostic33") && passed' in main and '"modelAccepted", false, "strictColdAcceptancePassed", false' in main and '"noSave", Mode != "edit"' in main)
script = (ROOT / 'build.ps1').read_text(encoding='utf-8-sig')
check('build refuses existing report or artifact directory', 'BUILD_REPORT_EXISTS_DO_NOT_OVERWRITE' in script and 'BUILD_ARTIFACT_DIRECTORY_EXISTS_DO_NOT_OVERWRITE' in script and "'build-v6.json'" in script)
check('compiled build script hash', sha(ROOT / 'build.ps1') == build['buildScriptSha256'], build['buildScriptSha256'])
runtime_path = ROOT / 'static-runtime.json'
runtime = json.loads(runtime_path.read_text(encoding='utf-8-sig'))
check('offline reflection tests match compiled artifact', runtime['sourceSha256'] == sha(SOURCE) and runtime['executableSha256'] == build['executableSha256'] and runtime['mainInvoked'] is False and runtime['nativeCadExecuted'] is False)
checks.extend(runtime['checks'])
(ROOT / 'source.diff').write_text(''.join(difflib.unified_diff(old.splitlines(keepends=True), new.splitlines(keepends=True), fromfile='v5/IdentityProbe.cs', tofile='v6/IdentityProbe.cs')), encoding='utf-8', newline='')
result = dict(schema='obsolete-door-v6-offline-validation-v1', generatedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), source=str(SOURCE), sourceSha256=sha(SOURCE), executable=build['executable'], executableSha256=build['executableSha256'], buildReport=str(BUILD), buildReportSha256=sha(BUILD), buildScriptSha256=sha(ROOT/'build.ps1'), staticSourceScriptSha256=sha(Path(__file__)), staticRuntimeScriptSha256=sha(ROOT/'static-runtime.ps1'), runtimeReportSha256=sha(runtime_path), sourceDiffSha256=sha(ROOT/'source.diff'), protectedMethodHashes=method_hashes, count=len(checks), passed=sum(row['passed'] for row in checks), failed=sum(not row['passed'] for row in checks), checks=checks, mainInvoked=False, nativeCadExecuted=False, runtimeAcceptance='UNVERIFIED: CAD execution belongs to primary agent; source75/candidate75 runtime inventories not collected by worker')
(ROOT / 'static-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps({key:result[key] for key in ['sourceSha256','executableSha256','buildReportSha256','buildScriptSha256','count','passed','failed','nativeCadExecuted','mainInvoked']}, ensure_ascii=False, indent=2))
for row in checks:
    if not row['passed']: print('FAIL: '+row['name'])
raise SystemExit(0 if result['failed']==0 else 1)
