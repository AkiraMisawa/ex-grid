"""verify-on-windows-17.md, Part D, two additions (Chrome, WebAssembly): what the toolbar's Comma Style does to
D10 holding 1234.5 when pressed with the real pointer (K, its KeyTip, left the text as it was), and, under the
MudBlazor Chrome, whether the lists FC, H, B and N open are closed after Escape (their elements stayed in the
DOM): the open popovers counted and the page's own picture of the toolbar's area.

    python3 part_d_extra.py
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402
sys.argv = [sys.argv[0], 'chrome', 'wasm']   # part_d_sheet reads its configuration at import
from part_d_sheet import READY, STATE, SHEET  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, 'records', 'part-d-extra-wasm-chrome.jsonl')
BASE = 'http://localhost:5318'


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:600], flush=True)


D10 = f'{SHEET} [id$="-r9c3"]'
POPOVERS = """() => ({ open: [...document.querySelectorAll('.mud-popover-open')].map(e => e.textContent.trim().slice(0, 40)),
  all: document.querySelectorAll('.mud-popover').length,
  visibleMenus: [...document.querySelectorAll('[role=menu],[role=listbox]')].filter(e => { const r = e.getBoundingClientRect(); const cs = getComputedStyle(e); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.opacity !== '0'; }).map(e => e.textContent.trim().slice(0, 30)) })"""

p = pg.Page.start('chrome', name='pg-chrome', log=os.path.join(HERE, 'records', 'part-d-extra-wasm-chrome-pg.jsonl'))
try:
    p.cmd(op='goto', url=f'{BASE}/sheet', ready=READY)
    p.cmd(op='calibrate'); p.input(f"english {p.eval('() => document.title')}")
    p.cmd(op='press', sel=D10); time.sleep(0.6)
    p.keys('1234.5{ENTER}{UP}'); time.sleep(0.6)
    before = p.eval(STATE)['d10']
    names = p.eval("() => [...document.querySelectorAll('.ex-sheet-toolbar [aria-label]')].map(e => e.getAttribute('aria-label'))")
    p.cmd(op='press', sel='.ex-sheet-toolbar [aria-label="Comma Style"]'); time.sleep(0.8)
    after = p.eval(STATE)
    record({'case': 'comma-style-pressed', 'toolbarLabels': names, 'before': before, 'after': after['d10'], 'keyboard': after['keyboard'],
            'bar': p.eval(f"() => document.querySelector('{SHEET} input.ex-formula-bar-text, {SHEET} .ex-formula-bar-text input')?.value")})
    p.cmd(op='shot', file=f'{pg.WIN_PATH}\\shots-d\\extra-comma-style.png', sel=SHEET)
    p.keys('^{KEYZ}'); time.sleep(0.6); p.keys('{DEL}'); time.sleep(0.6)

    p.cmd(op='goto', url=f'{BASE}/sheet?chrome=mud', ready=READY)
    p.cmd(op='calibrate')
    p.cmd(op='press', sel=D10); time.sleep(0.6)
    for letters in ('fc', 'h', 'b', 'n'):
        p.keys('{ALT}h' + letters, gap=120); time.sleep(0.9)
        opened = p.eval(POPOVERS)
        p.keys('{ESC}'); time.sleep(0.6)
        one = p.eval(POPOVERS)
        p.keys('{ESC}'); time.sleep(0.6)
        two = p.eval(POPOVERS)
        f = f'{pg.WIN_PATH}\\shots-d\\extra-mud-{letters}-after-escape.png'
        p.cmd(op='shot', file=f, clip={'x': 0, 'y': 0, 'width': 1500, 'height': 700})
        record({'case': 'mud-list-escape', 'letters': letters.upper(), 'opened': opened, 'afterOneEscape': one, 'afterTwoEscapes': two,
                'state': p.eval(STATE)['keyboard'], 'shot': os.path.basename(f.replace('\\', '/'))})
    record({'case': 'console', 'messages': p.cmd(op='console')['value']})
finally:
    p.stop()
