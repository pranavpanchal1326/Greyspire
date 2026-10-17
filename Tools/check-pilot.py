"""Verify the phase-eight candidate; hold G8 until a human has played this build."""
import argparse
import csv
import hashlib
import json
import re
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
start_path = root / 'Reports/phase-eight-verification-start.json'
prefixes = {'Scripts': 'Assets/Scripts', 'Editor': 'Assets/Editor',
            'Resources': 'Assets/Resources', 'Scene': 'Assets/Scenes'}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def inputs():
    files = set()
    with (root / 'Backups/phase-seven-verified-20261015/SHA256.csv').open(encoding='utf-8-sig', newline='') as stream:
        for row in csv.DictReader(stream):
            relative = Path(row['Path'])
            if relative.parts[0] in {*prefixes, 'Packages', 'ProjectSettings'}:
                files.add((Path(prefixes.get(relative.parts[0], relative.parts[0])) / Path(*relative.parts[1:])).as_posix())
    for directory in ('Assets/Scripts', 'Assets/Editor'):
        files.update(path.relative_to(root).as_posix() for path in (root / directory).rglob('*') if path.is_file())
    return {relative: digest(root / relative) for relative in sorted(files)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--start', action='store_true')
    args = parser.parse_args()
    if args.start:
        record = {'started_at': datetime.now().astimezone().isoformat(), 'runtime_inputs': inputs()}
        start_path.write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')
        print(f'Captured {len(record["runtime_inputs"])} final candidate inputs before verification')
        return
    start = json.loads(start_path.read_text(encoding='utf-8'))
    since = datetime.fromisoformat(start['started_at']).timestamp()
    failures = []

    def require(condition, message):
        if not condition:
            failures.append(message)

    def read(relative):
        path = root / relative
        require(path.is_file(), 'Missing evidence: ' + relative)
        if not path.is_file():
            return ''
        require(path.stat().st_mtime >= since, 'Stale evidence: ' + relative)
        return path.read_text(encoding='utf-8-sig', errors='replace')

    current = inputs()
    require(current == start['runtime_inputs'], 'Runtime or editor inputs changed after the verification start')
    checks = {}
    specifications = {
        'save': ('save-safety.log', r'^SAVE DONE (\d+) checks passed', 103),
        'contact': ('contact.log', r'^CONTACT DONE (\d+) checks passed', 65),
        'movement': ('feel.log', r'^FEEL DONE checks=(\d+)', 29),
        'terrain': ('terrain.log', r'^TERRAIN DONE (\d+) checks passed', 58),
        'growth': ('growth.log', r'^GROWTH DONE (\d+) checks passed', 194),
        'objective': ('objective.log', r'^OBJECTIVE DONE (\d+) checks passed', 120),
        'feedback': ('feedback.log', r'^FEEDBACK DONE (\d+) checks passed', 178),
        'production': ('production.log', r'^PRODUCTION DONE (\d+) checks passed', 201),
        'pilot': ('pilot.log', r'^PILOT DONE (\d+) checks passed', 102),
    }
    for name, (filename, pattern, expected) in specifications.items():
        log = read('Logs/' + filename)
        matches = re.findall(pattern, log, re.MULTILINE)
        require(len(matches) == 1 and int(matches[0]) == expected, f'{name}: expected {expected} passing checks')
        require(not re.search(r'error CS\d+|^\w+ FAILED', log, re.MULTILINE), name + ': failure in final log')
        checks[name] = int(matches[0]) if len(matches) == 1 else 0
    for name in ('story', 'nav'):
        log = read('Logs/terrain-' + name + '.log')
        require(bool(re.search('^' + name.upper() + r' DONE\b', log, re.MULTILINE)), name + ': scenario did not finish')
    for relative, marker in (
        ('Logs/phase-eight-pilot-runner.log', 'PILOT DONE'),
        ('Logs/phase-eight-production-runner.log', 'PRODUCTION DONE'),
        ('Logs/phase-eight-regression-runner.log', 'PRODUCTION REGRESSION DONE'),
    ):
        require(marker in read(relative), 'Incomplete runner: ' + relative)
    build = read('Logs/production-build.log')
    require(bool(re.search(r'^BUILD Succeeded\s+size=\d+ MB\s+errors=0\s+warnings=0', build, re.MULTILINE)),
            'Windows build did not finish with zero errors and warnings')
    dll = root / 'Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll'
    dll_hash = digest(dll)
    require(dll.stat().st_mtime >= since, 'Delivered runtime DLL predates final verification')
    table = list(csv.DictReader(read('Reports/pilot-evidence.csv').splitlines()))
    require(len(table) == 18 and len({(r['kind'], r['identity']) for r in table}) == 18
            and all(r['result'] == 'PASS' for r in table), 'Pilot evidence rows missing, duplicated or failed')
    values = {(r['kind'], r['identity']): float(r['value']) for r in table}
    require(values.get(('choice', 'courier-1')) == 20 and values.get(('choice', 'courier-2')) == 24,
            'The two courier branches did not retain their exact reward policies')
    require(values.get(('shortcut', 'open'), 100) < 24
            and values.get(('shortcut', 'closed'), 0) > 40, 'Shortcut does not change real navigation')
    require({r['identity'] for r in table if r['kind'] == 'route'} ==
            {f'{route}-{body}' for route in ('road', 'ridge') for body in ('0.35', '0.6', '1.25')},
            'Both routes were not validated for all body profiles')
    require(all(10 < values.get(('walk', route), 0) < 75 for route in ('road', 'ridge')),
            'Both routes were not traversed with baseline player movement')
    require(len({r['value'] for r in table if r['kind'] == 'lifecycle'}) == 1,
            'Repeated pilot/town construction accumulates owned resources')
    require(values.get(('comfort', 'reduced-effects')) == 1, 'Pilot reduced-effects evidence missing')
    for name in ('floor', 'mastery', 'event', 'callback', 'training'):
        image = root / f'Logs/pilot-{name}.png'
        require(image.is_file() and image.stat().st_mtime >= since and image.stat().st_size > 1024,
                'Missing fresh visual evidence: ' + name)
    production = json.loads(read('Reports/production-evidence-check.json') or '{}')
    stress = next(csv.DictReader(read('Reports/production-stress.csv').splitlines()), {})
    require(production.get('result') == 'PASS' and production.get('rows') == 1769
            and production.get('transitions') == 18 and production.get('events', {}).get('stress_probe') == 1200,
            'Independent production export validation did not confirm the complete soak')
    require(float(stress.get('duration_seconds', 0)) >= 600, 'Final stress run was shorter than ten minutes')
    human_path = root / 'Reports/phase-eight-human-playtest.json'
    human = json.loads(human_path.read_text(encoding='utf-8')) if human_path.is_file() else {}
    human_passed = human.get('result') == 'PASS' and human.get('runtime_dll_sha256') == dll_hash and bool(human.get('notes'))
    technical_passed = not failures
    report = {
        'phase': 8, 'gate': 'G7', 'checked_at': datetime.now().astimezone().isoformat(),
        'result': 'FAIL' if failures else 'PASS' if human_passed else 'TECHNICAL_PASS_HUMAN_PENDING',
        'technical_result': 'PASS' if technical_passed else 'FAIL', 'checks': checks,
        'total_checks': sum(checks.values()), 'runtime_input_files': len(current),
        'runtime_dll_sha256': dll_hash, 'runtime_dll_bytes': dll.stat().st_size,
        'build': {'result': 'Succeeded' if re.search(r'^BUILD Succeeded', build, re.MULTILINE) else 'Unconfirmed',
                  'final_errors': 0 if re.search(r'^BUILD Succeeded.*errors=0', build, re.MULTILINE) else None,
                  'final_warnings': 0 if re.search(r'^BUILD Succeeded.*warnings=0', build, re.MULTILINE) else None,
                  'cold_compile': json.loads((root / 'Reports/phase-eight-cold-build.json').read_text(encoding='utf-8-sig'))
                      if (root / 'Reports/phase-eight-cold-build.json').is_file() else None},
        'stress': stress, 'pilot_evidence_rows': len(table), 'errors': failures,
        'human_playtest': human if human.get('runtime_dll_sha256') == dll_hash and human.get('result') in ('PASS', 'FAIL') else {'result': 'PENDING',
            'required_by': 'GREYSPIRE_UPGRADE_MASTERPLAN.md section 40',
            'question': 'Are both routes understandable, all three early builds usable, and the event tradeoff and later callback clear in this playable build?'},
        'phase_nine_allowed': technical_passed and human_passed,
        'phase_nine_started': False,
        'scope': 'Local technical fixtures, baseline traversal, controller simulation, reduced-effects captures, replay/stress and Windows build. Human comprehension and build viability require a real playtest; no hardware certification.'
    }
    (root / 'Reports/phase-eight-acceptance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: report[key] for key in ('result', 'total_checks', 'runtime_input_files', 'phase_nine_allowed', 'errors')}, indent=2))
    raise SystemExit(bool(failures))


if __name__ == '__main__':
    main()
