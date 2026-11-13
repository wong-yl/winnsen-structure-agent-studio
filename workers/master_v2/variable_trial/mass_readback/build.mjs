import { copyFileSync, readFileSync, readdirSync, writeFileSync, constants } from 'node:fs'
import { resolve, join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash, randomBytes } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { archiveSources } from '../build.mjs'
import { SOURCE, DATA, TOOLCHAIN_SCHEMA, INTEROP_DLLS, inside, directory, hash, write, validateToolchain } from '../common.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const ROOT_SOURCE_NAMES = ['TrialAssembly.cs', 'TrialOutput.cs', 'VariableTrial.cs']
const ADAPTER_MARKER = '// temporary diagnostic adapter; original sources remain frozen; see adapter-provenance.json'
const digest = value => createHash('sha256').update(value).digest('hex').toUpperCase()

function replaceExactlyOnce(source, original, replacement, id) {
  const matches = source.split(original).length - 1
  if (matches !== 1) throw new Error('WEIGHT_ADAPTER_EXACT_ONE_REQUIRED ' + id + ' count=' + matches)
  return {
    source: source.replace(original, () => replacement),
    transformation: { id, matches, originalSha256: digest(original), replacementSha256: digest(replacement) },
  }
}

export function adaptWeightMain(original) {
  if (typeof original !== 'string' || original.includes('weight-readback-diagnostic') || original.includes(ADAPTER_MARKER)) throw new Error('WEIGHT_ADAPTER_FRESH_MAIN_REQUIRED')
  const newline = original.includes('\r\n') ? '\r\n' : '\n'
  const lines = values => values.join(newline)
  const actionAnchor = '            if(Text("action")=="rewire-inspect") {Rewire(Text("cadDirectory"));Inspect();}'
  const actionReplacement = lines([
    '            if(Text("action")=="weight-readback-diagnostic")',
    '            {',
    '                var diagnostic=WeightReadback.Run(Sw,Text("cadDirectory"),Job,delegate(object progress){Record(new {inspection="weight_readback_progress",result=progress,stageAcceptance=false});}) as Dictionary<string,object>;',
    '                Record(new {inspection="weight_readback_diagnostic",result=diagnostic,stageAcceptance=false});',
    '                object diagnosticSuccess,diagnosticComplete;',
    '                Need(diagnostic!=null&&diagnostic.TryGetValue("success",out diagnosticSuccess)&&diagnosticSuccess is bool&&(bool)diagnosticSuccess&&diagnostic.TryGetValue("readbackComplete",out diagnosticComplete)&&diagnosticComplete is bool&&(bool)diagnosticComplete,"WEIGHT_READBACK_DIAGNOSTIC_INCOMPLETE");',
    '            }',
    '            else if(Text("action")=="rewire-inspect") {Rewire(Text("cadDirectory"));Inspect();}',
  ])
  const cleanupAnchor = lines([
    '                    var ownedTitles=new List<string>();var foreignPaths=new List<string>();',
    '                    foreach(object value in Items(Sw.GetDocuments())) {ModelDoc2 doc=value as ModelDoc2;try{if(doc!=null&&OwnedDocument(doc))ownedTitles.Add(doc.GetTitle());else foreignPaths.Add(doc==null?"NON_MODEL":doc.GetPathName());}finally{Release(value);}}',
    '                    foreach(string title in ownedTitles)Sw.CloseDoc(title);',
    '                    Record(new {inspection="owned_document_cleanup",closed=ownedTitles,foreignDocuments=foreignPaths});',
    '                    Need(foreignPaths.Count==0,"EXTERNAL_DOCUMENT_SESSION_PRESERVED");',
    '                    Need(Items(Sw.GetDocuments()).Length==0,"DOCUMENTS_REMAIN_SESSION_PRESERVED");Sw.ExitApp();',
  ])
  const cleanupReplacement = lines([
    '                    if(Text("action")=="weight-readback-diagnostic")',
    '                    {',
    '                        var remainingPaths=new List<string>();',
    '                        foreach(object value in Items(Sw.GetDocuments())) {ModelDoc2 doc=value as ModelDoc2;try{remainingPaths.Add(doc==null?"NON_MODEL":doc.GetPathName());}finally{Release(value);}}',
    '                        int remainingCount=Sw.GetDocumentCount();',
    '                        Record(new {inspection="weight_readback_remaining_documents",documents=remainingPaths,observedCount=remainingPaths.Count,declaredCount=remainingCount,closeCalled=false});',
    '                        Need(remainingPaths.Count==remainingCount,"WEIGHT_FINAL_DOCUMENT_COUNT_MISMATCH_SESSION_PRESERVED");',
    '                        Need(remainingPaths.Count==0&&remainingCount==0,"WEIGHT_FINAL_DOCUMENTS_REMAIN_SESSION_PRESERVED");',
    '                        Sw.ExitApp();',
    '                    }',
    '                    else',
    '                    {',
    cleanupAnchor,
    '                    }',
  ])
  const action = replaceExactlyOnce(original, actionAnchor, actionReplacement, 'main_action')
  const cleanup = replaceExactlyOnce(action.source, cleanupAnchor, cleanupReplacement, 'main_finally_cleanup')
  const bom = cleanup.source.startsWith('\ufeff') ? '\ufeff' : ''
  return {
    source: bom + ADAPTER_MARKER + newline + cleanup.source.slice(bom.length),
    transformations: [action.transformation, cleanup.transformation],
  }
}

