"""Prints this record's tables from raw/.

    python3 tables.py [raw directory] [the first record's raw directory]

Reads the per-sample files as measure-read-ahead.spec.mjs wrote them, minified and gzipped here: one
directory a pass of the run, raw/p1 to raw/p5, each holding <layout>-scroll.json.gz and
<layout>-live.json.gz; and raw/trial-a657b79, the trigger's first run, which asked by a rule the next
commit replaced (README, "The trigger's first run"). It sums them up itself: every figure in the README
comes from the samples by the code below, and the README's tables are the first ones it prints. The first
record's per-sample files, ../2026-10-10-linux-remote-read-ahead/raw/, give its N = 60 beside this run's,
the anchor. A median is of the samples, or of the episodes, answers or ticks the column names; a range is
the least to the most.

The read-ahead keys are the spec's: b50, b100 and b200 are aligned blocks of that many rows covering the
range and 10 rows either side; 30, 60 and 120 are that many rows either side; a t10 after either asks
once the rows on screen and 10 either side are not all in the Window.
"""
import gzip
import json
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'raw')
FIRST = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, '..', '2026-10-10-linux-remote-read-ahead', 'raw')
TRIAL = 'trial-a657b79'
ORDER = ['b50', 'b100', 'b200', '30', '60', '120', 'b100t10', '60t10']
LAYOUTS = ('narrow', 'wide', 'wide-dense')
INPUT_NAMES = {'wheel': 'Wheel', 'arrow': 'ArrowDown', 'pagedown': 'PageDown'}
PACE_NAMES = {'step': 'a {} at a time', 'held': 'held'}
UNIT = {'wheel': 'notch', 'arrow': 'press', 'pagedown': 'press'}


def load(path):
    if not os.path.exists(path):
        return None
    with gzip.open(path, 'rt') as f:
        return json.load(f)


def passes():
    return sorted(d for d in os.listdir(RAW) if d.startswith('p') and os.path.isdir(os.path.join(RAW, d)))


def mode_of(key):
    return key.split(' rows, ')[0]


def trades_of(key):
    return int(key.split(' rows, ')[1].split(' ')[0])


def rank(key):
    mode = mode_of(key)
    return (ORDER.index(mode) if mode in ORDER else len(ORDER), key)


def merged(layout, part, names=None):
    """The read-aheads of every pass named, for a layout and part, in ORDER, with each pass's environment."""
    readAheads, environments = {}, []
    for name in names or passes():
        data = load(os.path.join(RAW, name, f'{layout}-{part}.json.gz'))
        if not data:
            continue
        environments.append((name, data['environment']))
        readAheads.update(data['readAheads'])
    return {k: readAheads[k] for k in sorted(readAheads, key=rank)}, environments


def numbers(values):
    return [v for v in values if isinstance(v, (int, float)) and not isinstance(v, bool)]


def med(values):
    values = numbers(values)
    return statistics.median(values) if values else None


def lo_hi(values):
    values = numbers(values)
    return (min(values), max(values)) if values else (None, None)


def n0(v):
    return '—' if v is None else f'{v:,.0f}'


def n1(v):
    return '—' if v is None else f'{v:,.1f}'


def kib(v):
    return '—' if v is None else f'{v / 1024:,.1f}'


def spread(values, fmt=n0):
    m = med(values)
    lo, hi = lo_hi(values)
    if m is None:
        return '—'
    if fmt(lo) == fmt(hi):
        return fmt(m)
    return f'{fmt(m)} ({fmt(lo)}–{fmt(hi)})'


def runs_of(value, input, pace):
    return [s for s in value['samples'] if s['input'] == input and s['pace'] == pace]


def share(runs):
    return med([100 * s['placeholderMs'] / s['elapsedMs'] for s in runs if s['elapsedMs']])


def cell(runs, pace):
    """A step pace's Placeholder time a sample, its episodes and one episode's median; a held pace's
    share of the scroll's time."""
    if not runs:
        return '—'
    if pace == 'held':
        return f'{n0(share(runs))}%'
    total = med([s['placeholderMs'] for s in runs])
    episodes = med([len(s['episodes']) for s in runs])
    one = med([e['frameMs'] for s in runs for e in s['episodes']])
    if not episodes:
        return 'none'
    seconds = total / 1000
    text = f'{seconds:,.1f} s' if seconds >= 1 else f'{seconds:,.2f} s'
    return f'{text} ({n0(episodes)} × {n0(one)} ms)'


def table(header, rows):
    print('| ' + ' | '.join(header) + ' |')
    print('|' + '---|' * len(header))
    for row in rows:
        print('| ' + ' | '.join(row) + ' |')


