#!/usr/bin/env python3
"""Ticket 13: composes the record's metrics.json (PV-48, LV-23, LV-15 and the pushed Window) from its
raw files, beside the before figures of 2026-10-06's records, in the shape the layer-3 specs record:
the browser under the project's key ("chrome"), CoreCLR under "coreclr", the machine under "machine";
each figure a spread {min, median, max, n}."""
import json
import os
import sys

record = sys.argv[1]
raw = os.path.join(record, 'raw')
verification = os.path.join(record, '..')
before_raw = os.path.join(verification, '2026-10-06-macos-live-update-costs-cc', 'raw')
oom_raw = os.path.join(verification, '2026-10-06-macos-pivot-oom', 'raw')
paint_raw = os.path.join(verification, '2026-10-06-macos-paint-text-cost', 'raw')


def load(path):
    with open(path) as f:
        return json.load(f)


def spread(stat, digits=3):
    if stat is None:
        return None
    if 'Min' in stat:
        return {'min': round(stat['Min'], digits), 'median': round(stat['Median'], digits), 'max': round(stat['Max'], digits), 'n': stat['Runs']}
    return {k: stat[k] for k in ('min', 'median', 'max', 'n') if k in stat}


def first(entries, name, **want):
    for e in entries:
        if e['name'] == name and all(e.get(k) == v for k, v in want.items()) and not e.get('build', '').startswith('before') and len(e.get('runs', [])) <= 20:
            return e
    return None


def results(path):
    return {r['k']: r for r in load(path)['measured']['results']}


browser_after = load(os.path.join(raw, 'browser-results.json'))
browser_before = load(os.path.join(before_raw, 'browser-results.json'))
SIZES = {9: '10,001', 100: '101,001', 400: '401,001'}

# PV-48, the browser.
pv48_browser = {}
for b, rows in SIZES.items():
    for k in (1, 1000):
        a = first(browser_after, 'pivot-wasm', a=1000, b=b, batch=k, probe=False)
        bb = first(browser_before, 'pivot-wasm', a=1000, b=b, batch=k, probe=False)
        pv48_browser[f'{rows} report rows, {k:,} change(s) a batch'] = {
            'applyToReportPaintedMs': {'before': spread(bb and bb['applyToReportPaintedMs']), 'after': spread(a['applyToReportPaintedMs'])},
            'longestTaskMs': {'before': spread(bb and bb['longestTaskMs']), 'after': spread(a['longestTaskMs'])},
        }
pv48_browser['401,001 report rows, 1 change(s) a batch']['note'] = 'before: one redraw timed cleanly (the diagnostic run); longer runs ran out of memory'
pv48_browser['401,001 report rows, 1,000 change(s) a batch']['note'] = 'before: ran out of memory'
long = {}
for e in browser_after:
    if e['name'] == 'pivot-wasm' and len(e.get('runs', [])) > 20:
        long[f'{SIZES[e["b"]]} report rows, 1 change a batch, {len(e["runs"])} redraws'] = {
            'applyToReportPaintedMs': spread(e['applyToReportPaintedMs']),
            'redraw 65, ms (the redraw CoreCLR shows laid out afresh in the same sequence of batches)': e['runs'][65 - e['warmup'] - 1]['applyToReportPaintedMs'],
        }
pv48_browser['long runs, after'] = long
probes = {}
for b, rows in SIZES.items():
    a = first(browser_after, 'pivot-wasm', a=1000, b=b, batch=1, probe=True)
    probes[f'{rows} report rows'] = {k: spread(a.get(k)) for k in ('probeNextCubeMs', 'probeNextReportMs', 'probeCubeMs', 'probeReportMs', 'probeKeysMs', 'probeRowsMs')}
pv48_browser['steps timed in the page, after (least/median/most of 15)'] = probes
same_day = {}
for e in browser_after:
    if e.get('build', '').startswith('before'):
        same_day[f'{SIZES[e["b"]]} report rows, 1 change a batch'] = spread(e['applyToReportPaintedMs'])
pv48_browser['the before code again, the same day, Apply to the painted report'] = same_day

