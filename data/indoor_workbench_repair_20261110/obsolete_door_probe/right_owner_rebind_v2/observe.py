"""Owned read-only diagnostic observer. Timeout preserves CAD and its global lock."""
import sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
import base64, datetime, hashlib, json, re, subprocess, time
from pathlib import Path

def load(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()
def put(path, value):
    with Path(path).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')

def named(state, name): return [row for row in state['processes'] if row['name'].lower() == name.lower()]
def ps(code):
    code = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; " + code
    encoded = base64.b64encode(code.encode('utf-16-le')).decode('ascii')
    result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-EncodedCommand', encoded], capture_output=True, timeout=12, creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode: raise RuntimeError(result.stderr.decode('utf-8', 'replace'))
    return json.loads(result.stdout.decode('utf-8-sig'))

def snapshot():
    return ps("""$rows=@(Get-CimInstance Win32_Process -Filter "Name='SLDWORKS.exe' OR Name='sldProcMon.exe' OR Name='sldworks_fs.exe'" | ForEach-Object { $parent=Get-CimInstance Win32_Process -Filter ('ProcessId='+$_.ParentProcessId); $live=Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; [pscustomobject]@{name=$_.Name;pid=$_.ProcessId;ppid=$_.ParentProcessId;created=$_.CreationDate.ToString('o');commandLine=$_.CommandLine;executablePath=$_.ExecutablePath;parentExists=($null -ne $parent);parentName=$parent.Name;startTimeUtcTicks=if($null -eq $live){$null}else{$live.StartTime.ToUniversalTime().Ticks.ToString()}} }); [pscustomobject]@{at=(Get-Date).ToString('o');processes=$rows} | ConvertTo-Json -Depth 6 -Compress""")

def check_lock(config):
    owner = load(config['lockOwner'])
    if (owner['token'], owner['workerId'], owner['pid']) != (config['lockToken'], config['lockWorker'], config['lockPid']): raise RuntimeError('NATIVE_LOCK_OWNERSHIP_CHANGED')
    age = (datetime.datetime.now(datetime.timezone.utc) - datetime.datetime.fromisoformat(owner['heartbeatAt'].replace('Z', '+00:00'))).total_seconds()
    if age < -5 or age > 30: raise RuntimeError('NATIVE_LOCK_HEARTBEAT_STALE')

def intact(config):
    rows = []
    for row in config['immutable']:
        path = Path(row['path']); stat = path.lstat()
        if stat.st_file_attributes & 0x400 or stat.st_nlink != 1: raise RuntimeError('INPUT_LINK_REFUSED ' + str(path))
        actual = sha(path); rows.append({**row, 'actualSha256': actual, 'unchanged': actual == row['sha256']})
    return rows

def witnessed(owner, states):
    return owner is not None and owner.get('exactOwnershipVerified') is True and owner.get('processHandleRetained') is True and any(row['pid'] == owner['cadPid'] and row.get('startTimeUtcTicks') == owner['startTimeUtcTicks'] and str(row.get('executablePath', '')).lower() == owner['executable'].lower() for state in states for row in named(state, 'SLDWORKS.exe'))

def cleanup_orphan(monitor, owner, states, config, out):
    keys = ('name', 'pid', 'ppid', 'created', 'executablePath', 'commandLine', 'startTimeUtcTicks')
    seen = any(all(row.get(key) == monitor.get(key) for key in keys) for state in states for row in named(state, 'sldProcMon.exe'))
    if not seen or monitor['ppid'] != owner['cadPid'] or monitor['parentExists'] or not re.search(r'(?:^|\s)--ppid=' + str(owner['cadPid']) + r'(?:\s|$)', monitor.get('commandLine', '')): raise RuntimeError('MONITOR_OWNERSHIP_NOT_ESTABLISHED_PRESERVED')
    check_lock(config); put(out / ('cleanup-intent-' + str(monitor['pid']) + '.json'), monitor)
    literal = lambda value: "'" + str(value).replace("'", "''") + "'"
    return ps("""$target=Get-CimInstance Win32_Process -Filter "ProcessId=%d"; $cad=@(Get-CimInstance Win32_Process -Filter "Name='SLDWORKS.exe'"); if($null -eq $target){throw 'MONITOR_ALREADY_GONE_RECHECK'}; $parent=Get-CimInstance Win32_Process -Filter ('ProcessId='+$target.ParentProcessId); $live=Get-Process -Id $target.ProcessId -ErrorAction Stop; if($cad.Count -ne 0 -or $null -ne $parent -or $target.Name -ine 'sldProcMon.exe' -or $target.ParentProcessId -ne %d -or $target.CreationDate.ToString('o') -cne %s -or $target.CommandLine -cne %s -or $target.ExecutablePath -cne %s -or $live.StartTime.ToUniversalTime().Ticks.ToString() -cne %s){throw 'MONITOR_IDENTITY_OR_ORPHAN_GATE_CHANGED'}; Stop-Process -Id $target.ProcessId -ErrorAction Stop; [pscustomobject]@{pid=$target.ProcessId;action='authorized_exact_orphan_monitor_only'} | ConvertTo-Json -Compress""" % (monitor['pid'], monitor['ppid'], literal(monitor['created']), literal(monitor['commandLine']), literal(monitor['executablePath']), literal(monitor['startTimeUtcTicks'])))

def main():
    config_path = Path(sys.argv[1])
    if sha(config_path) != sys.argv[2]: raise RuntimeError('RUNNER_CONFIG_HASH_CHANGED')
    config = load(config_path); out = Path(config['out']); before = intact(config)
    put(out / 'protected.before.json', before)
    if not all(row['unchanged'] for row in before): raise RuntimeError('PROTECTED_INPUT_CHANGED_BEFORE_DIAGNOSIS')
    check_lock(config); gate = snapshot(); put(out / 'process_gate.json', gate)
    if named(gate, 'SLDWORKS.exe') or named(gate, 'sldProcMon.exe'): raise RuntimeError('CAD_OCCUPIED')
    states, errors, actions, events, owner = [], [], [], [], None
    child = None; native_identity = None; timed_out = False; final = None; natural = False; complete = False; proved = False
    started = time.monotonic(); diagnostic_deadline = started + config['maximumDiagnosticSeconds']; hard_deadline = diagnostic_deadline + config['naturalExitSeconds']
    try:
        with (out / 'stdout.txt').open('xb') as stdout, (out / 'stderr.txt').open('xb') as stderr:
            child = subprocess.Popen(config['argv'], cwd=config['pack'], stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
            put(out / 'native-child-started.json', {'pid': child.pid, 'expectedExecutable': config['executable'], 'identityConfirmed': False})
            native_identity = ps("$p=Get-Process -Id %d -ErrorAction Stop; @{pid=$p.Id;startTicks=$p.StartTime.ToUniversalTime().Ticks.ToString();executable=$p.Path} | ConvertTo-Json -Compress" % child.pid)
            put(out / 'native-child.json', native_identity)
            while child.poll() is None:
                check_lock(config); states.append(snapshot())
                try: events = load(config['output'])['events']
                except (FileNotFoundError, json.JSONDecodeError): pass
                owners = [row for row in events if row.get('inspection') == 'session_owner']
                owner = owners[0] if len(owners) == 1 else None
                if time.monotonic() >= diagnostic_deadline and not timed_out:
                    timed_out = True; put(out / 'timeout.json', {'state': 'manual_disposition_required', 'diagnosticSeconds': config['maximumDiagnosticSeconds'], 'owner': owner, 'ownerExternallyWitnessed': witnessed(owner, states), 'cadTerminated': False, 'nextArmAllowed': False, 'modelAccepted': False})
                if time.monotonic() >= hard_deadline:
                    errors.append('DIAGNOSTIC_TIMEOUT_MANUAL_DISPOSITION_REQUIRED'); break
                time.sleep(1)
        if child.poll() is not None:
            events = load(config['output'])['events']; owners = [row for row in events if row.get('inspection') == 'session_owner']; owner = owners[0] if len(owners) == 1 else None
            proved = witnessed(owner, states)
            if not proved: errors.append('EXACT_NATIVE_OWNER_NOT_EXTERNALLY_WITNESSED')
            natural = proved and any(row.get('inspection') == 'natural_exit' and row.get('cadPid') == owner['cadPid'] and row.get('ownedCadExited') is True and row.get('forcedTermination') is False for row in events)
            final = snapshot()
            if natural and not named(final, 'SLDWORKS.exe'):
                for monitor in named(final, 'sldProcMon.exe'): actions.append(cleanup_orphan(monitor, owner, states, config, out))
                final = snapshot()
            completions = [row for row in events if row.get('inspection') == 'completion']
            complete = len(completions) == 1 and completions[0].get('diagnosticCompleted') is True and completions[0].get('modelAccepted') is False and completions[0].get('operationComplete') is False
    except Exception as error: errors.append(str(error))
    finally:
        proved = witnessed(owner, states)
        put(out / 'during_processes.json', states)
        try: after = intact(config); put(out / 'protected.after.json', after); unchanged = all(row['unchanged'] for row in after)
        except Exception as error: unchanged = False; errors.append(str(error))
        try: final = snapshot()
        except Exception as error: errors.append(str(error))
        live = child is not None and child.poll() is None
        empty = final is not None and not named(final, 'SLDWORKS.exe') and not named(final, 'sldProcMon.exe')
        passed = not timed_out and not live and child is not None and child.returncode == 0 and natural and complete and unchanged and empty and not errors
        result = {'pass': passed, 'arm': config['arm'], 'timedOut': timed_out, 'manualDispositionRequired': live or not empty, 'nextArmAllowed': passed, 'exitCode': None if child is None else child.poll(), 'nativeChildPid': None if child is None else child.pid, 'nativeChildAlive': live, 'nativeChildIdentity': native_identity, 'owner': owner, 'ownerExternallyWitnessed': proved, 'naturalExit': natural, 'allProtectedUnchanged': unchanged, 'errors': errors, 'cleanup': actions, 'finalProcesses': final, 'modelAccepted': False, 'operationComplete': False, 'strictColdAcceptancePassed': False}
        put(out / 'session.json', result); print(json.dumps(result, ensure_ascii=False), flush=True)
    return 0 if passed else 1

if __name__ == '__main__': sys.exit(main())
