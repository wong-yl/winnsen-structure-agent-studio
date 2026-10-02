$ErrorActionPreference='Stop'
$dir=$PSScriptRoot
$before=@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue)
if($before.Count -ne 0){throw 'Verification requires zero CAD processes'}
node (Join-Path $dir 'verify_static.mjs')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$after=@(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue)
if($after.Count -ne 0){throw 'Verification changed CAD process set'}
