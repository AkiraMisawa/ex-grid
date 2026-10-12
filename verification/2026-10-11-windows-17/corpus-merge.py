"""Part A: the corpus as -Update left it, carried onto a newer corpus that a merge brought in.

The run's -Update passes rewrote `source` to "observed" in a copy of the corpus on Windows (the copy the
oracle ran over). The requester's revisions (merged at e746b090 and ca486e45) changed three corpus files
meanwhile. This takes the newer files as they are and applies to them each promotion the run made, with
the oracle's own pattern (`"id": "<id>",` then `"source": "documented|uncertain"`), but only for a case
whose content in the newer file is the same as at the verified commit apart from `source`. A case the
revision reshaped or added keeps the newer file's source, and is asked again.

    python3 corpus-merge.py <base dir> <run's copy dir> <newer dir> <out dir>
"""
import json
import os
import re
import sys

base_dir, ours_dir, theirs_dir, out_dir = sys.argv[1:5]
os.makedirs(out_dir, exist_ok=True)
report = {'promoted': [], 'kept': [], 'files': 0}
for name in sorted(os.listdir(theirs_dir)):
    if not name.endswith('.json'):
        continue
    report['files'] += 1
    theirs_text = open(os.path.join(theirs_dir, name), encoding='utf-8').read()
    if 'cases' not in json.loads(theirs_text):
        # A file of fixtures, not of cases: taken as the newer corpus has it.
        with open(os.path.join(out_dir, name), 'w', encoding='utf-8', newline='') as f:
            f.write(theirs_text)
        continue
    theirs = {c['id']: c for c in json.loads(theirs_text)['cases']}
    base_path, ours_path = os.path.join(base_dir, name), os.path.join(ours_dir, name)
    if os.path.exists(base_path) and os.path.exists(ours_path):
        base = {c['id']: c for c in json.load(open(base_path, encoding='utf-8'))['cases']}
        ours = {c['id']: c for c in json.load(open(ours_path, encoding='utf-8'))['cases']}
        for i, o in ours.items():
            b = base.get(i)
            if o['source'] != 'observed' or b is None or b['source'] == 'observed':
                continue
            t = theirs.get(i)
            same = t is not None and {**t, 'source': b['source']} == b
            if not same:
                report['kept'].append(f'{name}:{i}')
                continue
            pattern = '("id": "' + re.escape(i) + r'",\s*"source": )"(documented|uncertain)"'
            theirs_text, n = re.subn(pattern, r'\1"observed"', theirs_text)
            if n != 1:
                raise SystemExit(f'{name}:{i}: the pattern matched {n} times')
            report['promoted'].append(f'{name}:{i}')
    with open(os.path.join(out_dir, name), 'w', encoding='utf-8', newline='') as f:
        f.write(theirs_text)
print(json.dumps({'files': report['files'], 'promoted': len(report['promoted']), 'kept': report['kept']}, indent=1))
