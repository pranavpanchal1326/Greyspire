# Q01: turns first-chapter playtest sessions into a findings report.
#
#   python Tools/playtest_report.py <kit folder> [--out report.md]
#
# Reads every session CSV the game wrote under <kit>/sessions (one per tester, started with
# -playtest <name> -playtest-dir <kit>/sessions) and any facilitator notes under <kit>/notes
# (<tester>.md, from the observation form). Writes <kit>/Q01-findings.md: per-session timelines,
# the chapter's funnel, where newcomers waited longest on one instruction, difficulty spikes, which
# helps were used, and a ranked list of fix candidates, each with the evidence behind it.
#
# The numbers say where and how long; only the notes say why. A finding marked "observe" needs the
# facilitator's notes before it becomes a fix.
import csv
import glob
import os
import statistics
import sys
from collections import Counter, defaultdict

PANELS = {1: 'tower gate', 2: 'Guild', 3: 'noble house', 4: 'home', 5: 'Field desk'}
STALL_TOWN = 240.0      # seconds on one objective in town before it counts as lost
STALL_FLOOR_FACTOR = 2.0
SPIKE_DEFEATS = 3


def load(path):
    rows = []
    with open(path, encoding='utf-8') as f:
        for r in csv.DictReader(f):
            try:
                r['t'] = float(r['seconds'])
                r['v'] = float(r['value'] or 0)
                r['f'] = int(float(r['floor'] or 0))
            except ValueError:
                continue
            rows.append(r)
    return rows


def session(path):
    rows = load(path)
    name = next((r['note'] for r in rows if r['event'] == 'session_start'), os.path.basename(path).split('_')[0])
    s = {'tester': name, 'file': os.path.basename(path), 'duration': rows[-1]['t'] if rows else 0.0}
    starts, clears = {}, {}
    defeats = Counter()
    s['guardian_engaged'] = sum(1 for r in rows if r['event'] == 'guardian_engage')
    s['guardian_won'] = any(r['event'] == 'guardian_won' for r in rows)
    s['checkpoint'] = any(r['event'] == 'checkpoint' for r in rows)
    s['journal'] = sum(1 for r in rows if r['event'] == 'journal_open')
    s['tips'] = max([int(r['v']) for r in rows if r['event'] == 'tip'] or [-1])
    s['panels'] = Counter(PANELS.get(int(r['v']), str(int(r['v']))) for r in rows if r['event'] == 'hub_panel')
    s['settings'] = [r['note'] for r in rows if r['event'] in ('access', 'rebind')]
    s['courier'] = next(({1: 'helped', 2: 'kept the kit'}.get(int(r['v']), '?') for r in rows if r['event'] == 'courier_choice'), None)
    s['rescue'] = next(({1: 'sent home', 2: 'kept for intel'}.get(int(r['v']), '?') for r in rows if r['event'] == 'rescue_choice'), None)
    for r in rows:
        if r['event'] == 'floor_start':
            starts.setdefault(int(r['v']), r['t'])
        elif r['event'] == 'floor_cleared' and r['note'] == 'first clear':
            clears.setdefault(int(r['v']), r['t'])
        elif r['event'] == 'player_defeat':
            defeats[r['f']] += 1
    s['floor_time'] = {f: clears[f] - starts[f] for f in clears if f in starts}
    s['furthest'] = max(clears) if clears else 0
    s['defeats'] = defeats
    # How long each objective stayed on screen, and where it was shown.
    spans, last = [], None
    for r in rows:
        if r['event'] == 'objective':
            if last:
                spans.append((last[1], r['t'] - last[0], last[2]))
            last = (r['t'], r['note'], 'floor' if r['f'] > 0 and not r['note'].startswith(('Enter', 'Climb', 'Report')) else 'town')
    if last:
        spans.append((last[1], s['duration'] - last[0], last[2]))
    s['spans'] = spans
    s['first_climb'] = starts.get(1)
    return s


