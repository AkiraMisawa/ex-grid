"""Part A's tables for report.md: the corpus counted before and after, what stays unasked and why, and
every disagreement on its own line, read from the results files this run wrote.

    python3 summarise.py <repo> > part-a-summary.md

The case of record for an id is the last pass, in the order below, that asked it in the way the
procedure names for it: the keys pass for an "ask by keys" case or one the procedure adds to it, the pass
under its own culture otherwise. A later pass of the same kind replaces an earlier one (the -Update call
reran each pass, and promoted only what agreed in it).
"""
import collections
import json
import os
import subprocess
import sys

repo = sys.argv[1]
O = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelOracle')
C = os.path.join(repo, 'tests/ExSheet.Engine.Tests/ExcelCases')
P = 'results-2026-10-11-run17'
# (file, culture, how, note) in the order they ran.
PASSES = [
    (f'{P}.json', 'en-US', 'COM', 'the whole corpus'),
    (f'{P}-keys.json', 'en-US', 'keys', 'the 89 "ask by keys"'),
    (f'{P}-de-DE.json', 'de-DE', 'COM', ''),
    (f'{P}-keys-de-DE.json', 'de-DE', 'keys', ''),
    (f'{P}-ja-JP.json', 'ja-JP', 'COM', ''),
    (f'{P}-keys-ja-JP.json', 'ja-JP', 'keys', ''),
    (f'{P}-update-ja-JP.json', 'ja-JP', 'COM', '-Update'),
    (f'{P}-update-de-DE.json', 'de-DE', 'COM', '-Update'),
    (f'{P}-update-en-US.json', 'en-US', 'COM', '-Update, the whole corpus'),
    (f'{P}-update-keys-en-US.json', 'en-US', 'keys', '-Update, the 89'),
    (f'{P}-en-GB.json', 'en-GB', 'COM', 'the 1031 that name en-GB'),
    (f'{P}-keys-en-GB.json', 'en-GB', 'keys (Formulas through Formula2)', 'the 15 en-GB "ask by keys"'),
    (f'{P}-aggregate-new.json', 'en-US', 'COM', 'e746b090\'s 11'),
    (f'{P}-update-aggregate-new.json', 'en-US', 'COM', '-Update, e746b090\'s 11'),
    (f'{P}-keys-en-GB-cases-typed-under-en-US.json', 'en-US', 'keys', 'an addition: the 15 from a copy marked en-US, so their Formulas are typed'),
    (f'{P}-update-en-GB.json', 'en-GB', 'COM', '-Update, the 1031'),
    (f'{P}-update-keys-en-GB.json', 'en-GB', 'keys (Formulas through Formula2)', '-Update, the 15'),
    (f'{P}-update-revision-en-US.json', 'en-US', 'COM', '-Update, ca486e45\'s new and reshaped cases, LVL-027 to 029'),
    (f'{P}-update-keys-errortype-en-US.json', 'en-US', 'keys', '-Update, ERRORTYPE-013, 016 to 019'),
] + [(f'{P}-update-keys-alone-{i}.json', 'en-US', 'keys', f'-Update, {i} alone (it raised a dialog)') for i in ('LET-102', 'LET-104', 'LET-105', 'TYPED-019', 'TYPED-020')] + [
    (f'{P}-update-keys-de-DE.json', 'de-DE', 'keys', '-Update'),
    (f'{P}-update-keys-ja-JP.json', 'ja-JP', 'keys', '-Update'),
]
KEYS_ADDED = {'ERRORTYPE-013', 'ERRORTYPE-016', 'ERRORTYPE-017', 'ERRORTYPE-018', 'ERRORTYPE-019'}


def corpus(at=None):
    out = {}
    names = sorted(os.listdir(C)) if at is None else subprocess.run(['git', '-C', repo, 'ls-tree', '--name-only', at, 'tests/ExSheet.Engine.Tests/ExcelCases/'], capture_output=True, text=True, check=True).stdout.split()
    for n in names:
        n = os.path.basename(n)
        text = open(os.path.join(C, n), encoding='utf-8').read() if at is None else subprocess.run(['git', '-C', repo, 'show', f'{at}:tests/ExSheet.Engine.Tests/ExcelCases/{n}'], capture_output=True, text=True, check=True).stdout
        j = json.loads(text)
        for c in j.get('cases', []):
            out[c['id']] = {**c, '_file': n}
    return out


def counts(cases):
    by_file = collections.defaultdict(collections.Counter)
    for c in cases.values():
        by_file[c['_file']][c['source']] += 1
    total = collections.Counter(c['source'] for c in cases.values())
    culture = collections.Counter(c.get('culture', '(none: en-US)') for c in cases.values())
    return total, culture, by_file


