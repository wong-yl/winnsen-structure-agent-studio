$ErrorActionPreference = 'Stop'

$toolDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $toolDir 'Add16029V43LockHoleDatums.cs'
$outputDir = Join-Path $toolDir 'bin'
$output = Join-Path $outputDir 'Add16029V43LockHoleDatums.exe'
$sldworks = Join-Path $outputDir 'SolidWorks.Interop.sldworks.dll'
$swconst = Join-Path $outputDir 'SolidWorks.Interop.swconst.dll'

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
if (-not (Test-Path -LiteralPath $sldworks)) {
  throw "Missing SolidWorks interop dll: $sldworks"
}
if (-not (Test-Path -LiteralPath $swconst)) {
  throw "Missing SolidWorks constants dll: $swconst"
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
  throw 'csc.exe not found'
}

& $csc /nologo /target:exe /platform:x64 /out:$output /reference:$sldworks /reference:$swconst $source
if ($LASTEXITCODE -ne 0) {
  throw "Add16029V43LockHoleDatums compilation failed with exit code $LASTEXITCODE"
}
Write-Output $output
