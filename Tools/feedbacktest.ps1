param([switch]$Build, [switch]$Visual)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$testLog = Join-Path $testRoot 'Logs\feedback.log'
$testSave = Join-Path $testRoot ('Logs\test-saves\feedback-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$saveKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveProviderPath = 'HKCU:\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveSnapshot = Join-Path $testRoot ('Backups\feedback-registry-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.reg')
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close the Unity editor before running the isolated feedback check.' }
New-Item -ItemType Directory -Path (Join-Path $testRoot 'Backups'), $testSave -Force | Out-Null
$hadSave = Test-Path -LiteralPath $saveProviderPath
if ($hadSave) {
    & reg.exe export $saveKey $saveSnapshot /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot editor preferences; the test was not started.' }
}
function Invoke-CheckedUnity([string]$method, [string]$log, [bool]$quit) {
    if (Test-Path -LiteralPath $log) {
        Move-Item -LiteralPath $log -Destination ($log + '.previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    }
    $arguments = @('-projectPath', ('"' + $testRoot + '"'), '-executeMethod', $method,
        '-greyspire-save-dir', ('"' + $testSave + '"'), '-logFile', ('"' + $log + '"'))
    if ($quit -or -not $Visual) { $arguments += '-batchmode' }
    if ($quit) { $arguments += '-quit' }
    $testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddMinutes(6)
        while (-not $testProcess.HasExited) {
            if ((Get-Date) -gt $deadline) { throw 'Unity feedback check timed out.' }
            if ((Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -Pattern 'error CS\d+' -Quiet)) { throw 'Unity script compilation failed; inspect the feedback log.' }
            Start-Sleep -Seconds 2
            $testProcess.Refresh()
        }
        Get-Content -LiteralPath $log | Select-String -Pattern '^FEEDBACK (DONE|FAILED)|^BUILD |error CS|Exception:' | ForEach-Object { $_.Line }
        if ($testProcess.ExitCode -ne 0) { throw ('Unity exited with code ' + $testProcess.ExitCode) }
    }
    finally {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id; $testProcess.WaitForExit() }
    }
}
try {
    Invoke-CheckedUnity 'FeedbackTest.Run' $testLog $false
    if (-not (Select-String -LiteralPath $testLog -Pattern '^FEEDBACK DONE' -Quiet)) { throw 'The feedback check did not report completion.' }
}
finally {
    # Only the explicitly named Greyspire registry key is restored. The player's
    # journey JSON files are never touched; tests use their own retained directory.
    if (Test-Path -LiteralPath $saveProviderPath) { Remove-Item -LiteralPath $saveProviderPath -Recurse -Force }
    if ($hadSave) {
        & reg.exe import $saveSnapshot | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ('Preferences restore failed; recover from ' + $saveSnapshot) }
    }
    Write-Output 'Greyspire editor preferences restored; journey saves were isolated.'
}
if ($Build) { Invoke-CheckedUnity 'BuildGame.Windows' (Join-Path $testRoot 'Logs\feedback-build.log') $true }
