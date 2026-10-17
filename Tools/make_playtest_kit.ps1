# Q01: builds the first-chapter playtest kit for fresh players.
#
#   powershell -ExecutionPolicy Bypass -File Tools/make_playtest_kit.ps1
#
# Writes Playtests\phase-nine-chapter-<stamp>\ with a launcher that refuses to run anything but the
# verified build (Reports\phase-nine-chapter-acceptance.json), a fresh journey per tester, the session
# recorder switched on, the facilitator's script and observation form, and Report.cmd, which turns
# the recorded sessions into Q01-findings.md (Tools\playtest_report.py).
param([string]$GameRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$kit = Join-Path $GameRoot ('Playtests\phase-nine-chapter-' + $stamp)
New-Item -ItemType Directory -Force -Path $kit, (Join-Path $kit 'sessions'), (Join-Path $kit 'notes'), (Join-Path $kit 'journeys') | Out-Null

$run = @'
$ErrorActionPreference = 'Stop'
$gameRoot = '__GAME__'
$acceptance = Join-Path $gameRoot 'Reports\phase-nine-chapter-acceptance.json'
if (-not (Test-Path -LiteralPath $acceptance)) { throw 'The chapter candidate has not finished verification yet.' }
$record = Get-Content -LiteralPath $acceptance -Raw | ConvertFrom-Json
if ($record.technical_result -ne 'PASS') { throw 'Wait for the verified chapter build.' }
$exe = Join-Path $gameRoot 'Builds\Greyspire\Greyspire.exe'
$dll = Join-Path $gameRoot 'Builds\Greyspire\Greyspire_Data\Managed\Assembly-CSharp.dll'
if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $record.runtime_dll_sha256) { throw 'The player differs from the verified chapter candidate.' }
if (Get-Process Greyspire -ErrorAction SilentlyContinue) { throw 'Close the other Greyspire player first.' }
$tester = Read-Host 'Tester name (first name or a code; used only to label this session)'
if ([string]::IsNullOrWhiteSpace($tester)) { $tester = 'tester' }
$safe = ($tester -replace '[^A-Za-z0-9_-]', '_')
$journey = Join-Path $PSScriptRoot ('journeys\' + $safe)
if (Test-Path -LiteralPath $journey) { throw ('A journey for ' + $safe + ' already exists. Each tester starts fresh: use a new name.') }
New-Item -ItemType Directory -Force -Path $journey | Out-Null
$note = Join-Path $PSScriptRoot ('notes\' + $safe + '.md')
if (-not (Test-Path -LiteralPath $note)) { Copy-Item (Join-Path $PSScriptRoot 'notes\TEMPLATE.md') $note }
Write-Host ('Session for ' + $safe + ' starting. Facilitator notes: ' + $note)
& $exe '-greyspire-save-dir' $journey '-playtest' $safe '-playtest-dir' (Join-Path $PSScriptRoot 'sessions') '-logFile' (Join-Path $journey 'player.log') '-screen-fullscreen' '0' '-screen-width' '1600' '-screen-height' '900'
'@
[IO.File]::WriteAllText((Join-Path $kit 'Run.ps1'), $run.Replace('__GAME__', $GameRoot))
[IO.File]::WriteAllText((Join-Path $kit 'Play.cmd'), "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Run.ps1`"`r`npause`r`n")
[IO.File]::WriteAllText((Join-Path $kit 'Report.cmd'), "@echo off`r`npython `"$GameRoot\Tools\playtest_report.py`" `"%~dp0.`"`r`nstart `"`" `"%~dp0Q01-findings.md`"`r`n")

$readme = @'
# Greyspire, chapter one: fresh-player playtest (Q01)

This is the playtest that closes phase nine. A person who has never played Greyspire plays the first
chapter, from a new journey in Brushwick to the floor-5 Guardian. You watch and write down what they
do, and you do not help. Plan on 60 to 75 minutes per tester.

## Before each session

1. Double-click **Play.cmd** and type the tester's name (or a code). Each tester gets a fresh journey;
   the launcher refuses a name that has already played.
2. Choose **New Game**. Leave every setting at its default unless the tester asks to change one;
   if they do, note what and why.
3. Open `notes\<name>.md` (the launcher copies the form there) and keep it beside you.

## During the session

- Ask them to think aloud: what they are looking at, what they expect, what confuses them.
- Do not explain the controls, the map, where to go, or what anything does. If they are stuck for
  three minutes, write down where and what they tried, then say only "What would you try next?".
  After ten minutes stuck, you may point the way: note that you did.
- Stop at the Gatekeeper's defeat, or at 75 minutes, whichever comes first.

## What to watch for (the form follows the same order)

1. **Brushwick, first minutes.** Do they find the tower without help? Do they read the objective line?
2. **Floor 1.** Do the tips teach movement, attack, dodge, guard? Do they understand the floor is cleared
   to open the stairs?
3. **Back in town.** Do they spend stat points? Notice the green NEXT line? Find the Field desk and
   the Guild? Open the journal (pause, JOURNAL)?
4. **Floor 2, the Millrace.** Do they find both routes? The winch and its gate?
5. **Floor 3, the courier.** Do they understand the choice and its cost?
6. **Floor 4, Rhea.** Do they understand the rescue choice? Mop's orders?
7. **Floor 5, the Gatekeeper.** Do they read his tells? Find the pillars? Understand the checkpoint
   after a defeat?
8. **Comfort.** Any motion discomfort, text too small, sound too loud, controls they wanted changed?

## After the session

Ask the four closing questions on the form and write the answers down as said.

## Turning sessions into findings

Double-click **Report.cmd**. It reads every recorded session in `sessions\` and every form in
`notes\` and writes **Q01-findings.md**: the chapter funnel, each session's timeline, where people
waited longest on one instruction, difficulty spikes, which helps they used, and ranked fix
candidates. Findings marked "observe" need your notes to say why before anything is changed.

Nothing in a session is sent anywhere: the recording is a local CSV in `sessions\`.
'@
[IO.File]::WriteAllText((Join-Path $kit 'README.md'), $readme)

$form = @'
# Observation form

Tester: ____   Date: ____   Facilitator: ____   Controller or keyboard: ____
Played games like this before? (never / a few / many): ____

For each stage write the time, what they did, what they said, and any moment they were stuck
(where, for how long, what they tried). Mark help you gave with **HELP**.

## 1. Brushwick, first minutes
- Found the tower unaided? (yes / after ___ min / with help)
- Read the objective line?

## 2. Floor 1
- Tips read and followed? Which were missed?
- Understood "clear the floor to open the stairs"?

## 3. Back in town
- Spent stat points? Saw the NEXT line? Found the Field desk / the Guild?
- Opened the journal?

## 4. Floor 2, the Millrace
- Routes found: upper / lower / both. Winch and gate found?

## 5. Floor 3, the courier
- Choice made and why? Understood the cost?

## 6. Floor 4, Rhea and Mop
- Choice made and why? Used Mop's orders?

## 7. Floor 5, the Gatekeeper
- Attempts: ___. Read the tells? Used the pillars? Understood the checkpoint retry?

## 8. Comfort and controls
- Anything uncomfortable, unreadable, too loud? Settings changed and why?

## Closing questions (write the answers as said)
1. What was the game asking you to do, in your own words?
2. Where were you most confused?
3. What did you enjoy most?
4. Would you keep playing? Why or why not?
'@
[IO.File]::WriteAllText((Join-Path $kit 'notes\TEMPLATE.md'), $form)

$record = [ordered]@{
    directory = $kit
    preset = 'fresh journey per tester (no synthetic preset)'
    scope = 'Q01 first-chapter fresh-player playtest: Brushwick to the floor-5 Guardian'
    launch_condition = 'Current runtime DLL matches Reports\phase-nine-chapter-acceptance.json (technical PASS)'
    human_review = 'PENDING'
}
$record | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $GameRoot 'Reports\phase-nine-chapter-playtest-kit.json')
Write-Output $kit
