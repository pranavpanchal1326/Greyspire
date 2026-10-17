param([string]$Label = 'candidate', [switch]$Build, [switch]$Visual)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$testLog = Join-Path $testRoot 'Logs\environment.log'
$testSave = Join-Path $testRoot ('Logs\test-saves\environment-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$saveKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveProviderPath = 'HKCU:\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
$saveSnapshot = Join-Path $testRoot ('Backups\environment-registry-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.reg')
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.ToLower().Contains($testRoot.ToLower().TrimEnd('\')) }) { throw 'Close the Unity editor before running the isolated environment capture.' }
New-Item -ItemType Directory -Path (Join-Path $testRoot 'Backups'), $testSave -Force | Out-Null
$hadSave = Test-Path -LiteralPath $saveProviderPath
if ($hadSave) {
    & reg.exe export $saveKey $saveSnapshot /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot editor preferences; the test was not started.' }
}
function Invoke-CheckedUnity([string]$method, [string]$log, [bool]$quit) {
    $arguments = @('-projectPath', ('"' + $testRoot + '"'), '-executeMethod', $method,
        '-greyspire-save-dir', ('"' + $testSave + '"'), '-logFile', ('"' + $log + '"'), '-greyspire-env-label', $Label)
    if ($quit -or -not $Visual) { $arguments += '-batchmode' }
    if ($quit) { $arguments += '-quit' }
    $testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddMinutes(6)
        while (-not $testProcess.HasExited) {
            if ((Get-Date) -gt $deadline) { throw 'Unity environment capture timed out.' }
            Start-Sleep -Seconds 2
            $testProcess.Refresh()
        }
        Get-Content -LiteralPath $log | Select-String -Pattern 'ENVIRONMENT |BUILD |error CS|Exception:' | ForEach-Object { $_.Line }
        if ($testProcess.ExitCode -ne 0) { throw ('Unity exited with code ' + $testProcess.ExitCode) }
    }
    finally {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id; $testProcess.WaitForExit() }
    }
}
try {
    Invoke-CheckedUnity 'EnvironmentShowcase.Run' $testLog $false
    if (-not (Select-String -LiteralPath $testLog -Pattern '^ENVIRONMENT DONE' -Quiet)) { throw 'The environment capture did not report completion.' }
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
if ($Build) { Invoke-CheckedUnity 'BuildGame.Windows' (Join-Path $testRoot 'Logs\environment-build.log') $true }
