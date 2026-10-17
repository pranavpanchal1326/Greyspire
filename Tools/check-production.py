"""Validate exported acceptance evidence independently of Unity's assertion counter."""
import csv
import json
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parent.parent
def read(name):
    with (root / 'Reports' / name).open(encoding='utf-8-sig', newline='') as file:
        return list(csv.DictReader(file))

events = read('production-events.csv')
assert events and all(None not in row and all(value is not None for value in row.values()) for row in events)
assert len({row['event_id'] for row in events}) == len(events)
assert all(float(b['seconds']) >= float(a['seconds']) for a, b in zip(events, events[1:]))
assert all(row['build'] == 'greyspire-g8-w01-v1' and row['content'] == 'legacy30-chapter-v3' for row in events)
assert all(row['save_schema'] == '2' and row['seed_policy'] == '1' and row['root_seed'] == '734912' for row in events)
counts = Counter(row['event'] for row in events)
assert counts['batch_probe'] == 512 and counts['stress_probe'] >= 120
assert all(row['note'] == 'Unicode café, "quoted"\nsecond line' for row in events if row['event'] == 'batch_probe')
assert counts['kill_reward'] == 2 and counts['player_defeat'] == 1 and counts['despawn'] == 3
contacts = [row for row in events if row['event'] == 'contact' and row['target'].startswith('player:')]
assert [(row['outcome'], float(row['value'])) for row in contacts] == [('Revived', 1), ('Killed', 1)]
assert next(float(row['value']) for row in events if row['event'] == 'summary_kills') == 2
assert next(float(row['value']) for row in events if row['event'] == 'summary_hits_taken') == 2
assert next(float(row['value']) for row in events if row['event'] == 'summary_falls') == 1
assert events[-1]['event'] == 'session_end' and events[-1]['note'] == 'dropped=0'
transitions = read('production-transitions.csv')
assert len(transitions) == 18
for scope in ('town', 'floor', 'wilds'):
    same = [row for row in transitions if row['scope'] == scope and int(row['cycle']) >= 1]
    assert len({row['owned_assets'] for row in same}) == 1
    assert len({row['meshes'] for row in same}) == 1
    assert len({row['materials'] for row in same}) == 1
stress = read('production-stress.csv')[0]
assert float(stress['duration_seconds']) >= 60 and int(stress['frames']) > 100
assert int(stress['dash_slots']) <= 192 and stress['world_voice_capacity'] == '12'
floors = read('floor-definitions.csv')
assert len(floors) == len({f['id'] for f in floors}) == 30
assert {int(f['number']) for f in floors} == set(range(1, 31))
assert next(int(f['layout_seed']) for f in floors if f['number']=='30') == 1002
report = {'rows': len(events), 'events': dict(sorted(counts.items())), 'transitions': len(transitions),
          'steady_native_counts': {scope: {key: next(row[key] for row in transitions if row['scope'] == scope and row['cycle'] == '5')
           for key in ('owned_assets', 'meshes', 'materials')} for scope in ('town', 'floor', 'wilds')},
          'csv_escaping': 'verified', 'event_ids': 'unique', 'seed_metadata': 'verified', 'result': 'PASS'}
(root / 'Reports' / 'production-evidence-check.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(f'PRODUCTION EXPORT PASS: {len(events)} unique events, 18 stable transitions, 30 definitions')
