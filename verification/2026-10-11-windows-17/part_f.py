"""verify-on-windows-17.md, Part F: where the keyboard is after Down, under NVDA, under Narrator and without a
screen reader (the Definition of Done's section 21.11).

/sheet, WebAssembly, under Chrome and then Edge. Two starting states: without a Focus (a fresh page, focus
given by script to the Revalue button, then a real Tab, which lands on the Sheet Toolbar, and a second, which reaches the
grid), and with
a Focus (a fresh page, a real press on D10). Then Down, Right, Down, Down, each a real key (SendInput). After
every step the probe reads document.activeElement (the Keyboard Field input.ex-key-field, the root, the Cell
Editor, or body), its aria-activedescendant, the Focus cell, and whether the document has focus; under NVDA,
what its Speech Viewer gained since the step before (its own edit control's text, read with WM_GETTEXT).

    python3 part_f.py <chrome|msedge> <none|nvda|narrator> [stepwise|paced] [reversed]

With "stepwise" (Narrator: the user listens), each step waits for a line on stdin before the next, and
records what the user heard, typed back by the session. With "paced" (Narrator, recorded: loopback.ps1), each step waits
6 s for the reader to finish speaking, and the record holds the step's time ("at", Unix seconds) so the recording can be
cut by step (part_f_heard.py); nothing else touches the desktop while it runs. Before a stepwise step, a browser that is no longer in front (the
user answered in a terminal) is brought back, 3 s before the step's key, and the record says so ("refronted").
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
STEPWISE = 'stepwise' in sys.argv[3:]
PACED = 'paced' in sys.argv[3:]
# "scanoff" (Narrator): after each fresh page, Narrator's scan mode turned off with its own key, Insert+Space, before
# the steps; the record's name says so.
SCANOFF = 'scanoff' in sys.argv[3:]
# "reversed": the with-a-Focus steps first, then the without-a-Focus ones, to see whether what a reader says depends on
# which comes first after the browser starts.
REVERSED = 'reversed' in sys.argv[3:]
BASE = 'http://localhost:5299'
SUFFIX = ('-reversed' if REVERSED else '') + ('-scan-off' if SCANOFF else '')
REC = os.path.join(HERE, 'records', f'part-f-{READER}-{CHANNEL}{SUFFIX}.jsonl')
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
    print(json.dumps(row, ensure_ascii=False), flush=True)


def speech():
    if READER != 'nvda':
        return None
    r = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', f'{pg.WIN_PATH}\\nvda-ctl.ps1', 'speech'],
                       capture_output=True, text=True, encoding='utf-8', errors='replace', cwd='/mnt/c', stdin=subprocess.DEVNULL)
    try:
        return json.loads(r.stdout.strip().splitlines()[-1])
    except Exception:
        return f'not read: {r.stdout[-200:]} {r.stderr[-200:]}'


SPOKEN = ['']
FRESH_AT = [None]
SCAN = [None]


def step(start, name, do):
    refronted = None
    if STEPWISE:
        # The user answers each step in a terminal on the same desktop, which takes the foreground; the browser is
        # brought back first, and Narrator given time to read the window, so the key reaches the page.
        title = p.eval('() => document.title')
        if title not in p.input('foreground'):
            try:
                refronted = p.cmd(op='raise', timeout=60)['ok']
            except Exception as e:
                # Windows refused: the user brings the browser back (Alt+Tab), and the step waits for that.
                print(json.dumps({'bring-back': f'{start} {name}', 'why': str(e)[:200]}), flush=True)
                t0 = time.time()
                while title not in p.input('foreground'):
                    if time.time() - t0 > 600:
                        raise TimeoutError('the browser was not brought back')
                    time.sleep(0.5)
                refronted = f'by the user after {time.time() - t0:.0f} s ({str(e)[:120]})'
            time.sleep(3)
    at = time.time()
    do()
    time.sleep(6 if PACED else 1.2 if READER != 'none' else 0.7)
    s = p.eval(STATE)
    row = {'reader': READER, 'channel': CHANNEL, 'start': start, 'step': name, 'at': round(at, 3), 'state': s, 'refronted': refronted,
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
    FRESH_AT[0] = round(time.time(), 3)
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    p.cmd(op='calibrate')
    p.input(f"english {p.eval('() => document.title')}")
    p.input('imeoff')
    if PACED:
        time.sleep(6)   # what the reader says as the page loads, before the first step
    if SCANOFF:
        SCAN[0] = {'at': round(time.time(), 3), 'answer': p.input('narratorkeys 300 SPACE')}
        time.sleep(4)


def run():
    if READER == 'nvda':
        sp = speech(); SPOKEN[0] = sp if isinstance(sp, str) else ''
    if REVERSED:
        with_focus(); without_focus()
    else:
        without_focus(); with_focus()
    record({'reader': READER, 'channel': CHANNEL, 'start': 'console', 'messages': p.cmd(op='console')['value']})


def without_focus():
    # Without a Focus: focus given by script to the Revalue button, the control before the grid, then Tab.
    fresh()
    record({'reader': READER, 'channel': CHANNEL, 'start': 'without-focus', 'step': 'fresh page', 'at': FRESH_AT[0], 'scanOff': SCAN[0], 'state': p.eval(STATE), 'version': p.version})
    step('without-focus', '0-focus-given-by-script-to-revalue', lambda: p.eval("() => { document.querySelector('#sheet-revalue').focus(); return document.activeElement?.id; }"))
    step('without-focus', '1-tab', lambda: p.keys('{TAB}'))
    # The Sheet Toolbar is the tab stop after the Revalue button (ADR-0100): a second Tab reaches the grid.
    step('without-focus', '1b-tab-again', lambda: p.keys('{TAB}'))
    for i, (n, k) in enumerate([('2-down', '{DOWN}'), ('3-right', '{RIGHT}'), ('4-down', '{DOWN}'), ('5-down', '{DOWN}')]):
        step('without-focus', n, lambda k=k: p.keys(k))


def with_focus():
    # With a Focus: a fresh page, a press on D10.
    fresh()
    record({'reader': READER, 'channel': CHANNEL, 'start': 'with-focus', 'step': 'fresh page', 'at': FRESH_AT[0], 'scanOff': SCAN[0], 'state': p.eval(STATE)})
    step('with-focus', '1-press-d10', lambda: p.cmd(op='press', sel=f'{SHEET} [id$="-r9c3"]'))
    for n, k in [('2-down', '{DOWN}'), ('3-right', '{RIGHT}'), ('4-down', '{DOWN}'), ('5-down', '{DOWN}')]:
        step('with-focus', n, lambda k=k: p.keys(k))


if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-f-{READER}-{CHANNEL}{SUFFIX}-pg.jsonl'))
    try:
        run()
    finally:
        p.stop()