export function buildWeightToolchain({ dataRoot = DATA } = {}) {
  const bin = directory(resolve(dataRoot, 'toolchain', 'weight-' + new Date().toISOString().replace(/[:.]/g, '-') + '-' + randomBytes(6).toString('hex')), dataRoot, true)
  const sourceRoot = directory(join(bin, 'generated-inputs'), dataRoot, true)
  const names = readdirSync(inside(SOURCE, SOURCE)).filter(name => name.endsWith('.cs')).sort()
  if (JSON.stringify(names) !== JSON.stringify(ROOT_SOURCE_NAMES)) throw new Error('WEIGHT_ROOT_SOURCE_SET_CHANGED')
  const originalSources = names.map(name => ({ name, path: inside(join(SOURCE, name), SOURCE), sha256: hash(join(SOURCE, name)) }))
  const weightPath = inside(join(HERE, 'WeightReadback.cs'), HERE), weightSha256 = hash(weightPath)
  const main = originalSources.find(item => item.name === 'VariableTrial.cs')
  const originalMain = readFileSync(main.path)
  if (digest(originalMain) !== main.sha256) throw new Error('WEIGHT_ROOT_SOURCE_CHANGED ' + main.path)
  if (!Buffer.from(originalMain.toString('utf8'), 'utf8').equals(originalMain)) throw new Error('WEIGHT_ROOT_MAIN_UTF8_REQUIRED')
  const adapted = adaptWeightMain(originalMain.toString('utf8'))
  for (const item of originalSources) {
    const target = inside(join(sourceRoot, item.name), sourceRoot)
    if (item.name === 'VariableTrial.cs') writeFileSync(target, adapted.source, { flag: 'wx', encoding: 'utf8' })
    else {
      copyFileSync(item.path, target, constants.COPYFILE_EXCL)
      if (hash(target) !== item.sha256) throw new Error('WEIGHT_ROOT_SOURCE_COPY_CHANGED ' + item.path)
    }
  }
  const copiedWeight = inside(join(sourceRoot, 'WeightReadback.cs'), sourceRoot)
  copyFileSync(weightPath, copiedWeight, constants.COPYFILE_EXCL)
  if (hash(copiedWeight) !== weightSha256) throw new Error('WEIGHT_IMPLEMENTATION_COPY_CHANGED')
  const sources = archiveSources(sourceRoot, bin, { dataRoot })
  const dlls = INTEROP_DLLS.map(name => {
    const original = inside(resolve('D:/soildworks2020/SOLIDWORKS/api/redist', name), 'D:/soildworks2020/SOLIDWORKS/api/redist')
    const path = inside(join(bin, name), dataRoot), sha256 = hash(original)
    copyFileSync(original, path, constants.COPYFILE_EXCL)
    if (hash(path) !== sha256 || hash(original) !== sha256) throw new Error('WEIGHT_INTEROP_COPY_CHANGED ' + name)
    return { path, sha256 }
  })
  const exe = inside(join(bin, 'VariableTrial.exe'), dataRoot)
  const args = ['/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', '/out:' + exe,
    '/reference:System.Web.Extensions.dll', '/reference:System.Management.dll', ...dlls.map(item => '/reference:' + item.path), ...sources.map(item => item.archivePath)]
  const compiled = spawnSync('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', args, { windowsHide: true, encoding: 'utf8' })
  if (compiled.error || compiled.status !== 0) throw new Error(compiled.error?.message || String(compiled.stdout) + String(compiled.stderr))
  for (const item of originalSources) if (hash(item.path) !== item.sha256) throw new Error('WEIGHT_ROOT_SOURCE_CHANGED ' + item.path)
  if (hash(weightPath) !== weightSha256) throw new Error('WEIGHT_IMPLEMENTATION_CHANGED_DURING_BUILD')
  const manifest = { schema: TOOLCHAIN_SCHEMA, exe, sha256: hash(exe), sources, dlls }
  validateToolchain(manifest, { dataRoot, sourceRoot })
  const provenancePath = inside(join(bin, 'adapter-provenance.json'), dataRoot)
  write(provenancePath, {
    schema: 'winnsen.variable_trial.weight_adapter.provenance.v1',
    description: 'temporary diagnostic adapter',
    diagnosticOnly: true,
    rootSourcesUnmodified: true,
    originalSources,
    weightSource: { path: weightPath, sha256: weightSha256 },
    generatedSourceRoot: sourceRoot,
    generatedSources: sources.map(({ path, sha256 }) => ({ path, sha256 })),
    transformations: adapted.transformations,
    executable: { path: exe, sha256: manifest.sha256 },
    production_release: false,
  }, { root: dataRoot })
  write(join(bin, 'manifest.json'), manifest, { root: dataRoot })
  return { manifest, sourceRoot, provenancePath }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(buildWeightToolchain())) } catch (error) { console.error(error); process.exitCode = 1 }
}
