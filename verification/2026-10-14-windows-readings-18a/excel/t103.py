"""format-cells-readings.md questions 9 and 10 (run 18a, ticket 103): a Custom code typed under one regional format and
read under another, and month names under zh-CN and ko-KR.

The regional format is changed with Set-Culture before a new Excel starts (Excel reads it when it starts), and put back
to en-GB at the end (restore-culture.ps1). Codes are typed into Format Cells' Custom Type box with real keys: Alt+C
(Category), End (Custom), Alt+T (Type), Ctrl+A, the code, Enter (OK). The workbook's styles are read from a copy saved
as .xlsx (savexml); the file reopened under en-US is saved to the scratch folder and never committed.

    python3 t103.py q9 | q10
"""
import json
import os
import re
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 't103.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 't103-xs.jsonl'))
SHOTS = '/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/readings-18a/xs/shots'
FILE = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-18a\\t103.xlsx'


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:900], flush=True)


def set_culture(name):
    out = subprocess.run(['powershell.exe', '-NoProfile', '-Command', f'Set-Culture {name}; Start-Sleep -Milliseconds 500; '
                          'powershell.exe -NoProfile -Command "(Get-Culture).Name"'],
                         capture_output=True, text=True, cwd='/mnt/c', stdin=subprocess.DEVNULL).stdout.strip()
    record({'step': f'set-culture {name}', 'aNewPowerShellReads': out})


def restart():
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start'); time.sleep(1.5)
    record({'step': 'excel', 'start': a, 'international': xs.cmd('ps "dateOrder=" + $xl.International(32) + " decimal=" + $xl.International(3)')})


def numfmt(name):
    xs.cmd(f'savexml {name}')
    s = open(os.path.join(SHOTS, f'{name}-sheet1.xml'), encoding='utf-8').read()
    t = open(os.path.join(SHOTS, f'{name}-styles.xml'), encoding='utf-8').read()
    c = re.search(r'<c r="A1"[^>]*>', s).group(0)
    sid = int(re.search(r' s="(\d+)"', c).group(1)) if ' s="' in c else 0
    xfs = re.findall(r'<xf [^>]*?/?>', re.search(r'<cellXfs.*?</cellXfs>', t, re.S).group(0))
    nf = re.search(r'numFmtId="(\d+)"', xfs[sid]).group(1)
    fmts = re.search(r'<numFmts.*?</numFmts>', t, re.S)
    return {'cell': c, 'xf': xfs[sid], 'numFmtId': int(nf), 'numFmts': fmts.group(0) if fmts else None}


def dialog_shot(name):
    w = [x for x in xs.cmd('windows')[len('ok windows '):].rsplit(' t=', 1)[0].split(' ;; ') if '|Format Cells|' in x]
    xs.cmd(f'shot {name}')
    if w:
        l, t, r, b = (int(v) for v in w[0].split('|')[3].split(','))
        xs.cmd(f'crop {name} {l} {t} {r - l} {b - t} {name}-c 1')
    return w


def custom(code, name):
    xs.cmd('select A1')
    xs.keys('^1'); time.sleep(1.8)
    xs.keys('%c'); time.sleep(0.4); xs.keys('{END}'); time.sleep(0.6)
    xs.keys('%t'); time.sleep(0.3); xs.keys('^a'); time.sleep(0.2)
    xs.keys(xs.lit(code), gap=60); time.sleep(0.6)
    w = dialog_shot(name)
    xs.keys('{ENTER}'); time.sleep(0.8)
    return w


def cell():
    return xs.cells('A1')['A1']


def q9():
    set_culture('en-GB'); restart()
    xs.cmd('setup A1 46027')
    w = custom('d-mmm-yy', 't103-q9-1-en-GB-typed')
    record({'q': 9, 'step': '1 en-GB: d-mmm-yy typed into Custom, OK', 'dialog': w, 'A1': cell(), 'file': numfmt('t103-q9-1-en-GB')})
    xs.cmd(f'ps $wb.SaveAs("{FILE}", 51); $wb.Name')
    set_culture('en-US'); restart()
    xs.cmd(f'ps $script:Book.Close($false); $script:Book = $xl.Workbooks.Open("{FILE}"); $script:Book.Name')
    xs.cmd('select A1')
    record({'q': 9, 'step': '2 en-US: the file opened', 'A1': cell(), 'file': numfmt('t103-q9-2-en-US-opened')})
    xs.keys('^1'); time.sleep(1.8)
    w = dialog_shot('t103-q9-3-en-US-dialog')
    record({'q': 9, 'step': '3 en-US: Ctrl+1 over A1', 'dialog': w, 'shot': 't103-q9-3-en-US-dialog-c.png'})
    xs.keys('%c'); time.sleep(0.4); xs.keys('{END}'); time.sleep(0.8)
    w = dialog_shot('t103-q9-4-en-US-custom')
    xs.keys('{ENTER}'); time.sleep(0.8)
    record({'q': 9, 'step': '4 en-US: Custom chosen, OK with no change', 'dialog': w, 'A1': cell(), 'file': numfmt('t103-q9-4-en-US-after-ok')})
    xs.cmd('ps $script:Book.Close($false); $script:Book = $xl.Workbooks.Add(); $script:Book.Name')


def q10():
    for culture in ('zh-CN', 'ko-KR'):
        set_culture(culture); restart()
        xs.cmd('setup A1 46027')
        for code in ('d-mmm-yy', 'mmm', 'mmmm'):
            w = custom(code, f't103-q10-{culture}-{code}')
            xs.cmd(f'shot t103-q10-{culture}-{code}-cells')
            g = xs.geo('A1', 'C2')
            xs.cmd(f"crop t103-q10-{culture}-{code}-cells {g['A1'][0] - 40} {g['A1'][1] - 30} {g['C2'][2] - g['A1'][0] + 50} {g['C2'][3] - g['A1'][1] + 40} t103-q10-{culture}-{code}-cells-c 2")
            record({'q': 10, 'culture': culture, 'code': code, 'dialog': w, 'A1': cell(),
                    'value2Text': xs.cmd('ps $ws.Range("A1").Text'), 'file': numfmt(f't103-q10-{culture}-{code}')})


if __name__ == '__main__':
    for q in sys.argv[1:]:
        globals()[q]()
