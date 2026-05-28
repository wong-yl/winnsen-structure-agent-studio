$ErrorActionPreference = 'Stop'

$toolDir = $PSScriptRoot
$source = Join-Path $toolDir 'BuildDoorWeldModule.cs'
$outputDir = Join-Path $toolDir 'bin'
$output = Join-Path $outputDir 'BuildDoorWeldModule.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$softwareInstallDirName = -join ([char[]](0x8F6F, 0x4EF6, 0x5B89, 0x88C5, 0x5F55))
$solidWorksRedist = Join-Path (Join-Path (Join-Path "D:\" $softwareInstallDirName) 'soildworks\SOLIDWORKS') 'api\redist'
$swInterop = Join-Path $solidWorksRedist 'SolidWorks.Interop.sldworks.dll'
$swConst = Join-Path $solidWorksRedist 'SolidWorks.Interop.swconst.dll'
$existingSwInterop = Join-Path $outputDir 'SolidWorks.Interop.sldworks.dll'
$existingSwConst = Join-Path $outputDir 'SolidWorks.Interop.swconst.dll'
if (Test-Path -LiteralPath $existingSwInterop) {
  $swInterop = $existingSwInterop
}
if (Test-Path -LiteralPath $existingSwConst) {
  $swConst = $existingSwConst
}

if (-not (Test-Path -LiteralPath $csc)) {
  throw "C# compiler was not found: $csc"
}
if (-not (Test-Path -LiteralPath $swInterop)) {
  if (Test-Path -LiteralPath $existingSwInterop) {
    $swInterop = $existingSwInterop
  } else {
    throw "SolidWorks interop DLL was not found: $swInterop"
  }
}
if (-not (Test-Path -LiteralPath $swConst)) {
  if (Test-Path -LiteralPath $existingSwConst) {
    $swConst = $existingSwConst
  } else {
    throw "SolidWorks constants interop DLL was not found: $swConst"
  }
}
if (-not (Test-Path -LiteralPath $source)) {
  throw "Source file was not found: $source"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $csc /nologo /platform:x64 /target:exe /out:$output /reference:$swInterop /reference:$swConst $source
if ($LASTEXITCODE -ne 0) {
  throw "BuildDoorWeldModule compilation failed with exit code $LASTEXITCODE"
}

if (([IO.Path]::GetFullPath($swInterop)) -ne ([IO.Path]::GetFullPath((Join-Path $outputDir 'SolidWorks.Interop.sldworks.dll')))) {
  Copy-Item -LiteralPath $swInterop -Destination (Join-Path $outputDir 'SolidWorks.Interop.sldworks.dll') -Force
}
if (([IO.Path]::GetFullPath($swConst)) -ne ([IO.Path]::GetFullPath((Join-Path $outputDir 'SolidWorks.Interop.swconst.dll')))) {
  Copy-Item -LiteralPath $swConst -Destination (Join-Path $outputDir 'SolidWorks.Interop.swconst.dll') -Force
}

Write-Output $output
