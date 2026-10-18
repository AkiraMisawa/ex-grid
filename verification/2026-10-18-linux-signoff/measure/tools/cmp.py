import json, sys
def flat(o, p=''):
    if isinstance(o, dict):
        for k, v in o.items(): yield from flat(v, f'{p}/{k}')
    elif isinstance(o, list):
        if all(isinstance(x,(int,float)) and not isinstance(x,bool) for x in o): return
        for i, v in enumerate(o): yield from flat(v, f'{p}[{i}]')
    elif isinstance(o,(int,float)) and not isinstance(o,bool):
        yield p, o
new = dict(flat(json.load(open(sys.argv[1]))))
old = dict(flat(json.load(open(sys.argv[2]))))
flag = float(sys.argv[3]) if len(sys.argv) > 3 else 0.2
for k in new:
    if k in old and old[k]:
        r = new[k]/old[k]-1
        mark = ' <<' if abs(r) > flag else ''
        if mark or '-v' in sys.argv: print(f'{r:+7.1%} {old[k]:>12g} -> {new[k]:>12g}  {k}{mark}')
print('only new:', len([k for k in new if k not in old]), 'only old:', len([k for k in old if k not in new]))
