#!/usr/bin/env python3
"""Ticket 13: the circuit's bytes per update, derived from the per-connection counts in
raw/browser-results.json, by ticket 01's method (its raw/server-bytes.json): the connection that
writes about 548 bytes at every reading, with or without a batch, is the harness polling
GET /api/wire and is excluded; the circuit's is the one whose bytes follow the render batches.
The spec's own pick of the connection (`connection`, `wire`) names the polling one here, as it did
in ticket 01's runs, and is not read."""
import json
import os
import statistics
import sys

raw = sys.argv[1]
entries = [e for e in json.load(open(os.path.join(raw, 'browser-results.json'))) if e['name'] == 'grid-server-bytes']


def stat(values):
    return {'min': min(values), 'median': statistics.median_low(values), 'max': max(values), 'mean': round(sum(values) / len(values), 1)}


results = []
for e in entries:
    samples = e['samples']
    ids = samples[0]['wireByConnection'].keys()
    polling = [c for c in ids if all(540 <= s['wireByConnection'][c] <= 560 for s in samples) and all(540 <= s['laterWireByConnection'][c] <= 560 for s in samples)]
    totals = {c: sum(s['wireByConnection'][c] + s['laterWireByConnection'][c] for s in samples) for c in ids if c not in polling}
    circuit = max(totals, key=totals.get)
    update = [s['wireByConnection'][circuit] for s in samples]
    unmark = [s['laterWireByConnection'][circuit] for s in samples]
    results.append({
        'rows': e['rows'], 'batch': e['batch'], 'presses': len(samples),
        'circuitConnection': circuit, 'pollConnection': polling,
        'updateRenderBatchPayload': stat([s['payload'] for s in samples]),
        'updateWire': stat(update),
        'unmarkRenderBatchPayload': stat([s['laterPayload'] for s in samples]),
        'unmarkWire': stat(unmark),
        'unmarkBatches': sum(s['laterRenderBatches'] for s in samples),
        'perUpdateTotalWireMean': round(sum(u + m for u, m in zip(update, unmark)) / len(samples), 1),
        'perUpdateTotalPayloadMean': round(sum(s['payload'] + s['laterPayload'] for s in samples) / len(samples), 1),
        'clientPayloadPerPress': stat([s['clientPayload'] for s in samples]),
        'webSocketExtensions': e.get('webSocketExtensions'),
    })
out = {'derivedFrom': 'browser-results.json (grid-server-bytes entries, per-connection deltas)',
       'method': 'the circuit connection is the one whose bytes follow the render batches; the one writing about 548 bytes at every reading, with or without a batch, is the harness polling GET /api/wire, and is excluded',
       'results': results}
with open(os.path.join(raw, 'server-bytes.json'), 'w') as f:
    json.dump(out, f, indent=1)
for r in results:
    print(r['rows'], r['batch'], 'payload', r['updateRenderBatchPayload'], 'wire', r['updateWire'], 'unmark', r['unmarkRenderBatchPayload'], r['unmarkWire'], 'per update', r['perUpdateTotalWireMean'])
