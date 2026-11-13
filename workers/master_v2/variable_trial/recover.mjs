import { existsSync, lstatSync, readdirSync } from 'node:fs'
import { basename, dirname, join, resolve, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { isDeepStrictEqual } from 'node:util'
import { DATA, SOURCE, inside, read, hash, write, files } from './common.mjs'
import { preflight } from './execute.mjs'
import { runProtectedPhase } from './run.mjs'

const need=(value,message)=>{if(!value)throw new Error(message)}
const done=n=>n.production_release===false&&n.events?.at(-1)?.inspection==='finished'&&n.events.at(-1).success===true&&n.events.at(-1).cadExited===true
function trialRun(path,dataRoot) {
  const run=inside(resolve(path),join(dataRoot,'runs'))
  need(dirname(run)===join(dataRoot,'runs'),'DIRECT_TRIAL_RUN_REQUIRED')
  return run
}
export function outputArtifacts(run,{dataRoot=DATA}={}) {
  run=trialRun(run,dataRoot)
  const root=join(run,'cad/outputs'),manifestPath=join(root,'output-manifest.json'),manifest=read(manifestPath)
  need(manifest.schema==='16029-variable-output-manifest/v2'&&manifest.complete===true&&manifest.outputScope==='parts_dxf_only'
    &&manifest.drawingStatus==='deferred_by_user_20261113'&&manifest.production_release===false&&manifest.cases?.length===6,'RECOVERY_COMPLETE_DXF_SCOPE_REQUIRED')
  const expected=new Set(['L/437/471.25','R/437/471.25','L/287/200','R/287/200','L/537/1000','R/537/1000']),artifacts=[]
  for(const item of manifest.cases) {
    need(expected.delete([item.hand,item.widthMm,item.heightMm].join('/'))&&item.drawing===null,'RECOVERY_EXACT_SIX_IDENTITIES_REQUIRED')
    need(typeof item.folder==='string'&&typeof item.part==='string','RECOVERY_PART_PATH_REQUIRED')
    const part=inside(join(root,item.folder,item.part),root),dxf=inside(join(dirname(part),'flat.dxf'),root)
    need(hash(part)===item.partSha256&&hash(dxf)===item.dxf?.sha256,'RECOVERY_OUTPUT_HASH_MISMATCH')
    artifacts.push({folder:item.folder,part:item.part,partSha256:item.partSha256,dxfSha256:item.dxf.sha256})
  }
  need(expected.size===0,'RECOVERY_EXACT_SIX_IDENTITIES_REQUIRED')
  return {manifestPath,manifestSha256:hash(manifestPath),artifacts}
}
export function recoverySource(sourceRun,auditPath,{dataRoot=DATA}={}) {
  const run=trialRun(sourceRun,dataRoot),native=read(join(run,'native-result.json')),job=read(join(run,'job.json'))
  need(done(native)&&native.action==='outputs-dxf'&&job.action==='outputs-dxf'&&job.outputScope==='parts_dxf_only'&&job.production_release===false,'RECOVERY_NATIVE_OUTPUT_COMPLETION_REQUIRED')
  need(!existsSync(join(run,'result.json')),'RECOVERY_FAILED_SOURCE_ONLY')
  const protection=read(join(run,'protection-result.json')),supervision=read(join(run,'supervision.json'))
  need(protection.pass===true&&protection.changed?.length===0,'RECOVERY_SOURCE_PROTECTION_REQUIRED')
  need(supervision.state==='cad_exited'&&supervision.nativeEnded===true&&supervision.cad?.length===0,'RECOVERY_SOURCE_CAD_EXIT_REQUIRED')
  const cancel=read(join(run,'cancel.json')),prefix='CAD_PROCESS_IDENTITY_MISMATCH '
  need(typeof cancel.reason==='string'&&cancel.reason.startsWith(prefix),'RECOVERY_SPECIFIC_PID_REUSE_REQUIRED')
  const mismatch=JSON.parse(cancel.reason.slice(prefix.length)),claim=mismatch.claim,observed=mismatch.observed
  const owned=native.events.filter(e=>e.inspection==='cad_owned')
  need(owned.length===1&&isDeepStrictEqual(owned[0],claim)&&native.cadPid===claim.pid&&observed.pid===claim.pid
    &&observed.name==='git.exe'&&typeof observed.executable==='string'&&basename(observed.executable).toLowerCase()==='git.exe'
    &&Date.parse(observed.startedAtUtc)>Date.parse(claim.startedAtUtc)
    &&Date.parse(observed.startedAtUtc)>Date.parse(native.eventTimes?.at(-1)?.atUtc)
    &&Date.parse(cancel.requestedAt)>=Date.parse(observed.startedAtUtc),'RECOVERY_CONFIRMED_POST_EXIT_GIT_PID_REUSE_REQUIRED')
  const outputs=outputArtifacts(run,{dataRoot}),auditFile=inside(resolve(auditPath),dataRoot),audit=read(auditFile)
  need(audit.schema==='16029.variable-trial.output-audit.v2'&&resolve(audit.source)===run&&audit.nativeResultSha256===hash(join(run,'native-result.json'))
    &&audit.scopeEvidence?.manifestSha256===outputs.manifestSha256&&audit.scopeEvidence.jobSha256===hash(join(run,'job.json'))
    &&audit.outputScope==='parts_dxf_only'&&audit.drawingStatus==='deferred_by_user_20261113'&&audit.caseCount===6
    &&audit.dxfGeometryScopePass===true&&audit.cases?.length===6&&audit.cases.every(c=>c.dxfGeometryPass===true)
    &&audit.fullPlanPass===false&&audit.production_release===false,'RECOVERY_SIX_CASE_GEOMETRY_AUDIT_REQUIRED')
  const declared=read(outputs.manifestPath).cases,auditCases=new Set()
  for(const item of declared) {
    const part=join(run,'cad/outputs',item.folder,item.part),matches=audit.cases.filter(c=>c.case===item.folder&&c.hand===item.hand
      &&c.widthMm===item.widthMm&&c.heightMm===item.heightMm&&typeof c.part==='string'&&resolve(c.part)===part)
    need(matches.length===1&&!auditCases.has(matches[0]),'RECOVERY_AUDIT_CASE_BINDING_REQUIRED')
    auditCases.add(matches[0])
  }
  need(auditCases.size===6,'RECOVERY_AUDIT_CASE_BINDING_REQUIRED')
  const compiled=preflight(job.specification,job.process)
  need(isDeepStrictEqual(compiled.doors,job.doors)&&isDeepStrictEqual(compiled.layouts,job.layouts),'RECOVERY_INPUT_COMPILATION_CHANGED')
  need(job.inputVersions?.length===2&&job.inputVersions.every(v=>inside(v.path,SOURCE)&&hash(v.path)===v.sha256),'RECOVERY_INPUT_VERSION_CHANGED')
  const sources=['job.json','native-result.json','cancel.json','supervision.json','protection-result.json','native-index-protection.json','toolchain.json','copy-manifest.json'].map(name=>join(run,name)).concat([outputs.manifestPath,auditFile])
  return {run,job,outputs,recovery:{schema:'16029-variable-artifact-recovery/v1',recoveredArtifactsFromFailedPhase:true,
    oldSupervisionAccepted:false,oldFailurePreserved:true,reason:'Post-exit git PID reuse; artifacts require new supervised T3 and T4 validation',
    sourceRun:run,sources:sources.map(path=>({path,sha256:hash(path)})),outputs:outputs.artifacts,production_release:false,fullPlanPass:false}}
}
function successfulRun(run,action,dataRoot) {
  run=trialRun(run,dataRoot)
  const native=read(join(run,'native-result.json')),result=read(join(run,'result.json')),guard=read(join(run,'protection-result.json')),supervision=read(join(run,'supervision.json'))
  need(done(native)&&native.action===action&&result.code===0&&result.cadExited===true&&guard.pass===true&&guard.changed?.length===0
    &&supervision.state==='cad_exited'&&supervision.nativeEnded===true&&supervision.cad?.length===0,'RECOVERY_NEW_SUPERVISED_PHASE_REQUIRED')
  return run
}
function fileInventory(cad,folder=cad) {
  return files(inside(folder,cad)).map(path=>({relativePath:relative(cad,path).split('\\').join('/'),size:lstatSync(path).size,sha256:hash(path)}))
}
function exactAssemblyCases(cad) {
  const root=inside(join(cad,'assemblies'),cad),entries=readdirSync(root,{withFileTypes:true})
  need(entries.length===3&&entries.every(entry=>entry.isDirectory()&&['case1','case2','case3'].includes(entry.name)),'RESUME_EXACT_THREE_CASES_REQUIRED')
  return root
}
function caseEvidence(run,index,layout) {
  const cad=join(run,'cad'),folder=join(cad,'assemblies','case'+index),path=join(folder,'assembly-evidence.json'),evidence=read(path)
  need(evidence.schema==='16029-independent-variable-minimum-assembly/v1'&&evidence.success===true&&evidence.nativeReadbackPass===true
    &&evidence.componentIdentityReadbackPass===true&&evidence.documentMetadataReadbackPass===true&&evidence.inputFilesUnchanged===true
    &&evidence.changedInputFiles?.length===0&&evidence.acceptancePass===false&&evidence.production_release===false
    &&!evidence.error&&!evidence.cleanupError&&isDeepStrictEqual(evidence.layout,layout),'RESUME_COMPLETE_UNACCEPTED_CASE_REQUIRED case'+index)
  need(resolve(evidence.outputDirectory)===folder&&resolve(evidence.assemblyPath)===join(folder,'16029_variable_minimum.SLDASM')
    &&hash(inside(evidence.assemblyPath,folder))===evidence.assemblySha256,'RESUME_SOURCE_ASSEMBLY_HASH_MISMATCH case'+index)
  const count=evidence.minimumScope?.expectedComponentCount,manifest=evidence.expectedManifest,instances=evidence.instances
  need(Number.isSafeInteger(count)&&count>0&&manifest?.length===count&&instances?.length===count&&evidence.documentMetadata?.length===count
    &&evidence.reopen?.pass===true&&evidence.reopen.componentCount===count&&evidence.reopen.assemblySha256===evidence.assemblySha256,'RESUME_COMPLETE_CASE_MANIFEST_REQUIRED case'+index)
  const identities=new Set(),components=new Set()
  for(const item of instances) {
    const declared=manifest.filter(v=>v.identity===item.identity)
    need(typeof item.identity==='string'&&!identities.has(item.identity)&&declared.length===1
      &&typeof item.component==='string'&&!components.has(item.component)&&item.fixedState===true&&item.transformPass===true
      &&declared[0].path===item.path&&['side','index','kind'].every(key=>declared[0][key]===item[key])
      &&Array.isArray(item.transform)&&item.transform.length>=13&&Array.isArray(declared[0].transform)&&declared[0].transform.length>=13
      &&item.transform.slice(0,13).every((v,i)=>Number.isFinite(v)&&Number.isFinite(declared[0].transform[i])&&Math.abs(v-declared[0].transform[i])<1e-8),'RESUME_CASE_IDENTITY_OR_TRANSFORM_MISMATCH case'+index)
    identities.add(item.identity);components.add(item.component)
    need(hash(inside(item.path,folder))===item.sha256,'RESUME_CASE_FILE_HASH_MISMATCH case'+index)
  }
  need(identities.size===count&&Array.isArray(evidence.parts)&&evidence.parts.length>0
    &&evidence.parts.every(item=>hash(inside(item.path,folder))===item.sha256),'RESUME_CASE_FILE_HASH_MISMATCH case'+index)
  const interference=evidence.interference,pairs=count*(count-1)/2,seenPairs=new Set()
  need(interference?.complete===true&&interference.errors?.length===0&&interference.componentCount===count&&interference.expectedPairCount===pairs
    &&interference.enumeratedPairCount===pairs&&interference.measuredPairCount===pairs&&interference.unknownPairCount===0
    &&interference.pairs?.length===pairs,'RESUME_COMPLETE_INTERFERENCE_MEASUREMENTS_REQUIRED case'+index)
  for(const pair of interference.pairs) {
    const key=JSON.stringify([pair.componentA,pair.componentB].sort())
    need(pair.componentA!==pair.componentB&&components.has(pair.componentA)&&components.has(pair.componentB)&&!seenPairs.has(key)
      &&pair.measurementComplete===true&&pair.nativeDetectionComplete===true,'RESUME_COMPLETE_INTERFERENCE_MEASUREMENTS_REQUIRED case'+index)
    seenPairs.add(key)
  }
  return {evidence,descriptor:{index,layout,evidenceRelativePath:relative(cad,path).split('\\').join('/'),evidenceSha256:hash(path),assemblySha256:evidence.assemblySha256,inventory:fileInventory(cad,folder)}}
}
export function assemblyResumeSource(source,failedAssemblyRun,{dataRoot=DATA}={}) {
  const run=trialRun(failedAssemblyRun,dataRoot),cad=join(run,'cad'),job=read(join(run,'job.json')),native=read(join(run,'native-result.json'))
  const finished=native.events?.at(-1),generated=native.events?.filter(e=>e.inspection==='minimum_assembly')||[]
  need(run!==source.run&&!existsSync(join(run,'result.json'))&&!existsSync(join(run,'cancel.json'))&&job.action==='assembly'&&native.action==='assembly'
    &&job.production_release===false&&native.production_release===false&&finished?.inspection==='finished'&&finished.success===false&&finished.cadExited===true
    &&typeof finished.error==='string'&&finished.error.includes('MINIMUM_ASSEMBLY_GENERATION_FAILED case3')
    &&generated.length===3&&generated.every((event,index)=>event.index===index+1),'RESUME_FAILED_CASE3_SOURCE_REQUIRED')
  need(job.outputScope==='parts_dxf_only'&&isDeepStrictEqual(job.recovery,source.recovery)
    &&['doors','layouts','specification','process','inputVersions'].every(key=>isDeepStrictEqual(job[key],source.job[key]))
    &&job.layouts?.length===3&&!job.assemblyResume,'RESUME_SOURCE_INPUT_BINDING_CHANGED')
  const guard=read(join(run,'protection-result.json')),supervision=read(join(run,'supervision.json'))
  need(guard.pass===true&&guard.changed?.length===0&&(!guard.nativeTaskIndex||guard.nativeTaskIndex.accepted===true),'RESUME_SOURCE_PROTECTION_REQUIRED')
  need(supervision.state==='cad_exited'&&supervision.nativeEnded===true&&supervision.cad?.length===0,'RESUME_SOURCE_CAD_EXIT_REQUIRED')
  exactAssemblyCases(cad)
  const outputs=outputArtifacts(run,{dataRoot})
  need(outputs.manifestSha256===source.outputs.manifestSha256&&isDeepStrictEqual(outputs.artifacts,source.outputs.artifacts)
    &&files(join(cad,'outputs')).every(path=>!/\.(slddrw|pdf)$/i.test(path)),'RESUME_SOURCE_OUTPUTS_CHANGED')
  const cases=[1,2].map(index=>{
    const {evidence,descriptor}=caseEvidence(run,index,job.layouts[index-1])
    need(isDeepStrictEqual(generated[index-1].result,evidence),'RESUME_NATIVE_CASE_EVIDENCE_MISMATCH case'+index)
    return descriptor
  })
  need(generated[2].result?.success===false&&generated[2].result.acceptancePass===false&&generated[2].result.production_release===false
    &&isDeepStrictEqual(generated[2].result.layout,job.layouts[2])&&typeof generated[2].result.error==='string'
    &&generated[2].result.error.includes('ASSEMBLY_DERIVED_ACTUAL_WIDTH_FAILED'),'RESUME_EXPECTED_CASE3_FAILURE_REQUIRED')
  const sourceEvidence=['job.json','native-result.json','toolchain.json','protected-before.json','protection-result.json','supervision.json','copy-manifest.json','native-index-protection.json'].map(name=>{
    const path=inside(join(run,name),run);return {path,sha256:hash(path)}
  })
  return {run,job,sourceInventory:fileInventory(cad),assemblyResume:{schema:'16029-variable-assembly-resume/v1',sourceRun:run,generateCaseIndices:[3],reuseCaseIndices:[1,2],sourceEvidence,cases,
    mixedProvenance:true,technicalRecoveryOnly:true,recoveredCasesFromFailedPhase:true,oldFailurePreserved:true,oldSupervisionAccepted:false,acceptancePass:false,fullPlanPass:false,production_release:false}}
}
function originalResumeUnchanged(resume) {
  need(resume.assemblyResume.sourceEvidence.every(item=>hash(item.path)===item.sha256),'RESUME_ORIGINAL_EVIDENCE_CHANGED')
  need(!existsSync(join(resume.run,'result.json'))&&!existsSync(join(resume.run,'cancel.json'))
    &&isDeepStrictEqual(fileInventory(join(resume.run,'cad')),resume.sourceInventory),'RESUME_ORIGINAL_SOURCE_FILES_CHANGED')
}
function resumedCases(run,resume) {
  const cad=join(run,'cad'),root=exactAssemblyCases(cad)
  const cases=resume.assemblyResume.cases.map(item=>{
    const actual=fileInventory(cad,join(root,'case'+item.index))
    need(hash(inside(join(cad,item.evidenceRelativePath),cad))===item.evidenceSha256,'RESUME_COPIED_EVIDENCE_CHANGED')
    need(isDeepStrictEqual(actual.map(v=>v.relativePath),item.inventory.map(v=>v.relativePath))
      &&actual.every((v,index)=>/\.(sldprt|sldasm|slddrw)$/i.test(v.relativePath)||isDeepStrictEqual(v,item.inventory[index])),'RESUME_COPIED_CASE_FILES_CHANGED')
    return {...item,inventory:actual}
  })
  const job=read(join(run,'job.json')),native=read(join(run,'native-result.json')),generated=native.events?.filter(e=>e.inspection==='minimum_assembly')||[]
  need(job.action==='assembly'&&job.outputScope==='parts_dxf_only'&&job.production_release===false&&isDeepStrictEqual(job.assemblyResume,resume.assemblyResume)
    &&['doors','layouts','specification','process','inputVersions','recovery'].every(key=>isDeepStrictEqual(job[key],resume.job[key])),'RESUME_JOB_BINDING_CHANGED')
  const third=caseEvidence(run,3,job.layouts[2])
  need(generated.length===1&&generated[0].index===3&&isDeepStrictEqual(generated[0].result,third.evidence),'RESUME_ONLY_CASE3_GENERATION_REQUIRED')
  return [...cases,third.descriptor]
}
function originalOutputsUnchanged(source,dataRoot) {
  need(source.recovery.sources.every(s=>hash(s.path)===s.sha256),'RECOVERY_ORIGINAL_EVIDENCE_CHANGED')
  const original=outputArtifacts(source.run,{dataRoot})
  need(original.manifestSha256===source.outputs.manifestSha256&&isDeepStrictEqual(original.artifacts,source.outputs.artifacts),'RECOVERY_ORIGINAL_OUTPUTS_CHANGED')
}
export async function resumeAssembly(sourceRun,auditPath,failedAssemblyRun,{dataRoot=DATA,runPhase=runProtectedPhase}={}) {
  const source=recoverySource(sourceRun,auditPath,{dataRoot}),resume=assemblyResumeSource(source,failedAssemblyRun,{dataRoot}),job=source.job
  const result=await runPhase({tag:'resume-T3',sourceDirectory:join(resume.run,'cad'),excludeRelativeDirectories:['assemblies/case3'],
    job:{action:'assembly',outputScope:'parts_dxf_only',doors:job.doors,layouts:job.layouts,specification:job.specification,process:job.process,inputVersions:job.inputVersions,
      recovery:source.recovery,assemblyResume:resume.assemblyResume,production_release:false}})
  need(result.pass===true,'RECOVERY_PHASE_FAILED')
  const run=successfulRun(result.dir,'assembly',dataRoot),outputs=outputArtifacts(run,{dataRoot}),cases=resumedCases(run,resume)
  need(outputs.manifestSha256===source.outputs.manifestSha256&&isDeepStrictEqual(outputs.artifacts,source.outputs.artifacts),'RECOVERY_COPIED_OUTPUTS_CHANGED')
  originalResumeUnchanged(resume);originalOutputsUnchanged(source,dataRoot)
  write(join(run,'recovery-source-check.json'),{...source.recovery,newRun:run,outputsUnchanged:true,sourcesUnchanged:true},{root:dataRoot})
  const checkPath=join(run,'assembly-resume-source-check.json')
  write(checkPath,{...resume.assemblyResume,newRun:run,sourceEvidenceUnchanged:true,sourceCadFilesUnchanged:true,sourceInventory:resume.sourceInventory,finalCadInventory:fileInventory(join(run,'cad')),cases,
    jobSha256:hash(join(run,'job.json')),nativeResultSha256:hash(join(run,'native-result.json'))},{root:dataRoot})
  write(join(dataRoot,'recovery-T3-current.json'),{dir:run,cadDirectory:join(run,'cad'),nativeResult:join(run,'native-result.json'),resumeSourceRun:resume.run,
    mixedProvenance:true,resumeSourceCheckSha256:hash(checkPath),production_release:false},{root:dataRoot})
  return result
}
export async function recover(mode,sourceRun,auditPath,{dataRoot=DATA,runPhase=runProtectedPhase,failedAssemblyRun}={}) {
  if(mode==='resume-assembly')return resumeAssembly(sourceRun,auditPath,failedAssemblyRun,{dataRoot,runPhase})
  need(mode==='assembly'||mode==='relocate','USAGE: node recover.mjs <assembly|relocate> <failed-output-run> <geometry-audit.json>')
  const source=recoverySource(sourceRun,auditPath,{dataRoot})
  let input=join(source.run,'cad'),resume=null,resumeCases=null,inputInventory=null
  if(mode==='relocate') {
    const pointer=read(join(dataRoot,'recovery-T3-current.json')),run=successfulRun(pointer.dir,'assembly',dataRoot)
    need(pointer.cadDirectory===join(run,'cad')&&pointer.nativeResult===join(run,'native-result.json'),'RECOVERY_T3_POINTER_INVALID')
    const prior=read(join(run,'job.json'))
    need(isDeepStrictEqual(prior.recovery,source.recovery),'RECOVERY_SOURCE_BINDING_CHANGED')
    const copied=outputArtifacts(run,{dataRoot})
    need(copied.manifestSha256===source.outputs.manifestSha256&&isDeepStrictEqual(copied.artifacts,source.outputs.artifacts),'RECOVERY_T3_OUTPUTS_CHANGED')
    input=join(run,'cad')
    if(prior.assemblyResume) {
      resume=assemblyResumeSource(source,prior.assemblyResume.sourceRun,{dataRoot})
      need(isDeepStrictEqual(prior.assemblyResume,resume.assemblyResume),'RESUME_SOURCE_BINDING_CHANGED')
      const checkPath=join(run,'assembly-resume-source-check.json'),check=read(checkPath)
      resumeCases=resumedCases(run,resume)
      need(pointer.resumeSourceRun===resume.run&&pointer.mixedProvenance===true&&pointer.production_release===false&&pointer.resumeSourceCheckSha256===hash(checkPath)
        &&check.jobSha256===hash(join(run,'job.json'))&&check.nativeResultSha256===hash(join(run,'native-result.json'))
        &&check.sourceEvidenceUnchanged===true&&check.sourceCadFilesUnchanged===true&&isDeepStrictEqual(check.sourceInventory,resume.sourceInventory)
        &&isDeepStrictEqual(check.cases,resumeCases)&&isDeepStrictEqual(check.finalCadInventory,fileInventory(input)),'RESUME_T3_PROVENANCE_CHANGED')
      inputInventory=fileInventory(input)
    }
  }
  const job=source.job
  const result=await runPhase({tag:mode==='assembly'?'recovery-T3':'recovery-T4',sourceDirectory:input,
    job:{action:mode,outputScope:'parts_dxf_only',doors:job.doors,layouts:job.layouts,specification:job.specification,
      process:job.process,inputVersions:job.inputVersions,recovery:source.recovery,...(resume?{assemblyResume:resume.assemblyResume}:{}),production_release:false}})
  need(result.pass===true,'RECOVERY_PHASE_FAILED')
  const run=successfulRun(result.dir,mode,dataRoot),outputs=outputArtifacts(run,{dataRoot})
  need(outputs.manifestSha256===source.outputs.manifestSha256&&isDeepStrictEqual(outputs.artifacts,source.outputs.artifacts),'RECOVERY_COPIED_OUTPUTS_CHANGED')
  originalOutputsUnchanged(source,dataRoot)
  if(resume) {
    originalResumeUnchanged(resume)
    need(isDeepStrictEqual(fileInventory(input),inputInventory),'RESUME_T3_SOURCE_FILES_CHANGED')
    for(const item of resumeCases)need(hash(inside(join(run,'cad',item.evidenceRelativePath),join(run,'cad')))===item.evidenceSha256,'RESUME_T4_SOURCE_CASE_EVIDENCE_CHANGED')
  }
  write(join(run,'recovery-source-check.json'),{...source.recovery,newRun:run,outputsUnchanged:true,sourcesUnchanged:true,...(resume?{assemblyResume:resume.assemblyResume}: {})},{root:dataRoot})
  write(join(dataRoot,mode==='assembly'?'recovery-T3-current.json':'recovery-T4-current.json'),{dir:run,cadDirectory:join(run,'cad'),nativeResult:join(run,'native-result.json'),
    ...(resume?{resumeSourceRun:resume.run,mixedProvenance:true,production_release:false}:{})},{root:dataRoot})
  return result
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    const [mode,sourceRun,auditPath,failedAssemblyRun]=process.argv.slice(2)
    console.log(JSON.stringify(await recover(mode,sourceRun,auditPath,{failedAssemblyRun})))
  }catch(error){console.error(error);process.exitCode=1}
}
