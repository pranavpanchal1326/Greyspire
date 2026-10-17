param([switch]$Build, [ValidateSet("save","contact","feel","terrain","growth","objective","feedback","story","nav")][string]$StartAt="save", [ValidatePattern('^[a-z0-9-]+$')][string]$LogPrefix="phase-seven")
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Set-Location -LiteralPath $testRoot
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before verification.' }
$cases = @(
    @{ Name='save'; File='savetest.ps1'; Args=@() },
    @{ Name='contact'; File='contacttest.ps1'; Args=@('-Visual') },
    @{ Name='feel'; File='feeltest.ps1'; Args=@() },
    @{ Name='terrain'; File='terraintest.ps1'; Args=@('-Visual') },
    @{ Name='growth'; File='growthtest.ps1'; Args=@('-Visual') },
    @{ Name='objective'; File='objectivetest.ps1'; Args=@('-Visual') },
    @{ Name='feedback'; File='feedbacktest.ps1'; Args=@('-Visual') },
    @{ Name='story'; File='scenariotest.ps1'; Args=@('-Scenario','Story') },
    @{ Name='nav'; File='scenariotest.ps1'; Args=@('-Scenario','Nav') }
)
$starting = $false
foreach ($case in $cases) {
    if ($case.Name -eq $StartAt) { $starting = $true }
    if (-not $starting) { continue }
    $caseLog = Join-Path $testRoot ('Logs\' + $LogPrefix + '-' + $case.Name + '-runner.log')
    $caseArgs = $case.Args
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot $case.File) @caseArgs *> $caseLog
    if ($LASTEXITCODE -ne 0) { throw ('Regression failed: ' + $case.Name + '; inspect ' + $caseLog) }
    Write-Output ('VERIFIED ' + $case.Name)
}
& python (Join-Path $PSScriptRoot 'check-production.py')
if ($LASTEXITCODE -ne 0) { throw 'Export evidence validation failed.' }
if ($Build) {
    $buildLog = Join-Path $testRoot 'Logs\production-build.log'
    $editor = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
    $buildProcess = Start-Process -FilePath $editor -ArgumentList @('-batchmode','-quit','-projectPath',('"'+$testRoot+'"'),'-executeMethod','BuildGame.Windows','-logFile',('"'+$buildLog+'"')) -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddMinutes(10)
        while (-not $buildProcess.HasExited) {
            if ((Get-Date) -gt $deadline) { throw 'Windows build timed out.' }
            Start-Sleep -Seconds 2; $buildProcess.Refresh()
        }
        if ($buildProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $buildLog -Pattern '^BUILD Succeeded' -Quiet)) { throw 'Windows build failed; inspect production-build.log.' }
        Select-String -LiteralPath $buildLog -Pattern '^BUILD ' | ForEach-Object { $_.Line }
    }
    finally { if (-not $buildProcess.HasExited) { Stop-Process -Id $buildProcess.Id; $buildProcess.WaitForExit() } }
}
Write-Output 'PRODUCTION REGRESSION DONE'
