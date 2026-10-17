"""Close G6 only with fresh regression, stress, build and source evidence."""
import argparse
import csv
import hashlib
import json
import re
from datetime import datetime
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--since', required=True, help='ISO timestamp from before the closure reruns')
    args = parser.parse_args()
    since = datetime.fromisoformat(args.since).timestamp()
    root = Path(__file__).resolve().parent.parent
    failures = []

    def require(ok, message):
        if not ok:
            failures.append(message)

    def read(relative):
        path = root / relative
        require(path.is_file(), f'Missing evidence: {relative}')
        if not path.is_file():
            return ''
        require(path.stat().st_mtime >= since, f'Stale evidence: {relative}')
        return path.read_text(encoding='utf-8-sig', errors='replace')

    baseline = json.loads((root / 'Reports/phase-seven-closure-checkpoint.json').read_text())
    require(baseline['result'] == 'PASS' and baseline['checkpoint_files'] == 589
            and baseline['live_comparisons'] == 588 and not baseline['errors'],
            'Initial phase-seven checkpoint did not match the live project')
    # Recheck source independently after Unity has completed every run. Logs and
    # measurements are expected to change; runtime inputs must remain identical.
    checkpoint = Path(baseline['checkpoint'])
    prefixes = {'Scripts': 'Assets/Scripts', 'Editor': 'Assets/Editor',
                'Resources': 'Assets/Resources', 'Scene': 'Assets/Scenes'}
    source_checks = 0
    with (checkpoint / 'SHA256.csv').open(encoding='utf-8-sig', newline='') as file:
        for row in csv.DictReader(file):
            relative = Path(row['Path'])
            head = relative.parts[0]
            if head not in {*prefixes, 'Packages', 'ProjectSettings'}:
                continue
            path = (root / prefixes.get(head, head)).joinpath(*relative.parts[1:])
            source_checks += 1
            if not path.is_file():
                require(False, f'Runtime input missing after verification: {relative}')
                continue
            with path.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest().upper()
            require(digest == row['SHA256'].upper(), f'Runtime input changed after checkpoint: {relative}')
    for folder, prefix in (('Assets/Scripts', 'Scripts'), ('Assets/Editor', 'Editor')):
        archived = {p.relative_to(checkpoint / prefix) for p in (checkpoint / prefix).rglob('*') if p.is_file()}
        current = {p.relative_to(root / folder) for p in (root / folder).rglob('*') if p.is_file()}
        require(current == archived, f'{folder} inventory differs from the checkpoint')

    specifications = {
        'save-safety': ('save-safety.log', r'^SAVE DONE (\d+) checks passed', 103),
        'contact': ('contact.log', r'^CONTACT DONE (\d+) checks passed', 65),
        'feel': ('feel.log', r'^FEEL DONE checks=(\d+)', 29),
        'terrain': ('terrain.log', r'^TERRAIN DONE (\d+) checks passed', 58),
        'growth': ('growth.log', r'^GROWTH DONE (\d+) checks passed', 194),
        'objective': ('objective.log', r'^OBJECTIVE DONE (\d+) checks passed', 120),
        'feedback': ('feedback.log', r'^FEEDBACK DONE (\d+) checks passed', 178),
        'production': ('production.log', r'^PRODUCTION DONE (\d+) checks passed', 201),
    }
    checks = {}
    for name, (filename, pattern, expected) in specifications.items():
        log = read('Logs/' + filename)
        matches = re.findall(pattern, log, flags=re.MULTILINE)
        require(len(matches) == 1 and int(matches[0]) == expected, f'{name}: expected {expected} passing checks')
        require(not re.search(r'error CS\d+|^\w+ FAILED', log, flags=re.MULTILINE), f'{name}: failure in log')
        checks[name] = int(matches[0]) if len(matches) == 1 else None
    for name in ('story', 'nav'):
        log = read(f'Logs/terrain-{name}.log')
        require(bool(re.search(r'^' + name.upper() + r' DONE\b', log, flags=re.MULTILINE)), f'{name}: scenario did not complete')
    runner = read('Logs/phase-seven-closure-regression.log')
    require('PRODUCTION REGRESSION DONE' in runner, 'Sequential regression runner did not finish')
    for name in ('save', 'contact', 'feel', 'terrain', 'growth', 'objective', 'feedback', 'story', 'nav'):
        require(bool(re.search(r'^VERIFIED ' + name + r'\s*$', runner, re.MULTILINE)), f'Runner did not verify {name}')
    build_log = read('Logs/production-build.log')
    require(bool(re.search(r'^BUILD Succeeded\s+size=\d+ MB\s+errors=0\s+warnings=0', build_log, re.MULTILINE)),
            'Windows build did not complete with zero errors and warnings')
    dll = root / 'Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll'
    with dll.open('rb') as stream:
        dll_hash = hashlib.file_digest(stream, 'sha256').hexdigest().upper()
    recorded = json.loads((root / 'Reports/phase-seven-acceptance.json').read_text(encoding='utf-8-sig'))
    require(dll_hash == recorded['build']['dll_sha256'],
            'Fresh runtime DLL differs from the delivered phase-seven runtime')
    player_log = read('Logs/phase-seven-player-smoke.log')
    require(not re.search(r'Exception:|SAVE LOAD FAILED|error CS\d+|Assertion failed', player_log),
            'Windows player startup reported an error')
    player_runner = read('Logs/phase-seven-player-smoke-runner.log')
    require('PLAYER SMOKE PASS:' in player_runner and 'player preferences restored' in player_runner,
            'Windows player smoke check or preference restoration did not finish')
    capture = root / 'Logs/phase-seven-player-smoke.png'
    captured = capture.is_file() and capture.stat().st_size > 1024 and capture.stat().st_mtime >= since
    require(captured or ('PLAYER CAPTURE UNAVAILABLE:' in player_runner and 'Failed to capture screen shot.' in player_log),
            'Windows player capture result is undisclosed')

    stress_text = read('Reports/production-stress.csv')
    stress = next(csv.DictReader(stress_text.splitlines()), {})
    require(float(stress.get('duration_seconds', 0)) >= 600, 'Fresh soak did not cover ten minutes')
    evidence = json.loads(read('Reports/production-evidence-check.json') or '{}')
    require(evidence.get('result') == 'PASS' and evidence.get('transitions') == 18
            and evidence.get('events', {}).get('stress_probe') == 1200,
            'Independent event/transition validation did not confirm the ten-minute run')
    report = {
        'phase': 7, 'gate': 'G6', 'result': 'FAIL' if failures else 'PASS',
        'checked_at': datetime.now().astimezone().isoformat(), 'fresh_since': args.since,
        'baseline_checkpoint': baseline, 'unchanged_runtime_input_files': source_checks,
        'checks': checks, 'total_checks': sum(n or 0 for n in checks.values()),
        'scenarios': ['both rescue branches and skip', 'tower route/follow/hunt'],
        'stress': stress, 'export_evidence': evidence,
        'runtime_dll_sha256': dll_hash, 'errors': failures,
        'windows_player_smoke': {'log': 'Logs/phase-seven-player-smoke.log',
                                 'capture': 'Logs/phase-seven-player-smoke.png' if captured else None,
                                 'capture_result': 'PASS' if captured else 'UNAVAILABLE: hidden window has no capturable backbuffer',
                                 'scope': 'Built player startup, clean exit and isolated save. Visual review uses fresh editor captures; no standalone player visual sign-off is claimed.'},
        'next_phase': 8, 'next_gate': 'G7', 'next_gate_deliverable': 'Branching pilot',
        'phase_eight_authorized_to_start': not failures,
        'scope': 'Technical acceptance for the recorded local fixtures and Windows build. Human campaign, listening, comfort and target-hardware certification remain open.'
    }
    target = root / 'Reports/phase-seven-closure-acceptance.json'
    target.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'result': report['result'], 'checks': report['total_checks'],
                      'unchanged_runtime_inputs': source_checks, 'errors': failures,
                      'report': str(target)}, indent=2))
    raise SystemExit(bool(failures))


if __name__ == '__main__':
    main()
