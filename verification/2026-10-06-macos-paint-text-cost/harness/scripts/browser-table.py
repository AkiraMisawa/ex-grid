import json, os, statistics
R='/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text/verification/2026-10-06-macos-paint-text-cost/raw/'
p = R + 'browser-scroll.json'
if os.path.exists(p):
    recs = json.load(open(p))
    for which in ('live', 'wide'):
        rs = [r for r in recs if r.get('which') == which]
        if not rs: continue
        print(f"\n## scroll, {which}: {rs[0]['geometry']}")
        names = [x['scenario'] for x in rs[0]['results']]
        print("| Scenario | variant (run) | task ms/step min/med/max | script ms/step med | frame p50 / p95 / max (med of runs) | frames >16.7 / >33.4 (med) | longest task ms (max) | settle ms (med) | errors |")
        for name in names:
            for r in rs:
                s = next(x for x in r['results'] if x['scenario'] == name)
                t = s['taskMsPerStep']; sc = s['scriptMsPerStep']
                settle = s['settledMs']['median'] if s['settledMs'] else None
                print(f"| {name} | {r['label']} | {t['min']:.2f} / {t['median']:.2f} / {t['max']:.2f} | {sc['median']:.2f} | {s['intervalP50']['median']:.1f} / {s['intervalP95']['median']:.1f} / {s['intervalMax']['median']:.1f} | {s['over17']['median']} / {s['over34']['median']} | {s['longestTaskMs']['max']:.0f} | {settle if settle is None else round(settle)} | {len(r['consoleErrors'])} |")
p = R + 'browser-live.json'
if os.path.exists(p):
    recs = json.load(open(p))
    print("\n## live update, /grid-live-local?rows=1000000&batch=1000&interval=0")
    print("| variant (run) | applied ms min/med/max | apply->frame min/med/max | apply->painted min/med/max | longest task med/max | grid changed | errors |")
    for r in recs:
        f = lambda k: f"{r[k]['min']:.1f} / {r[k]['median']:.1f} / {r[k]['max']:.1f}"
        print(f"| {r['label']} | {f('appliedMs')} | {f('applyToFrameMs')} | {f('applyToPaintedMs')} | {r['longestTaskMs']['median']} / {r['longestTaskMs']['max']} | {r['gridChanged']} | {len(r['consoleErrors'])} |")
