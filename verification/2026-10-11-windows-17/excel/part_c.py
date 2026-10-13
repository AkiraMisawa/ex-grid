"""verify-on-windows-17.md, Part C in Excel: the seventh run's 22 F4 cases (verify-on-windows-7.md, Part A), and
its Part C (the Heading reading).

Every F4 is a real key press sent to Excel's window (SendInput). The text is read before the edit is
committed, through Excel's own clipboard: after the case's keys, F2 (only when the edit is in Enter or
Point mode, to make the caret's keys move the caret rather than commit; never when the case already
pressed F2), then Ctrl+C (what is selected), Shift+Home and Ctrl+C (the text from the start to the
caret, or to the selection's anchor), End, Shift+Home and Ctrl+C (the whole text); then Escape. A
picture is taken before any of that. When Excel answers COM after the case's keys, no edit is open:
the entry was committed (or never opened), and the cells are read instead.

Each case runs as written, typed into the cell (`typed`), and, where its keys move the caret, also with
F2 pressed before them (`f2`, an addition): typed into a cell, Excel is in Enter mode, where an arrow or
Home commits the entry instead of moving the caret.

    python3 part_c.py excel            the 22 cases
    python3 part_c.py heading          the Heading reading
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'part-c-excel.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'part-c-excel-xs.jsonl'))
L = xs.lit
NAV = '__nav__'

# id: (cell, typed text, caret keys, presses, keys after the presses, setup)
CASES = {
    1: ('D10', '=B2', '', [1, 2, 3, 4], '', None),
    2: ('D10', '=A1+B2', '{LEFT}{LEFT}{LEFT}', [1], '', None),
    3: ('D10', '=A1+B2', '{HOME}{RIGHT}', [1], '', None),
    4: ('D10', '=A1+B2', '{HOME}{RIGHT}{RIGHT}', [1], '', None),
    5: ('D10', '=SUM(A1:B2)', '{LEFT}', [1, 2, 3, 4], '', None),
    6: ('D10', '=$A1:B2', '{LEFT}', [1], '', None),
    7: ('D10', '=A1+B2', '+{HOME}+{RIGHT}', [1], '', None),
    8: ('D10', '=SUM(A:A)', '{LEFT}', [1, 2, 3], '', None),
    9: ('D10', '=SUM(1:1)', '{LEFT}', [1, 2, 3], '', None),
    10: ('D10', '=Sheet2!A1', '', [1], '', 'sheet2'),
    11: ('D1', '=SUM(Positions[PV])', '{LEFT}', [1], '', 'table'),
    12: ('D10', '=SUM(A1)', '{HOME}{RIGHT}{RIGHT}', [1], '', None),
    13: ('D10', '=1+2', '', [1], '', None),
    14: ('D10', 'B2', '', [1], '', None),
    15: ('A1', '=', '{DOWN}', [1], '', None),
    '15b': ('A1', '=', '{DOWN}', [1], '{DOWN}', None),
    16: ('A1', '=', '{DOWN}', [1], L('+'), None),
    18: ('D10', '=A1+B1', '{LEFT}{LEFT}+{LEFT}', [1], '', None),
    19: ('D10', '=A1+B2', '{HOME}{RIGHT}{RIGHT}+{RIGHT}+{RIGHT}+{RIGHT}', [1], '', None),
    20: ('D10', '=A1+$B$2', '{HOME}{RIGHT}+{END}', [1], '', None),
    21: ('D10', '=b2', '', [1], '', None),
    22: ('D10', '=B2:A1', '', [1], '', None),
}
POINTING = {15, '15b', 16}   # "=", then the arrow points: Point mode, not a caret's move


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:700])


def shot(name):
    name = ''.join(ch if ch.isalnum() or ch in '-_' else '-' for ch in name)
    xs.cmd(f'shot {name}')
    # The Formula Bar and the Name Box, and the status bar's mode, cropped at once.
    xs.cmd(f'crop {name} 0 270 1500 60 {name}-bar 1')
    xs.cmd(f'crop {name} 0 2055 500 33 {name}-mode 1')
    return name


def copy(keys):
    xs.cmd('clipclear')
    xs.keys(keys, gap=60)
    time.sleep(0.35)
    return xs.clipboard()


def trial(cid, variant, n):
    cell, text, nav, _, after, setup = CASES[cid]
    xs.cmd('clearsheet')
    if setup == 'sheet2' and 'Sheet2' not in xs.cmd('uia'):
        xs.cmd('addsheet Sheet2')
    if setup == 'table':
        xs.cmd('setup A1 Id'); xs.cmd('setup B1 PV'); xs.cmd('setup A2 R-1'); xs.cmd('setup A3 R-2'); xs.cmd('setup B2 10'); xs.cmd('setup B3 20')
        xs.cmd('table A1:B3 Positions')
    xs.cmd(f'select {cell}')
    time.sleep(0.2)
    keys = L(text) + ('{F2}' if variant == 'f2' else '') + nav + '{F4}' * n + after
    xs.keys(keys, gap=80)
    time.sleep(0.6)
    name = f'c{cid}-{variant}-f4x{n}'
    row = {'case': cid, 'variant': variant, 'presses': n, 'cell': cell, 'keys': keys, 'shot': shot(name)}
    r = xs.ready()
    row['ready'] = r
    if r.startswith('ready='):
        # No edit open: what the keys left in the sheet.
        row['committed'] = True
        row['cells'] = xs.cells(cell, 'A1', 'A2', 'A3', 'D10', 'D11', 'C10', 'E10')
    else:
        row['committed'] = False
        to_edit = '' if variant == 'f2' else '{F2}'
        row['f2ToRead'] = bool(to_edit)
        if to_edit:
            xs.keys(to_edit); time.sleep(0.3)
        row['selected'] = copy('^c')
        row['toCaret'] = copy('+{HOME}^c')
        row['whole'] = copy('{END}+{HOME}^c')
        xs.keys('{ESC}'); time.sleep(0.3)
        if not xs.ready().startswith('ready='):
            xs.keys('{ESC}'); time.sleep(0.3)
        row['afterEscape'] = xs.ready()
    record(row)


def excel():
    record({'case': 'start', 'excel': xs.cmd('start')})
    time.sleep(1)
    # Case 17 first, in the fresh Excel: F4 with no edit open on a cell holding =B2.
    xs.cmd('setup D10 =B2'); xs.cmd('select D10'); time.sleep(0.3)
    before = xs.cells('D10', 'D11', 'E10', 'D9', 'C10')
    xs.keys('{F4}'); time.sleep(0.8)
    record({'case': 17, 'variant': 'no-edit', 'note': 'D10 =B2 set through COM (setup), D10 selected, F4 with no edit open, in a fresh Excel where nothing was done by hand yet', 'before': before, 'after': xs.cells('D10', 'D11', 'E10', 'D9', 'C10'), 'ready': xs.ready(), 'shot': shot('c17-no-edit')})
    # An addition: F4 after a typed entry elsewhere, so that Excel has an action to repeat.
    xs.cmd('select F10'); xs.keys('x{ENTER}'); time.sleep(0.3); xs.cmd('select D10'); time.sleep(0.2)
    xs.keys('{F4}'); time.sleep(0.8)
    record({'case': 17, 'variant': 'after-a-typed-entry', 'note': 'an addition: F10 typed x, Enter; D10 selected; F4', 'after': xs.cells('D10', 'D11', 'E10', 'F10', 'F11'), 'ready': xs.ready(), 'shot': shot('c17-after-a-typed-entry')})
    for cid, (cell, text, nav, presses, after, setup) in CASES.items():
        variants = ['typed'] + (['f2'] if nav and cid not in POINTING else [])
        for v in variants:
            for n in presses:
                trial(cid, v, n)


def heading():
    """The seventh run's Part C: on an empty sheet with the view at the top left, a click on the Column
    Heading C, then Ctrl+Shift+Right."""
    xs.cmd('clearsheet')
    xs.cmd('select A1'); xs.keys('^{HOME}'); time.sleep(0.3)
    g = xs.geo('A1', 'C1')
    x = (g['C1'][0] + g['C1'][2]) // 2; y = g['C1'][1] - 14
    xs.cmd(f'click {x} {y}'); time.sleep(0.5)
    a = xs.ready(); s1 = shot('heading-1-c-clicked')
    xs.keys('^+{RIGHT}'); time.sleep(0.8)
    b = xs.ready(); s2 = shot('heading-2-ctrl-shift-right')
    g2 = xs.geo('A1', 'C1')
    record({'case': 'heading', 'clickAt': [x, y], 'afterClick': a, 'afterKeys': b, 'cellsBefore': g, 'cellsAfter': g2, 'shots': [s1, s2]})


def repeat_check():
    """An addition: does F4 with no edit open repeat the setup's COM Clear (Cells.Clear) between cases?"""
    xs.cmd('clearsheet'); xs.cmd('select D10'); xs.keys('abc{ENTER}'); time.sleep(0.4)
    xs.cmd('select D10'); before = xs.cells('D10')
    xs.keys('{F4}'); time.sleep(0.6)
    after = xs.cells('D10')
    record({'case': 'repeat-check', 'note': 'clearsheet (COM Cells.Clear), D10 typed abc Enter, D10 selected (COM), F4 with no edit open', 'before': before, 'after': after, 'shot': shot('repeat-check')})


