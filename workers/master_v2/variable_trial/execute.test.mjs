import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, realpathSync, rmSync, writeFileSync, existsSync, unlinkSync, readdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { DATA, read, hash } from './common.mjs'
import { execute, parseCliArguments, preflight, runParametricEnvelopePhases } from './execute.mjs'
const spec=read(new URL('./inputs/specification.json',import.meta.url))
const processInput=read(new URL('./inputs/process.json',import.meta.url))
test('approved pilot produces six distinct door identities and the three exact layouts',()=>{
  const result=preflight(spec,processInput)
  assert.equal(result.doors.length,6);assert.equal(new Set(result.doors.map(d=>d.identity)).size,6)
  assert.deepEqual(result.layouts.map(l=>[l.cabinet.widthMm,l.columnDoorCounts,l.doorLeafWidthMm]),[[1000,[4,4],437],[1000,[4,3],437],[900,[4,3],387]])
})
test('missing inputs and malformed or unsupported sizes fail before native execution',()=>{
  for(const s of [null,{}, {...spec,doorCases:[]}])assert.throws(()=>preflight(s,processInput))
  for(const dimension of [NaN,0,20,100000,'437']) {
    const copy=structuredClone(spec);copy.doorCases[0].widthMm=dimension;assert.throws(()=>preflight(copy,processInput))
  }
  assert.throws(()=>preflight(spec,{}))
})
test('duplicate and conflicting hands or identities are rejected',()=>{
  const duplicate=structuredClone(spec);duplicate.doorCases[1]=duplicate.doorCases[0];assert.throws(()=>preflight(duplicate,processInput),/DUPLICATE/)
  for(const change of [{identity:'right-door'},{properties:{hand:'R'}}]) {const copy=structuredClone(spec);Object.assign(copy.doorCases[0],change);assert.throws(()=>preflight(copy,processInput),/CONFLICT/)}
})
test('24 door column cannot silently exceed height budget',()=>{
  const copy=structuredClone(spec);copy.assemblyCases[0].columns[0].doors=Array.from({length:24},()=>({heightUnits:1}));assert.throws(()=>preflight(copy,processInput),/100 mm/)
})
test('pilot rejects depth and height expansion before native execution',()=>{
  for(const [field,value] of [['heightMm',2100],['depthMm',550]]){const copy=structuredClone(spec);copy.assemblyCases[0].cabinet[field]=value;assert.throws(()=>preflight(copy,processInput),/PILOT_PROFILE/)}
})

function sourceFixture(t) {
  const dataRoot=mkdtempSync(join(realpathSync.native(tmpdir()),'winnsen-dxf-only-cli-'))
  t.after(()=>rmSync(dataRoot,{recursive:true,force:true}))
  const json=(path,value)=>writeFileSync(path,JSON.stringify(value))
  const add=(pointerFile,action,manifest)=>{
    const dir=join(dataRoot,'runs',pointerFile.replace('.json','')),cadDirectory=join(dir,'cad')
    mkdirSync(cadDirectory,{recursive:true})
    const nativeResult=join(dir,'native-result.json')
    json(nativeResult,{action,production_release:false,events:[{inspection:'finished',success:true,cadExited:true}]})
    json(join(dataRoot,pointerFile),{dir,cadDirectory,nativeResult})
    if(manifest){mkdirSync(join(cadDirectory,'outputs'));json(join(cadDirectory,'outputs/output-manifest.json'),manifest)}
    return cadDirectory
  }
  const calls=[]
  const invoke=mode=>execute(mode,undefined,undefined,{dataRoot,runPhase:async value=>{calls.push(value);return value}})
  return {dataRoot,add,invoke,calls,json}
}
const fullManifest=()=>({schema:'16029-variable-output-manifest/v1',complete:true,cases:Array.from({length:6},()=>({drawing:{}}))})
const dxfManifest=()=>({schema:'16029-variable-output-manifest/v2',outputScope:'parts_dxf_only',drawingStatus:'deferred_by_user_20261113',production_release:false,complete:true,cases:Array.from({length:6},()=>({drawing:null}))})

test('outputs-dxf uses T1 and its own action, scope, tag and pointer; full outputs remains separate',async t=>{
  const fixture=sourceFixture(t),source=fixture.add('T1-current.json','variables')
  fixture.add('outputs-current.json','outputs',fullManifest())
  fixture.add('outputs-dxf-current.json','outputs-dxf',dxfManifest())
  for(const [mode,scope,tag] of [['outputs-dxf','parts_dxf_only','T2-outputs-dxf'],['outputs','parts_dxf_pdf','T2-outputs']]) {
    const call=await fixture.invoke(mode)
    assert.equal(call.sourceDirectory,source);assert.equal(call.job.action,mode);assert.equal(call.job.outputScope,scope)
    assert.equal(call.tag,tag);assert.equal(call.currentFile,mode+'-current.json');assert.equal(call.job.production_release,false)
  }
})

