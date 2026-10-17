# Shoots the trailer's footage: runs CineFilm.Run in a Unity window and prints what it logged.
#   -Scout          one frame in twelve, into Logs\cine_scout (to check framing)
#   -Only a,b       only these groups of shots (town, hero, night, floor, statue, combat, guardian, climb, wilds, king, end)
# A full shoot writes every frame to Logs\cine\<shot>\. The player's own save is set aside and put back.
param([switch]$Scout, [string]$Only = '*')
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\cine.log'
$backups = Join-Path $root 'Temp\__Backupscenes'
if (Test-Path $backups) { Remove-Item $backups -Recurse -Force }
if (Test-Path $log) { Remove-Item $log }
$mode = 'full'
if ($Scout) { $mode = 'scout' }
Set-Content -Path (Join-Path $root 'Logs\cine_plan.txt') -Value @($mode, $Only) -Encoding ascii
$regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveBackup = Join-Path $root 'Logs\save-backup.reg'
if (Test-Path $saveBackup) { Remove-Item $saveBackup }
cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
$p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'CineFilm.Run', '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(3000000)) { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
if (Test-Path $saveBackup) {
    Start-Sleep -Seconds 2
    cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
    cmd /c "reg import `"$saveBackup`" >nul 2>&1"
    'save restored'
}
Select-String -Path $log -Pattern '^CINE |error CS|Shader error|Exception:' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 80 | ForEach-Object { $_.Line }
