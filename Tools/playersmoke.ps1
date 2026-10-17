param([string]$Preset)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$player = Join-Path $testRoot 'Builds\Greyspire\Greyspire.exe'
$testLog = Join-Path $testRoot 'Logs\phase-seven-player-smoke.log'
$capture = Join-Path $testRoot 'Logs\phase-seven-player-smoke.png'
$testSave = Join-Path $testRoot ('Logs\test-saves\player-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$saveKey = 'HKCU\Software\DefaultCompany\Greyspire'
$saveProviderPath = 'HKCU:\Software\DefaultCompany\Greyspire'
$saveSnapshot = Join-Path $testRoot ('Backups\player-smoke-registry-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.reg')
if (Get-Process Unity,Greyspire -ErrorAction SilentlyContinue) { throw 'Close Unity and Greyspire before the isolated player smoke check.' }
if (-not (Test-Path -LiteralPath $player)) { throw 'Build the Windows player before the smoke check.' }
New-Item -ItemType Directory -Path $testSave -Force | Out-Null
if ($Preset) {
    $presetRoot = [IO.Path]::GetFullPath($Preset)
    foreach ($name in @('journey.json','journey.json.bak','journey.json.campaign')) {
        $source = Join-Path $presetRoot $name
        if (-not (Test-Path -LiteralPath $source)) { throw ('Preset file missing: ' + $source) }
        Copy-Item -LiteralPath $source -Destination (Join-Path $testSave $name)
    }
}
$hadSave = Test-Path -LiteralPath $saveProviderPath
if ($hadSave) {
    & reg.exe export $saveKey $saveSnapshot /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot player preferences; smoke check was not started.' }
}
$testProcess = $null
try {
    foreach ($file in @($testLog, $capture)) {
        if (Test-Path -LiteralPath $file) {
            Move-Item -LiteralPath $file -Destination ($file + '.previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        }
    }
    $arguments = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
        '-greyspire-save-dir', ('"' + $testSave + '"'), '-logFile', ('"' + $testLog + '"'),
        '-shot', ('"' + $capture + '"'), '5')
    $testProcess = Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $testRoot -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(90)
    while (-not $testProcess.HasExited) {
        if ((Get-Date) -gt $deadline) { throw 'Windows player smoke check timed out.' }
        Start-Sleep -Seconds 1
        $testProcess.Refresh()
    }
    if ($testProcess.ExitCode -ne 0) { throw ('Windows player exited with code ' + $testProcess.ExitCode) }
    if (Select-String -LiteralPath $testLog -Pattern 'Exception:|SAVE LOAD FAILED|error CS\d+|Assertion failed' -Quiet) { throw 'Windows player reported an error; inspect the retained log.' }
    $captured = (Test-Path -LiteralPath $capture) -and (Get-Item -LiteralPath $capture).Length -ge 1024
    if (-not $captured -and -not (Select-String -LiteralPath $testLog -SimpleMatch 'Failed to capture screen shot.' -Quiet)) {
        throw 'Windows player produced neither the requested capture nor its capture diagnostic.'
    }
    # The -shot coroutine is only scheduled after Game.Awake completes startup.
    # A hidden Windows window has no capturable backbuffer; report that separately
    # from startup health rather than claiming a player visual inspection.
    if ($captured) { Write-Output 'PLAYER CAPTURE PASS: rendered frame retained.' }
    else { Write-Output 'PLAYER CAPTURE UNAVAILABLE: hidden window has no capturable backbuffer; use the retained editor captures for visual evidence.' }
    Write-Output ('PLAYER SMOKE PASS: startup and clean exit; isolated save at ' + $testSave)
}
finally {
    if ($null -ne $testProcess -and -not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id; $testProcess.WaitForExit() }
    if (Test-Path -LiteralPath $saveProviderPath) { Remove-Item -LiteralPath $saveProviderPath -Recurse -Force }
    if ($hadSave) {
        & reg.exe import $saveSnapshot | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ('Player preferences restore failed; recover from ' + $saveSnapshot) }
    }
    Write-Output 'Greyspire player preferences restored; journey saves were isolated.'
}
