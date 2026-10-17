param([switch]$Build)
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path $PSScriptRoot -Parent
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$testLog = Join-Path $testRoot 'Logs\feel.log'
$saveKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveProviderPath = 'HKCU:\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveSnapshot = Join-Path $testRoot ('Backups\feel-save-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.reg')
$hadSave = Test-Path -LiteralPath $saveProviderPath
if ($hadSave) {
    & reg.exe export $saveKey $saveSnapshot /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot the editor save; no test was started.' }
}
try {
    $arguments = @('-batchmode', '-projectPath', ('"' + $testRoot + '"'), '-executeMethod', 'FeelTest.Run', '-logFile', ('"' + $testLog + '"'))
    $testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $testDeadline = (Get-Date).AddMinutes(5)
    while (-not $testProcess.HasExited) {
        if ((Get-Date) -gt $testDeadline) { Stop-Process -Id $testProcess.Id; throw 'Unity feel test timed out.' }
        Start-Sleep -Seconds 2
        $testProcess.Refresh()
    }
    Get-Content -LiteralPath $testLog | Select-String -Pattern 'FEEL |error CS|Exception:' | ForEach-Object { $_.Line }
    if ($testProcess.ExitCode -ne 0) { throw ('Unity exited with code ' + $testProcess.ExitCode) }
    if (-not (Select-String -LiteralPath $testLog -Pattern 'FEEL DONE' -Quiet)) { throw 'The test did not report completion.' }
}
finally {
    # This exact registry key is Unity's Greyspire editor save; the snapshot is kept as a backup.
    if (Test-Path -LiteralPath $saveProviderPath) { Remove-Item -LiteralPath $saveProviderPath -Recurse -Force }
    if ($hadSave) {
        & reg.exe import $saveSnapshot | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ('Save restore failed; recover from ' + $saveSnapshot) }
    }
    Write-Output 'Greyspire editor save restored.'
}
if ($Build) {
    $buildLog = Join-Path $testRoot 'Logs\upgrade-build.log'
    $buildArguments = @('-batchmode', '-quit', '-projectPath', ('"' + $testRoot + '"'), '-executeMethod', 'BuildGame.Windows', '-logFile', ('"' + $buildLog + '"'))
    $buildProcess = Start-Process -FilePath $unityEditor -ArgumentList $buildArguments -WindowStyle Hidden -PassThru
    while (-not $buildProcess.HasExited) { Start-Sleep -Seconds 2; $buildProcess.Refresh() }
    Get-Content -LiteralPath $buildLog | Select-String -Pattern 'BUILD |error CS|Exception:' | ForEach-Object { $_.Line }
    if ($buildProcess.ExitCode -ne 0) { throw ('Unity build exited with code ' + $buildProcess.ExitCode) }
}
