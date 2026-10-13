"""verify-on-windows-17.md, Part H: what else waits on Excel, Excel's half.

Each case: an Excel of xl-server.ps1's own, a new workbook of one sheet at 100% zoom; keys and the mouse through
SendInput; what Excel holds read through COM only while it is Ready; a picture of each step, cut to the cells it
is about (never the title bar, which shows the account). One JSON line per step in records/part-h-excel.jsonl.

    python3 part_h_excel.py h1 h2 ...
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'part-h-excel.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'part-h-excel-xs.jsonl'))


def record(case, step, **kw):
    row = {'case': case, 'step': step, 't': time.strftime('%H:%M:%S'), **kw}
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:700], flush=True)


def name_of(case, step):
    return ''.join(ch if ch.isalnum() or ch in '-_' else '-' for ch in f'h-{case}-{step}')


def picture(case, step, top_left='A1', bottom_right='G9', pad=60, scale=1):
    """Excel's windows drawn by themselves, cut to the cells from top_left to bottom_right with the
    Headings (pad pixels to the left and above), saved as <name>-cells.png."""
    name = name_of(case, step)
    xs.cmd(f'shot {name}')
    g = xs.geo(top_left, bottom_right)
    x0, y0 = max(0, g[top_left][0] - pad), max(0, g[top_left][1] - pad)
    x1, y1 = g[bottom_right][2] + 8, g[bottom_right][3] + 8
    xs.cmd(f'crop {name} {x0} {y0} {x1 - x0} {y1 - y0} {name}-cells {scale}')
    return f'{name}-cells.png'


def state(case, step, *cells, pic=('A1', 'G9'), wait=0.6, look=None, scale=1):
    time.sleep(wait)
    r = xs.ready()
    c = xs.cells(*cells) if cells and r.startswith('ready=') else None
    lk = xs.cmd('look ' + ' '.join(look)) if look and r.startswith('ready=') else None
    shot = picture(case, step, *pic, scale=scale) if pic else None
    record(case, step, ready=r, cells=c, look=lk, shot=shot)
    return r


def fresh():
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start')
    time.sleep(1.5)
    return a


def centre(a):
    return xs.centre(xs.geo(a)[a])


def drag(a, b, ctrl=False):
    (x0, y0), (x1, y1) = centre(a), centre(b)
    xs.cmd(f'{"ctrldrag" if ctrl else "drag"} {x0} {y0} {x1} {y1}')


# ---- h1 to h4: what a selection looks like, and the active cell after a take-out -----------------------

def h1():
    record('h1', 'start', excel=fresh())
    for a, v in (('A1', '1'), ('B1', '2'), ('C1', '3'), ('A2', '4'), ('C5', '5')):
        xs.cmd(f'setup {a} {v}')
    drag('A1', 'C5'); state('h1', '1-a1-c5-dragged')


def h2():
    record('h2', 'start', excel=fresh())
    drag('A1', 'B2'); state('h2', '1-a1-b2')
    drag('D4', 'E6', ctrl=True); state('h2', '2-ctrl-d4-e6')


def h3():
    record('h3', 'start', excel=fresh())
    c1 = xs.geo('C1')['C1']; a3 = xs.geo('A3')['A3']
    # The column's Heading lies just above row 1, the row's just left of column A.
    xs.cmd(f'click {(c1[0] + c1[2]) // 2} {c1[1] - 12}'); state('h3', '1-column-c', pic=('A1', 'G9'))
    xs.cmd(f'click {a3[0] - 14} {(a3[1] + a3[3]) // 2}'); state('h3', '2-row-3', pic=('A1', 'G9'))


def h4():
    record('h4', 'start', excel=fresh())
    drag('A1', 'B2'); state('h4', '1-a1-b2')
    x, y = centre('F6')
    xs.cmd(f'ctrlclick {x} {y}'); state('h4', '2-ctrl-click-f6')
    xs.cmd(f'ctrlclick {x} {y}'); state('h4', '3-ctrl-click-f6-again')
    for i in range(1, 4):
        xs.keys('{ENTER}'); state('h4', f'{3 + i}-enter-{i}')


# ---- h5: the lists an argument offers -------------------------------------------------------------------

H5 = ['=VLOOKUP(1,A1:B2,2,', '=HLOOKUP(1,A1:B2,2,', '=MATCH(1,A1:A2,', '=YEARFRAC(A1,A2,', '=XMATCH(1,A1:A2,', '=XMATCH(1,A1:A2,0,']


def h5():
    record('h5', 'start', excel=fresh())
    # While a cell is edited COM refuses every call: the picture's box is read before the typing.
    g = xs.geo('A1', 'H12')
    x0, y0 = max(0, g['A1'][0] - 60), max(0, g['A1'][1] - 60)
    x1, y1 = g['H12'][2] + 8, g['H12'][3] + 8
    for i, text in enumerate(H5, 1):
        xs.keys(xs.lit(text), gap=30)
        time.sleep(1.2)
        # The list's entries read "TRUE - Approximate match", "0 - Exact match" and the like.
        names = xs.cmd(r'uianames ( - |^TRUE|^FALSE)')
        name = name_of('h5', f'{i}-list')
        xs.cmd(f'shot {name}'); xs.cmd(f'crop {name} {x0} {y0} {x1 - x0} {y1 - y0} {name}-cells 1')
        record('h5', f'{i}-{text}', names=names, shot=f'{name}-cells.png')
        xs.keys('{ESC}'); time.sleep(0.4); xs.keys('{ESC}'); time.sleep(0.4)


# ---- h7, h8: what widens a column over #### -------------------------------------------------------------

def hashes(case):
    """A1 holds 123456.78 in 0.00, column A 5 characters wide (a width of its own), so it shows ####."""
    record(case, 'start', excel=fresh())
    xs.cmd('setwidth A1 5')
    xs.cmd('setfmt A1 0.00')
    xs.cmd('setup A1 123456.78')
    xs.cmd('select A1')
    state(case, '0-hashes', 'A1', look=['A1'])


