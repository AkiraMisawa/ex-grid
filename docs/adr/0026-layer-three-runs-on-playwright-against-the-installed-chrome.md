# Layer 3 runs on Playwright, against the Chrome that is already installed

The browser layer of the test suite (`AGENTS.md`, layer 3) is a Playwright project at
`tests/ExGrid.Browser`. It takes **one npm dependency**, `@playwright/test`, and it drives
the machine's own Google Chrome through `channel: 'chrome'` rather than a browser
downloaded into the repository.

## The problem this fixes first

`AGENTS.md` had declared layer 3 as "CDP driver" and listed what it covers. **The driver
did not exist in the repository.** It lived in an agent's scratchpad and disappeared with
the session — so the one layer that exists precisely because the other two cannot reach
the browser was the one nobody else could run, on the one platform
([ADR-0013](./0013-fixed-row-height.md): Windows and Linux) where its assertions are not
tautologies.

A declared gate that cannot be run is worse than an undeclared one. It reads as covered.

## Why Playwright rather than continuing to hand-roll CDP

Not because CDP is unworkable — the hand-rolled driver did work, and the low-level parts
of it are still used (below). Three specific failures decided it, and each had a cost that
was actually paid.

- **Input synthesis.** The driver passed a Windows virtual key code in
  `nativeVirtualKeyCode`. On macOS that code is the numeric keypad's decimal point, so
  **every "Ctrl+A" check in the suite was pressing Ctrl+`.`** and passing without testing
  anything. It was found only because a human asked whether select-all was covered.
  `page.keyboard.press('Control+A')` cannot make that mistake: there is no platform key
  code to get wrong.
- **Finding the browser.** Locating Chrome's executable on Windows is code that was never
  written, and it is exactly the code that stands between "the suite exists" and "someone
  else can run it." `channel: 'chrome'` is that code, maintained by somebody else.
- **Waiting.** The driver was sprinkled with `sleep(150)` — the standard way to make a
  browser suite flaky enough that people stop trusting its failures. Playwright's
  auto-waiting and `expect` polling wait for the condition instead of for a duration.

## What Playwright is deliberately not used for

Changing the device scale factor, reading layout, observing whether an event was
`defaultPrevented` — these are done raw, through `newCDPSession` and `page.evaluate`.
Playwright's abstractions buy nothing there, and a layer between the test and the number
it is measuring is a place for the measurement to go wrong quietly, which is the failure
mode this whole ADR is about.

## The cost, stated plainly

- **A `node_modules` and an npm lockfile enter the repository's test tree.** The shipped
  packages keep **zero** dependencies; [ADR-0019](./0019-one-repository-many-packages.md)
  is about what `ExGrid` and its companions may depend on, and a test project is not one
  of them. Nothing in `src/` knows this directory exists.
- **A second package manager to keep current.** Accepted: one direct dependency, pinned by
  a lockfile, and layer 3 is run by hand rather than on every build.
- **Chrome must be installed.** Not a new constraint —
  [ADR-0017](./0017-target-chromium-browsers-only.md) already restricts the target to
  Chromium browsers. And it is what makes the suite worth running: half of what layer 3
  asks about is what the **real browser on the real OS** does, so a bundled Chromium build
  would be answering for a browser no user has.

## Consequences

- **`nix develop .#browser` had to be repaired to be usable at all.** It listed
  `pkgs.chromium`, which has no `aarch64-darwin` build, so the shell failed to *evaluate*
  on Apple Silicon — it had never once worked on the machine this project is developed on.
  It now carries Node and `dotnet` (Playwright starts the DemoHost itself) and **no
  browser at all**: `channel: 'chrome'` resolves Google Chrome by its own well-known
  paths and would never pick up a nix store one, so a `chromium` there is unused and
  misleading, and `google-chrome` is unfree and would make the shell fail to build for
  anyone who has not opted in.
- **The suite runs headed by default, and that is a measured decision.** On macOS, headless
  Chrome keeps overlay scrollbars on the horizontal axis whatever the CSS asks for, so the
  15px the row band loses cannot be reproduced there and the scrollbar tests would pass
  while proving nothing. `EXGRID_HEADLESS=1` is available for a machine without a display,
  and is safe on Windows and Linux where the native scrollbars already occupy layout.
- **Layer 3 does not gate automatically, because there is no CI.** It is run by hand, and
  the ways it can lie are guarded inside the tests themselves — a scrollbar that occupies
  no space makes every assertion trivially true, so each test checks the forcing took
  before checking anything else. *(Replaced on 2026-09-24 by
  [ADR-0041](./0041-ci-runs-every-layer-and-layer-three-gates-on-linux.md): CI now runs this
  suite on Linux, headed under xvfb, on the runner's installed Chrome and Edge, and it gates
  there. The guards inside the tests are what make that run mean something, and they stay.
  Windows and a real IME remain by hand.)*
- **The three suites still in a scratchpad (keys, cells, server) move in next.** This
  directory is the ground they move onto; until they do, `AGENTS.md`'s layer 3 row names
  what is actually there and what is still to come.
- **Both target browsers run, because [ADR-0017](./0017-target-chromium-browsers-only.md) says
  both are verified.** `projects` names `chrome` and `msedge`, each resolving an installed
  browser by channel rather than downloading one; a single `npx playwright test` runs every
  spec on both, so the wall clock doubles and the effort asked of a person does not. A machine
  without Edge fails that project by name, which is the honest outcome — the README says so.

  > **The first version of this bullet had it backwards.** It read "if the browser target ever
  > widens, `channel: 'chrome'` becomes a projects matrix", treating Edge as a widening still to
  > come. ADR-0017 had already made it a hard requirement, in those words: *"Verification runs on
  > both Chrome and Edge. Passing on one does not satisfy the requirement."* This ADR was narrower
  > than the decision it was implementing, and the two sat contradicting each other in the
  > repository until someone read them side by side. The two things ADR-0017 names as differing in
  > practice — the minimum Edge version an organisation deploys, and enterprise clipboard policy —
  > are exactly the things a Chrome-only run cannot see.