# PV-48, CoreCLR.
pv48_core = {}
for b, rows in SIZES.items():
    before = results(os.path.join(before_raw, f'pivot-1000x{b}.json'))
    rerun = os.path.join(before_raw, f'pivot-1000x{b}-rerun-1000-then-1.json')
    if os.path.exists(rerun):
        before[1000] = results(rerun)[1000]
    after = results(os.path.join(raw, f'pivot-1000x{b}.json'))
    second = results(os.path.join(raw, f'pivot-1000x{b}-r2.json'))
    same = results(os.path.join(raw, f'before-pivot-1000x{b}.json'))
    for k in (1, 1000):
        a = after[k]
        pv48_core[f'{rows} report rows, {k:,} change(s) a batch'] = {
            'wholeRedrawMs': {'before': spread(before[k]['wholeRedraw']), 'beforeSameDay': spread(same[k]['wholeRedraw']),
                              'after': spread(a['wholeRedraw']), 'afterSecondRound': spread(second[k]['wholeRedraw'])},
            'stepsAfterMs': {
                'fold': spread(a['sourceFold']), 'answer': spread(a['sourceAnswer']), 'nextCube': spread(a['nextCubeAsync']),
                'nextCubeEngineCompares': spread(a['nextCubeEngineCompares']), 'nextReport': spread(a['nextReportAsync']),
                'hasSameRowsAs': spread(a['hasSameRowsAsAsync']), 'changesSince': spread(a['changesSinceAsync']),
                'show': spread(a['show']), 'gridApplyState': spread(a['gridApplyState']), 'render': spread(a['renderInRenderer']),
                'changesSinceBothLaidOutAfresh': spread(second[k]['changesSinceAfreshBothCold']),
            },
            'stepsBeforeMs': {
                'cube': spread(before[k]['cubeAsync']), 'report': spread(before[k]['reportAsync']),
                'labelWidths': spread(before[k]['labelWidthsAsync']), 'gridKeyCheck': spread(before[k]['gridRequireDistinctKeys']),
                'render': spread(before[k]['renderInRenderer']),
            },
            'paintedRowsRenderedPerRedrawOf11': {'before': spread(before[k]['paintedRowsRendered']), 'after': spread(a['paintedRowsRendered'])},
            'allocatedPerRedrawMB': spread(a['wholeRedrawAllocatedMB']),
        }


def gc(path):
    m = load(path)['measured']
    gens = [0, 0, 0]
    compacting = 0
    for r in m['rows']:
        for c in r.get('collections', []):
            gens[c['Generation']] += 1
            compacting += 1 if c.get('Compacting') else 0
    return {'redrawMs': spread(m['redrawMs']), 'gcPauseMs': spread(m['gcPauseMs']),
            'collectionsGen0Gen1Gen2InAllRedraws': gens, 'compacting': compacting, 'redraws': len(m['rows']),
            'allocatedMB': spread(m.get('allocatedMB'))}


pv48_gc = {}
for b, rows in SIZES.items():
    pv48_gc[f'{rows} report rows'] = {'beforeSameDay': gc(os.path.join(raw, f'before-pivot-gc-1000x{b}.json')), 'after': gc(os.path.join(raw, f'pivot-gc-1000x{b}.json'))}
pv48_gc['101,001 report rows']['beforeRecord'] = gc(os.path.join(paint_raw, 'core-gc-full-1-v0.json'))