def case17_keys():
    """Case 17 again, by keys alone in a fresh Excel (no COM write before it): D10 typed =B2, Enter; Up; F4.
    Then, as an addition, a format by keys (Ctrl+B on F10), D10 again, F4."""
    xs.cmd('stop'); record({'case': 17, 'variant': 'keys-only start', 'excel': xs.cmd('start')}); time.sleep(1)
    xs.keys('{DOWN 9}{RIGHT 3}', gap=40); xs.keys(L('=B2') + '{ENTER}{UP}'); time.sleep(0.4)
    before = xs.cells('D10', 'D11', 'E10'); r0 = xs.ready()
    xs.keys('{F4}'); time.sleep(0.8)
    record({'case': 17, 'variant': 'keys-only', 'note': 'a fresh Excel; D10 reached by keys, =B2 typed, Enter, Up; F4 with no edit open', 'readyBefore': r0, 'before': before, 'after': xs.cells('D10', 'D11', 'E10'), 'ready': xs.ready(), 'shot': shot('c17-keys-only')})
    xs.keys('{RIGHT 2}^b{LEFT 2}', gap=60); time.sleep(0.4)
    xs.keys('{F4}'); time.sleep(0.8)
    record({'case': 17, 'variant': 'after-ctrl-b', 'note': 'an addition: F10 made bold with Ctrl+B, back to D10, F4', 'ready': xs.ready(), 'cells': xs.cells('D10', 'F10'), 'shot': shot('c17-after-ctrl-b')})


if __name__ == '__main__':
    for name in sys.argv[1:]:
        globals()[name]()
