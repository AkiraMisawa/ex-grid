# 08: The editor opens on the Entry; the Formula Bar shows it

Status: ready-for-agent

**What to build:** Two ADR-0051 pieces that need no typing-time traffic. A Consumer supplies the text the Cell
Editor opens on, and ExSheet supplies the Entry, so F2 on a Formula's cell shows `=A1*2`. The
Formula Bar band, inside the root above the header and switched on by the Consumer, shows the Name
Box (the Consumer's label: `D200`) and the Focus cell's text. On a plain ExGrid it shows the full
value, which discharges ADR-0016's display. Its height comes from the Grid Metrics.

**Blocked by:** 02

- [ ] Editing a Formula's cell opens on the Formula, not the Value
- [ ] The Formula Bar follows the Focus and shows the Entry; the Name Box shows the address
- [ ] On a plain ExGrid it shows the full value behind `####` (ADR-0016)
- [ ] Switched off, there is no band and the geometry is unchanged
- [ ] The rows take what the band leaves, with no stylesheet/C# pairing (ADR-0027/0028)

## Comments
