"""widening.md questions 1 to 11 (run 18a): which gestures widen a column over ####, by R6's procedure (the 2026-10-12
readings, r6.py), under the regional format en-US.

Each case in a new workbook of a new Excel of xl-server.ps1's own: A1 holds 123456789.12 in 0.00 (the format first,
on the empty cell, then the number, both through COM), column A at the sheet's standard width, so A1 shows ########.
Then the gesture, with the real pointer and keys (SendInput), or through COM where the case says so. After the gesture,
and after every step that sets something through COM, read through COM: A1's Text; column A's ColumnWidth and
UseStandardWidth; A1's Font.Bold, Italic, Underline, Strikethrough and Color; and a picture of A1:C3. Where a COM step
before the gesture widened the column, UseStandardWidth is set back to True through COM and ######## checked.

Format Cells' controls are pressed where its pictures show them, relative to the dialog's window (w-dlg-*-c.png, this
machine at 150%): its UI Automation tree gave no controls this run.

    python3 widen.py [case ...]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xs  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REC = os.path.join(HERE, '..', 'records', 'widen.jsonl')
xs.log_to(os.path.join(HERE, '..', 'records', 'widen-xs.jsonl'))

TAB = {'Number': (63, 75), 'Alignment': (154, 75), 'Font': (245, 75), 'Border': (325, 75)}
STRIKE = (58, 408)
NORMAL_FONT = (603, 341)
BOLD = (421, 218)          # the Font style list's Bold
HORIZONTAL = (150, 180)
OUTLINE = (430, 175)
OK = (569, 739)
FONT_COLOUR_FACE, FONT_COLOUR_ARROW = (571, 201), (600, 201)

READ = ('$r = $ws.Range("A1"); $c = $ws.Range("C1"); $f = $r.Font; '
        '"text=" + $r.Text + " width=" + $r.ColumnWidth + " useStandard=" + $r.EntireColumn.UseStandardWidth + '
        '" bold=" + $f.Bold + " italic=" + $f.Italic + " underline=" + $f.Underline + " strike=" + $f.Strikethrough + '
        '" color=" + $f.Color + " hAlign=" + $r.HorizontalAlignment + '
        '" C1text=" + $c.Text + " C1width=" + $c.ColumnWidth + " C1useStandard=" + $c.EntireColumn.UseStandardWidth + " C1bold=" + $c.Font.Bold + '
        '" borders=" + (@(7,8,9,10) | % { $r.Borders.Item($_).LineStyle }) -join ","')


def record(row):
    row['t'] = time.strftime('%H:%M:%S')
    with open(REC, 'a', encoding='utf-8') as f:
        f.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(json.dumps(row, ensure_ascii=False)[:700], flush=True)


def state(case, step):
    time.sleep(0.8)
    xs.cmd('move 3700 1900'); time.sleep(0.3)
    r = xs.ready()
    name = f'w-{case}-{step}'
    row = {'reading': 'widen', 'case': case, 'step': step, 'ready': r}
    if r.startswith('ready='):
        row['read'] = xs.cmd('ps ' + READ)[len('ok ps '):].rsplit(' t=', 1)[0]
        xs.cmd(f'shot {name}')
        g = xs.geo('A1', 'C3')
        xs.cmd(f"crop {name} {g['A1'][0] - 40} {g['A1'][1] - 30} {g['C3'][2] - g['A1'][0] + 50} {g['C3'][3] - g['A1'][1] + 40} {name}-cells 2")
        row['shot'] = f'{name}-cells.png'
    record(row)
    return row


def reset_if_widened(case, row):
    if 'useStandard=False' in row.get('read', ''):
        xs.cmd('ps $ws.Columns(1).UseStandardWidth = $true; $ws.Columns(3).UseStandardWidth = $true')
        state(case, 'reset-to-standard')


def fresh(case, also_c1=False):
    try:
        xs.cmd('stop')
    except Exception:
        pass
    a = xs.cmd('start'); time.sleep(1.5)
    xs.cmd('setfmt A1 0.00'); xs.cmd('setup A1 123456789.12')
    if also_c1:
        xs.cmd('setfmt C1 0.00'); xs.cmd('setup C1 123456789.12')
    xs.cmd('select A1')
    record({'reading': 'widen', 'case': case, 'step': 'start', 'excel': a, 'culture': xs.cmd('ps (Get-Culture).Name + " dateOrder=" + $xl.International(32)')})
    return state(case, '0-hashes')


def dialog():
    xs.keys('^1'); time.sleep(1.6)
    w = [x for x in xs.cmd('windows')[len('ok windows '):].rsplit(' t=', 1)[0].split(' ;; ') if '|Format Cells|' in x]
    if not w:
        raise RuntimeError('Format Cells did not open')
    l, t = (int(v) for v in w[0].split('|')[3].split(',')[:2])
    return l, t


def press(origin, rel, wait=0.7):
    xs.cmd(f'click {origin[0] + rel[0]} {origin[1] + rel[1]}'); time.sleep(wait)


def through_dialog(case, tab, *then):
    o = dialog()
    press(o, TAB[tab], wait=1.2)
    for t in then:
        if isinstance(t, tuple):
            press(o, t)
        else:
            xs.keys(t, gap=150); time.sleep(0.5)
    xs.cmd(f'shot w-{case}-1-before-ok')
    xs.cmd(f'crop w-{case}-1-before-ok {o[0]} {o[1]} 774 780 w-{case}-1-before-ok-dialog 1')
    press(o, OK, wait=1.0)
    return state(case, '2-after-ok')


def com_then(case, setter, *after):
    fresh(case)
    xs.cmd('ps ' + setter)
    row = state(case, '1-' + setter.split('=')[0].strip().split('.')[-1].lower())
    reset_if_widened(case, row)
    for n, a in enumerate(after, 2):
        xs.keys(a); state(case, f'{n}-keys-{a}')


def q1():
    fresh('q1-font-strike'); through_dialog('q1-font-strike', 'Font', STRIKE)


def q2():
    case = 'q2-normal-font'
    fresh(case)
    xs.cmd('ps $ws.Range("A1").Font.Color = 255')
    reset_if_widened(case, state(case, '1-com-color-255'))
    through_dialog(case, 'Font', NORMAL_FONT)


def q3():
    fresh('q3-font-tab-only'); through_dialog('q3-font-tab-only', 'Font')


def q4():
    fresh('q4-align-center'); through_dialog('q4-align-center', 'Alignment', HORIZONTAL, '{HOME}{DOWN}{DOWN}{ENTER}')


def q5a():
    case = 'q5a-font-colour-face'
    fresh(case)
    record({'reading': 'widen', 'case': case, 'step': 'face', 'uia': xs.cmd('uiaboxes ^Font Colou?r')})
    xs.cmd(f'click {FONT_COLOUR_FACE[0]} {FONT_COLOUR_FACE[1]}')
    state(case, '1-face-pressed')


def q5b():
    case = 'q5b-font-colour-blue'
    fresh(case)
    xs.cmd(f'click {FONT_COLOUR_ARROW[0]} {FONT_COLOUR_ARROW[1]}'); time.sleep(1.2)
    xs.cmd(f'shot w-{case}-1-palette')
    record({'reading': 'widen', 'case': case, 'step': 'palette', 'uia': xs.cmd('uiaboxes ^Blue$')})


def q6():
    com_then('q6-com-italic', '$ws.Range("A1").Font.Italic = $true')
    com_then('q6-com-strike', '$ws.Range("A1").Font.Strikethrough = $true')
    com_then('q6-com-color', '$ws.Range("A1").Font.Color = 255')


def q7():
    com_then('q7-ctrl-u-off', '$ws.Range("A1").Font.Underline = 2', '^u')
    com_then('q7-ctrl-5-off', '$ws.Range("A1").Font.Strikethrough = $true', '^5')


def q8():
    fresh('q8-ctrl-4'); xs.keys('^4'); state('q8-ctrl-4', '1-ctrl-4')


def q9():
    fresh('q9-ctrl-i-then-u'); xs.keys('^i'); state('q9-ctrl-i-then-u', '1-ctrl-i'); xs.keys('^u'); state('q9-ctrl-i-then-u', '2-ctrl-u')


def q10():
    case = 'q10a-two-ranges'
    fresh(case, also_c1=True)
    a1, c1 = xs.geo('A1', 'C1')['A1'], xs.geo('C1')['C1']
    xs.cmd(f'click {(a1[0] + a1[2]) // 2} {(a1[1] + a1[3]) // 2}'); xs.cmd(f'ctrlclick {(c1[0] + c1[2]) // 2} {(c1[1] + c1[3]) // 2}')
    record({'reading': 'widen', 'case': case, 'step': 'selection', 'ready': xs.ready()})
    through_dialog(case, 'Font', BOLD)
    xs.keys('^z'); state(case, '3-one-undo')
    case = 'q10b-column-a'
    fresh(case)
    xs.cmd('click 89 340')
    record({'reading': 'widen', 'case': case, 'step': 'selection', 'ready': xs.ready()})
    through_dialog(case, 'Font', BOLD)


def q11():
    fresh('q11a-outline-key'); xs.keys('^{&}'); state('q11a-outline-key', '1-ctrl-shift-amp')
    case = 'q11b-no-borders-key'
    fresh(case)
    xs.cmd('ps foreach ($e in 7,8,9,10) { $b = $ws.Range("A1").Borders.Item($e); $b.LineStyle = 1; $b.Weight = 2 }')
    reset_if_widened(case, state(case, '1-com-four-thin-edges'))
    xs.keys('^{_}'); state(case, '2-ctrl-shift-underscore')
    fresh('q11c-border-tab-outline'); through_dialog('q11c-border-tab-outline', 'Border', OUTLINE)


CASES = {'q1': q1, 'q2': q2, 'q3': q3, 'q4': q4, 'q5a': q5a, 'q5b': q5b, 'q6': q6, 'q7': q7, 'q8': q8, 'q9': q9, 'q10': q10, 'q11': q11}

if __name__ == '__main__':
    for c in (sys.argv[1:] or [k for k in CASES if k != 'q5b']):
        CASES[c]()
