# 140: The browser tells the grid its Device Pixel

Status: done

**What to build:** ADR-0090's first two parts. A `matchMedia` listener in `ex-grid.js` reports
`devicePixelRatio` at attach and whenever `(resolution: <current>dppx)` stops matching, and arms itself
on the new value (ADR-0021's eighth entry). C# keeps the ratio and writes `--ex-dp` inline on the
instance root, so every line drawn in Device Pixels is exact at any resolution, not only at the
stylesheet's steps.

**Blocked by:** None (can start immediately)

- [x] **The report crosses at attach and on a change of resolution only**, never per render, and is
      disposed with the grid.
- [x] **`--ex-dp` is inline on the root once told**; before that the stylesheet's steps stand.
- [x] **A report that is not a finite positive number is ignored.**
- [x] **Layer 2** for the inline token; **layer 3**: the report at attach, and at an emulated
      `devicePixelRatio` of 2.25 a gridline two whole Device Pixels (VZ-16). A thin line at a real
      225% is read by hand on Windows: emulation blends it at any scale.

## Comments

2026-10-02, claude/exsheet-part-c. `ex-grid.js` arms a `matchMedia('(resolution: <ratio>dppx)')`
listener at attach and re-arms it on each `change`; `OnDevicePixelAsync` keeps the ratio and
`RootStyle` writes `--ex-dp` inline. Layer 2: `DevicePixelTests`, and the allowlist count in
`ShippedStylesheetTests`. Layer 3: `device-pixel-225.spec.mjs` (the report at attach, and a gridline
two whole Device Pixels at an emulated 2.25). A change driven by a CDP override was told under
Playwright's Chromium but never under CI's branded Chrome and Edge, which fire no change for it, so it
is not a layer-3 test: a real zoom is read by hand on Windows, with the thin line.
Emulation blends a thin Border line over two Device Pixels even at 1.5, where the real `chrome-150`
project draws it in one, so a thin line at a real 225% is the next Windows run's.
