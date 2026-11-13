import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync, realpathSync, cpSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, relative } from 'node:path'
import { read, hash, SOURCE } from './common.mjs'
import { preflight } from './execute.mjs'
import { recoverySource, outputArtifacts, recover, assemblyResumeSource, resumeAssembly } from './recover.mjs'

function fixture(t) {
  const dataRoot=mkdtempSync(join(realpathSync.native(tmpdir()),'winnsen-artifact-recovery-'))
  t.after(()=>rmSync(dataRoot,{recursive:true,force:true}))
  const run=join(dataRoot,'runs','failed-outputs'),root=join(run,'cad/outputs')
  mkdirSync(root,{recursive:true})
  const json=(path,value)=>writeFileSync(path,JSON.stringify(value))
  const specification=read(join(SOURCE,'inputs/specification.json')),processInput=read(join(SOURCE,'inputs/process.json'))
  const compiled=preflight(specification,processInput)
  const job={action:'outputs-dxf',outputScope:'parts_dxf_only',production_release:false,specification,process:processInput,...compiled,
    inputVersions:['specification.json','process.json'].map(name=>{const path=join(SOURCE,'inputs',name);return {path,sha256:hash(path)}})}
  json(join(run,'job.json'),job)
  const claim={inspection:'cad_owned',pid:1001,startedAtUtc:'2026-11-13T01:00:00Z',executable:'D:/cad/SLDWORKS.exe'}
  const native={action:'outputs-dxf',cadPid:1001,production_release:false,events:[claim,{inspection:'finished',success:true,cadExited:true}],eventTimes:[{}, {atUtc:'2026-11-13T01:40:00Z'}]}
  json(join(run,'native-result.json'),native)
  const mismatch={claim,observed:{pid:1001,name:'git.exe',executable:'C:/git/git.exe',startedAtUtc:'2026-11-13T01:40:01Z'}}
  json(join(run,'cancel.json'),{reason:'CAD_PROCESS_IDENTITY_MISMATCH '+JSON.stringify(mismatch),requestedAt:'2026-11-13T01:40:02Z'})
  json(join(run,'protection-result.json'),{pass:true,changed:[]})
  json(join(run,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[]})
  for(const name of ['native-index-protection.json','toolchain.json','copy-manifest.json'])json(join(run,name),{})
  const cases=specification.doorCases.map((item,index)=>{
    const folder=item.hand+index,part='door.SLDPRT',dir=join(root,folder)
    mkdirSync(dir);writeFileSync(join(dir,part),'part-'+index);writeFileSync(join(dir,'flat.dxf'),'dxf-'+index)
    return {...item,folder,part,partSha256:hash(join(dir,part)),drawing:null,dxf:{sha256:hash(join(dir,'flat.dxf'))}}
  })
  const manifest={schema:'16029-variable-output-manifest/v2',complete:true,outputScope:'parts_dxf_only',drawingStatus:'deferred_by_user_20261113',production_release:false,cases}
  json(join(root,'output-manifest.json'),manifest)
  const auditPath=join(dataRoot,'output-audit.json'),audit={schema:'16029.variable-trial.output-audit.v2',source:run,nativeResultSha256:hash(join(run,'native-result.json')),
    scopeEvidence:{manifestSha256:hash(join(root,'output-manifest.json')),jobSha256:hash(join(run,'job.json'))},outputScope:'parts_dxf_only',drawingStatus:'deferred_by_user_20261113',caseCount:6,
    dxfGeometryScopePass:true,cases:cases.map(c=>({...c,case:c.folder,part:join(root,c.folder,c.part),dxfGeometryPass:true})),fullPlanPass:false,production_release:false}
  json(auditPath,audit)
  return {dataRoot,run,root,json,job,native,mismatch,manifest,auditPath,audit,validate:()=>recoverySource(run,auditPath,{dataRoot})}
}

