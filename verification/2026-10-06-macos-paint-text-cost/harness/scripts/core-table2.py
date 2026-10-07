import json
R='/Users/akira338/github/ex-grid/.claude/worktrees/live-data-paint-text/verification/2026-10-06-macos-paint-text-cost/raw/'
L=lambda n: json.load(open(R+f'core-{n}.json'))['measured']['results']
def row(name, a1, b1, a2=None, b2=None):
    def us(s): return f"{s['Min']*1000:.1f}"
    def ms(s): return f"{s['Min']:.3f}"
    m=lambda r: r['measured']
    pa1,pb1=m(a1),m(b1)
    cells=[name]
    if a2:
        pa2,pb2=m(a2),m(b2)
        cells.append(f"{us(pa1['notePaintAlone'])} / {us(pa2['notePaintAlone'])}")
        cells.append(f"**{us(pb1['notePaintAlone'])} / {us(pb2['notePaintAlone'])}** (median {pb1['notePaintAlone']['Median']*1000:.1f})")
        cells.append(f"{pa1['notePaintAloneBytes']['Median']:,.0f}")
        cells.append(f"{pb1['notePaintAloneBytes']['Median']:,.0f}")
        cells.append(f"{ms(pa1['render'])} / {ms(pa2['render'])}")
        cells.append(f"{ms(pb1['render'])} / {ms(pb2['render'])}")
        cells.append(f"{pb1['paintShareOfRenderAtLeast']*100:.0f}% / {pb2['paintShareOfRenderAtLeast']*100:.0f}%")
        cells.append(f"{pa1['stepBytes']['Median']:,.0f} / {pb1['stepBytes']['Median']:,.0f}")
        rr=lambda p: f"{p['rowsRendered']['Min']:.0f}" if p['rowsRendered']['Min']==p['rowsRendered']['Max'] else f"{p['rowsRendered']['Min']:.0f}-{p['rowsRendered']['Max']:.0f}"
        cells.append(f"{rr(pa1)} / {rr(pb1)} / {rr(pa2)} / {rr(pb2)}")
    else:
        cells.append(us(pa1['notePaintAlone']))
        cells.append(f"**{us(pb1['notePaintAlone'])}** (median {pb1['notePaintAlone']['Median']*1000:.1f})")
        cells.append(f"{pa1['notePaintAloneBytes']['Median']:,.0f}")
        cells.append(f"{pb1['notePaintAloneBytes']['Median']:,.0f}")
        cells.append(ms(pa1['render']))
        cells.append(ms(pb1['render']))
        cells.append(f"{pb1['paintShareOfRenderAtLeast']*100:.1f}%")
        cells.append(f"{pa1['stepBytes']['Median']:,.0f} / {pb1['stepBytes']['Median']:,.0f}")
        rr=lambda p: f"{p['rowsRendered']['Min']:.0f}" if p['rowsRendered']['Min']==p['rowsRendered']['Max'] else f"{p['rowsRendered']['Min']:.0f}-{p['rowsRendered']['Max']:.0f}"
        cells.append(f"{rr(pa1)} / {rr(pb1)}")
    return "| " + " | ".join(cells) + " |"
g0,g1,g02,g12=L('grid-v0'),L('grid-v1'),L('grid-v0-r2'),L('grid-v1-r2')
for i in range(len(g0)):
    print(row(g0[i]['scenario'], g0[i], g1[i], g02[i], g12[i]))
print()
p0,p1=L('pivot-v0-r2'),L('pivot-v1-r2')
for i in range(len(p0)):
    print(row(p0[i]['scenario'], p0[i], p1[i]))
# whole step for pivot e
print(json.dumps({k: (L('pivot-v0-r2')[4]['measured']['step'][k], L('pivot-v1-r2')[4]['measured']['step'][k]) for k in ('Min','Median','Max')}))