test('assembly prefers valid full outputs, otherwise valid DXF outputs, then T1 without completed output scope',async t=>{
  const fixture=sourceFixture(t),t1=fixture.add('T1-current.json','variables')
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,t1)
  assert.equal(Object.hasOwn(fixture.calls.at(-1).job,'outputScope'),false)
  const dxf=fixture.add('outputs-dxf-current.json','outputs-dxf',dxfManifest())
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,dxf)
  assert.equal(fixture.calls.at(-1).job.outputScope,'parts_dxf_only')
  const full=fixture.add('outputs-current.json','outputs',fullManifest())
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,full)
  assert.equal(fixture.calls.at(-1).job.outputScope,'parts_dxf_pdf')
  fixture.json(join(full,'outputs/output-manifest.json'),{...fullManifest(),cases:Array.from({length:6},()=>({drawing:null}))})
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,dxf)
  fixture.json(join(full,'outputs/output-manifest.json'),{...fullManifest(),complete:false})
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,dxf)
  fixture.json(join(dxf,'outputs/output-manifest.json'),{...dxfManifest(),schema:'16029-variable-output-manifest/v1'})
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,t1)
})

test('relocate propagates the source manifest scope and rejects malformed DXF-only metadata',async t=>{
  const fixture=sourceFixture(t),source=fixture.add('assembly-current.json','assembly',dxfManifest())
  assert.equal((await fixture.invoke('relocate')).job.outputScope,'parts_dxf_only')
  for(const change of [{drawingStatus:'complete'},{production_release:true},{outputScope:'parts_dxf_pdf'},{cases:[{drawing:null}]}]) {
    fixture.json(join(source,'outputs/output-manifest.json'),{...dxfManifest(),...change})
    await assert.rejects(fixture.invoke('relocate'),/OUTPUT_MANIFEST_REQUIRED/)
  }
})

test('invalid completed output pointer cannot override a valid DXF source',async t=>{
  const fixture=sourceFixture(t),source=fixture.add('outputs-dxf-current.json','outputs-dxf',dxfManifest())
  fixture.add('T1-current.json','variables')
  fixture.json(join(fixture.dataRoot,'outputs-current.json'),{dir:fixture.dataRoot,cadDirectory:fixture.dataRoot,nativeResult:'missing'})
  assert.equal((await fixture.invoke('assembly')).sourceDirectory,source)
})

function doorSourceFixture(t) {
  const fixture=sourceFixture(t),{dataRoot,json}=fixture
  const specPath=join(dataRoot,'specification.json'),processPath=join(dataRoot,'process.json')
  json(specPath,spec);json(processPath,processInput)
  const source=fixture.add('outputs-dxf-current.json','outputs-dxf',dxfManifest()),run=dirname(source)
  const compiled=preflight(spec,processInput),nativeResult=join(run,'native-result.json')
  json(join(run,'job.json'),{action:'outputs-dxf',outputScope:'parts_dxf_only',...compiled,specification:spec,process:processInput,
    inputVersions:[{path:specPath,sha256:hash(specPath)},{path:processPath,sha256:hash(processPath)}],production_release:false,cadDirectory:source,resultPath:nativeResult})
  json(nativeResult,{action:'outputs-dxf',cadPid:1234,production_release:false,events:[
    {inspection:'cad_owned',pid:1234},{inspection:'finished',success:true,cadExited:true,error:null}]})
  json(join(run,'result.json'),{code:0,stderr:'',warnings:[],cadExited:true})
  json(join(run,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[],warnings:[]})
  json(join(run,'protection-result.json'),{pass:true,changed:[],nativeTaskIndex:{accepted:true}})
  const manifest=dxfManifest()
  manifest.cases=compiled.doors.map((door,index)=>{
    const {DOOR_W:widthMm,DOOR_H:heightMm}=door.variables,folder=door.hand+index,part=`door_${door.hand}_W${widthMm}_H${String(heightMm).replace('.','p')}_t0p8_matfixture_pv1.SLDPRT`
    const directory=join(source,'outputs',folder),partPath=join(directory,part),dxfPath=join(directory,'flat.dxf')
    const nativeCutPath=join(directory,'flat.native-cut.dxf'),nativeBendsPath=join(directory,'flat.native-bends.dxf')
    mkdirSync(directory);writeFileSync(partPath,'fixture-part-'+index);writeFileSync(dxfPath,'fixture-dxf-'+index)
    writeFileSync(nativeCutPath,'fixture-native-cut-'+index);writeFileSync(nativeBendsPath,'fixture-native-bends-'+index)
    return {folder,part,hand:door.hand,widthMm,heightMm,partSha256:hash(partPath),drawing:null,flatPreparation:{success:true},
      processReadback:{schema:'16029-door-process-readback/v1',rawReadbackComplete:true,fixedExplicitOverridesMatch:true,
        candidateFixedProcessMatches:true,pilotTopologyMatches:true,effectiveInheritanceVerified:false,manufacturingOrientationVerified:false,manufacturingAccepted:false},
      dxf:{path:dxfPath,sha256:hash(dxfPath),success:true,nativeCutPath,nativeCutSha256:hash(nativeCutPath),nativeBendsPath,nativeBendsSha256:hash(nativeBendsPath)}}
  })
  const manifestPath=join(source,'outputs/output-manifest.json')
  json(manifestPath,manifest)
  const mutate=(name,change)=>{
    const path=join(run,name),value=read(path);change(value);json(path,value)
  }
  const invoke=()=>execute('doors-reopen',specPath,processPath,{dataRoot,runPhase:async value=>{fixture.calls.push(value);return value}})
  return {...fixture,source,run,specPath,processPath,manifestPath,manifest,compiled,mutate,invoke}
}

