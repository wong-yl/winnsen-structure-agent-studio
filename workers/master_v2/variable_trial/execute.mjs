import { resolve, join, dirname, basename } from 'node:path'
import { existsSync, statSync, mkdirSync, copyFileSync, renameSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { isDeepStrictEqual } from 'node:util'
import { SOURCE,DATA,read,hash,inside,files } from './common.mjs'
import { compileDoor,compileLayout } from './layout.mjs'
import { runProtectedPhase,runNativeJob,trialSource } from './run.mjs'

const need=(v,m)=>{if(!v)throw new Error(m)}
const useWidthEnvelope=(specification,options)=>options?.parametricWidth===true||specification?.envelopeMode==='parametric_width'
const useFullEnvelope=(specification,options)=>options?.parametricEnvelope===true||specification?.envelopeMode==='parametric_envelope'
export function preflight(specification,processInput,options={}) {
  need(specification&&Array.isArray(specification.doorCases)&&specification.doorCases.length>0,'DOOR_CASES_REQUIRED')
  need(Array.isArray(specification.assemblyCases)&&specification.assemblyCases.length>0,'ASSEMBLY_CASES_REQUIRED')
  const parametricWidth=useWidthEnvelope(specification,options)
  const parametricEnvelope=useFullEnvelope(specification,options)
  need(!(parametricWidth&&parametricEnvelope),'CONFLICTING_ENVELOPE_MODES')
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
    const validEnvelope=parametricEnvelope
      ?layout.cabinet.widthMm>=700&&layout.cabinet.widthMm<=1200&&layout.cabinet.heightMm>=1700&&layout.cabinet.heightMm<=2200&&layout.cabinet.depthMm>=250&&layout.cabinet.depthMm<=650
      :layout.cabinet.heightMm===2000&&layout.cabinet.depthMm===500&&(parametricWidth?layout.cabinet.widthMm>=700&&layout.cabinet.widthMm<=1200:[900,1000].includes(layout.cabinet.widthMm))
    need(validEnvelope,parametricEnvelope?'ASSEMBLY_OUTSIDE_PARAMETRIC_ENVELOPE':parametricWidth?'ASSEMBLY_OUTSIDE_WIDTH_ENVELOPE':'ASSEMBLY_OUTSIDE_PILOT_PROFILE')
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
function parsePositiveIntOption(text, errorCode) {
  need(/^[1-9]\d*$/.test(String(text || '')), errorCode)
  const parsed = Number.parseInt(text, 10)
  need(Number.isSafeInteger(parsed) && parsed >= 1, errorCode)
  return parsed
}
export function parseCliArguments(argv) {
  let parametricWidth=false, parametricEnvelope=false, caseIndex
  const positional=[]
  for(let i=0;i<argv.length;i++) {
    const arg=argv[i]
    if(arg==='--parametric-width') { parametricWidth=true; continue }
    if(arg==='--parametric-envelope'||arg==='--full-cabinet') { parametricEnvelope=true; continue }
    if(arg==='--case'||arg.startsWith('--case=')) {
      need(caseIndex===undefined,'DUPLICATE_CASE_OPTION')
      const raw=arg==='--case'?argv[++i]:arg.slice('--case='.length)
      caseIndex=parsePositiveIntOption(raw,'INVALID_CASE_OPTION')
      continue
    }
    need(!arg.startsWith('--'),'UNSUPPORTED_OPTION '+arg)
    positional.push(arg)
  }
  need(!(parametricWidth&&parametricEnvelope),'CONFLICTING_ENVELOPE_MODES')
  need(positional.length<=3,'USAGE: node execute.mjs <outputs|outputs-dxf|doors-reopen|assembly|relocate> [specification.json] [process.json] [--parametric-width|--parametric-envelope|--full-cabinet] [--case <index>]')
  if(parametricEnvelope)need(positional[0]==='assembly','PARAMETRIC_ENVELOPE_REQUIRES_ASSEMBLY_MODE')
  if(caseIndex!==undefined)need(positional[0]==='assembly','CASE_OPTION_REQUIRES_ASSEMBLY_MODE')
  return {mode:positional[0],specPath:positional[1],processPath:positional[2],parametricWidth,...(parametricEnvelope?{parametricEnvelope:true}:{}),...(caseIndex!==undefined?{caseIndex}:{})}
}
const ENVELOPE_PHASE_ARTIFACTS = [
  'job.json',
  'toolchain.json',
  'native-result.json',
  'native.stdout.log',
  'native.stderr.log',
  'supervision.json',
  'cancel.json',
]

function archiveEnvelopePhaseArtifacts(dir, phaseDir, { removeSource } = {}) {
  mkdirSync(phaseDir, { recursive: true })
  for (const name of ENVELOPE_PHASE_ARTIFACTS) {
    const src = join(dir, name)
    if (!existsSync(src)) continue
    const dst = join(phaseDir, name)
    if (removeSource) renameSync(src, dst)
    else copyFileSync(src, dst)
  }
}

export async function runParametricEnvelopePhases(job, dir, opts = {}) {
  const runNative = opts.runNative || runNativeJob
  const caseIndices = job.onlyCaseIndex !== undefined
    ? [job.onlyCaseIndex]
    : job.layouts.map((_, index) => index + 1)
  const phaseResults = []
  let lastResult = null
  const phasePlan = [
    ['prepare', 'prepare'],
    ['build_and_measure', 'build'],
  ]
  for (let i = 0; i < caseIndices.length; i++) {
    const k = caseIndices[i]
    for (const [phase, folderSuffix] of phasePlan) {
      const isLast = i === caseIndices.length - 1 && phase === 'build_and_measure'
      const phaseDir = join(dir, 'phases', `case${k}-${folderSuffix}`)
      let succeeded = false
      try {
        lastResult = await runNative({ ...job, onlyCaseIndex: k, envelopePhase: phase }, dir, opts)
        succeeded = true
        phaseResults.push({ caseIndex: k, phase, phaseDir, ...lastResult })
      } finally {
        archiveEnvelopePhaseArtifacts(dir, phaseDir, { removeSource: succeeded && !isLast })
      }
    }
  }
  return { ...lastResult, phases: phaseResults }
}

export async function execute(mode,specPath=join(SOURCE,'inputs/specification.json'),processPath=join(SOURCE,'inputs/process.json'),{dataRoot=DATA,runPhase=runProtectedPhase,parametricWidth=false,parametricEnvelope=false,caseIndex,signal=null}={}) {
  need(['outputs','outputs-dxf','doors-reopen','assembly','relocate'].includes(mode),'USAGE: node execute.mjs <outputs|outputs-dxf|doors-reopen|assembly|relocate> [specification.json] [process.json] [--parametric-width|--parametric-envelope|--full-cabinet] [--case <index>]')
  const specification=read(resolve(specPath)),processInput=read(resolve(processPath))
  const wantWidth=useWidthEnvelope(specification,{parametricWidth})
  const wantEnvelope=useFullEnvelope(specification,{parametricEnvelope})
  need(!(wantWidth&&wantEnvelope),'CONFLICTING_ENVELOPE_MODES')
  const envelopeMode=wantEnvelope?'parametric_envelope':wantWidth?'parametric_width':'pilot'
  if(envelopeMode==='parametric_envelope')need(mode==='assembly','PARAMETRIC_ENVELOPE_REQUIRES_ASSEMBLY_MODE')
  const compiled=preflight(specification,processInput,{parametricWidth:envelopeMode==='parametric_width',parametricEnvelope:envelopeMode==='parametric_envelope'})
  if(caseIndex!==undefined) {
    need(mode==='assembly','CASE_OPTION_REQUIRES_ASSEMBLY_MODE')
    need(Number.isInteger(caseIndex)&&caseIndex>=1&&caseIndex<=compiled.layouts.length,'INVALID_CASE_INDEX')
  }
  const inputVersions=[{path:resolve(specPath),sha256:hash(resolve(specPath))},{path:resolve(processPath),sha256:hash(resolve(processPath))}]
  const source=mode==='doors-reopen'?doorReopenSource(dataRoot,{compiled,specification,processInput,inputVersions}):selectSource(mode,dataRoot)
  const outputScope=mode==='outputs'?'parts_dxf_pdf':mode==='outputs-dxf'?'parts_dxf_only':source.outputScope
  const tag=envelopeMode==='parametric_envelope'
    ?(caseIndex!==undefined?'T3-envelope-case'+caseIndex:'T3-envelope')
    :(mode==='outputs'?'T2-outputs':mode==='outputs-dxf'?'T2-outputs-dxf':mode==='doors-reopen'?'T2-doors-reopen':mode==='assembly'?(caseIndex!==undefined?'T3-assembly-case'+caseIndex:'T3-assembly'):'T4-relocate')
  const currentFile=envelopeMode==='parametric_envelope'||mode==='doors-reopen'||caseIndex!==undefined?null:mode+'-current.json'
  const phaseArgs={tag,sourceDirectory:source.sourceDirectory,job:{action:mode,...(outputScope?{outputScope}:{}),...(envelopeMode!=='pilot'?{envelopeMode}:{}),...(caseIndex!==undefined?{onlyCaseIndex:caseIndex}:{}),...compiled,specification,process:processInput,inputVersions,
    ...(source.doorSource?{doorSource:source.doorSource,drawingStatus:'deferred_by_user_20261113',fullPlanPass:false}:{}),production_release:false},currentFile}
  if(envelopeMode==='parametric_envelope') return await runPhase(phaseArgs,{executeNative:runParametricEnvelopePhases,...(signal?{signal}:{})})
  return signal?await runPhase(phaseArgs,{signal}):await runPhase(phaseArgs)
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const cli=parseCliArguments(process.argv.slice(2))
  const result=await execute(cli.mode,cli.specPath,cli.processPath,{parametricWidth:cli.parametricWidth,parametricEnvelope:Boolean(cli.parametricEnvelope),caseIndex:cli.caseIndex});console.log(JSON.stringify(result))
}