def h7():
    for key, name in (('^i', 'ctrl-i'), ('^u', 'ctrl-u')):
        hashes(f'h7-{name}')
        xs.keys(key); state(f'h7-{name}', f'1-{name}', 'A1', look=['A1'])
        xs.cmd('colwidth A1')
        record(f'h7-{name}', '2-width', colwidth=xs.cmd('colwidth A1'))
    # Format Cells' Font tab: Ctrl+1 opens on the Number tab in a fresh Excel; Ctrl+Tab twice is Font.
    # Font style (Alt+O) Bold, then OK.
    hashes('h7-font-tab')
    xs.keys('^1'); time.sleep(1.5); xs.keys('^{TAB}'); time.sleep(0.4); xs.keys('^{TAB}'); time.sleep(0.6)
    record('h7-font-tab', '1-dialog', names=xs.cmd(r'uianames ^(Font|Font style:|Size:|Bold|Underline:|Effects|Strikethrough|Format Cells)$'))
    xs.keys('%o'); time.sleep(0.3); xs.keys('Bold', gap=40); time.sleep(0.4)
    xs.cmd(f'shot {name_of("h7-font-tab", "2-bold-chosen")}')
    xs.keys('{ENTER}'); state('h7-font-tab', '3-ok', 'A1', look=['A1'])
    record('h7-font-tab', '4-width', colwidth=xs.cmd('colwidth A1'))


def hashes_standard(case):
    """A1 holds 123456789.12 in 0.00 at the standard width, a width nobody set, so it shows ########."""
    record(case, 'start', excel=fresh())
    xs.cmd('setfmt A1 0.00'); xs.cmd('setup A1 123456789.12'); xs.cmd('select A1')
    state(case, '0-hashes', 'A1', look=['A1'])


def h7s():
    # h7 again at the standard width (h8's second half showed Ctrl+5 widening there and not at a set width).
    for key, name in (('^i', 'ctrl-i'), ('^u', 'ctrl-u')):
        hashes_standard(f'h7-standard-{name}')
        xs.keys(key); state(f'h7-standard-{name}', f'1-{name}', 'A1', look=['A1'])
        record(f'h7-standard-{name}', '2-width', colwidth=xs.cmd('colwidth A1'))
    hashes_standard('h7-standard-font-tab')
    xs.keys('^1'); time.sleep(1.5); xs.keys('^{TAB}'); time.sleep(0.4); xs.keys('^{TAB}'); time.sleep(0.6)
    xs.keys('%o'); time.sleep(0.3); xs.keys('Bold', gap=40); time.sleep(0.4)
    xs.keys('{ENTER}'); state('h7-standard-font-tab', '3-ok', 'A1', look=['A1'])
    record('h7-standard-font-tab', '4-width', colwidth=xs.cmd('colwidth A1'))


