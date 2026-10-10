"""Untimed reference only: encode the same initial A data as parallel JSON arrays.

Run after measure.mjs, while the own server is still up. This is not PivotJson and is
not fed to the browser. No conclusions about its parse time or allocation are measured.
"""
import decimal
import gzip
import json
import pathlib
import urllib.request
import zlib

root = pathlib.Path(__file__).resolve().parent
base = 'http://127.0.0.1:5894'
results = []
compact = lambda value: json.dumps(value, separators=(',', ':'), ensure_ascii=True)
for leaves in (2976, 100000, 400000):
    prepared = json.load(urllib.request.urlopen(f'{base}/api/prepare?mode=A&leaves={leaves}&records=1000000'))
    with urllib.request.urlopen(f'{base}/api/run?session={prepared["session"]}&action=initial&batch=0&visible=true') as response:
        compressed = response.read()
        assert response.headers['Content-Encoding'] == 'gzip'
    body = gzip.decompress(compressed)
    decoded = json.loads(body, parse_float=decimal.Decimal)
    rows = decoded['leaves']
    arrays = '{"id":' + compact([r['id'] for r in rows]) + ',"group":' + compact([r['group'] for r in rows])
    arrays += ',"label":' + compact([r['label'] for r in rows])
    arrays += ',"value":[' + ','.join(str(r['value']) for r in rows) + ']}'
    columns = ('{"version":0,"reset":true,"leaves":' + arrays
        + ',"changes":null,"rows":null,"patches":null,"reportRows":' + str(decoded['reportRows']) + '}').encode()
    round_trip = json.loads(columns, parse_float=decimal.Decimal)['leaves']
    assert all({k: round_trip[k][i] for k in round_trip} == r for i, r in enumerate(rows))
    results.append(dict(leaves=leaves, records=1000000, rowRaw=len(body), rowActualHttpGzip=len(compressed),
        rowPythonGzipLevel1=len(gzip.compress(body, compresslevel=1, mtime=0)),
        columnarRaw=len(columns), columnarPythonGzipLevel1=len(gzip.compress(columns, compresslevel=1, mtime=0)),
        identicalValues=True))
output = dict(note='Untimed encoding-only reference; custom parallel arrays, not shipped PivotJson. No browser parse measurement.',
              zlib=zlib.ZLIB_VERSION, results=results)
(root / 'raw/encoding-sizes.json').write_text(json.dumps(output, indent=2))
print(json.dumps(output, indent=2))
