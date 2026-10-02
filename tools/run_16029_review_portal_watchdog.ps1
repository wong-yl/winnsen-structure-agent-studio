param(
  [int]$Port = 5180,
  [int]$PollSeconds = 5
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$serverScript = Join-Path $PSScriptRoot 'serve_16029_review_downloads.mjs'
$serverLauncher = Join-Path $PSScriptRoot 'start_16029_review_portal.mjs'
$parametricWorkerScript = Join-Path $PSScriptRoot 'run_16029_parametric_portal_worker.mjs'
$parametricWorkerLauncher = Join-Path $PSScriptRoot 'start_16029_parametric_portal_worker.mjs'
$runtimeDir = Join-Path $repo 'data\review_portal_runtime'
$watchdogLog = Join-Path $runtimeDir 'watchdog.log'
$serverOutLog = Join-Path $runtimeDir 'server.stdout.log'
$serverErrorLog = Join-Path $runtimeDir 'server.stderr.log'
$mutex = New-Object System.Threading.Mutex($false, 'Global\Winnsen16029ReviewPortalWatchdog')

if (-not $mutex.WaitOne(0, $false)) {
  Write-Output '16029 review portal watchdog is already running.'
  exit 0
}

try {
  if (-not (Test-Path -LiteralPath $serverScript)) {
    throw "Review portal server script not found: $serverScript"
  }
  $node = (Get-Command node -ErrorAction Stop).Source
  New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null

  while ($true) {
    $parametricWorker = Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" -ErrorAction Stop |
      Where-Object { $_.CommandLine -like "*$parametricWorkerScript*--watch*" } |
      Select-Object -First 1
    if (-not $parametricWorker) {
      & $node $parametricWorkerLauncher 2>&1 | ForEach-Object { Add-Content -LiteralPath $watchdogLog -Encoding UTF8 -Value $_ }
    }
    $listener = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
      Where-Object { $_.Port -eq $Port } | Select-Object -First 1
    if ($listener) {
      Start-Sleep -Seconds $PollSeconds
      continue
    }

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $watchdogLog -Encoding UTF8 -Value "$timestamp starting review portal on port $Port"
    $env:STUDIO_REVIEW_PORT = [string]$Port
    & $node $serverLauncher 2>&1 | ForEach-Object { Add-Content -LiteralPath $watchdogLog -Encoding UTF8 -Value $_ }
    $exitCode = $LASTEXITCODE
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $watchdogLog -Encoding UTF8 -Value "$timestamp server launcher returned exit code $exitCode"
    Start-Sleep -Seconds $PollSeconds
  }
}
finally {
  $mutex.ReleaseMutex()
  $mutex.Dispose()
}