def report(kit, out):
    files = sorted(glob.glob(os.path.join(kit, 'sessions', '*.csv')))
    sessions = [session(f) for f in files]
    notes = {os.path.splitext(os.path.basename(n))[0]: open(n, encoding='utf-8').read() for n in glob.glob(os.path.join(kit, 'notes', '*.md'))}
    L = ['# Q01 findings: first-chapter playtest', '']
    if not sessions:
        L += ['No sessions recorded yet. Run `Play.cmd` with each tester (see README.md); their sessions land in `sessions/`.', '']
        open(out, 'w', encoding='utf-8').write('\n'.join(L))
        return out
    synthetic = [s for s in sessions if s['tester'].lower().startswith('synthetic')]
    real = [s for s in sessions if s not in synthetic]
    dry = not real
    runs = 'dry run' if len(synthetic) == 1 else 'dry runs'
    if dry:
        L += [f'Sessions: {len(sessions)}, all synthetic {runs}. **No tester has played yet:** everything below only proves the recording and report pipeline.', '']
        real = sessions
    elif synthetic:
        L += [f'Sessions: {len(sessions)} ({len(synthetic)} synthetic {runs}, excluded from the findings below).', '']
    else:
        L += [f'Sessions: {len(sessions)}.', '']

    L += ['## Funnel', '', '| Stage | Reached |', '|---|---|']
    def count(pred):
        return f'{sum(1 for s in real if pred(s))} / {len(real)}'
    L += [f'| Entered floor 1 | {count(lambda s: s["first_climb"] is not None)} |']
    for f in range(1, 6):
        L += [f'| Cleared floor {f} | {count(lambda s, f=f: s["furthest"] >= f)} |']
    L += [f'| Faced the Gatekeeper | {count(lambda s: s["guardian_engaged"] > 0)} |', f'| Beat the Gatekeeper | {count(lambda s: s["guardian_won"])} |', '']

    L += ['## Sessions', '']
    for s in sessions:
        L += [f'### {s["tester"]}  ({s["duration"] / 60:.1f} min){"  (synthetic dry run)" if s in synthetic else ""}', '']
        if s['first_climb'] is not None:
            L += [f'- First climb started after {s["first_climb"]:.0f} s.']
        if s['floor_time']:
            L += ['- Floor times: ' + ', '.join(f'F{f} {t:.0f} s' for f, t in sorted(s['floor_time'].items()))]
        if s['defeats']:
            L += ['- Defeats: ' + ', '.join(f'F{f} x{n}' for f, n in sorted(s['defeats'].items()))]
        L += [f'- Gatekeeper: {s["guardian_engaged"]} attempt(s), ' + ('won' if s['guardian_won'] else 'not beaten') + (', checkpoint used' if s['checkpoint'] else '')]
        L += [f'- Choices: courier {s["courier"] or "not met"}, Rhea {s["rescue"] or "not met"}']
        L += [f'- Helps: tips reached {s["tips"] + 1} of 9, journal opened {s["journal"]}x, town services ' + (', '.join(f'{k} x{v}' for k, v in s['panels'].items()) or 'none')]
        if s['settings']:
            L += ['- Settings changed: ' + '; '.join(s['settings'])]
        if s['tester'] in notes:
            L += ['', '> Facilitator notes:', ''] + ['> ' + line for line in notes[s['tester']].splitlines()]
        L += ['']

    # Findings, ranked.
    findings = []
    town_stalls = defaultdict(list)
    for s in real:
        for text, secs, where in s['spans']:
            if where == 'town' and secs >= STALL_TOWN:
                town_stalls[text].append((s['tester'], secs))
    for text, who in town_stalls.items():
        findings.append((3 if len(who) > 1 else 2, 'observe', f'Newcomers stood on "{text}" for a long time in town: ' + ', '.join(f'{t} {secs / 60:.1f} min' for t, secs in who)))
    medians = {}
    for f in range(1, 6):
        times = [s['floor_time'][f] for s in real if f in s['floor_time']]
        if times:
            medians[f] = statistics.median(times)
    for s in real:
        for f, t in s['floor_time'].items():
            if f in medians and len(real) > 2 and t > STALL_FLOOR_FACTOR * medians[f]:
                findings.append((1, 'observe', f'{s["tester"]} took {t:.0f} s on floor {f} (median {medians[f]:.0f} s).'))
    for f in range(1, 6):
        hit = [s['tester'] for s in real if s['defeats'][f] >= SPIKE_DEFEATS]
        if hit:
            findings.append((3 if len(hit) > 1 else 2, 'tune', f'Difficulty spike on floor {f}: {SPIKE_DEFEATS}+ defeats for ' + ', '.join(hit) + '.'))
    stuck_guardian = [s['tester'] for s in real if s['guardian_engaged'] >= 3 and not s['guardian_won']]
    if stuck_guardian:
        findings.append((3, 'tune', 'The Gatekeeper beat ' + ', '.join(stuck_guardian) + ' three or more times without a win.'))
    quit_early = [s['tester'] for s in real if s['furthest'] < 5 and not s['guardian_won']]
    if quit_early:
        findings.append((2, 'observe', 'Did not finish the chapter: ' + ', '.join(f'{s["tester"]} (furthest floor {s["furthest"]})' for s in real if s['tester'] in quit_early) + '. Check the notes for why.'))
    no_help = [s['tester'] for s in real if s['journal'] == 0 and any(secs >= STALL_TOWN for _, secs, _ in s['spans'])]
    if no_help:
        findings.append((2, 'observe', 'Lost for a while but never opened the journal: ' + ', '.join(no_help) + '. Is the journal discoverable?'))
    early_settings = [s['tester'] for s in real if s['settings']]
    if early_settings:
        findings.append((1, 'observe', 'Changed controls or access settings: ' + ', '.join(f'{s["tester"]} ({"; ".join(s["settings"])})' for s in real if s['settings']) + '. Are the defaults right?'))
    findings.sort(key=lambda x: -x[0])
    L += ['## Ranked findings', '']
    if not findings:
        L += ['Nothing crossed a threshold in the recorded sessions. Read the facilitator notes for anything the numbers cannot show.', '']
    for i, (sev, kind, text) in enumerate(findings, 1):
        L += [f'{i}. **{["", "Low", "Medium", "High"][sev]}** ({kind}) {text}']
    L += ['', '"tune" findings can be fixed from the numbers; "observe" findings need the facilitator notes to say why before a fix is chosen.', '']
    open(out, 'w', encoding='utf-8').write('\n'.join(L))
    return out


if __name__ == '__main__':
    kit = sys.argv[1]
    out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else os.path.join(kit, 'Q01-findings.md')
    print(report(kit, out))