def h8():
    hashes('h8')
    xs.keys('^5'); state('h8', '1-ctrl-5', 'A1', look=['A1'])
    record('h8', '2-width', colwidth=xs.cmd('colwidth A1'))
    # The same at the standard width, as the fourteenth run's case 7 read it: a width nobody set.
    record('h8-standard', 'start', excel=fresh())
    xs.cmd('setfmt A1 0.00'); xs.cmd('setup A1 123456789.12'); xs.cmd('select A1')
    state('h8-standard', '0-hashes', 'A1', look=['A1'])
    xs.keys('^5'); state('h8-standard', '1-ctrl-5', 'A1', look=['A1'])
    record('h8-standard', '2-width', colwidth=xs.cmd('colwidth A1'))


# ---- h9: Custom codes typed in Format Cells ----------------------------------------------------------------

def custom(case, code):
    """A1 a date with a time (12 October 2026 13:30, serial 46307.5625), A2 a time alone (0.5625); both selected,
    Format Cells, Number tab, Category Custom (the last), Type, the code typed, OK."""
    xs.cmd('setup A1 46307.5625'); xs.cmd('setup A2 0.5625'); xs.cmd('setwidth A1 30'); xs.cmd('select A1:A2')
    xs.keys('^1'); time.sleep(1.5)
    xs.keys('%c'); time.sleep(0.3); xs.keys('{END}'); time.sleep(0.5)
    xs.keys('%t'); time.sleep(0.3); xs.keys('^a'); xs.keys(xs.lit(code), gap=40); time.sleep(0.5)
    record(case, f'dialog {code}', names=xs.cmd(r'uianames ^(Type:|Sample|Custom|Format Cells)'))
    xs.cmd(f'shot {name_of(case, "dialog-" + code)}')
    xs.keys('{ENTER}'); state(case, f'ok {code}', 'A1', 'A2', pic=('A1', 'C4'))


def h9(culture):
    record(f'h9-{culture}', 'start', excel=fresh())
    for code in (['d-mmm-yy', 'm/d/yyyy', 'h:mm'] if culture == 'en-GB' else ['mmmmm']):
        custom(f'h9-{culture}', code)


# ---- h10: the Border tab ----------------------------------------------------------------------------------

def border_dialog(case, step):
    """Format Cells on the selection, its Border tab (Ctrl+Tab three times from Number), read and pictured."""
    xs.keys('^1'); time.sleep(1.5)
    for _ in range(3):
        xs.keys('^{TAB}'); time.sleep(0.4)
    time.sleep(0.5)
    names = xs.cmd(r'uianames ^(Top Border|Bottom Border|Left Border|Right Border|Border|Presets|None|Outline|Inside|Style:|Colour:|Format Cells)$')
    xs.cmd(f'shot {name_of(case, step)}')
    record(case, step, names=names, shot=f'{name_of(case, step)}.png (the dialog; kept on this machine)')


