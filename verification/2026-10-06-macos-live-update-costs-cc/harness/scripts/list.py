import json, sys
def g(d, k):
    v = d.get(k) or {}
    return f"{v.get('min')}/{v.get('median')}/{v.get('max')}"
for d in json.load(open(sys.argv[1])):
    if d['name'] == 'grid-wasm':
        print(d['at'][11:19], d['rows'], d['batch'], d['probe'], len(d['runs']), 'applied', g(d,'appliedMs'), 'painted', g(d,'applyToPaintedMs'), 'f->p', g(d,'frameToPaintedMs'), 'longest', g(d,'longestTaskMs'), 'changed', d.get('gridChanged'), 'src', g(d,'probeSourceMs'), 'keys', g(d,'probeKeysMs'), 'rows', g(d,'probeRowsMs'))
    elif d['name'] == 'pivot-wasm':
        print(d['at'][11:19], 'pivot', d['b'], d['batch'], d['probe'], len(d['runs']), 'applied', g(d,'appliedMs'), 'painted', g(d,'applyToReportPaintedMs'), 'longest', g(d,'longestTaskMs'), 'longTasks', g(d,'longTasks'), 'cube', g(d,'probeCubeMs'), 'report', g(d,'probeReportMs'), 'keys', g(d,'probeKeysMs'), 'rows', g(d,'probeRowsMs'), d.get('load'))
    else:
        print(d['at'][11:19], d['name'], d['rows'], d['batch'], 'payload', g(d,'payload'), 'wire', g(d,'wire'), 'batches', g(d,'renderBatches'), 'client', g(d,'clientPayload'), 'later', g(d,'laterRenderBatches'), g(d,'laterPayload'), g(d,'laterWire'), d.get('connection'))
