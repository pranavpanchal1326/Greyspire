param([switch]$Build, [switch]$Visual, [ValidateRange(60,900)][int]$StressSeconds=60)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$testLog = Join-Path $testRoot 'Logs\production.log'
$testSave = Join-Path $testRoot ('Logs\test-saves\production-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$saveKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveProviderPath = 'HKCU:\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveSnapshot = Join-Path $testRoot ('Backups\production-registry-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.reg')
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close the Unity editor before running the isolated production check.' }
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
        '-greyspire-save-dir', ('"' + $testSave + '"'), '-greyspire-stress-seconds', $StressSeconds.ToString(), '-logFile', ('"' + $log + '"'))
    if ($quit -or -not $Visual) { $arguments += '-batchmode' }
    if ($quit) { $arguments += '-quit' }
    $testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(360+$StressSeconds)
        while (-not $testProcess.HasExited) {
            if ((Get-Date) -gt $deadline) { throw 'Unity production check timed out.' }
            if ((Test-Path -LiteralPath $log) -and (Select-String -LiteralPath $log -Pattern 'error CS\d+' -Quiet)) { throw 'Unity script compilation failed; inspect the production log.' }
            Start-Sleep -Seconds 2
            $testProcess.Refresh()
        }
        Get-Content -LiteralPath $log | Select-String -Pattern '^PRODUCTION (DONE|FAILED)|^BUILD |error CS|Exception:' | ForEach-Object { $_.Line }
        if ($testProcess.ExitCode -ne 0) { throw ('Unity exited with code ' + $testProcess.ExitCode) }
    }
    finally {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id; $testProcess.WaitForExit() }
    }
}
try {
    Invoke-CheckedUnity 'ProductionTest.Run' $testLog $false
    if (-not (Select-String -LiteralPath $testLog -Pattern '^PRODUCTION DONE' -Quiet)) { throw 'The production check did not report completion.' }
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
if ($Build) { Invoke-CheckedUnity 'BuildGame.Windows' (Join-Path $testRoot 'Logs\production-build.log') $true }

