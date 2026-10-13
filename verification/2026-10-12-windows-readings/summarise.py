"""The readings' tables for report.md, from records/r1.jsonl, r4.jsonl, r6.jsonl, r8.jsonl and part-e/records.

    python3 summarise.py > summary.md
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))


def rows(name):
    return [json.loads(l) for l in open(os.path.join(HERE, 'records', name), encoding='utf-8')]


def q(s):
    return '`' + str(s).replace('|', '\\|') + '`' if s not in (None, '') else '(empty)'


print('## R1\n')
print('| Case | Step | Keys | Mode | Bar (UI Automation) | Selected in the bar | Committed | Ctrl+C | Shift+Home, Ctrl+C | Cells after |')
print('|---|---|---|---|---|---|---|---|---|---|')
for r in rows('r1.jsonl'):
    if r['case'] == 'start':
        continue
    b = (r.get('bar') or [{}])[0]
    sel = ', '.join(x for x in (b.get('selection') or []) if x) or ''
    cells = ''
    if r.get('cells'):
        cells = '; '.join(f"{k} {v.split(' | ')[0]}" for k, v in r['cells'].items() if not v.startswith('empty'))
        cells += f" — {r['ready'].split('ready=True ')[1]}"
    print(f"| {r['case']} | {r['step']} | `{r['keys']}` | {r.get('mode')} | {q(b.get('text'))} | {q(sel)} | {r.get('committed', '')} | "
          f"{q(r.get('selected')) if 'selected' in r else ''} | {q(r.get('toCaret')) if 'toCaret' in r else ''} | {cells} |")
print()

print('## R4\n')
print('| Zoom | Case | Active | Selection | Left edge | Right edge | Top edge | Bottom edge |')
print('|---|---|---|---|---|---|---|---|')
for r in rows('r4.jsonl'):
    if 'lines' not in r:
        continue
    L = r['lines']
    f = lambda k: ' '.join(re.sub(r'^\d+,\d+ ', '', x) for x in L[k])
    print(f"| {r['zoom']}% | {r['case']} | {r['active']} | {r['selection']} | {f('left')} | {f('right')} | {f('top')} | {f('bottom')} |")
print()

print('## R6\n')
print('| Case | Step | Bold | Italic | Underline | A1 shows | Column A width | Standard width used |')
print('|---|---|---|---|---|---|---|---|')
for r in rows('r6.jsonl'):
    if r['step'] == 'start':
        continue
    lk, cw = r.get('look') or '', r.get('colwidth') or ''
    m = re.search(r'bold (\S+) italic (\S+) underline (\S+)', lk)
    t = re.search(r'text ("[^"]*")', lk)
    w = re.search(r'A1: width (\S+) \| useStandardWidth (\S+)', cw)
    print(f"| {r['case']} | {r['step']} | {m.group(1)} | {m.group(2)} | {m.group(3)} | `{t.group(1)[1:-1]}` | {w.group(1)} | {w.group(2)} |")
print()

print('## R8\n')
print('| Case | Selection | Edges set (cell, edge, LineStyle, Weight) | What each cell holds after Escape |')
print('|---|---|---|---|')
by = {}
for r in rows('r8.jsonl'):
    by.setdefault(r['case'], []).append(r)
for c, rs in by.items():
    a = rs[0]; b = rs[-1]
    held = ' ;; '.join(re.sub(r'bold .*?\| (top=.*?) \| width.*', r'\1', x) for x in b['look'][len('ok look '):].rsplit(' t=', 1)[0].split(' ;; '))
    print(f"| {c} | {a['selection']} | {', '.join('/'.join(map(str, e)) for e in a['edges']) or 'none'} | {held} |")
print()

print('## R3 (a) and (c): the cells\n')
x = {i['id']: i for i in json.load(open(os.path.join(HERE, 'part-e', 'records', 'excel-r3.json'), encoding='utf-8-sig'))}
e = {i['id']: i for i in json.load(open(os.path.join(HERE, 'part-e', 'records', 'expivot-r3-wide.json'), encoding='utf-8'))['items']}
for i in ('e05c', 'e16b'):
    xs_ = [[c['text'] for c in row] for row in x[i]['cells']]
    rep = e[i]['report']
    es = [[h['text'] for h in sorted(rep['headers'], key=lambda h: h['col'])]] + [r['cells'] for r in rep['rows']]
    for side, t in (('Excel', xs_), ('ExPivot', es)):
        print(f'**{i}, {side}**\n')
        w = max(len(r) for r in t)
        print('| ' + ' | '.join(q(c) if c != '' else ' ' for c in t[0] + [''] * (w - len(t[0]))) + ' |')
        print('|' + '---|' * w)
        for r in t[1:]:
            print('| ' + ' | '.join(q(c) if c != '' else ' ' for c in r + [''] * (w - len(r))) + ' |')
        print()
    same = [[a == b for a, b in zip(ra, rb)] for ra, rb in zip(xs_[1:], es[1:])]
    print(f"Body cells the same, row by row: {[all(r) for r in same]}; the headers that differ: "
          f"{[(a, b) for a, b in zip(xs_[0], es[0]) if a != b]}\n")
