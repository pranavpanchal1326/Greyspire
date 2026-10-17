param([ValidateSet('route','pilot','production','regression')][string]$StartAt='route')
$ErrorActionPreference='Stop'
$chapterRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Set-Location -LiteralPath $chapterRoot
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Close Unity before candidate verification.'}
if($StartAt -eq 'route'){
    & python (Join-Path $PSScriptRoot 'check-chapter-route.py') --start
    if($LASTEXITCODE -ne 0){throw 'Could not fingerprint candidate inputs.'}
}
$cases=@(
    @{Name='route';File='chapterroutetest.ps1';Args=@('-Visual')},
    @{Name='pilot';File='pilottest.ps1';Args=@('-Visual')},
    @{Name='production';File='productiontest.ps1';Args=@('-Visual','-StressSeconds','600')},
    @{Name='regression';File='productionverify.ps1';Args=@('-Build','-LogPrefix','phase-nine')}
)
$started=$false
foreach($case in $cases){
    if($case.Name -eq $StartAt){$started=$true}
    if(-not $started){continue}
    $runnerLog=Join-Path $chapterRoot ('Logs\phase-nine-'+$case.Name+'-runner.log')
    $caseArgs=$case.Args
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot $case.File) @caseArgs *> $runnerLog
    if($LASTEXITCODE -ne 0){throw ('Candidate verification failed: '+$case.Name+'; inspect '+$runnerLog)}
    Write-Output ('VERIFIED '+$case.Name)
}
& python (Join-Path $PSScriptRoot 'check-chapter-route.py')
if($LASTEXITCODE -ne 0){throw 'Final route-package evidence did not pass.'}
