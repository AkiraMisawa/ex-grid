"""Part E: ExPivot's report beside Excel's PivotTable, item by item, from expivot.json (pivot-probe.mjs) and
excel.json (part-e-excel.ps1). Each side as the text of its cells, row by row; then the body compared cell by
cell, by row label: for each label both show, the values in order. A cell is compared as the text each shows.

    python3 compare.py expivot.json excel.json items.json > expivot-cells.md
"""
import json
import sys

ex = {i['id']: i for i in json.load(open(sys.argv[1], encoding='utf-8'))['items']}
xl = {i['id']: i for i in json.load(open(sys.argv[2], encoding='utf-8-sig'))}
spec = {i['id']: i for i in json.load(open(sys.argv[3], encoding='utf-8'))['items']}


def expivot_rows(r):
    rep = r.get('report') or {}
    if 'rows' not in rep:
        return [[f"(no report: {rep.get('empty')})"]]
    head = [h['text'] for h in sorted(rep['headers'], key=lambda h: h['col'])]
    return [head] + [row['cells'] for row in rep['rows']]


def excel_rows(r):
    return [[c['text'] for c in row] for row in r.get('cells', [])]


def table(rows):
    w = max((len(r) for r in rows), default=0)
    out = ['| ' + ' | '.join(f'`{c}`' if c != '' else ' ' for c in (r + [''] * (w - len(r)))) + ' |' for r in rows]
    if out:
        out.insert(1, '|' + '---|' * w)
    return '\n'.join(out)


def by_label(rows, skip):
    d = {}
    for r in rows[skip:]:
        if r and str(r[0]).strip() != '':
            # ExPivot's outer labels carry their expand or collapse button's glyph (U+2212 or +) before the text.
            d[str(r[0]).strip().lstrip('\u2212+').strip()] = r[1:]
    return d


def number(text):
    s = str(text).strip().replace(',', '')
    pct = s.endswith('%')
    try:
        v = float(s[:-1] if pct else s)
    except ValueError:
        return None
    return v / 100 if pct else v


def same_value(xc, et):
    """Excel's cell (its text and Value2) beside ExPivot's text: 'same', 'value' (the same number shown
    differently), or None."""
    if xc['text'].strip() == et.strip():
        return 'same'
    a, b = number(xc['value2']) if xc['value2'] is not None else None, number(et)
    # The same number when ExPivot's text, read back, is Excel's Value2 to 15 significant digits.
    if a is not None and b is not None and float(f'{a:.15g}') == float(f'{b:.15g}'):
        return 'value'
    return None


for i, s in spec.items():
    e, x = ex.get(i), xl.get(i)
    print(f'### {i}: item {s["item"]}{" (" + s["half"] + ")" if s.get("half") else ""}{" — gates 0.1.0" if s.get("gates") else ""}\n')
    print(f'{s["what"]}.\n')
    print('The records (`part-e/' + i + '.csv`): `' + s['csv'].replace('\r\n', '` / `').rstrip(' /`') + '`\n')
    print(f'Layout: rows {s["rows"]}, columns {s["cols"]}, values {[(v["field"], v["agg"], v.get("show")) for v in s["values"]]}'
          + (f', hidden {s["hide"]}' if s.get('hide') else '') + (f', form {s["layout"]}' if s.get('layout') else '') + '\n')
    if x is None or x.get('error'):
        print(f'**Excel:** not read ({(x or {}).get("error")})\n')
    else:
        print(f'**Excel** (`{x["tableRange2"]}`; Value Fields {[(v["caption"], v["numberFormat"]) for v in x["valueFields"]]}):\n')
        print(table(excel_rows(x)) + '\n')
    if e is None or e.get('error'):
        print(f'**ExPivot:** not read ({(e or {}).get("error")})\n')
    else:
        print(f'**ExPivot** (the Areas: {e["areas"]}):\n')
        print(table(expivot_rows(e)) + '\n')
        notes = [st for st in e['steps'] if isinstance(st, dict) and (st.get('notOffered') or st.get('refusal'))]
        for n in notes:
            print(f'- ExPivot: {n.get("notOffered") and "the Aggregation " + n["notOffered"] + " is offered disabled: " + "; ".join(n.get("reasons") or [])}{n.get("refusal") or ""}')
    if x and e and not x.get('error') and not e.get('error'):
        xr = x.get('cells', [])
        er = expivot_rows(e)
        xs = next((k for k, r in enumerate(xr) if r and r[0]['text'] in ('Row Labels', *s['rows'])), 0) + 1
        xb = {str(r[0]['text']).strip(): r[1:] for r in xr[xs:] if r and str(r[0]['text']).strip()}
        eb = by_label(er, 1)
        diffs, shown = [], []
        for label in list(dict.fromkeys(list(xb) + list(eb))):
            a, b = xb.get(label), eb.get(label)
            if a is None or b is None:
                diffs.append(f'`{label}`: Excel {[c["text"] for c in a] if a is not None else "has no such row"}, ExPivot {b if b is not None else "has no such row"}')
                continue
            for k in range(max(len(a), len(b))):
                xc = a[k] if k < len(a) else {'text': '(none)', 'value2': None}
                et = b[k] if k < len(b) else '(none)'
                r = same_value(xc, et)
                if r is None:
                    wide = f', at an ample width `{xc["wideText"]}`' if xc.get('wideText') is not None else ''
                    diffs.append(f'`{label}`, value column {k + 1}: Excel `{xc["text"]}`{wide} (Value2 {xc["value2"]}), ExPivot `{et}`')
                elif r == 'value':
                    wide = f', at an ample width `{xc["wideText"]}`' if xc.get('wideText') is not None else ''
                    shown.append(f'`{label}`, value column {k + 1}: Excel shows `{xc["text"]}`{wide} (Value2 {xc["value2"]}), ExPivot `{et}`')
        print('**The body, cell by cell, by row label:** ' + ('the same, every cell.' if not diffs and not shown else ''))
        for d in diffs:
            print(f'- **differs:** {d}')
        for d in shown:
            print(f'- the same value, shown differently: {d}')
        print()
