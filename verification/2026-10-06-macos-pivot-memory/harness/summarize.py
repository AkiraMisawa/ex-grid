"""Check the controlled WASM observations and derive memory tables; no timing inference."""
from pathlib import Path
import json
from statistics import median
root=Path(__file__).resolve().parents[1]
raw=root/'raw'
cases=['instrumented','gc-only','no-highlight','batch-one','drop-paints','drop-paints-gc','million-low-cardinality']
rows=[]
for name in cases:
 d=json.loads((raw/name/'result.json').read_text());a=d['memory'];first=a[0];last=a[-1]
 failed=name in cases[:4]
 assert d['completed']==(6 if failed else {'drop-paints':20,'drop-paints-gc':12,'million-low-cardinality':70}[name])
 assert len(a)==d['completed']+1
 assert len(d['errors'])==(1 if failed else 0)
 if name=='gc-only': assert 'Garbage collector could not allocate 16384u bytes' in d['errors'][0]
 elif failed: assert 'System.OutOfMemoryException' in d['errors'][0] and 'PivotCube.BuildAsync' in d['errors'][0]
 if name.endswith('gc') or name=='gc-only':assert all(x['forcedGc'] for x in a)
 assert last['records']==(1000000 if name=='million-low-cardinality' else 400000)
 assert last['reportRows']==(1101 if name=='million-low-cardinality' else 401001)
 rows.append(dict(case=name,completed=d['completed'],outcome='OOM on update 7' if failed else 'completed requested updates',
  managedFirstMiB=first['managedBytes']/2**20,managedLastMiB=last['managedBytes']/2**20,
  wasmLastMiB=last['wasmHeapBytes']/2**20,paintReports=last['roots']['paintDistinctReports'],
  reportWeakAlive=last['tracked']['Report']['alive'],historyVersions=last['roots']['historyVersions'],
  threadAllocatedTotalMiB=last['threadAllocatedBytes']/2**20,
  threadAllocatedMedianPerUpdateMiB=median((y['threadAllocatedBytes']-x['threadAllocatedBytes'])/2**20 for x,y in zip(a,a[1:]))))
a=json.loads((raw/'gc-only/result.json').read_text())['memory']
b=json.loads((raw/'drop-paints-gc/result.json').read_text())['memory']
slope=(a[-1]['managedBytes']-a[0]['managedBytes'])/2**20/6
matchedExtra=(a[6]['managedBytes']-b[6]['managedBytes'])/2**20/(a[6]['tracked']['Report']['alive']-b[6]['tracked']['Report']['alive'])
assert all(x['tracked']['Snapshot']['alive']==4 and x['tracked']['Answer']['alive']==1 for x in a[4:])
assert all(x['tracked']['Report']['alive']==3 for x in b[3:])
csv=json.loads((raw/'million-csv/result.json').read_text())
assert csv['errors']==[] and csv['refusal']=='' and csv['grid']['rows']=='111'
assert '1,000,000 rows' in csv['shown']
result=dict(cases=rows,retainedSlopeMiBPerGeneration=slope,matchedExtraReportMiB=matchedExtra,csv=csv)
(raw/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
print('all scenarios and memory/root counts checked')
print('retained slope MiB/generation:',round(slope,3),'matched extra report MiB:',round(matchedExtra,3))
