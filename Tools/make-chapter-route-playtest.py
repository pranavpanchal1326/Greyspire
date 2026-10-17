"""Create a new isolated floor-2 preview; never overwrite a prior playtest journey."""
import base64
import hashlib
import json
import uuid
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
kit = root / 'Playtests' / ('phase-nine-route-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
journey = kit / 'journey'
journey.mkdir(parents=True, exist_ok=False)
campaign = uuid.uuid4().hex
values = {'objectiveVersion': 1, 'pilotVersion': 1, 'chapterVersion': 1,
          'productionSeed': 734912, 'seedPolicy': 1, 'level': 2, 'xp': 0, 'statPoints': 3,
          'bestFloor': 1, 'towerFloor': 1, 'storyFloor': 1, 'sparks': 100, 'intro': 1,
          'tipSparks': 1, 'tipForge': 1, 'qa:phase9:route': 1}
payload = json.dumps({'version': 2, 'campaign': campaign,
                      'entries': [{'key': key, 'value': str(value)} for key, value in sorted(values.items())]}, separators=(',', ':'))
checksum = base64.b64encode(hashlib.sha256(payload.encode('utf-8')).digest()).decode('ascii')
envelope = json.dumps({'payload': payload, 'checksum': checksum}, separators=(',', ':'))
for filename in ('journey.json', 'journey.json.bak'):
    (journey / filename).write_text(envelope, encoding='utf-8')
(journey / 'journey.json.campaign').write_text(campaign, encoding='utf-8')
(kit / 'Play-Millrace.cmd').write_text('@echo off\npwsh -NoProfile -File "%~dp0Run.ps1"\n', encoding='ascii')
(kit / 'Run.ps1').write_text('''$ErrorActionPreference='Stop'
$gameRoot='D:\\game'
$acceptance=Join-Path $gameRoot 'Reports\\phase-nine-route-acceptance.json'
if(-not(Test-Path -LiteralPath $acceptance)){throw 'The Millrace candidate has not finished verification yet.'}
$record=Get-Content -LiteralPath $acceptance -Raw | ConvertFrom-Json
if($record.technical_result -ne 'PASS'){throw 'Wait for the verified Millrace build.'}
$dll=Join-Path $gameRoot 'Builds\\Greyspire\\Greyspire_Data\\Managed\\Assembly-CSharp.dll'
if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $record.runtime_dll_sha256){throw 'The player differs from the verified Millrace candidate.'}
if(Get-Process Greyspire -ErrorAction SilentlyContinue){throw 'Close the other Greyspire player first.'}
& (Join-Path $gameRoot 'Builds\\Greyspire\\Greyspire.exe') '-greyspire-save-dir' (Join-Path $PSScriptRoot 'journey') '-logFile' (Join-Path $PSScriptRoot 'player.log') '-screen-fullscreen' '0' '-screen-width' '1600' '-screen-height' '900'
''', encoding='utf-8')
(kit / 'README.md').write_text('''# The Millrace — phase-nine route preview

Double-click `Play-Millrace.cmd`, then choose Continue. Enter the tower from town to arrive on floor 2. The launcher requires the verified route-package build and checks its runtime hash.

This is a synthetic level-2 journey with floor 1 already cleared, 100 Sparks and 3 unspent stat points. No mastery node, service-gate receipt or chest reward is granted. It is a route preview, not a fresh-player chapter acceptance test. Its journey stays in this folder; normal camera/audio preferences remain shared with the game.

Read the route board near the entrance. Walk straight up the ramps onto the bridge, then try the gold trail down on the banks. The winch west of the underpass permanently lifts its gate. One chest is beside the upper bridge, one beside the lower bank; each retains its original ten carried Sparks. The lower spring restores two hearts once per visit. All monsters must be defeated before the north stairs open.

Try returning to town and continuing: the service gate and opened chest receipts should remain. The game keeps the floor-3 courier/mastery pilot and later progression. The full phase-nine chapter, including its Guardian/combat/assets/accessibility packages and fresh-player review, remains in progress.
''', encoding='utf-8')
manifest = {'directory': str(kit), 'preset': 'synthetic level-2 entry before floor 2',
            'scope': 'W01/W02 route preview; full phase-nine acceptance remains open',
            'launch_condition': 'Current runtime DLL matches a route-package technical PASS report'}
(root / 'Reports/phase-nine-route-playtest-kit.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2))
