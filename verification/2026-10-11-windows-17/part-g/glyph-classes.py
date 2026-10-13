"""Part G, FN-12e: the widest glyph of each CellTextMetrics class (tests/GlyphWidths/classes.mjs) in each record
measure.mjs wrote on this desktop, at 14px and 12px, regular (the widest of 400, 500 and 600) and bold (700),
beside the core's defaults (GridMetrics.Resolve's preset table, at the verified commit).

    python3 glyph-classes.py <record.json> ...
"""
import json
import sys

WIDE = list('%€−+#')
NARROW = list('.,()/: ')
DIGIT = list('0123456789$£¥₹₺₫E-\'’') + [' ', ' ', '‎', '‏', '؜']
DEFAULTS = {('14', 'regular'): {'wide': 14.04, 'digit': 9.75, 'narrow': 6.41}, ('14', 'bold'): {'wide': 14.36, 'digit': 9.75, 'narrow': 6.41},
            ('12', 'regular'): {'wide': 12.04, 'digit': 8.36, 'narrow': 5.49}, ('12', 'bold'): {'wide': 12.44, 'digit': 8.36, 'narrow': 5.49}}
for f in sys.argv[1:]:
    j = json.load(open(f, encoding='utf-8'))
    glyphs = j['glyphs']
    print(f'## {f.split("/")[-1]}: {j["family"]!r}, {j["userAgent"].split(") ")[-1]}, unit {j["unit"]}')
    painters = j.get('painters')
    for size in ('14', '12'):
        g = j['sizes'][size]['glyphs']
        for kind, weights in (('regular', ['400', '500', '600']), ('bold', ['700'])):
            row = []
            for cls, members in (('wide', WIDE), ('digit', DIGIT), ('narrow', NARROW)):
                best, who = 0, None
                for w in weights:
                    widths = g[w]
                    for ch in members:
                        if ch in glyphs:
                            v = widths[glyphs.index(ch)] / 64
                            if v > best:
                                best, who = v, (ch, w)
                d = DEFAULTS[(size, kind)][cls]
                row.append(f'{cls} {best:.3f} ({who[0]!r} at {who[1]}) vs default {d} -> {"within" if best <= d else "OVER by %.3f" % (best - d)}')
            print(f'  {size}px {kind}: ' + '; '.join(row))
    if painters:
        p400 = painters['400'] if isinstance(painters, dict) else painters
        fam = {}
        for ch, who in zip(glyphs, p400):
            fam.setdefault(who, []).append(ch)
        print('  painted at 400 by: ' + '; '.join(f'{k} ({len(v)} glyphs)' for k, v in sorted(fam.items(), key=lambda kv: -len(kv[1]))))
    # The other class: every glyph of no class, its widest at each size (the default 15.46 at 14px, 13.26
    # at 12px, regular and bold), beside the full-width em, which the grid charges at the font size.
    for size, d in (('14', 15.46), ('12', 13.26)):
        g = j['sizes'][size]['glyphs']
        best = max(((g[w][i] / 64, ch, w) for w in ('400', '500', '600', '700') for i, ch in enumerate(glyphs)
                    if ch not in WIDE + NARROW + DIGIT and not ('\u3000' <= ch <= '\u9fff' or '\uac00' <= ch <= '\ud7af' or '\uff00' <= ch <= '\uffef')), default=None)
        print(f'  {size}px other (no class, full-width aside): widest {best[0]:.3f} ({best[1]!r} at {best[2]}) vs default {d} -> {"within" if best[0] <= d else "OVER by %.3f" % (best[0] - d)}')
