"""format-cells-readings.md questions 1 to 8 (run 18a): Format Cells' Border tab on opening, as R8 read it (r8.py).

Each case in a new workbook of a new Excel of xl-server.ps1's own, at 100%; the lines set through COM on each cell's own
edges (LineStyle and Weight: xlContinuous 1, xlDash -4115; xlThin 2, xlMedium -4138, xlThick 4; a colour as RRGGBB, or
Automatic when none is named); the cells selected (through COM, or with the real pointer where the case says); Ctrl+1
(the dialog opens on the Number tab in a new Excel), Ctrl+Tab three times to the Border tab; a picture of the dialog,
the Style list enlarged 4 times, the Colour box and the Border buttons enlarged twice; then Escape, and every line read
back through COM.

    python3 fc.py [case ...]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'fc.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'fc-xs.jsonl'))

TOP, BOTTOM, LEFT, RIGHT = 8, 9, 7, 10
THIN, MEDIUM, THICK = 2, -4138, 4
CASES = {
    '1a-b2-left-thick': ('B2', [('B2', LEFT, 1, THICK, None)]),
    '1b-c2-left-thick-select-b2': ('B2', [('C2', LEFT, 1, THICK, None)]),
    '2-b2-thick-top-medium-right': ('B2', [('B2', TOP, 1, THICK, None), ('B2', RIGHT, 1, MEDIUM, None)]),
    '3-b2-thick-top-thick-bottom': ('B2', [('B2', TOP, 1, THICK, None), ('B2', BOTTOM, 1, THICK, None)]),
    '4-a1-b1-bottom-thick-select-a2-b2': ('A2:B2', [('A1', BOTTOM, 1, THICK, None), ('B1', BOTTOM, 1, THICK, None)]),
    '5-a1-c1-bottom-thick-a2-ctrl-c2': ('click A2, ctrlclick C2', [('A1', BOTTOM, 1, THICK, None), ('C1', BOTTOM, 1, THICK, None)]),
    '6a-a1-bottom-thick-red-select-a2': ('A2', [('A1', BOTTOM, 1, THICK, 'FF0000')]),
    '6b-a1-bottom-medium-dashed-select-a2': ('A2', [('A1', BOTTOM, -4115, MEDIUM, None)]),
    '8-a1-bottom-thick-a2-top-thin-select-a1': ('A1', [('A1', BOTTOM, 1, THICK, None), ('A2', TOP, 1, THIN, None)]),
}
READBACK = ('(@({cells}) | % {{ $c = $_; $r = $ws.Range($c); $c + ": " + ((@(8,9,7,10) | % {{ $b = $r.Borders.Item($_); '
            '@{{8="top";9="bottom";7="left";10="right"}}[$_] + "=" + $b.LineStyle + "/" + $b.Weight + "/" + ("{{0:X6}}" -f [int]$b.Color) }}) -join " ") }}) -join " ;; "')


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:900], flush=True)


def readback(cells):
    return xs.cmd('ps ' + READBACK.format(cells=','.join(f'"{c}"' for c in cells)))[len('ok ps '):].rsplit(' t=', 1)[0]


def setedge(cell, edge, style, weight, colour):
    s = f'$b = $ws.Range("{cell}").Borders.Item({edge}); $b.LineStyle = {style}; $b.Weight = {weight}'
    if colour:
        rgb = int(colour, 16); bgr = ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF)
        s += f'; $b.Color = {bgr}'
    xs.cmd('ps ' + s)


def press_cell(kind, a):
    g = xs.geo(a)[a]
    xs.cmd(f'{kind} {(g[0] + g[2]) // 2} {(g[1] + g[3]) // 2}'); time.sleep(0.4)


def fresh():
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start'); time.sleep(1.5)
    return a


def open_border_tab(name):
    xs.keys('^1'); time.sleep(1.6)
    for _ in range(3):
        xs.keys('^{TAB}'); time.sleep(0.5)
    time.sleep(0.8)
    w = [x for x in xs.cmd('windows')[len('ok windows '):].rsplit(' t=', 1)[0].split(' ;; ') if '|Format Cells|' in x]
    xs.cmd(f'shot {name}-dialog')
    if w:
        l, t, r, b = (int(v) for v in w[0].split('|')[3].split(','))
        xs.cmd(f'crop {name}-dialog {l} {t} {r - l} {b - t} {name}-dialog-c 1')
        xs.cmd(f'crop {name}-dialog {l + 44} {t + 160} 186 192 {name}-style 4')
        xs.cmd(f'crop {name}-dialog {l + 44} {t + 385} 186 44 {name}-colour 2')
        xs.cmd(f'crop {name}-dialog {l + 262} {t + 262} 340 212 {name}-border 2')
    return w


def run(case, sel, edges):
    a = fresh()
    for e in edges:
        setedge(*e)
    if sel.startswith('click'):
        for part in sel.split(', '):
            kind, cell = part.split(' ')
            press_cell(kind, cell)
    else:
        xs.cmd(f'select {sel}'); time.sleep(0.4)
    cells = sorted({e[0] for e in edges} | {'A1', 'A2', 'B1', 'B2', 'C1', 'C2'})
    before = readback(cells)
    name = f'fc-{case}'
    xs.cmd(f'shot {name}-cells')
    g = xs.geo('A1', 'E6')
    xs.cmd(f"crop {name}-cells {g['A1'][0] - 40} {g['A1'][1] - 30} {g['E6'][2] - g['A1'][0] + 50} {g['E6'][3] - g['A1'][1] + 40} {name}-cells-c 2")
    ready = xs.ready()
    w = open_border_tab(name)
    record({'reading': 'FC', 'case': case, 'selection': sel, 'ready': ready, 'edges': edges, 'excel': a, 'lines': before,
            'dialogWindow': w, 'shots': [f'{name}-cells-c.png', f'{name}-dialog-c.png', f'{name}-style.png', f'{name}-colour.png', f'{name}-border.png']})
    xs.keys('{ESC}'); time.sleep(0.6)
    after = readback(cells)
    record({'reading': 'FC', 'case': case, 'step': 'after-escape', 'ready': xs.ready(), 'lines': after, 'unchanged': after == before})


if __name__ == '__main__':
    for c in (sys.argv[1:] or list(CASES)):
        run(c, *CASES[c])
