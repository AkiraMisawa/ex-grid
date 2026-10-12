"""verify-on-windows-17.md, Part F: where the keyboard is after Down, under NVDA, under Narrator and without a
screen reader (the Definition of Done's section 21.11).

/sheet, WebAssembly, under Chrome and then Edge. Two starting states: without a Focus (a fresh page, focus
given by script to the control before the grid, the Revalue button, then a real Tab into the grid), and with
a Focus (a fresh page, a real press on D10). Then Down, Right, Down, Down, each a real key (SendInput). After
every step the probe reads document.activeElement (the Keyboard Field input.ex-key-field, the root, the Cell
Editor, or body), its aria-activedescendant, the Focus cell, and whether the document has focus; under NVDA,
what its Speech Viewer gained since the step before (its own edit control's text, read with WM_GETTEXT).

    python3 part_f.py <chrome|msedge> <none|nvda> [stepwise]

With "stepwise" (Narrator: the user listens), each step waits for a line on stdin before the next, and
records what the user heard, typed back by the session.
"""
import json
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL, READER = sys.argv[1], sys.argv[2]
STEPWISE = len(sys.argv) > 3 and sys.argv[3] == 'stepwise'
BASE = 'http://localhost:5299'
REC = os.path.join(HERE, 'records', f'part-f-{READER}-{CHANNEL}.jsonl')
SHEET = '.ex-grid:has(> .ex-formula-bar)'
READY = """() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)'); if (!g) return false;
  const c = [...g.querySelectorAll('[id$="-r11c1"]')].map(e => e.textContent).join(''); return c.includes('318.25') && !!g.querySelector('input.ex-key-field'); }"""
STATE = """() => {
  const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
  const f = g.querySelector('input.ex-key-field');
  const editor = g.querySelector('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
  const ae = document.activeElement;
  const letters = (c) => { let s = ''; c += 1; while (c > 0) { const m = (c - 1) % 26; s = String.fromCharCode(65 + m) + s; c = Math.floor((c - 1) / 26); } return s; };
  const a1 = (id) => id?.replace(/^.*-r(\\d+)c(\\d+)$/, (x, r, c) => letters(+c) + (+r + 1)) ?? null;
  const role = !ae ? null : ae === f ? 'key-field input.ex-key-field' : ae === g ? 'root' : ae === editor ? 'cell-editor' : ae === document.body ? 'body' : (ae.tagName.toLowerCase() + (ae.id ? '#' + ae.id : '') + (typeof ae.className === 'string' && ae.className ? '.' + ae.className.split(' ')[0] : ''));
  const ad = ae?.getAttribute?.('aria-activedescendant') ?? null;
  return { activeElement: role, ariaActiveDescendant: ad, activeDescendantCell: ad ? a1(ad) : null, activeDescendantExists: ad ? !!document.getElementById(ad) : null,
    focus: a1(f?.getAttribute('aria-activedescendant') ?? g.getAttribute('aria-activedescendant')), hasFocus: document.hasFocus(), editing: !!editor,
    fieldRole: f?.getAttribute('role') ?? null, fieldLabel: f?.getAttribute('aria-label') ?? null };
}"""


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:500], flush=True)


def speech():
    if READER != 'nvda':
        return None
    r = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', f'{pg.WIN_PATH}\\nvda-ctl.ps1', 'speech'],
                       capture_output=True, text=True, cwd='/mnt/c', stdin=subprocess.DEVNULL)
    try:
        return json.loads(r.stdout.strip().splitlines()[-1])
    except Exception:
        return f'not read: {r.stdout[-200:]} {r.stderr[-200:]}'


SPOKEN = ['']


def step(start, name, do):
    do()
    time.sleep(1.2 if READER != 'none' else 0.7)
    s = p.eval(STATE)
    row = {'reader': READER, 'channel': CHANNEL, 'start': start, 'step': name, 'state': s,
           'events': [e for e in p.cmd(op='events')['value'] if e.get('type') in ('keydown', 'focusin', 'focusout')][-10:],
           'front': p.input('foreground')}
    sp = speech()
    if sp is not None:
        row['spokenSince'] = sp[len(SPOKEN[0]):] if sp.startswith(SPOKEN[0]) else sp
        SPOKEN[0] = sp
    if STEPWISE:
        print(json.dumps({'waiting': f'{start} {name}'}), flush=True)
        row['heard'] = sys.stdin.readline().strip()
    record(row)


def fresh():
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    p.cmd(op='calibrate')
    p.input(f"english {p.eval('() => document.title')}")
    p.input('imeoff')


def run():
    if READER == 'nvda':
        sp = speech(); SPOKEN[0] = sp if isinstance(sp, str) else ''
    # Without a Focus: focus given by script to the Revalue button, the control before the grid, then Tab.
    fresh()
    record({'reader': READER, 'channel': CHANNEL, 'start': 'without-focus', 'step': 'fresh page', 'state': p.eval(STATE), 'version': p.version})
    step('without-focus', '0-focus-given-by-script-to-revalue', lambda: p.eval("() => { document.querySelector('#sheet-revalue').focus(); return document.activeElement?.id; }"))
    step('without-focus', '1-tab', lambda: p.keys('{TAB}'))
    for i, (n, k) in enumerate([('2-down', '{DOWN}'), ('3-right', '{RIGHT}'), ('4-down', '{DOWN}'), ('5-down', '{DOWN}')]):
        step('without-focus', n, lambda k=k: p.keys(k))
    # With a Focus: a fresh page, a press on D10.
    fresh()
    record({'reader': READER, 'channel': CHANNEL, 'start': 'with-focus', 'step': 'fresh page', 'state': p.eval(STATE)})
    step('with-focus', '1-press-d10', lambda: p.cmd(op='press', sel=f'{SHEET} [id$="-r9c3"]'))
    for n, k in [('2-down', '{DOWN}'), ('3-right', '{RIGHT}'), ('4-down', '{DOWN}'), ('5-down', '{DOWN}')]:
        step('with-focus', n, lambda k=k: p.keys(k))
    record({'reader': READER, 'channel': CHANNEL, 'start': 'console', 'messages': p.cmd(op='console')['value']})


if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-f-{READER}-{CHANNEL}-pg.jsonl'))
    try:
        run()
    finally:
        p.stop()
