"""Throwaway exact old-door migration observer; no CAD force termination."""
import sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
import json, hashlib, importlib.util, subprocess, time, datetime
from pathlib import Path

def load(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def put(p,v):
    with Path(p).open('x',encoding='utf-8') as f: json.dump(v,f,ensure_ascii=False,indent=2); f.write('\n')
config_path=Path(sys.argv[1])
if sha(config_path)!=sys.argv[2]: raise RuntimeError('Runner config hash differs')
c=load(config_path); out=Path(c['out'])
def intact():
    results=[]
    for row in c['immutable']:
        p=Path(row['path']); s=p.lstat()
        if s.st_file_attributes & 0x400 or s.st_nlink!=1: raise RuntimeError('Input link refused: '+str(p))
        actual=sha(p); results.append({**row,'actualSha256':actual,'unchanged':actual==row['sha256']})
    return results
def lock():
    o=load(c['lockOwner'])
    if (o['token'],o['workerId'],o['pid'])!=(c['lockToken'],c['lockWorker'],c['lockPid']): raise RuntimeError('Native lock ownership differs')
    age=(datetime.datetime.now(datetime.timezone.utc)-datetime.datetime.fromisoformat(o['heartbeatAt'].replace('Z','+00:00'))).total_seconds()
    if age < -5 or age > 30: raise RuntimeError('Native lock heartbeat stale')
spec=importlib.util.spec_from_file_location('frozen_identity_process_observer',c['observer']['path'])
obs=importlib.util.module_from_spec(spec);spec.loader.exec_module(obs)
def snapshot():
    return obs.ps("""$rows=@(Get-CimInstance Win32_Process -Filter "Name='SLDWORKS.exe' OR Name='sldProcMon.exe' OR Name='sldworks_fs.exe'" | ForEach-Object { $parent=Get-CimInstance Win32_Process -Filter ('ProcessId='+$_.ParentProcessId); $live=Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; [pscustomobject]@{name=$_.Name;pid=$_.ProcessId;ppid=$_.ParentProcessId;created=$_.CreationDate.ToString('o');commandLine=$_.CommandLine;executablePath=$_.ExecutablePath;parentExists=($null -ne $parent);parentName=$parent.Name;startTimeUtcTicks=if($null -eq $live){$null}else{$live.StartTime.ToUniversalTime().Ticks.ToString()}} }); [pscustomobject]@{at=(Get-Date).ToString('o');processes=$rows} | ConvertTo-Json -Depth 6 -Compress""")
before=intact();put(out/'protected.before.json',before)
if not all(r['unchanged'] for r in before): raise RuntimeError('Protected input changed before diagnosis')
lock(); gate=snapshot();put(out/'process_gate.json',gate)
if obs.named(gate,'SLDWORKS.exe') or obs.named(gate,'sldProcMon.exe'): raise RuntimeError('CAD occupied')
command=c['argv']
put(out/'command.json',{'argv':command,'saveRequested':c['mode']=='edit','scope':'isolated old-door '+c['mode']})
during=[];errors=[];actions=[];owner=None;natural=False;events=[];child=None;final=None
try:
    with (out/'stdout.txt').open('xb') as stdout,(out/'stderr.txt').open('xb') as stderr:
        child=subprocess.Popen(command,cwd=out,stdout=stdout,stderr=stderr,creationflags=subprocess.CREATE_NO_WINDOW)
        while child.poll() is None:
            try: lock();during.append(snapshot())
            except Exception as e: errors.append(str(e))
            time.sleep(4)
    events=load(c['output'])['events']
    owners=[r for r in events if r.get('inspection')=='session_owner']
    if len(owners)!=1 or owners[0].get('exactOwnershipVerified') is not True: raise RuntimeError('Native session owner missing')
    owner=owners[0]
    witnessed=any(r['pid']==owner['cadPid'] and r.get('startTimeUtcTicks')==owner['startTimeUtcTicks'] and str(r.get('executablePath','')).lower()==owner['executable'].lower() for s in during for r in obs.named(s,'SLDWORKS.exe'))
    if not witnessed: raise RuntimeError('Native owner not externally witnessed')
    natural=any(r.get('inspection')=='natural_exit' and r.get('cadPid')==owner['cadPid'] and r.get('ownedCadExited') is True and r.get('forcedTermination') is False for r in events)
    after=snapshot();put(out/'after_native_processes.json',after)
    if natural and not obs.named(after,'SLDWORKS.exe'):
        for monitor in obs.named(after,'sldProcMon.exe'):
            keys=('name','pid','ppid','created','executablePath','commandLine','startTimeUtcTicks')
            seen=any(all(r.get(k)==monitor.get(k) for k in keys) for s in during for r in obs.named(s,'sldProcMon.exe'))
            if monitor['ppid']!=owner['cadPid'] or monitor['parentExists'] or not seen: raise RuntimeError('Monitor ownership not established; preserved')
            lock();actions.append(obs.cleanup_orphan(monitor,out/'probe'))
    final=snapshot()
except Exception as e: errors.append(str(e))
finally:
    if child is not None and child.poll() is None: child.wait()
    put(out/'during_processes.json',during)
    after_files=intact();put(out/'protected.after.json',after_files)
    completion=[r for r in events if r.get('inspection')=='completion']
    complete=len(completion)==1 and completion[0].get('operationComplete') is True
    passed=child is not None and child.returncode==0 and natural and complete and all(r['unchanged'] for r in after_files) and not errors and final is not None and not obs.named(final,'SLDWORKS.exe') and not obs.named(final,'sldProcMon.exe')
    result={'pass':passed,'exitCode':None if child is None else child.returncode,'naturalExit':natural,'allProtectedUnchanged':all(r['unchanged'] for r in after_files),'errors':errors,'cleanup':actions,'finalProcesses':final,'completion':completion,'modelAccepted':False,'scope':'isolated old-door '+c['mode']}
    put(out/'session.json',result);print(json.dumps({'output':str(out),'pass':passed,'exitCode':result['exitCode'],'naturalExit':natural,'allProtectedUnchanged':result['allProtectedUnchanged'],'errors':errors,'cleanup':actions,'nativeReadFailureCount':sum(r.get('inspection')=='read_failure' for r in events),'modelAccepted':False},ensure_ascii=False),flush=True)
if not passed: raise RuntimeError('Isolated old-door migration did not complete cleanly')
lock()
