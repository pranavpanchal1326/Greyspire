# Runs every phase-nine and earlier suite against an isolated mirror of the project, so the user's
# own editor can stay open on D:\game. Mirrors Assets, Packages, ProjectSettings, Tools and
# UserSettings (the editor search index; without it the editor logs an error the suites reject),
# patches the older runners to guard only their own project path and to use Windows PowerShell,
# then runs the suites in turn and prints one line per suite. Logs: <Mirror>\Logs\regress-<suite>.txt
#
#   powershell -ExecutionPolicy Bypass -File Tools/verify-phase-nine.ps1 [-Mirror D:\gs-mirror] [-Soak 600] [-Build]
#
# Frame-timing checks (terrain chase, feel gait, production keyboard movement, the soak's day
# boundary) have failed on battery power; run on mains for acceptance evidence.
param([string]$Mirror = 'D:\gs-mirror', [int]$Soak = 600, [switch]$Build)
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
foreach ($d in 'Assets', 'Packages', 'ProjectSettings', 'Tools', 'UserSettings')
{
    robocopy (Join-Path $source $d) (Join-Path $Mirror $d) /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS /NP /XF UnityLockfile | Out-Null
}
$tools = Join-Path $Mirror 'Tools'
$guard = 'Get-CimInstance Win32_Process -Filter "Name=''Unity.exe''" | Where-Object { $_.CommandLine -and $_.CommandLine.ToLower().Contains(''' + $Mirror.ToLower() + ''') }'
foreach ($f in Get-ChildItem $tools -Filter *.ps1)
{
    $t = [IO.File]::ReadAllText($f.FullName)
    $n = $t.Replace('Get-Process Unity -ErrorAction SilentlyContinue', $guard).Replace('& pwsh -NoProfile', '& powershell -NoProfile -ExecutionPolicy Bypass')
    if ($n -ne $t) { [IO.File]::WriteAllText($f.FullName, $n) }
}
$production = @('-Visual', '-StressSeconds', [string]$Soak)
if ($Build) { $production += '-Build' }
$cases = @(
    @{ Name = 'learning'; File = 'learningpathtest.ps1'; Args = @() },
    @{ Name = 'anim'; File = 'animationtest.ps1'; Args = @() },
    @{ Name = 'access'; File = 'accesscontrolstest.ps1'; Args = @() },
    @{ Name = 'envaudio';File = 'environmentaudiotest.ps1'; Args = @() },
    @{ Name = 'branch'; File = 'branchcompaniontest.ps1'; Args = @() },
    @{ Name = 'guardian'; File = 'guardiantest.ps1'; Args = @() },
    @{ Name = 'roles'; File = 'encounterroletest.ps1'; Args = @() },
    @{ Name = 'weapon'; File = 'weaponfamilytest.ps1'; Args = @() },
    @{ Name = 'save'; File = 'savetest.ps1'; Args = @() },
    @{ Name = 'contact'; File = 'contacttest.ps1'; Args = @('-Visual') },
    @{ Name = 'feel'; File = 'feeltest.ps1'; Args = @() },
    @{ Name = 'terrain'; File = 'terraintest.ps1'; Args = @('-Visual') },
    @{ Name = 'growth'; File = 'growthtest.ps1'; Args = @('-Visual') },
    @{ Name = 'objective'; File = 'objectivetest.ps1'; Args = @('-Visual') },
    @{ Name = 'feedback'; File = 'feedbacktest.ps1'; Args = @('-Visual') },
    @{ Name = 'story'; File = 'scenariotest.ps1'; Args = @('-Scenario', 'Story') },
    @{ Name = 'nav'; File = 'scenariotest.ps1'; Args = @('-Scenario', 'Nav') },
    @{ Name = 'pilot'; File = 'pilottest.ps1'; Args = @('-Visual') },
    @{ Name = 'route'; File = 'chapterroutetest.ps1'; Args = @('-Visual') },
    @{ Name = 'production'; File = 'productiontest.ps1'; Args = $production }
)
# Runners write harmless lines to stderr (reg.exe reports success there); never treat that as fatal.
$ErrorActionPreference = 'Continue'
$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
if ($battery -and $battery.BatteryStatus -eq 1) { 'warning: running on battery; timing checks may fail' }
foreach ($c in $cases)
{
    $log = Join-Path $Mirror ('Logs\regress-' + $c.Name + '.txt')
    $start = Get-Date
    $a = $c.Args
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools $c.File) @a *> $log
    '{0,-10} exit={1} {2:N0}s' -f $c.Name, $LASTEXITCODE, ((Get-Date) - $start).TotalSeconds
}
