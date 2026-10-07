import json
R='/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text/verification/2026-10-06-macos-paint-text-cost/raw/'
contended={l.split()[0] if not l.startswith('browser') else l.split()[1] for l in open(R+'contended-runs.txt') if l.strip() and not l.startswith('#')}
recs=json.load(open(R+'browser-scroll.json'))
out=[]
for which, title in (('live','**`/grid-live-local`**, 18 painted rows × 11 columns'),('wide','**`/wide`**, 22 painted rows × 10 columns')):
    rs=[r for r in recs if r.get('which')==which]
    out.append(f"{title}. Run number: least / median / most task ms per step. Runs marked \\* were overlapped by another agent's work and are not read.\n")
    out.append("| Scenario | V0 runs | V1 runs | Frames, V0 clean runs | Frames, V1 clean runs |")
    out.append("|---|---|---|---|---|")
    for n in [x['scenario'] for x in rs[0]['results']]:
        cells={'v0':[], 'v1':[]}; fr={'v0':[], 'v1':[]}
        for r in rs:
            lbl=r['label'].split(' ')[0]; v=lbl.split('-')[-1]; run=lbl.split('-')[2]
            s=next(x for x in r['results'] if x['scenario']==n); t=s['taskMsPerStep']
            mark='\\*' if lbl in contended else ''
            cells[v].append(f"{run}{mark}: {t['min']:.2f} / {t['median']:.2f} / {t['max']:.2f}")
            if not mark: fr[v].append(f"{s['intervalP50']['median']:.1f} / {s['intervalP95']['median']:.1f} / {s['intervalMax']['median']:.1f}")
        out.append(f"| {n} | {'<br>'.join(cells['v0'])} | {'<br>'.join(cells['v1'])} | {'<br>'.join(fr['v0'])} | {'<br>'.join(fr['v1'])} |")
    out.append("")
print('\n'.join(out))
