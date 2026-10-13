"""The inked column runs of ink.ps1's output, in screen x, and the rows the last run (a line's last glyph) inks.

    powershell -File ink.ps1 <png> <x0> <y0> <x1> <y1> | python3 inkruns.py <x0> <y0>
"""
import sys

x0, y0 = int(sys.argv[1]), int(sys.argv[2])
rows = [l.rstrip('\r\n').split(' ', 1)[1] if ' ' in l.strip() else '' for l in sys.stdin if l.strip()]
rows = [r.strip() for r in rows]
w = max(len(r) for r in rows)
cols = [x for x in range(w) if any(len(r) > x and r[x] == '#' for r in rows)]
runs, s, p = [], None, None
for x in cols:
    if s is None:
        s = x
    elif x != p + 1:
        runs.append((s + x0, p + x0))
        s = x
    p = x
runs.append((s + x0, p + x0))
a, b = runs[-1][0] - x0, runs[-1][1] - x0
print('runs', runs)
print('last glyph rows', [i + y0 for i, r in enumerate(rows) if '#' in r[a:b + 1]])