test('doors-reopen submits only its dedicated action with verified source fingerprints and no phase pointer',async t=>{
  const fixture=doorSourceFixture(t),before=readdirSync(fixture.dataRoot).sort(),call=await fixture.invoke()
  assert.equal(fixture.calls.length,1);assert.equal(call.sourceDirectory,fixture.source)
  assert.equal(call.tag,'T2-doors-reopen');assert.equal(call.currentFile,null)
  assert.equal(call.job.action,'doors-reopen');assert.equal(call.job.outputScope,'parts_dxf_only')
  assert.equal(call.job.drawingStatus,'deferred_by_user_20261113');assert.equal(call.job.fullPlanPass,false);assert.equal(call.job.production_release,false)
  assert.deepEqual(call.job.doors,fixture.compiled.doors);assert.deepEqual(call.job.layouts,fixture.compiled.layouts)
  assert.deepEqual(call.job.inputVersions,read(join(fixture.run,'job.json')).inputVersions)
  assert.equal(call.job.doorSource.schema,'16029-variable-door-reopen-source/v1');assert.equal(call.job.doorSource.run,fixture.run)
  assert.equal(call.job.doorSource.evidence.length,7);assert.equal(call.job.doorSource.artifacts.length,6)
  for(const item of call.job.doorSource.evidence)assert.equal(item.sha256,hash(item.path))
  for(const item of call.job.doorSource.artifacts) {
    assert.equal(item.partSha256,hash(join(fixture.source,'outputs',item.folder,item.part)))
    assert.equal(item.dxfSha256,hash(join(fixture.source,'outputs',item.folder,'flat.dxf')))
    assert.equal(item.nativeCutSha256,hash(join(fixture.source,'outputs',item.folder,'flat.native-cut.dxf')))
    assert.equal(item.nativeBendsSha256,hash(join(fixture.source,'outputs',item.folder,'flat.native-bends.dxf')))
  }
  assert.deepEqual(readdirSync(fixture.dataRoot).sort(),before)
  assert.equal(existsSync(join(fixture.dataRoot,'doors-reopen-current.json')),false)
})

test('doors-reopen fixture matches the archived native three-DXF manifest and rejects its missing process readback',async t=>{
  const manifestPath=join(DATA,'runs/2026-11-13T01-00-00-839Z-T2-outputs-dxf-e377825c/cad/outputs/output-manifest.json')
  const before=hash(manifestPath),archived=read(manifestPath),fixture=doorSourceFixture(t)
  assert.equal(archived.cases.length,fixture.manifest.cases.length)
  const shape=item=>Object.fromEntries(['path','nativeCutPath','nativeBendsPath'].map(key=>[key,item.dxf[key].split(/[\\/]/).at(-1)]))
  for(const [index,item] of archived.cases.entries()) {
    assert.deepEqual(shape(fixture.manifest.cases[index]),shape(item))
    for(const key of ['sha256','nativeCutSha256','nativeBendsSha256'])assert.match(item.dxf[key],/^[A-F0-9]{64}$/)
    assert.equal(item.processReadback,undefined)
    delete fixture.manifest.cases[index].processReadback
  }
  fixture.json(fixture.manifestPath,fixture.manifest)
  await assert.rejects(fixture.invoke(),/FIXED_PROCESS_READBACK_REQUIRED/);assert.equal(fixture.calls.length,0)
  assert.equal(hash(manifestPath),before)
})

