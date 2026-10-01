"""Condenses the probe's records to one line per page, case and state, and compares the configurations.

    python3 summarise.py records/            -> the lines, then for each state whether every
                                                configuration read the same, and what differed

A state's line holds what the tables of pointing-scope.md are read from: where DOM focus is, whether an
edit is open, the Cell Editor's text and caret, the Name Box, the completion list and its selected
option, what is written after Enter (the target cell's text), whether the positions grid is pointed at,
its own Focus and Selection, its Reference Outlines and dashes (the cells each covers, its colour, and
whether it lies inside the scroller's client area), the refusal the page shows, and the dashes' pixels
along each side (runs, and how far along the side they reach).
"""
import json
import os
import re
import sys
from collections import defaultdict


def grid_part(g):
    if not g:
        return None
    return {
        'pointedAt': g['pointedAt'],
        # The cell's id without the instance's prefix (ex2-, ex4-…), which differs from load to load.
        'ownFocus': re.sub(r'^ex\d+-', '', g['activeDescendant']) if g['activeDescendant'] else None,
        'ownSelectionDrawn': g['selectionDrawn'],
        'outlines': [(o['cls'].split()[-1], o['lineColour'], o['covers']['columns'], o['covers']['rows'], o['insideClient']) for o in g['outlines']],
        'dashes': [(o['outline'], o['covers']['columns'], o['covers']['rows'], o['insideClient']) for o in g['dashes']],
        'scroll': (g['scroller']['top'], g['scroller']['left']),
    }


def reach(text):
    """'whole' when the dashes run from within 4 device px of a side's start to within 4 px of its end
    (a device pixel of rounding moves them between browsers); otherwise how much of the side they cover,
    to the nearest 5%."""
    if not text:
        return 'none'
    span, of = text.split(' of ')
    first, last = (int(x) for x in span.split('-'))
    if first <= 4 and last >= int(of) - 5:
        return 'whole'
    return f'{round((last - first + 1) / int(of) * 20) * 5}% of the side'


def signature(state):
    r = state['read']
    sig = {
        'active': r['active'],
        'editing': r['editing'],
        'cell': (r['cell'] or {}).get('value'),
        'caret': (r['cell'] or {}).get('selection'),
        'nameBox': r['nameBox'],
        'focus': r['focus'],
        'completion': [(o['text'], o['selected']) for o in (r['completion'] or [])] if r['completion'] is not None else None,
        'target': r['targetText'],
        'positions': grid_part(r['positions']),
        'other': grid_part(r.get('other')),
        'refusal': r['refusal'],
        'pointedSpans': [s['text'] for s in (((r['cell'] or {}).get('layer') or {}).get('spans') or []) if s['pointed']],
    }
    if r.get('cursorUnderPointer'):
        sig['cursor'] = r['cursorUnderPointer']['cursor']
        sig['osCursor'] = 'system arrow' if 'system=arrow' in (r.get('osCursor') or '') else 'not a system cursor' if "none of the system's" in (r.get('osCursor') or '') else r.get('osCursor')
    px = []
    for b in state['pixels']['boxes']:
        if b['kind'] == 'dashes':
            px.append(('dashes', b['covers']['columns'], b['covers']['rows'], {k: (v['runs'], reach(v['reach'])) for k, v in b['sides'].items()}, b.get('changedIn300ms')))
        elif b['kind'] == 'outline':
            px.append(('outline', b['covers']['columns'], b['line'][0].split(':')[0] if b['line'] else None, b['fill'][0].split(':')[0] if b['fill'] else None))
    sig['pixels'] = px
    return sig


def main(folder):
    by_state = defaultdict(dict)
    order = []
    runs = []
    for name in sorted(os.listdir(folder)):
        if not name.endswith('.json') or name.startswith(('trial', 'fast-', 'hc-', 'extra-')):
            continue
        d = json.load(open(os.path.join(folder, name)))
        config = f"{d['label']}-{d['channel']}"
        # The page, from the file's name (<page>-<label>-<channel>.json): the record's "page" holds what
        # the page reported of itself (its scale, its size, its colour scheme).
        page = name.split('-')[0]
        d['page'] = page
        runs.append((page, config, d.get('browserVersion'), len(d['messages'])))
        for c in d['cases']:
            if 'error' in c:
                key = (d['page'], c['case'], 'ERROR')
                by_state[key][config] = c['error']
                if key not in order:
                    order.append(key)
                continue
            for s in c['states']:
                key = (d['page'], c['case'], s['state'])
                by_state[key][config] = json.dumps(signature(s), sort_keys=True)
                if key not in order:
                    order.append(key)
    for r in runs:
        print('run', *r)
    same = 0
    for key in order:
        configs = by_state[key]
        values = set(configs.values())
        if len(values) == 1:
            same += 1
            print(f"{'/'.join(key)}: same in {len(configs)}: {next(iter(values))}")
        else:
            print(f"{'/'.join(key)}: DIFFERS")
            for v in values:
                print(f"    {[c for c, x in configs.items() if x == v]}: {v}")
    print(f'{same} of {len(order)} states read the same in every configuration')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'records')
