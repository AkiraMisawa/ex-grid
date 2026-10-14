#!/usr/bin/env python3
"""Apply an apply list: '### N. <path>' then 'Old:' and 'New:' fenced blocks, in order.

Usage: apply_list.py <list.md> [--check] [--root DIR]
Each old text must occur exactly once in its file when its edit is applied; the run stops at the first
edit that does not. With --check nothing is written.
"""
import re
import sys
from pathlib import Path

args = sys.argv[1:]
check = '--check' in args
root = Path('.')
if '--root' in args:
    root = Path(args[args.index('--root') + 1])
lst = Path([a for a in args if not a.startswith('--') and a != str(root)][0]).read_text()

edit_re = re.compile(r'^### (\d+)\. (\S+)\s*$', re.M)
heads = list(edit_re.finditer(lst))
edits = []
for i, h in enumerate(heads):
    body = lst[h.end(): heads[i + 1].start() if i + 1 < len(heads) else len(lst)]
    blocks = []
    pos = 0
    for label in ('Old:', 'New:'):
        j = body.find('\n' + label, pos)
        if j < 0:
            sys.exit(f'edit {h.group(1)}: no {label}')
        m = re.compile(r'^(`{3,})[^\n]*\n', re.M).search(body, j)
        fence = m.group(1)
        end = body.find('\n' + fence + '\n', m.end() - 1)
        if end < 0:
            end = body.find('\n' + fence, m.end() - 1)
        blocks.append(body[m.end():end + 1])
        pos = end + len(fence) + 1
    edits.append((int(h.group(1)), h.group(2), blocks[0], blocks[1]))

files = {}
for n, path, old, new in edits:
    p = root / path
    text = files.get(path)
    if text is None:
        text = p.read_text() if p.exists() else ''
    count = text.count(old)
    if count != 1:
        sys.exit(f'edit {n} ({path}): old text occurs {count} times')
    files[path] = text.replace(old, new, 1)
    print(f'edit {n}: {path} ok')

if not check:
    for path, text in files.items():
        (root / path).write_text(text)
print(f'{len(edits)} edits {"check" if check else "applied"}')
