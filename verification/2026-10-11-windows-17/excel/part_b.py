"""verify-on-windows-17.md, Part B: what the corpus cannot ask, by hand in Excel, with real keys.

Each case: an Excel of xl-server.ps1's own, a new workbook of one sheet; keys through SendInput (a
virtual-key and a scan code per key); the Values read through COM only while Excel is Ready, after a
step; a picture of each step (PrintWindow) under shots/. One JSON line per step in records/part-b.jsonl.

    python3 part_b.py b1 b2 ...
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'part-b.jsonl')
os.makedirs(os.path.dirname(REC), exist_ok=True)
xs.log_to(os.path.join(HERE, '..', 'records', 'part-b-xs.jsonl'))


def record(case, step, **kw):
    row = {'case': case, 'step': step, 't': time.strftime('%H:%M:%S'), **kw}
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:600])


def shot(case, step):
    name = ''.join(ch if ch.isalnum() or ch in '-_' else '-' for ch in f'{case}-{step}')
    xs.cmd(f'shot {name}')
    return f'{name}.png'


def a1(case, step, *more, wait=0.6):
    time.sleep(wait)
    r = xs.ready()
    c = xs.cells('A1', *more) if r.startswith('ready=') else None
    record(case, step, ready=r, cells=c, shot=shot(case, step))
    return c


def fresh():
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start')
    time.sleep(1.5)
    return a


def b1():
    record('b1', 'start', excel=fresh())
    xs.keys(xs.lit('=RAND()') + '{ENTER}'); a1('b1', '1-rand-entered', 'B1')
    xs.keys('{UP}{RIGHT}'); a1('b1', '2-b1-selected', 'B1')
    xs.keys('1{ENTER}'); a1('b1', '3-b1-typed', 'B1')
    xs.keys('^z'); a1('b1', '4-ctrl-z', 'B1')
    xs.keys('^y'); a1('b1', '5-ctrl-y', 'B1')
    # An addition: the same again in the same workbook, to see the answer is not chance.
    xs.keys('^z'); a1('b1', '6-ctrl-z-again', 'B1')
    xs.keys('^y'); a1('b1', '7-ctrl-y-again', 'B1')


def b2():
    record('b2', 'start', excel=fresh())
    xs.keys(xs.lit('=RAND()') + '{ENTER}'); a1('b2', '1-rand-entered')
    u = xs.cmd('uia'); record('b2', 'places', uia=u)
    tab = [p for p in u.split(' ') if p.startswith('Sheet1:')][0].split(':')[1].split(',')
    x, y = (int(tab[0]) + int(tab[2])) // 2, (int(tab[1]) + int(tab[3])) // 2
    xs.cmd(f'dblclick {x} {y}'); time.sleep(0.6); record('b2', '2-tab-double-clicked', shot=shot('b2', '2-tab-double-clicked'))
    xs.keys('Data{ENTER}'); c = a1('b2', '3-renamed')
    record('b2', '3-sheet', ready=xs.ready())
    xs.keys('^z'); a1('b2', '4-ctrl-z'); record('b2', '4-sheet', ready=xs.ready())


def completion_list(case, typed, downs=40, page=6):
    xs.keys(xs.lit(typed)); time.sleep(1.0)
    record(case, f'typed {typed}', shot=shot(case, f'{typed_name(typed)}-0'))
    for i in range(1, downs // page + 1):
        xs.keys('{DOWN}' * page, gap=60); time.sleep(0.5)
        record(case, f'{typed} down x{i * page}', shot=shot(case, f'{typed_name(typed)}-down{i * page}'))
    xs.keys('{ESC}'); time.sleep(0.3); xs.keys('{ESC}'); time.sleep(0.4)
    record(case, 'escaped', ready=xs.ready())


def typed_name(t):
    return ''.join(ch if ch.isalnum() else '_' for ch in t).strip('_')


def b4():
    record('b4', 'start', excel=fresh())
    completion_list('b4', '=SUBTOTAL(', downs=24)
    completion_list('b4', '=AGGREGATE(', downs=24)
    completion_list('b4', '=AGGREGATE(1,', downs=12)


def status_menu(case, step):
    u = xs.cmd('uia'); record(case, 'places', uia=u)
    sb = [p for p in u.split(' ') if p.startswith('Bar:') or p.startswith('Status')]
    return u


def type_column(entries):
    for e in entries:
        xs.keys((xs.lit(e) if e else '') + '{ENTER}')
        time.sleep(0.2)



CSV = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\run-2026-10-11-17\\b3.csv'


def b3_1():
    record('b3', 'start', excel=fresh(), csv='Key,Val / a,1 / b,2 / c,3 (CRLF) at %LOCALAPPDATA%\\exgrid-layer3\\run-2026-10-11-17\\b3.csv, SHA-256 0a34e13b...')
    xs.keys(xs.lit('=RAND()') + '{ENTER}'); a1('b3', '1-rand-entered')
    xs.keys('{ALT}', gap=300); xs.keys('a', gap=300); time.sleep(1.0)
    record('b3', '2-alt-a', shot=shot('b3', '2-alt-a'))



def b3_2():
    xs.keys('ft', gap=400); time.sleep(2.5)
    record('b3', '3-file-dialog', windows=xs.cmd('allwindows'))
    xs.keys(xs.lit(CSV), gap=15); time.sleep(0.4); xs.keys('{ENTER}'); time.sleep(5)
    record('b3', '4-preview', windows=xs.cmd('allwindows'), shot=shot('b3', '4-preview'))



def b3_3():
    xs.cmd('click 2203 1478'); time.sleep(6)
    r = xs.ready(); record('b3', '5-loaded', ready=r, shot=shot('b3', '5-loaded'))
    loaded = r.split('active=')[1].split('!')[0]
    record('b3', '5-loaded-table', cells=xs.cells(*[f'{loaded}!{a}' for a in ('A1', 'B1', 'A4', 'B4')]), sheet1=xs.cells('Sheet1!A1'))
    xs.keys('^{PGUP}'); a1('b3', '6-back-on-sheet1')
    xs.keys('^%{F5}'); time.sleep(6); a1('b3', '7-refresh-all-unchanged', wait=0.2)
    record('b3', '7-loaded-table', cells=xs.cells(*[f'{loaded}!{a}' for a in ('B2', 'B3', 'B4')]))
    xs.keys('^%{F5}'); time.sleep(6); a1('b3', '7b-refresh-all-unchanged-again', wait=0.2)



def s1(case, step, wait=0.6):
    time.sleep(wait)
    r = xs.ready()
    c = xs.cells('Sheet1!A1', 'b3!B4') if r.startswith('ready=') else None
    record(case, step, ready=r, cells=c, shot=shot(case, step))


def b3_4():
    record('b3', 'note', note='steps 6, 7 and 7b above ran on the sheet b3 (Ctrl+PgUp did not move: b3 stands left of Sheet1); Sheet1!A1 was not read after them')
    xs.keys('^{PGDN}'); s1('b3', '8-on-sheet1')
    xs.keys('^%{F5}'); time.sleep(6); s1('b3', '9-refresh-all-unchanged', wait=0.2)
    xs.keys('^%{F5}'); time.sleep(6); s1('b3', '10-refresh-all-unchanged-again', wait=0.2)


def b3_5():
    xs.keys('^%{F5}'); time.sleep(6); s1('b3', '12-refresh-all-after-the-file-changed', wait=0.2)
    xs.keys('^%{F5}'); time.sleep(6); s1('b3', '13-refresh-all-unchanged-after-that', wait=0.2)



def b5_menu():
    record('b5', 'start', excel=fresh())
    xs.cmd('rclick 2600 2071'); time.sleep(1.2)
    record('b5', '1-status-bar-menu', windows=xs.cmd('windows'), shot=shot('b5', '1-status-bar-menu'))



def b5_tick():
    for y in (1733, 1777, 1821):
        xs.cmd(f'click 2700 {y}'); time.sleep(0.7)
    record('b5', '2-ticked', windows=xs.cmd('windows'), shot=shot('b5', '2-ticked'))
    xs.keys('{ESC}'); time.sleep(0.6)
    record('b5', '3-menu-closed', windows=xs.cmd('windows'), ready=xs.ready())


def status(case, step):
    time.sleep(1.0)
    record(case, step, ready=xs.ready(), shot=shot(case, step))


def b5_values():
    xs.cmd('select A1')
    for e in ('1', '2', '=1/0', 'x'):
        xs.keys(xs.lit(e) + '{ENTER}', gap=40); time.sleep(0.3)
    record('b5', '4-typed', cells=xs.cells('A1', 'A2', 'A3', 'A4', 'A5'))
    g = xs.geo('A1', 'A5', 'A3')
    record('b5', 'places', geo=g)
    xs.cmd('click %d %d' % xs.centre(g['A1'])); time.sleep(0.4)
    xs.cmd('shiftclick %d %d' % xs.centre(g['A5']))
    status('b5', '5-a1-a5-selected')


def b6():
    g = xs.geo('A1', 'A5', 'A3')
    xs.cmd('click %d %d' % xs.centre(g['A3'])); time.sleep(0.4)
    xs.keys('{DEL}'); time.sleep(0.4)
    record('b6', '1-a3-cleared', cells=xs.cells('A1', 'A2', 'A3', 'A4', 'A5'))
    xs.cmd('click %d %d' % xs.centre(g['A1'])); time.sleep(0.4)
    xs.cmd('shiftclick %d %d' % xs.centre(g['A5']))
    status('b6', '2-a1-a5-selected')



def clear_a():
    g = xs.geo('A1', 'A5')
    xs.cmd('click %d %d' % xs.centre(g['A1'])); time.sleep(0.3)
    xs.cmd('shiftclick %d %d' % xs.centre(g['A5'])); time.sleep(0.3)
    xs.keys('{DEL}'); time.sleep(0.4)


def sel(case, step, a, b):
    g = xs.geo(a, b)
    xs.cmd('click %d %d' % xs.centre(g[a])); time.sleep(0.4)
    if b != a:
        xs.cmd('shiftclick %d %d' % xs.centre(g[b]))
    status(case, step)


def b7():
    clear_a()
    xs.cmd('select A1')
    for e in ('1', 'TRUE', '2026-10-11', 'x'):
        xs.keys(xs.lit(e) + '{ENTER}', gap=40); time.sleep(0.3)
    record('b7', '1-typed', cells=xs.cells('A1', 'A2', 'A3', 'A4', 'A5'))
    sel('b7', '2-a1-a4', 'A1', 'A4')
    sel('b7', '3-a1-a2', 'A1', 'A2')
    sel('b7', '4-a3', 'A3', 'A3')


def b8():
    clear_a()
    xs.cmd('setfmt A1:A2 General')
    xs.cmd('setfmt A1 #,##0.00'); xs.cmd('setfmt A2 0%')
    record('b8', 'note', note='A1:A5 cleared by Delete, then the formats set through COM on the empty cells (setup only): A1 #,##0.00, A2 0%')
    xs.cmd('select A1')
    for e in ('1000', '2000'):
        xs.keys(xs.lit(e) + '{ENTER}', gap=40); time.sleep(0.3)
    record('b8', '1-typed', cells=xs.cells('A1', 'A2'))
    sel('b8', '2-a1-a2', 'A1', 'A2')
    sel('b8', '3-a2-a1-from-a2', 'A2', 'A1')



def b5_untick():
    xs.cmd('rclick 2600 2071'); time.sleep(1.2)
    for y in (1733, 1777, 1821):
        xs.cmd(f'click 2700 {y}'); time.sleep(0.7)
    record('b5', '9-unticked-again', shot=shot('b5', '9-unticked-again'))
    xs.keys('{ESC}'); time.sleep(0.6)
    record('b5', '9-menu-closed', ready=xs.ready())



def ribbon(*letters):
    xs.keys('{ALT}', gap=600)
    for l in letters:
        xs.keys(l, gap=700)


def heads():
    g = xs.geo('B1', 'A2')
    return {'colB': ((g['B1'][0] + g['B1'][2]) // 2, g['B1'][1] - 14), 'row2': (g['A2'][0] - 18, (g['A2'][1] + g['A2'][3]) // 2)}


def b9_cw013():
    record('b9', 'CW-013 start', excel=fresh())
    xs.cmd('select B1'); ribbon('h', 'o', 'w'); time.sleep(0.8); xs.keys('20{ENTER}'); time.sleep(0.8)
    record('b9', 'CW-013 1-width-20 through Home > Format > Column Width', width=xs.cmd('colwidth B1'), shot=shot('b9', 'CW-013-1'))
    xs.keys('^z'); time.sleep(0.8)
    record('b9', 'CW-013 2-ctrl-z', width=xs.cmd('colwidth B1'), shot=shot('b9', 'CW-013-2'))


def b9_cw014():
    record('b9', 'CW-014 start', excel=fresh())
    xs.cmd('select B1'); ribbon('h', 'o', 'w'); time.sleep(0.8); xs.keys('20{ENTER}'); time.sleep(0.8)
    record('b9', 'CW-014 1-width-20', width=xs.cmd('colwidth B1'))
    xs.cmd('select B1'); ribbon('h', 'd', 'c'); time.sleep(0.8)
    record('b9', 'CW-014 2-column-B-deleted through Home > Delete > Delete Sheet Columns', width=xs.cmd('colwidth B1'))
    xs.keys('^z'); time.sleep(0.8)
    record('b9', 'CW-014 3-ctrl-z', width=xs.cmd('colwidth B1'), shot=shot('b9', 'CW-014-3'))


def number_format_number():
    ribbon('h', 'n'); time.sleep(0.5); xs.keys('Number{ENTER}'); time.sleep(0.9)


def b9_lvl023():
    record('b9', 'LVL-023 start', excel=fresh())
    xs.cmd('select B2'); xs.keys('0.5{ENTER}'); xs.cmd('select B2')
    xs.keys('^+5'); time.sleep(0.6)
    record('b9', 'LVL-023 1-b2-ctrl-shift-5', cells=xs.cells('B2'))
    h = heads(); xs.cmd('click %d %d' % h['colB']); time.sleep(0.5)
    number_format_number()
    record('b9', 'LVL-023 2-column-B-set-to-Number', ready=xs.ready(), cells=xs.cells('B2', 'B5'))
    xs.keys('^z'); time.sleep(0.8)
    record('b9', 'LVL-023 3-ctrl-z', cells=xs.cells('B2', 'B5'), shot=shot('b9', 'LVL-023-3'))


def b9_lvl024():
    record('b9', 'LVL-024 start', excel=fresh())
    xs.cmd('select B2'); xs.keys('0.5{ENTER}')
    h = heads(); xs.cmd('click %d %d' % h['row2']); time.sleep(0.5)
    xs.keys('^+5'); time.sleep(0.6)
    record('b9', 'LVL-024 1-row-2-ctrl-shift-5', ready=xs.ready(), cells=xs.cells('B2', 'E2'))
    xs.cmd('select B2'); ribbon('h', 'd', 'r'); time.sleep(0.8)
    record('b9', 'LVL-024 2-row-2-deleted through Home > Delete > Delete Sheet Rows', cells=xs.cells('B2'))
    xs.keys('^z'); time.sleep(0.8)
    record('b9', 'LVL-024 3-ctrl-z', cells=xs.cells('B2', 'E2'), shot=shot('b9', 'LVL-024-3'))


def b9_lvl030():
    record('b9', 'LVL-030 start', excel=fresh())
    xs.cmd('select B2'); xs.keys('0.5{ENTER}'); xs.cmd('select B2')
    xs.keys('^+5'); time.sleep(0.6)
    record('b9', 'LVL-030 1-b2-ctrl-shift-5', cells=xs.cells('B2'))
    h = heads(); xs.cmd('click %d %d' % h['colB']); time.sleep(0.5); xs.cmd('ctrlclick %d %d' % h['row2']); time.sleep(0.5)
    record('b9', 'LVL-030 2-column-B-and-row-2-selected', ready=xs.ready())
    number_format_number()
    record('b9', 'LVL-030 3-both-set-to-Number', cells=xs.cells('B2', 'B5', 'E2'))
    xs.keys('^z'); time.sleep(0.8)
    record('b9', 'LVL-030 4-ctrl-z', cells=xs.cells('B2', 'B5', 'E2'), shot=shot('b9', 'LVL-030-4'))



WIDE = '12345678901'   # eleven digits: General shows them all, and the entry widens a standard column


def widen(col):
    xs.cmd(f'select {col}1'); xs.keys(WIDE + '{ENTER}'); time.sleep(0.8)


def cw(case, step, *cells_, shot_=False):
    row = {'width': xs.cmd('colwidth ' + ' '.join(cells_)), 'cols': xs.cmd('savecols'), 'cells': xs.cells(*cells_)}
    if shot_:
        row['shot'] = shot('b10', f'{case}-{step}')
    record('b10', f'{case} {step}', **row)


def b10_021():
    record('b10', 'CW-021 start', excel=fresh(), entry=WIDE)
    widen('B'); cw('CW-021', '1-widened-by-the-entry', 'B1', shot_=True)


def b10_022():
    record('b10', 'CW-022 start', excel=fresh())
    widen('B'); cw('CW-022', '1-widened', 'B1')
    xs.cmd('select B1'); ribbon('h', 'o', 'w'); time.sleep(0.8); xs.keys('11{ENTER}'); time.sleep(0.8)
    cw('CW-022', '2-width-11-through-Column-Width', 'B1')
    xs.cmd('select D5'); xs.keys('123456789012345{ENTER}'); time.sleep(0.6)
    record('b10', 'CW-022 note', note='an addition: a longer number typed in D5 afterwards does not touch B (a control of the read)')


def b10_023():
    record('b10', 'CW-023 start', excel=fresh())
    widen('B'); cw('CW-023', '1-widened', 'B1', 'C1')
    xs.cmd('select C1'); ribbon('h', 'i', 'c'); time.sleep(0.8)
    cw('CW-023', '2-column-inserted-at-C through Home > Insert > Insert Sheet Columns', 'B1', 'C1', 'D1', shot_=True)


def b10_025():
    record('b10', 'CW-025 start', excel=fresh())
    widen('C'); cw('CW-025', '1-C-widened', 'B1', 'C1')
    xs.cmd('select B1'); ribbon('h', 'd', 'c'); time.sleep(0.8)
    cw('CW-025', '2-column-B-deleted', 'B1', 'C1', shot_=True)


def b10_026():
    record('b10', 'CW-026 start', excel=fresh())
    widen('B'); cw('CW-026', '1-widened', 'B1')
    xs.cmd('select B1'); ribbon('h', 'o', 'w'); time.sleep(0.8); xs.keys('20{ENTER}'); time.sleep(0.8)
    cw('CW-026', '2-width-20', 'B1')
    xs.keys('^z'); time.sleep(0.8)
    cw('CW-026', '3-ctrl-z', 'B1', shot_=True)


if __name__ == '__main__':
    for name in sys.argv[1:]:
        globals()[name]()