def show(v):
    return json.dumps(v, ensure_ascii=False)


before = corpus('ca486e45')
first = corpus('8436e45e')
after = corpus()
print('## Counts\n')
for label, cs in (('`8436e45e` (the verified commit, step 1)', first), ('`ca486e45` (after the revisions merged)', before), ('after this run', after)):
    t, cu, _ = counts(cs)
    print(f'- {label}: {len(cs)} cases; ' + ', '.join(f'{k} {t[k]}' for k in ('observed', 'documented', 'uncertain')) + '; by culture ' + ', '.join(f'{k} {v}' for k, v in sorted(cu.items())))
print()
_, _, bf0 = counts(first)
_, _, bf1 = counts(after)
print('| File | 8436e45e: observed / documented / uncertain | after: observed / documented / uncertain |')
print('|---|---|---|')
for f in sorted(set(bf0) | set(bf1)):
    a, b = bf0.get(f, collections.Counter()), bf1.get(f, collections.Counter())
    if a == b:
        continue
    print(f'| `{f}` | {a["observed"]} / {a["documented"]} / {a["uncertain"]} | {b["observed"]} / {b["documented"]} / {b["uncertain"]} |')
print('\n(Files whose counts did not change are left out.)\n')

record = {}
runs = []
for f, cult, how, note in PASSES:
    p = os.path.join(O, f)
    j = json.load(open(p, encoding='utf-8-sig'))
    runs.append((f, cult, how, note, j['ran'], j['summary'], j['formulasEnteredThrough']))
    for c in j['cases']:
        if c['status'] == 'blocked' and 'needs ' in (c.get('reason') or '') and 'regional format' in (c.get('reason') or ''):
            continue
        case = after.get(c['id']) or before.get(c['id']) or {}
        by_keys = 'ask by keys' in (case.get('oracleSkip') or '') or c['id'] in KEYS_ADDED
        # Which pass answers a case: for one asked by keys, a pass that typed its Formulas, then one
        # that typed only its constants, then COM; for any other, COM, then keys. A later pass of the
        # same rank replaces an earlier one.
        rank = ({'keys': 3, 'keys (Formulas through Formula2)': 2}.get(how, 1)) if by_keys else (3 if how == 'COM' else 2)
        if c['id'] not in record or rank >= record[c['id']][4]:
            record[c['id']] = (c, how, cult, f, rank)
print('## The passes\n')
print('| File | Regional format | Entered | What | Ran | agree | disagree | blocked | recorded |')
print('|---|---|---|---|---|---|---|---|---|')
for f, cult, how, note, ran, s, fe in runs:
    print(f'| `{f}` | {cult} | {how} | {note} | {ran} | {s["agree"]} | {s["disagree"]} | {s["blocked"]} | {s["recorded"]} |')

print('\n## Still `uncertain` or `documented` after the run, and why\n')
left = [c for c in after.values() if c['source'] in ('uncertain', 'documented')]
why = collections.defaultdict(list)
for c in left:
    r = record.get(c['id'])
    if r is None:
        why['not asked by this run'].append(c['id'])
        continue
    res = r[0]
    if res['status'] == 'blocked':
        why[f'blocked: {res.get("reason", "")}'].append(c['id'])
    elif res['status'] == 'disagree':
        why['disagrees (below)'].append(c['id'])
    elif res['status'] == 'recorded':
        why['recorded: the case differs from Excel by decision and has no excelExpect'].append(c['id'])
    else:
        why[f'agrees in {r[3]}, which -Update did not cover'].append(c['id'])
for k, ids in sorted(why.items(), key=lambda kv: -len(kv[1])):
    print(f'- **{k}** ({len(ids)}): ' + ', '.join(sorted(ids)))

print('\n## Every disagreement\n')
print('| Case | File | Formula or input | Excel | `expect` (or `excelExpect`) | Pass |')
print('|---|---|---|---|---|---|')
for i in sorted(record):
    res, how, cult, f, _ = record[i]
    if res['status'] != 'disagree':
        continue
    case = after.get(i) or before.get(i)
    cells = case.get('cells', {})
    ex = res.get('excel') or {}
    if 'refusedEntry' in ex:
        excel = f'refused: {ex["refusedEntry"]}'
    else:
        excel = ', '.join(f'{k} {show(ex[k])}' for k in ('value2', 'text', 'formula', 'numberFormat') if k in ex)
    expect = case.get('excelExpect', case.get('expect'))
    print(f'| {i} | `{case["_file"]}` | `{show(cells)[:220]}`{" + actions" if case.get("action") or case.get("actions") else ""} | {excel[:200]} | `{show(expect)[:160]}` | {how}, {cult} |')
