# Prints the tables of README.md from raw/drag-cost.json and raw/scroll-layers.json.gz (run-scroll.mjs
# writes it uncompressed; it is kept gzipped, read with zcat).
#     python3 tables.py
import gzip
import json
import pathlib

here = pathlib.Path(__file__).parent


def quantile(values, q):
    s = sorted(values)
    at = (len(s) - 1) * q
    low = int(at)
    high = min(low + 1, len(s) - 1)
    return round(s[low] + (s[high] - s[low]) * (at - low), 1)


drag = json.loads((here / 'raw' / 'drag-cost.json').read_text())
print('| Host | Start, pinned, Chrome | n per side | Before: task (IQR) | After: task (IQR) | Before: style | After: style | Before: script | After: script |')
print('|---|---|---|---|---|---|---|---|---|')
for c in drag['comparisons']:
    before, after = c['sides']['before']['rows'], c['sides']['after']['rows']
    start = 'A2' if c['start'] == 'r1c0' else 'B2'
    chrome = 'MudBlazor' if c['chrome'] == 'mud' else 'built-in'
    m = lambda rows, k: quantile([r[k] for r in rows], 0.5)
    iqr = lambda rows: f"{quantile([r['task'] for r in rows], 0.25)}-{quantile([r['task'] for r in rows], 0.75)}"
    print(f"| {c['host']} | {start}, pin={c['pin']}, {chrome} | {len(before)} / {len(after)} | {m(before, 'task')} ({iqr(before)}) | {m(after, 'task')} ({iqr(after)}) | {m(before, 'style')} | {m(after, 'style')} | {m(before, 'script')} | {m(after, 'script')} |")

print()
scroll = json.loads(gzip.decompress((here / 'raw' / 'scroll-layers.json.gz').read_bytes()))
runs = [r for r in scroll['runs'] if 'failed' not in r]
print('| Host | Scale | Chrome | Build | Layers, A1 / column A | Column A, down: median, p95, max | Column A, across: median, p95, max | A1, down: median, p95 |')
print('|---|---|---|---|---|---|---|---|')
for host in ['server', 'wasm']:
    for scale in [1, 1.5]:
        for chrome in ['builtin', 'mud']:
            for label in ['before', 'after']:
                group = [r for r in runs if r['host'] == host and r['scale'] == scale and r['chrome'] == chrome and r['label'] == label]
                if not group:
                    continue
                layers = sorted({(r['oneCell']['layers']['count'], r['columnA']['layers']['count']) for r in group})
                pooled = lambda which, axis: [v for r in group for v in r[which]['frames'][axis]['raw']]
                stat = lambda values: f"{quantile(values, 0.5)}, {quantile(values, 0.95)}, {max(values)}"
                print(f"| {host} | {int(scale * 100)}% | {'MudBlazor' if chrome == 'mud' else 'built-in'} | {label} | {', '.join(f'{a} / {b}' for a, b in layers)} | {stat(pooled('columnA', 'down'))} | {stat(pooled('columnA', 'across'))} | {quantile(pooled('oneCell', 'down'), 0.5)}, {quantile(pooled('oneCell', 'down'), 0.95)} |")
