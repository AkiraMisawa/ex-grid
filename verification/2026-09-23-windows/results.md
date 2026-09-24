# Verification — 2026-09-23, Windows

**Scope: layer 3 only, on a real Windows desktop.** The first layer-3 run with the
browsers on Windows itself, made for VZ-14, which nothing else can discharge. Layers 1
and 2 were not re-run for this record. The 150% runs were on the evening of 2026-09-23.
The 125% runs came after a Windows sign-out, just past local midnight (2026-09-24 00:42),
and are filed here because they finish the same verification.

## Environment

- Windows 11 Pro (build 26200), one display, 2560×1440
- Google Chrome 153.0.8010.48 at 150% and 153.0.8010.53 at 125% (an update applied in
  between; below), and Microsoft Edge 153.0.4234.48 for both. All are the Windows builds,
  run headed
- The runner: portable Node v24.14.1 on Windows, Playwright 1.62.1, driving a copy of
  `tests/ExGrid.Browser`
- The DemoHost: in WSL2 (.NET SDK 10 via nix), on `localhost:5299`, reached from Windows
  through WSL's localhost forwarding and reused by the config's `reuseExistingServer`.
  Where the host runs is not what these tests ask about; the browsers and the OS drawing
  their scrollbars are

## A finding before any result: the suite never saw the OS scale

Playwright's default viewport is a CDP emulation, and it pins the scale:
`deviceScaleFactor: options.deviceScaleFactor || 1` (playwright-core 1.62). On this
desktop, at 150%, every pre-existing test measured `devicePixelRatio` 1.00000003. The
OS's scale never reached the page. Run as it stood, `scrollbar.spec.mjs` would have passed
here at 125% without testing VZ-14. The new VZ-14 test sets `viewport: null`, and it fails
by name unless the page is on Windows, the bars occupy layout, and the DPR is fractional.

## Results

| Run | Scale | Result |
|---|---|---|
| `scrollbar.spec.mjs` | 150% | **8 passed**, 0 failed, 0 skipped, 0 flaky (4 per browser) |
| full suite, `layer3-150.log` | 150% | **140 passed**, 0 failed, 0 skipped, 0 flaky (70 per browser) |
| `scrollbar.spec.mjs`, `scrollbar-125-launch-failure.log` | 125% | **7 passed, 1 failed**: Chrome exited at launch and no layout was measured. See below |
| `scrollbar.spec.mjs`, `scrollbar-125.log` | 125% | **8 passed**, 0 failed, 0 skipped, 0 flaky (4 per browser) |

What the VZ-14 test recorded at 150%, identical on both browsers:

- DPR **1.5**; the bars occupy layout, 15×15 by `offsetWidth − clientWidth` (rounded)
- the gutter as the grid is told it (border box − content box, from a ResizeObserver):
  **15.34375 CSS px** each way. That is 23 device px ÷ 1.5, snapped to layout's 1/64 px.
  A non-integer number of CSS pixels, so the premise VZ-14 names held
- the Focus inside the readable area at the far corner and back, on both

What the VZ-14 test recorded at 125%, identical on both browsers:

- DPR **1.25**; the bars occupy layout, 15×15 rounded
- the gutter as the grid is told it: **15.203125 CSS px** each way. That is 19 device px
  ÷ 1.25, again not a whole number of CSS pixels
- the Focus inside the readable area at the far corner and back, on both

**The one failure at 125% was Chrome, not the grid.** The first test on `chrome` failed at
0 ms with `browserType.launch: Target page, context or browser has been closed`. Chrome had
started and exited with code 0 before a page existed. The explanation that fits the
evidence is a Chrome update waiting for the next launch. Playwright's launch was the first
after the sign-in, so it applied the update and exited. The evidence is circumstantial:
`chrome.exe` read 153.0.8010.48 the evening before and .53 after the failure, and Chrome's
`SetupMetrics` directory was written at 00:42:43 local, the second the run began. Whether a
`new_chrome.exe` was waiting beforehand was not looked at. The spec was run again unchanged
on the updated Chrome and passed 8 of 8. Both logs are kept.

Not asserted, but seen: with the forcing stylesheet's 15px bars, `offsetWidth − clientWidth`
read 14×14 at 125% and 15×15 at 150%. Those pages are the emulated DPR 1 ones. Why the
reading differs was not investigated, and nothing depends on it: the Focus assertion holds
either way, and the gutter is not asserted by design.

`metrics.json` holds the observational numbers (PST-6, DOM-5, BIG-7) from the full run,
recorded, never gated.

## What this discharges

- **ADR-0037's layer 3 on both target browsers** (KB-20..27, A11Y-17, UX-14): they had
  run only on the bundled Chromium; they pass here on `chrome` and `msedge`
- **VZ-14**: a real Windows desktop at 125%, the OS doing the scaling, and a native bar
  of 15.203125 CSS px. The Focus lands inside the readable area on `chrome` and `msedge`.
  150% was run first and passes too
