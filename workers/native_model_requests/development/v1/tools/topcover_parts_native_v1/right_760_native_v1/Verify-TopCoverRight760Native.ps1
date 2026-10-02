$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest



$toolDir = $PSScriptRoot

$before = @(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue |

    Sort-Object ProcessName,Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })

if ($before.Count -ne 0) { throw 'Static verification requires zero CAD processes' }



$output = @(& node (Join-Path $toolDir 'verify_static.mjs') 2>&1)

if ($LASTEXITCODE -ne 0) {

    throw "Node static verifier failed: $($output -join [Environment]::NewLine)"

}

$output | ForEach-Object { Write-Output $_ }



$after = @(Get-Process -Name SLDWORKS,sldProcMon -ErrorAction SilentlyContinue |

    Sort-Object ProcessName,Id | ForEach-Object { "$($_.ProcessName):$($_.Id)" })

if (@(Compare-Object -ReferenceObject $before -DifferenceObject $after).Count -ne 0) {

    throw 'Static verification changed the CAD process set'

}