test('completed native artifacts enter bounded recovery without changing failed phase acceptance',t=>{
  const f=fixture(t),r=f.validate()
  assert.equal(r.recovery.oldSupervisionAccepted,false)
  assert.equal(r.recovery.oldFailurePreserved,true)
  assert.equal(r.recovery.recoveredArtifactsFromFailedPhase,true)
  assert.equal(r.recovery.outputs.length,6)
  assert.equal(r.recovery.production_release,false)
})
test('recovery refuses a successful phase, unrelated cancellation and reused CAD process',t=>{
  const f=fixture(t)
  f.json(join(f.run,'result.json'),{code:0})
  assert.throws(f.validate,/FAILED_SOURCE_ONLY/)
  rmSync(join(f.run,'result.json'))
  for(const observed of [{...f.mismatch.observed,name:'SLDWORKS.exe'}, {...f.mismatch.observed,pid:2222}, {...f.mismatch.observed,startedAtUtc:'2026-11-13T01:39:59Z'}]) {
    f.json(join(f.run,'cancel.json'),{reason:'CAD_PROCESS_IDENTITY_MISMATCH '+JSON.stringify({...f.mismatch,observed}),requestedAt:'2026-11-13T01:40:02Z'})
    assert.throws(f.validate,/POST_EXIT_GIT_PID_REUSE_REQUIRED/)
  }
  f.json(join(f.run,'cancel.json'),{reason:'TRIAL_INTERRUPTED'})
  assert.throws(f.validate,/SPECIFIC_PID_REUSE_REQUIRED/)
})
test('recovery refuses failed or incomplete native, protection and supervision evidence',t=>{
  const f=fixture(t)
  f.json(join(f.run,'native-result.json'),{...f.native,events:[{inspection:'finished',success:false,cadExited:true}]})
  assert.throws(f.validate,/NATIVE_OUTPUT_COMPLETION_REQUIRED/)
  f.json(join(f.run,'native-result.json'),f.native)
  f.json(join(f.run,'protection-result.json'),{pass:false,changed:['source']})
  assert.throws(f.validate,/SOURCE_PROTECTION_REQUIRED/)
  f.json(join(f.run,'protection-result.json'),{pass:true,changed:[]})
  f.json(join(f.run,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[{pid:1002}]})
  assert.throws(f.validate,/SOURCE_CAD_EXIT_REQUIRED/)
})
test('recovery binds actual artifacts and the exact geometry audit',t=>{
  const f=fixture(t)
  for(const change of [{dxfGeometryScopePass:false},{nativeResultSha256:'wrong'},{source:join(f.dataRoot,'runs','other')},{production_release:true},{caseCount:5}]) {
    f.json(f.auditPath,{...f.audit,...change})
    assert.throws(f.validate,/SIX_CASE_GEOMETRY_AUDIT_REQUIRED/)
  }
  f.json(f.auditPath,f.audit)
  writeFileSync(join(f.root,'L0','flat.dxf'),'changed')
  assert.throws(f.validate,/OUTPUT_HASH_MISMATCH/)
})
test('recovery rejects duplicate identities, path escape and changed compiled inputs',t=>{
  const f=fixture(t)
  f.json(join(f.root,'output-manifest.json'),{...f.manifest,cases:[f.manifest.cases[0],...f.manifest.cases.slice(0,5)]})
  assert.throws(()=>outputArtifacts(f.run,{dataRoot:f.dataRoot}),/EXACT_SIX_IDENTITIES_REQUIRED/)
  f.json(join(f.root,'output-manifest.json'),{...f.manifest,cases:f.manifest.cases.map((c,i)=>i?c:{...c,folder:'../../../escape'})})
  assert.throws(()=>outputArtifacts(f.run,{dataRoot:f.dataRoot}),/PATH_ESCAPE/)
  f.json(join(f.root,'output-manifest.json'),f.manifest)
  f.job.doors[0].identity='conflict';f.json(join(f.run,'job.json'),f.job)
  f.audit.scopeEvidence.jobSha256=hash(join(f.run,'job.json'));f.json(f.auditPath,f.audit)
  assert.throws(f.validate,/INPUT_COMPILATION_CHANGED/)
})
test('recovery refuses duplicate and misbound audit cases even when top-level hashes match',t=>{
  const f=fixture(t)
  for(const cases of [Array(6).fill(f.audit.cases[0]),f.audit.cases.map((c,i)=>i?c:{...c,hand:'R'}),f.audit.cases.map((c,i)=>i?c:{...c,part:join(f.root,'other.SLDPRT')})]) {
    f.json(f.auditPath,{...f.audit,cases})
    assert.throws(f.validate,/AUDIT_CASE_BINDING_REQUIRED/)
  }
})
function phaseRunner(f,mutate) {
  let index=0
  return async options=>{
    assert.equal(Object.hasOwn(options,'currentFile'),false)
    const run=join(f.dataRoot,'runs','recovered-'+(++index)+'-'+options.job.action)
    mkdirSync(run);cpSync(options.sourceDirectory,join(run,'cad'),{recursive:true,errorOnExist:true})
    f.json(join(run,'job.json'),options.job)
    f.json(join(run,'native-result.json'),{action:options.job.action,production_release:false,events:[{inspection:'finished',success:true,cadExited:true}]})
    f.json(join(run,'result.json'),{code:0,cadExited:true})
    f.json(join(run,'protection-result.json'),{pass:true,changed:[]})
    f.json(join(run,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[]})
    mutate?.(run)
    return {pass:true,dir:run}
  }
}
test('new supervised phases publish separate recovery pointers with exact source binding',async t=>{
  const f=fixture(t),options={dataRoot:f.dataRoot,runPhase:phaseRunner(f)}
  const t3=await recover('assembly',f.run,f.auditPath,options)
  assert.equal(read(join(f.dataRoot,'recovery-T3-current.json')).dir,t3.dir)
  assert.equal(existsSync(join(f.run,'result.json')),false)
  const t4=await recover('relocate',f.run,f.auditPath,options)
  assert.equal(read(join(f.dataRoot,'recovery-T4-current.json')).dir,t4.dir)
  assert.equal(read(join(t4.dir,'recovery-source-check.json')).sourcesUnchanged,true)
})
test('copied manifest evidence changes cannot publish a recovery pointer',async t=>{
  const f=fixture(t),runPhase=phaseRunner(f,run=>{
    const p=join(run,'cad/outputs/output-manifest.json'),m=read(p)
    m.cases[0].activeConfiguration='unverified';f.json(p,m)
  })
  await assert.rejects(recover('assembly',f.run,f.auditPath,{dataRoot:f.dataRoot,runPhase}),/COPIED_OUTPUTS_CHANGED/)
  assert.equal(existsSync(join(f.dataRoot,'recovery-T3-current.json')),false)
})
test('original output changes during a phase cannot publish a recovery pointer',async t=>{
  const f=fixture(t),runPhase=phaseRunner(f,()=>writeFileSync(join(f.root,'L0','flat.dxf'),'external-change'))
  await assert.rejects(recover('assembly',f.run,f.auditPath,{dataRoot:f.dataRoot,runPhase}),/OUTPUT_HASH_MISMATCH/)
  assert.equal(existsSync(join(f.dataRoot,'recovery-T3-current.json')),false)
})
test('phase failure and missing or invalid supervised evidence cannot publish recovery pointers',async t=>{
  for(const kind of ['phase','missing','protection','native']) {
    const f=fixture(t)
    const runPhase=kind==='phase'?async()=>({pass:false}):phaseRunner(f,run=>{
      if(kind==='missing')rmSync(join(run,'result.json'))
      if(kind==='protection')f.json(join(run,'protection-result.json'),{pass:false,changed:['source']})
      if(kind==='native')f.json(join(run,'native-result.json'),{action:'assembly',production_release:false,events:[{inspection:'finished',success:false,cadExited:true}]})
    })
    await assert.rejects(recover('assembly',f.run,f.auditPath,{dataRoot:f.dataRoot,runPhase}))
    assert.equal(existsSync(join(f.dataRoot,'recovery-T3-current.json')),false)
  }
})
test('relocation rejects a T3 pointer bound to a different recovery source',async t=>{
  const f=fixture(t),options={dataRoot:f.dataRoot,runPhase:phaseRunner(f)}
  const t3=await recover('assembly',f.run,f.auditPath,options)
  const path=join(t3.dir,'job.json'),job=read(path)
  job.recovery.sourceRun='different-source';f.json(path,job)
  await assert.rejects(recover('relocate',f.run,f.auditPath,options),/SOURCE_BINDING_CHANGED/)
  assert.equal(existsSync(join(f.dataRoot,'recovery-T4-current.json')),false)
})

