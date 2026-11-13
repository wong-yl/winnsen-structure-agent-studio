import { resolve, join, dirname, basename } from 'node:path'
import { existsSync, statSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { isDeepStrictEqual } from 'node:util'
import { SOURCE,DATA,read,hash,inside,files } from './common.mjs'
import { compileDoor,compileLayout } from './layout.mjs'
import { runProtectedPhase,trialSource } from './run.mjs'

const need=(v,m)=>{if(!v)throw new Error(m)}
export function preflight(specification,processInput) {
  need(specification&&Array.isArray(specification.doorCases)&&specification.doorCases.length>0,'DOOR_CASES_REQUIRED')
  need(Array.isArray(specification.assemblyCases)&&specification.assemblyCases.length>0,'ASSEMBLY_CASES_REQUIRED')
  const doors=specification.doorCases.map(input=>{
    const door=compileDoor(input,processInput)
    need(input.widthMm>=287&&input.widthMm<=537&&input.heightMm>=200&&input.heightMm<=1000,'DOOR_OUTSIDE_PILOT_ENVELOPE')
    if(input.identity!==undefined)need(input.identity===door.identity,'IDENTITY_CONFLICT')
    if(input.properties?.hand!==undefined)need(input.properties.hand===input.hand,'HAND_IDENTITY_CONFLICT')
    return door
  })
  need(new Set(doors.map(d=>d.identity)).size===doors.length,'DUPLICATE_DOOR_IDENTITY')
  const layouts=specification.assemblyCases.map(spec=>{
    const layout=compileLayout(spec)
    need(layout.cabinet.heightMm===2000&&layout.cabinet.depthMm===500&&[900,1000].includes(layout.cabinet.widthMm),'ASSEMBLY_OUTSIDE_PILOT_PROFILE')
    return layout
  })
  return {doors,layouts}
}
function sourceOutputScope(sourceDirectory) {
  const manifestPath=join(sourceDirectory,'outputs/output-manifest.json')
  if(!existsSync(manifestPath))return undefined
  const manifest=read(manifestPath)
  need(manifest.complete===true&&Array.isArray(manifest.cases)&&manifest.cases.length===6,'COMPLETE_OUTPUT_MANIFEST_REQUIRED')
  if(manifest.schema==='16029-variable-output-manifest/v1') {
    need(manifest.outputScope===undefined||manifest.outputScope==='parts_dxf_pdf','OUTPUT_SCOPE_MISMATCH')
    need(manifest.drawingStatus===undefined&&manifest.cases.every(item=>item.drawing&&typeof item.drawing==='object'),'FULL_OUTPUT_MANIFEST_REQUIRED')
    return 'parts_dxf_pdf'
  }
  need(manifest.schema==='16029-variable-output-manifest/v2'&&manifest.outputScope==='parts_dxf_only'
    &&manifest.drawingStatus==='deferred_by_user_20261113'&&manifest.production_release===false
    &&manifest.cases.every(item=>item.drawing===null),'DXF_ONLY_OUTPUT_MANIFEST_REQUIRED')
  return 'parts_dxf_only'
}
function phaseSource(pointerFile,dataRoot) {
  const pointer=read(join(dataRoot,pointerFile)),sourceDirectory=trialSource(pointer,{dataRoot})
  need(statSync(sourceDirectory).isDirectory(),'SOURCE_CAD_DIRECTORY_REQUIRED')
  const native=read(pointer.nativeResult),finished=native.events?.at(-1)
  need(native.production_release===false&&finished?.inspection==='finished'&&finished.success===true&&finished.cadExited===true,'SOURCE_NATIVE_COMPLETION_REQUIRED')
  return {sourceDirectory,outputScope:sourceOutputScope(sourceDirectory),action:native.action}
}
function selectSource(mode,dataRoot) {
  if(mode!=='assembly')return phaseSource(mode==='relocate'?'assembly-current.json':'T1-current.json',dataRoot)
  for(const [pointerFile,action,scope] of [['outputs-current.json','outputs','parts_dxf_pdf'],['outputs-dxf-current.json','outputs-dxf','parts_dxf_only']]) {
    if(!existsSync(join(dataRoot,pointerFile)))continue
    try {
      const source=phaseSource(pointerFile,dataRoot)
      need(source.action===action&&source.outputScope===scope,'OUTPUT_POINTER_SCOPE_MISMATCH')
      return source
    } catch {}
  }
  return phaseSource('T1-current.json',dataRoot)
}
function doorReopenSource(dataRoot,{compiled,specification,processInput,inputVersions}) {
  const pointerPath=inside(join(dataRoot,'outputs-dxf-current.json'),dataRoot),pointer=read(pointerPath)
  const sourceDirectory=trialSource(pointer,{dataRoot}),run=dirname(sourceDirectory),evidence=[]
  need(statSync(sourceDirectory).isDirectory(),'SOURCE_CAD_DIRECTORY_REQUIRED')
  const record=path=>{
    path=inside(path,dataRoot)
    need(statSync(path).isFile(),'DOORS_REOPEN_SOURCE_FILE_REQUIRED')
    const sha256=hash(path),value=read(path)
    need(hash(path)===sha256,'DOORS_REOPEN_SOURCE_CHANGED')
    evidence.push({path,sha256})
    return value
  }
  need(isDeepStrictEqual(record(pointerPath),pointer),'DOORS_REOPEN_SOURCE_CHANGED')
  const job=record(join(run,'job.json')),native=record(pointer.nativeResult),result=record(join(run,'result.json'))
  const supervision=record(join(run,'supervision.json')),protection=record(join(run,'protection-result.json'))
  need(!existsSync(inside(join(run,'cancel.json'),run)),'DOORS_REOPEN_CANCELLED_SOURCE_FORBIDDEN')
  const finished=native.events?.at(-1),claims=Array.isArray(native.events)?native.events.filter(item=>item.inspection==='cad_owned'):[]
  need(native.action==='outputs-dxf'&&native.production_release===false&&finished?.inspection==='finished'
    &&finished.success===true&&finished.cadExited===true&&!finished.error&&claims.length===1
    &&Number.isSafeInteger(native.cadPid)&&native.cadPid>0&&claims[0].pid===native.cadPid,'DOORS_REOPEN_SOURCE_NATIVE_COMPLETION_REQUIRED')
  need(result.code===0&&result.cadExited===true,'DOORS_REOPEN_SOURCE_RUNNER_SUCCESS_REQUIRED')
  need(supervision.state==='cad_exited'&&supervision.nativeEnded===true&&Array.isArray(supervision.cad)
    &&supervision.cad.length===0,'DOORS_REOPEN_SOURCE_SUPERVISION_REQUIRED')
  need(protection.pass===true&&Array.isArray(protection.changed)&&protection.changed.length===0
    &&(!protection.nativeTaskIndex||protection.nativeTaskIndex.accepted===true),'DOORS_REOPEN_SOURCE_PROTECTION_REQUIRED')
  need(job.action==='outputs-dxf'&&job.outputScope==='parts_dxf_only'&&job.production_release===false
    &&job.cadDirectory===sourceDirectory&&job.resultPath===pointer.nativeResult,'DOORS_REOPEN_SOURCE_JOB_REQUIRED')
  need(compiled.doors.length===6&&isDeepStrictEqual(job.doors,compiled.doors)&&isDeepStrictEqual(job.layouts,compiled.layouts)
    &&isDeepStrictEqual(job.specification,specification)&&isDeepStrictEqual(job.process,processInput),'DOORS_REOPEN_SOURCE_INPUT_BINDING_CHANGED')
  need(isDeepStrictEqual(job.inputVersions,inputVersions),'DOORS_REOPEN_SOURCE_INPUT_VERSION_CHANGED')
  const outputRoot=inside(join(sourceDirectory,'outputs'),sourceDirectory),manifestPath=join(outputRoot,'output-manifest.json'),manifest=record(manifestPath)
  need(manifest.schema==='16029-variable-output-manifest/v2'&&manifest.complete===true&&manifest.outputScope==='parts_dxf_only'
    &&manifest.drawingStatus==='deferred_by_user_20261113'&&manifest.production_release===false
    &&Array.isArray(manifest.cases)&&manifest.cases.length===6,'DOORS_REOPEN_COMPLETE_DXF_MANIFEST_REQUIRED')
  const remaining=new Map(compiled.doors.map(door=>[JSON.stringify([door.hand,door.variables.DOOR_W,door.variables.DOOR_H]),door]))
  const artifactPaths=new Set(),artifacts=[]
  for(const item of manifest.cases) {
    const identity=JSON.stringify([item.hand,item.widthMm,item.heightMm])
    need(remaining.delete(identity)&&item.drawing===null&&item.flatPreparation?.success===true&&item.dxf?.success===true,'DOORS_REOPEN_EXACT_SIX_CASES_REQUIRED')
    need(item.processReadback?.schema==='16029-door-process-readback/v1'&&item.processReadback.rawReadbackComplete===true
      &&item.processReadback.fixedExplicitOverridesMatch===true&&item.processReadback.candidateFixedProcessMatches===true
      &&item.processReadback.pilotTopologyMatches===true
      &&item.processReadback.effectiveInheritanceVerified===false&&item.processReadback.manufacturingOrientationVerified===false
      &&item.processReadback.manufacturingAccepted===false,'DOORS_REOPEN_FIXED_PROCESS_READBACK_REQUIRED')
    need(typeof item.folder==='string'&&/^[LR][0-9]+$/.test(item.folder)&&typeof item.part==='string'
      &&basename(item.part)===item.part&&/\.sldprt$/i.test(item.part),'DOORS_REOPEN_SOURCE_PART_PATH_INVALID')
    const folder=inside(join(outputRoot,item.folder),outputRoot),part=inside(join(folder,item.part),folder)
    need(!artifactPaths.has(part),'DOORS_REOPEN_DUPLICATE_ARTIFACT_PATH')
    need(/^[A-F0-9]{64}$/.test(item.partSha256||'')&&hash(part)===item.partSha256,'DOORS_REOPEN_SOURCE_ARTIFACT_HASH_MISMATCH')
    artifactPaths.add(part)
    for(const [filename,pathKey,hashKey] of [['flat.dxf','path','sha256'],['flat.native-cut.dxf','nativeCutPath','nativeCutSha256'],['flat.native-bends.dxf','nativeBendsPath','nativeBendsSha256']]) {
      const path=inside(join(folder,filename),folder)
      need(!artifactPaths.has(path),'DOORS_REOPEN_DUPLICATE_ARTIFACT_PATH')
      need(typeof item.dxf[pathKey]==='string'&&resolve(item.dxf[pathKey])===path,'DOORS_REOPEN_SOURCE_DXF_PATH_CHANGED '+pathKey)
      need(/^[A-F0-9]{64}$/.test(item.dxf[hashKey]||'')&&hash(path)===item.dxf[hashKey],'DOORS_REOPEN_SOURCE_ARTIFACT_HASH_MISMATCH '+hashKey)
      artifactPaths.add(path)
    }
    artifacts.push({folder:item.folder,part:item.part,hand:item.hand,widthMm:item.widthMm,heightMm:item.heightMm,partSha256:item.partSha256,
      dxfSha256:item.dxf.sha256,nativeCutSha256:item.dxf.nativeCutSha256,nativeBendsSha256:item.dxf.nativeBendsSha256})
  }
  const outputFiles=files(outputRoot),nativeOutputs=outputFiles.filter(path=>/\.(sldprt|dxf)$/i.test(path))
  need(remaining.size===0&&nativeOutputs.every(path=>artifactPaths.has(path))&&nativeOutputs.length===24,'DOORS_REOPEN_OUTPUT_INVENTORY_CHANGED')
  need(outputFiles.every(path=>!/\.(sldasm|slddrw|pdf)$/i.test(path)),'DOORS_REOPEN_OUTPUT_DRAWINGS_OR_ASSEMBLIES_FORBIDDEN')
  need(evidence.every(item=>hash(item.path)===item.sha256),'DOORS_REOPEN_SOURCE_CHANGED')
  return {sourceDirectory,outputScope:'parts_dxf_only',doorSource:{schema:'16029-variable-door-reopen-source/v1',run,evidence,artifacts,
    fullPlanPass:false,production_release:false}}
}
export async function execute(mode,specPath=join(SOURCE,'inputs/specification.json'),processPath=join(SOURCE,'inputs/process.json'),{dataRoot=DATA,runPhase=runProtectedPhase}={}) {
  need(['outputs','outputs-dxf','doors-reopen','assembly','relocate'].includes(mode),'USAGE: node execute.mjs <outputs|outputs-dxf|doors-reopen|assembly|relocate> [specification.json] [process.json]')
  const specification=read(resolve(specPath)),processInput=read(resolve(processPath))
  const compiled=preflight(specification,processInput)
  const inputVersions=[{path:resolve(specPath),sha256:hash(resolve(specPath))},{path:resolve(processPath),sha256:hash(resolve(processPath))}]
  const source=mode==='doors-reopen'?doorReopenSource(dataRoot,{compiled,specification,processInput,inputVersions}):selectSource(mode,dataRoot)
  const outputScope=mode==='outputs'?'parts_dxf_pdf':mode==='outputs-dxf'?'parts_dxf_only':source.outputScope
  const tag=mode==='outputs'?'T2-outputs':mode==='outputs-dxf'?'T2-outputs-dxf':mode==='doors-reopen'?'T2-doors-reopen':mode==='assembly'?'T3-assembly':'T4-relocate'
  return await runPhase({tag,sourceDirectory:source.sourceDirectory,job:{action:mode,...(outputScope?{outputScope}:{}),...compiled,specification,process:processInput,inputVersions,
    ...(source.doorSource?{doorSource:source.doorSource,drawingStatus:'deferred_by_user_20261113',fullPlanPass:false}:{}),production_release:false},currentFile:mode==='doors-reopen'?null:mode+'-current.json'})
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const result=await execute(...process.argv.slice(2));console.log(JSON.stringify(result))
}
