"""Audit the Excel evidence and regenerate the per-case results table. No Excel dependency."""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
import re
import struct

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repo', type=Path, default=HERE.parents[1], help='Baseline repository checkout, when the evidence directory was copied elsewhere')
ROOT = parser.parse_args().repo.resolve()

def hashes(path):
    raw = path.read_bytes()
    # Git may check text out with CRLF on Windows; permit that sole byte-level difference.
    return {hashlib.sha256(raw).hexdigest(), hashlib.sha256(raw.replace(b'\r\n', b'\n')).hexdigest()}

def load(name):
    return json.loads((HERE / name).read_text(encoding='utf-8-sig'))

def number(cell):
    # PowerShell 5.1's JSON number spelling can lose a low bit. The recorded R spelling is exact.
    return float(cell.get('roundTrip', cell['value2']))

def first(case):
    return case['states'][0]['cells'][0]

def display(cell):
    if cell['kind'] == 'number':
        return cell['roundTrip']
    if cell['kind'] == 'error':
        return cell['text']
    if cell['kind'] == 'blank':
        return '(blank)'
    return json.dumps(cell['value2'], ensure_ascii=False)

def audit(input_name, result_name):
    source, report = load(input_name), load(result_name)
    assert report['metadata']['caseFileSha256'].lower() in hashes(HERE / input_name)
    assert report['metadata']['scriptSha256'].lower() in hashes(HERE / 'probe.ps1')
    assert report['metadata']['culture'] == source['culture'] == 'en-GB'
    inputs = {c['id']: c for c in source['cases']}
    assert len(inputs) == len(source['cases'])
    assert len(report['cases']) == len(inputs)
    assert {c['id'] for c in report['cases']} == inputs.keys()
    for c in report['cases']:
        s = inputs[c['id']]
        assert c['formula'] == s['formula']
        assert c['status'] in {'observed', 'refused-entry'}, (c['id'], c.get('failure'))
        readback = {v['address']: v for v in c['fixtureReadback']}
        assert readback.keys() == s['cells'].keys(), c['id']
        for address, expected in s['cells'].items():
            actual = readback[address]
            if isinstance(expected, str) and expected.startswith('='):
                continue  # Formula results are observations, not invented expectations.
            kind = 'blank' if expected is None else 'boolean' if isinstance(expected, bool) else 'text' if isinstance(expected, str) else 'number'
            assert actual['kind'] == kind, (c['id'], address, expected, actual)
            assert actual['value2'] == expected, (c['id'], address, expected, actual)
        if c['status'] == 'refused-entry':
            assert c['refusal'] and c['hresult']
            continue
        assert len(c['states']) >= s.get('samples', 1)
        for state in c['states']:
            for cell in state['cells']:
                if cell['kind'] == 'number':
                    bits = struct.pack('>d', number(cell)).hex().upper()
                    assert bits == cell['bits'], (c['id'], bits, cell)
        if s.get('lifecycle'):
            states = {x['step']: x['cells'] for x in c['states']}
            for step in ['automatic-baseline', 'automatic-unrelated-edit', 'manual-baseline', 'manual-unrelated-edit', 'manual-explicit-calculation']:
                assert step in states
                a, b = states[step]
                assert number(b) == 2 * number(a), (c['id'], step)
            assert states['manual-baseline'][0]['bits'] == states['manual-unrelated-edit'][0]['bits'], c['id']
            assert states['automatic-baseline'][0]['bits'] != states['automatic-unrelated-edit'][0]['bits'], c['id']
            assert states['manual-baseline'][0]['bits'] != states['manual-explicit-calculation'][0]['bits'], c['id']
        if s['formula'] == '=RAND()':
            assert all(0 <= number(x['cells'][0]) < 1 for x in c['states']), c['id']
        if s['formula'] == '=LET(x,RAND(),x=x)':
            assert all(x['cells'][0]['value2'] is True for x in c['states'])
        if s['formula'] == '=LET(x,RAND(),x-x)':
            assert all(number(x['cells'][0]) == 0 for x in c['states'])
        if s['function'] == 'RANDBETWEEN':
            for state in c['states']:
                cell = state['cells'][0]
                if cell['kind'] == 'number':
                    assert number(cell).is_integer(), c['id']
    return report

pilot = load('pilot-independent-results.json')['cases']
pilot_template = load('pilot-template-results.json')['cases']
assert len(pilot) == len(pilot_template) == 40
for a, b in zip(pilot, pilot_template):
    assert a['id'] == b['id'] and a['status'] == b['status'] == 'observed'
    assert a['fixtureReadback'] == b['fixtureReadback'], a['id']
    assert a['states'] == b['states'], a['id']

