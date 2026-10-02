param(
  [string]$TaskName = 'Winnsen16029ReviewPortal',
  [string]$FirewallRuleName = 'Winnsen 16029 Review Portal TCP 5180',
  [int]$Port = 5180,
  [string]$LanAddress = '192.168.100.117',
  [string]$ExpectedReviewRound = '16029-v43-v37-760w-six-door-engineering-assistance-20260811',
  [string]$ExpectedAssetId = '16029-v43-v37-760w-six-door-engineering-assistance-zip',
  [string]$ExpectedAssetSha256 = '6B9F62E4F697B96909131D81EAD0FA341E059530204DB76480148460883CD1C0',
  [long]$ExpectedAssetSizeBytes = 24535798
)

$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check {
  param(
    [string]$Name,
    [bool]$Ok,
    [object]$Actual,
    [object]$Expected
  )
  $checks.Add([pscustomobject]@{
    name = $Name
    ok = $Ok
    actual = $Actual
    expected = $Expected
  })
}

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runCommand = if (Test-Path -LiteralPath $runKeyPath) { (Get-ItemProperty -LiteralPath $runKeyPath -Name $TaskName -ErrorAction SilentlyContinue).$TaskName } else { $null }
$autostartExists = $null -ne $task -or -not [string]::IsNullOrWhiteSpace([string]$runCommand)
Add-Check -Name 'autostart_registration_exists' -Ok $autostartExists -Actual $(if ($task) { "scheduled-task:$($task.TaskName)" } elseif ($runCommand) { "hkcu-run:$runCommand" } else { '' }) -Expected 'scheduled task or HKCU Run entry for the review portal watchdog'

if ($task) {
  $taskInfo = Get-ScheduledTaskInfo -TaskName $TaskName
  $actionText = (@($task.Actions | ForEach-Object { "$($_.Execute) $($_.Arguments)" }) -join ' ')
  $hasLogonTrigger = @($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }).Count -gt 0
  Add-Check -Name 'scheduled_task_action_is_watchdog' -Ok ($actionText -match 'run_16029_review_portal_watchdog\.ps1' -and $actionText -match '-Port\s+5180') -Actual $actionText -Expected 'run_16029_review_portal_watchdog.ps1 -Port 5180'
  Add-Check -Name 'scheduled_task_has_logon_trigger' -Ok $hasLogonTrigger -Actual $hasLogonTrigger -Expected $true
  Add-Check -Name 'scheduled_task_is_running' -Ok ($task.State -eq 'Running') -Actual ([string]$task.State) -Expected 'Running'
  Add-Check -Name 'scheduled_task_last_result_clean' -Ok ($taskInfo.LastTaskResult -eq 0 -or $task.State -eq 'Running') -Actual $taskInfo.LastTaskResult -Expected 0
} elseif ($runCommand) {
  Add-Check -Name 'hkcu_run_action_is_windowless_launcher' -Ok ($runCommand -match 'wscript\.exe' -and $runCommand -match 'launch_16029_review_portal_watchdog_hidden\.vbs' -and $runCommand -match '\s5180$') -Actual $runCommand -Expected 'wscript.exe launch_16029_review_portal_watchdog_hidden.vbs 5180'
}

$firewallRule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue | Select-Object -First 1
$nodePath = (Get-Command node -ErrorAction Stop).Source
$nodeFirewallRule = Get-NetFirewallApplicationFilter -PolicyStore ActiveStore -ErrorAction SilentlyContinue |
  Where-Object { [string]::Equals($_.Program, $nodePath, [StringComparison]::OrdinalIgnoreCase) } |
  ForEach-Object { Get-NetFirewallRule -AssociatedNetFirewallApplicationFilter $_ -PolicyStore ActiveStore -ErrorAction SilentlyContinue } |
  Where-Object { $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and ([string]$_.Profile -match 'Public|Any') } |
  Select-Object -First 1
$firewallReady = $null -ne $firewallRule -or $null -ne $nodeFirewallRule
Add-Check -Name 'firewall_allows_node_inbound' -Ok $firewallReady -Actual $(if ($firewallRule) { $firewallRule.DisplayName } elseif ($nodeFirewallRule) { "$($nodeFirewallRule.DisplayName)/$($nodeFirewallRule.Profile)" } else { '' }) -Expected 'enabled inbound allow rule for node.exe on the active Public profile'

