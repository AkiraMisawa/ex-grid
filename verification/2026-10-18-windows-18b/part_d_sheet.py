"""verify-on-windows-17.md, Part D in the browsers: d3 to d6 (KeyTips, Ctrl+F1 and F10, ticket 160, SH-51, SH-52)
on /sheet, and d3, d4 and d6 on /sheet?chrome=mud.

Every key and press is real OS input (pg-server.mjs through input-server.ps1: SendInput, a virtual-key and a
scan code per key). After each step: the Sheet's KeyTips (their letters and the item each stands on), the
toolbar, DOM focus and whether the document has focus, the edit, the Focus, D10's look; the window in front
and every visible top-level window of the browser's process (a menu the browser opens is a window of its
own); and a picture of the screen where the browser's window stands (CopyFromScreen).

    python3 part_d_sheet.py <chrome|msedge> <wasm|server>
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL, HOST = sys.argv[1], sys.argv[2]
BASE = 'http://localhost:5318' if HOST == 'wasm' else 'http://localhost:5317'
SETTLE = 0.8 if HOST != 'wasm' else 0.6
REC = os.path.join(HERE, 'records', f'part-d-sheet-{HOST}-{CHANNEL}.jsonl')
SHOTS = os.path.join(pg.WIN, 'shots-d')
os.makedirs(SHOTS, exist_ok=True)
SHEET = '.ex-grid:has(> .ex-formula-bar)'
READY = """() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)'); if (!g) return false;
  const c = [...g.querySelectorAll('[id$="-r11c1"]')].map(e => e.textContent).join(''); return c.includes('318.25') && !!g.querySelector('input.ex-key-field'); }"""
STATE = """() => {
  const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
  const f = g.querySelector('input.ex-key-field');
  const editor = g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
  const ae = document.activeElement;
  const id = f?.getAttribute('aria-activedescendant');
  const letters = (c) => { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; };
  const where = (e) => !e ? null : e === f ? 'key-field' : e === editor ? 'cell-editor' : e.closest('.ex-sheet-toolbar') ? 'toolbar:' + (e.getAttribute('aria-label') || e.className.split(' ')[0]) : e === document.body ? 'body' : g.contains(e) ? 'grid:' + (e.className.split(' ')[0] || e.tagName.toLowerCase()) : e.tagName.toLowerCase() + (e.id ? '#' + e.id : '');
  const tb = document.querySelector('.ex-sheet-toolbar');
  const tips = [...document.querySelectorAll('.ex-sheet-keytip')].map(k => {
    const item = k.closest('[role=button],[role=menuitem],[role=tab],button,[aria-label],[title]') ?? k.parentElement;
    return { tip: k.textContent, on: (item?.getAttribute('aria-label') || item?.getAttribute('title') || item?.textContent || '').trim().slice(0, 60) };
  });
  const d10 = [...g.querySelectorAll('[id$="-r9c3"]')].find(e => e.closest('.ex-viewport'));
  const t = d10?.querySelector('*') ?? d10; const cs = t ? getComputedStyle(t) : null; const cb = d10 ? getComputedStyle(d10) : null;
  return { keyboard: where(ae), hasFocus: document.hasFocus(), visibility: document.visibilityState, editing: !!editor, editorValue: editor?.value ?? null,
    focus: id?.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null,
    toolbar: !!tb, toolbarItems: tb ? [...tb.querySelectorAll('[aria-pressed=true]')].map(e => e.getAttribute('aria-label') || e.textContent.trim()).filter(Boolean) : null,
    keytips: tips, focusFont: document.querySelector('#sheet-focus-font')?.textContent ?? null,
    dialogs: [...document.querySelectorAll('[role=dialog],[role=listbox],[role=menu]')].filter(d => d.offsetParent !== null).map(d => (d.getAttribute('aria-label') || d.textContent).trim().slice(0, 60)),
    d10: cs ? { weight: cs.fontWeight, style: cs.fontStyle, lines: cs.textDecorationLine, color: cs.color, fill: cb.backgroundColor, align: cs.textAlign, justify: cb.justifyContent, size: cs.fontSize, family: cs.fontFamily.slice(0, 30), text: d10.textContent } : null,
    window: { sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, dpr: devicePixelRatio } };
}"""


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps({k: row.get(k) for k in ('case', 'step', 'page')}, ensure_ascii=False), json.dumps(row.get('state', {}), ensure_ascii=False)[:400])


def state():
    return p.eval(STATE)


def browser_windows():
    w = p.input('windows')
    fg = p.input('foreground')
    # Only the browser's own windows (its process), and the window in front.
    name = 'chrome' if CHANNEL == 'chrome' else 'msedge'
    mine = [x for x in w[len('ok windows '):].split(' ;; ') if f'|{name}|' in x]
    return {'front': fg, 'browserWindows': mine[:12]}


def picture(name):
    s = state()['window']
    x, y = int(s['sx'] * s['dpr']), int(s['sy'] * s['dpr'])
    w, h = int(s['ow'] * s['dpr']), int(s['oh'] * s['dpr'])
    f = f'{pg.WIN_PATH}\\shots-d\\{name}-{PAGE}-{HOST}-{CHANNEL}.png'
    p.input(f'screen {x} {y} {w} {h} {f}')
    return os.path.basename(f.replace('\\', '/'))


def step(case, name, keys=None, settle=None, pic=True):
    if keys:
        p.keys(keys, gap=120)
    time.sleep(settle if settle is not None else SETTLE + 0.2)
    row = {'case': case, 'step': name, 'page': PAGE, 'keys': keys, 'state': state(), **browser_windows()}
    if pic:
        row['shot'] = picture(f'{case}-{name}')
    row['events'] = [e for e in p.cmd(op='events')['value'] if e.get('type') in ('keydown', 'keyup', 'focusin', 'focusout')][-14:]
    record(row)
    return row


def cell(a):
    import re
    m = re.match(r'([A-Z]+)(\d+)', a)
    col = 0
    for ch in m.group(1):
        col = col * 26 + ord(ch) - 64
    return f'{SHEET} [id$="-r{int(m.group(2)) - 1}c{col - 1}"]'


def home():
    """Nothing open, the keyboard back on the page, D10 the Focus alone."""
    for _ in range(5):
        s = state()
        if not s['editing'] and not s['keytips'] and not s['dialogs'] and s['keyboard'] == 'key-field' and s['hasFocus']:
            break
        p.cmd(op='front'); p.keys('{ESC}'); time.sleep(SETTLE / 2)
    s = state()
    if s['focus'] != 'D10' or s['keyboard'] != 'key-field' or not s['hasFocus']:
        p.cmd(op='press', sel=cell('D10')); time.sleep(SETTLE)


def d3():
    home(); step('d3', '0-pressed-d10')
    step('d3', '1-alt-alone', '{ALT}')
    step('d3', '2-escape', '{ESC}')
    home(); step('d3', '3-f10', '{F10}')
    step('d3', '4-escape', '{ESC}')


def d4():
    home()
    # D10 holds a number, so that a number format's letter shows what it ran; cleared again at the end.
    p.keys('1234.5{ENTER}{UP}'); time.sleep(SETTLE)
    r = step('d4', '0-alt-h', '{ALT}h')
    tips = [t['tip'] for t in r['state']['keytips']]
    p.keys('{ESC}{ESC}'); time.sleep(SETTLE)
    for tip in tips:
        home()
        before = state()
        keys = '{ALT}h' + ''.join('{' + c + '}' if c in '+^%~(){}[]' else c.lower() for c in tip)
        row = step('d4', f'letter-{tip}', keys)
        row_after = row['state']
        changed = {k: (before.get(k), row_after.get(k)) for k in ('d10', 'toolbarItems', 'dialogs', 'editing', 'keyboard', 'focusFont') if before.get(k) != row_after.get(k)}
        record({'case': 'd4', 'step': f'letter-{tip}-what-it-ran', 'page': PAGE, 'changed': changed})
        if row_after['dialogs'] or row_after['keytips']:
            p.keys('{ESC}'); time.sleep(SETTLE)
            p.keys('{ESC}'); time.sleep(SETTLE)
        step('d4', f'letter-{tip}-after-escape')
        if state()['d10'] != before['d10']:
            home(); p.keys('^{KEYZ}'); time.sleep(SETTLE)
            record({'case': 'd4', 'step': f'letter-{tip}-undone', 'page': PAGE, 'd10': state()['d10']})
    home(); p.keys('{DEL}'); time.sleep(SETTLE)


def d5():
    home()
    step('d5', '1-ctrl-f1', '^{F1}')
    step('d5', '2-ctrl-f1-again', '^{F1}')


def d6():
    home()
    step('d6', '0-f2', '{F2}')
    step('d6', '1-alt', '{ALT}')
    step('d6', '2-f10', '{F10}')
    step('d6', '3-escape', '{ESC}')
    home()


def run():
    global PAGE
    for PAGE, path, cases in (('sheet', '/sheet', (d3, d4, d5, d6)), ('mud', '/sheet?chrome=mud', (d3, d4, d6))):
        p.cmd(op='goto', url=f'{BASE}{path}', ready=READY)
        cal = p.cmd(op='calibrate')
        k = p.input(f"english {p.eval('() => document.title')}")
        p.input('imeoff')
        record({'case': 'start', 'step': PAGE, 'page': PAGE, 'channel': CHANNEL, 'version': p.version, 'host': HOST, 'calibration': cal, 'keyboard': k})
        for c in cases:
            try:
                c()
            except Exception as e:
                record({'case': c.__name__, 'step': 'failed', 'page': PAGE, 'error': str(e)[:400]})
                home()
        record({'case': 'console', 'step': PAGE, 'page': PAGE, 'messages': p.cmd(op='console')['value']})


PAGE = 'sheet'
if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-d-sheet-{HOST}-{CHANNEL}-pg.jsonl'))
    try:
        for name in sys.argv[3:] or ['run']:
            globals()[name]()
    finally:
        p.stop()
