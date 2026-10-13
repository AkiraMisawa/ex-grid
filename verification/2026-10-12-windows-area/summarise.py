"""The area pass's tables for report.md, read from the results files the pass wrote and the corpus before and
after it.

    python3 summarise.py <repo> <base commit> > summary.md

The pass of record for a case is the last one, in the order below, that asked it the way the procedure names
for it: the keys pass for an "ask by keys" case, the passes under its own culture for a case that names one,
the en-US COM pass otherwise.
"""
import collections
import json
import os
import subprocess
import sys

repo, base = sys.argv[1], sys.argv[2]
O = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelOracle')
C = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelCases')
P = 'results-2026-10-12-area'
PASSES = [  # (file, culture, how)
    (f'{P}-com-en-US.json', 'en-US', 'COM'),
    (f'{P}-keys-en-US.json', 'en-US', 'keys'),
    (f'{P}-com-de-DE.json', 'de-DE', 'COM'),
    (f'{P}-keys-de-DE.json', 'de-DE', 'keys'),
    (f'{P}-com-ja-JP.json', 'ja-JP', 'COM'),
    (f'{P}-keys-ja-JP.json', 'ja-JP', 'keys'),
]


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
    culture = before[i].get('culture')
    pick = [x for x in rows if (x[1] == culture if culture else x[1] == 'en-US') and (x[2] == 'keys' if keys_case else True)]
    return (pick or rows)[-1]


def answer(r):
    x = r.get('excel') or {}
    if not x:
        return '(none)'
    s = f"Value2 `{json.dumps(x.get('value2'), ensure_ascii=False)}` ({x.get('kind')}), text `{x.get('text')}`"
    if x.get('columnWidth') is not None and ('width' in json.dumps(r.get('expected') or {}) or 'widens' in json.dumps(r.get('expected') or {})):
        s += f", column width {x.get('columnWidth')}"
    return s


def expected(r):
    e = r.get('expected')
    return '`' + json.dumps(e, ensure_ascii=False) + '`' if e is not None else '(none)'


ids = sorted(asked, key=lambda i: (before[i]['_file'], i))
status = {i: of_record(i)[3]['status'] for i in ids}

print('## The cases ExSheet decides money by: NPV-001, NPV-002 and every PMT case\n')
print('| Case | Source before | Formula | Outcome | Excel | Expected |')
print('|---|---|---|---|---|---|')
for i in [i for i in ids if i in ('NPV-001', 'NPV-002') or i.startswith('PMT-')]:
    f, cu, how, r = of_record(i)
    formula = (r.get('excel') or {}).get('formula') or ''
    print(f"| {i} | {before[i]['source']} | `{formula}` | **{r['status']}** | {answer(r)} | {expected(r)} |")
print()

print('## Every disagreement\n')
print('| Case | File | Source before | Formula | Excel | Expected (`' + 'expect` or `excelExpect`) | What differs |')
print('|---|---|---|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] != 'disagree':
        continue
    formula = (r.get('excel') or {}).get('formula') or ''
    print(f"| {i} | `{before[i]['_file']}` | {before[i]['source']} | `{formula}` | {answer(r)} | {expected(r)} ({r.get('comparedWith')}) | "
          + '; '.join(r.get('differences') or []).replace('|', '\\|') + ' |')
print()

print('## Blocked, and why\n')
print('| Case | File | Source | Why |')
print('|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] == 'blocked':
        print(f"| {i} | `{before[i]['_file']}` | {before[i]['source']} | {r.get('reason')} |")
print()

print('## Recorded (Excel answers, the engine refuses by decision; nothing is compared)\n')
print('| Case | File | Excel | The engine | Decision |')
print('|---|---|---|---|---|')
for i in ids:
    f, cu, how, r = of_record(i)
    if r['status'] == 'recorded':
        print(f"| {i} | `{before[i]['_file']}` | {answer(r)} | `{json.dumps(r.get('engineExpect'), ensure_ascii=False)}` | {r.get('engineDiffersByDecision') or ''} |")
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
    print(f"| `{n}` | {', '.join(d['agree']) or '–'} | {', '.join(prom) or '–'} | {', '.join(d['disagree']) or '–'} | "
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
print('Sources rewritten by `-Update`: ' + ', '.join(f'{a} → {b}: {n}' for (a, b), n in sorted(moved.items())) + '.')
still = [i for i in ids if after[i]['source'] != 'observed']
print(f'\nStill not `observed` after the pass ({len(still)}): ' + ', '.join(f"{i} ({after[i]['source']}, {status[i]})" for i in still) + '.')
