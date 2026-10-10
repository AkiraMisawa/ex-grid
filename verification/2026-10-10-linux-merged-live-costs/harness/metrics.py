#!/usr/bin/env python3
"""Composes the record's metrics.json (PV-48 and LV-23, and ExGrid's live update beside them) from its
raw files, in the shape the layer-3 specs record: the browser under the project's key ("chrome"),
CoreCLR under "coreclr", the machine under "machine"; each figure a spread {min, median, max, n}, main
and the branch side by side. Run: python3 -I harness/metrics.py <the record's directory>."""
import json
import os
import statistics
import sys

record = sys.argv[1]
raw = os.path.join(record, 'raw')


def load(path):
    with open(os.path.join(raw, path)) as f:
        return json.load(f)


def spread(values, digits=3):
    values = list(values)
    return {'min': round(min(values), digits), 'median': round(statistics.median(values), digits),
            'max': round(max(values), digits), 'n': len(values)}


def stat(s, digits=3):
    return {'min': round(s['Min'], digits), 'median': round(s['Median'], digits), 'max': round(s['Max'], digits), 'n': s['Runs']}


SIDES = ('main', 'branch')
metrics = {
    'machine': {
        'cpu': 'Intel Xeon @ 2.10 GHz, 4 vCPUs (a cloud container)',
        'memoryGiB': 15,
        'os': 'Ubuntu 24.04.5 LTS, Linux 6.18',
        'dotnet': 'SDK 10.0.203 through nix develop, runtime 10.0.7 (CoreCLR, workstation GC), Release, DOTNET_TieredCompilation=0',
        'browser': 'Chromium 141.0.7390.37 (Playwright\'s build), headless',
        'webassembly': 'samples/ExGrid.DemoHost published in Release, trimmed, no AOT, no wasm-tools workload',
        'commit': 'branch claude/live-data-best at 80d6b38 (src as at c82b894); main at db60f6f',
    },
    'coreclr': {},
    'chrome': {},
}

# PV-48: ExPivot's whole live update, Apply through the component's render, ticket 01's fixture.
whole = {}
for round_ in (1, 2, 3):
    for side in SIDES:
        for entry in load(f'pivot/pivot-r{round_}-{side}.json'):
            gap = '1 ms between batches' if round_ < 3 else '5 s between batches, past the 1 s highlight'
            for batch in entry['batches']:
                key = f"{entry['reportRows']:,} report rows, {batch['changes']:,} change(s) a batch, {gap}"
                cell = whole.setdefault(key, {}).setdefault(side, {'ms': [], 'allocatedMiB': [], 'gcPauseMs': []})
                cell['ms'] += [r['applyThroughRenderMs'] for r in batch['runs']]
                cell['allocatedMiB'] += [r['allocatedBytes'] / 2**20 for r in batch['runs']]
                cell['gcPauseMs'] += [r['gcPauseMs'] for r in batch['runs']]
            first = f"{entry['reportRows']:,} report rows, {gap}"
            cell = whole.setdefault('first report: ' + first, {}).setdefault(side, {'firstReportMs': [], 'heapAfterUpdatesMiB': []})
            cell['firstReportMs'].append(entry['firstReportMs'])
            cell['heapAfterUpdatesMiB'].append(entry['managedHeapAfterUpdatesBytes'] / 2**20)
metrics['coreclr']['PV-48 ExPivot whole live update, Apply through the component render (bUnit)'] = {
    key: {figure: {side: spread(values[side][figure]) for side in SIDES} for figure in values['main']}
    for key, values in whole.items()
}

# PV-48: the update step by step, and the component, on the branch.
steps = {}
for entry in load('steps/pivot-steps.json'):
    steps[f"{entry['reportRows']:,} report rows, 1,000 changes a batch"] = {
        figure: spread([r[figure] for r in entry['runs']])
        for figure in ('snapshotApplyMs', 'aggregateFoldMs', 'affectedCubeMs', 'affectedReportStructureMs', 'labelWidthsMs')}
metrics['coreclr']['PV-48 ExPivot update step by step, branch'] = steps
component = {}
for entry in load('steps/pivot-component.json'):
    component[f"{entry['reportRows']:,} report rows, 1,000 changes a batch"] = {
        'applyThroughComponentRenderMs': spread([r['applyThroughComponentRenderMs'] for r in entry['runs']]),
        'rowsRendered': spread([r['rendered'] for r in entry['runs']], 0),
        'rowsMounted': spread([r['mounted'] for r in entry['runs']], 0),
        'vouchedWindowAdmissionMs': spread([r['vouchedWindowAdmissionMs'] for r in entry['runs']], 4)}
metrics['coreclr']['PV-48 ExPivot component, branch'] = component

# ExGrid's whole live update over GridSource.From keyed, and a pushed Window's full repaint.
grid = {}
for side in SIDES:
    for round_ in (1, 2):
        run = load(f'grid/grid-r{round_}-{side}.json')
        for k in run['liveUpdate']['perK']:
            key = f"{run['liveUpdate']['rows']:,} rows, {k['changes']:,} change(s) a batch, round {round_}"
            grid.setdefault(key, {})[side] = {'withContinuationsMs': stat(k['withContinuationsMs']),
                                              'allocatedMiB': stat({**k['allocatedBytes'], **{f: k['allocatedBytes'][f] / 2**20 for f in ('Min', 'Median', 'Max')}})}
        for which in ('repaintKeyed', 'repaintUnkeyed'):
            grid.setdefault(f'{which}, round {round_}', {})[side] = {'ms': stat(run[which]['ms']), 'painted': run[which]['painted']}
metrics['coreclr']['ExGrid whole live update and full repaint'] = grid

# LV-23 and PV-48's browser half: the loop, each redraw a click to the changed text.
loop = {}
for inner, rows in ((10, 11001), (100, 101001), (400, 401001)):
    for tag in ('', 'long-'):
        for side in SIDES:
            path = f'browser/lv23-{tag}b{inner}-{side}.json'
            if not os.path.exists(os.path.join(raw, path)):
                continue
            run = load(path)
            samples = run['samples']
            key = f"{rows:,} report rows, 1,000 changes a batch, {len(samples) - 1} redraws"
            loop.setdefault(key, {})[side] = {
                'firstReportS': run['firstReportSeconds'],
                'redrawMs': spread([s['redrawMs'] for s in samples if 'redrawMs' in s], 0),
                'managedMiBAfterFullCollection': [s['managedMB'] for s in samples],
                'webAssemblyHeapMiB': [s['linearMB'] for s in samples],
                'outOfMemory': bool(run['errors']),
            }
metrics['chrome']['LV-23 and PV-48 ExPivot live redraw, click to the changed text, /perf-live-memory'] = loop

with open(os.path.join(record, 'metrics.json'), 'w') as f:
    json.dump(metrics, f, indent=2)
    f.write('\n')