def h10():
    # A shown top edge taken away: A1's bottom thick (set through COM), A2 records nothing; Format Cells over
    # A2, the Top button pressed to take it away, OK. What each cell keeps after.
    record('h10-take-away', 'start', excel=fresh())
    xs.cmd('setedge A1 9 1 4'); xs.cmd('select A2')
    state('h10-take-away', '0-a1-bottom-thick', look=['A1', 'A2'], pic=('A1', 'C4'))
    border_dialog('h10-take-away', '1-dialog-over-a2')
    # Alt+T is the Top Border button's key in the English dialog.
    xs.keys('%t'); time.sleep(0.6)
    border_dialog_after = xs.cmd(r'uianames ^(Top Border|Bottom Border)$')
    xs.cmd(f'shot {name_of("h10-take-away", "2-top-pressed")}')
    record('h10-take-away', '2-top-pressed', names=border_dialog_after)
    xs.keys('{ENTER}'); state('h10-take-away', '3-ok', look=['A1', 'A2'], pic=('A1', 'C4'))
    # Both cells record an outer edge: A1's bottom thick and A2's top thin, each set through COM on its own
    # cell. What each keeps, and which the dialog over A2 shows as its top.
    record('h10-both', 'start', excel=fresh())
    xs.cmd('setedge A1 9 1 4'); xs.cmd('setedge A2 8 1 2'); xs.cmd('select A2')
    state('h10-both', '0-both-set', look=['A1', 'A2'], pic=('A1', 'C4'))
    border_dialog('h10-both', '1-dialog-over-a2')
    xs.keys('{ESC}'); state('h10-both', '2-cancelled', look=['A1', 'A2'], pic=('A1', 'C4'))
    xs.cmd('select A1'); border_dialog('h10-both', '3-dialog-over-a1')
    xs.keys('{ESC}'); state('h10-both', '4-cancelled', look=['A1', 'A2'], pic=('A1', 'C4'))


# ---- h11: fills side by side, and dashes over a covered gridline ---------------------------------------------

def h11():
    # The DemoHost's case `fills` (samples/ExGrid.DemoPages/SheetCases.cs), cell for cell: Yellow FFFF00,
    # LightBlue 00B0F0, a double black line (xlDouble -4119, xlThick 4). The case holds double lines, and no dashes.
    record('h11', 'start', excel=fresh())
    for a, c in (('B2', 'FFFF00'), ('C2', '00B0F0'), ('E3', 'FFFF00'), ('B5', 'FFFF00'), ('C5', '00B0F0'), ('F5', 'FFFF00'), ('B8', 'FFFF00'), ('B9', '00B0F0')):
        xs.cmd(f'fill {a} {c}')
    for a, edge in (('E2', 9), ('B5', 10), ('E5', 10), ('B8', 9)):
        xs.cmd(f'setedge {a} {edge} -4119 4')
    for z in (100, 150):
        xs.cmd(f'zoom {z}'); state('h11', f'zoom-{z}', look=['B2', 'C2', 'E2', 'B5', 'E5', 'B8'], pic=('A1', 'G10'), scale=2)
    xs.cmd('zoom 100')


# ---- h12: a text section in a Number Format -------------------------------------------------------------------

def h12():
    # Each code on a number (12) and on text (abc): first through COM, then typed in Format Cells' Custom box,
    # as a user would. A refusal is recorded with its words.
    for code in ['@;0', '0;@;0']:
        case = f'h12-{code}'
        record(case, 'start', excel=fresh())
        xs.cmd('setup A1 12'); xs.cmd('setup A2 abc'); xs.cmd('setwidth A1 20')
        try:
            xs.cmd(f'setfmt A1:A2 {code}')
            record(case, 'com', result='set')
        except RuntimeError as e:
            record(case, 'com', result=str(e))
        state(case, '1-after-com', 'A1', 'A2', pic=('A1', 'C4'))
        xs.cmd('select A1:A2')
        xs.keys('^1'); time.sleep(1.5)
        xs.keys('%c'); time.sleep(0.3); xs.keys('{END}'); time.sleep(0.5)
        xs.keys('%t'); time.sleep(0.3); xs.keys('^a'); xs.keys(xs.lit(code), gap=40); time.sleep(0.5)
        xs.keys('{ENTER}'); time.sleep(1.2)
        names = xs.cmd(r'uianames (cannot|number format|Microsoft Excel|OK|Help)')
        xs.cmd(f'shot {name_of(case, "2-after-ok")}')
        record(case, '2-after-ok', names=names, shot=f'{name_of(case, "2-after-ok")}.png (the whole window; kept on this machine)')
        r = xs.ready()
        if not r.startswith('ready='):
            xs.keys('{ENTER}'); time.sleep(0.8); xs.keys('{ESC}'); time.sleep(0.8)
        state(case, '3-cells', 'A1', 'A2', pic=('A1', 'C4'))


if __name__ == '__main__':
    for arg in sys.argv[1:]:
        if arg.startswith('h9-'):
            h9(arg[3:])
        else:
            globals()[arg]()
    xs.cmd('stop')
