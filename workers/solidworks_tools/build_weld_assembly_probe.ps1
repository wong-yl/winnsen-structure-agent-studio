$ErrorActionPreference = 'Stop'

$toolDir = $PSScriptRoot
$source = Join-Path $toolDir 'WeldAssemblyProbe.cs'
$outputDir = Join-Path $toolDir 'bin'
$output = Join-Path $outputDir 'WeldAssemblyProbe.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
  throw "C# compiler was not found: $csc"
}
if (-not (Test-Path -LiteralPath $source)) {
  throw "Source file was not found: $source"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $csc /nologo /platform:x64 /target:exe /out:$output /reference:Microsoft.CSharp.dll $source
if ($LASTEXITCODE -ne 0) {
  throw "WeldAssemblyProbe compilation failed with exit code $LASTEXITCODE"
}

Write-Output $output
