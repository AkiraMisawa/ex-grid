"""Prints this record's tables from raw/.

    python3 tables.py [raw directory]

Reads the per-sample files, raw/<layout>-scroll.json.gz and raw/<layout>-live.json.gz, as
measure-read-ahead.spec.mjs wrote them (minified and gzipped here), and sums them up itself: every
figure in the README comes from the samples by the code below. A median is of the samples, or of the
episodes, answers or ticks the column names; a range is the least to the most.
"""
import gzip
import json
import os
import statistics
import sys

RAW = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), 'raw')
INPUT_NAMES = {'wheel': 'Wheel', 'arrow': 'ArrowDown', 'pagedown': 'PageDown'}


def load(name):
    path = os.path.join(RAW, name + '.gz')
    if not os.path.exists(path):
        return None
    with gzip.open(path, 'rt') as f:
        return json.load(f)


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


def scroll(layout):
    data = load(f'{layout}-scroll.json')
    if not data:
        return
    keys = list(data['readAheads'].keys())
    first = data['readAheads'][keys[0]]
    env = data['environment']
    print(f'\n## {layout}: scrolling\n')
    print(f"Report rows {first['reportRows']:,}; rows painted {first['paintedRows']}; PageDown moves {first['pageRows']} rows; "
          f"{env['samples']} samples of {env['distanceRows']} rows each, from rows "
          f"{sorted({s['startRow'] for s in first['samples']})}; lab commit {env['commit']}.\n")
    print('The Window each sample started from, as asked when its start was placed (rows):\n')
    for key in keys:
        counts = sorted({e['count'] for s in data['readAheads'][key]['samples'] for e in s.get('setup', [])
                         if e['outcome'] == 'answered'})
        print(f'- {key}: {counts}')
    for pace in ('step', 'held'):
        print(f'\n### {pace}\n')
        print('| Input | Read-ahead | Rows scrolled | Placeholder time a sample, ms | Share of the time | '
              'Episodes | One episode, ms | Rows not held, most | Questions | Superseded | Input every, ms |')
        print('|---|---|---|---|---|---|---|---|---|---|---|')
        for input in ('wheel', 'arrow', 'pagedown'):
            for key in keys:
                runs = [s for s in data['readAheads'][key]['samples'] if s['input'] == input and s['pace'] == pace]
                if not runs:
                    continue
                total = [s['placeholderMs'] for s in runs]
                share = [100 * s['placeholderMs'] / s['elapsedMs'] for s in runs if s['elapsedMs']]
                episodes = [len(s['episodes']) for s in runs]
                one = [e['frameMs'] for s in runs for e in s['episodes']]
                most = [e['maxRows'] for s in runs for e in s['episodes']]
                asked = [s['questions']['asked'] for s in runs]
                superseded = [s['questions']['cancelled'] for s in runs]
                every = [s['inputIntervalMs']['median'] for s in runs if s.get('inputIntervalMs')]
                rows = [s['rowsScrolled'] for s in runs]
                flings = sum(len(s['flings']) for s in runs)
                print(f"| {INPUT_NAMES[input]} | {key} | {n0(med(rows))} | {spread(total)} | {n0(med(share))}% | "
                      f"{spread(episodes)} | {spread(one)} | {max(most) if most else 0} | {spread(asked)} | "
                      f"{spread(superseded)} | {n0(med(every))} |" + (f' fling episodes {flings}' if flings else ''))


def answer_row(key, entries, logs):
    windows = [l for l in logs if l['kind'] == 'window' and l.get('complete')]
    checks = [l for l in logs if l['kind'] == 'check' and l['changes'] == -1 and l['rows'] >= 0]
    rows = [e['rows'] for e in entries]
    filled = [e['filledCells'] / (e['rows'] * e['valueColumns']) * 100 for e in entries if e['rows'] and e['valueColumns']]
    return (f"| {key} | {len(entries)} | {spread(rows)} | {kib(med([e['rawBytes'] for e in entries]))} | "
            f"{kib(med([e['metadataBytes'] for e in entries]))} | {kib(med([e['rowsBytes'] for e in entries]))} | "
            f"{kib(med([e['encodedBytes'] for e in entries]))} | {n0(med(filled))}% | "
            f"{n1(med([e['timing'].get('project') for e in entries]))} / {n1(med([e['timing'].get('serialize') for e in entries]))} | "
            f"{n0(med([w['arrivedMs'] for w in windows]))} / {spread([w['parseMs'] for w in windows])} / {n1(med([c['ms'] for c in checks]))} |")