test('doors-reopen cannot fall back to T1 or recovery evidence when the successful output pointer is absent or invalid',async t=>{
  for(const scenario of ['missing','outside-run','different-cad','different-native'])await t.test(scenario,async t=>{
    const fixture=doorSourceFixture(t),pointerPath=join(fixture.dataRoot,'outputs-dxf-current.json'),pointer=read(pointerPath)
    fixture.add('T1-current.json','variables');fixture.add('recovery-T4-current.json','relocate',dxfManifest())
    if(scenario==='missing')unlinkSync(pointerPath)
    else if(scenario==='outside-run')fixture.json(pointerPath,{...pointer,dir:fixture.dataRoot})
    else if(scenario==='different-cad')fixture.json(pointerPath,{...pointer,cadDirectory:fixture.dataRoot})
    else fixture.json(pointerPath,{...pointer,nativeResult:join(fixture.dataRoot,'native-result.json')})
    await assert.rejects(fixture.invoke());assert.equal(fixture.calls.length,0)
  })
})

test('doors-reopen rejects incomplete or contradictory generation acceptance evidence before running a phase',async t=>{
  const scenarios=[
    ['missing-result',f=>unlinkSync(join(f.run,'result.json'))],
    ['runner-code',f=>f.mutate('result.json',v=>{v.code=1})],
    ['runner-code-string',f=>f.mutate('result.json',v=>{v.code='0'})],
    ['runner-cad-alive',f=>f.mutate('result.json',v=>{v.cadExited=false})],
    ['native-action',f=>f.mutate('native-result.json',v=>{v.action='relocate'})],
    ['native-release',f=>f.mutate('native-result.json',v=>{v.production_release=true})],
    ['native-failure',f=>f.mutate('native-result.json',v=>{v.events.at(-1).success=false})],
    ['native-unfinished',f=>f.mutate('native-result.json',v=>{v.events.pop()})],
    ['native-cad-alive',f=>f.mutate('native-result.json',v=>{v.events.at(-1).cadExited=false})],
    ['native-error',f=>f.mutate('native-result.json',v=>{v.events.at(-1).error='failure'})],
    ['native-claim-mismatch',f=>f.mutate('native-result.json',v=>{v.cadPid++})],
    ['native-duplicate-claim',f=>f.mutate('native-result.json',v=>{v.events.unshift(v.events[0])})],
    ['supervision-running',f=>f.mutate('supervision.json',v=>{v.state='waiting_for_cad_exit'})],
    ['supervision-native-running',f=>f.mutate('supervision.json',v=>{v.nativeEnded=false})],
    ['supervision-monitor-remains',f=>f.mutate('supervision.json',v=>{v.cad=[{name:'sldProcMon.exe',pid:1235}]})],
    ['protection-failed',f=>f.mutate('protection-result.json',v=>{v.pass=false})],
    ['protection-changed',f=>f.mutate('protection-result.json',v=>{v.changed=['protected-source']})],
    ['protection-index-rejected',f=>f.mutate('protection-result.json',v=>{v.nativeTaskIndex.accepted=false})],
    ['protection-index-coerced',f=>f.mutate('protection-result.json',v=>{v.nativeTaskIndex.accepted='true'})],
    ['cancelled',f=>f.json(join(f.run,'cancel.json'),{reason:'historical supervision failure'})],
  ]
  for(const [name,change] of scenarios)await t.test(name,async t=>{
    const fixture=doorSourceFixture(t);change(fixture)
    await assert.rejects(fixture.invoke());assert.equal(fixture.calls.length,0)
  })
})

test('doors-reopen binds source job, canonical compilation and the exact current input bytes',async t=>{
  const scenarios=[
    ['job-action',f=>f.mutate('job.json',v=>{v.action='outputs'})],
    ['job-scope',f=>f.mutate('job.json',v=>{v.outputScope='parts_dxf_pdf'})],
    ['job-release',f=>f.mutate('job.json',v=>{v.production_release=true})],
    ['job-cad-directory',f=>f.mutate('job.json',v=>{v.cadDirectory=f.dataRoot})],
    ['job-result-path',f=>f.mutate('job.json',v=>{v.resultPath=join(f.dataRoot,'native-result.json')})],
    ['compiled-process-variable',f=>f.mutate('job.json',v=>{v.doors[0].variables.BEND_DED=1.5})],
    ['compiled-process-property',f=>f.mutate('job.json',v=>{v.doors[0].properties.thicknessMm=1})],
    ['compiled-process-record',f=>f.mutate('job.json',v=>{v.doors[0].process.innerRadiusMm=0.3})],
    ['source-process',f=>f.mutate('job.json',v=>{v.process.bendDeductionMm=1.5})],
    ['source-specification',f=>f.mutate('job.json',v=>{v.specification.doorCases[0].widthMm=438})],
    ['source-layout',f=>f.mutate('job.json',v=>{v.layouts[0].doorLeafWidthMm=438})],
    ['input-sha',f=>f.mutate('job.json',v=>{v.inputVersions[0].sha256='A'.repeat(64)})],
    ['input-path',f=>f.mutate('job.json',v=>{v.inputVersions[0].path=f.processPath})],
    ['input-count',f=>f.mutate('job.json',v=>{v.inputVersions.pop()})],
    ['current-process-version',f=>f.json(f.processPath,{...processInput,version:'v2'})],
    ['current-input-bytes',f=>writeFileSync(f.specPath,JSON.stringify(spec,null,2))],
    ['fixed-process-change',f=>f.json(f.processPath,{...processInput,bendDeductionMm:1.5})],
  ]
  for(const [name,change] of scenarios)await t.test(name,async t=>{
    const fixture=doorSourceFixture(t);change(fixture)
    await assert.rejects(fixture.invoke());assert.equal(fixture.calls.length,0)
  })
})

