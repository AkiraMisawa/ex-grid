#!/usr/bin/env python3
"""Ticket 13: CoreCLR tables, before | after, from the raw JSON of ticket 01's record (before), this
record's same-session reruns of the before code (before-*.json) and this record's after runs."""
import json
import os
import sys

V = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
BEFORE = os.path.join(V, '2026-10-06-macos-live-update-costs-cc', 'raw')
AFTER = os.path.join(V, '2026-10-07-macos-live-update-costs-after', 'raw')
if len(sys.argv) > 1:
    AFTER = sys.argv[1]
    BEFORE = os.path.join(AFTER, '..', '..', '2026-10-06-macos-live-update-costs-cc', 'raw')


def load(path):
    with open(path) as f:
        return json.load(f)


def results(path):
    return {r['k']: r for r in load(path)['measured']['results']}


def least(stat):
    return None if stat is None else stat['Min']


def fmt(v, digits=None):
    if v is None:
        return '—'
    if digits is None:
        digits = 3 if v < 1 else 2 if v < 10 else 1
    return f'{v:,.{digits}f}'


def pivot_before(size):
    # Ticket 01's tables take k = 1,000 at 101,001 and 401,001 from the reruns (k = 1,000 first).
    base = results(os.path.join(BEFORE, f'pivot-1000x{size}.json'))
    rerun = os.path.join(BEFORE, f'pivot-1000x{size}-rerun-1000-then-1.json')
    if os.path.exists(rerun):
        base[1000] = results(rerun)[1000]
    return base