# ---- The README's tables ---------------------------------------------------------------------------

def side_by_side(layout, scroll):
    keys = list(scroll)
    print(f'\n### {layout}\n')
    rows = []
    for input in ('wheel', 'arrow', 'pagedown'):
        for pace in ('step', 'held'):
            rows.append([f"{INPUT_NAMES[input]}, {PACE_NAMES[pace].format(UNIT[input])}"]
                        + [cell(runs_of(scroll[k], input, pace), pace) for k in keys])
    table(['Input'] + keys, rows)


def held_questions(scrolls):
    print('\n### Questions asked while held, and superseded (median of three samples)\n')
    keys = list(dict.fromkeys(k for scroll in scrolls.values() for k in scroll))
    rows = []
    for layout, scroll in scrolls.items():
        for input in ('wheel', 'arrow', 'pagedown'):
            row = [f'{layout}, {INPUT_NAMES[input]}']
            for k in keys:
                runs = runs_of(scroll[k], input, 'held') if k in scroll else []
                row.append(f"{n0(med([s['questions']['asked'] for s in runs]))}, {n0(med([s['questions']['cancelled'] for s in runs]))}"
                           if runs else '—')
            rows.append(row)
    table(['Report, input'] + keys, rows)


def anchor(scrolls):
    print('\n### N = 60, the first record and this run\n')
    rows = []
    paces = []
    for layout, scroll in scrolls.items():
        first = load(os.path.join(FIRST, f'{layout}-scroll.json.gz'))
        if not first or '60' not in first['readAheads'] or '60' not in scroll:
            continue
        then, now = first['readAheads']['60'], scroll['60']
        for input in ('wheel', 'arrow', 'pagedown'):
            for pace in ('step', 'held'):
                rows.append([f"{layout}, {INPUT_NAMES[input]}, {PACE_NAMES[pace].format(UNIT[input])}",
                             cell(runs_of(then, input, pace), pace), cell(runs_of(now, input, pace), pace)])
        every = lambda value: med([s['inputIntervalMs']['median'] for s in value['samples']
                                   if s['pace'] == 'held' and s.get('inputIntervalMs')])
        paces.append(f'{n0(every(then))} and {n0(every(now))} ms apart ({layout})')
    if rows:
        table(['Report, input', '60, 2026-10-10', '60, this run'], rows)
        print('\nHeld inputs came a median, then and now, ' + '; '.join(paces) + '.')


def answer_cells(entries, logs):
    windows = [l for l in logs if l['kind'] == 'window' and l.get('complete')]
    checks = [l for l in logs if l['kind'] == 'check' and l['changes'] == -1 and l['rows'] >= 0]
    filled = [e['filledCells'] / (e['rows'] * e['valueColumns']) * 100 for e in entries if e['rows'] and e['valueColumns']]
    return [str(len(entries)), spread([e['rows'] for e in entries]), kib(med([e['rawBytes'] for e in entries])),
            kib(med([e['metadataBytes'] for e in entries])), kib(med([e['encodedBytes'] for e in entries])), f'{n0(med(filled))}%',
            f"{n0(med([w['arrivedMs'] for w in windows]))} / {n0(med([w['parseMs'] for w in windows]))} / {n1(med([c['ms'] for c in checks]))}",
            f"{n1(med([e['timing'].get('project') for e in entries]))} / {n1(med([e['timing'].get('serialize') for e in entries]))}"]


def answers(data):
    print("\n### A complete Window's answer\n")
    rows = []
    for layout in LAYOUTS:
        scroll, _, live, _ = data[layout]
        if scroll:
            for key, value in scroll.items():
                entries = [e for s in value['samples'] for e in s['network'] if e.get('outcome') == 'answered' and e.get('complete')]
                logs = [l for s in value['samples'] for l in s['pageLog']]
                if entries:
                    rows.append([layout, key] + answer_cells(entries, logs))
        elif live:
            # Too short to scroll: the complete Window each live run placed.
            by = {}
            for key, value in live.items():
                entries = [e for e in value['placedAnswers'] if e.get('outcome') == 'answered' and e.get('complete')]
                by.setdefault(mode_of(key), ([], []))
                by[mode_of(key)][0].extend(entries)
                by[mode_of(key)][1].extend(value['placedLog'])
            for mode, (entries, logs) in sorted(by.items(), key=lambda kv: rank(kv[0])):
                if entries:
                    rows.append([layout, mode] + answer_cells(entries, logs))
    table(['Report', 'Read-ahead', 'Answers', 'Rows', 'JSON, KiB', 'of which metadata', 'Wire, KiB', 'Cells filled',
           'Browser: arrived / parse / check, ms', 'Server: project / write, ms'], rows)