test('doors-reopen rejects changed, incomplete or out-of-scope output artifacts before running a phase',async t=>{
  const scenarios=[
    ['manifest-incomplete',f=>{f.manifest.complete=false}],
    ['manifest-schema',f=>{f.manifest.schema='16029-variable-output-manifest/v1'}],
    ['manifest-scope',f=>{f.manifest.outputScope='parts_dxf_pdf'}],
    ['manifest-drawing-status',f=>{f.manifest.drawingStatus='complete'}],
    ['manifest-release',f=>{f.manifest.production_release=true}],
    ['case-missing',f=>{f.manifest.cases.pop()}],
    ['case-duplicate',f=>{f.manifest.cases[1]=structuredClone(f.manifest.cases[0])}],
    ['case-unknown',f=>{f.manifest.cases[0].widthMm=438}],
    ['case-dimension-string',f=>{f.manifest.cases[0].widthMm='437'}],
    ['case-drawing',f=>{f.manifest.cases[0].drawing={}}],
    ['flat-failed',f=>{f.manifest.cases[0].flatPreparation.success=false}],
    ['dxf-failed',f=>{f.manifest.cases[0].dxf.success=false}],
    ['process-readback-missing',f=>{delete f.manifest.cases[0].processReadback}],
    ['process-readback-schema',f=>{f.manifest.cases[0].processReadback.schema='unsupported'}],
    ['process-readback-incomplete',f=>{f.manifest.cases[0].processReadback.rawReadbackComplete=false}],
    ['process-readback-override-mismatch',f=>{f.manifest.cases[0].processReadback.fixedExplicitOverridesMatch=false}],
    ['process-readback-candidate-unproven',f=>{f.manifest.cases[0].processReadback.candidateFixedProcessMatches=false}],
    ['process-readback-topology-mismatch',f=>{f.manifest.cases[0].processReadback.pilotTopologyMatches=false}],
    ['process-readback-coerced',f=>{f.manifest.cases[0].processReadback.candidateFixedProcessMatches='true'}],
    ['process-readback-inheritance-overclaim',f=>{f.manifest.cases[0].processReadback.effectiveInheritanceVerified=true}],
    ['process-readback-orientation-overclaim',f=>{f.manifest.cases[0].processReadback.manufacturingOrientationVerified=true}],
    ['process-readback-manufacturing-overclaim',f=>{f.manifest.cases[0].processReadback.manufacturingAccepted=true}],
    ['folder-escape',f=>{f.manifest.cases[0].folder='../outside'}],
    ['part-escape',f=>{f.manifest.cases[0].part='../outside.SLDPRT'}],
    ['part-sha',f=>{f.manifest.cases[0].partSha256='A'.repeat(64)}],
    ['dxf-sha',f=>{f.manifest.cases[0].dxf.sha256='A'.repeat(64)}],
    ['dxf-path',f=>{f.manifest.cases[0].dxf.path=join(f.dataRoot,'flat.dxf')}],
    ['native-cut-path-missing',f=>{delete f.manifest.cases[0].dxf.nativeCutPath}],
    ['native-bends-path-missing',f=>{delete f.manifest.cases[0].dxf.nativeBendsPath}],
    ['native-cut-path',f=>{f.manifest.cases[0].dxf.nativeCutPath=join(f.dataRoot,'flat.native-cut.dxf')}],
    ['native-bends-path',f=>{f.manifest.cases[0].dxf.nativeBendsPath=join(f.dataRoot,'flat.native-bends.dxf')}],
    ['native-cut-sha',f=>{f.manifest.cases[0].dxf.nativeCutSha256='A'.repeat(64)}],
    ['native-bends-sha',f=>{f.manifest.cases[0].dxf.nativeBendsSha256='A'.repeat(64)}],
    ['part-bytes',f=>{const c=f.manifest.cases[0];writeFileSync(join(f.source,'outputs',c.folder,c.part),'tampered')}],
    ['dxf-bytes',f=>{const c=f.manifest.cases[0];writeFileSync(join(f.source,'outputs',c.folder,'flat.dxf'),'tampered')}],
    ['native-cut-bytes',f=>{const c=f.manifest.cases[0];writeFileSync(join(f.source,'outputs',c.folder,'flat.native-cut.dxf'),'tampered')}],
    ['native-bends-bytes',f=>{const c=f.manifest.cases[0];writeFileSync(join(f.source,'outputs',c.folder,'flat.native-bends.dxf'),'tampered')}],
    ['part-missing',f=>{const c=f.manifest.cases[0];unlinkSync(join(f.source,'outputs',c.folder,c.part))}],
    ['native-cut-missing',f=>{const c=f.manifest.cases[0];unlinkSync(join(f.source,'outputs',c.folder,'flat.native-cut.dxf'))}],
    ['native-bends-missing',f=>{const c=f.manifest.cases[0];unlinkSync(join(f.source,'outputs',c.folder,'flat.native-bends.dxf'))}],
    ['extra-part',f=>writeFileSync(join(f.source,'outputs','extra.SLDPRT'),'unexpected')],
    ['extra-dxf',f=>writeFileSync(join(f.source,'outputs','extra.dxf'),'unexpected')],
    ['extra-drawing',f=>writeFileSync(join(f.source,'outputs','unverified.SLDDRW'),'unexpected')],
    ['extra-pdf',f=>writeFileSync(join(f.source,'outputs','unverified.pdf'),'unexpected')],
    ['extra-assembly',f=>writeFileSync(join(f.source,'outputs','unverified.SLDASM'),'unexpected')],
  ]
  for(const [name,change] of scenarios)await t.test(name,async t=>{
    const fixture=doorSourceFixture(t);change(fixture);fixture.json(fixture.manifestPath,fixture.manifest)
    await assert.rejects(fixture.invoke());assert.equal(fixture.calls.length,0)
  })
})

