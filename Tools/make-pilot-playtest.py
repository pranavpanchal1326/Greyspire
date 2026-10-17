"""Create two new, isolated synthetic saves for human review of the phase-eight pilot."""
import base64
import hashlib
import json
import uuid
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kit = root / 'Playtests' / ('phase-eight-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
kit.mkdir(parents=True, exist_ok=False)
for variant in ('A', 'B'):
    directory = kit / variant / 'journey'
    directory.mkdir(parents=True, exist_ok=False)
    campaign = uuid.uuid4().hex
    values = {'objectiveVersion': 1, 'pilotVersion': 1, 'productionSeed': 734912, 'seedPolicy': 1,
              'level': 4, 'xp': 0, 'statPoints': 9, 'bestFloor': 2, 'towerFloor': 2, 'storyFloor': 2,
              'sparks': 100, 'intro': 1, 'tipSparks': 1, 'tipForge': 1, 'qa:phase8': 1}
    snapshot = {'version': 2, 'campaign': campaign,
                'entries': [{'key': key, 'value': str(value)} for key, value in sorted(values.items())]}
    payload = json.dumps(snapshot, separators=(',', ':'))
    checksum = base64.b64encode(hashlib.sha256(payload.encode('utf-8')).digest()).decode('ascii')
    envelope = json.dumps({'payload': payload, 'checksum': checksum}, separators=(',', ':'))
    for filename in ('journey.json', 'journey.json.bak'):
        (directory / filename).write_text(envelope, encoding='utf-8')
    (directory / 'journey.json.campaign').write_text(campaign, encoding='utf-8')
    (kit / f'Play-{variant}.cmd').write_text(
        f'@echo off\npwsh -NoProfile -File "%~dp0Run.ps1" -Variant {variant}\n', encoding='ascii')

(kit / 'Run.ps1').write_text('''param([ValidateSet('A','B')][string]$Variant='A')
$ErrorActionPreference='Stop'
$gameRoot='D:\\game'
$acceptance=Join-Path $gameRoot 'Reports\\phase-eight-acceptance.json'
if(-not(Test-Path -LiteralPath $acceptance)){throw 'The final phase-eight candidate has not finished verification yet.'}
$record=Get-Content -LiteralPath $acceptance -Raw | ConvertFrom-Json
if($record.technical_result -ne 'PASS'){throw 'The phase-eight technical checks are not complete. Wait for the verified build.'}
$dll=Join-Path $gameRoot 'Builds\\Greyspire\\Greyspire_Data\\Managed\\Assembly-CSharp.dll'
if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $record.runtime_dll_sha256){throw 'The player differs from the verified candidate. Rebuild and recheck before this playtest.'}
if(Get-Process Greyspire -ErrorAction SilentlyContinue){throw 'Close the other Greyspire player first.'}
$save=Join-Path $PSScriptRoot ($Variant+'\\journey')
$log=Join-Path $PSScriptRoot ($Variant+'\\player.log')
& (Join-Path $gameRoot 'Builds\\Greyspire\\Greyspire.exe') '-greyspire-save-dir' $save '-logFile' $log '-screen-fullscreen' '0' '-screen-width' '1600' '-screen-height' '900'
''', encoding='utf-8')
(kit / 'README.md').write_text('''# Phase-eight human playtest

The launchers wait for the final technical acceptance report and verify the runtime DLL hash. They open two separate synthetic journeys at level 4, with floors 1–2 already cleared, 100 Sparks and 9 unspent stat points. No prior mastery, event decision or chest reward is granted. These presets are QA setup, not campaign accomplishments.

Double-click `Play-A.cmd` in Explorer, then choose Continue. Close the game before launching `Play-B.cmd`. Both journeys are stored inside this folder; normal audio/camera preferences remain shared with the game.

1. In town, approach the **Field desk** southeast of the central well. Preview the three mastery paths, learn one node, and enter practice. Try guarding late, dodging and countering, and a charged heavy. Practice grants no progression; its return marker takes you home.
2. Enter the tower. You arrive on **floor 3: Surveyor's Crossing**. Try the combat road east and the ramps west. Find the courier and overlook chest. The north stairs still require clearing the floor's monsters.
3. On journey A, choose **SEND IT HOME**: the cross-pass opens immediately; after clearing the floor, return to the town courier report for the one-time 20-Spark payment. On journey B, choose **KEEP THE FIELD KIT**: receive 24 banked Sparks immediately, open the west latch yourself, and read the different town report.
4. After floor 3, try the second mastery node and free town respec. Revisit the crossing from the Field desk: earned tower progress should stay put and opened chests should stay opened.

Please report whether the route directions were clear, the three early mastery paths were usable and distinct, and the event tradeoff and later response were understandable. Include anything confusing, obstructed or unrewarding. This human assessment is required by master-plan section 40 before phase nine; the automated fixtures do not establish enjoyment or build viability.
''', encoding='utf-8')
manifest = {'directory': str(kit), 'preset': 'synthetic level-4 entry before floor 3',
            'variants': ['A: send delivery home', 'B: keep field kit'], 'human_review': 'PENDING',
            'launch_condition': 'Current runtime DLL matches a phase-eight technical PASS report'}
(root / 'Reports/phase-eight-playtest-kit.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2))