def answers(layout):
    header = ('| Read-ahead | Answers | Rows | JSON, KiB | of which metadata | of which rows | Brotli on the wire, KiB | '
              'Cells filled | Server: project / write JSON, ms | Browser: arrived / parse / check, ms |')
    rule = '|---|---|---|---|---|---|---|---|---|---|'
    data = load(f'{layout}-scroll.json')
    if data:
        print(f'\n## {layout}: a complete Window\'s answer, while scrolling\n')
        print(header)
        print(rule)
        for key, value in data['readAheads'].items():
            entries = [e for s in value['samples'] for e in s['network'] if e.get('outcome') == 'answered' and e.get('complete')]
            logs = [l for s in value['samples'] for l in s['pageLog']]
            if entries:
                print(answer_row(key, entries, logs))
        first = next(iter(data['readAheads'].values()))
        question = [e['requestBytes'] for s in first['samples'] for e in s['network'] if e.get('requestBytes')]
        print(f'\nEvery question: {kib(med(question))} KiB of JSON.')
        return
    live = load(f'{layout}-live.json')
    if not live:
        return
    # A layout too short to scroll: the complete Window each live run placed, read ahead.
    print(f'\n## {layout}: a complete Window\'s answer, as each live run placed its Window\n')
    print(header)
    print(rule)
    by = {}
    for key, value in live['readAheads'].items():
        readAhead = key.split(' rows, ')[0]
        entries = [e for e in value['placedAnswers'] if e.get('outcome') == 'answered' and e.get('complete')]
        by.setdefault(readAhead, ([], []))
        by[readAhead][0].extend(entries)
        by[readAhead][1].extend(value['placedLog'])
    for readAhead, (entries, logs) in by.items():
        print(answer_row(readAhead, entries, logs))


def live(layout):
    data = load(f'{layout}-live.json')
    if not data:
        return
    print(f'\n## {layout}: live updates over the Window held\n')
    print('| Trades a tick | Read-ahead | Window rows | Window Changes, rows | Complete instead | JSON, KiB | '
          'of which changes | Brotli, KiB | Server: compute / project, ms | Browser: parse / check, ms |')
    print('|---|---|---|---|---|---|---|---|---|---|')
    for key, value in data['readAheads'].items():
        entries = [e for t in value['ticks'] for e in t['network'] if e.get('baseline') and e.get('outcome') == 'answered']
        changes = [e for e in entries if not e.get('complete')]
        complete = [e for e in entries if e.get('complete')]
        logs = [l for t in value['ticks'] for l in t['pageLog']]
        checks = [l for l in logs if l['kind'] == 'check' and l['rows'] >= 0]
        windows = [l for l in logs if l['kind'] == 'window']
        rows = [e['server']['windowRows'] for e in entries if e.get('server')]
        readAhead, trades = key.split(' rows, ')[0], key.split(' rows, ')[1].split(' trades a tick')[0]
        print(f"| {trades} | {readAhead} | {n0(med(rows))} | {spread([e['changes'] for e in changes])} | {len(complete)} of {len(entries)} | "
              f"{spread([e['rawBytes'] / 1024 for e in changes], n1)} | {spread([(e['changesBytes'] or 0) / 1024 for e in changes], n1)} | "
              f"{spread([e['encodedBytes'] / 1024 for e in changes], n1)} | "
              f"{n0(med([e['timing'].get('compute') for e in entries]))} / {spread([e['timing'].get('project') for e in entries], n1)} | "
              f"{n1(med([w['parseMs'] for w in windows]))} / {n1(med([c['ms'] for c in checks]))} |")
    complete = [(key, e) for key, value in data['readAheads'].items() for t in value['ticks'] for e in t['network']
                if e.get('baseline') and e.get('outcome') == 'answered' and e.get('complete')]
    if complete:
        print('\nThe complete Windows answered in place of Window Changes (the row sequence moved): '
              + '; '.join(f"{key}: {kib(e['rawBytes'])} KiB" for key, e in complete) + '.')
    print(f'\n## {layout}: the Windows the report source keeps (LV-22)\n')
    print('| Trades a tick | Read-ahead | Before the ticks: Windows / rows | After: Windows / rows | Distinct rows | '
          'Value cells | Filled | Heap they hold, KiB: with − without (three readings each) |')
    print('|---|---|---|---|---|---|---|---|')
    for key, value in data['readAheads'].items():
        before = value['keptBefore']
        kept = value['keptAfter']
        released = value['released']
        readAhead, trades = key.split(' rows, ')[0], key.split(' rows, ')[1].split(' trades a tick')[0]
        spread_with = max(released['heapWithBytes']) - min(released['heapWithBytes'])
        spread_without = max(released['heapWithoutBytes']) - min(released['heapWithoutBytes'])
        print(f"| {trades} | {readAhead} | {before['keptWindows']} / {before['keptRows']} | {kept['keptWindows']} / {kept['keptRows']:,} | "
              f"{kept['keptDistinctRows']:,} | {kept['keptValueCells']:,} | {kept['keptFilledCells']:,} | "
              f"{kib(released['heldBytes'])} (readings within {kib(max(spread_with, spread_without))}) |")


for layout in ('narrow', 'wide', 'wide-dense'):
    scroll(layout)
    answers(layout)
    live(layout)
