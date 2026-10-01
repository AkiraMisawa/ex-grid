"""Summarise part-b-probe.mjs's records (part-b/*.json) into one line per case, surface and state,
and say where the configurations (host, round trip, browser) differ from one another.

    python3 summarise.py part-b/wasm-chrome.json ...   one line per state; "same in N" or DIFFERS
    python3 summarise.py --pixels part-b/*.json          the states whose pixel readings differ
"""
import json
import sys
from collections import defaultdict


def spans(surface):
    if not surface or not surface.get('layer'):
        return 'no layer'
    out = []
    for s in surface['layer']['spans']:
        t = f"{s['text']} {s['color']}"
        if s['fill'] != s['color']:
            t += f" fill {s['fill']}"
        if s['background'] not in ('#000000/0', None):
            t += f" on {s['background']}"
        out.append(t)
    return '; '.join(out) or 'no Reference'


def signature(read, px):
    """What a reader would see, from the DOM, in words: the text coloured in which surface, the
    outlines and the dashes, and the positions grid's outlines."""
    active = read['active']
    parts = [f"edit in {active}" if read['editing'] else f"no edit ({active})"]
    for name in ('cell', 'bar'):
        s = read.get(name)
        if s is None:
            continue
        shown = 'coloured' if s['shown'] and s['layer'] and s['layer']['visibility'] == 'visible' else 'plain'
        parts.append(f"{name} {shown} {json.dumps(s['value'])}" + (f" [{spans(s)}]" if shown == 'coloured' else ''))
    parts.append('outlines: ' + ('; '.join(f"{o['cells']} {o['color']}" for o in read['outlines']) or 'none'))
    parts.append('dashes: ' + ('; '.join(f"{o['cells']} {o['outline']}" for o in read['points']) or 'none'))
    if read.get('positions'):
        parts.append('positions grid: ' + '; '.join(f"{','.join(o['columns'])} {o['color']}" for o in read['positions']))
    if read.get('completion'):
        parts.append('list: ' + ', '.join(read['completion'][:8]))
    return ' | '.join(parts)


def pixel_words(px):
    words = []
    for b in px['boxes']:
        if b['kind'] == 'text':
            if b.get('shownFraction', 1) == 0:
                words.append(f"{b['text']} out of view")
            else:
                g = (b['ground'] or '').split(':')[0]
                words.append(f"{b['text']} {b['darkest']}" + (f" on {g}" if g != '#ffffff' else ''))
        elif b['kind'] == 'outline':
            words.append(f"outline {b['cells']}: line {b['line'][0].split(':')[0] if b['line'] else '-'}, fill {b['fill'][0].split(':')[0] if b['fill'] else '-'}")
        else:
            d = b.get('dashes') or {}
            words.append(f"dashes {b['cells']}: {d.get('colour')} x{d.get('runsAlongTop')}, changed in 300 ms {b.get('changedIn300ms')}")
    return '; '.join(words)


def main(files):
    by_key = defaultdict(dict)
    configs = []
    for f in files:
        d = json.load(open(f, encoding='utf-8'))
        cfg = f"{d['label']}-{d['channel']}"
        configs.append(cfg)
        for c in d['cases']:
            if 'excluded' in c:
                by_key[(c['case'], '-', '-')][cfg] = ('excluded: ' + c['excluded'], '')
                continue
            if 'error' in c:
                by_key[(c['case'], c.get('surface', '?'), 'error')][cfg] = (c['error'], '')
                continue
            for s in c['states']:
                surface = s['read']['active'] if c['case'] == '21' else c['surface']
                by_key[(c['case'], surface, s['state'])][cfg] = (signature(s['read'], s['pixels']), pixel_words(s['pixels']))
    for key in by_key:
        seen = by_key[key]
        sigs = {v[0] for v in seen.values()}
        print(f"== {key[0]} {key[1]} {key[2]}: {'same in ' + str(len(seen)) if len(sigs) == 1 else 'DIFFERS'}")
        if len(sigs) == 1:
            first = next(iter(seen.values()))
            print('   ' + first[0])
            print('   px: ' + first[1])
        else:
            for cfg, v in seen.items():
                print(f"   [{cfg}] {v[0]}")
                print(f"   [{cfg}] px: {v[1]}")



def pixel_differences(files):
    """The states whose pixel readings differ between configurations, and how."""
    by_key = defaultdict(dict)
    for f in files:
        d = json.load(open(f, encoding='utf-8'))
        cfg = f"{d['label']}-{d['channel']}"
        for c in d['cases']:
            for s in c.get('states', []):
                surface = s['read']['active'] if c['case'] == '21' else c['surface']
                by_key[(c['case'], surface, s['state'])][cfg] = pixel_words(s['pixels'])
    for key, seen in by_key.items():
        if len(set(seen.values())) > 1:
            print(f"== {key}")
            for cfg, v in seen.items():
                print(f"   [{cfg}] {v}")


if __name__ == '__main__':
    if sys.argv[1:2] == ['--pixels']:
        pixel_differences(sys.argv[2:])
    else:
        main(sys.argv[1:])
