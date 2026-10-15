"""R4 (h2, ticket 24): the active cell's border in a selection of several areas, in Device Pixels, at Excel's 100% and
150% zoom on the 150% display.

A new workbook in an Excel of xl-server.ps1's own; the selections made with the real pointer (SendInput): (1) A1:B2
dragged, then D4:E6 dragged with Ctrl held; (2) Enter, one at a time, until the active cell lies inside A1:B2; (3) A1:B2
dragged, then D4 pressed alone with Ctrl held. After each, Excel's windows drawn by themselves (PrintWindow), and the
colours along four short lines across the active cell's edges (px.ps1): its left and right edges at the cell's middle
height, its top and bottom edges at the middle width, each from 6 px outside to 10 px inside.

    python3 r4.py
"""
import json
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'r4.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'r4-xs.jsonl'))
WIN = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-2026-10-12'


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:1500], flush=True)


def px(shot, x0, y0, x1, y1):
    out = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', f'{WIN}\\px.ps1',
                          f'{WIN}\\xs\\shots\\{shot}.png', str(x0), str(y0), str(x1), str(y1)],
                         capture_output=True, text=True, cwd='/mnt/c').stdout
    return [l.strip() for l in out.replace('\r', '').splitlines() if l.strip()]


def centre(a):
    return xs.centre(xs.geo(a)[a])


def active():
    r = xs.ready()
    return r.split('active=Sheet1!')[1].split(' ')[0], r.split('selection=')[1].split(' ')[0]


def measure(zoom, case, step):
    time.sleep(0.6)
    xs.cmd('move 3700 1900'); time.sleep(0.5)
    act, sel = active()
    g = xs.geo(act, 'A1', 'F7')[act]
    name = f'r4-z{zoom}-{case}-{step}'
    xs.cmd(f'shot {name}')
    a1, f7 = xs.geo('A1')['A1'], xs.geo('F7')['F7']
    xs.cmd(f'crop {name} {a1[0] - 40} {a1[1] - 30} {f7[2] - a1[0] + 50} {f7[3] - a1[1] + 40} {name}-cells 2')
    xs.cmd(f'crop {name} {g[0] - 12} {g[1] - 12} {g[2] - g[0] + 24} {g[3] - g[1] + 24} {name}-active 8')
    my, mx = (g[1] + g[3]) // 2, (g[0] + g[2]) // 2
    lines = {
        'left': px(name, g[0] - 6, my, g[0] + 10, my),
        'right': px(name, g[2] - 10, my, g[2] + 6, my),
        'top': px(name, mx, g[1] - 6, mx, g[1] + 10),
        'bottom': px(name, mx, g[3] - 10, mx, g[3] + 6),
    }
    record({'reading': 'R4', 'zoom': zoom, 'case': case, 'step': step, 'active': act, 'selection': sel, 'box': g, 'lines': lines, 'shot': name})
    return act


def run(zoom):
    xs.cmd('clearsheet'); xs.cmd(f'zoom {zoom}'); xs.cmd('select H20'); time.sleep(0.5)
    # (1)
    (x0, y0), (x1, y1) = centre('A1'), centre('B2'); xs.cmd(f'drag {x0} {y0} {x1} {y1}')
    (x0, y0), (x1, y1) = centre('D4'), centre('E6'); xs.cmd(f'ctrldrag {x0} {y0} {x1} {y1}')
    measure(zoom, 1, 'a1-b2-then-ctrl-d4-e6')
    # (2)
    for n in range(1, 10):
        xs.keys('{ENTER}'); time.sleep(0.4)
        act, sel = active()
        record({'reading': 'R4', 'zoom': zoom, 'case': 2, 'step': f'enter-{n}', 'active': act, 'selection': sel})
        if act in ('A1', 'A2', 'B1', 'B2'):
            measure(zoom, 2, f'enter-{n}-inside-a1-b2')
            break
    # (3)
    xs.cmd('select H20'); time.sleep(0.3)
    (x0, y0), (x1, y1) = centre('A1'), centre('B2'); xs.cmd(f'drag {x0} {y0} {x1} {y1}')
    x, y = centre('D4'); xs.cmd(f'ctrlclick {x} {y}')
    measure(zoom, 3, 'a1-b2-then-ctrl-click-d4')


if __name__ == '__main__':
    for z in (100, 150):
        run(z)
    xs.cmd('zoom 100')