test('opt-in parametric width envelope accepts 700..1200mm while preserving default pilot restrictions',async t=>{
  const customSpec=structuredClone(spec)
  customSpec.assemblyCases[0].cabinet.widthMm=800
  customSpec.assemblyCases[1].cabinet.widthMm=1100
  assert.throws(()=>preflight(customSpec,processInput),/PILOT_PROFILE/)
  for(const optioned of [preflight(customSpec,processInput,{parametricWidth:true}),preflight({...customSpec,envelopeMode:'parametric_width'},processInput)]) {
    assert.deepEqual(optioned.layouts.map(l=>[l.cabinet.widthMm,l.doorLeafWidthMm]),[[800,337],[1100,487],[900,387]])
  }
  for(const [field,value,pattern] of [['widthMm',699,/700–1200 mm/],['widthMm',1201,/700–1200 mm/],['heightMm',2100,/WIDTH_ENVELOPE/],['depthMm',550,/WIDTH_ENVELOPE/]]) {
    const invalid=structuredClone(customSpec);invalid.assemblyCases[0].cabinet[field]=value
    assert.throws(()=>preflight(invalid,processInput,{parametricWidth:true}),pattern)
  }
  assert.deepEqual(parseCliArguments(['assembly','--parametric-width']),{mode:'assembly',specPath:undefined,processPath:undefined,parametricWidth:true})
  assert.deepEqual(parseCliArguments(['assembly','a.json','b.json','--parametric-width']),{mode:'assembly',specPath:'a.json',processPath:'b.json',parametricWidth:true})
  assert.throws(()=>parseCliArguments(['assembly','--unknown-flag']),/UNSUPPORTED_OPTION/)

  const fixture=sourceFixture(t);fixture.add('T1-current.json','variables')
  const specPath=join(fixture.dataRoot,'custom-width-spec.json')
  fixture.json(specPath,customSpec)
  const pilotCall=await execute('assembly',undefined,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v})
  assert.equal('envelopeMode' in pilotCall.job,false)
  const optInCall=await execute('assembly',specPath,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,parametricWidth:true})
  assert.equal(optInCall.job.envelopeMode,'parametric_width')
  assert.deepEqual(optInCall.job.layouts.map(l=>l.cabinet.widthMm),[800,1100,900])
})

