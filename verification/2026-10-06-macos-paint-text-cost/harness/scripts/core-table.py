import json, sys
R='/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text/verification/2026-10-06-macos-paint-text-cost/raw/'
def load(n): return json.load(open(R+f'core-{n}.json'))
def f(x, d=3): return f"{x:.{d}f}"
for kind, pairs in (('grid', [('grid-v0','grid-v1'),('grid-v0-r2','grid-v1-r2')]), ('pivot', [('pivot-v0','pivot-v1')])):
    for a, b in pairs:
        A, B = load(a), load(b)
        print(f"\n## {kind}: {a} vs {b}; load {A['machine']['loadavg']} / {B['machine']['loadavg']}")
        print("| Scenario | NotePaint V0 µs (min/med/max) | NotePaint V1 µs | bytes V0 | bytes V1 | render V0 ms (min/med) | render V1 ms (min/med) | share V1 (min) | rows rendered V0/V1 | new paints |")
        for ra, rb in zip(A['measured']['results'], B['measured']['results']):
            ma, mb = ra['measured'], rb['measured']
            assert ra['scenario']==rb['scenario']
            pa, pb = ma['notePaintAlone'], mb['notePaintAlone']
            us = lambda s: f"{s['Min']*1000:.1f} / {s['Median']*1000:.1f} / {s['Max']*1000:.1f}"
            rows = f"{ma['rowsRendered']['Min']:.0f}-{ma['rowsRendered']['Max']:.0f} / {mb['rowsRendered']['Min']:.0f}-{mb['rowsRendered']['Max']:.0f} (+{ma['rowsRenderedAfter']['Max']:.0f}/+{mb['rowsRenderedAfter']['Max']:.0f})"
            print(f"| {ra['scenario']} | {us(pa)} | {us(pb)} | {ma['notePaintAloneBytes']['Median']:.0f} | {mb['notePaintAloneBytes']['Median']:.0f} | {f(ma['render']['Min'])} / {f(ma['render']['Median'])} | {f(mb['render']['Min'])} / {f(mb['render']['Median'])} | {mb['paintShareOfRenderAtLeast']*100:.1f}% | {rows} | {ma['newPaints']['Median']:.0f}/{mb['newPaints']['Median']:.0f} |")
            # step bytes too
        print("step bytes (median) V0/V1:", [ (r['scenario'][:2], int(r['measured']['stepBytes']['Median']), int(q['measured']['stepBytes']['Median'])) for r,q in zip(A['measured']['results'], B['measured']['results'])])
