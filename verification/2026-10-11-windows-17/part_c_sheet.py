"""verify-on-windows-17.md, Part C in ExSheet: the seventh run's 22 F4 cases (verify-on-windows-7.md, Part B) on
/sheet, typed into a cell and again into the Formula Bar; F4 with no edit open; case 1 as a burst behind 150 ms;
and the seventh run's Part C (the Heading reading).

Every key and press is real OS input (pg-server.mjs through input-server.ps1: SendInput, a virtual-key and
a scan code per key). What the edit holds is read from the DOM after the case's keys: the focused field's
value and selection, which surface holds the keyboard, whether an edit is open, and the Focus.

Every case is typed in D10 of /sheet, which the page leaves empty (the Excel half typed cases 11, 15 and
16 in D1 and A1: the References then read D11 and D12 here where Excel's read A2 and A3). Before each case
D10:F13 is cleared with one Delete when anything stands there.

    python3 part_c_sheet.py <chrome|msedge> <wasm|server|server-150>
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL, HOST = sys.argv[1], sys.argv[2]
BASE = 'http://localhost:5299' if HOST == 'wasm' else 'http://localhost:5298'
CONTROL = 'http://localhost:7298'
RTT = 150 if HOST == 'server-150' else 0
SETTLE = 0.9 if RTT else 0.6
REC = os.path.join(HERE, 'records', f'part-c-sheet-{HOST}-{CHANNEL}.jsonl')
SHOTS = os.path.join(pg.WIN, 'shots-c')
os.makedirs(SHOTS, exist_ok=True)
L = pg.lit

SHEET = '.ex-grid:has(> .ex-formula-bar)'
READY = """() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)'); if (!g) return false;
  const c = [...g.querySelectorAll('[id$="-r11c1"]')].map(e => e.textContent).join(''); return c.includes('318.25') && !!g.querySelector('input.ex-key-field'); }"""

READ = """(cells) => {
  const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
  const ae = document.activeElement;
  const editor = g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
  const bar = g.querySelector('input.ex-formula-bar-text, .ex-formula-bar-text input');
  const nb = g.querySelector('input.ex-name-box, .ex-name-box input');
  const field = g.querySelector('input.ex-key-field');
  const role = (e) => !e ? null : e === g ? 'root' : e === field ? 'key-field' : e === editor ? 'cell' : e === bar ? 'bar' : e === nb ? 'name-box' : (e.tagName.toLowerCase() + (e.id ? '#' + e.id : ''));
  const f = (e) => e ? { value: e.value, sel: [e.selectionStart, e.selectionEnd], dir: e.selectionDirection } : null;
  const letters = (c) => { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; };
  const id = field?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant');
  const a1 = id?.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null;
  const text = (a) => { const m = /^([A-Z]+)(\\d+)$/.exec(a); let col = 0; for (const ch of m[1]) col = col * 26 + ch.charCodeAt(0) - 64; return [...g.querySelectorAll(`[id$="-r${Number(m[2]) - 1}c${col - 1}"]`)].map(e => e.textContent).join('|'); };
  const points = [...g.querySelectorAll('.ex-point, .ex-point-dashes')].length;
  return { active: role(ae), editing: !!editor, editor: f(editor), bar: f(bar), nameBox: nb?.value ?? null, focus: a1,
    selected: [...g.querySelectorAll('[aria-selected=true][id]')].slice(0, 6).map(e => e.id.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1))),
    points, cells: Object.fromEntries(cells.map(a => [a, text(a)])),
    scroll: (() => { const v = g.querySelector('.ex-viewport'); return v ? [v.scrollLeft, v.scrollTop] : null; })() };
}"""

CELLS = ['D10', 'D11', 'D12', 'E10', 'E11', 'F10', 'F13']
CASES = {
    1: ('=B2', '', [1, 2, 3, 4], ''),
    2: ('=A1+B2', '{LEFT}{LEFT}{LEFT}', [1], ''),
    3: ('=A1+B2', '{HOME}{RIGHT}', [1], ''),
    4: ('=A1+B2', '{HOME}{RIGHT}{RIGHT}', [1], ''),
    5: ('=SUM(A1:B2)', '{LEFT}', [1, 2, 3, 4], ''),
    6: ('=$A1:B2', '{LEFT}', [1], ''),
    7: ('=A1+B2', '+{HOME}+{RIGHT}', [1], ''),
    8: ('=SUM(A:A)', '{LEFT}', [1, 2, 3], ''),
    9: ('=SUM(1:1)', '{LEFT}', [1, 2, 3], ''),
    10: ('=Sheet2!A1', '', [1], ''),
    11: ('=SUM(Positions[PV])', '{LEFT}', [1], ''),
    12: ('=SUM(A1)', '{HOME}{RIGHT}{RIGHT}', [1], ''),
    13: ('=1+2', '', [1], ''),
    14: ('B2', '', [1], ''),
    15: ('=', '{DOWN}', [1], ''),
    '15b': ('=', '{DOWN}', [1], '{DOWN}'),
    16: ('=', '{DOWN}', [1], L('+')),
    18: ('=A1+B1', '{LEFT}{LEFT}+{LEFT}', [1], ''),
    19: ('=A1+B2', '{HOME}{RIGHT}{RIGHT}+{RIGHT}+{RIGHT}+{RIGHT}', [1], ''),
    20: ('=A1+$B$2', '{HOME}{RIGHT}+{END}', [1], ''),
    21: ('=b2', '', [1], ''),
    22: ('=B2:A1', '', [1], ''),
}
POINTING = {15, '15b', 16}


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')


def read():
    return p.eval(READ, CELLS)


def cell_sel(a):
    m = __import__('re').match(r'([A-Z]+)(\d+)', a)
    col = 0
    for ch in m.group(1):
        col = col * 26 + ord(ch) - 64
    return f'{SHEET} [id$="-r{int(m.group(2)) - 1}c{col - 1}"]'


def ensure_d10():
    """D10 drawn: a case that commits with Home or End can leave the view far from it (case 20 as typed
    ended at the row's end). Escape, then Ctrl+Home with the keyboard on the grid, brings the view back."""
    for _ in range(3):
        if p.eval('(sel) => !!document.querySelector(sel)', cell_sel('D10')):
            return True
        p.keys('{ESC}'); time.sleep(SETTLE / 2)
        p.keys('^{HOME}'); time.sleep(SETTLE)
    return p.eval('(sel) => !!document.querySelector(sel)', cell_sel('D10'))


def reset():
    ensure_d10()
    for _ in range(6):
        s = read()
        if not s['editing'] and s['active'] in ('key-field', 'root'):
            break
        p.keys('{ESC}'); time.sleep(SETTLE / 2)
    s = read()
    held = [a for a in ('D10', 'D11', 'D12', 'E10', 'E11', 'F10', 'F13') if s['cells'].get(a)]
    if held:
        p.cmd(op='press', sel=cell_sel('D10')); time.sleep(SETTLE)
        p.cmd(op='press', sel=cell_sel('F13'), button='shift'); time.sleep(SETTLE)
        p.keys('{DEL}'); time.sleep(SETTLE)
        p.cmd(op='press', sel=cell_sel('D12')); time.sleep(SETTLE)
    ensure_d10()
    s = read()
    if s['focus'] != 'D10' or len(s['selected']) > 1:
        p.cmd(op='press', sel=cell_sel('D10')); time.sleep(SETTLE)
    return held


def trial(cid, surface, variant, n):
    text, nav, _, after = CASES[cid]
    held = reset()
    pre = []
    if surface == 'bar':
        pre.append(p.cmd(op='press', sel=f'{SHEET} input.ex-formula-bar-text, {SHEET} .ex-formula-bar-text input', at='right', dx=8))
        time.sleep(SETTLE)
    keys = L(text) + ('{F2}' if variant == 'f2' else '') + nav + '{F4}' * n + after
    p.keys(keys, gap=80)
    time.sleep(SETTLE + 0.3)
    s = read()
    shot = os.path.join(SHOTS, f'c{cid}-{surface}-{variant}-f4x{n}-{HOST}-{CHANNEL}.png')
    p.cmd(op='shot', file=shot.replace(pg.WIN, pg.WIN_PATH).replace('/', '\\'), sel=f'{SHEET}')
    ev = p.cmd(op='events')['value']
    record({'case': cid, 'surface': surface, 'variant': variant, 'presses': n, 'keys': keys, 'clearedBefore': held, 'state': s,
            'f4': [e for e in ev if e.get('type') == 'keydown' and e.get('key') == 'F4'], 'shot': os.path.basename(shot)})
    p.keys('{ESC}'); time.sleep(SETTLE / 2)
    return s


def no_edit_f4():
    reset()
    p.keys(L('=B2') + '{ENTER}{UP}'); time.sleep(SETTLE)
    before = read()
    p.keys('{F4}'); time.sleep(SETTLE + 0.3)
    after = read()
    ev = p.cmd(op='events')['value']
    record({'case': 'no-edit-f4', 'note': 'D10 typed =B2, Enter, Up; F4 with no edit open', 'before': before, 'after': after,
            'f4': [e for e in ev if e.get('type') == 'keydown' and e.get('key') == 'F4']})


def burst():
    reset()
    r = p.input(f"burst {L('=B2')}{{F4}}{{F4}}{{F4}}{{F4}}")
    states = []
    for wait in (0.3, 1.0, 3.0):
        time.sleep(wait)
        states.append(read())
    ev = p.cmd(op='events')['value']
    record({'case': '1-burst', 'note': '=B2 then four F4 in one SendInput call', 'input': r, 'states': states,
            'keys': [(e.get('type'), e.get('key'), e.get('value')) for e in ev if e.get('type') in ('keydown', 'input')]})
    p.keys('{ESC}'); time.sleep(SETTLE)


def heading():
    """The seventh run's Part C on /sheet: a press on the Column Heading C with the view at the top left,
    then Ctrl+Shift+Right. /sheet is not an empty Sheet: what stands in row 1 is recorded."""
    reset()
    p.keys('^{HOME}'); time.sleep(SETTLE)
    before = read()
    hdr = p.eval("""() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const hs = [...g.querySelectorAll('[role=columnheader].ex-header-cell')];
        const i = hs.findIndex(x => x.textContent.trim() === 'C'); const b = hs[i].getBoundingClientRect();
        const row1 = [...g.querySelectorAll('[id$="-r0c0"],[id$="-r0c1"],[id$="-r0c2"],[id$="-r0c3"],[id$="-r0c4"],[id$="-r0c5"]')].map(e => e.textContent);
        return { i, w: b.width, row1 }; }""")
    p.cmd(op='press', sel=f'{SHEET} [role=columnheader].ex-header-cell', nth=hdr['i'], at='left', dx=hdr['w'] * 0.35)
    time.sleep(SETTLE + 0.3)
    s1 = read()
    p.keys('^+{RIGHT}'); time.sleep(SETTLE + 0.6)
    s2 = read()
    shot = os.path.join(SHOTS, f'heading-{HOST}-{CHANNEL}.png')
    p.cmd(op='shot', file=shot.replace(pg.WIN, pg.WIN_PATH).replace('/', '\\'), sel=f'{SHEET}')
    extra = p.eval("""() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const hs = [...g.querySelectorAll('[role=columnheader].ex-header-cell')].map(h => h.textContent.trim()).filter(Boolean);
        return { headersShown: [hs[0], hs[hs.length - 1]], selectedHeaders: [...g.querySelectorAll('[role=columnheader][aria-selected=true]')].length }; }""")
    record({'case': 'heading', 'row1': hdr['row1'], 'before': before, 'afterPress': s1, 'afterCtrlShiftRight': s2, 'view': extra, 'shot': os.path.basename(shot)})
    p.keys('^{HOME}'); time.sleep(SETTLE)


def run_all():
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    cal = p.cmd(op='calibrate')
    k = p.input(f"english {p.cmd(op='eval', fn='() => document.title')['value']}")
    p.input('imeoff')
    rt = p.cmd(op='rtt', control=CONTROL, ms=RTT) if HOST.startswith('server') else None
    record({'case': 'start', 'channel': CHANNEL, 'version': p.version, 'host': HOST, 'base': BASE, 'rtt': rt, 'calibration': cal, 'keyboard': k,
            'page': p.eval("() => ({ dpr: devicePixelRatio, inner: [innerWidth, innerHeight], ua: navigator.userAgent })")})
    no_edit_f4()
    for surface in ('cell', 'bar'):
        for cid, (text, nav, presses, after) in CASES.items():
            variants = ['typed'] + (['f2'] if surface == 'cell' and nav and cid not in POINTING else [])
            for v in variants:
                for n in presses:
                    trial(cid, surface, v, n)
    if HOST == 'server-150':
        burst()
    heading()
    if RTT:
        p.cmd(op='rtt', control=CONTROL, ms=0)
    record({'case': 'console', 'messages': p.cmd(op='console')['value']})




def smoke():
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    print(json.dumps(p.cmd(op='calibrate'))[:300])
    print(p.input(f"english {p.eval('() => document.title')}"))
    print(json.dumps(read())[:600])
    s = trial(1, 'cell', 'typed', 1)
    print(json.dumps(s)[:800])


def run_burst():
    """Behind 150 ms: case 1 as a burst, and F4 with no edit open."""
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    cal = p.cmd(op='calibrate')
    k = p.input(f"english {p.eval('() => document.title')}")
    p.input('imeoff')
    rt = p.cmd(op='rtt', control=CONTROL, ms=RTT)
    record({'case': 'start', 'channel': CHANNEL, 'version': p.version, 'host': HOST, 'base': BASE, 'rtt': rt, 'calibration': cal, 'keyboard': k})
    burst()
    burst()
    no_edit_f4()
    p.cmd(op='rtt', control=CONTROL, ms=0)
    record({'case': 'console', 'messages': p.cmd(op='console')['value']})


if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-c-sheet-{HOST}-{CHANNEL}-pg.jsonl'))
    try:
        for name in sys.argv[3:] or ['run_all']:
            globals()[name]()
    finally:
        p.stop()
