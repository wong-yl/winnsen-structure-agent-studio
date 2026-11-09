import { acquireNativeWorkerInstanceLock } from 'file:///D:/Winnsen_Structure_Agent_Studio/tools/run_16029_native_worker.mjs';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn, execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
const stage=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const dataDir='D:/Winnsen_Structure_Agent_Studio/data';
const [contractName,mode,tag]=process.argv.slice(2);
if(!/^[a-z0-9_]+\.json$/.test(contractName)||!['relocate','sizing_survey','layout_survey','layout_probe','generate','verify'].includes(mode)||!/^[a-z0-9_]+$/.test(tag))throw Error('Bounded arguments required');
const read=p=>JSON.parse(readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
const contractPath=resolve(stage,'evidence',contractName),evidenceName=`evidence/${tag}_${mode}.json`,evidencePath=resolve(stage,evidenceName);
if(existsSync(evidencePath))throw Error('Preserve evidence');
const seed=read(resolve(stage,'seed_manifest.json'));
function checkSeed(){for(const[n,h]of Object.entries(seed.files))if(hash(resolve(seed.root,n))!==h)throw Error('Accepted source changed '+n);}
function runtime(){
 const raw=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',"@(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'SLDWORKS.exe'} | Select-Object ProcessId,ExecutablePath,CommandLine) | ConvertTo-Json -Compress"],{windowsHide:true,encoding:'utf8'}).trim();
 const processes=raw?JSON.parse(raw):[];
 if((Array.isArray(processes)?processes.length:1)>0)throw Error('CAD busy');
 for(const n of readdirSync(resolve(dataDir,'native_model_requests')).filter(n=>/^(NATIVE|PARAMETRIC)-.*\.json$/.test(n))){const s=read(resolve(dataDir,'native_model_requests',n)).nativeBuild?.state;if(['created','queued','claimed','planning','building','running','validating'].includes(s))throw Error('Indoor CAD queue occupied');}
 return {checked_utc:new Date().toISOString(),processes};
}
checkSeed();runtime();
const response=await fetch('http://127.0.0.1:5180/status.json',{signal:AbortSignal.timeout(10000)});const service=await response.json();
if(response.status!==200||service.status!=='ok')throw Error('Shared service unhealthy');
const lock=acquireNativeWorkerInstanceLock({dataDir,workerId:`outdoor-shade-${process.pid}`});const timer=setInterval(()=>lock.heartbeat(),10000);
try{
 const before=runtime();writeFileSync(evidencePath+'.launch.json',JSON.stringify({mode,contract_sha256:hash(contractPath),tool_sha256:hash(resolve(stage,'OutdoorLayoutOfflineR247.exe')),before,service:{http_status:response.status,status:service.status}},null,2),{flag:'wx'});
 const child=spawn(resolve(stage,'OutdoorLayoutOfflineR247.exe'),[contractPath,mode,evidencePath],{stdio:'inherit',windowsHide:true});
 const code=await new Promise((yes,no)=>{child.on('error',no);child.on('exit',c=>yes(c??1));});
 const e=read(evidencePath);
 if(e.exit_app_called)execFileSync('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',resolve(stage,'scripts/cleanup_owned_residual_poll_r4.ps1'),'-EvidenceName',evidenceName],{stdio:'inherit',windowsHide:true});
 checkSeed();const after=runtime();writeFileSync(evidencePath+'.after.json',JSON.stringify(after,null,2),{flag:'wx'});
 if(code!==0||e.status!==mode+'_finished')throw Error('Native operation failed: '+e.error);
 console.log(JSON.stringify({status:e.status,evidence:evidencePath}));
}finally{clearInterval(timer);lock.release();}
