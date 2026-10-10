# Prints the record's tables from the raw files. Run with the record's raw directory as the argument.
import json, statistics as st, sys, os
R = sys.argv[1]
def load(p): return json.load(open(os.path.join(R, p)))
f2 = lambda x: f"{x:,.2f}"
f1 = lambda x: f"{x:,.1f}"
f0 = lambda x: f"{x:,.0f}"

print("## pivot whole update: median ms per round (r1 / r2 at 1 ms between batches; r3 at 5 s)")
data = {}
for r in (1, 2, 3):
    for side in ("main", "branch"):
        for e in load(f"pivot/pivot-r{r}-{side}.json"):
            for b in e["batches"]:
                runs = b["runs"]
                key = (e["reportRows"], b["changes"])
                data.setdefault(key, {}).setdefault(side, []).append(dict(
                    r=r, med=st.median(x["applyThroughRenderMs"] for x in runs),
                    alloc=st.median(x["allocatedBytes"] for x in runs) / 2**20,
                    pause=sum(x["gcPauseMs"] for x in runs) / len(runs),
                    g2=sum(x["gen2"] for x in runs)))
print("| Report rows | Changes | main, median ms | branch, median ms | main / branch | Allocated per update, MiB: main → branch | Collector pause per update, ms: main → branch |")
print("|---|---|---|---|---|---|---|")
for (rows, k), v in sorted(data.items()):
    m = v["main"]; b = v["branch"]
    ratio = st.median(x["med"] for x in m) / st.median(x["med"] for x in b)
    print(f"| {rows:,} | {k:,} | {' / '.join(f2(x['med']) for x in m)} | {' / '.join(f2(x['med']) for x in b)} | {ratio:,.0f}× | "
          f"{f1(st.median(x['alloc'] for x in m))} → {f1(st.median(x['alloc'] for x in b))} | "
          f"{f0(st.median(x['pause'] for x in m))} → {f0(st.median(x['pause'] for x in b))} |")

print("\n## first report ms and live heap after the updates MiB")
print("| Report rows | First report, ms: main | branch | Heap after the updates, MiB: main | branch |")
print("|---|---|---|---|---|")
first = {}
for r in (1, 2, 3):
    for side in ("main", "branch"):
        for e in load(f"pivot/pivot-r{r}-{side}.json"):
            first.setdefault(e["reportRows"], {}).setdefault(side, []).append((e["firstReportMs"], e["managedHeapAfterUpdatesBytes"] / 2**20))
for rows, v in sorted(first.items()):
    print(f"| {rows:,} | {' / '.join(f0(x[0]) for x in v['main'])} | {' / '.join(f0(x[0]) for x in v['branch'])} | "
          f"{' / '.join(f0(x[1]) for x in v['main'])} | {' / '.join(f0(x[1]) for x in v['branch'])} |")

print("\n## steps (branch): least / median of 9, ms")
keys = [("snapshotApplyMs", "Snapshot Apply"), ("aggregateFoldMs", "Fold"), ("affectedCubeMs", "Affected Cube"),
        ("affectedReportStructureMs", "Report structure"), ("labelWidthsMs", "Label widths")]
print("| Report rows | " + " | ".join(n for _, n in keys) + " | Row sequence shared |")
print("|---|" + "---|" * (len(keys) + 1))
for e in load("steps/pivot-steps.json"):
    cells = [f"{min(x[k] for x in e['runs']):.3f} / {st.median(x[k] for x in e['runs']):.3f}" for k, _ in keys]
    print(f"| {e['reportRows']:,} | " + " | ".join(cells) + f" | {'yes' if all(x['sameRowSequence'] for x in e['runs']) else 'NO'} |")

print("\n## component (branch): ms least / median / most of 9")
print("| Report rows | Apply → component render, ms | Painted | Rendered | Text changed | Mounted | Vouched Window admission, ms (least) |")
print("|---|---|---|---|---|---|---|")
for e in load("steps/pivot-component.json"):
    ms = [x["applyThroughComponentRenderMs"] for x in e["runs"]]
    s = lambda k: "/".join(str(v) for v in sorted({x[k] for x in e["runs"]}))
    print(f"| {e['reportRows']:,} | {min(ms):.2f} / {st.median(ms):.2f} / {max(ms):.2f} | {s('painted')} | {s('rendered')} | {s('paintedTextRowsChanged')} | {s('mounted')} | {min(x['vouchedWindowAdmissionMs'] for x in e['runs']):.4f} |")

print("\n## grid: whole live update, 10^6 rows, ms least / median / most of 15")
g = {side: [load(f"grid/grid-r{r}-{side}.json") for r in (1, 2)] for side in ("main", "branch")}
print("| Changes | main, r1 / r2 | branch, r1 / r2 | Allocated, MiB (median): main → branch |")
print("|---|---|---|---|")
for i, k in enumerate(x["changes"] for x in g["main"][0]["liveUpdate"]["perK"]):
    fmt = lambda d: f"{d['Min']:.2f} / {d['Median']:.2f} / {d['Max']:.2f}" if 'Min' in d else str(d)
    m = [fmt(run["liveUpdate"]["perK"][i]["withContinuationsMs"]) for run in g["main"]]
    b = [fmt(run["liveUpdate"]["perK"][i]["withContinuationsMs"]) for run in g["branch"]]
    am = g["main"][0]["liveUpdate"]["perK"][i]["allocatedBytes"]; ab = g["branch"][0]["liveUpdate"]["perK"][i]["allocatedBytes"]
    print(f"| {k:,} | {' ; '.join(m)} | {' ; '.join(b)} | {am.get('Median', 0) / 2**20:.1f} → {ab.get('Median', 0) / 2**20:.1f} |")
for which in ("repaintKeyed", "repaintUnkeyed"):
    m = [run[which]["ms"] for run in g["main"]]; b = [run[which]["ms"] for run in g["branch"]]
    fmt = lambda d: f"{d['Min']:.2f} / {d['Median']:.2f} / {d['Max']:.2f}"
    print(f"| {which} ({g['main'][0][which]['painted']} painted) | {' ; '.join(fmt(x) for x in m)} | {' ; '.join(fmt(x) for x in b)} | |")

print("\n## browser loops")
print("| Report rows | Side | Redraws | First report, s | Redraw ms, median (least–most) | Heap after a full collection, MiB: loaded → after redraw 5 → last | MiB a redraw over the last 10 | WebAssembly heap, MiB: loaded → last |")
print("|---|---|---|---|---|---|---|---|")
for b, rows in ((10, 11001), (100, 101001), (400, 401001)):
    for side in ("main", "branch"):
        for tag in ("", "long-"):
            p = f"browser/lv23-{tag}b{b}-{side}.json"
            if not os.path.exists(os.path.join(R, p)): continue
            d = load(p); s = d["samples"]; red = [x["redrawMs"] for x in s if "redrawMs" in x]
            n = len(s) - 1
            last10 = (s[-1]["managedMB"] - s[max(0, len(s) - 11)]["managedMB"]) / min(10, n) if n else 0
            five = s[5]["managedMB"] if len(s) > 5 else None
            err = " — out of memory at redraw %d" % (n + 1) if d["errors"] else ""
            print(f"| {rows:,} | {side}{err} | {n} | {d['firstReportSeconds']:.2f} | {st.median(red):.0f} ({min(red)}–{max(red)}) | "
                  f"{s[0]['managedMB']} → {five} → {s[-1]['managedMB']} | {last10:+.2f} | {s[0]['linearMB']} → {s[-1]['linearMB']} |")
