"""verify-on-windows-17.md, Part G, VZ-16: the browser's zoom by real keys on a display-only page (/wide, never
/sheet, whose Sheet claims Ctrl+- and Ctrl+= for its structure keys), devicePixelRatio and the --ex-dp the
grid's root carries after each change; then a real 2.25 (the display's 150% and the browser's 150%) on
/sheet?case=11, a thin line and a gridline read in device pixels from the compositor's picture.

    python3 part_g_vz16.py <chrome|msedge>
"""
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pg  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL = sys.argv[1]
BASE = 'http://localhost:5318'
REC = os.path.join(HERE, 'records', f'part-g-vz16-{CHANNEL}.jsonl')
SHOTS = f'{pg.WIN_PATH}\\shots-g'
os.makedirs(os.path.join(pg.WIN, 'shots-g'), exist_ok=True)
READ = """() => { const g = document.querySelector('.ex-grid'); const s = g?.getAttribute('style') ?? '';
  const m = /--ex-dp:\\s*([0-9.]+)/.exec(s);
  return { dpr: devicePixelRatio, exDp: m ? Number(m[1]) : null, exDpRaw: m ? m[0] : null, inner: [innerWidth, innerHeight], outer: [outerWidth, outerHeight],
    keyboard: document.activeElement?.className?.split?.(' ')[0] ?? document.activeElement?.tagName }; }"""


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:400], flush=True)


def zoom(name, keys):
    p.cmd(op='front')
    p.keys(keys, gap=200)
    time.sleep(1.5)
    record({'step': name, 'keys': keys, 'read': p.eval(READ)})


