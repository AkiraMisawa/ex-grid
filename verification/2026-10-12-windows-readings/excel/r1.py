"""R1 (ADR-0051, the Formula Bar): Excel's Formula Bar and cell after =, the arrows, F4 and the Shift keys.

A blank workbook in an Excel of xl-server.ps1's own, D10 selected; every key and press a real one (SendInput).
Each step is its own trial from a cleared sheet (COM Cells.Clear, D10 selected), so that reading one step never
changes the next: the trial's keys up to that step, then

  - without a key: a picture (the bar, the status bar's mode, C9 to E13), the bar's text and selection through UI
    Automation, the status bar's "Cell Mode";
  - then, as Part C read it but never with F2: Ctrl+C (what is selected), Shift+Home and Ctrl+C (from the start to
    the caret), each through Excel's own clipboard; the bar and the mode again after the reading;
  - Escape until Excel is Ready. When Excel answers COM straight after the keys, no edit is open: the entry was
    committed (or never opened), and the cells and the selection are read instead.

    python3 r1.py [i ii iii iv v vi vii viii]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'r1.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'r1-xs.jsonl'))
L = xs.lit
# The bar's text area ends at x 3801 (the expand button spans 3802 to 3826, the white box's edge is 3827, on this
# 3840-wide screen at 150%): the press is 8 px inside the text area's right end. 8 px inside the white box (3819)
# lands on the expand button, which expands the bar and opens no edit (recorded as step 0).
BAR = (3793, 295)
BAR_BOX_EDGE = (3819, 295)

STEPS = {
    'i': [('press', BAR), ('keys', '='), ('keys', '{DOWN}')],
    'iii': [('press', BAR), ('keys', '='), ('keys', '{DOWN}'), ('keys', '{F4}'), ('keys', L('+'))],
    'iv': [('press', BAR), ('keys', L('=B2')), ('keys', '{F4}')],
    'v': [('press', BAR), ('keys', L('=SUM(A1:B2)')), ('keys', '{LEFT}'), ('keys', '{F4}')],
    'vi': [('keys', '='), ('keys', '{DOWN}'), ('keys', '+{DOWN}'), ('keys', '{F4}')],
    'viii': [('keys', '='), ('keys', '{DOWN}'), ('keys', '+{HOME}')],
}
STEPS['ii'] = STEPS['i'] + [('keys', '{F4}'), ('keys', '{DOWN}')]
VII = [('abc', '+{RIGHT}'), ('abc', '+{DOWN}'), (L('=A1+B2'), '+{LEFT}'), ('abc', '+{END}'), ('abc', '^{RIGHT}'), ('abc', '^+{RIGHT}')]


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:900], flush=True)


def safe(name):
    return ''.join(ch if ch.isalnum() or ch in '-_' else '-' for ch in name)


def mode():
    s = json.loads(xs.cmd('status')[len('ok status '):].rsplit(' t=', 1)[0])
    return next((x[len('Cell Mode '):] for x in s if x.startswith('Cell Mode ')), None)


def bar():
    b = json.loads(xs.cmd('bar')[len('ok bar '):].rsplit(' t=', 1)[0])
    return [{k: e[k] for k in ('value', 'text', 'selection', 'focus')} for e in b]


def look(name):
    name = safe(name)
    xs.cmd(f'shot {name}')
    xs.cmd(f'crop {name} 0 255 3840 80 {name}-bar 1')
    xs.cmd(f'crop {name} 0 2055 600 33 {name}-mode 2')
    x0, y0 = G['C9'][0] - 50, G['C9'][1] - 50
    xs.cmd(f"crop {name} {x0} {y0} {G['E13'][2] + 10 - x0} {G['E13'][3] + 10 - y0} {name}-cells 2")
    return {'shot': name, 'bar': bar(), 'mode': mode()}


def copy(keys):
    xs.cmd('clipclear')
    xs.keys(keys, gap=60)
    time.sleep(0.4)
    return xs.clipboard()


def back_to_ready():
    for _ in range(4):
        r = xs.ready()
        if r.startswith('ready='):
            return r
        xs.keys('{ESC}'); time.sleep(0.4)
    return xs.ready()


def fresh():
    xs.cmd('clearsheet'); xs.cmd('select D10'); time.sleep(0.3)


def run(steps):
    for kind, arg in steps:
        if kind == 'press':
            xs.cmd(f'click {arg[0]} {arg[1]}')
        else:
            xs.keys(arg, gap=80)
        time.sleep(0.5)


def describe(steps):
    return ' '.join(f'press({a[0]},{a[1]})' if k == 'press' else a for k, a in steps)


def trial(rid, k, steps):
    fresh()
    run(steps)
    time.sleep(0.4)
    row = {'reading': 'R1', 'case': rid, 'step': k, 'keys': describe(steps), **look(f'r1-{rid}-{k}')}
    r = xs.ready()
    row['ready'] = r
    if r.startswith('ready='):
        row['committed'] = True
        row['cells'] = xs.cells('D10', 'D11', 'D12', 'E10', 'A1')
    else:
        row['committed'] = False
        row['selected'] = copy('^c')
        row['toCaret'] = copy('+{HOME}^c')
        row['afterReading'] = {'bar': bar(), 'mode': mode()}
        xs.cmd(f"shot {safe(f'r1-{rid}-{k}-after-reading')}")
        row['afterEscape'] = back_to_ready()
    record(row)


def case(rid):
    steps = STEPS[rid]
    for k in range(1, len(steps) + 1):
        trial(rid, k, steps[:k])


def vii():
    for n, (text, key) in enumerate(VII, 1):
        fresh()
        xs.keys(text, gap=80); time.sleep(0.4)
        before = {'bar': bar(), 'mode': mode()}
        xs.keys(key, gap=80); time.sleep(0.6)
        row = {'reading': 'R1', 'case': 'vii', 'step': n, 'keys': f'{text} {key}', 'beforeKey': before, **look(f'r1-vii-{n}')}
        r = xs.ready()
        row['ready'] = r
        if r.startswith('ready='):
            row['committed'] = True
            row['cells'] = xs.cells('D10', 'E10', 'D11', 'C10')
        else:
            row['committed'] = False
            row['selected'] = copy('^c')
            row['toCaret'] = copy('+{HOME}^c')
            row['afterEscape'] = back_to_ready()
        record(row)


def step0():
    """The press 8 px inside the bar's white box: on the expand button."""
    fresh()
    xs.cmd(f'click {BAR_BOX_EDGE[0]} {BAR_BOX_EDGE[1]}'); time.sleep(0.8)
    row = {'reading': 'R1', 'case': 'press-at-box-edge', 'step': 1, 'keys': f'press({BAR_BOX_EDGE[0]},{BAR_BOX_EDGE[1]})', **look('r1-press-at-box-edge'), 'ready': xs.ready()}
    record(row)
    # The bar put back: the same button again.
    xs.cmd(f'click 3813 284'); time.sleep(0.8)
    record({'reading': 'R1', 'case': 'press-at-box-edge', 'step': 2, 'keys': 'press(3813,284), the button again', **look('r1-press-at-box-edge-2'), 'ready': xs.ready()})


if __name__ == '__main__':
    xs.cmd('select D10')
    G = xs.geo('C9', 'E13', 'D10', 'D12')
    record({'reading': 'R1', 'case': 'start', 'geo': G, 'uia': xs.cmd('uia'), 'layout': xs.cmd('layout')})
    for name in (sys.argv[1:] or ['step0', 'i', 'ii', 'iii', 'iv', 'v', 'vi', 'vii', 'viii']):
        if name in ('vii', 'step0'):
            globals()[name]()
        else:
            case(name)
