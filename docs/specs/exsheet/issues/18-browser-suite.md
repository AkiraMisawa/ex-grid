# 18: ExSheet in real browsers

Status: ready-for-agent

**What to build:** Layer 3 for everything above, on Chrome and Edge, against both hosts. It covers typing, Point by
keys and by mouse, completion accepted with Tab, the Formula Bar mirroring, the fill-handle drag,
paste from the real clipboard, Ctrl+arrow, Headings, insertion, and two ExSheets on one page staying
independent. The console must stay clean, and the invariants must hold. Only one agent runs
layer 3 at a time.

**Blocked by:** 06, 07, 10, 11, 13, 15, 16

- [ ] Every ExSheet criterion marked layer 3 in the Definition of Done passes on both browsers and both hosts
- [ ] Two ExSheets: keys, popovers, undo and the Formula Bar never cross (ADR-0018)
- [ ] No console message, and the DOM does not grow with the extent

## Comments
