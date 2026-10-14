"""Edit one case of a corpus file in place, keeping the file's own layout.

setcase(path, id, source=..., description=..., expect=<json text>, extra=[(key, json text)], drop=[keys])
"""
import json, re

def setcase(path, cid, source=None, description=None, expect=None, extra=(), drop=()):
    s = open(path, encoding='utf-8').read()
    start = s.index('"id": "%s"' % cid)
    # the block runs to the closing brace of this case: the next line that is exactly '    }' or '    },'
    m = re.compile(r'\n    \}', re.M).search(s, start)
    end = m.start()
    block = s[start:end]
    def sub(key, value):
        nonlocal block
        pat = re.compile(r'("%s": )(.*?)(,?)$' % re.escape(key), re.M)
        mm = pat.search(block)
        assert mm, (cid, key)
        block = block[:mm.start()] + mm.group(1) + value + mm.group(3) + block[mm.end():]
    if source is not None: sub('source', json.dumps(source))
    if description is not None: sub('description', json.dumps(description, ensure_ascii=False))
    if expect is not None: sub('expect', expect)
    for key in drop:
        pat = re.compile(r',\n\s*"%s": .*?$' % re.escape(key), re.M)
        mm = pat.search(block)
        assert mm, (cid, 'drop', key)
        block = block[:mm.start()] + block[mm.end():]
    for key, value in extra:
        indent = re.search(r'\n(\s*)"source"', '\n' + block).group(1) if False else '      '
        block = block.rstrip() + ',\n' + indent + '"%s": %s' % (key, value)
    s = s[:start] + block + s[end:]
    json.loads(s)
    open(path, 'w', encoding='utf-8').write(s)
