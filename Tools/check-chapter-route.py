"""Verify the first G8 route package without claiming the full chapter gate."""
import argparse
import csv
import hashlib
import importlib.util
import json
import re
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('pilot_verifier_inputs', root / 'Tools/check-pilot.py')
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)
start_path = root / 'Reports/phase-nine-route-verification-start.json'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--start', action='store_true')
    args = parser.parse_args()
    if args.start:
        start_path.write_text(json.dumps({'started_at': datetime.now().astimezone().isoformat(),
                                         'runtime_inputs': baseline.inputs()}, indent=2) + '\n', encoding='utf-8')
        print('Captured final G8 route candidate inputs')
        return
    start = json.loads(start_path.read_text(encoding='utf-8'))
    since = datetime.fromisoformat(start['started_at']).timestamp()
    failures, checks = [], {}

    def require(ok, message):
        if not ok:
            failures.append(message)

    def read(relative):
        path = root / relative
        require(path.is_file(), 'Missing evidence: ' + relative)
        if not path.is_file():
            return ''
        require(path.stat().st_mtime >= since, 'Stale evidence: ' + relative)
        return path.read_text(encoding='utf-8-sig', errors='replace')

    current = baseline.inputs()
    require(current == start['runtime_inputs'], 'Candidate inputs changed after verification started')
    for name, (filename, pattern, expected) in {
        'save': ('save-safety.log', r'^SAVE DONE (\d+) checks passed', 103),
        'contact': ('contact.log', r'^CONTACT DONE (\d+) checks passed', 65),
        'movement': ('feel.log', r'^FEEL DONE checks=(\d+)', 29),
        'terrain': ('terrain.log', r'^TERRAIN DONE (\d+) checks passed', 58),
        'growth': ('growth.log', r'^GROWTH DONE (\d+) checks passed', 194),
        'objective': ('objective.log', r'^OBJECTIVE DONE (\d+) checks passed', 120),
        'feedback': ('feedback.log', r'^FEEDBACK DONE (\d+) checks passed', 178),
        'production': ('production.log', r'^PRODUCTION DONE (\d+) checks passed', 201),
        'pilot': ('pilot.log', r'^PILOT DONE (\d+) checks passed', 102),
        'chapter_route': ('chapter-route.log', r'^CHAPTER DONE (\d+) checks passed', 33)
    }.items():
        log = read('Logs/' + filename)
        found = re.findall(pattern, log, re.MULTILINE)
        require(len(found) == 1 and int(found[0]) == expected, f'{name}: expected {expected} passing checks')
        require(not re.search(r'error CS\d+|^\w+ FAILED', log, re.MULTILINE), name + ': failure in final log')
        checks[name] = int(found[0]) if len(found) == 1 else 0
    for name in ('story', 'nav'):
        require(bool(re.search('^' + name.upper() + r' DONE\b', read('Logs/terrain-' + name + '.log'), re.MULTILINE)),
                name + ': scenario did not complete')
    for name, marker in (('route', 'CHAPTER DONE'), ('pilot', 'PILOT DONE'), ('production', 'PRODUCTION DONE'),
                         ('regression', 'PRODUCTION REGRESSION DONE')):
        require(marker in read(f'Logs/phase-nine-{name}-runner.log'), 'Incomplete runner: ' + name)
    build = read('Logs/production-build.log')
    build_match = re.search(r'^BUILD Succeeded\s+size=(\d+) MB\s+errors=0\s+warnings=(\d+)', build, re.MULTILINE)
    require(bool(build_match) and int(build_match[2]) == 0, 'Final Windows build must succeed with zero errors and warnings')
    dll = root / 'Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll'
    require(dll.is_file() and dll.stat().st_mtime >= since, 'Delivered DLL predates final verification')
    table = list(csv.DictReader(read('Reports/chapter-route-evidence.csv').splitlines()))
    require(len(table) == 14 and len({(r['kind'], r['identity']) for r in table}) == 14
            and all(r['result'] == 'PASS' for r in table), 'Missing, duplicate or failed chapter route evidence')
    values = {(r['kind'], r['identity']): float(r['value']) for r in table}
    require({r['identity'] for r in table if r['kind'] == 'route'} ==
            {f'{route}-{body}' for route in ('upper', 'lower') for body in ('0.35', '0.6', '1.25')}, 'Both levels need all three navigation profiles')
    require(all(10 < values.get(('walk', route), 0) < 80 for route in ('upper', 'lower')), 'Missing physical baseline traversals')
    require(values.get(('shortcut', 'closed'), 0) > 30 and values.get(('shortcut', 'open'), 100) < 18, 'Missing real shortcut path improvement')
    require(len({r['value'] for r in table if r['kind'] == 'lifecycle'}) == 1, 'Reconstruction accumulates owned assets')
    for relative in [f'Logs/chapter-route-{name}.png' for name in ('overview', 'upper', 'lower')] + [
            f'Logs/pilot-{name}.png' for name in ('floor', 'mastery', 'event', 'callback', 'training')]:
        path = root / relative
        require(path.is_file() and path.stat().st_mtime >= since and path.stat().st_size > 1024, 'Missing fresh capture: ' + relative)
    production = json.loads(read('Reports/production-evidence-check.json') or '{}')
    stress = next(csv.DictReader(read('Reports/production-stress.csv').splitlines()), {})
    require(production.get('result') == 'PASS' and production.get('rows') == 1769 and production.get('transitions') == 18
            and production.get('events', {}).get('stress_probe') == 1200, 'Incomplete independent production evidence')
    require(float(stress.get('duration_seconds', 0)) >= 600, 'Sustained fixture must run at least ten minutes')
    # Pilot evidence must still demonstrate its two exact consequences and baseline routes.
    pilot = list(csv.DictReader(read('Reports/pilot-evidence.csv').splitlines()))
    pv = {(r['kind'], r['identity']): float(r['value']) for r in pilot}
    require(len(pilot) == 18 and len(pv) == 18 and all(r['result'] == 'PASS' for r in pilot), 'Pilot evidence is incomplete')
    require(pv.get(('choice', 'courier-1')) == 20 and pv.get(('choice', 'courier-2')) == 24
            and pv.get(('comfort', 'reduced-effects')) == 1, 'Pilot reward or comfort policy regressed')
    require(all(10 < pv.get(('walk', route), 0) < 75 for route in ('road', 'ridge')), 'Pilot physical traversal evidence missing')
    report = {
        'phase': 9, 'gate': 'G8', 'package': 'W01/W02 floor-2 route',
        'checked_at': datetime.now().astimezone().isoformat(),
        'result': 'FAIL' if failures else 'PACKAGE_TECHNICAL_PASS_CHAPTER_OPEN',
        'technical_result': 'FAIL' if failures else 'PASS', 'checks': checks, 'total_checks': sum(checks.values()),
        'runtime_input_files': len(current), 'runtime_dll_sha256': baseline.digest(dll),
        'runtime_dll_bytes': dll.stat().st_size,
        'build': {'result': 'Succeeded' if build_match else 'Unconfirmed',
                  'size_mb': int(build_match[1]) if build_match else None,
                  'errors': 0 if build_match else None,
                  'warnings': int(build_match[2]) if build_match else None},
        'stress': stress, 'route_evidence_rows': len(table), 'errors': failures,
        'phase_nine_started': True, 'phase_nine_complete': False, 'phase_ten_allowed': False,
        'remaining_acceptance': 'Full P1 chapter including motion/assets, combat/Guardian, companions, presentation, information/rebinding/onboarding and fresh-player Q01 review. Phase-eight human acceptance is historical and does not certify this chapter candidate.'
    }
    (root / 'Reports/phase-nine-route-acceptance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: report[key] for key in ('result', 'total_checks', 'runtime_input_files', 'errors')}, indent=2))
    raise SystemExit(bool(failures))


if __name__ == '__main__':
    main()
