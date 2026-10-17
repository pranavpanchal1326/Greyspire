# Runs the NavMesh check in a Unity window (NavTest.Run) and prints what it logged.
# Pictures land in Logs\nav. The player's own save is set aside and put back, as in smoke.ps1.
#   -Batch : headless compile/import check only
param([switch]$Batch)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\nav.log'
Remove-Item $log -ErrorAction SilentlyContinue
Remove-Item (Join-Path $root 'Temp\__Backupscenes') -Recurse -Force -ErrorAction SilentlyContinue

if ($Batch) {
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$root`"", '-logFile', "`"$log`"" -PassThru
    if (-not $p.WaitForExit(540000)) { $p.Kill(); 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
} else {
    Remove-Item (Join-Path $root 'Logs\nav\*.png') -ErrorAction SilentlyContinue
    $regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
    $saveBackup = Join-Path $root 'Logs\save-backup.reg'
    Remove-Item $saveBackup -ErrorAction SilentlyContinue
    cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
    $p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'NavTest.Run', '-logFile', "`"$log`"" -PassThru
    if (-not $p.WaitForExit(420000)) { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
    if (Test-Path $saveBackup) {
        Start-Sleep -Seconds 2
        cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
        cmd /c "reg import `"$saveBackup`" >nul 2>&1"
        'save restored'
    }
}
Select-String -Path $log -Pattern '^NAV |error CS|Shader error|NullReference|Exception:|MissingComponent' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 60 | ForEach-Object { $_.Line }
Get-ChildItem (Join-Path $root 'Logs\nav') -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
