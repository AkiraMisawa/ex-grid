"""The WSL side of xl-server.ps1: one command written to its stdin file, its one answer read back.

xl-server.ps1 runs on Windows, fed by `tail -f cmd.txt`, and appends one answer per command to
out.txt (xs-start.sh starts it). Every exchange is also appended to a log, with the time, so the
record of a Part holds each step and what Excel answered.
"""
import json
import os
import time

D = '/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/readings-2026-10-12/xs'
WIN_SHOTS = 'C:\\Users\\amisa\\AppData\\Local\\exgrid-layer3\\readings-2026-10-12\\xs\\shots'
LOG = None


def log_to(path):
    global LOG
    LOG = path


def _lines():
    with open(os.path.join(D, 'out.txt'), encoding='utf-8', errors='replace') as f:
        return f.read().replace('\r', '').splitlines()


def cmd(line, timeout=120):
    n = len(_lines())
    with open(os.path.join(D, 'cmd.txt'), 'a', encoding='utf-8') as f:
        f.write(line + '\n')
    t0 = time.time()
    while time.time() - t0 < timeout:
        ls = _lines()
        if len(ls) > n:
            a = ls[n]
            if LOG:
                with open(LOG, 'a', encoding='utf-8') as f:
                    f.write(json.dumps({'t': time.strftime('%H:%M:%S'), 'cmd': line, 'answer': a}, ensure_ascii=False) + '\n')
            if a.startswith('err'):
                raise RuntimeError(f'{line}: {a}')
            return a
        time.sleep(0.05)
    raise TimeoutError(line)


def keys(spec, gap=40):
    return cmd(f'keys {gap} {spec}')


def lit(text):
    """Literal text in the server's key notation: + ^ % { } braced."""
    return ''.join('{' + c + '}' if c in '+^%{}~()' else c for c in text)


def cells(*addresses):
    """Each cell's Value2, Formula2 and Text, through COM (Excel must be Ready)."""
    a = cmd('cell ' + ' '.join(addresses))
    out = {}
    for part in a[len('ok cell '):].rsplit(' t=', 1)[0].split(' ;; '):
        name, rest = part.split(': ', 1)
        out[name] = rest
    return out


def geo(*addresses):
    a = cmd('geo ' + ' '.join(addresses))
    out = {}
    for part in a[len('ok geo '):].rsplit(' t=', 1)[0].split(' '):
        name, box = part.split(':')
        out[name] = [int(v) for v in box.split(',')]
    return out


def centre(box):
    return (box[0] + box[2]) // 2, (box[1] + box[3]) // 2


def clipboard():
    a = cmd('clipboard')
    body = a[len('ok clipboard '):].rsplit(' t=', 1)[0]
    if body.startswith('error'):
        return None
    return json.loads(body)


def ready():
    a = cmd('ready')
    return a[len('ok ready '):].rsplit(' t=', 1)[0]
