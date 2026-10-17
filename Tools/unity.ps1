# Runs one editor method headlessly and prints anything that went wrong.
#   Tools\unity.ps1 VillageImport.Preview
param([Parameter(Mandatory = $true)][string]$Method, [string]$Marker = 'DONE')
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\unity.log'
Remove-Item $log -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$root`"", '-executeMethod', $Method, '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(540000)) { $p.Kill(); 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
Select-String -Path $log -Pattern "error CS|Shader error|Exception|$Marker" |
    Where-Object { $_.Line -notmatch 'Packages/|Licensing' } | Select-Object -First 20 | ForEach-Object { $_.Line }
