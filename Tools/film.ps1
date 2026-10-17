# Films the gameplay video: runs FilmTest.Run in a Unity window (every frame saved to
# Logs\film), then encodes the frames into Builds\Greyspire_Gameplay.mp4 with Tools\encode_film.py.
# The player's own save is set aside and put back, as in smoke.ps1.
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\film.log'
Remove-Item (Join-Path $root 'Temp\__Backupscenes') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $log -ErrorAction SilentlyContinue
Remove-Item (Join-Path $root 'Logs\film') -Recurse -Force -ErrorAction SilentlyContinue
$regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveBackup = Join-Path $root 'Logs\save-backup.reg'
Remove-Item $saveBackup -ErrorAction SilentlyContinue
cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
$p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'FilmTest.Run', '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
if (Test-Path $saveBackup) {
    Start-Sleep -Seconds 2
    cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
    cmd /c "reg import `"$saveBackup`" >nul 2>&1"
    'save restored'
}
Select-String -Path $log -Pattern '^FILM |error CS|NullReference|Exception:|Story: ' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 60 | ForEach-Object { $_.Line }
"frames: $((Get-ChildItem (Join-Path $root 'Logs\film') -Filter *.png -ErrorAction SilentlyContinue).Count)"
