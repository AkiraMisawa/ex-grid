# Crops of ExSheet's pictures, enlarged as Part A's Excel crops are: the cells a case reads, with 8
# device px around, two or four times. Writes into <out>/crops/ and prints what it wrote.
#
#     RUN_DIR=<the run's folder on the machine, with records/ and shots/> python3 crops.py <out> <record> ...
import json, sys, os
sys.path.insert(0, os.path.dirname(__file__))
import png
W = os.environ['RUN_DIR']
OUT = sys.argv[1]
SPEC = {'1': ('A1', 'B8', 2), '2': ('A1', 'B1', 4), '3b': ('A1', 'A4', 4), '3c': ('A1', 'A1', 4), '4': ('B2', 'D4', 4), '5': ('B2', 'D4', 4),
        '6': ('B2', 'C2', 4), '7': ('B2', 'C2', 4), '7x': ('B2', 'C2', 4), '8': ('B2', 'B3', 4), '9': ('B2', 'B14', 2), '10': ('B2', 'B3', 4),
        '11': ('A2', 'D4', 4), '12': ('A2', 'C4', 4), '13': ('A2', 'E5', 2), '14': ('B2', 'F6', 2), '12-1': ('B2', 'E5', 2), '12x': ('A2', 'C4', 4), '12-14': ('A3', 'B5', 4),
        '16': ('A1', 'A2', 4), '17': ('A1', 'A2', 4), '18': ('A1', 'A1', 4), '24': ('A1', 'A2', 4), '25': ('A1', 'A2', 4)}
def col(a):
    n = 0
    for ch in a.rstrip('0123456789'): n = n * 26 + ord(ch) - 64
    return n
def row(a): return int(a.lstrip('ABCDEFGHIJKLMNOPQRSTUVWXYZ'))
os.makedirs(os.path.join(OUT, 'crops'), exist_ok=True)
for rec in sys.argv[2:]:
    r = json.load(open(f'{W}/records/{rec}.json'))
    for c in r['cases']:
        if c['case'] not in SPEC: continue
        a, b, k = SPEC[c['case']]
        for s in c.get('states', []):
            if not s.get('shot') or 'dialog' in s['read']: continue
            boxes = [v['box'] for n, v in s['read']['cells'].items() if v and col(a) <= col(n) <= col(b) and row(a) <= row(n) <= row(b) and v['box']['h'] > 0]
            if not boxes: continue
            sc = s['pixels']['scale']; cl = s.get('shotClip') or s['clip']
            x0 = int((min(x['x'] for x in boxes) - cl['x']) * sc) - 8; y0 = int((min(x['y'] for x in boxes) - cl['y']) * sc) - 8
            x1 = int((max(x['x'] + x['w'] for x in boxes) - cl['x']) * sc) + 8; y1 = int((max(x['y'] + x['h'] for x in boxes) - cl['y']) * sc) + 8
            px = png.read(f'{W}/shots/{s["shot"]}')
            name = s['shot'].replace('.png', f'-x{k}.png')
            png.write(os.path.join(OUT, 'crops', name), png.crop(px, x0, y0, x1, y1), k)
            print(name)
