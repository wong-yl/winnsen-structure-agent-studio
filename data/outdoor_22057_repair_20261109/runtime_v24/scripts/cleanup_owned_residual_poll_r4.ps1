param([Parameter(Mandatory=$true)][string]$EvidenceName)
$ErrorActionPreference='Stop'
$stage=Split-Path -Parent $PSScriptRoot
$e=Get-Content -LiteralPath (Join-Path $stage $EvidenceName) -Encoding UTF8 | ConvertFrom-Json
if(-not $e.exit_app_called){throw 'No completed owned-session exit evidence'}
if($e.PSObject.Properties.Name -contains '3d_interconnect_original_enabled' -and -not $e.'3d_interconnect_preference_restored'){throw 'User preference restoration not proven'}
$ownedPid=[int]$e.process_id
$out=Join-Path $stage ([IO.Path]::GetFileNameWithoutExtension($EvidenceName)+'_owned_cleanup_poll_r4.json')
if(Test-Path -LiteralPath $out){throw 'Preserve prior cleanup evidence'}
$record=[ordered]@{pid=$ownedPid;exit_evidence=$EvidenceName;checked_utc=[datetime]::UtcNow.ToString('o');action='already exited';exit_confirmed=$false}
$p=Get-Process -Id $ownedPid -ErrorAction SilentlyContinue
if($p){
 if($p.StartTime.ToUniversalTime().Ticks -ne ([datetime]$e.process_start_utc).ToUniversalTime().Ticks){throw 'Owned process start mismatch'}
 if($p.Path -notlike 'D:\SOILDW*\SOLIDW*\SLDWORKS.exe' -and $p.Path -ne 'D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe'){throw 'Owned process image mismatch'}
 if($p.MainWindowTitle -and $p.MainWindowTitle -notmatch '^SOLIDWORKS(?: Premium)? 2020'){throw 'Possible reopened document; refuse residual cleanup'}
 $record.start_utc=$p.StartTime.ToUniversalTime().ToString('o');$record.path=$p.Path;$record.title=$p.MainWindowTitle
 $deadline=[datetime]::UtcNow.AddSeconds(90)
 do {
  $remaining=Get-Process -Id $ownedPid -ErrorAction SilentlyContinue
  if(-not $remaining){break}
  if($remaining.StartTime.ToUniversalTime().Ticks -ne ([datetime]$e.process_start_utc).ToUniversalTime().Ticks){throw 'Process id was reused; refuse further action'}
  Start-Sleep -Milliseconds 200
 } while([datetime]::UtcNow -lt $deadline)
 if($remaining){throw 'Owned CAD did not exit naturally within 90 seconds; no termination attempted'}
 $record.action='waited for verified owned CAD to exit naturally'
}
$record.exit_confirmed=$true
$record.finished_utc=[datetime]::UtcNow.ToString('o')
$record|ConvertTo-Json|Set-Content -LiteralPath $out -Encoding UTF8
Write-Output "Owned CAD exit confirmed $ownedPid"
