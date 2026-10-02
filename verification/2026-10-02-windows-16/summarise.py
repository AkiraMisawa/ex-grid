"""Reads records/*.json (ime-probe.mjs) and writes, per case and state, what was read, grouped by the
configurations that read the same (the events the page saw, the Cell Editor's own scroll, the colours read
from the pictures and the accessibility tree are listed under each state per configuration, not grouped
by): the keyboard's place, the Keyboard Field (its value, whether it composes, its opacity, read-only),
the root's focus ring mark, the edit, the Focus and the Name Box, the surface's value, caret and coloured
layer, the list, the points and outlines, Find, the cells the page names, the other grids on the page, the
IME's open status, and the events the page saw (keydowns with isComposing, composition events with their
data, input events, presses, copy and paste). The fifteenth run's script (2026-10-01-windows-15/summarise.py)
with this run's readings. Trial records (trial*) are not read.

    python3 summarise.py > summary.txt
"""
import glob
import json
import os
import sys
from collections import OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
ORDER = ([f'{p}-{l}-{c}' for l in ('wasm', 'server', 'server-150') for c in ('chrome', 'msedge') for p in ('sheet', 'mud', 'features', 'features-mud', 'sheets', 'sheets-mud')]
         + [f'partc-{p}-{l}-chrome' for l in ('wasm', 'server') for p in ('sheet', 'mud', 'features')])


def events(evs):
    out = []
    for e in evs:
        t = e['type']
        if t == 'keydown':
            out.append(f"keydown {e.get('key')}{' composing' if e.get('isComposing') else ''}@{e['target']}")
        elif t == 'mousedown':
            out.append(f"mousedown@{e['target']}")
        elif t in ('copy', 'paste'):
            out.append(f"{t} {e.get('text')!r}@{e['target']}")
        elif t.startswith('composition'):
            out.append(f"{t[11:]} {e.get('data')!r}@{e['target']}")
        elif t == 'input':
            out.append(f"input {e.get('inputType')} {e.get('data')!r}{' composing' if e.get('isComposing') else ''}")
    return '; '.join(out)


def surface_of(r):
    a = r['active']
    if a == 'bar':
        return 'bar', r['bar']
    if a == 'name-box':
        return 'name box', r['nameBoxField']
    if a == 'key-field':
        return 'cell', r['cell']
    if a == 'find':
        return 'find', None
    return 'cell', r['cell']


def reading(state):
    r = state['read']
    where, s = surface_of(r)
    parts = OrderedDict()
    parts['keyboard'] = r['active']
    k = r.get('keyField')
    if k:
        parts['Keyboard Field'] = f"{k['value']!r}{' composing' if k['composing'] else ''}, opacity {k['opacity']}{', read-only' if k['readOnly'] else ''}"
    parts['ring mark'] = r['root']['focusVisibleMark']
    parts['edit'] = 'open' if r['editing'] else 'closed'
    parts['Focus'] = r['focus']
    parts['Name Box'] = r['nameBox']
    if s:
        parts[where] = f"{s['value']!r} caret {s['selection']}"
        if s.get('layer') is not None:
            spans = ', '.join(f"{x['text']} {x['color']}" for x in s['layer']['spans'])
            parts['layer'] = f"{'shown' if s['shown'] else 'hidden'} {s['layer']['text']!r}{' [' + spans + ']' if spans else ''}"
    if where != 'bar' and r['bar'] is not None:
        parts['bar'] = repr(r['bar']['value'])
    if r['find']:
        parts['Find'] = f"{r['find']['value']!r} outcome {r['find']['outcome']!r}"
    if r['completion'] is not None:
        parts['list'] = ' / '.join(('*' if o['selected'] else '') + o['text'] for o in r['completion']) or '(empty)'
    if r['points'] or r['outlines']:
        parts['drawn'] = f"{len(r['points'])} point(s), {len(r['outlines'])} outline(s)"
    if r['message']:
        parts['message'] = ' | '.join(r['message'])
    if r.get('sorted'):
        parts['sorted'] = ', '.join(r['sorted'])
    cells = {a: t for a, t in r['cells'].items() if t}
    parts['cells'] = ', '.join(f"{a} {t!r}" for a, t in cells.items()) or '(the named cells are empty)'
    if len(r['grids']) > 1:
        # A grid by its nearest container's id; the Sheet's own container has none on the Server host and
        # is the app's root on WebAssembly, so it goes by its place on the page instead.
        parts['grids'] = '; '.join(f"{g['in'] if g['in'] not in (None, 'app') else 'grid ' + str(g['i'])}: {'keyboard ' if g['hasKeyboard'] else ''}{'edit ' + repr(g['editorValue']) if g['editing'] else 'no edit'}"
                                   f"{', field ' + repr(g['keyField']['value']) + (' composing' if g['keyField']['composing'] else '') if g['keyField'] and g['keyField']['value'] else ''}, Focus {g['focus']}" for g in r['grids'])
    ime = state['ime'].replace('ok imestate ', '')
    parts['IME'] = ime.split(' mode=')[0] + (' ' + ime.split('(')[1].split(')')[0] if '(' in ime else '')
    return parts


def scroll_note(state):
    """The Cell Editor's own horizontal scroll: kept out of the grouping, since its widths differ with
    the Chrome's font and padding."""
    c = state['read']['cell']
    if not c or not c.get('clientWidth'):
        return ''
    return f"scrollLeft {c['scrollLeft']} of scrollWidth {c['scrollWidth']} (clientWidth {c['clientWidth']}), caret at {c['selection'][1]} of {len(c['value'])}"


