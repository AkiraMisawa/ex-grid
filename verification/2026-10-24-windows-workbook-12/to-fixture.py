"""Writes Excel's answers in records/com-*.jsonl as the engine's layer 1 fixture,
tests/ExSheet.Engine.Tests/ThreeDReadings/excel-readings.json, which ThreeDExcelReadingsTests reads.

    python -I verification/2026-10-24-windows-workbook-12/to-fixture.py

Only the cases ExSheet can ask are written: a Formula that calls a function ExSheet does not declare
(ISREF, AREAS) is left out, and so is one Excel refused for its other arguments. Each Formula case names
its setup: "base" (B2 and C3 on Sheet1 to Sheet5) or "more" (with the cells the `more` group added), and
the cell of Out it stood in.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'tests', 'ExSheet.Engine.Tests', 'ThreeDReadings', 'excel-readings.json')
ERRORS = {
    -2146826281: '#DIV/0!', -2146826246: '#N/A', -2146826259: '#NAME?', -2146826288: '#NULL!',
    -2146826252: '#NUM!', -2146826265: '#REF!', -2146826273: '#VALUE!',
}
UNDECLARED = ('ISREF(', 'AREAS(')


def records(name):
    with open(os.path.join(HERE, 'records', name), encoding='utf-8-sig') as f:
        for line in f:
            line = line.strip()
            if line:
                r = json.loads(line)
                if r['group'] != 'meta':
                    yield r


def answer(cell):
    value, text = cell['value'], cell['text']
    if isinstance(value, int) and not isinstance(value, bool) and value in ERRORS and text.startswith('#'):
        return {'error': ERRORS[value]}
    if isinstance(value, bool):
        return {'boolean': value}
    if isinstance(value, str):
        return {'text': value}
    if value is None:
        return {'blank': True}
    return {'number': float(value)}


def cells(read):
    return {k: {'formula': v['formula'], **answer(v)} for k, v in read.items()}


formulas = []
# OFFSET's cases of `more` named the cell they stood in; com-offset.jsonl asked them again from Out!A9.
# com-later.jsonl asked the arguments past the third, from Out!A9 with Out!C1:C2 set as in `more`.
asked_again = {r['what'] for r in records('com-offset.jsonl')}
for name, setup, at in (('com-functions.jsonl', 'base', 'A1'), ('com-forms.jsonl', 'base', 'A1'), ('com-more.jsonl', 'more', 'A1'), ('com-offset.jsonl', 'base', 'A9'), ('com-later.jsonl', 'more', 'A9')):
    for r in records(name):
        if r.get('refused') or 'read' not in r or not r['what'].startswith('='):
            continue
        if any(u in r['what'] for u in UNDECLARED) or (name == 'com-more.jsonl' and r['what'] in asked_again):
            continue
        cell = r['read']['Out!' + at]
        formulas.append({'formula': r['what'], 'setup': setup, 'cell': at, 'written': cell['formula'], **answer(cell)})

sheets = [{'id': r['id'], 'order': r['order'].split(','), 'read': cells(r['read'])} for r in records('com-sheets.jsonl')]
rows, copies = [], []
for r in records('com-rest.jsonl'):
    if r['group'] == 'rows' and r['id'] != 'grouped-insert-row-1':
        rows.append({'id': r['id'], 'read': cells(r['read'])})
    elif r['group'] == 'copy' and r['id'] != 'copy-2-new-book':
        copies.append({'id': r['id'], 'order': r['order'].split(','), 'copy': r['copy'], 'read': cells(r['read'])})

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    json.dump({
        'source': 'verification/2026-10-24-windows-workbook-12 (Excel 16.0.20430, en-GB), written by to-fixture.py',
        'formulas': formulas, 'sheets': sheets, 'rows': rows, 'copies': copies,
    }, f, indent=1, ensure_ascii=False)
    f.write('\n')
print(len(formulas), len(sheets), len(rows), len(copies))
