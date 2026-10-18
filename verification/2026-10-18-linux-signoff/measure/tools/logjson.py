import json, re, sys
def blocks(path):
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    out, i = {}, 0
    while i < len(lines):
        m = re.match(r'^([A-Z][^{]*?) \{$', lines[i])
        if m:
            buf = ['{']; j = i + 1
            while j < len(lines):
                buf.append(lines[j])
                if lines[j] == '}': break
                j += 1
            try: out[m.group(1)] = json.loads('\n'.join(buf))
            except Exception as e: pass
            i = j
        i += 1
    return out
if __name__ == '__main__':
    for p in sys.argv[1:]:
        b = blocks(p); print(p.split('/')[-1], list(b))
