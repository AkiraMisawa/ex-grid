"""ED-30 k8 with the Microsoft IME's predictive input off: each configuration's states, and the reading of k8.

    python3 summarise.py <records dir>
"""
import glob
import json
import os
import sys

out = []
ok = 0
files = sorted(glob.glob(os.path.join(sys.argv[1], '*.json')))
for f in files:
    d = json.load(open(f, encoding='utf-8-sig'))
    c = d['cases'][0]
    cfg = os.path.basename(f)[:-len('.json')].ljust(22)
    for s in c['states']:
        r = s['read']
        kf = r.get('keyField') or {}
        ev = [e for e in r.get('events', []) if e['type'] in ('compositionend', 'mousedown', 'pointerdown', 'click')]
        out.append(f"{cfg} {s['state']:<12} active={r.get('active')} editing={r.get('editing')} focus={r.get('focus')} "
                   f"field={kf.get('value')!r} composing={kf.get('composing')} D10={r['cells'].get('D10')!r} D12={r['cells'].get('D12')!r}"
                   + (f" events={[(e['type'], e.get('data') or e.get('target')) for e in ev][:4]}" if s['state'] == 'pressed-d12' else ''))
    last = c['states'][-1]['read']
    k8 = last['cells'].get('D10') == 'かな' and last.get('focus') == 'D12' and not last.get('editing') and last.get('active') == 'key-field'
    ok += k8
    out.append(f"{cfg} k8 as written: {'met' if k8 else 'NOT met'} (browser {d.get('browserVersion')}, messages {[m.get('type') for m in d['messages']]})")
    out.append('')
out.append(f'k8 met in {ok} of {len(files)} configurations')
print('\n'.join(out))
