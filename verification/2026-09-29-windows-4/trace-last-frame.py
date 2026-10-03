#!/usr/bin/env python3
"""The last screencast frame a Playwright trace holds, and the failing assertion's message.

    trace-last-frame.py <test-results dir> <out dir>

For every trace.zip under the test-results directory: the frame of the last "screencast-frame"
event in its trace, written to <out dir>/<test folder>.jpeg, and the first lines of the folder's
error-context.md ("Error details"), printed with it.
"""
import json, os, sys, zipfile

root, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
for folder in sorted(os.listdir(root)):
    path = os.path.join(root, folder, 'trace.zip')
    if not os.path.exists(path):
        continue
    with zipfile.ZipFile(path) as z:
        last = None
        for name in z.namelist():
            if not name.endswith('.trace'):
                continue
            for line in z.read(name).decode('utf-8', 'replace').splitlines():
                try:
                    e = json.loads(line)
                except ValueError:
                    continue
                if e.get('type') == 'screencast-frame' and (last is None or e.get('timestamp', 0) >= last.get('timestamp', 0)):
                    last = e
        frame = None
        if last is not None:
            frame = os.path.join(out, folder + '.jpeg')
            with open(frame, 'wb') as f:
                f.write(z.read('resources/' + last['sha1']))
    detail = ''
    ctx = os.path.join(root, folder, 'error-context.md')
    if os.path.exists(ctx):
        text = open(ctx, encoding='utf-8').read()
        i = text.find('# Error details')
        detail = text[i:i + 900] if i >= 0 else ''
    print(f'== {folder}\nlast frame: {frame}\n{detail}\n')
