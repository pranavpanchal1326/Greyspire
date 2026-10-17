"""Verify a Greyspire source/evidence checkpoint and its corresponding live files."""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('checkpoint', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    checkpoint = args.checkpoint.resolve()
    manifest = checkpoint / 'SHA256.csv'
    prefixes = {'Scripts': 'Assets/Scripts', 'Editor': 'Assets/Editor',
                'Resources': 'Assets/Resources', 'Scene': 'Assets/Scenes'}
    errors, checked, live = [], 0, 0
    with manifest.open(encoding='utf-8-sig', newline='') as source:
        rows = list(csv.DictReader(source))
    seen = set()
    for row in rows:
        relative = Path(row['Path'])
        if relative.is_absolute() or '..' in relative.parts or relative in seen:
            errors.append(f'Invalid or duplicate manifest path: {relative}')
            continue
        seen.add(relative)
        candidates = [('checkpoint', checkpoint / relative)]
        if relative.name != 'CHECKPOINT.md':
            if relative.as_posix() == 'Assembly-CSharp.dll':
                target = root / 'Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll'
            elif relative.parts[0] == 'ProductionJourneyProof':
                # These files are copies of the retained, isolated acceptance journal.
                target = None
                for directory in sorted((root / 'Logs/test-saves').glob('production-*'), reverse=True):
                    candidate = directory.joinpath(*relative.parts[1:])
                    if candidate.is_file() and digest(candidate) == row['SHA256'].upper():
                        target = candidate
                        break
                if target is None:
                    errors.append(f'Live isolated journal missing or changed: {relative}')
            else:
                target = root / prefixes.get(relative.parts[0], relative.parts[0])
                target = target.joinpath(*relative.parts[1:])
            if target is not None:
                candidates.append(('live', target))
        for kind, path in candidates:
            if kind == 'checkpoint':
                checked += 1
            else:
                live += 1
            if not path.is_file():
                errors.append(f'{kind} missing: {path}')
            elif path.stat().st_size != int(row['Bytes']) or digest(path) != row['SHA256'].upper():
                errors.append(f'{kind} mismatch: {path}')
    report = {'checkpoint': str(checkpoint), 'manifest_sha256': digest(manifest),
              'checkpoint_files': checked, 'live_comparisons': live,
              'errors': errors, 'result': 'FAIL' if errors else 'PASS'}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
    raise SystemExit(bool(errors))


if __name__ == '__main__':
    main()
