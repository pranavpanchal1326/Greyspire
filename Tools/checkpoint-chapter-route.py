"""Archive a verified G8 route package; this does not close the chapter gate."""
import csv
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
report = json.loads((root / 'Reports/phase-nine-route-acceptance.json').read_text(encoding='utf-8'))
if report['technical_result'] != 'PASS' or report['phase_nine_complete']:
    raise SystemExit('Expected a technically accepted route package with the chapter gate still open.')
start = json.loads((root / 'Reports/phase-nine-route-verification-start.json').read_text(encoding='utf-8'))


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


files = {Path(relative) for relative in start['runtime_inputs']}
for relative, expected in start['runtime_inputs'].items():
    if digest(root / relative) != expected:
        raise SystemExit('Candidate input changed: ' + relative)
runtime = Path('Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll')
if digest(root / runtime) != report['runtime_dll_sha256']:
    raise SystemExit('Delivered DLL differs from accepted route candidate.')
files.add(runtime)
files.update(path.relative_to(root) for path in root.glob('GREYSPIRE*.md'))
files.add(Path('CREDITS.md'))
for directory in ('Tools', 'Reports'):
    files.update(path.relative_to(root) for path in (root / directory).iterdir() if path.is_file())
files.update(path.relative_to(root) for path in (root / 'Reports').rglob('*') if path.is_file())
for name in ('chapter-route.log', 'pilot.log', 'production.log', 'production-build.log', 'save-safety.log',
             'contact.log', 'feel.log', 'terrain.log', 'growth.log', 'objective.log', 'feedback.log',
             'terrain-story.log', 'terrain-nav.log', 'feedback-reduced.png', 'feedback-map.png', 'terrain-course.png'):
    files.add(Path('Logs') / name)
files.add(Path('Logs/feel/town-camera.png'))
for pattern in ('phase-nine-*.log', 'chapter-route-*.png', 'pilot-*.png'):
    files.update(path.relative_to(root) for path in (root / 'Logs').glob(pattern) if path.is_file())
for prefix in ('chapter-route-', 'pilot-', 'production-', 'player-smoke-'):
    proof = max((root / 'Logs/test-saves').glob(prefix + '*'), key=lambda path: path.stat().st_mtime)
    files.update(path.relative_to(root) for path in proof.iterdir() if path.is_file())
kit = Path(json.loads((root / 'Reports/phase-nine-route-playtest-kit.json').read_text(encoding='utf-8'))['directory'])
files.update(path.relative_to(root) for path in kit.iterdir() if path.is_file())
missing = sorted(str(path) for path in files if not (root / path).is_file())
if missing:
    raise SystemExit('Checkpoint input missing: ' + ', '.join(missing))
target = root / 'Backups' / ('phase-nine-route-verified-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
target.mkdir(parents=True, exist_ok=False)
for relative in sorted(files):
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, destination)
(target / 'CHECKPOINT.md').write_text(
    '# Phase-nine route-package checkpoint\n\n'
    'G8 is open. This archives the exact technically verified floor-2 route candidate, '
    'its prior-gate regressions and delivered Windows runtime DLL, using repository-relative paths. '
    'It is not a complete chapter acceptance. Use with repository assets and the player under Builds/Greyspire. '
    'Logs/test-saves contains isolated automated fixtures. Human working journeys stay live in Playtests. '
    'SHA256.csv covers every archive file except itself.\n\n'
    f'Technical checks: {report["total_checks"]}; phase nine complete: false; phase ten allowed: false.\n', encoding='utf-8')
rows = [{'Path': path.relative_to(target).as_posix(), 'SHA256': digest(path), 'Bytes': path.stat().st_size}
        for path in sorted(target.rglob('*')) if path.is_file()]
with (target / 'SHA256.csv').open('w', encoding='utf-8', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=('Path', 'SHA256', 'Bytes'))
    writer.writeheader()
    writer.writerows(rows)
summary = {'checkpoint': str(target), 'files': len(rows), 'manifest_sha256': digest(target / 'SHA256.csv'),
           'technical_result': 'PASS', 'phase_nine_complete': False, 'phase_ten_allowed': False}
(root / 'Logs/phase-nine-checkpoint-created.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps(summary, indent=2))
