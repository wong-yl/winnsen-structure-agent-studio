param(
  [string]$TaskName = 'Winnsen16029ReviewPortal',
  [string]$FirewallRuleName = 'Winnsen 16029 Review Portal TCP 5180',
  [int]$Port = 5180,
  [string]$LanAddress = '192.168.100.117',
  [string]$LanSubnet = '192.168.100.0/24'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$watchdogPath = Join-Path $PSScriptRoot 'run_16029_review_portal_watchdog.ps1'
$launcherPath = Join-Path $PSScriptRoot 'launch_16029_review_portal_watchdog_hidden.vbs'
$serverPath = Join-Path $PSScriptRoot 'serve_16029_review_downloads.mjs'
$verifyPath = Join-Path $PSScriptRoot 'verify_16029_review_portal_host.ps1'

foreach ($path in @($watchdogPath, $launcherPath, $serverPath, $verifyPath)) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Required review portal file not found: $path"
  }
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$windowsPrincipal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdministrator = $windowsPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$nodePath = (Get-Command node -ErrorAction Stop).Source
$wscriptExe = Join-Path $env:SystemRoot 'System32\wscript.exe'
if (-not (Test-Path -LiteralPath $wscriptExe -PathType Leaf)) {
  throw "Windows Script Host was not found: $wscriptExe"
}
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runCommand = "`"$wscriptExe`" `"$launcherPath`" $Port"
New-Item -Path $runKeyPath -Force | Out-Null
New-ItemProperty -Path $runKeyPath -Name $TaskName -PropertyType String -Value $runCommand -Force | Out-Null

$existingNodeFirewallRule = Get-NetFirewallApplicationFilter -PolicyStore ActiveStore -ErrorAction SilentlyContinue |
  Where-Object { [string]::Equals($_.Program, $nodePath, [StringComparison]::OrdinalIgnoreCase) } |
  ForEach-Object { Get-NetFirewallRule -AssociatedNetFirewallApplicationFilter $_ -PolicyStore ActiveStore -ErrorAction SilentlyContinue } |
  Where-Object { $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and ([string]$_.Profile -match 'Public|Any') } |
  Select-Object -First 1

$firewallMode = 'existing_node_program_rule'
if (-not $existingNodeFirewallRule) {
  if (-not $isAdministrator) {
    throw 'No enabled inbound Node.js firewall rule exists, and this session is not elevated to create one.'
  }
  Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
  New-NetFirewallRule `
    -Name 'Winnsen16029ReviewPortalTcp5180' `
    -DisplayName $FirewallRuleName `
    -Description 'Allow only the 192.168.100.0/24 engineering LAN to reach the Winnsen 16029 review portal.' `
    -Enabled True `
    -Profile Any `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalAddress $LanAddress `
    -LocalPort $Port `
    -RemoteAddress $LanSubnet `
    -EdgeTraversalPolicy Block | Out-Null
  $firewallMode = 'dedicated_lan_rule'
}

$watchdogProcess = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -match 'run_16029_review_portal_watchdog\.ps1' -and $_.CommandLine -match "-Port\s+$Port" } |
  Select-Object -First 1
if (-not $watchdogProcess) {
  $created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $runCommand }
  if ($created.ReturnValue -ne 0 -or $created.ProcessId -le 0) {
    throw "Windows process service failed to launch the windowless review portal starter: return value $($created.ReturnValue)."
  }

  $watchdogDeadline = (Get-Date).AddSeconds(10)
  do {
    Start-Sleep -Milliseconds 200
    $watchdogProcess = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
      Where-Object { $_.CommandLine -match 'run_16029_review_portal_watchdog\.ps1' -and $_.CommandLine -match "-Port\s+$Port" } |
      Select-Object -First 1
  } while (-not $watchdogProcess -and (Get-Date) -lt $watchdogDeadline)
}

if (-not $watchdogProcess) {
  throw 'The windowless starter ran, but the review portal watchdog process did not stay running.'
}
$watchdogProcessId = [int]$watchdogProcess.ProcessId

$status = $null
$deadline = (Get-Date).AddSeconds(30)
do {
  try {
    $status = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/status.json" -TimeoutSec 2
  } catch {
    $status = $null
  }
  if ($status.status -eq 'ok') { break }
  Start-Sleep -Milliseconds 500
} while ((Get-Date) -lt $deadline)

if ($status.status -ne 'ok') {
  throw "Review portal did not become ready on port $Port after the autostart launch."
}

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop | Select-Object -First 1
$result = [ordered]@{
  schema = 'winnsen.locker16029.review_portal_autostart_install.v1'
  installed_at = (Get-Date).ToString('o')
  status = 'PASS'
  autostart_name = $TaskName
  autostart_mode = 'hkcu_run_windowless'
  autostart_user = $identity
  autostart_command = $runCommand
  watchdog_process_id = $watchdogProcessId
  firewall_mode = $firewallMode
  firewall_rule = if ($firewallMode -eq 'dedicated_lan_rule') { $FirewallRuleName } else { $existingNodeFirewallRule.DisplayName }
  firewall_local_address = $LanAddress
  firewall_remote_subnet = $LanSubnet
  listener = "$($listener.LocalAddress):$($listener.LocalPort)"
  portal_status = $status.status
  review_round = $status.reviewRound
}

$result | ConvertTo-Json -Depth 4
