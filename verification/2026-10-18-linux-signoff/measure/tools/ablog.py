import sys, glob, re
sys.path.insert(0, sys.argv[0].rsplit('/',1)[0])
from logjson import blocks
def flat(o, p=''):
    if isinstance(o, dict):
        for k, v in o.items(): yield from flat(v, f'{p}/{k}')
    elif isinstance(o, list):
        if o and all(isinstance(x,(int,float)) and not isinstance(x,bool) for x in o): return
        for i, v in enumerate(o): yield from flat(v, f'{p}[{i}]')
    elif isinstance(o,(int,float)) and not isinstance(o,bool): yield p, o
O, kind = sys.argv[1], sys.argv[2]; filt = sys.argv[3] if len(sys.argv) > 3 else ''
runs = {'rc3': [], 'ref': []}
for f in sorted(glob.glob(f'{O}/{kind}-[0-9]-*.log')):
    m = re.search(r'-(rc3|ref)\.log$', f)
    if m: runs[m.group(1)].append(dict(flat(blocks(f))))
keys = [k for k in runs['rc3'][0] if all(k in r for r in runs['rc3'] + runs['ref'])]
for k in keys:
    if filt and not re.search(filt, k): continue
    a = [r[k] for r in runs['rc3']]; b = [r[k] for r in runs['ref']]
    ma, mb = sum(a)/len(a), sum(b)/len(b); r = ma/mb - 1 if mb else 0
    print(f'{r:+7.1%} ref {"/".join(f"{x:g}" for x in b):>16}  rc3 {"/".join(f"{x:g}" for x in a):>16}  {k}{" <<" if abs(r)>0.2 else ""}')
