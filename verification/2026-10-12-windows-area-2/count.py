"""The area pass's counts: every case of the files the pass asks about, by `source` and by `culture`, per file.

    python3 count.py <ExcelCases dir> <area list, comma-separated>   (markdown on stdout)
"""
import collections
import json
import os
import sys

cases_dir, areas = sys.argv[1], sys.argv[2].split(',')
print('| File | cases | observed | documented | uncertain | cultures |')
print('|---|---|---|---|---|---|')
total = collections.Counter()
for a in areas:
    d = json.load(open(os.path.join(cases_dir, a + '.json'), encoding='utf-8'))
    s = collections.Counter(c.get('source') for c in d['cases'])
    cu = collections.Counter(c.get('culture', 'none') for c in d['cases'])
    total.update(s); total['cases'] += len(d['cases'])
    print(f"| `{a}` | {len(d['cases'])} | {s['observed']} | {s['documented']} | {s['uncertain']} | "
          + ', '.join(f'{k} {v}' for k, v in sorted(cu.items())) + ' |')
print(f"| **all {len(areas)}** | {total['cases']} | {total['observed']} | {total['documented']} | {total['uncertain']} | |")
# The whole corpus, for the cases the pass leaves out.
everything = collections.Counter(); outside = []
for n in sorted(os.listdir(cases_dir)):
    d = json.load(open(os.path.join(cases_dir, n), encoding='utf-8'))
    if 'cases' not in d:
        continue
    for c in d['cases']:
        everything[c.get('source')] += 1
        if n[:-5] not in areas and c.get('source') != 'observed':
            outside.append(f"{c['id']} ({n}, {c.get('source')})")
print()
print('The whole corpus: ' + ', '.join(f'{k} {v}' for k, v in sorted(everything.items())) + '.')
print('Not `observed` and outside the pass: ' + (', '.join(outside) if outside else 'none') + '.')
