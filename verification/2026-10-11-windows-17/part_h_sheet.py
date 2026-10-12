"""verify-on-windows-17.md, Part H: ExSheet's half of h1 to h3 and h11, beside Excel's (excel/part_h_excel.py).

/sheet and /sheet?case=fills on the WebAssembly host, headed Chrome; every press and drag a real one through
input-server.ps1 (SendInput). After each step: the page's picture (the viewport only, never the browser's own
frame), and what the grid holds (the Name Box, the Focus, the selected cells, the outlines it draws).

    python3 part_h_sheet.py <chrome|msedge> h1 h2 h3 h11
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL = sys.argv[1]
BASE = 'http://localhost:5299'
REC = os.path.join(HERE, 'records', f'part-h-sheet-{CHANNEL}.jsonl')
SHOTS = f'{pg.WIN_PATH}\\shots-h'
os.makedirs(os.path.join(pg.WIN, 'shots-h'), exist_ok=True)
SHEET = '.ex-grid:has(> .ex-formula-bar)'
READY = f"""() => !!document.querySelector('{SHEET} .ex-viewport [id$="-r0c0"]') && !!document.querySelector('{SHEET} input.ex-key-field')"""
READ = f"""() => {{
  const g = document.querySelector('{SHEET}');
  const letters = (c) => {{ let s = ''; c += 1; while (c > 0) {{ const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); }} return s; }};
  const a1 = (id) => id?.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null;
  const box = (e) => {{ const b = e.getBoundingClientRect(); return [+b.left.toFixed(1), +b.top.toFixed(1), +b.width.toFixed(1), +b.height.toFixed(1)]; }};
  const f = g.querySelector('input.ex-key-field');
  return {{
    nameBox: g.querySelector('input.ex-name-box, .ex-name-box input')?.value ?? null,
    focus: a1(f?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant')),
    selected: [...g.querySelectorAll('.ex-viewport [aria-selected=true][id]')].map((e) => a1(e.id)).slice(0, 40),
    drawn: [...g.querySelectorAll('[class*="ex-selection"], [class*="ex-range"], [class*="ex-fill-handle"], [class*="ex-focus-cell"], [class*="ex-active"]')]
      .map((e) => ({{ cls: typeof e.className === 'string' ? e.className : '', box: box(e) }})).slice(0, 30),
    dpr: devicePixelRatio,
  }};
}}"""


def record(case, step, **kw):
    row = {'case': case, 'step': step, 'channel': CHANNEL, 't': time.strftime('%H:%M:%S'), **kw}
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:500], flush=True)


def state(case, step):
    time.sleep(0.8)
    p.input('move 3700 300')
    time.sleep(0.4)
    name = f'h-{case}-{step}-{CHANNEL}.png'
    p.cmd(op='shot', file=f'{SHOTS}\\{name}')
    record(case, step, read=p.eval(READ), shot=name)


def cell(a):
    col = ord(a[0]) - 65
    return f'{SHEET} .ex-viewport [id$="-r{int(a[1:]) - 1}c{col}"]'


def where(sel, nth=0, dx=0, dy=0):
    return p.cmd(op='where', sel=sel, nth=nth, dx=dx, dy=dy)['screen']


def drag(a, b, ctrl=False):
    (x0, y0), (x1, y1) = where(cell(a)), where(cell(b))
    p.input(f'{"ctrldrag" if ctrl else "drag"} {x0} {y0} {x1} {y1} 20')


def heading(text, role):
    """The screen point of the column Heading (role columnheader) or the row Heading (rowheader) reading text."""
    nth = p.eval(f"""(t) => [...document.querySelectorAll('{SHEET} [role={role}]')].findIndex((e) => e.textContent.trim() === t)""", text)
    if nth < 0:
        raise RuntimeError(f'no {role} {text}')
    return where(f'{SHEET} [role={role}]', nth)


def fresh(path='/sheet'):
    p.cmd(op='goto', url=f'{BASE}{path}', ready=READY)
    p.cmd(op='calibrate')
    p.input(f"english {p.eval('() => document.title')}")


def h1():
    fresh(); drag('A1', 'C5'); state('h1', '1-a1-c5-dragged')


def h2():
    fresh(); drag('A1', 'B2'); state('h2', '1-a1-b2')
    drag('D4', 'E6', ctrl=True); state('h2', '2-ctrl-d4-e6')


def h3():
    fresh()
    x, y = heading('C', 'columnheader'); p.input(f'click {x} {y}'); state('h3', '1-column-c')
    x, y = heading('3', 'rowheader'); p.input(f'click {x} {y}'); state('h3', '2-row-3')


def h11():
    # The browser's zoom is changed on /wide, a display-only page, never on a Sheet (ADR-0050), and kept by the
    # origin when /sheet?case=fills opens.
    for zoom, keys in ((100, '^{KEY0}'), (150, '^{OEMPLUS}^{OEMPLUS}^{OEMPLUS}')):
        p.cmd(op='goto', url=f'{BASE}/wide', ready="() => !!document.querySelector('.ex-grid .ex-row')")
        p.cmd(op='front'); p.keys('^{KEY0}', gap=200); time.sleep(1.0)
        if zoom != 100:
            p.keys(keys, gap=300); time.sleep(1.5)
        fresh('/sheet?case=fills')
        state('h11', f'zoom-{zoom}')
    p.cmd(op='goto', url=f'{BASE}/wide', ready="() => !!document.querySelector('.ex-grid .ex-row')")
    p.cmd(op='front'); p.keys('^{KEY0}', gap=200); time.sleep(1.0)
    record('h11', 'zoom-back', dpr=p.eval('() => devicePixelRatio'))


if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-h-sheet-{CHANNEL}-pg.jsonl'))
    try:
        for arg in sys.argv[2:]:
            globals()[arg]()
        record('console', 'end', messages=p.cmd(op='console')['value'])
    finally:
        p.stop()