function assemblyEvidence(f,cad,index) {
  const folder=join(cad,'assemblies','case'+index),part=join(folder,'component.SLDPRT'),assembly=join(folder,'16029_variable_minimum.SLDASM')
  mkdirSync(folder,{recursive:true});writeFileSync(part,'native-part-'+index);writeFileSync(assembly,'native-assembly-'+index)
  const instances=Array.from({length:3},(_,i)=>({identity:'role-'+i,component:'component-'+i,path:part,sha256:hash(part),side:'L',index:i+1,kind:'test-part',
    fixedState:true,transformPass:true,transform:[1,0,0,0,1,0,0,0,1,i/1000,0,0,1,0,0,0]}))
  const pairs=instances.flatMap((a,i)=>instances.slice(i+1).map(b=>({componentA:a.component,componentB:b.component,nativeDetectionComplete:true,measurementComplete:true,pass:false})))
  const evidence={schema:'16029-independent-variable-minimum-assembly/v1',production_release:false,success:true,acceptancePass:false,
    nativeReadbackPass:true,componentIdentityReadbackPass:true,documentMetadataReadbackPass:true,inputFilesUnchanged:true,changedInputFiles:[],
    layout:f.job.layouts[index-1],outputDirectory:folder,assemblyPath:assembly,assemblySha256:hash(assembly),
    minimumScope:{expectedComponentCount:instances.length},instances,parts:[{path:part,sha256:hash(part)}],
    expectedManifest:instances.map(({identity,path,side,index,kind,transform})=>({identity,path,side,index,kind,transform})),
    documentMetadata:instances.map(({identity})=>({identity,readbackComplete:true})),
    interference:{complete:true,errors:[],componentCount:instances.length,expectedPairCount:pairs.length,enumeratedPairCount:pairs.length,measuredPairCount:pairs.length,
      unknownPairCount:0,pairs,collisions:[{approved:false,pass:false,volumeMm3:5}],pass:false},
    reopen:{pass:true,coldSession:false,assemblySha256:hash(assembly),componentCount:instances.length}}
  const path=join(folder,'assembly-evidence.json');f.json(path,evidence)
  return {path,evidence,part,assembly}
}
function assemblyFixture(t) {
  const f=fixture(t),source=f.validate(),failedRun=join(f.dataRoot,'runs','failed-assemblies'),cad=join(failedRun,'cad')
  mkdirSync(failedRun);cpSync(join(f.run,'cad'),cad,{recursive:true,errorOnExist:true});writeFileSync(join(cad,'root-input.SLDPRT'),'protected-root-input')
  const cases=[1,2].map(index=>assemblyEvidence(f,cad,index)),failedFolder=join(cad,'assemblies/case3')
  mkdirSync(failedFolder)
  const failedCase={success:false,production_release:false,acceptancePass:false,layout:f.job.layouts[2],error:'ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED bottom-frame'}
  f.json(join(failedFolder,'assembly-evidence.json'),failedCase)
  const assemblyJob={...f.job,action:'assembly',recovery:source.recovery,sourceCadDirectory:join(f.run,'cad'),cadDirectory:cad}
  const error='MINIMUM_ASSEMBLY_GENERATION_FAILED case3',assemblyNative={action:'assembly',production_release:false,events:[
    ...cases.map((item,index)=>({inspection:'minimum_assembly',index:index+1,result:item.evidence})),
    {inspection:'minimum_assembly',index:3,result:failedCase},{inspection:'failure',error},{inspection:'finished',success:false,cadExited:true,error}]}
  f.json(join(failedRun,'job.json'),assemblyJob);f.json(join(failedRun,'native-result.json'),assemblyNative)
  f.json(join(failedRun,'protection-result.json'),{pass:true,changed:[]})
  f.json(join(failedRun,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[]})
  for(const name of ['toolchain.json','copy-manifest.json','native-index-protection.json','protected-before.json'])f.json(join(failedRun,name),{})
  return {...f,failedRun,cad,cases,assemblyJob,assemblyNative,validateResume:()=>assemblyResumeSource(f.validate(),failedRun,{dataRoot:f.dataRoot})}
}
function resumeRunner(f,mutate) {
  return async options=>{
    assert.equal(options.tag,'resume-T3');assert.equal(options.job.action,'assembly')
    assert.deepEqual(options.excludeRelativeDirectories,['assemblies/case3'])
    assert.deepEqual(options.job.layouts,f.job.layouts)
    assert.deepEqual(options.job.assemblyResume.generateCaseIndices,[3]);assert.deepEqual(options.job.assemblyResume.reuseCaseIndices,[1,2])
    f.options=options
    const run=join(f.dataRoot,'runs','resumed-assembly'),cad=join(run,'cad')
    mkdirSync(run);cpSync(options.sourceDirectory,cad,{recursive:true,errorOnExist:true,filter:path=>{
      const tail=relative(options.sourceDirectory,path).split('\\').join('/')
      return !options.excludeRelativeDirectories.some(excluded=>tail===excluded||tail.startsWith(excluded+'/'))
    }})
    assert.equal(existsSync(join(cad,'assemblies/case3')),false)
    const third=assemblyEvidence(f,cad,3)
    f.json(join(run,'job.json'),options.job)
    f.json(join(run,'native-result.json'),{action:'assembly',production_release:false,events:[
      {inspection:'minimum_assembly_reused',index:1},{inspection:'minimum_assembly_reused',index:2},
      {inspection:'minimum_assembly',index:3,result:third.evidence},{inspection:'finished',success:true,cadExited:true}]})
    f.json(join(run,'result.json'),{code:0,cadExited:true})
    f.json(join(run,'protection-result.json'),{pass:true,changed:[]})
    f.json(join(run,'supervision.json'),{state:'cad_exited',nativeEnded:true,cad:[]})
    mutate?.(run,options)
    return {pass:true,dir:run}
  }
}
test('resume binds both completed cases and all original source files without accepting failed batch',t=>{
  const f=assemblyFixture(t),resume=f.validateResume(),contract=resume.assemblyResume
  assert.deepEqual(contract.generateCaseIndices,[3]);assert.deepEqual(contract.reuseCaseIndices,[1,2])
  assert.equal(contract.oldFailurePreserved,true);assert.equal(contract.oldSupervisionAccepted,false)
  assert.equal(contract.mixedProvenance,true);assert.equal(contract.technicalRecoveryOnly,true)
  assert.equal(contract.acceptancePass,false);assert.equal(contract.fullPlanPass,false);assert.equal(contract.production_release,false)
  assert.deepEqual(contract.cases.map(c=>c.index),[1,2])
  for(const item of contract.cases) {
    assert.equal(item.evidenceSha256,hash(f.cases[item.index-1].path))
    assert.equal(item.assemblySha256,hash(f.cases[item.index-1].assembly))
    assert.deepEqual(item.layout,f.job.layouts[item.index-1])
    assert.ok(item.inventory.every(file=>file.relativePath.startsWith('assemblies/case'+item.index+'/')&&file.sha256===hash(join(f.cad,file.relativePath))))
  }
  assert.ok(resume.sourceInventory.some(item=>item.relativePath==='root-input.SLDPRT'))
  assert.ok(resume.sourceInventory.some(item=>item.relativePath.startsWith('assemblies/case3/')))
  assert.deepEqual(contract.sourceEvidence.map(item=>relative(f.failedRun,item.path)).sort(),['job.json','native-result.json','toolchain.json','protected-before.json','protection-result.json','supervision.json','copy-manifest.json','native-index-protection.json'].sort())
})
test('resume rejects a successful, cancelled, still-running or unrelated failed assembly batch',t=>{
  for(const kind of ['successful','cancelled','native-success','cad-running','other-case','other-error','protection','input-binding','recovery-binding']) {
    const f=assemblyFixture(t)
    if(kind==='successful')f.json(join(f.failedRun,'result.json'),{code:0})
    if(kind==='cancelled')f.json(join(f.failedRun,'cancel.json'),{reason:'TRIAL_INTERRUPTED'})
    if(kind==='native-success')f.assemblyNative.events.at(-1).success=true
    if(kind==='cad-running')f.json(join(f.failedRun,'supervision.json'),{state:'waiting_for_cad_exit',nativeEnded:true,cad:[{pid:1001}]})
    if(kind==='other-case')f.assemblyNative.events.at(-1).error='MINIMUM_ASSEMBLY_GENERATION_FAILED case2'
    if(kind==='other-error')f.assemblyNative.events[2].result.error='UNRELATED_FAILURE'
    if(kind==='protection')f.json(join(f.failedRun,'protection-result.json'),{pass:false,changed:['source']})
    if(kind==='input-binding')f.assemblyJob.layouts=f.assemblyJob.layouts.slice(1)
    if(kind==='recovery-binding')f.assemblyJob.recovery={...f.assemblyJob.recovery,oldSupervisionAccepted:true}
    f.json(join(f.failedRun,'job.json'),f.assemblyJob);f.json(join(f.failedRun,'native-result.json'),f.assemblyNative)
    assert.throws(f.validateResume,/RESUME_/)
  }
})
test('resume refuses case evidence not exactly bound to its native event',t=>{
  const f=assemblyFixture(t),e=read(f.cases[0].path)
  e.reopen.coldSession=true;f.json(f.cases[0].path,e)
  assert.throws(f.validateResume,/NATIVE_CASE_EVIDENCE_MISMATCH/)
})
test('resume rejects incomplete measurements, changed identity, duplicate pairs and acceptance promotion',t=>{
  for(const kind of ['readback','metadata','acceptance','release','input-changed','unknown-pair','missing-pair','duplicate-pair','identity','transform']) {
    const f=assemblyFixture(t),e=f.cases[0].evidence
    if(kind==='readback')e.nativeReadbackPass=false
    if(kind==='metadata')e.documentMetadataReadbackPass=false
    if(kind==='acceptance')e.acceptancePass=true
    if(kind==='release')e.production_release=true
    if(kind==='input-changed')e.inputFilesUnchanged=false
    if(kind==='unknown-pair')e.interference.unknownPairCount=1
    if(kind==='missing-pair')e.interference.pairs.pop()
    if(kind==='duplicate-pair')e.interference.pairs[1]={...e.interference.pairs[0]}
    if(kind==='identity')e.instances[1].identity=e.instances[0].identity
    if(kind==='transform') {e.instances[0].transform=[...e.instances[0].transform];e.instances[0].transform[9]+=.1}
    f.json(f.cases[0].path,e);f.json(join(f.failedRun,'native-result.json'),f.assemblyNative)
    assert.throws(f.validateResume,/RESUME_/)
  }
})
test('resume checks actual assembly, component, evidence paths and required source evidence files',t=>{
  for(const kind of ['assembly','part','missing-part','escape','missing-toolchain','missing-evidence','outputs']) {
    const f=assemblyFixture(t)
    if(kind==='assembly')writeFileSync(f.cases[0].assembly,'changed-assembly')
    if(kind==='part')writeFileSync(f.cases[1].part,'changed-part')
    if(kind==='missing-part')rmSync(f.cases[1].part)
    if(kind==='escape') {
      f.cases[0].evidence.instances[0].path=join(f.cad,'root-input.SLDPRT')
      f.json(f.cases[0].path,f.cases[0].evidence);f.json(join(f.failedRun,'native-result.json'),f.assemblyNative)
    }
    if(kind==='missing-toolchain')rmSync(join(f.failedRun,'toolchain.json'))
    if(kind==='missing-evidence')rmSync(f.cases[0].path)
    if(kind==='outputs')writeFileSync(join(f.cad,'outputs/L0/flat.dxf'),'changed-dxf')
    assert.throws(f.validateResume)
  }
})
test('resume publishes a separate mixed-provenance T3 after generating only case3 and supports T4',async t=>{
  const f=assemblyFixture(t),originalNative=hash(join(f.failedRun,'native-result.json')),originalEvidence=f.cases.map(item=>hash(item.path))
  const t3=await recover('resume-assembly',f.run,f.auditPath,{dataRoot:f.dataRoot,failedAssemblyRun:f.failedRun,runPhase:resumeRunner(f)})
  const pointer=read(join(f.dataRoot,'recovery-T3-current.json')),check=read(join(t3.dir,'assembly-resume-source-check.json'))
  assert.equal(pointer.dir,t3.dir);assert.equal(pointer.resumeSourceRun,f.failedRun);assert.equal(pointer.mixedProvenance,true)
  assert.equal(pointer.resumeSourceCheckSha256,hash(join(t3.dir,'assembly-resume-source-check.json')))
  assert.deepEqual(check.cases.map(item=>item.index),[1,2,3]);assert.equal(check.sourceCadFilesUnchanged,true)
  assert.equal(hash(join(f.failedRun,'native-result.json')),originalNative);assert.equal(existsSync(join(f.failedRun,'result.json')),false)
  for(const item of [1,2])assert.equal(hash(join(t3.dir,'cad/assemblies/case'+item+'/assembly-evidence.json')),originalEvidence[item-1])
  const t4=await recover('relocate',f.run,f.auditPath,{dataRoot:f.dataRoot,runPhase:phaseRunner(f)})
  assert.equal(read(join(f.dataRoot,'recovery-T4-current.json')).dir,t4.dir)
  assert.deepEqual(read(join(t4.dir,'job.json')).assemblyResume,f.options.job.assemblyResume)
  for(const item of check.cases)assert.equal(hash(join(t4.dir,'cad',item.evidenceRelativePath)),item.evidenceSha256)
})
test('resume records rewritten CAD hashes while preserving original evidence and source files',async t=>{
  const f=assemblyFixture(t),original=hash(f.cases[0].part)
  const result=await resumeAssembly(f.run,f.auditPath,f.failedRun,{dataRoot:f.dataRoot,runPhase:resumeRunner(f,run=>writeFileSync(join(run,'cad/assemblies/case1/component.SLDPRT'),'rewired-native-copy'))})
  const check=read(join(result.dir,'assembly-resume-source-check.json')),rewired=check.cases[0].inventory.find(item=>item.relativePath.endsWith('/component.SLDPRT'))
  assert.notEqual(rewired.sha256,original);assert.equal(hash(f.cases[0].part),original)
  assert.equal(check.cases[0].evidenceSha256,hash(f.cases[0].path))
})
test('resume cannot publish when generated cases, job contract, copied evidence or sources change',async t=>{
  for(const kind of ['repeat-case','missing-case','case3-failed','job','copied-evidence','extra-case-file','source-native','source-file','source-added-file','source-dxf','output-manifest']) {
    const f=assemblyFixture(t),runPhase=resumeRunner(f,run=>{
      const nativePath=join(run,'native-result.json'),native=read(nativePath)
      if(kind==='repeat-case')native.events.unshift({inspection:'minimum_assembly',index:1,result:f.cases[0].evidence})
      if(kind==='missing-case')rmSync(join(run,'cad/assemblies/case3'),{recursive:true})
      if(kind==='case3-failed')native.events[2].result.success=false
      if(kind==='job') {const path=join(run,'job.json'),job=read(path);job.assemblyResume.generateCaseIndices=[1,2,3];f.json(path,job)}
      if(kind==='copied-evidence')writeFileSync(join(run,'cad/assemblies/case1/assembly-evidence.json'),'changed')
      if(kind==='extra-case-file')writeFileSync(join(run,'cad/assemblies/case2/untracked.txt'),'new')
      if(kind==='source-native')f.json(join(f.failedRun,'native-result.json'),{changed:true})
      if(kind==='source-file')writeFileSync(join(f.cad,'root-input.SLDPRT'),'changed-root-input')
      if(kind==='source-added-file')writeFileSync(join(f.cad,'untracked.txt'),'new')
      if(kind==='source-dxf')writeFileSync(join(f.root,'L0/flat.dxf'),'changed-old-dxf')
      if(kind==='output-manifest') {const path=join(run,'cad/outputs/output-manifest.json'),manifest=read(path);manifest.untracked=true;f.json(path,manifest)}
      f.json(nativePath,native)
    })
    await assert.rejects(resumeAssembly(f.run,f.auditPath,f.failedRun,{dataRoot:f.dataRoot,runPhase}))
    assert.equal(existsSync(join(f.dataRoot,'recovery-T3-current.json')),false)
  }
})
test('resume requires successful newly supervised execution before publishing a pointer',async t=>{
  for(const kind of ['phase','missing-result','failed-native','guard','supervision']) {
    const f=assemblyFixture(t),runPhase=kind==='phase'?async()=>({pass:false}):resumeRunner(f,run=>{
      if(kind==='missing-result')rmSync(join(run,'result.json'))
      if(kind==='failed-native') {const path=join(run,'native-result.json'),native=read(path);native.events.at(-1).success=false;f.json(path,native)}
      if(kind==='guard')f.json(join(run,'protection-result.json'),{pass:false,changed:['source']})
      if(kind==='supervision')f.json(join(run,'supervision.json'),{state:'native_running',cad:[],nativeEnded:false})
    })
    await assert.rejects(resumeAssembly(f.run,f.auditPath,f.failedRun,{dataRoot:f.dataRoot,runPhase}))
    assert.equal(existsSync(join(f.dataRoot,'recovery-T3-current.json')),false)
  }
})
test('T4 rejects changed mixed-provenance source checks, case files and original failed evidence before execution',async t=>{
  for(const kind of ['pointer','receipt','case-file','root-file','case-evidence','original-evidence']) {
    const f=assemblyFixture(t),t3=await resumeAssembly(f.run,f.auditPath,f.failedRun,{dataRoot:f.dataRoot,runPhase:resumeRunner(f)})
    if(kind==='pointer') {const path=join(f.dataRoot,'recovery-T3-current.json'),pointer=read(path);pointer.resumeSourceRun=f.run;f.json(path,pointer)}
    if(kind==='receipt') {const path=join(t3.dir,'assembly-resume-source-check.json'),check=read(path);check.sourceCadFilesUnchanged=false;f.json(path,check)}
    if(kind==='case-file')writeFileSync(join(t3.dir,'cad/assemblies/case2/component.SLDPRT'),'later-change')
    if(kind==='root-file')writeFileSync(join(t3.dir,'cad/root-input.SLDPRT'),'later-change')
    if(kind==='case-evidence')writeFileSync(join(t3.dir,'cad/assemblies/case1/assembly-evidence.json'),'later-change')
    if(kind==='original-evidence')f.json(join(f.failedRun,'toolchain.json'),{changed:true})
    let called=false
    await assert.rejects(recover('relocate',f.run,f.auditPath,{dataRoot:f.dataRoot,runPhase:async()=>{called=true;return {pass:false}}}))
    assert.equal(called,false);assert.equal(existsSync(join(f.dataRoot,'recovery-T4-current.json')),false)
  }
})
