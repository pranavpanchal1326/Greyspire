# Runs the check of the changes made after the first play-test (IterTest.Run) in a Unity
# window and prints what it logged. Pictures land in Logs\iter. The player's own save is set
# aside and put back, as in smoke.ps1.
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\iter.log'
$backups = Join-Path $root 'Temp\__Backupscenes'
if (Test-Path $backups) { Remove-Item $backups -Recurse -Force }
if (Test-Path $log) { Remove-Item $log }
$shots = Join-Path $root 'Logs\iter'
if (Test-Path $shots) { Get-ChildItem $shots -Filter *.png | Remove-Item }
$regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveBackup = Join-Path $root 'Logs\save-backup.reg'
if (Test-Path $saveBackup) { Remove-Item $saveBackup }
cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
$p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'IterTest.Run', '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(500000)) { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
if (Test-Path $saveBackup) {
    Start-Sleep -Seconds 2
    cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
    cmd /c "reg import `"$saveBackup`" >nul 2>&1"
    'save restored'
}
Select-String -Path $log -Pattern '^ITER |error CS|Exception:' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 40 | ForEach-Object { $_.Line }
