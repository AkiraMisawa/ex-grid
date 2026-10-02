# 140: The browser tells the grid its Device Pixel

Status: ready-for-agent

**What to build:** ADR-0090's first two parts. A `matchMedia` listener in `ex-grid.js` reports
`devicePixelRatio` at attach and whenever `(resolution: <current>dppx)` stops matching, and arms itself
on the new value (ADR-0021's eighth entry). C# keeps the ratio and writes `--ex-dp` inline on the
instance root, so every line drawn in Device Pixels is exact at any resolution, not only at the
stylesheet's steps.

**Blocked by:** None (can start immediately)

- [ ] **The report crosses at attach and on a change of resolution only**, never per render, and is
      disposed with the grid.
- [ ] **`--ex-dp` is inline on the root once told**; before that the stylesheet's steps stand.
- [ ] **A report that is not a finite positive number is ignored.**
- [ ] **Layer 2** for the inline token; **layer 3** at an emulated `devicePixelRatio` of 2.25: a thin
      line is one Device Pixel and a gridline two, whole (VZ-16).
