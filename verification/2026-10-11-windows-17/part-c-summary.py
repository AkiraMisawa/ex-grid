"""Part C's tables for report.md, from records/part-c-excel.jsonl and records/part-c-sheet-*.jsonl.

For each case and number of F4 presses: the seventh run's reading, Excel's text (and where its caret or
selection was), and ExSheet's in each configuration and surface. A cell is the text and, in brackets, the
selection: [n] a caret after n characters, [a-b] a selection from a to b; "committed" when the keys left no
edit open (the arrow or Home of Enter mode committed the entry before F4).

    python3 part-c-summary.py > part-c-summary.md
"""
import glob
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
R = os.path.join(HERE, 'records')
READING = {
    (1, 1): '=$B$2', (1, 2): '=B$2', (1, 3): '=$B2', (1, 4): '=B2', (2, 1): '=$A$1+B2', (3, 1): '=$A$1+B2', (4, 1): '=$A$1+B2',
    (5, 1): '=SUM($A$1:$B$2)', (5, 2): '=SUM(A$1:B$2)', (5, 3): '=SUM($A1:$B2)', (5, 4): '=SUM(A1:B2)', (6, 1): '=A1:B2', (7, 1): '=$A$1+$B$2 (the span selected)',
    (8, 1): '=SUM($A:$A)', (8, 2): '=SUM(A:A)', (8, 3): '=SUM($A:$A)', (9, 1): '=SUM($1:$1)', (9, 2): '=SUM(1:1)', (9, 3): '=SUM($1:$1)',
    (10, 1): '=Sheet2!$A$1', (11, 1): 'unchanged', (12, 1): 'unchanged', (13, 1): 'unchanged', (14, 1): 'unchanged',
    (15, 1): '=$A$2', ('15b', 1): '=A3 (or =$A$3: record which)', (16, 1): '=$A$2+, pointing ends', (18, 1): '=$A$1+$B$1', (19, 1): '=$A$1+$B$2',
    (20, 1): '=$A$1+$B$2', (21, 1): '=$b$2', (22, 1): '=$B$2:$A$1'}


def sel(text, a, b):
    return f'{text} [{a}]' if a == b else f'{text} [{a}-{b}]'


def excel():
    out = {}
    for l in open(os.path.join(R, 'part-c-excel.jsonl'), encoding='utf-8'):
        r = json.loads(l)
        if r.get('presses') is None:
            continue
        key = (r['case'], r['variant'], r['presses'])
        if r.get('committed'):
            out[key] = 'committed'
            continue
        whole, to, s = r['whole'] or '', r['toCaret'] or '', r['selected'] or ''
        if s:
            a = whole.find(s) if whole.find(s) >= 0 else len(to)
            out[key] = sel(whole, a, a + len(s))
        else:
            out[key] = sel(whole, len(to), len(to))
    return out


def sheet():
    out = {}
    for f in sorted(glob.glob(os.path.join(R, 'part-c-sheet-*.jsonl'))):
        if f.endswith('-pg.jsonl'):
            continue
        cfg = os.path.basename(f)[len('part-c-sheet-'):-len('.jsonl')]
        for l in open(f, encoding='utf-8'):
            r = json.loads(l)
            if r.get('presses') is None:
                continue
            s = r['state']
            surf = s['editor'] if r['surface'] == 'cell' else s['bar']
            v = 'committed' if not s['editing'] else sel(surf['value'], surf['sel'][0], surf['sel'][1])
            out[(r['case'], r['surface'], r['variant'], r['presses'], cfg)] = v
    return out


ex, sh = excel(), sheet()
cfgs = sorted({k[4] for k in sh})
print('| Case | F4 | Reading | Excel, typed | Excel, F2 first | ExSheet, cell typed | ExSheet, cell F2 first | ExSheet, Formula Bar | Configurations agreeing |')
print('|---|---|---|---|---|---|---|---|---|')
for (c, n), reading in READING.items():
    def one(surface, variant):
        vals = {cfg: sh.get((c, surface, variant, n, cfg)) for cfg in cfgs}
        got = {v for v in vals.values() if v is not None}
        if not got:
            return '—', ''
        if len(got) == 1:
            return got.pop(), 'all'
        return ' / '.join(f'{cfg}: {v}' for cfg, v in vals.items()), 'differ'
    a, aa = one('cell', 'typed'); b, bb = one('cell', 'f2'); d, dd = one('bar', 'typed')
    agree = 'all' if {aa, bb, dd} <= {'all', ''} else 'differ'
    print(f'| {c} | {n} | `{reading}` | `{ex.get((c, "typed", n), "—")}` | `{ex.get((c, "f2", n), "—")}` | `{a}` | `{b}` | `{d}` | {agree} ({", ".join(cfgs)}) |')
