import { read, write, hash } from './common.mjs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const need=(value,message)=>{if(!value)throw new Error(message)}
const near=(a,b,t=1e-7)=>Math.abs(a-b)<=t
function sameGeometry(a,b) {
  need(a.bodies.length===1&&b.bodies.length===1,'ONE_BODY_REQUIRED')
  const x=a.bodies[0],y=b.bodies[0]
  need(x.sheetMetal&&y.sheetMetal,'SHEET_METAL_REQUIRED')
  need(x.boxMm.every((n,i)=>near(n,y.boxMm[i],.01)),'RESTORED_BOUNDS_DIFFER')
  need(x.massProperties.every((n,i)=>near(n,y.massProperties[i],1e-10)),'RESTORED_MASS_DIFFER')
  need(x.circleEdges.length===y.circleEdges.length,'RESTORED_EDGES_DIFFER')
  need(x.circleEdges.every((c,i)=>c.every((n,k)=>near(n,y.circleEdges[i][k]))),'RESTORED_CIRCLES_DIFFER')
}
function checkLandmarks(body,hand,width,height) {
  const s=hand==='L'?1:-1
  const holes=body.circleEdges.filter(c=>c[6]>.0015).map(c=>c.map((v,i)=>i<3||i===6?v*1000:v))
  need(holes.length===6,'HOLE_EDGE_COUNT_CHANGED')
  const hinge=holes.filter(c=>near(c[6],5.1,.01)),lock=holes.filter(c=>near(c[6],2,.01))
  need(hinge.length===4&&lock.length===2,'HOLE_RADIUS_CHANGED')
  need(hinge.every(c=>near(c[0],-(width/2-10)*s,.01)&&near(c[2],-7,.01)),'HINGE_XZ_NOT_FOLLOWING')
  const expected=[-height/2,-height/2+.8,height/2-.8,height/2].sort((a,b)=>a-b)
  need(hinge.map(c=>c[1]).sort((a,b)=>a-b).every((v,i)=>near(v,expected[i],.01)),'HINGE_Y_NOT_FOLLOWING')
  need(lock.every(c=>near(c[0],(width/2-6)*s,.01)&&near(c[1],0,.01)),'LOCK_HOLE_NOT_FOLLOWING')
  return {hingeRadiusMm:5.1,hingeXmm:-(width/2-10)*s,hingeEdgeYmm:expected,hingeZmm:-7,lockRadiusMm:2,lockXmm:(width/2-6)*s,lockYmm:0,observedHoleEdges:holes}
}
export function verifyMatrix(path) {
  const data=read(path),events=data.events
  need(events.at(-1)?.inspection==='finished'&&events.at(-1).success&&events.at(-1).cadExited,'NATIVE_NOT_FINISHED')
  const checks=[]
  for(const hand of ['L','R']) {
    const needle=hand==='L'?'_left_':'_right_'
    const before=events.find(e=>e.inspection==='panel_before_binding'&&e.path.includes(needle))
    const after=events.find(e=>e.inspection==='panel_after_binding'&&e.path.includes(needle))
    need(before&&after,'BINDING_EVIDENCE_MISSING')
    sameGeometry(before.geometry,after.geometry)
    const sizes=[[437,471.25],[287,200],[437,471.25],[537,1000],[437,471.25]]
    const rows=events.filter(e=>e.inspection==='size_geometry'&&e.path.includes(needle))
    need(rows.length===sizes.length,'MATRIX_EVIDENCE_MISSING')
    for(const [index,[width,height]] of sizes.entries()) {
      const row=rows.find(e=>e.index===index);need(row,'MATRIX_INDEX_MISSING')
      const b=row.geometry.bodies[0]
      need(b.circleEdges.length===before.geometry.bodies[0].circleEdges.length,'CIRCLE_TOPOLOGY_CHANGED')
      const landmarks=checkLandmarks(b,hand,width,height)
      if(index===0||index===2||index===4)sameGeometry(before.geometry,row.geometry)
      checks.push({hand,index,widthMm:width,heightMm:height,landmarks,restoration:index===2||index===4,pass:true})
    }
  }
  const health=events.filter(e=>e.inspection==='panel_health')
  need(health.length===12,'FEATURE_HEALTH_EVIDENCE_MISSING')
  for(const record of health)need(record.features.every(f=>f.suppressed||f.error===0),'UNSUPPRESSED_FEATURE_ERROR')
  const sheets=events.filter(e=>e.inspection==='sheet_parameters')
  need(sheets.length===12&&sheets.every(s=>near(s.thicknessMm,.8,.01)&&near(s.radiusMm,.2,.01)&&s.allowanceType===4&&near(s.bendDeductionMm,1.4,.01)),'PROCESS_DRIFT')
  return {schema:'16029.variable-trial.matrix.v1',source:path,sourceSha256:hash(path),nativeProcess:{pid:data.cadPid,startTicks:data.cadTicks,executable:data.cadExe},pass:true,checks,scope:'internal variable rebuild/save and folded hole landmarks; independent reopen, flat contours and drawing acceptance are separate',processBinding:{DOOR_W:'D2@草图1',DOOR_H:'D1@草图1',T:['D1@抽壳1','D1@凸台-拉伸2'],BEND_R:'D1@钣金1',BEND_DED:'fixed native deduction, verified at 1.4 mm; not equation-bound'},manufacturingValidation:'incomplete',dfmThresholds:'undefined',production_release:false}
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const input=process.argv[2],output=process.argv[3]
  need(input&&output,'USAGE: node verify.mjs <native-result.json> <new-report.json>')
  const report=verifyMatrix(resolve(input));write(resolve(output),report);console.log(JSON.stringify({pass:report.pass,cases:report.checks.length,output:resolve(output)}))
}