if ($firewallRule) {
  $portFilter = Get-NetFirewallPortFilter -AssociatedNetFirewallRule $firewallRule
  $addressFilter = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $firewallRule
  Add-Check -Name 'firewall_rule_allows_inbound_tcp' -Ok ($firewallRule.Enabled -eq 'True' -and $firewallRule.Direction -eq 'Inbound' -and $firewallRule.Action -eq 'Allow' -and $portFilter.Protocol -eq 'TCP' -and [string]$portFilter.LocalPort -eq [string]$Port) -Actual "$($firewallRule.Enabled)/$($firewallRule.Direction)/$($firewallRule.Action)/$($portFilter.Protocol)/$($portFilter.LocalPort)" -Expected "True/Inbound/Allow/TCP/$Port"
  Add-Check -Name 'firewall_rule_is_lan_scoped' -Ok ($addressFilter.LocalAddress -contains $LanAddress -and $addressFilter.RemoteAddress -contains '192.168.100.0/255.255.255.0') -Actual "local=$($addressFilter.LocalAddress -join ','); remote=$($addressFilter.RemoteAddress -join ',')" -Expected "local=$LanAddress; remote=192.168.100.0/255.255.255.0"
}

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
Add-Check -Name 'portal_port_is_listening' -Ok ($null -ne $listener) -Actual $(if ($listener) { "$($listener.LocalAddress):$($listener.LocalPort)" } else { '' }) -Expected "0.0.0.0:$Port"

$watchdogProcess = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -match 'run_16029_review_portal_watchdog\.ps1' -and $_.CommandLine -match '-Port\s+5180' } |
  Select-Object -First 1
Add-Check -Name 'watchdog_process_is_running' -Ok ($null -ne $watchdogProcess) -Actual $(if ($watchdogProcess) { $watchdogProcess.ProcessId } else { '' }) -Expected 'running watchdog process id'
if ($watchdogProcess) {
  $watchdogUiProcess = Get-Process -Id $watchdogProcess.ProcessId -ErrorAction SilentlyContinue
  Add-Check -Name 'watchdog_has_no_visible_window' -Ok ($watchdogUiProcess.MainWindowHandle -eq 0) -Actual $watchdogUiProcess.MainWindowHandle -Expected 0
}

if ($listener) {
  $process = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
  Add-Check -Name 'listener_is_review_portal' -Ok ($process.CommandLine -match 'serve_16029_review_downloads\.mjs') -Actual $process.CommandLine -Expected 'serve_16029_review_downloads.mjs'
}

$localStatus = $null
try {
  $localStatus = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/status.json" -TimeoutSec 3
} catch {
  $localStatus = $null
}
Add-Check -Name 'localhost_status_ok' -Ok ($localStatus.status -eq 'ok') -Actual $(if ($localStatus) { $localStatus.status } else { '' }) -Expected 'ok'

$lanStatus = $null
try {
  $lanStatus = Invoke-RestMethod -Uri "http://${LanAddress}:$Port/status.json" -TimeoutSec 3
} catch {
  $lanStatus = $null
}
Add-Check -Name 'lan_status_ok' -Ok ($lanStatus.status -eq 'ok') -Actual $(if ($lanStatus) { $lanStatus.status } else { '' }) -Expected 'ok'

if ($lanStatus) {
  $currentAsset = @($lanStatus.assets)[0]
  Add-Check -Name 'review_round_is_current' -Ok ($lanStatus.reviewRound -eq $ExpectedReviewRound) -Actual $lanStatus.reviewRound -Expected $ExpectedReviewRound
  Add-Check -Name 'current_asset_is_available' -Ok ($currentAsset.id -eq $ExpectedAssetId -and $currentAsset.available -eq $true -and $currentAsset.integrityVerified -eq $true) -Actual "$($currentAsset.id)/$($currentAsset.available)/$($currentAsset.integrityVerified)" -Expected "$ExpectedAssetId/True/True"
  Add-Check -Name 'current_asset_integrity_metadata_matches' -Ok ($currentAsset.sha256 -eq $ExpectedAssetSha256 -and [long]$currentAsset.expectedSizeBytes -eq $ExpectedAssetSizeBytes -and [long]$currentAsset.sizeBytes -eq $ExpectedAssetSizeBytes) -Actual "$($currentAsset.sha256)/$($currentAsset.expectedSizeBytes)/$($currentAsset.sizeBytes)" -Expected "$ExpectedAssetSha256/$ExpectedAssetSizeBytes/$ExpectedAssetSizeBytes"
}

$failed = @($checks | Where-Object { -not $_.ok })
$result = [ordered]@{
  schema = 'winnsen.locker16029.review_portal_host_verification.v1'
  verified_at = (Get-Date).ToString('o')
  status = if ($failed.Count -eq 0) { 'PASS' } else { 'FAIL' }
  checks_total = $checks.Count
  checks_failed = $failed.Count
  autostart_name = $TaskName
  port = $Port
  lan_address = $LanAddress
  checks = $checks
}

$result | ConvertTo-Json -Depth 6
if ($failed.Count -gt 0) { exit 1 }
