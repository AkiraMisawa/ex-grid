"""R6 (h7): which formats widen a column over ####, at the standard width.

Each case in a new workbook of an Excel of xl-server.ps1's own: A1 holds 123456789.12 in 0.00 (set through COM, the
format first, on the empty cell), column A at the sheet's standard width, a width nobody set, so A1 shows ########.
Then one gesture, with the real pointer and keys (SendInput), or through COM where the case says so; then A1's font
and text and column A's width, through COM, and a picture of A1:C3.

Format Cells' controls are pressed where its picture shows them, relative to the dialog's window (r6-*-dialog.png).

    python3 r6.py [case ...]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'r6.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'r6-xs.jsonl'))

# Relative to Format Cells' window, in device pixels (this machine, 150%).
TAB = {'Alignment': (154, 75), 'Font': (245, 75), 'Fill': (405, 75)}
ITALIC = (422, 197)
UNDERLINE_LIST, SINGLE = (210, 341), (60, 390)
COLOUR_LIST, RED = (490, 341), (465, 769)
YELLOW = (141, 352)
VERTICAL_LIST = (150, 245)
OK = (569, 739)
# The Home tab's buttons (UI Automation's boxes, their centres).
BUTTON = {'Bold': (302, 201), 'Italic': (341, 201), 'Underline': (380, 201)}


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:900], flush=True)


def state(case, step):
    time.sleep(0.8)
    xs.cmd('move 3700 1900'); time.sleep(0.3)
    r = xs.ready()
    name = f'r6-{case}-{step}'
    xs.cmd(f'shot {name}')
    g = xs.geo('A1', 'C3')
    xs.cmd(f"crop {name} {g['A1'][0] - 40} {g['A1'][1] - 30} {g['C3'][2] - g['A1'][0] + 50} {g['C3'][3] - g['A1'][1] + 40} {name}-cells 2")
    row = {'reading': 'R6', 'case': case, 'step': step, 'ready': r}
    if r.startswith('ready='):
        row['look'] = xs.cmd('look A1')
        row['colwidth'] = xs.cmd('colwidth A1')
    row['shot'] = f'{name}-cells.png'
    record(row)


def fresh(case):
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start'); time.sleep(1.5)
    xs.cmd('setfmt A1 0.00'); xs.cmd('setup A1 123456789.12'); xs.cmd('select A1')
    record({'reading': 'R6', 'case': case, 'step': 'start', 'excel': a})
    state(case, '0-hashes')


def dialog():
    xs.keys('^1'); time.sleep(1.6)
    w = [x for x in xs.cmd('windows')[len('ok windows '):].rsplit(' t=', 1)[0].split(' ;; ') if '|Format Cells|' in x]
    if not w:
        raise RuntimeError('Format Cells did not open')
    l, t = (int(v) for v in w[0].split('|')[3].split(',')[:2])
    return l, t


def press(origin, rel, wait=0.6):
    xs.cmd(f'click {origin[0] + rel[0]} {origin[1] + rel[1]}'); time.sleep(wait)


def through_dialog(case, tab, *then):
    fresh(case)
    o = dialog()
    press(o, TAB[tab])
    for t in then:
        if isinstance(t, tuple):
            press(o, t)
        else:
            xs.keys(t, gap=120); time.sleep(0.5)
    xs.cmd(f'shot r6-{case}-1-before-ok')
    press(o, OK, wait=1.0)
    state(case, '2-after-ok')


CASES = {
    'font-italic': lambda: through_dialog('font-italic', 'Font', ITALIC),
    'font-underline': lambda: through_dialog('font-underline', 'Font', UNDERLINE_LIST, SINGLE),
    'font-colour': lambda: through_dialog('font-colour', 'Font', COLOUR_LIST, RED),
    'fill-tab-only': lambda: through_dialog('fill-tab-only', 'Fill'),
    'fill-yellow': lambda: through_dialog('fill-yellow', 'Fill', YELLOW),
    'alignment-tab-only': lambda: through_dialog('alignment-tab-only', 'Alignment'),
    'alignment-vertical-top': lambda: through_dialog('alignment-vertical-top', 'Alignment', VERTICAL_LIST, '{HOME}{ENTER}'),
}


def button(name):
    case = f'button-{name.lower()}'
    fresh(case)
    x, y = BUTTON[name]
    xs.cmd(f'click {x} {y}')
    state(case, f'1-{name.lower()}-pressed')


def ctrl_b_twice():
    fresh('ctrl-b-twice')
    xs.keys('^b'); state('ctrl-b-twice', '1-ctrl-b')
    xs.keys('^b'); state('ctrl-b-twice', '2-ctrl-b-again')


def com(prop, value):
    case = f'com-{prop.lower()}'
    fresh(case)
    xs.cmd(f'font A1 {prop} {value}')
    state(case, f'1-font-{prop.lower()}-{value}')


ALL = list(CASES) + ['button-Bold', 'button-Italic', 'button-Underline', 'ctrl-b-twice', 'com-Bold', 'com-Underline']

if __name__ == '__main__':
    for c in (sys.argv[1:] or ALL):
        if c in CASES:
            CASES[c]()
        elif c.startswith('button-'):
            button(c[len('button-'):])
        elif c == 'ctrl-b-twice':
            ctrl_b_twice()
        elif c == 'com-Bold':
            com('Bold', 'True')
        elif c == 'com-Underline':
            com('Underline', '2')
