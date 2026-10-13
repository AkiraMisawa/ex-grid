"""f4-enter-mode.md questions 1 to 8 (run 18a): Excel's Formula Bar, Name Box and mode after each key.

A blank workbook in an Excel of xl-server.ps1's own (a Sheet2 added for question 4), D10 selected; every key and press
a real one (SendInput). Each case starts from a cleared sheet with D10 selected. After every key, with no key sent to
read it:

  - the Formula Bar's text and its selection or caret (start and end offsets) through UI Automation ('bar2');
  - the status bar's Cell Mode through UI Automation;
  - a picture (Excel's windows drawn by themselves), cut to the bar with the Name Box, the status bar's mode and C8:F14.

At the end Escape until Excel answers COM; where it answered straight after the keys, the entry was committed and the
cells and the Selection are read through COM.

    python3 f4.py [q1 q2a q2b q3 q4 q5a q5b q6a q6b q7 q8]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'f4.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'f4-xs.jsonl'))
L = xs.lit
# The bar's text area ends at x 3801 on this 3840-wide screen at 150% (the 2026-10-12 readings, R1): a press 8 px inside it.
BAR = (3793, 295)

CASES = {
    'q1': [('press', BAR), ('keys', L('=A1+B2')), ('keys', '{F2}'), ('keys', '{F4}')],
    'q2a': [('press', BAR), ('keys', L('=A1+B2')), ('keys', '{LEFT}'), ('keys', '{RIGHT}'), ('keys', '{F4}')],
    'q2b': [('press', BAR), ('keys', L('=A1+B2')), ('keys', '{LEFT}'), ('keys', '{LEFT}'), ('keys', '{LEFT}'), ('keys', '0'), ('keys', '{F4}')],
    'q3': [('keys', '='), ('keys', '{DOWN}'), ('keys', '+{DOWN}'), ('keys', '+{HOME}')],
    'q4': [('keys', '{F2}'), ('keys', L('=Sheet2!A1')), ('keys', '{F4}')],
    'q5a': [('keys', '='), ('keys', '^{DOWN}')],
    'q5b': [('keys', '='), ('keys', '^+{DOWN}')],
    'q6a': [('keys', '='), ('keys', '{DOWN}')],
    'q6b': [('keys', '='), ('keys', '{DOWN}'), ('keys', '{DOWN}')],
    'q8': [('keys', '='), ('keys', '+{HOME}')],
}
Q7 = ['+{UP}', '^{LEFT}', '^{UP}', '^{DOWN}', '^+{LEFT}', '^+{UP}', '^+{DOWN}']


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:1200], flush=True)


def safe(name):
    return ''.join(ch if ch.isalnum() or ch in '-_' else '-' for ch in name)


def mode():
    s = json.loads(xs.cmd('status')[len('ok status '):].rsplit(' t=', 1)[0])
    return next((x[len('Cell Mode '):] for x in s if x.startswith('Cell Mode ')), None)


def bar():
    return json.loads(xs.cmd('bar2')[len('ok bar2 '):].rsplit(' t=', 1)[0])


def look(name):
    name = safe(name)
    xs.cmd(f'shot {name}')
    xs.cmd(f'crop {name} 0 255 1400 80 {name}-bar 1')
    xs.cmd(f'crop {name} 0 2055 600 33 {name}-mode 2')
    x0, y0 = G['C8'][0] - 50, G['C8'][1] - 50
    xs.cmd(f"crop {name} {x0} {y0} {G['F14'][2] + 10 - x0} {G['F14'][3] + 10 - y0} {name}-cells 1")
    return {'shot': name, 'bar': bar(), 'mode': mode()}


def back_to_ready():
    for _ in range(4):
        r = xs.ready()
        if r.startswith('ready='):
            return r
        xs.keys('{ESC}'); time.sleep(0.4)
    return xs.ready()


def fresh():
    xs.cmd('clearsheet'); xs.cmd('select D10'); time.sleep(0.3)


def describe(kind, arg):
    return f'press({arg[0]},{arg[1]})' if kind == 'press' else arg


def run_case(cid, steps, setup=None):
    fresh()
    if setup:
        setup()
    record({'reading': 'F4', 'case': cid, 'step': 0, 'keys': '', **look(f'f4-{cid}-0')})
    for n, (kind, arg) in enumerate(steps, 1):
        if kind == 'press':
            xs.cmd(f'click {arg[0]} {arg[1]}')
        else:
            xs.keys(arg, gap=80)
        time.sleep(0.6)
        record({'reading': 'F4', 'case': cid, 'step': n, 'keys': describe(kind, arg), **look(f'f4-{cid}-{n}')})
    r = xs.ready()
    row = {'reading': 'F4', 'case': cid, 'step': 'end', 'readyAfterKeys': r}
    if r.startswith('ready='):
        row['cells'] = xs.cells('D10', 'D11', 'D12', 'E10', 'C10')
    else:
        row['afterEscape'] = back_to_ready()
    record(row)


def q7():
    for n, key in enumerate(Q7, 1):
        run_case(f'q7-{n}', [('keys', 'abc'), ('keys', key)])


if __name__ == '__main__':
    xs.cmd('select D10')
    G = xs.geo('C8', 'F14', 'D10')
    record({'reading': 'F4', 'case': 'start', 'geo': G, 'uia': xs.cmd('uia'), 'layout': xs.cmd('layout'),
            'international32': xs.cmd('ps $xl.International(32)')})
    for name in (sys.argv[1:] or list(CASES) + ['q7']):
        if name == 'q7':
            q7()
        else:
            run_case(name, CASES[name])