def per_rows_scrolled(data):
    """What a scroll costs, one input at a time. Steadily: the rows a question asks, and how far the scroll
    goes from one question to the next, from ArrowDown's questions one after another. Over the samples:
    every question, and every answer's JSON, Brotli, rows and parse, per 100 rows scrolled, which counts
    where each sample's start fell against the Windows, since its first Window was asked before it began.
    Held paces are left out: a question they supersede is aborted before its answer is read."""
    print('\n### What a scroll costs, one input at a time\n')
    rows = []
    for layout in LAYOUTS:
        scroll = data[layout][0]
        for key, value in scroll.items():
            runs = [s for s in value['samples'] if s['pace'] == 'step']
            asked, advanced = [], []
            for s in runs:
                if s['input'] == 'arrow':
                    answered_ = [e for e in s['network'] if e.get('outcome') == 'answered']
                    asked += [e['count'] for e in answered_]
                    advanced += [b['start'] - a['start'] for a, b in zip(answered_, answered_[1:])]
            scrolled = sum(s['rowsScrolled'] for s in runs)
            answered = [e for s in runs for e in s['network'] if e.get('outcome') == 'answered']
            parse = sum(l['parseMs'] for s in runs for l in s['pageLog'] if l['kind'] == 'window')
            per = lambda total: total * 100 / scrolled if scrolled else None
            ratio = med(asked) / med(advanced) if med(asked) and med(advanced) else None
            rows.append([layout, key, n0(med(asked)), n0(med(advanced)), '—' if ratio is None else f'{ratio:.2f}',
                         n1(per(sum(s['questions']['asked'] for s in runs))), kib(per(sum(e['rawBytes'] for e in answered))),
                         kib(per(sum(e['encodedBytes'] for e in answered))), n0(per(parse))])
    table(['Report', 'Read-ahead', 'Rows a question', 'Rows scrolled from one to the next', 'Rows answered a row scrolled',
           'Questions per 100 rows', 'JSON per 100 rows, KiB', 'Wire per 100 rows, KiB', 'Parse per 100 rows, ms'], rows)


def live_rows(data, trades):
    rows = []
    for layout in LAYOUTS:
        live = data[layout][2]
        for key, value in live.items():
            if trades_of(key) != trades:
                continue
            entries = [e for t in value['ticks'] for e in t['network'] if e.get('baseline') and e.get('outcome') == 'answered']
            changes = [e for e in entries if not e.get('complete')]
            complete = [e for e in entries if e.get('complete')]
            logs = [l for t in value['ticks'] for l in t['pageLog']]
            checks = [l for l in logs if l['kind'] == 'check' and l['rows'] >= 0]
            windows = [l for l in logs if l['kind'] == 'window']
            held = sorted({(e['start'], e['count']) for e in entries})
            rows.append([f'{layout}, {trades:,}', mode_of(key), ', '.join(f'{s:,}–{s + c - 1:,}' for s, c in held),
                         spread([e['changes'] for e in changes]), f'{len(complete)} of {len(entries)}',
                         spread([e['rawBytes'] / 1024 for e in changes], n1), spread([e['encodedBytes'] / 1024 for e in changes], n1),
                         f"{n0(med([e['timing'].get('compute') for e in entries]))} / {spread([e['timing'].get('project') for e in entries], n1)}",
                         f"{n1(med([w['parseMs'] for w in windows]))} / {n1(med([c['ms'] for c in checks]))}"])
    return rows


def live(data):
    print('\n### A live update over the Window held\n')
    rows = live_rows(data, 1000) + live_rows(data, 20)
    table(['Report, trades a tick', 'Read-ahead', 'Window held, rows', 'Window Changes, rows', 'Complete instead',
           'JSON, KiB', 'Wire, KiB', 'Server: compute / project, ms', 'Browser: parse / check, ms'], rows)
    complete = {}
    for layout in LAYOUTS:
        for key, value in data[layout][2].items():
            sizes = [e['rawBytes'] for t in value['ticks'] for e in t['network']
                     if e.get('baseline') and e.get('outcome') == 'answered' and e.get('complete')]
            if sizes:
                complete[f'{layout} {mode_of(key)}, {trades_of(key):,} trades'] = sizes
    if complete:
        print('\nThe complete Windows answered in place of Window Changes, when the row sequence moved: '
              + '; '.join(f'{name}, {len(sizes)} of {kib(min(sizes))}–{kib(max(sizes))} KiB' for name, sizes in complete.items()) + '.')


