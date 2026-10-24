"""The client of xl-server.ps1 (copied unchanged from the 2026-10-14 readings): one command written to its
stdin file, its one answer read back, every exchange appended to records/keys-xs.jsonl.

    python xs.py "<command>" ["<command>" ...]

Run from Git Bash on the Windows desktop itself, not from WSL as the earlier runs were.
"""
import json
import os
import sys
import time

D = r'C:\Users\amisa\AppData\Local\exgrid-layer3\workbook-12\xs'
LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'records', 'keys-xs.jsonl')


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
            with open(LOG, 'a', encoding='utf-8') as f:
                f.write(json.dumps({'t': time.strftime('%H:%M:%S'), 'cmd': line, 'answer': a}, ensure_ascii=False) + '\n')
            return a
        time.sleep(0.05)
    raise TimeoutError(line)


if __name__ == '__main__':
    for c in sys.argv[1:]:
        if c.startswith('sleep '):
            time.sleep(float(c[6:]))
            continue
        print(cmd(c))
