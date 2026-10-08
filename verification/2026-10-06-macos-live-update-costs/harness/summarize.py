"""Summarize retained observations. Does not infer a missing or failed update's time."""
import json
from pathlib import Path
from statistics import median
root=Path(__file__).resolve().parents[1]
raw=root/'raw'

def fmt(x): return '<0.001' if 0 <= x < .001 else f'{x:.3f}'
def table(headers,rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join(['---']*len(headers))+' |']+['| '+' | '.join(map(str,r))+' |' for r in rows])
def minimum(config,key):return min(s[key] for s in config['runs'])
def read(name):return json.loads((raw/name).read_text())
def combined(pattern):return [sample for file in sorted(raw.glob(pattern)) for sample in json.loads(file.read_text())['runs']]
previous=read('tables.json') if (raw/'tables.json').exists() else {}
summary={}
g=read('grid-core.json');p=read('pivot-core.json');t=read('pivot-trees-and-keys.json')
summary['GRID_CORE']=table(['Rows','Changes','Apply ms','Publication ms','Requery alone ms','TakeInWindow ms','ApplyState ms','bUnit render ms'],[[f"{x['rows']:,}",x['changes'],*[fmt(minimum(x,k)) for k in ['applyMs','publicationMs','isolatedRequeryMs','takeInWindowMs','applyStateMs','renderBunitMs']]] for x in g])
summary['PIVOT_CORE']=table(['Report rows','Apply/fold ms','Next answer ms','Cube ms','Report ms','SameRows ms','Label widths ms','Show ms'],[[f"{x['reportRows']:,}",*[fmt(minimum(x,k)) for k in ['applyMs','answerMs','cubeMs','reportMs','sameRowsMs','labelWidthsMs','showMs']]] for x in p])
summary['PIVOT_TREE']=table(['Report rows','Row tree ms','Column tree ms','Cached keys made ms'],[[f"{x['reportRows']:,}",*[fmt(minimum(x,k)) for k in ['rowTreeMs','columnTreeMs','cachedKeyConstructionMs']]] for x in t])
summary['PIVOT_GRID']=table(['Report rows','Key check ms','Instance check ms','TakeInWindow ms','ApplyState after check ms','Diagnostic bUnit render ms'],[[f"{x['reportRows']:,}",*[fmt(minimum(x,k)) for k in ['keysMs','instancesMs','takeInWindowMs','applyStateAfterCheckMs','renderBunitMs']]] for x in p])
full=read('pivot-full-render.json')
assert len(full)==3
for x in full:
 assert len(x['runs'])==9
 assert all(r['painted']==18 and r['rendered']==18 and r['mounted']==0 and r['rowRenderCalls']==18 and r['gridRenderCalls']==1 and r['pivotRenderCalls']==1 and r['hasChangeHighlight'] and r['paintedTextRowsChanged']>0 for r in x['runs'])
summary['PIVOT_FULL_RENDER']=table(['Report rows','Actual ExPivot bUnit render, including Window check ms','Painted rows','Re-rendered rows','Remounted rows'],[[f"{x['reportRows']:,}",fmt(minimum(x,'renderIncludingWindowCheckMs')),18,18,0] for x in full])
rows=[];badpause=[];badframe=[];sametask=0;changedcount=0
for n in [100000,1000000]:
 for k in [1,100,1000]:
  runs=combined(f'grid-browser-{n}-{k}-repeat[012].json')
  assert len(runs)==36,(n,k,len(runs))
  badpause += [x for x in runs if x['status']!=x['statusAfterPause']]
  badframe += [x for x in runs if x['gridFrame'] is not None and x['gridFrame']>x['statusFrame']+1]
  sametask+=sum(x['gridChangedAt']==x['statusAt'] for x in runs)
  changedcount+=sum(x['gridChangedAt'] is not None for x in runs)
  times=[x['applyToFrameMs'] for x in runs]
  rows.append([f'{n:,}',k,len(runs),f"{median(x['appliedMsRounded'] for x in runs):.1f}",f'{median(times):.1f}',f'{min(times):.1f}–{max(times):.1f}',sum(x['gridChangedAt'] is None for x in runs),f"{median(x['gridMutationRecords'] for x in runs):g}"])
summary['GRID_BROWSER']=table(['Rows','Changes','Samples','Apply ms, median (rounded)','Apply → frame ms, median','Range ms','No painted text change','Grid mutations, median'],rows)
summary['GRID_BROWSER']+=f'\n\nAll 216 samples retained the observed batch number after Pause. In all {changedcount} samples with changed grid text, the grid and status mutations were seen in the same MutationObserver delivery, and the grid frame was no later than the status frame (within 1 ms). The other {216-changedcount} samples measure a frame after a publication without a painted text change. No sample was substituted by a later batch.\n'
assert not badpause and not badframe and sametask==changedcount
rows=[]
for inner in [10,100,400]:
 runs=combined(f'pivot-browser-{inner}-repeat[012].json'); times=[x['applyToFrameMs'] for x in runs]
 assert len(runs)==(18 if inner==400 else 27)
 rows.append([f'{inner*1000+1001:,}',len(runs),f'{median(times):.1f}',f'{min(times):.1f}–{max(times):.1f}','Failed on update 7 in 3/3 runs' if inner==400 else '3/3 runs passed (9 updates each)'])
summary['PIVOT_BROWSER']=table(['Report rows','Completed frames','Apply → frame ms, median','Range ms','Outcome'],rows)
wire=raw/'grid-wire.json'
if wire.exists():
 d=read('grid-wire.json'); rows=[]
 assert len(d['results'])==6
 for x in d['results']:
  assert not x['logs']
  assert all(r['batches']==1 and r['acks']==1 and r['eventCompletions']==1 for r in x['runs'])
  assert len(x['runs'])==15
  rows.append([f"{x['rows']:,}",x['changes'],x['paintedRows'],int(median(r['renderPayloadBytes'] for r in x['runs'])),int(median(r['wireBytes'] for r in x['runs']))])
 summary['WIRE']=table(['Rows','Changes','Painted rows','Render payload bytes/update, median','Compressed wire bytes/update, median'],rows)
 summary['WIRE']+='\n\nAll 90 retained updates produced exactly one render batch, one acknowledgement and one event completion. All six configurations negotiated `permessage-deflate; client_max_window_bits=15`; the browser console was clean. Bytes do not grow with total rows when the same visible rows change; k=100 and k=1,000 amend the same painted slice in this controlled workload.\n'
(raw/'tables.json').write_text(json.dumps(summary,indent=2)+'\n')
text=(root/'README.md').read_text()
for key,value in summary.items():
 marker='<!-- '+key+' -->'
 if marker in text:text=text.replace(marker,value)
 elif key in previous:text=text.replace(previous[key],value)
 assert value in text, f'README table {key} is missing or diverged'
assert '<!-- ' not in text
(root/'README.md').write_text(text)
print('tables updated:',', '.join(summary))