def steady(path, a, b):
    rows = [r for r in load(path)['measured']['rows'] if a <= r['redraw'] <= b]
    times = sorted(r['redrawMs'] for r in rows)
    pause = sum(r['gcPauseMs'] for r in rows)
    return {'redraws': f'{a}-{b}', 'medianMs': times[len(times) // 2], 'meanMs': round(sum(times) / len(times), 2), 'mostMs': times[-1],
            'gcPauseMsInAll': round(pause, 1), 'gcPauseMsPerRedraw': round(pause / len(rows), 2),
            'heapAfterLastGcMB': rows[-1]['lastGc']['heapSizeMB']}


pv48_steady = {
    '101,001 report rows, before (2026-10-06 paint-text record, V0, 150 redraws)': [steady(os.path.join(paint_raw, 'core-steady150-1-v0.json'), a, b) for a, b in ((11, 60), (61, 70), (71, 100), (101, 150), (11, 150))],
    '101,001 report rows, after (150 redraws)': [steady(os.path.join(raw, 'pivot-steady-1000x100.json'), a, b) for a, b in ((11, 60), (61, 70), (71, 100), (101, 150), (11, 150))],
    '10,001 report rows, after (70 redraws)': [steady(os.path.join(raw, 'pivot-steady-1000x9-70.json'), 11, 70)],
    '401,001 report rows, after (70 redraws)': [steady(os.path.join(raw, 'pivot-steady-1000x400-70.json'), 11, 70)],
}

# LV-23.


def loop(path):
    out = []
    for line in open(path):
        if line.startswith('{"step"'):
            d = json.loads(line)
            managed = d.get('managedAfterGcMB')
            out.append({'after': d['step'], 'managedHeapAfterFullCollectionMB': float(str(managed).split()[0]) if managed else None,
                        'reportsAlive': (str(managed).split('report=')[1].split(',')[0] if managed and 'report=' in str(managed) else None),
                        'webAssemblyHeapMB': d.get('heapMB'), 'outOfMemory': d.get('oom')})
    return out


lv23 = {
    '101,001 report rows, 20 redraws, after': loop(os.path.join(raw, 'oom-1000x100-20.log')),
    '101,001 report rows, before (2026-10-06, 12 redraws)': loop(os.path.join(oom_raw, 'run2-1000x100-gc.log')),
    '401,001 report rows, 15 redraws, after': loop(os.path.join(raw, 'oom-1000x400-15.log')),
    '401,001 report rows, 80 redraws, after (crosses the redraw a compaction lays out afresh)': loop(os.path.join(raw, 'oom-1000x400-80.log')),
    '401,001 report rows, before (2026-10-06, no collection forced)': loop(os.path.join(oom_raw, 'run1-1000x400.log')),
}
lv23_core = {
    '101,001 report rows, 20 redraws, after': load(os.path.join(raw, 'pivot-memory-1000x100.json'))['measured']['samples'],
    '401,001 report rows, 15 redraws, after': load(os.path.join(raw, 'pivot-memory-1000x400.json'))['measured']['samples'],
}

# LV-15 and the pushed Window.
lv15 = {}
for rows in (100000, 1000000):
    for k in (1, 100, 1000):
        a = first(browser_after, 'grid-wasm', rows=rows, batch=k, probe=False)
        bb = first(browser_before, 'grid-wasm', rows=rows, batch=k, probe=False)
        lv15[f'{rows:,} rows, {k:,} change(s) a batch, Apply to the painted frame'] = {'before': spread(bb['applyToPaintedMs']), 'after': spread(a['applyToPaintedMs'])}
probe_a = first(browser_after, 'grid-wasm', rows=1000000, batch=1000, probe=True)
probe_b = first(browser_before, 'grid-wasm', rows=1000000, batch=1000, probe=True)
lv15['1,000,000 rows: the source alone, the same batch'] = {'before': spread(probe_b['probeSourceMs']), 'after': spread(probe_a['probeSourceMs'])}
bytes_after = load(os.path.join(raw, 'server-bytes.json'))['results']
bytes_before = [r for r in load(os.path.join(before_raw, 'server-bytes.json'))['results'] if r['rows'] == 1000000]
lv15['bytes per update on the Server host, 1,000,000 rows'] = {
    f'{r["batch"]:,} change(s)': {
        'renderBatch': {'before': next(x for x in bytes_before if x['batch'] == r['batch'])['updateRenderBatchPayload'], 'after': r['updateRenderBatchPayload']},
        'wire': {'before': next(x for x in bytes_before if x['batch'] == r['batch'])['updateWire'], 'after': r['updateWire']},
        'unmarkRenderBatch': r['unmarkRenderBatchPayload'], 'unmarkWire': r['unmarkWire'], 'perUpdateWireMean': r['perUpdateTotalWireMean'],
    } for r in bytes_after}
pushed = {}
for name, label in (('grid-wasm-pushed', 'not vouched'), ('grid-wasm-pushed-vouched', 'vouched (VouchesDistinctRows)')):
    for k in (1000, 1):
        e = first(browser_after, name, rows=1000000, batch=k)
        pushed[f'1,000,000 rows pushed under a Row Key, {label}, {k:,} change(s) a batch'] = {
            'applyToPaintedMs': spread(e['applyToPaintedMs']), 'appliedMs': spread(e['appliedMs']), 'longestTaskMs': spread(e['longestTaskMs'])}
pushed['the check by key alone over 1,000,000 rows (RequireDistinctKeys)'] = {'before': spread(probe_b['probeKeysMs']), 'after': spread(probe_a['probeKeysMs'])}

grid_core = {}
for size, label in (('1e5', '100,000'), ('1e6', '1,000,000')):
    a = results(os.path.join(raw, f'grid-{size}.json'))
    b = results(os.path.join(before_raw, f'grid-{size}.json'))
    same = results(os.path.join(raw, f'before-grid-{size}.json'))
    for k in (1, 100, 1000):
        grid_core[f'{label} rows, {k:,} change(s) a batch'] = {
            'wholeUpdateMs': {'before': spread(b[k]['wholeUpdate']), 'after': spread(a[k]['wholeUpdate'])},
            'requeryAloneMs': spread(a[k]['liveRequeryAlone']),
            'gridApplyStateMs': spread(a[k]['gridApplyState']),
            'selectionSummaryWalkLastRowChangedMs': {'before': spread(b[k]['noteRowsForSummaryWorstCase']), 'after': spread(a[k]['noteRowsForSummaryWorstCase'])},
            'pushedTakeInUnvouchedMs': {'before': spread(b[k]['pushedWindowApplyStateWithRowKey']), 'beforeSameDay': spread(same[k]['pushedWindowApplyStateWithRowKey']), 'after': spread(a[k]['pushedWindowApplyStateWithRowKey'])},
            'keyCheckAloneMs': {'before': spread(b[k]['requireDistinctKeysAlone']), 'beforeSameDay': spread(same[k]['requireDistinctKeysAlone']), 'after': spread(a[k]['requireDistinctKeysAlone'])},
            'pushedTakeInVouchedMs': spread(a[k]['pushedWindowApplyStateVouched']),
            'pushedTakeInAndRenderVouchedMs': spread(a[k]['pushedWindowApplyStateAndRenderVouched']),
        }



def pv21(path):
    # The spec prints the entry it records, after "PV-21 1,000 changes ", as JSON.
    text = open(path).read()
    start = text.index('PV-21 1,000 changes {') + len('PV-21 1,000 changes ')
    depth = 0
    for i, ch in enumerate(text[start:]):
        depth += ch == '{'
        depth -= ch == '}'
        if depth == 0:
            return json.loads(text[start:start + i + 1])


pv21_entry = {
    'after': pv21(os.path.join(raw, 'browser-pv21-live-after.log')),
    'beforeSameDay (41c8d8c8)': pv21(os.path.join(raw, 'browser-before-pv21-live.log')),
    'targets': 'PV-21: 1,000 changes <= 0.2 s; the page blocked <= 50 ms at a time (a published build without AOT)',
}

machine = {
    'cpu': 'Apple M4 Pro, 12 cores (8 performance, 4 efficiency)',
    'memoryGiB': 24,
    'os': 'macOS 26.6.2 (25G83)',
    'dotnet': 'SDK 10.0.203 through nix develop, runtime 10.0.7 (CoreCLR, workstation GC), Release, DOTNET_TieredCompilation=0',
    'browser': 'Google Chrome 154.0.8037.98, headless, through Playwright 1.63.0 (Node 24.14.1)',
    'webassembly': 'samples/ExGrid.DemoHost published in Release, trimmed, no AOT',
    'commit': '526f3b75 (after); 41c8d8c8 (before, the records of 2026-10-06 and this record\'s same-day runs)',
}
metrics = {
    'machine': machine,
    'chrome': {
        'PV-48 ExPivot live redraw, Apply to the painted report, /pivot-live-costs (D10\'s report)': pv48_browser,
        'LV-23 a live ExPivot over 20 redraws, managed heap after a full collection, /pivot-live-costs': lv23,
        'LV-15 1,000 changes to 1,000,000 rows and its parts, /grid-live-local': lv15,
        'LV-10 ticket 07: a pushed Window of 1,000,000 rows, /grid-live-local?push=1': pushed,
        'PV-21 1,000 changes, /pivot-live over 1,000,000 trades (beside PV-48)': pv21_entry,
    },
    'coreclr': {
        'how': 'Release, DOTNET_TieredCompilation=0, each step alone on the same inputs, the least of 15 runs after 3 untimed (a full collection before each); spikes/live-update/Costs (harness/)',
        'PV-48 ExPivot live redraw, step by step': pv48_core,
        'PV-48 the collector inside one redraw (a full collection before each, GC events)': pv48_gc,
        'PV-48 steady state, consecutive redraws, no forced collection (H3\'s method)': pv48_steady,
        'LV-23 ExPivot memory after a full collection per redraw': lv23_core,
        'LV-15 GridSource.From keyed, one live update': grid_core,
    },
}
with open(os.path.join(record, 'metrics.json'), 'w') as f:
    f.write(json.dumps(metrics, indent=2, ensure_ascii=False) + '\n')
print('wrote', os.path.join(record, 'metrics.json'))
