"""A copy of the corpus in which ARITH-136..149 name en-GB, for oracle.ps1 -Cases under en-GB.

oracle.ps1 at the verified commit reports a case with no "culture" as blocked on any machine whose
regional format is not en-US. The procedure runs under the machine's own en-GB. This copy changes
nothing but "culture" on those fourteen cases; the corpus in the repository is not touched.

    python3 en-GB-copy.py <out dir>      writes <out dir>/arithmetic.json and fixtures.json
"""
import json, pathlib, sys

src = pathlib.Path(__file__).resolve().parents[2] / 'tests/ExSheet.Engine.Tests/ExcelCases'
out = pathlib.Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
ids = {f'ARITH-{n}' for n in range(136, 150)}
corpus = json.loads((src / 'arithmetic.json').read_text(encoding='utf-8-sig'))
named = 0
for case in corpus['cases']:
    if case['id'] in ids:
        assert 'culture' not in case, case['id']
        case['culture'] = 'en-GB'; named += 1
assert named == len(ids), named
(out / 'arithmetic.json').write_text(json.dumps(corpus, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(out / 'fixtures.json').write_bytes((src / 'fixtures.json').read_bytes())
print(f'{named} cases name en-GB in {out}')
