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
    name = f'{case}-{step}'
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


if __name__ == '__main__':
    for name in sys.argv[1:]:
        globals()[name]()