def ax_note(state):
    a = state.get('ax')
    if not a:
        return ''
    if 'error' in a:
        return a['error']
    d = a.get('activeDescendant')
    return f"{a['tag']}.{a.get('cls') or ''} role {a['role']} name {a['name']!r} focused {a['focused']}; active descendant " + (f"{d['role']} {d['name']!r} ({d['idref']})" if d else 'none')


def pixels_note(state):
    """The colours seen in each coloured span's box, and in the whole field."""
    out = []
    for b in state.get('pixels', {}).get('boxes', []):
        out.append(f"{b['text']}: saturated {', '.join(b['saturated']) or 'none'}, darkest {b['darkest']}")
    return '; '.join(out)


def main():
    recs = OrderedDict()
    for f in sorted(glob.glob(os.path.join(HERE, 'records', '*.json'))):
        name = os.path.basename(f)[:-5]
        if name.startswith('trial'):
            continue
        r = json.load(open(f, encoding='utf-8'))
        if 'cases' in r:   # d5's records (d5-narrator.mjs) are read in the report, not here
            recs[name] = r
    names = [n for n in ORDER if n in recs] + [n for n in recs if n not in ORDER]
    cases = OrderedDict()
    for n in names:
        for c in recs[n]['cases']:
            # The record's name says its page: the probe's own "page" holds the page's report (DPR, size).
            key = c['case'] + ('@features' if c['case'] == 'pc' and n.startswith('partc-features') else '')
            cases.setdefault(key, OrderedDict())[n] = c
    total = same = 0
    for cid, by in cases.items():
        first = next(iter(by.values()))
        print(f"=== {cid}: {first.get('what', '')}")
        errs = [n for n, c in by.items() if c.get('error')]
        if errs:
            listed = ', '.join(n + ': ' + by[n]['error'] for n in errs)
            print(f"  ERRORS: {listed}")
        states = OrderedDict()
        for n, c in by.items():
            for s in c.get('states', []):
                states.setdefault(s['state'], OrderedDict())[n] = s
        for sname, per in states.items():
            total += 1
            groups = OrderedDict()
            for n, s in per.items():
                key = json.dumps(reading(s), ensure_ascii=False)
                groups.setdefault(key, []).append(n)
            if len(groups) == 1:
                same += 1
            print(f"  -- {sname}: {'the same in all ' + str(len(per)) if len(groups) == 1 else str(len(groups)) + ' readings'}")
            for key, ns in groups.items():
                if len(groups) > 1:
                    print(f"     [{', '.join(ns)}]")
                for k, v in json.loads(key, object_pairs_hook=OrderedDict).items():
                    print(f"       {k}: {v}")
            ev = OrderedDict()
            for n, st in per.items():
                ev.setdefault(events(st['read']['events']) or '(none)', []).append(n)
            for k, ns in ev.items():
                print(f"       events{'' if len(ev) == 1 else ' [' + ', '.join(ns) + ']'}: {k}")
            sc = OrderedDict()
            for n, st in per.items():
                sc.setdefault(scroll_note(st), []).append(n)
            if any(k for k in sc):
                for k, ns in sc.items():
                    print(f"       cell scroll{'' if len(sc) == 1 else ' [' + ', '.join(ns) + ']'}: {k}")
            ax = OrderedDict()
            for n, st in per.items():
                ax.setdefault(ax_note(st), []).append(n)
            if any(k for k in ax):
                for k, ns in ax.items():
                    print(f"       accessibility{'' if len(ax) == 1 else ' [' + ', '.join(ns) + ']'}: {k}")
            px = OrderedDict()
            for n, s in per.items():
                px.setdefault(pixels_note(s), []).append(n)
            if any(k for k in px):
                for k, ns in px.items():
                    print(f"       pixels{'' if len(px) == 1 else ' [' + ', '.join(ns) + ']'}: {k}")
        d10 = OrderedDict()
        for n, c in by.items():
            d10.setdefault(repr(c.get('d10Entry')), []).append(n)
        print(f"  D10's Entry afterwards: " + ' | '.join(f"{k}{'' if len(d10) == 1 else ' [' + ', '.join(v) + ']'}" for k, v in d10.items()))
        resets = [f"{n}: {c['before'].get('offInAnEdit')}" for n, c in by.items() if (c.get('before') or {}).get('offInAnEdit')]
        if resets:
            print(f"  the IME switched off in an F2 edit before the case: {', '.join(resets)}")
    print(f"\n{same} of {total} states read the same in every configuration that ran them.")
    print('\nConsole messages per record:')
    for n in names:
        msgs = recs[n]['messages']
        kinds = OrderedDict()
        for m in msgs:
            kinds.setdefault(f"{m['type']}: {m['text'][:100]}", 0)
            kinds[f"{m['type']}: {m['text'][:100]}"] += 1
        print(f"  {n} ({recs[n].get('browserVersion')}): " + ('; '.join(f'{k} x{v}' for k, v in kinds.items()) or 'none'))
    print('\nThe keyboard per record (before, set, after):')
    for n in names:
        k = recs[n]['keyboard']
        print(f"  {n}: {k.get('before')} | {k.get('set')} | {k.get('after')} | {k.get('imeAfter')}")


if __name__ == '__main__':
    sys.exit(main())
