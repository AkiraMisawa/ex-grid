# Prints PV-21's figures for main and the branch side by side, from each side's metrics.json.
import json, sys
main = json.load(open(sys.argv[1]))['chrome']
branch = json.load(open(sys.argv[2]))['chrome']
def fmt(v):
    if isinstance(v, dict) and 'median' in v:
        return f"{v['median']:,} ({v['min']:,}–{v['max']:,})"
    return str(v)
for key in sorted(set(main) | set(branch)):
    if not (key.startswith('PV-21') or key.startswith('DA-17')):
        continue
    print(f"## {key}")
    m = main.get(key, {}); b = branch.get(key, {})
    for field in list(dict.fromkeys(list(m) + list(b))):
        print(f"  {field}: main {fmt(m.get(field))} | branch {fmt(b.get(field))}")
