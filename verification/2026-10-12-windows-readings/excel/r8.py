"""R8 (h10): what Format Cells' Border tab has selected in its Style list and its Colour on opening.

Each case in a new workbook of an Excel of xl-server.ps1's own; the lines set through COM on each cell's own edges
(LineStyle and Weight: xlContinuous 1, xlDash -4115, xlDouble -4119; xlThin 2, xlMedium -4138, xlThick 4); the cells
selected; Ctrl+1 (the dialog opens on the Number tab in a new Excel), Ctrl+Tab three times to the Border tab; a picture
of the dialog (its own window), then Escape.

    python3 r8.py
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'r8.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'r8-xs.jsonl'))

CASES = {
    # (1) a cell with a thin top and a thick right of its own.
    '1-thin-top-thick-right': ('B2', [('B2', 8, 1, 2), ('B2', 10, 1, 4)]),
    # (2) A2:B3, each cell with a different line of its own.
    '2-range-different-lines': ('A2:B3', [('A2', 8, 1, 2), ('B2', 10, 1, -4138), ('A3', 9, 1, 4), ('B3', 7, -4115, 2)]),
    # (3) a cell with no line.
    '3-no-lines': ('D5', []),
}


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:900], flush=True)


def run(case, sel, edges):
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start'); time.sleep(1.5)
    for cell, edge, style, weight in edges:
        xs.cmd(f'setedge {cell} {edge} {style} {weight}')
    xs.cmd(f'select {sel}'); time.sleep(0.4)
    cells = sorted({e[0] for e in edges} | {sel.split(':')[0]})
    look = xs.cmd('look ' + ' '.join(cells))
    name = f'r8-{case}'
    xs.cmd(f'shot {name}-cells')
    g = xs.geo('A1', 'E6')
    xs.cmd(f"crop {name}-cells {g['A1'][0] - 40} {g['A1'][1] - 30} {g['E6'][2] - g['A1'][0] + 50} {g['E6'][3] - g['A1'][1] + 40} {name}-cells-c 2")
    xs.keys('^1'); time.sleep(1.6)
    for _ in range(3):
        xs.keys('^{TAB}'); time.sleep(0.5)
    time.sleep(0.6)
    w = [x for x in xs.cmd('windows')[len('ok windows '):].rsplit(' t=', 1)[0].split(' ;; ') if '|Format Cells|' in x]
    xs.cmd(f'shot {name}-dialog')
    if w:
        l, t, r, b = (int(v) for v in w[0].split('|')[3].split(','))
        xs.cmd(f'crop {name}-dialog {l} {t} {r - l} {b - t} {name}-dialog-c 1')
    names = xs.cmd(r'uianames ^(Style:|Colour:|None|Automatic|Border|Presets|Outline|Inside)$')
    record({'reading': 'R8', 'case': case, 'selection': sel, 'edges': edges, 'excel': a, 'look': look, 'dialogWindow': w, 'names': names,
            'shots': [f'{name}-cells-c.png', f'{name}-dialog-c.png']})
    xs.keys('{ESC}'); time.sleep(0.6)
    record({'reading': 'R8', 'case': case, 'step': 'after-escape', 'ready': xs.ready(), 'look': xs.cmd('look ' + ' '.join(cells))})


if __name__ == '__main__':
    for c in (sys.argv[1:] or list(CASES)):
        run(c, *CASES[c])