test('opt-in --case isolates one assembly layout without overwriting assembly-current.json',async t=>{
  assert.deepEqual(parseCliArguments(['assembly','--case','3']),{mode:'assembly',specPath:undefined,processPath:undefined,parametricWidth:false,caseIndex:3})
  assert.deepEqual(parseCliArguments(['assembly','custom.json','--parametric-width','--case=2']),{mode:'assembly',specPath:'custom.json',processPath:undefined,parametricWidth:true,caseIndex:2})
  for(const bad of [['assembly','--case','0'],['assembly','--case=abc'],['assembly','--case'],['assembly','--case','1','--case','2'],['outputs-dxf','--case','1']]) {
    assert.throws(()=>parseCliArguments(bad))
  }
  const fixture=sourceFixture(t);fixture.add('T1-current.json','variables')
  const call=await execute('assembly',undefined,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,caseIndex:3})
  assert.equal(call.tag,'T3-assembly-case3')
  assert.equal(call.currentFile,null)
  assert.equal(call.job.onlyCaseIndex,3)
  assert.equal(call.job.layouts.length,3)
  await assert.rejects(()=>execute('assembly',undefined,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,caseIndex:4}),/INVALID_CASE_INDEX/)
  await assert.rejects(()=>execute('outputs-dxf',undefined,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,caseIndex:1}),/CASE_OPTION_REQUIRES_ASSEMBLY_MODE/)
})

test('opt-in parametric envelope accepts arbitrary W/H/D within 700..1200 x 1700..2200 x 250..650',async t=>{
  const envSpec=structuredClone(spec)
  envSpec.assemblyCases=[
    {cabinet:{widthMm:740,heightMm:1700,depthMm:250},columns:[{side:'L',doors:[{heightUnits:1},{heightUnits:1},{heightUnits:1}]},{side:'R',doors:[{heightUnits:1},{heightUnits:1},{heightUnits:1}]}]},
    {cabinet:{widthMm:1200,heightMm:2200,depthMm:650},columns:[{side:'L',doors:[{heightUnits:1},{heightUnits:2},{heightUnits:1}]},{side:'R',doors:[{heightUnits:2},{heightUnits:1},{heightUnits:1}]}]},
  ]
  assert.throws(()=>preflight(envSpec,processInput),/PILOT_PROFILE|HEIGHT|1700/)
  for(const optioned of [preflight(envSpec,processInput,{parametricEnvelope:true}),preflight({...envSpec,envelopeMode:'parametric_envelope'},processInput)]) {
    assert.deepEqual(optioned.layouts.map(l=>[l.cabinet.widthMm,l.cabinet.heightMm,l.cabinet.depthMm,l.doorLeafWidthMm]),[[740,1700,250,307],[1200,2200,650,537]])
  }
  assert.throws(()=>preflight(envSpec,processInput,{parametricWidth:true,parametricEnvelope:true}),/CONFLICTING_ENVELOPE_MODES/)
  for(const [field,value] of [['heightMm',1699],['heightMm',2201],['depthMm',249],['depthMm',651],['widthMm',699],['widthMm',1201]]) {
    const bad=structuredClone(envSpec);bad.assemblyCases[0].cabinet[field]=value
    assert.throws(()=>preflight(bad,processInput,{parametricEnvelope:true}))
  }
  assert.deepEqual(parseCliArguments(['assembly','--parametric-envelope']),{mode:'assembly',specPath:undefined,processPath:undefined,parametricWidth:false,parametricEnvelope:true})
  assert.deepEqual(parseCliArguments(['assembly','--full-cabinet','--case=2']),{mode:'assembly',specPath:undefined,processPath:undefined,parametricWidth:false,parametricEnvelope:true,caseIndex:2})
  assert.throws(()=>parseCliArguments(['assembly','--parametric-width','--parametric-envelope']),/CONFLICTING_ENVELOPE_MODES/)
  assert.throws(()=>parseCliArguments(['outputs','--parametric-envelope']),/PARAMETRIC_ENVELOPE_REQUIRES_ASSEMBLY_MODE/)

  const fixture=sourceFixture(t);fixture.add('T1-current.json','variables')
  const specPath=join(fixture.dataRoot,'envelope-spec.json')
  fixture.json(specPath,envSpec)
  const fullCall=await execute('assembly',specPath,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,parametricEnvelope:true})
  assert.equal(fullCall.tag,'T3-envelope')
  assert.equal(fullCall.currentFile,null)
  assert.equal(fullCall.job.envelopeMode,'parametric_envelope')
  assert.deepEqual(fullCall.job.layouts.map(l=>[l.cabinet.widthMm,l.cabinet.heightMm,l.cabinet.depthMm]),[[740,1700,250],[1200,2200,650]])
  const singleCall=await execute('assembly',specPath,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,parametricEnvelope:true,caseIndex:2})
  assert.equal(singleCall.tag,'T3-envelope-case2')
  assert.equal(singleCall.currentFile,null)
  assert.equal(singleCall.job.onlyCaseIndex,2)
  let capturedPhaseOpts=null
  await execute('assembly',specPath,undefined,{dataRoot:fixture.dataRoot,runPhase:async (v,opts)=>{capturedPhaseOpts=opts;return v},parametricEnvelope:true,caseIndex:1})
  assert.equal(capturedPhaseOpts?.executeNative,runParametricEnvelopePhases)
  await assert.rejects(()=>execute('outputs',specPath,undefined,{dataRoot:fixture.dataRoot,runPhase:async v=>v,parametricEnvelope:true}),/PARAMETRIC_ENVELOPE_REQUIRES_ASSEMBLY_MODE/)
})