def pivot_table():
    sizes = [9, 100, 400]
    cols = [(s, k) for s in sizes for k in (1, 1000)]
    before = {s: pivot_before(s) for s in sizes}
    same = {s: results(os.path.join(AFTER, f'before-pivot-1000x{s}.json')) for s in sizes if os.path.exists(os.path.join(AFTER, f'before-pivot-1000x{s}.json'))}
    after = {s: results(os.path.join(AFTER, f'pivot-1000x{s}.json')) for s in sizes}
    rows = [
        ('The source folds the batch (`SnapshotPivotSource.Apply`)', 'sourceFold', 'sourceFold'),
        ('The source answers (`AggregateAsync`; after: naming the leaves that changed)', 'sourceAnswer', 'sourceAnswer'),
        ('The cube: before `CubeAsync`, after `NextCubeAsync` (leaves named)', 'cubeAsync', 'nextCubeAsync'),
        ('The report: before `ReportAsync`, after `NextReportAsync`', 'reportAsync', 'nextReportAsync'),
        ('`HasSameRowsAsAsync`', 'hasSameRowsAsAsync', 'hasSameRowsAsAsync'),
        ('`ChangesSinceAsync` (after only: the Change Highlight\'s comparison)', None, 'changesSinceAsync'),
        ('`LabelWidthsAsync` (after: skipped, the report was made from the one on screen)', 'labelWidthsAsync', None),
        ('`Show` (after: with the history\'s `Record`)', 'show', 'show'),
        ('The grid\'s check of the report\'s keys (after: vouched, not run)', 'gridRequireDistinctKeys', None),
        ('The grid takes the report in (`ApplyState`, alone)', 'gridApplyState', 'gridApplyState'),
        ('Render (after: with the painted rows\' keys checked)', 'renderInRenderer', 'renderInRenderer'),
        ('**Whole redraw** (`Apply` on the renderer\'s context, slicing off)', 'wholeRedraw', 'wholeRedraw'),
    ]
    head = '| Step | ' + ' | '.join(f'{["10,001", "101,001", "401,001"][sizes.index(s)]}, k = {k:,}' for s, k in cols) + ' |'
    print(head)
    print('|---|' + '---|' * len(cols))
    for label, b, a in rows:
        cells = []
        for s, k in cols:
            bv = least(before[s][k].get(b)) if b else None
            av = least(after[s][k].get(a)) if a else None
            if a is None:
                av = 0.0 if b in ('labelWidthsAsync', 'gridRequireDistinctKeys') else None
            cells.append(f'{fmt(bv)} → {fmt(av)}' if b else f'— → {fmt(av)}')
        print(f'| {label} | ' + ' | '.join(cells) + ' |')
    # The whole redraw's median and most, and the same-session rerun of the before code.
    for label, key in (('Whole redraw, median', 'Median'), ('Whole redraw, most', 'Max')):
        cells = []
        for s, k in cols:
            cells.append(f'{fmt(before[s][k]["wholeRedraw"][key])} → {fmt(after[s][k]["wholeRedraw"][key])}')
        print(f'| {label} | ' + ' | '.join(cells) + ' |')
    if same:
        cells = []
        for s, k in cols:
            v = same.get(s, {}).get(k)
            cells.append(fmt(least(v['wholeRedraw'])) if v else '—')
        print('| Whole redraw, the before code again today (same session) | ' + ' | '.join(cells) + ' |')
    print()
    print('Beside it, after, on the same inputs:')
    print()
    print('| | ' + ' | '.join(f'{["10,001", "101,001", "401,001"][sizes.index(s)]}, k = {k:,}' for s, k in cols) + ' |')
    print('|---|' + '---|' * len(cols))
    extra = [
        ('`NextCubeAsync` with the engine comparing (no leaves named)', 'nextCubeEngineCompares'),
        ('The cube built afresh (`CubeAsync`, before\'s path)', 'cubeAsyncAfresh'),
        ('The report laid out afresh (`ReportAsync`, before\'s path)', 'reportAsyncAfresh'),
        ('`LabelWidthsAsync` alone (not run in the redraw)', 'labelWidthsAsync'),
        ('The grid\'s key check alone (not run: vouched)', 'gridRequireDistinctKeys'),
    ]
    for label, a in extra:
        print(f'| {label} | ' + ' | '.join(fmt(least(after[s][k][a])) for s, k in cols) + ' |')
    for label, a in (('Leaves the answer named (median)', 'answerNamedLeaves'),
                     ('Report rows shared with the report before (median)', 'reportRowsSharedWithPrevious'),
                     ('Painted rows rendered per redraw, of 11 (median; most)', 'paintedRowsRendered'),
                     ('Painted rows whose painted value changed (median; most)', 'paintedRowsWithAChangedValue'),
                     ('Allocated per whole redraw, MB (median)', 'wholeRedrawAllocatedMB'),
                     ('Collections inside a whole redraw, gen 0 / 1 / 2 (most)', None)):
        cells = []
        for s, k in cols:
            r = after[s][k]
            if a is None:
                cells.append(f'{r["wholeRedrawGen0"]["Max"]:.0f} / {r["wholeRedrawGen1"]["Max"]:.0f} / {r["wholeRedrawGen2"]["Max"]:.0f}')
            elif a in ('paintedRowsRendered', 'paintedRowsWithAChangedValue'):
                cells.append(f'{r[a]["Median"]:.0f}; {r[a]["Max"]:.0f}')
            elif a == 'wholeRedrawAllocatedMB':
                cells.append(fmt(r[a]['Median'], 1))
            else:
                cells.append(f'{r[a]["Median"]:,.0f}')
        print(f'| {label} | ' + ' | '.join(cells) + ' |')
    print()
    print('Labels skipped / vouched / inline:', {f'{s}/{k}': (after[s][k]['labelWidthsSkippedInRedraws'], after[s][k]['gridWindowVouched'], after[s][k]['redrawsShownInline']) for s, k in cols})
    print('Load before/after:', {f'{s}/{k}': (after[s][k]['machineBefore']['loadavg'], after[s][k]['machineAfter']['loadavg']) for s, k in cols})
    if same:
        print()
        print('Same-session before (least):')
        for s in same:
            for k in (1, 1000):
                r = same[s][k]
                print(f'  1000x{s} k={k}: fold {fmt(least(r["sourceFold"]))} answer {fmt(least(r["sourceAnswer"]))} cube {fmt(least(r["cubeAsync"]))} report {fmt(least(r["reportAsync"]))} labels {fmt(least(r["labelWidthsAsync"]))} keys {fmt(least(r["gridRequireDistinctKeys"]))} render {fmt(least(r["renderInRenderer"]))} whole {fmt(least(r["wholeRedraw"]))} / {fmt(r["wholeRedraw"]["Median"])}  load {r["machineBefore"]["loadavg"]}')


