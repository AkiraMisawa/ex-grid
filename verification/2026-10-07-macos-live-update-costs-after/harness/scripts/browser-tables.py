#!/usr/bin/env python3
"""Ticket 13: the browser tables, before | after, from ticket 01's raw/browser-results.json (before)
and this record's raw/browser-results.json (after, and the same-session before runs)."""
import json
import os
import sys

AFTER = sys.argv[1]
BEFORE = os.path.join(AFTER, '..', '..', '2026-10-06-macos-live-update-costs-cc', 'raw')


def load(path):
    with open(path) as f:
        return json.load(f)


def trio(stat, digits=1):
    if not stat:
        return '—'
    return f'{stat["min"]:,.{digits}f} / {stat["median"]:,.{digits}f} / {stat["max"]:,.{digits}f}'


def pick(entries, name, **want):
    # The first matching entry: ticket 01's tables read its first runs, and this record's same-day
    # runs of the before code carry build = "before…", and its 80-redraw runs n = 80.
    found = [e for e in entries if e['name'] == name and all(e.get(k) == v for k, v in want.items())
             and not e.get('build', '').startswith('before') and len(e.get('runs', [])) <= 20]
    return found[0] if found else None


before = load(os.path.join(BEFORE, 'browser-results.json'))
after = load(os.path.join(AFTER, 'browser-results.json'))

print('## ExPivot, Apply -> painted report (least / median / most)')
for b in (9, 100, 400):
    for k in (1, 1000):
        eb = pick(before, 'pivot-wasm', a=1000, b=b, batch=k, probe=False)
        ea = pick(after, 'pivot-wasm', a=1000, b=b, batch=k, probe=False)
        print(f'1000x{b} k={k}: before {trio(eb and eb["applyToReportPaintedMs"], 0)} | after {trio(ea and ea["applyToReportPaintedMs"], 0)}'
              f' | applied before {trio(eb and eb["appliedMs"])} after {trio(ea and ea["appliedMs"])}'
              f' | longest after {trio(ea and ea["longestTaskMs"], 0)} long tasks {trio(ea and ea["longTasks"], 0)} n={ea and ea["applyToReportPaintedMs"]["n"]}')
print()
print('## ExPivot probes (least / median)')
for b in (9, 100, 400):
    eb = pick(before, 'pivot-wasm', a=1000, b=b, batch=1, probe=True)
    ea = pick(after, 'pivot-wasm', a=1000, b=b, batch=1, probe=True)
    for key in ('probeCubeMs', 'probeReportMs', 'probeKeysMs', 'probeRowsMs', 'probeNextCubeMs', 'probeNextReportMs'):
        print(f'1000x{b} {key}: before {trio(eb and eb.get(key))} | after {trio(ea and ea.get(key))}')
    print(f'1000x{b} painted with probe: before {trio(eb and eb["applyToReportPaintedMs"], 0)} | after {trio(ea and ea["applyToReportPaintedMs"], 0)}')
print()
print('## ExPivot same-session before (V0 hosts) and long runs')
for e in after:
    if e['name'] == 'pivot-wasm' and (e.get('build', '').startswith('before') or len(e.get('runs', [])) > 20):
        print(f'  {e["build"]} {e["a"]}x{e["b"]} k={e["batch"]} probe={e["probe"]} n={e["applyToReportPaintedMs"]["n"]}: painted {trio(e["applyToReportPaintedMs"], 0)} applied {trio(e["appliedMs"])} at {e["at"]}')
print()
print('## ExGrid, Apply -> painted frame (least / median / most)')
for rows in (100000, 1000000):
    for k in (1, 100, 1000):
        eb = pick(before, 'grid-wasm', rows=rows, batch=k, probe=False)
        ea = pick(after, 'grid-wasm', rows=rows, batch=k, probe=False)
        print(f'{rows:,} k={k}: before {trio(eb and eb["applyToPaintedMs"])} | after {trio(ea and ea["applyToPaintedMs"])}'
              f' | applied before {trio(eb and eb["appliedMs"])} after {trio(ea and ea["appliedMs"])}'
              f' | grid changed {eb and eb["gridChanged"]} / {ea and ea["gridChanged"]} | frame-paint after {trio(ea and ea["frameToPaintedMs"])}')
eb = pick(before, 'grid-wasm', rows=1000000, batch=1000, probe=True)
ea = pick(after, 'grid-wasm', rows=1000000, batch=1000, probe=True)
for key in ('probeSourceMs', 'probeKeysMs', 'probeRowsMs'):
    print(f'probe 10^6 k=1000 {key}: before {trio(eb and eb.get(key))} | after {trio(ea and ea.get(key))}')
print()
print('## Pushed 10^6 Window')
for name in ('grid-wasm-pushed', 'grid-wasm-pushed-vouched'):
    for k in (1000, 1):
        e = pick(after, name, rows=1000000, batch=k)
        print(f'{name} k={k}: painted {trio(e and e["applyToPaintedMs"])} applied {trio(e and e["appliedMs"])} frame->painted {trio(e and e["frameToPaintedMs"])} longest {trio(e and e["longestTaskMs"], 0)} changed {e and e["gridChanged"]}')
print()
print('## Server bytes (median (min-max))')
for k in (1, 100, 1000):
    eb = pick(before, 'grid-server-bytes', rows=1000000, batch=k)
    ea = pick(after, 'grid-server-bytes', rows=1000000, batch=k)
    def b(e, key):
        if not e or not e.get(key):
            return '—'
        s = e[key]
        return f'{s["median"]:,} ({s["min"]:,}–{s["max"]:,})'
    for key in ('payload', 'wire', 'laterPayload', 'laterWire', 'clientPayload'):
        print(f'k={k} {key}: before {b(eb, key)} | after {b(ea, key)}')
    if ea:
        print(f'   after extensions {ea.get("webSocketExtensions")}, connection {ea.get("connection")}, poll {ea.get("poll")}')
