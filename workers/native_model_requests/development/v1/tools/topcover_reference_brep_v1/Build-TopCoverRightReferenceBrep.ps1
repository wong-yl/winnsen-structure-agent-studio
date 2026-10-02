[CmdletBinding()]
param([switch]$SkipVerify)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$dir = $PSScriptRoot
$source = Join-Path $dir 'TopCoverRightReferenceBrep.cs'
$exe = Join-Path $dir 'TopCoverRightReferenceBrep.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$interopRoot = [IO.Path]::GetFullPath((Join-Path $dir '..\bin'))
$swSource = Join-Path $interopRoot 'SolidWorks.Interop.sldworks.dll'
$constSource = Join-Path $interopRoot 'SolidWorks.Interop.swconst.dll'
$sw = Join-Path $dir 'SolidWorks.Interop.sldworks.dll'
$const = Join-Path $dir 'SolidWorks.Interop.swconst.dll'
foreach ($p in @($source,$compiler,$swSource,$constSource,(Join-Path $dir 'verify_static.mjs'),(Join-Path $dir 'TopCoverRightReferenceBrep.exe.config'),(Join-Path $dir 'README.md'))) { if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "Missing build input: $p"} }
function Cad { @(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue | Sort-Object ProcessName,Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" }) }
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToUpperInvariant() }
function NormalizedSourceHash {
  $text=[IO.File]::ReadAllText($source,[Text.Encoding]::UTF8).Replace("`r`n","`n").Replace("`r","`n")
  $pattern='(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)'
  if([regex]::Matches($text,$pattern).Count -ne 1){throw 'Expected exactly one source stamp'}
  $normal=[regex]::Replace($text,$pattern,'${1}__SOURCE_SHA256__${2}')
  $sha=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($normal)))).Replace('-','')}finally{$sha.Dispose()}
}
$before=@(Cad);if($before.Count -ne 0){throw 'Build requires zero CAD processes'}
if((Hash $swSource) -cne 'EAB80E05D11A96FAB2037F072D114892DD975ED8DD7FE149D2A3B192B69E7DEC'){throw 'sldworks interop hash mismatch'}
if((Hash $constSource) -cne '296C1400FEEDC82096854A41ED2B10EFF6F0ACD194FC0552B02FD48E8FE22359'){throw 'swconst interop hash mismatch'}
foreach($pair in @(@($swSource,$sw),@($constSource,$const))){
  if(Test-Path -LiteralPath $pair[1]){if((Hash $pair[0]) -cne (Hash $pair[1])){throw "Local interop hash mismatch: $($pair[1])"}}
  else{Copy-Item -LiteralPath $pair[0] -Destination $pair[1]}
}
$stamp=NormalizedSourceHash
$text=[IO.File]::ReadAllText($source,[Text.Encoding]::UTF8)
$text=[regex]::Replace($text,'(ExpectedSourceSha256\s*=\s*")(?:__SOURCE_SHA256__|[A-F0-9]{64})(";)','${1}'+$stamp+'${2}')
[IO.File]::WriteAllText($source,$text,[Text.UTF8Encoding]::new($false))
$output=@(& $compiler /nologo /target:exe /platform:x64 /optimize+ /warnaserror+ /codepage:65001 "/out:$exe" /reference:System.Core.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll "/reference:$sw" "/reference:$const" $source 2>&1)
if($LASTEXITCODE -ne 0){throw "C# compilation failed: $($output -join [Environment]::NewLine)"}
if(@(Compare-Object -ReferenceObject $before -DifferenceObject @(Cad)).Count -ne 0){throw 'Compilation changed CAD process set'}
$manifest=[ordered]@{schema='winnsen.16029.native_toolchain_manifest.v1';tool=[ordered]@{id='topcover_reference_brep_v1';mode='left-side-760-and-1000-sharp-flat-detailed-v7';sourceNormalizedSha256=$stamp;executableSha256=(Hash $exe);cadMode='read_only_reference_capture';purpose='structure_engineering_assistance';runtimeDependencies=@([ordered]@{path='SolidWorks.Interop.sldworks.dll';sha256=(Hash $sw)},[ordered]@{path='SolidWorks.Interop.swconst.dll';sha256=(Hash $const)})}}
[IO.File]::WriteAllText((Join-Path $dir 'toolchain_manifest.json'),(($manifest|ConvertTo-Json -Depth 6)+"`n"),[Text.UTF8Encoding]::new($false))
if(-not $SkipVerify){& (Join-Path $dir 'Verify-TopCoverRightReferenceBrep.ps1');if($LASTEXITCODE -ne 0){throw 'Static verification failed'}}
$files=@('Build-TopCoverRightReferenceBrep.ps1','TopCoverRightReferenceBrep.cs','TopCoverRightReferenceBrep.exe','TopCoverRightReferenceBrep.exe.config','SolidWorks.Interop.sldworks.dll','SolidWorks.Interop.swconst.dll','verify_static.mjs','Verify-TopCoverRightReferenceBrep.ps1','README.md','toolchain_manifest.json')
[IO.File]::WriteAllText((Join-Path $dir 'SHA256SUMS.txt'),(($files|ForEach-Object{"$(Hash (Join-Path $dir $_))  $_"})-join "`n")+"`n",[Text.Encoding]::ASCII)
[ordered]@{status='PASS';sourceNormalizedSha256=$stamp;executableSha256=(Hash $exe);cadProcessesAfter=(Cad)}|ConvertTo-Json -Depth 5
