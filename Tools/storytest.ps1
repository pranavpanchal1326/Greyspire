# Runs the rescue-scene check in a Unity window (StoryTest.Run) and prints what it logged.
# Pictures land in Logs\story. The player's own save is set aside and put back, as in smoke.ps1.
#   -Build : first rebuild the story assets headlessly (StoryBuild.Build)
param([switch]$Build)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\story.log'
Remove-Item (Join-Path $root 'Temp\__Backupscenes') -Recurse -Force -ErrorAction SilentlyContinue

if ($Build) {
    $blog = Join-Path $root 'Logs\storybuild.log'
    Remove-Item $blog -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$root`"", '-executeMethod', 'StoryBuild.Build', '-logFile', "`"$blog`"" -PassThru
    if (-not $p.WaitForExit(540000)) { $p.Kill(); 'BUILD TIMEOUT - killed' } else { "build exit: $($p.ExitCode)" }
    Select-String -Path $blog -Pattern '^STORY |error CS|Exception' | Where-Object { $_.Line -notmatch 'Packages/' } |
        Select-Object -First 20 | ForEach-Object { $_.Line }
    if ($p.ExitCode -ne 0) { return }
}

Remove-Item $log -ErrorAction SilentlyContinue
Remove-Item (Join-Path $root 'Logs\story\*.png') -ErrorAction SilentlyContinue
$regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveBackup = Join-Path $root 'Logs\save-backup.reg'
Remove-Item $saveBackup -ErrorAction SilentlyContinue
cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
$p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'StoryTest.Run', '-logFile', "`"$log`"" -PassThru
if (-not $p.WaitForExit(480000)) { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
if (Test-Path $saveBackup) {
    Start-Sleep -Seconds 2
    cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
    cmd /c "reg import `"$saveBackup`" >nul 2>&1"
    'save restored'
}
Select-String -Path $log -Pattern '^STORY |error CS|Shader error|NullReference|Exception:|MissingComponent' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 70 | ForEach-Object { $_.Line }
Get-ChildItem (Join-Path $root 'Logs\story') -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
