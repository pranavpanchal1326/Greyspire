# Authors the A01 gait clips and their ledger (Assets/Editor/GaitAuthoring.cs) on the isolated
# mirror, then copies the results back. Prints the ledger as a table.
#
#   powershell -ExecutionPolicy Bypass -File Tools/author_gaits.ps1 [-Mirror D:\gs-mirror]
param([string]$Mirror = 'D:\gs-mirror')
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
robocopy (Join-Path $source 'Assets') (Join-Path $Mirror 'Assets') /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
$log = Join-Path $Mirror 'Logs\gait.log'
$p = Start-Process $unity -ArgumentList '-projectPath', $Mirror, '-batchmode', '-quit', '-executeMethod', 'GaitAuthoring.Build', '-logFile', $log -PassThru -WindowStyle Hidden
$p.WaitForExit()
Select-String -LiteralPath $log -Pattern 'error CS|GAIT FAILED' | ForEach-Object { $_.Line }
if ($p.ExitCode -ne 0) { throw ('Unity exited with code ' + $p.ExitCode) }
Get-Content (Join-Path $Mirror 'Logs\gait-authoring.txt')
$anim = Join-Path $Mirror 'Assets\Resources\Humans\Anim'
Copy-Item (Join-Path $anim 'Gait_*') (Join-Path $source 'Assets\Resources\Humans\Anim') -Force
Copy-Item (Join-Path $Mirror 'Assets\Resources\Humans\gait_ledger.json*') (Join-Path $source 'Assets\Resources\Humans') -Force
$ledger = Get-Content (Join-Path $Mirror 'Assets\Resources\Humans\gait_ledger.json') -Raw | ConvertFrom-Json
$ledger.clips | ForEach-Object { '{0,-16} fwd {1,6:N2} right {2,6:N2} slip {3:N2} drift {4:N3} gap {5:N3} plant {6:N2} floor {7:N3}' -f $_.clip, $_.paceForward, $_.paceRight, $_.stanceSlip, $_.stanceDrift, $_.minFootGap, $_.leftPlant, $_.floorGap }
