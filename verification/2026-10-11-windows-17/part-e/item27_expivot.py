"""Part E, item 27, ExPivot's half: /pivot?words=ja (PivotWords.Japanese, ADR-0060) on the WebAssembly host, Chrome,
read from the DOM: the report's header and label cells, and the Field List's words; and a picture of the page.

    python3 part-e/item27_expivot.py
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import pg  # noqa: E402

OUT = os.path.join(HERE, 'part-e', 'item27')
READY = "() => !!document.querySelector('.ex-pivot .ex-grid [role=gridcell], .ex-pivot [role=gridcell]')"
READ = """() => {
  const t = (e) => (e.innerText ?? e.textContent ?? '').replace(/\\s+/g, ' ').trim();
  const root = document.querySelector('.ex-pivot') ?? document.body;
  const rows = [...root.querySelectorAll('[role=row]')].slice(0, 30).map(r => [...r.querySelectorAll('[role=columnheader],[role=rowheader],[role=gridcell]')].map(t));
  const words = new Set();
  for (const e of root.querySelectorAll('h1,h2,h3,h4,h5,h6,label,button,[role=heading],[aria-label],legend,.ex-pivot-field-list *')) {
    const s = t(e); if (s && s.length < 80) words.add(s);
    const a = e.getAttribute && e.getAttribute('aria-label'); if (a) words.add('aria-label: ' + a);
  }
  return { url: location.href, rows, words: [...words].slice(0, 200) };
}"""

if __name__ == '__main__':
    p = pg.Page.start('chrome', name='pg-chrome', log=os.path.join(OUT, 'item27-expivot-pg.jsonl'))
    try:
        p.cmd(op='goto', url='http://localhost:5299/pivot?words=ja', ready=READY)
        r = p.eval(READ)
        r['version'] = p.version
        win = pg.WIN_PATH + '\\item27\\item27-expivot.png'
        p.cmd(op='shot', file=win)
        r['console'] = p.cmd(op='console')['value']
        json.dump(r, open(os.path.join(OUT, 'item27-expivot.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print(json.dumps({k: v for k, v in r.items() if k != 'words'}, ensure_ascii=False)[:3000])
        print('\n'.join(r['words']))
    finally:
        p.stop()
