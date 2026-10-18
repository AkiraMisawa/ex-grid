"""verify-on-windows-18.md, Part A, ED-30, "The IME and the Alt key" (from toolbar.md, item 3; ED-30 shares the root's
keyup listener with the Alt hold): on /sheet with the toolbar shown, the window on the Japanese keyboard.

  t3a  With the Microsoft IME on and nothing composed, the IME's own toggle: 半角/全角 (VK_OEM_AUTO with the key's scan
       code, 0x29), and Alt+半角/全角. The keyboard here is the Japanese one, which has no Alt+` (the US keyboard's
       toggle), so that is not asked. Alt tapped alone with the IME on is read beside them. After each: the Sheet's
       KeyTips, where the keyboard is, the IME's open status, and every keydown and keyup the page heard.
  t3b  Alt, H, 1 typed at once (one SendInput call) with the IME off, then the IME on and `kana` typed, then Enter twice:
       whether the composition starts in the Focus cell, D10's text after it, and nothing of it lost or doubled. Also
       typed slowly, beside it.

    python3 t3_ime.py <chrome|msedge> <wasm|server>
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402
from sh52_speed import READY, STATE  # noqa: E402  (sh52_speed reads its configuration from argv at import)

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL, HOST = sys.argv[1], sys.argv[2]
BASE = 'http://localhost:5318' if HOST == 'wasm' else 'http://localhost:5317'
SETTLE = 1.0
REC = os.path.join(HERE, 'records', f'ed30-t3-{HOST}-{CHANNEL}.jsonl')
TOGGLES = [
    ('alt-alone', 'raw 12:d 12:u'),
    ('hankaku-zenkaku', 'raw F3:d:s29 F3:u:s29'),
    ('alt-hankaku-zenkaku', 'raw 12:d F3:d:s29 F3:u:s29 12:u'),
]


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    s = row.get('state') or {}
    print(row.get('case'), row.get('step'), 'tips', s.get('keytips'), 'kbd', s.get('keyboard'), 'focus', s.get('focus'),
          'edit', s.get('editing'), s.get('editorValue'), 'd10', s.get('d10'), 'd11', s.get('d11'), 'ime', row.get('ime'),
          'keys', [f"{e['type'][3:]}:{e.get('key')}" for e in row.get('events', []) if e['type'] in ('keydown', 'keyup')][:16],
          'comp', [f"{e['type'][11:]}:{e.get('data')}" for e in row.get('events', []) if e['type'].startswith('composition')][:10])


def title():
    return p.eval('() => document.title')


def read(case, step, sent=None):
    time.sleep(SETTLE)
    row = {'case': case, 'step': step, 'sent': sent, 'state': p.eval(STATE), 'ime': p.input(f'imestate {title()}'),
           'front': p.input('foreground'),
           'events': [e for e in p.cmd(op='events')['value'] if e.get('type') in ('keydown', 'keyup', 'compositionstart', 'compositionupdate', 'compositionend', 'focusin', 'focusout', 'input')]}
    record(row)
    return row


def fresh():
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    p.cmd(op='front')
    p.input(f'japanese {title()}')
    p.input('imeoff')
    p.cmd(op='press', sel='.ex-grid:has(> .ex-formula-bar) [id$="-r9c3"]')
    time.sleep(SETTLE)
    p.cmd(op='events')


def t3a():
    for name, cmd in TOGGLES:
        fresh()
        p.input('imeon')
        read('t3a', f'{name}-0-ime-on')
        sent = p.input(cmd)
        read('t3a', f'{name}-1-after', sent)
        p.cmd(op='shot', file=f'{pg.WIN_PATH}\\shots-t3\\t3a-{name}-{HOST}-{CHANNEL}.png')


def t3b():
    for speed in ('at-once', 'slow'):
        fresh()
        # The IME off before the letters, read back (in a first pass it was still on after fresh(), and h and 1 composed).
        for _ in range(5):
            p.cmd(op='front'); p.input('imeoff'); time.sleep(0.5)
            if 'open=False' in p.input(f'imestate {title()}'):
                break
        p.cmd(op='events')
        sent = p.input('burst {ALT}h1') if speed == 'at-once' else p.keys('{ALT}h1', gap=250)
        read('t3b', f'{speed}-1-alt-h-1', sent)
        p.input('imeon')
        sent = p.input('burst kana') if speed == 'at-once' else p.keys('kana', gap=150)
        read('t3b', f'{speed}-2-kana', sent)
        p.cmd(op='shot', file=f'{pg.WIN_PATH}\\shots-t3\\t3b-{speed}-kana-{HOST}-{CHANNEL}.png')
        read('t3b', f'{speed}-3-enter', p.keys('{ENTER}', gap=0))
        read('t3b', f'{speed}-4-enter-again', p.keys('{ENTER}', gap=0))
        p.input('imeoff')


if __name__ == '__main__':
    os.makedirs(os.path.join(pg.WIN, 'shots-t3'), exist_ok=True)
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'ed30-t3-{HOST}-{CHANNEL}-pg.jsonl'))
    try:
        p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
        record({'case': 'start', 'channel': CHANNEL, 'version': p.version, 'host': HOST, 'calibration': p.cmd(op='calibrate')})
        for c in [globals()[n] for n in (sys.argv[3:] or ['t3a', 't3b'])]:
            try:
                c()
            except Exception as e:
                record({'case': c.__name__, 'step': 'failed', 'error': str(e)[:400]})
        record({'case': 'console', 'messages': p.cmd(op='console')['value']})
        p.input('imeoff')
    finally:
        p.stop()
