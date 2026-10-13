"""The eighteenth run's (18a) area pass's tables for report.md, read from the results files the pass wrote and the corpus before and
after it.

    python3 summarise.py <repo> <base commit> > summary.md

Excel's double is read from each answer's "number" (G17, which round-trips; afd70b15), never from "value2", which
ConvertTo-Json wrote. The pass of record for a case is the last one, in the order below, that asked it the way the
procedure names for it: the keys pass for an "ask by keys" case, the en-US COM pass otherwise.
"""
import collections
import json
import os
import subprocess
import sys

repo, base = sys.argv[1], sys.argv[2]
O = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelOracle')
C = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelCases')
P = 'results-2026-10-14-run18a'
PASSES = [(f, cu, how) for f, cu, how in [  # (file, culture, how)
    (f'{P}-com-en-US.json', 'en-US', 'COM'),
    (f'{P}-keys-en-US.json', 'en-US', 'keys'),
] if os.path.exists(os.path.join(O, f))]


def corpus(at=None):
    out = {}
    for n in sorted(os.listdir(C)):
        text = (open(os.path.join(C, n), encoding='utf-8').read() if at is None else
                subprocess.run(['git', '-C', repo, 'show', f'{at}:tests/ExSheet.Engine.Tests/ExcelCases/{n}'],
                               capture_output=True, text=True, check=True).stdout)
        for c in json.loads(text).get('cases', []):
            out[c['id']] = {**c, '_file': n}
    return out


before, after = corpus(base), corpus()
asked = {}      # id -> list of (pass, culture, how, result)
for f, cu, how in PASSES:
    for r in json.load(open(os.path.join(O, f), encoding='utf-8-sig'))['cases']:
        asked.setdefault(r['id'], []).append((f, cu, how, r))


def of_record(i):
    rows = asked[i]
    keys_case = any(r['status'] == 'blocked' and 'ask by keys' in (r.get('reason') or '') for _, _, how, r in rows if how == 'COM')
    pick = [x for x in rows if (x[2] == 'keys' if keys_case else True)]
    return (pick or rows)[-1]


def excel(r):
    """Excel's answer: the double from "number", otherwise value2 and its kind."""
    x = r.get('excel') or {}
    if not x:
        return '(none)'
    if x.get('kind') == 'number':
        return x.get('number')
    if x.get('kind') == 'text':
        return f"`{x.get('value2')}` (text)"
    if x.get('kind') == 'blank':
        return 'an empty cell'
    return f"`{x.get('value2')}`"


def text(r):
    return f"`{(r.get('excel') or {}).get('text')}`"


def expected(r):
    e = r.get('expected')
    if e is None:
        return '(none)'
    if set(e) == {'value2'}:
        v = e['value2']
        return repr(v) if isinstance(v, float) else f'`{v}`'
    return '`' + json.dumps(e, ensure_ascii=False) + '`'


def ulps(r):
    """How far apart Excel's double and the expected one are, in units in the last place, when both are numbers."""
    x, e = r.get('excel') or {}, (r.get('expected') or {}).get('value2')
    if x.get('kind') != 'number' or not isinstance(e, (int, float)) or isinstance(e, bool):
        return ''
    a, b = float(x['number']), float(e)
    if a == b:
        return '0'
    import math
    n = 0
    lo, hi = min(a, b), max(a, b)
    while lo < hi and n < 1_000_000:
        lo = math.nextafter(lo, math.inf)
        n += 1
    return f'{n} ({abs(a - b):.2g})' if n < 1_000_000 else f'{abs(a - b):.3g}'


def formula(r):
    """The checked cell's Formula; for a cell a spill reaches, the Formula that spills into it."""
    f = (r.get('excel') or {}).get('formula')
    if f:
        return f'`{f}`'
    c = before[r['id']]
    anchors = [(k, v) for k, v in (c.get('cells') or {}).items() if isinstance(v, str) and v.startswith('=')]
    return f"{c['check']}, spilled by {', '.join(f'`{v}` in {k}' for k, v in anchors)}" if anchors else ''


ids = sorted(asked, key=lambda i: (before[i]['_file'], i))
status = {i: of_record(i)[3]['status'] for i in ids}

print('## Every disagreement\n')
print('| Case | File | Source before | Formula | Excel | Excel\'s text | Expected | Units in the last place | What differs |')
print('|---|---|---|---|---|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] != 'disagree':
        continue
    print(f"| {i} | `{before[i]['_file']}` | {before[i]['source']} | {formula(r)} | {excel(r)} | {text(r)} | {expected(r)} | {ulps(r)} | "
          + '; '.join(r.get('differences') or []).replace('|', '\\|') + ' |')
print()

print('## Blocked, and why\n')
print('| Case | File | Source | Why |')
print('|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] == 'blocked':
        print(f"| {i} | `{before[i]['_file']}` | {before[i]['source']} | {(r.get('reason') or '').replace('|', '\\|')} |")
print()

print('## Recorded (the engine refuses by decision; Excel\'s answer is read, not compared)\n')
print('| Case | File | Formula | Excel | Excel\'s text | The engine | Decision |')
print('|---|---|---|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] == 'recorded':
        print(f"| {i} | `{before[i]['_file']}` | {formula(r)} | {excel(r)} | {text(r)} | `{json.dumps(r.get('engineExpect'), ensure_ascii=False)}` | "
              f"{(r.get('engineDiffersByDecision') or '')[:90]}… |")
print()

print('## Agreed and promoted\n')
print('| Case | File | Formula | Excel | Excel\'s text |')
print('|---|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] == 'agree' and before[i]['source'] != 'observed' and after[i]['source'] == 'observed':
        print(f"| {i} | `{before[i]['_file']}` | {formula(r)} | {excel(r)} | {text(r)} |")
print()

print('## Every case the pass asked, by file\n')
print('Each case once, by its pass of record. A case promoted is one whose `source` this pass rewrote to `observed`.\n')
print('| File | agreed | of which promoted | disagreed | blocked | recorded |')
print('|---|---|---|---|---|---|')
byfile = collections.defaultdict(lambda: collections.defaultdict(list))
for i in ids:
    byfile[before[i]['_file']][status[i]].append(i)
for n, d in sorted(byfile.items()):
    prom = [i for i in d['agree'] if before[i]['source'] != 'observed' and after[i]['source'] == 'observed']
    print(f"| `{n}` | {len(d['agree'])} | {', '.join(prom) or '–'} | {', '.join(d['disagree']) or '–'} | "
          f"{', '.join(d['blocked']) or '–'} | {', '.join(d['recorded']) or '–'} |")
print()

print('## The passes\n')
print('| Results file | Regional format | How | agree | disagree | blocked | recorded |')
print('|---|---|---|---|---|---|---|')
for f, cu, how in PASSES:
    j = json.load(open(os.path.join(O, f), encoding='utf-8-sig'))
    s = j['summary']
    print(f"| `{f}` | {j['windowsRegionalFormat']} | {how} | {s['agree']} | {s['disagree']} | {s['blocked']} | {s['recorded']} |")
print()

moved = collections.Counter((before[i]['source'], after[i]['source']) for i in before if i in after and before[i]['source'] != after[i]['source'])
print('Sources rewritten by `-Update`: ' + (', '.join(f'{a} → {b}: {n}' for (a, b), n in sorted(moved.items())) or 'none') + '.')
still = [i for i in ids if after[i]['source'] != 'observed']
print(f'\nStill not `observed` after the pass ({len(still)}): ' + ', '.join(f"{i} ({after[i]['source']}, {status[i]})" for i in still) + '.')
