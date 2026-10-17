"""Archive an accepted phase-eight technical candidate without overwriting an older checkpoint."""
import csv
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
report = json.loads((root / 'Reports/phase-eight-acceptance.json').read_text(encoding='utf-8'))
if report['technical_result'] != 'PASS':
    raise SystemExit('Finish technical acceptance before checkpointing.')
start = json.loads((root / 'Reports/phase-eight-verification-start.json').read_text(encoding='utf-8'))
label = 'complete' if report['phase_nine_allowed'] else 'technical'
target = root / 'Backups' / ('phase-eight-' + label + '-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
if not target.resolve().is_relative_to((root / 'Backups').resolve()):
    raise SystemExit('Checkpoint path escaped Backups.')


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


files = set()
for relative, expected in start['runtime_inputs'].items():
    if digest(root / relative) != expected:
        raise SystemExit('Candidate input changed: ' + relative)
    files.add(Path(relative))
runtime = Path('Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll')
if digest(root / runtime) != report['runtime_dll_sha256']:
    raise SystemExit('Delivered DLL differs from acceptance.')
files.add(runtime)
files.update(path.relative_to(root) for path in root.glob('GREYSPIRE*.md'))
files.add(Path('CREDITS.md'))
files.update(path.relative_to(root) for path in (root / 'Tools').glob('*') if path.is_file())
files.update(path.relative_to(root) for path in (root / 'Reports').glob('*') if path.is_file())
log_names = ['pilot.log', 'production.log', 'production-build.log', 'save-safety.log', 'contact.log',
             'feel.log', 'terrain.log', 'growth.log', 'objective.log', 'feedback.log',
             'terrain-story.log', 'terrain-nav.log', 'feedback-reduced.png', 'feedback-map.png', 'terrain-course.png']
for name in log_names:
    files.add(Path('Logs') / name)
for pattern in ('phase-eight-*.log', 'pilot-*.png', 'pilot-*.log'):
    files.update(path.relative_to(root) for path in (root / 'Logs').glob(pattern) if path.is_file())
files.add(Path('Logs/feel/town-camera.png'))
for prefix in ('pilot-', 'production-'):
    proof = max((root / 'Logs/test-saves').glob(prefix + '*'), key=lambda path: path.stat().st_mtime)
    files.update(path.relative_to(root) for path in proof.iterdir() if path.is_file())
kit = Path(json.loads((root / 'Reports/phase-eight-playtest-kit.json').read_text(encoding='utf-8'))['directory'])
# Keep launch instructions and source, not human working journeys that will change during review.
files.update(path.relative_to(root) for path in kit.iterdir() if path.is_file())
missing = sorted(str(path) for path in files if not (root / path).is_file())
if missing:
    raise SystemExit('Checkpoint input missing: ' + ', '.join(missing))
target.mkdir(parents=True, exist_ok=False)
for relative in sorted(files):
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, destination)
(target / 'CHECKPOINT.md').write_text(
    '# Phase-eight ' + label + ' checkpoint\n\n'
    'Source/evidence checkpoint for the exact accepted Windows runtime. Paths retain repository '
    'locations. Use with the repository assets and full player under Builds/Greyspire. '
    'Logs/test-saves contains isolated automated fixture proof, never a player campaign. '
    'Playtest launchers are retained; human working saves remain in the live Playtests folder. '
    'SHA256.csv covers every checkpoint file except itself.\n\n'
    f'Technical checks: {report["total_checks"]}. Human acceptance: '
    + ('PASS' if report['phase_nine_allowed'] else 'PENDING; phase nine is held.') + '\n', encoding='utf-8')
rows = [{'Path': path.relative_to(target).as_posix(), 'SHA256': digest(path), 'Bytes': path.stat().st_size}
        for path in sorted(target.rglob('*')) if path.is_file()]
with (target / 'SHA256.csv').open('w', encoding='utf-8', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=('Path', 'SHA256', 'Bytes'))
    writer.writeheader()
    writer.writerows(rows)
summary = {'checkpoint': str(target), 'files': len(rows), 'manifest_sha256': digest(target / 'SHA256.csv'),
           'technical_result': 'PASS', 'phase_nine_allowed': report['phase_nine_allowed']}
(root / 'Logs/phase-eight-checkpoint-created.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps(summary, indent=2))
