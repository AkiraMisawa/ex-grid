"""Part D's table for report.md, from records/part-d-sheet-<host>-<browser>.jsonl: per page, case and step, the
Sheet's KeyTips, where the keyboard is, whether the document kept focus, the edit, and what each d4 letter
changed on D10 (it holds 1234.5), the same in every configuration or each configuration's own.

    python3 part-d-summary.py
"""
import glob
import json
import os
from collections import defaultdict

R = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'records')
rows = defaultdict(dict)
order = []
for f in sorted(glob.glob(os.path.join(R, 'part-d-sheet-*.jsonl'))):
    if f.endswith('-pg.jsonl'):
        continue
    cfg = os.path.basename(f)[len('part-d-sheet-'):-len('.jsonl')]
    for l in open(f, encoding='utf-8'):
        r = json.loads(l)
        if r['case'] in ('start', 'console'):
            if r['case'] == 'console':
                rows[(r['page'], 'console', '')][cfg] = ', '.join(sorted({m['type'] for m in r['messages']})) or 'none'
            continue
        key = (r['page'], r['case'], r['step'])
        if key not in order:
            order.append(key)
        st = r.get('state')
        if st:
            v = (f"keyboard {st['keyboard']}; focus {'kept' if st['hasFocus'] else 'LOST'}; {'edit open' if st['editing'] else 'no edit'}; "
                 f"toolbar {'shown' if st['toolbar'] else 'hidden'}; KeyTips {' '.join(t['tip'] for t in st['keytips']) or 'none'}; "
                 f"D10 {st['d10']['text'] if st['d10'] else '?'}; front: {'the browser' if 'run18' in r.get('front', '') else r.get('front', '')[:40]}")
        elif 'changed' in r:
            ch = r['changed']
            parts = []
            for k, (a, b) in ch.items():
                if k == 'd10':
                    parts.append('D10 ' + ', '.join(f'{kk} {a.get(kk)} -> {vv}' for kk, vv in (b or {}).items() if (a or {}).get(kk) != vv))
                elif k == 'dialogs':
                    parts.append('opened ' + ' / '.join(x[:24] for x in b if x not in (a or [])))
                else:
                    parts.append(f'{k} {a} -> {b}')
            v = '; '.join(parts) or 'no change'
        elif 'd10' in r:
            v = 'undone: D10 ' + (r['d10'] or {}).get('text', '?')
        else:
            v = json.dumps(r)[:120]
        rows[key][cfg] = v
cfgs = sorted({c for d in rows.values() for c in d})
print('| Page | Case | Step | ' + ' | '.join(['Read (the same in every configuration unless listed)']) + ' |')
print('|---|---|---|---|')
for key in order + [k for k in rows if k[1] == 'console']:
    d = rows[key]
    vals = set(d.values())
    text = d[cfgs[0]] if len(vals) == 1 and len(d) == len(cfgs) else ' // '.join(f'{c}: {v}' for c, v in d.items())
    print(f'| {key[0]} | {key[1]} | {key[2]} | {text} |')
