$ErrorActionPreference = 'Stop'

$toolDir = $PSScriptRoot
$source = Join-Path $toolDir 'BuildOrdinaryDoorModule.cs'
$outputDir = Join-Path $toolDir 'bin'
$output = Join-Path $outputDir 'BuildOrdinaryDoorModule.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$softwareInstallDirName = -join ([char[]](0x8F6F, 0x4EF6, 0x5B89, 0x88C5, 0x5F55))
$solidWorksRedist = Join-Path (Join-Path (Join-Path "D:\" $softwareInstallDirName) 'soildworks\SOLIDWORKS') 'api\redist'
$swInterop = Join-Path $solidWorksRedist 'SolidWorks.Interop.sldworks.dll'
$swConst = Join-Path $solidWorksRedist 'SolidWorks.Interop.swconst.dll'

if (-not (Test-Path -LiteralPath $csc)) {
  throw "C# compiler was not found: $csc"
}
if (-not (Test-Path -LiteralPath $swInterop)) {
  throw "SolidWorks interop DLL was not found: $swInterop"
}
if (-not (Test-Path -LiteralPath $swConst)) {
  throw "SolidWorks constants interop DLL was not found: $swConst"
}
if (-not (Test-Path -LiteralPath $source)) {
  throw "Source file was not found: $source"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $csc /nologo /platform:x64 /target:exe /out:$output /reference:$swInterop /reference:$swConst $source
if ($LASTEXITCODE -ne 0) {
  throw "BuildOrdinaryDoorModule compilation failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $swInterop -Destination (Join-Path $outputDir 'SolidWorks.Interop.sldworks.dll') -Force
Copy-Item -LiteralPath $swConst -Destination (Join-Path $outputDir 'SolidWorks.Interop.swconst.dll') -Force

Write-Output $output