def grid_table():
    sizes = ['1e5', '1e6']
    cols = [(s, k) for s in sizes for k in (1, 100, 1000)]
    before = {s: results(os.path.join(BEFORE, f'grid-{s}.json')) for s in sizes}
    after = {s: results(os.path.join(AFTER, f'grid-{s}.json')) for s in sizes}
    same = {s: results(os.path.join(AFTER, f'before-grid-{s}.json')) for s in sizes if os.path.exists(os.path.join(AFTER, f'before-grid-{s}.json'))}
    rows = [
        ('Fold (`Apply`, gathering)', 'sourceFold'),
        ('Publication (`PublishGathered`)', 'sourcePublish'),
        ('&nbsp;&nbsp;of which the incremental requery (`LiveRequery.Apply`, alone)', 'liveRequeryAlone'),
        ('&nbsp;&nbsp;of which the Change Highlight\'s record (alone)', 'changeHighlightRecordAlone'),
        ('The grid takes the Window in (`ApplyState`)', 'gridApplyState'),
        ('&nbsp;&nbsp;the Selection Summary\'s walk, alone, when only the last row changed', 'noteRowsForSummaryWorstCase'),
        ('Render (`StateHasChanged`)', 'gridRender'),
        ('**Whole update**', 'wholeUpdate'),
        ('A pushed Window\'s take-in under a Row Key, unvouched (`ApplyState`)', 'pushedWindowApplyStateWithRowKey'),
        ('&nbsp;&nbsp;its check alone (`RequireDistinctKeys`)', 'requireDistinctKeysAlone'),
        ('&nbsp;&nbsp;by instance, no Row Key (`ApplyState`)', 'pushedWindowApplyStateByInstance'),
        ('A pushed Window\'s take-in, vouched (`ApplyState`)', 'pushedWindowApplyStateVouched'),
        ('&nbsp;&nbsp;with the render that checks the painted rows\' keys', 'pushedWindowApplyStateAndRenderVouched'),
    ]
    print('| Step | ' + ' | '.join(f'{"10⁵" if s == "1e5" else "10⁶"}, k = {k:,}' for s, k in cols) + ' |')
    print('|---|' + '---|' * len(cols))
    for label, key in rows:
        cells = []
        for s, k in cols:
            bv = least(before[s][k].get(key))
            av = least(after[s][k].get(key))
            cells.append(f'{fmt(bv)} → {fmt(av)}')
        print(f'| {label} | ' + ' | '.join(cells) + ' |')
    for label, key in (('Whole update, median', 'Median'), ('Whole update, most', 'Max')):
        print(f'| {label} | ' + ' | '.join(f'{fmt(before[s][k]["wholeUpdate"][key])} → {fmt(after[s][k]["wholeUpdate"][key])}' for s, k in cols) + ' |')
    print('| Painted rows rendered per update, of 18 (median) | ' + ' | '.join(f'{before[s][k]["rowsRenderedPerUpdate"]["Median"]:.0f} → {after[s][k]["rowsRenderedPerUpdate"]["Median"]:.0f}' for s, k in cols) + ' |')
    print('| Render batch per update, estimated bytes (median) | ' + ' | '.join(f'{before[s][k]["perUpdateBatches"]["estimatedBytes"]["Median"]:,.0f} → {after[s][k]["perUpdateBatches"]["estimatedBytes"]["Median"]:,.0f}' for s, k in cols) + ' |')
    if same:
        print()
        print('Same-session before (least):')
        for s in same:
            for k in (1, 100, 1000):
                r = same[s][k]
                print(f'  {s} k={k}: whole {fmt(least(r["wholeUpdate"]))} / {fmt(r["wholeUpdate"]["Median"])} / {fmt(r["wholeUpdate"]["Max"])}; keys {fmt(least(r["requireDistinctKeysAlone"]))} rows {fmt(least(r["requireDistinctRowsAlone"]))} pushedKey {fmt(least(r["pushedWindowApplyStateWithRowKey"]))} pushedInstance {fmt(least(r["pushedWindowApplyStateByInstance"]))} summaryWorst {fmt(least(r["noteRowsForSummaryWorstCase"]))} publish {fmt(least(r["sourcePublish"]))} load {r["machineBefore"]["loadavg"]}')


if __name__ == '__main__':
    which = sys.argv[2] if len(sys.argv) > 2 else 'all'
    if which in ('all', 'pivot'):
        pivot_table()
        print()
    if which in ('all', 'grid'):
        grid_table()