def kept(data):
    print('\n### The Windows the report source keeps (LV-22), after the ticks\n')
    rows = []
    for trades in (1000, 20):
        for layout in LAYOUTS:
            for key, value in data[layout][2].items():
                if trades_of(key) != trades:
                    continue
                k, released = value['keptAfter'], value['released']
                within = max(max(released['heapWithBytes']) - min(released['heapWithBytes']),
                             max(released['heapWithoutBytes']) - min(released['heapWithoutBytes']))
                rows.append([f'{layout}, {trades:,}', mode_of(key), f"{k['keptWindows']} / {k['keptRows']:,}", f"{k['keptDistinctRows']:,}",
                             f"{k['keptValueCells']:,}", kib(released['heldBytes']), kib(within)])
    table(['Report, trades a tick', 'Read-ahead', 'Windows / rows', 'Distinct rows', 'Value cells', 'Heap they hold, KiB',
           'Readings within, KiB'], rows)


# ---- The detail behind them ------------------------------------------------------------------------

def detail(layout, scroll, environments):
    first = next(iter(scroll.values()))
    print(f'\n### {layout}: every scrolling case\n')
    print(f"Report rows {first['reportRows']:,}; rows painted {first['paintedRows']}; PageDown moves {first['pageRows']} rows; "
          f"samples from rows {sorted({s['startRow'] for s in first['samples']})}.\n")
    for name, env in environments:
        print(f"- {name}: {env['at']}, Chromium {env['browser']}, {env['roundTripMs']} ms, read-aheads {env['readAheads']['scroll']}, "
              f"{env['samples']} samples of {env['distanceRows']} rows, lab commit {env['commit'][:7]}")
    print('\nThe Window each sample started from, as asked when its start was placed:\n')
    for key, value in scroll.items():
        windows = set()
        for s in value['samples']:
            answered = [e for e in s.get('setup', []) if e['outcome'] == 'answered']
            if answered:
                windows.add((s['startRow'], answered[-1]['start'], answered[-1]['count']))
        print(f"- {key}: " + ', '.join(f'from row {r:,}, rows {a:,}–{a + c - 1:,}' for r, a, c in sorted(windows)))
    for pace in ('step', 'held'):
        print(f'\n{layout}, {pace}:\n')
        rows = []
        for input in ('wheel', 'arrow', 'pagedown'):
            for key, value in scroll.items():
                runs = runs_of(value, input, pace)
                if not runs:
                    continue
                most = [e['maxRows'] for s in runs for e in s['episodes']]
                rows.append([INPUT_NAMES[input], key, n0(med([s['rowsScrolled'] for s in runs])), spread([s['placeholderMs'] for s in runs]),
                             f'{n0(share(runs))}%', spread([len(s['episodes']) for s in runs]),
                             spread([e['frameMs'] for s in runs for e in s['episodes']]), str(max(most) if most else 0),
                             spread([s['questions']['asked'] for s in runs]), spread([s['questions']['cancelled'] for s in runs]),
                             n0(med([s['inputIntervalMs']['median'] for s in runs if s.get('inputIntervalMs')])),
                             str(sum(len(s['flings']) for s in runs))])
        table(['Input', 'Read-ahead', 'Rows scrolled', 'Placeholder time a sample, ms', 'Share of the time', 'Episodes',
               'One episode, ms', 'Rows not held, most', 'Questions', 'Superseded', 'Input every, ms', 'Fling episodes'], rows)


data = {layout: merged(layout, 'scroll') + merged(layout, 'live') for layout in LAYOUTS}
scrolls = {layout: data[layout][0] for layout in LAYOUTS if data[layout][0]}

print("# The README's tables")
print('\n## Placeholders while scrolling')
for layout, scroll in scrolls.items():
    side_by_side(layout, scroll)
held_questions(scrolls)
anchor(scrolls)
answers(data)
per_rows_scrolled(data)
live(data)
kept(data)

if os.path.isdir(os.path.join(RAW, TRIAL)):
    print(f"\n## The trigger's first run ({TRIAL}): a question in flight superseded once the rows on screen and 10 "
          "either side left its Window")
    trials = {}
    for layout in ('narrow', 'wide'):
        trial, _ = merged(layout, 'scroll', [TRIAL])
        if trial:
            trials[layout] = trial
            side_by_side(layout, trial)
    held_questions(trials)

print('\n# The detail behind them')
for layout, scroll in scrolls.items():
    detail(layout, scroll, data[layout][1])
