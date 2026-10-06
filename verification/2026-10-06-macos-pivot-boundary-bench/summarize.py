import json
import pathlib
import statistics
from collections import defaultdict

root = pathlib.Path(__file__).resolve().parent
data = json.loads((root / 'raw/measurements.json').read_text())
groups = defaultdict(list)
for sample in data['samples']:
    groups[sample['leaves'], sample['rtt'], sample['mode'], sample['index']].append(
        {key.lower(): value for key, value in sample.items()})
names = ['initial', 'visible 1', 'off-screen 1', 'visible 1000', 'off-screen 1000', 'collapse', 'expand', 'sort']
summary = []
for (leaves, rtt, mode, index), values in sorted(groups.items()):
    row = dict(leaves=leaves, rtt=rtt, mode=mode, action=names[index], samples=len(values))
    for key in ['actiontoframems', 'networktobodyms', 'clientparsems', 'clientcomputems', 'clientwindowapplyms',
                'clientrenderms', 'sourcems', 'reportms', 'serializems', 'gzipms', 'serverprocesscpums',
                'rawbytes', 'gzipbytes', 'sentrows', 'sentleaves', 'changedpaintedrows', 'managedallocatedbytes']:
        samples = [v[key] for v in values if key in v]
        row[key] = {'min': min(samples), 'median': statistics.median(samples), 'max': max(samples)} if samples else None
    summary.append(row)
(root / 'raw/summary.json').write_text(json.dumps(summary, indent=2))
lines = ['| Leaves | RTT ms | Mode | Action | Frame median (min) ms | Parse ms | Compute ms | Server report min ms | gzip bytes |',
         '|---:|---:|:---:|:---|---:|---:|---:|---:|---:|']
for row in summary:
    def number(key, statistic='median'):
        return f"{row[key][statistic]:.2f}" if row[key] is not None else '—'
    lines.append(f"| {row['leaves']:,} | {row['rtt']} | {row['mode']} | {row['action']} | {number('actiontoframems')} ({number('actiontoframems', 'min')}) | {number('clientparsems')} | {number('clientcomputems')} | {number('reportms', 'min')} | {number('gzipbytes')} |")
(root / 'tables.md').write_text('\n'.join(lines) + '\n')
print('\n'.join(lines))