def run_across(at, along, axis, name, zoom=1.0, scroll=(0, 0)):
    """Device pixels across an edge at `at` CSS px, along `along`: the clip's origin on a multiple of 4 CSS
    px (9 Device Pixels at 2.25), as device-pixel-225.spec.mjs reads it.

    The compositor's clip is in the window's own units, before the browser's zoom: a CSS px is `zoom` of
    them (devicePixelRatio over the display's 1.5). The first pass gave the clip in CSS px and read a place
    1/1.5 as far from the origin (records/part-g-first-try/)."""
    start = (int(at) // 4) * 4 - 8
    line = (int(along) // 4) * 4
    css = {'x': start, 'y': line, 'width': 16, 'height': 4} if axis == 'x' else {'x': line, 'y': start, 'width': 4, 'height': 16}
    # The compositor's clip is also the document's, not the viewport's: the page's scroll is added (the
    # third pass left it out and read white, records/part-g-first-try/).
    clip = {'x': (css['x'] + scroll[0]) * zoom, 'y': (css['y'] + scroll[1]) * zoom, 'width': css['width'] * zoom, 'height': css['height'] * zoom}
    px = p.cmd(op='pixels', clip=clip, file=f'{SHOTS}\\{name}-{CHANNEL}.png')
    rows = px['rows']
    if axis == 'x':
        mid = rows[len(rows) // 2]
        seq = mid
    else:
        seq = [r[len(r) // 2] for r in rows]
    nonwhite = [(i, c) for i, c in enumerate(seq) if max(abs(v - 255) for v in c) > 2]
    return {'cssClip': css, 'clip': clip, 'image': [px['width'], px['height']], 'across': seq, 'notWhite': nonwhite, 'count': len(nonwhite)}


def cell_box(a):
    m = re.match(r'([A-Z]+)(\d+)', a)
    col = 0
    for ch in m.group(1):
        col = col * 26 + ord(ch) - 64
    return p.eval("""(id) => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const c = [...g.querySelectorAll(`[id$="${id}"]`)].find(e => e.closest('.ex-viewport')); const b = c.getBoundingClientRect();
        return { x: b.left, y: b.top, width: b.width, height: b.height }; }""", f'-r{int(m.group(2)) - 1}c{col - 1}')


def run():
    p.cmd(op='goto', url=f'{BASE}/wide', ready="() => !!document.querySelector('.ex-grid .ex-row')")
    p.cmd(op='calibrate')
    p.input(f"english {p.eval('() => document.title')}")
    # The keyboard on the page, outside any Sheet: /wide holds one display-only grid. Its root takes a
    # press; the zoom keys are the browser's.
    record({'step': 'start', 'version': p.version, 'read': p.eval(READ)})
    zoom('ctrl-0', '^{KEY0}')
    for i in range(1, 6):
        zoom(f'ctrl-plus-{i}', '^{OEMPLUS}')
    zoom('ctrl-0-again', '^{KEY0}')
    for i in range(1, 5):
        zoom(f'ctrl-minus-{i}', '^{OEMMINUS}')
    zoom('ctrl-0-third', '^{KEY0}')
    # A real 2.25: the browser at 150% on the display's 150%, then /sheet?case=11 on the same origin.
    for i in range(1, 4):
        zoom(f'to-150-{i}', '^{OEMPLUS}')
    p.cmd(op='goto', url=f'{BASE}/sheet?case=11', ready="() => !!document.querySelector('.ex-grid:has(> .ex-formula-bar) .ex-row') && /--ex-dp/.test(document.querySelector('.ex-grid:has(> .ex-formula-bar)')?.getAttribute('style') ?? '')")
    time.sleep(1.5)
    # E8 lies below the fold at 2.25 (its top at 703 CSS px, the viewport 637 high), and the compositor's
    # picture is white there (the second pass, records/part-g-first-try/). The page is scrolled by a whole
    # multiple of 4 CSS px, 9 Device Pixels, so every edge keeps its place within a Device Pixel.
    scrolled = p.eval("""() => { const c = [...document.querySelectorAll('.ex-grid:has(> .ex-formula-bar) [id$="-r7c4"]')].find(e => e.closest('.ex-viewport'));
        const y = Math.max(0, Math.floor((c.getBoundingClientRect().top + scrollY - 300) / 4) * 4); scrollTo(0, y); return { asked: y, scrollY }; }""")
    time.sleep(1.0)
    p.input('move 3700 300')
    time.sleep(0.5)
    read = p.eval("""() => { const g = document.querySelector('.ex-grid:has(> .ex-formula-bar)'); const s = g.getAttribute('style') ?? '';
        const m = /--ex-dp:\\s*([0-9.]+)/.exec(s); return { dpr: devicePixelRatio, exDp: m ? Number(m[1]) : null }; }""")
    b2, e8 = cell_box('B2'), cell_box('E8')
    z = read['dpr'] / 1.5
    sc = tuple(p.eval('() => [scrollX, scrollY]'))
    record({'step': '2.25 on /sheet?case=11', 'read': read, 'scrolled': scrolled, 'B2': b2, 'E8': e8,
            'thin B2 right (B2|C2)': run_across(b2['x'] + b2['width'], b2['y'] + b2['height'] / 2, 'x', 'thin-b2-right', z, sc),
            'thin B2 bottom (B2/B3)': run_across(b2['y'] + b2['height'], b2['x'] + b2['width'] / 2, 'y', 'thin-b2-bottom', z, sc),
            'gridline E8 right': run_across(e8['x'] + e8['width'], e8['y'] + e8['height'] / 2, 'x', 'grid-e8-right', z, sc),
            'gridline E8 bottom': run_across(e8['y'] + e8['height'], e8['x'] + e8['width'] / 2, 'y', 'grid-e8-bottom', z, sc)})
    p.cmd(op='cdpshot', file=f'{SHOTS}\\case-11-at-225-{CHANNEL}.png')
    # Back to 100% for the origin, whatever the profile keeps.
    p.cmd(op='goto', url=f'{BASE}/wide', ready="() => !!document.querySelector('.ex-grid .ex-row')")
    zoom('ctrl-0-end', '^{KEY0}')
    record({'step': 'console', 'messages': p.cmd(op='console')['value']})


if __name__ == '__main__':
    p = pg.Page.start(CHANNEL, name=f'pg-{CHANNEL}', log=os.path.join(HERE, 'records', f'part-g-vz16-{CHANNEL}-pg.jsonl'))
    try:
        run()
    finally:
        p.stop()