reports = [audit('cases.json', 'results.json'), audit('followup-cases.json', 'followup-results.json'), audit('repeat-cases.json', 'repeat-results.json'), audit('additional-cases.json', 'additional-results.json')]
cases = reports[0]['cases'] + reports[1]['cases'] + reports[3]['cases']
index = {c['id']: c for c in cases}
observed = set(re.findall(r'^\| `([^`]+)` \| Observe \|', (ROOT / 'docs/specs/exsheet-functions/spec.md').read_text(), re.M))
assert {c['function'] for c in cases} == observed | {'LET', 'RAND', 'RANDBETWEEN'}
for repeat in reports[2]['cases']:
    original = index[repeat['id']]
    assert repeat['status'] == original['status'], repeat['id']
    if repeat['status'] == 'observed':
        a, b = first(original), first(repeat)
        assert a['kind'] == b['kind'], repeat['id']
        if a['kind'] == 'number':
            assert a['bits'] == b['bits'], (repeat['id'], a, b)
        else:
            assert a['value2'] == b['value2'], repeat['id']

counts = collections.Counter(c['function'] for c in cases)
lines = ['# Excel function observations: per-case index', '',
         'Generated by `python3 summarize.py`. Full fixtures and every state are in the JSON files.', '',
         'A returned Excel Error Value is an observation; `refused-entry` is COM rejecting Formula2.',
         'Neither is a probe failure. Variable samples are observations, never fixed expected values.', '',
         '| Function | Cases |', '|---|---|']
lines += [f'| `{f}` | {n} |' for f, n in sorted(counts.items())]
for fn in sorted(counts):
    lines += ['', f'## {fn}', '', '| Case | Formula | Question | Excel |', '|---|---|---|---|']
    for c in [c for c in cases if c['function'] == fn]:
        formula = c['formula'] if len(c['formula']) < 200 else '(126/127 bindings; see cases.json)'
        if c['status'] == 'refused-entry':
            result = 'refused-entry'
        else:
            result = display(first(c))
            samples = [x['cells'][0] for x in c['states'] if x['step'].startswith('calculation-')]
            distinct = {display(x) for x in samples}
            if len(distinct) > 1:
                if all(x['kind'] == 'number' for x in samples):
                    vals = [number(x) for x in samples]
                    result = f'{len(samples)} samples; min {min(vals)!r}, max {max(vals)!r}; {len(distinct)} distinct'
                else:
                    result = f'{len(samples)} samples: ' + ', '.join(sorted(distinct))
            if len(c['states']) > len(samples):
                result += '; additional edit/recalculation states in JSON'
        question = c['question'] if c['id'] != 'COUNTIFS-035' else 'One-range baseline; the mismatch is actually asked in COUNTIFS-ADDITIONAL-001'
        fields = [c['id'], f'`{formula}`', question, result]
        lines.append('| ' + ' | '.join(x.replace('|', '\\|').replace('\n', '<br>') for x in fields) + ' |')
keyboard = load('keyboard-results.json')
keyboard_input = load('keyboard-cases/let.json')
assert keyboard['windowsRegionalFormat'] == 'en-GB'
assert keyboard['formulasEnteredThrough'].startswith('typed')
for field, path in [('wrapperSha256', HERE / 'keyboard-probe.ps1'),
                    ('originalOracleSha256', ROOT / 'tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1'),
                    ('caseFileSha256', HERE / 'keyboard-cases/let.json')]:
    assert keyboard[field].lower() in hashes(path)
assert len(keyboard['cases']) == len(keyboard_input['cases']) == 14
assert all(c['status'] == 'recorded' and c['excel'] for c in keyboard['cases'])
lines += ['', '## LET typed with real keys', '',
          'These rows use the existing Oracle SendInput and dialog watcher. A returned Error Value',
          'and an entry rejected by a dialog are different observations.', '',
          '| Case | Typed Formula | Excel |', '|---|---|---|']
for c, source in zip(keyboard['cases'], keyboard_input['cases']):
    assert c['id'] == source['id']
    formula = source['cells']['A10']
    a = c['excel']
    result = 'refused-entry (dialog recorded)' if 'refusedEntry' in a else a.get('number', json.dumps(a['value2'], ensure_ascii=False))
    if 'refusedEntry' not in a:
        assert a['formula'] == formula, c['id']
    if len(formula) > 200:
        formula = '(250/255-character name; see keyboard-cases/let.json)'
    lines.append(f"| {c['id']} | `{formula}` | {result} |")
assert keyboard['cases'][3]['excel'].get('refusedEntry')  # A1 as a name: COM alone was misleading.
assert keyboard['cases'][1]['excel']['value2'] == keyboard['cases'][2]['excel']['value2'] == 2

(HERE / 'case-index.md').write_text('\n'.join(lines) + '\n')
print(f'Audited {len(cases)} cases covering {len(counts)} functions, all input kinds/Values, exact numeric bits, random/lifecycle invariants, and {len(reports[2]["cases"])} independent repeats; 14 typed LET observations and the Oracle/wrapper/input hashes verified.')