test('runParametricEnvelopePhases isolates per-case prepare and build phases and archives artifacts cleanly', async t => {
  const dir=mkdtempSync(join(tmpdir(),'env-phases-'))
  t.after(()=>rmSync(dir,{recursive:true,force:true}))
  const seen=[]
  const mockRunNative=async (job,runDir)=>{
    assert.equal(existsSync(join(runDir,'toolchain.json')),false,'toolchain.json must be cleared before each new phase')
    seen.push({onlyCaseIndex:job.onlyCaseIndex,envelopePhase:job.envelopePhase})
    const marker=`case${job.onlyCaseIndex}-${job.envelopePhase}`
    writeFileSync(join(runDir,'toolchain.json'),JSON.stringify({marker}))
    writeFileSync(join(runDir,'job.json'),JSON.stringify({onlyCaseIndex:job.onlyCaseIndex,envelopePhase:job.envelopePhase}))
    writeFileSync(join(runDir,'native-result.json'),JSON.stringify({marker}))
    writeFileSync(join(runDir,'native.stdout.log'),marker+'\n')
    writeFileSync(join(runDir,'supervision.json'),JSON.stringify({state:'cad_exited',marker}))
    return {code:0,cadExited:true,marker}
  }
  const singleRes=await runParametricEnvelopePhases({action:'assembly',envelopeMode:'parametric_envelope',onlyCaseIndex:2,layouts:[{},{}]},dir,{runNative:mockRunNative})
  assert.deepEqual(seen,[{onlyCaseIndex:2,envelopePhase:'prepare'},{onlyCaseIndex:2,envelopePhase:'build_and_measure'}])
  assert.equal(singleRes.phases.length,2)
  assert.equal(singleRes.marker,'case2-build_and_measure')
  assert.equal(read(join(dir,'phases','case2-prepare','job.json')).envelopePhase,'prepare')
  assert.equal(read(join(dir,'phases','case2-build','job.json')).envelopePhase,'build_and_measure')
  assert.equal(read(join(dir,'native-result.json')).marker,'case2-build_and_measure')

  const multiDir=mkdtempSync(join(tmpdir(),'env-phases-multi-'))
  t.after(()=>rmSync(multiDir,{recursive:true,force:true}))
  seen.length=0
  const multiRes=await runParametricEnvelopePhases({action:'assembly',envelopeMode:'parametric_envelope',layouts:[{},{}]},multiDir,{runNative:mockRunNative})
  assert.deepEqual(seen,[
    {onlyCaseIndex:1,envelopePhase:'prepare'},
    {onlyCaseIndex:1,envelopePhase:'build_and_measure'},
    {onlyCaseIndex:2,envelopePhase:'prepare'},
    {onlyCaseIndex:2,envelopePhase:'build_and_measure'},
  ])
  assert.equal(multiRes.phases.length,4)
  for(const sub of ['case1-prepare','case1-build','case2-prepare','case2-build']) {
    assert.ok(existsSync(join(multiDir,'phases',sub,'native-result.json')),'missing '+sub)
  }

  const failDir=mkdtempSync(join(tmpdir(),'env-phases-fail-'))
  t.after(()=>rmSync(failDir,{recursive:true,force:true}))
  await assert.rejects(()=>runParametricEnvelopePhases({action:'assembly',envelopeMode:'parametric_envelope',onlyCaseIndex:1,layouts:[{}]},failDir,{
    runNative:async (job,runDir)=>{
      writeFileSync(join(runDir,'toolchain.json'),'{}')
      writeFileSync(join(runDir,'native-result.json'),JSON.stringify({phase:job.envelopePhase}))
      if(job.envelopePhase==='build_and_measure') throw new Error('INJECTED_BUILD_PHASE_FAILURE')
      return {code:0,cadExited:true}
    },
  }),/INJECTED_BUILD_PHASE_FAILURE/)
  assert.equal(read(join(failDir,'phases','case1-prepare','native-result.json')).phase,'prepare')
  assert.equal(read(join(failDir,'phases','case1-build','native-result.json')).phase,'build_and_measure')
  assert.equal(read(join(failDir,'native-result.json')).phase,'build_and_measure')
})
