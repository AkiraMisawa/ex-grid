import json, os, statistics, sys
R='/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text/verification/2026-10-06-macos-paint-text-cost/raw/'
def med(xs): return statistics.median(xs) if xs else float('nan')
def summary(name):
    p=R+f'core-{name}.json'
    if not os.path.exists(p): return None
    d=json.load(open(p))['measured']; rows=d['rows']
    out=[]
    for r in rows:
        gcs=r.get('collections',[])
        comp=[g for g in gcs if g['Compacting']]; sweep=[g for g in gcs if g['Compacting'] is False]
        out.append(dict(
            ms=r['redrawMs'], pause=r['gcPauseMs'], n=len(gcs),
            g0=sum(1 for g in gcs if g['Generation']==0), g1=sum(1 for g in gcs if g['Generation']==1), g2=sum(1 for g in gcs if g['Generation']==2),
            ncomp=len(comp), pcomp=sum(g['pauseMs'] for g in comp), psweep=sum(g['pauseMs'] for g in sweep),
            gen1pause=sum(g['pauseMs'] for g in gcs if g['Generation']==1),
            fl=sum((g['freeListAllocatedMB'] or 0) for g in gcs), eos=sum((g['endOfSegAllocatedMB'] or 0) for g in gcs),
            prom1=sum(g['promotedMB'][1] for g in gcs if g['promotedMB']),
            gen2=(gcs[-1]['generationSizesMB'][2] if gcs else None),
            frag=(r['lastEphemeralInRedraw'] or {}).get('fragmentedMB'),
            commit0=r['afterPreCollection']['committedMB'], commit1=(r['lastInRedraw'] or {}).get('committedMB'),
            ws0=r['workingSetBeforeMB'], ws1=r['workingSetAfterMB']))
    return d, out
def line(name):
    s=summary(name)
    if s is None: return f"| {name} | (missing) |"
    d,o=s
    f=lambda k: med([x[k] for x in o])
    mn=lambda k: min(x[k] for x in o)
    return (f"| {name} | {mn('ms'):.1f} / {f('ms'):.1f} / {max(x['ms'] for x in o):.1f} | {f('pause'):.1f} | {f('g0'):.0f}+{f('g1'):.0f}+{f('g2'):.0f} | "
            f"{sum(x['ncomp'] for x in o)} in {sum(1 for x in o if x['ncomp'])} of {len(o)} | {f('pcomp'):.1f} / {f('psweep'):.1f} | {f('fl'):.1f} / {f('eos'):.1f} | {f('prom1'):.1f} | {f('gen2'):.0f} | {f('frag'):.1f} | {f('commit0'):.0f} → {f('commit1'):.0f} | {f('ws1')-f('ws0'):+.0f} |")
print("| Run | redraw ms least / median / most | GC pause median | GCs gen0+1+2 (median) | compacting GCs (runs with one) | pause in compacting / sweeping GCs, median ms | promoted into free list / end of segment, MB | gen1 promoted MB | gen2 MB after | fragmented MB after | committed MB before → after | working set Δ MB |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
for n in sys.argv[1:]:
    print(line(n))
