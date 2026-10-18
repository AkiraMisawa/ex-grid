"""verify-on-windows-18.md, Part A, SH-52, "Keys at machine speed" (from toolbar.md, item 2; ADR-0100, ADR-0021's
seventh entry as widened on 2026-10-13): on /sheet with the toolbar shown, each sequence typed at once (one SendInput
call: input-server.ps1's burst, or its raw for the chord) and, beside it, typed slowly (250 ms between keys), each from
a fresh page with D10 pressed. After each: whether the browser's menu stayed closed (the window in front, the browser's
own top-level windows, whether the document kept focus), the Sheet's KeyTips, where the keyboard is, the edit, the
Focus, D10's and D11's look, and every keydown and keyup the page heard.

    python3 sh52_speed.py <chrome|msedge> <wasm|server-80>
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
CONTROL = 'http://localhost:7317'
RTT = 80 if HOST == 'server-80' else None
SETTLE = 1.5 if RTT else 0.8
REC = os.path.join(HERE, 'records', f'sh52-speed-{HOST}-{CHANNEL}.jsonl')
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
  const tips = [...document.querySelectorAll('.ex-sheet-keytip')].map(k => k.textContent);
  const look = (r, c) => { const d = [...g.querySelectorAll(`[id$="-r${r}c${c}"]`)].find(e => e.closest('.ex-viewport')); const t = d?.querySelector('*') ?? d; const cs = t ? getComputedStyle(t) : null;
    return cs ? { text: d.textContent, weight: cs.fontWeight } : null; };
  return { keyboard: where(ae), hasFocus: document.hasFocus(), editing: !!editor, editorValue: editor?.value ?? null,
    focus: id?.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null,
    toolbar: !!tb, pressed: tb ? [...tb.querySelectorAll('[aria-pressed=true]')].map(e => e.getAttribute('aria-label') || e.textContent.trim()).filter(Boolean) : null,
    keytips: tips, lists: [...document.querySelectorAll('[role=dialog],[role=listbox],[role=menu]')].filter(d => d.offsetParent !== null).map(d => (d.getAttribute('aria-label') || d.textContent).trim().slice(0, 40)),
    d10: look(9, 3), d11: look(10, 3) };
}"""

# Each sequence: (name, slow keys for `type 250`, how it is sent at once). D10 holds 7 before each (typed, Enter, Up).
CASES = [
    ('alt-alone', '{ALT}', ('burst', '{ALT}')),
    ('enter-then-alt-h-1', '{ENTER}{ALT}h1', ('burst', '{ENTER}{ALT}h1')),
    ('alt-alt-h-1', '{ALT}{ALT}h1', ('burst', '{ALT}{ALT}h1')),
    # Alt down, Down down, Alt up, Down up: the Alt released before the Down.
    ('alt-down-alt-released-first', None, ('raw', '12:d 28:d:e 12:u 28:u:e')),
]


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    s = row.get('state') or {}
    print(row.get('case'), row.get('speed'), 'tips', s.get('keytips'), 'kbd', s.get('keyboard'), 'focus', s.get('focus'),
          'edit', s.get('editing'), s.get('editorValue'), 'd10', s.get('d10'), 'd11', s.get('d11'), 'hasFocus', s.get('hasFocus'),
          'front', (row.get('front') or '')[:60], 'browserWindows', len(row.get('browserWindows') or []))


def browser_windows():
    w = p.input('windows')
    name = 'chrome' if CHANNEL == 'chrome' else 'msedge'
    return {'front': p.input('foreground'), 'browserWindows': [x for x in w[len('ok windows '):].split(' ;; ') if f'|{name}|' in x][:12]}


def fresh():
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    p.cmd(op='front')
    if RTT is not None:
        p.cmd(op='rtt', control=CONTROL, ms=0)
    p.cmd(op='press', sel='.ex-grid:has(> .ex-formula-bar) [id$="-r9c3"]')
    time.sleep(SETTLE)
    p.keys('7{ENTER}{UP}', gap=150)
    time.sleep(SETTLE)
    p.cmd(op='events')
    if RTT is not None:
        p.cmd(op='rtt', control=CONTROL, ms=RTT)


def run():
    global p
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    cal = p.cmd(op='calibrate')
    k = p.input(f"english {p.eval('() => document.title')}")
    p.input('imeoff')
    record({'case': 'start', 'channel': CHANNEL, 'version': p.version, 'host': HOST, 'rtt': RTT, 'calibration': cal, 'keyboard': k})
    for name, slow, (how, keys) in CASES:
        for speed in ('slow', 'at-once'):
            if speed == 'slow' and slow is None:
                continue
            try:
                fresh()
                if name == 'enter-then-alt-h-1':
                    p.keys('{F2}', gap=0)
                    time.sleep(SETTLE)
                    p.cmd(op='events')
                sent = p.keys(slow, gap=250) if speed == 'slow' else p.input(f'{how} {keys}')
                time.sleep(SETTLE + 1.0)
                row = {'case': name, 'speed': speed, 'sent': sent, 'state': p.eval(STATE), **browser_windows()}
                row['events'] = [e for e in p.cmd(op='events')['value'] if e.get('type') in ('keydown', 'keyup', 'focusin', 'focusout')]
                row['shot'] = f'sh52-{name}-{speed}-{HOST}-{CHANNEL}.png'
                p.cmd(op='shot', file=f'{pg.WIN_PATH}\\shots-sh52\\{row["shot"]}')
                record(row)
            except Exception as e:
                record({'case': name, 'speed': speed, 'error': str(e)[:400]})
    if RTT is not None:
        p.cmd(op='rtt', control=CONTROL, ms=0)
    record({'case': 'console', 'messages': p.cmd(op='console')['value']})


if __name__ == '__main__':
    os.makedirs(os.path.join(pg.WIN, 'shots-sh52'), exist_ok=True)
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'sh52-speed-{HOST}-{CHANNEL}-pg.jsonl'))
    try:
        run()
    finally:
        p.stop()
