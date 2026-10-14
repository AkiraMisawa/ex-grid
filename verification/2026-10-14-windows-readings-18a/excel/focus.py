"""focus-among-ranges.md questions 2 to 4 (run 18a): the active cell's border in a selection of several areas, read as
the 2026-10-12 readings' R4 read it (r4.py), with one change to the sheet each:

  fill      D4 filled yellow (#FFFF00) through COM before the first selection (question 2);
  edit      the first selection, then F2 on D4, the active cell (question 3);
  nogrid    the three selections with View > Gridlines off (ActiveWindow.DisplayGridlines = False, question 4).

Each at Excel's 100% and 150% zoom on this 150% display. After each selection, Excel's windows drawn by themselves
(PrintWindow) and the colours along four short lines across the active cell's edges (px.ps1), each from 6 px outside to
10 px inside, at the edge's middle.

    python3 focus.py [fill edit nogrid]
"""
import json
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'focus.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'focus-xs.jsonl'))
WIN = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-18a'


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:1500], flush=True)


def px(shot, x0, y0, x1, y1):
    out = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', f'{WIN}\\px.ps1',
                          f'{WIN}\\xs\\shots\\{shot}.png', str(x0), str(y0), str(x1), str(y1)],
                         capture_output=True, text=True, cwd='/mnt/c', stdin=subprocess.DEVNULL).stdout
    return [l.strip() for l in out.replace('\r', '').splitlines() if l.strip()]


def centre(a):
    return xs.centre(xs.geo(a)[a])


def active():
    r = xs.ready()
    if not r.startswith('ready='):
        return None, None
    return r.split('active=Sheet1!')[1].split(' ')[0], r.split('selection=')[1].split(' ')[0]


def measure(variant, zoom, case, step, act=None, sel=None, g=None, frame=None):
    time.sleep(0.6)
    xs.cmd('move 3700 1900'); time.sleep(0.5)
    if act is None:
        act, sel = active()
    if g is None:
        g = xs.geo(act)[act]
    name = f'focus-{variant}-z{zoom}-{case}-{step}'
    xs.cmd(f'shot {name}')
    # While an edit is open Excel refuses COM: the boxes are read before it opens.
    a1, f7 = frame or (xs.geo('A1')['A1'], xs.geo('F7')['F7'])
    xs.cmd(f'crop {name} {a1[0] - 40} {a1[1] - 30} {f7[2] - a1[0] + 50} {f7[3] - a1[1] + 40} {name}-cells 2')
    xs.cmd(f'crop {name} {g[0] - 12} {g[1] - 12} {g[2] - g[0] + 24} {g[3] - g[1] + 24} {name}-active 8')
    my, mx = (g[1] + g[3]) // 2, (g[0] + g[2]) // 2
    # The edit's text sits at the left of the cell: the left line is read a quarter of the way down instead of at the middle.
    ly = g[1] + (g[3] - g[1]) // 4 if variant == 'edit' else my
    lines = {
        'left': px(name, g[0] - 6, ly, g[0] + 10, ly),
        'right': px(name, g[2] - 10, my, g[2] + 6, my),
        'top': px(name, mx, g[1] - 6, mx, g[1] + 10),
        'bottom': px(name, mx, g[3] - 10, mx, g[3] + 6),
    }
    record({'reading': 'focus', 'variant': variant, 'zoom': zoom, 'case': case, 'step': step, 'active': act,
            'selection': sel, 'box': g, 'lines': lines, 'shot': name})
    return act


def first_selection():
    xs.cmd('select H20'); time.sleep(0.3)
    (x0, y0), (x1, y1) = centre('A1'), centre('B2'); xs.cmd(f'drag {x0} {y0} {x1} {y1}')
    (x0, y0), (x1, y1) = centre('D4'), centre('E6'); xs.cmd(f'ctrldrag {x0} {y0} {x1} {y1}')


def three(variant, zoom):
    # (1)
    first_selection()
    measure(variant, zoom, 1, 'a1-b2-then-ctrl-d4-e6')
    # (2)
    for n in range(1, 10):
        xs.keys('{ENTER}'); time.sleep(0.4)
        act, sel = active()
        if act in ('A1', 'A2', 'B1', 'B2'):
            measure(variant, zoom, 2, f'enter-{n}-inside-a1-b2')
            break
    # (3)
    xs.cmd('select H20'); time.sleep(0.3)
    (x0, y0), (x1, y1) = centre('A1'), centre('B2'); xs.cmd(f'drag {x0} {y0} {x1} {y1}')
    x, y = centre('D4'); xs.cmd(f'ctrlclick {x} {y}')
    measure(variant, zoom, 3, 'a1-b2-then-ctrl-click-d4')


def run(variant, zoom):
    xs.cmd('clearsheet'); xs.cmd(f'zoom {zoom}'); xs.cmd('ps $xl.ActiveWindow.DisplayGridlines = $true; $xl.ActiveWindow.DisplayGridlines')
    time.sleep(0.4)
    if variant == 'fill':
        xs.cmd('fill D4 FFFF00')
        first_selection()
        measure(variant, zoom, 1, 'a1-b2-then-ctrl-d4-e6')
    elif variant == 'edit':
        first_selection()
        measure(variant, zoom, 1, 'before-f2')
        g, frame = xs.geo('D4')['D4'], (xs.geo('A1')['A1'], xs.geo('F7')['F7'])
        xs.keys('{F2}'); time.sleep(0.6)
        measure(variant, zoom, 1, 'after-f2', act='D4', sel='(editing)', g=g, frame=frame)
        xs.keys('{ESC}'); time.sleep(0.4)
    elif variant == 'nogrid':
        record({'reading': 'focus', 'variant': variant, 'zoom': zoom, 'gridlines': xs.cmd('ps $xl.ActiveWindow.DisplayGridlines = $false; $xl.ActiveWindow.DisplayGridlines')})
        three(variant, zoom)
        xs.cmd('ps $xl.ActiveWindow.DisplayGridlines = $true; $xl.ActiveWindow.DisplayGridlines')


if __name__ == '__main__':
    for v in (sys.argv[1:] or ['fill', 'edit', 'nogrid']):
        for z in (100, 150):
            run(v, z)
    xs.cmd('zoom 100')
