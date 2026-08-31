# Layer 3 — the browser

The third gate of `AGENTS.md`'s test table: the things that only exist in a browser, and
that neither xUnit nor bUnit can reach. Capture-phase keys, the clipboard, popovers,
multiple-instance independence — and what a **scrollbar takes out of the Viewport**, which
is what is here today.

## Running it

```sh
npm ci
npx playwright test
```

That is the whole command on any machine. Playwright starts the DemoHost itself (`dotnet
run`, port 5299) and reuses one that is already running, so an edit-and-rerun loop does
not pay for a restart.

Requirements: **Node** and **Google Chrome installed on the machine**. Nothing here
downloads a browser — `channel: 'chrome'` uses the real one, because half of what this
suite asks about is what the real browser on the real OS does. `npx playwright install`
is therefore *not* a step.

On this repository's own toolchain, `dotnet` and Node come from nix:

```sh
nix develop .#browser -c npx playwright test
```

**This suite does not run automatically.** There is no CI; it is run by hand, and
deliberately so on Windows and Linux, which is where the assertions below are not
tautologies.

### It runs headed by default

Not a preference — measured. On macOS, headless Chrome keeps **overlay** scrollbars on
the horizontal axis whatever the CSS asks for, so the 15px the row band loses could not be
reproduced at all and the scrollbar tests would pass while proving nothing. Headed reports
15/15.

`EXGRID_HEADLESS=1` is available for a machine without a display. It is safe on Windows
and Linux, where the native scrollbars already occupy layout and nothing has to be forced.
Do not make it the default.

## What `scrollbar.spec.mjs` asserts, and what it deliberately does not

One invariant, on every platform and at every zoom level:

> **Wherever the Focus ends up, all of it is inside the area the scrollbars left
> readable.**

Not "the gutter is 15px on Windows". What a native scrollbar is worth in CSS pixels — at
100% zoom or at 200% — could not be measured on the machine this was written on: macOS
forces overlay scrollbars and `--disable-features=OverlayScrollbar` does not turn them
off. Asserting a guessed number would be recording a guess. The invariant holds whatever
the answer turns out to be, and **the zoom test is where that answer lives**: run it on
Windows and it either passes, or it names the zoom level at which the chain from
ResizeObserver to painted geometry breaks.

Two things in the file are there because of specific ways this went wrong before:

- **The corner keys are pressed *after* the change, never before.** Read the Focus's
  position without pressing and it may simply still be sitting where the previous scroll
  offset left it, inside the box by luck.
- **Each classic-scrollbar test checks the forcing actually took** before it checks
  anything else. A scrollbar that occupies no space makes every assertion trivially true,
  and the first version of this file passed for hours doing exactly that.

## Why Playwright and not the hand-rolled CDP driver

`AGENTS.md` had declared layer 3 as "CDP driver" while the driver itself existed only in a
scratchpad and vanished with the session. It is here now because other people on other
machines have to be able to run it. Three concrete failures decided the shape:

- **Input synthesis.** The hand-rolled driver passed a Windows virtual key code in
  `nativeVirtualKeyCode`, which on macOS sends the numeric keypad's decimal point. Every
  "Ctrl+A" check was pressing Ctrl+`.` and passing vacuously. `keyboard.press('Control+End')`
  cannot make that mistake.
- **Finding the browser.** Locating Chrome on Windows was code nobody had written.
  `channel: 'chrome'` is that code.
- **Waiting.** The driver was sprinkled with `sleep(150)`. `expect` polling and
  `waitForFunction` wait for the condition instead of for a duration.

The low-level parts — changing the device scale factor, reading layout — are still done
raw, through `newCDPSession` and `page.evaluate`. Playwright buys nothing there, and
pretending otherwise would add a layer between the test and what it is measuring.

## Not here yet

The three suites that live in a scratchpad — keys, cells, server — are to be moved in.
Doing that is a task of its own; this directory is the ground they move onto.
