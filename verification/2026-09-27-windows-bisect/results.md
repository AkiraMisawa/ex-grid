# The bisect of the Windows-only layer-3 failures — 2026-09-27

**Scope: Part E of [`verify-on-windows-2.md`](../../docs/specs/exsheet/verify-on-windows-2.md),
following [`bisect-on-windows.md`](../../docs/specs/exsheet/bisect-on-windows.md).** Nothing is
decided or fixed here.

**What was looked for.** On Windows 11 at 150%, three tests of "the Focus is never behind a
scrollbar" (ADR-0012/0013) and VZ-14 in `scrollbar.spec.mjs`, and BIG-1 and BIG-5 in
`virtualisation.spec.mjs`, fail on both browsers at `789b208` and after. They fail again at this
run's verified commit, `e9c448a` ([Part D](../2026-09-27-windows-2/results.md)). The same scrollbar
tests passed on this machine on 2026-09-23 (`../2026-09-23-windows/`).

## Result

**The first bad commit is `062be53a87e82add40ee1a4917ab05bd8029e81a`**, "Hold the large-data
criteria to the Definition of Done's numbers, and write the harness's layer-3 half" (2026-09-24
11:22). **Both groups start failing there.** The scrollbar group, VZ-14 included, passes at its
parent `7ee1c0c` and fails from it on. BIG-1 and BIG-5 at 10⁶ rows **did not exist before it**:
this commit wrote them, and they have failed on this machine at 150% ever since.

What the commit changes (18 files, 995 insertions, 217 deletions):

- **`/wide` goes from 100,000 rows to 1,000,000** (`samples/ExGrid.DemoHost/DemoData.cs`,
  `Pages/Wide.razor`), made one row at a time on first request, and takes `?rows=N`
- `virtualisation.spec.mjs` is rewritten at 10⁶ rows: BIG-1, VZ-1/BIG-2/DOM-1, BIG-5, BIG-3 and
  PF-1's layer-3 half. Before it, the test was "the far corner is reachable and painted at 100,000
  rows (BIG-1-shaped, on /wide)"
- `fixtures.mjs` gains the console capture (CON-1/2/3/6), and every spec imports it
- **`scrollbar.spec.mjs` changes by one line**, its import, from `@playwright/test` to
  `./fixtures.mjs`. Its tests open `/wide`, as they did before
- `memory.spec.mjs` and `observational.spec.mjs` are new, with a `/lifecycle` page, and
  `docs/definition-of-done.md` and `docs/implementation-status.md` are updated

**How the tests fail at `062be53`**, on Chrome at 150%, as at `789b208` in the first run:

- The three "never behind a scrollbar" tests and VZ-14: **"the Focus is behind the horizontal
  scrollbar"**. After the Focus moves to the far corner, its bottom edge is at 898.2 px where the
  readable area ends at 872.2 px (899.5 in the second test), about one 28-px row below it. The
  failure is geometric, not a console message
- BIG-1: **the last row's cell `-r999999c99` never appears** within 15 s. BIG-5: the last row's
  first cell, `-r999999c0` (`K-999999`), never appears either

**Whether VZ-14 and BIG-1/5 share one cause is not decided here.** What the bisect shows is that
both begin with the commit that made `/wide` a million rows. The scrollbar tests had passed on
`/wide` at 100,000 rows, and their own text barely changed. Part D records a related number:
`sheet.spec.mjs` SH-18/DC-2/DC-3 found a column's selection painted 22,369,617.79 px tall where
more than 28,000,000 was expected, and 2²⁵ ÷ 1.5 = 22,369,621.3. It is a figure beside the result,
not a conclusion.

## The runs

Each step: the commit checked out in a worktree, its WebAssembly DemoHost built and started in WSL
on `localhost:5399`, its own `tests/ExGrid.Browser` copied to Windows over the same
`node_modules` (Playwright 1.62.1; every commit in the range asks for `^1.56.0`), and
`scrollbar.spec.mjs` and `virtualisation.spec.mjs` run whole on **Chrome at 150%**
(`--project=chrome`, `EXGRID_BASE_URL=http://localhost:5399`). The good/bad test was the
scrollbar group. The BIG tests were recorded alongside. The scripts are
[`bisect-step.sh`](bisect-step.sh) and [`bisect-run.sh`](bisect-run.sh), and `git bisect run` drove
them. Each step's log is `step-<commit>.log`, and the bisect's own log is
[`bisect-log.txt`](bisect-log.txt).

| Step | Commit | Date | Scrollbar group (4) | BIG-1, BIG-5 | Totals |
|---|---|---|---|---|---|
| check | `13311ff` (good) | 09-24 00:46 | **pass** | not yet written | 7 passed |
| 1 | `94c806b` | 09-25 15:56 | fail | fail | 4 passed, 6 failed |
| 2 | `3a80b09` | 09-24 22:54 | fail | fail | 4 passed, 6 failed |
| 3 | `35c60f1` | 09-24 13:12 | fail | fail | 4 passed, 6 failed |
| 4 | `b4a32b9` | 09-24 00:47 | pass | not yet written | 7 passed |
| 5 | `062be53` | 09-24 11:22 | **fail** | **fail** | 4 passed, 6 failed |
| 6 | `7ee1c0c` | 09-24 00:53 | pass | not yet written | 7 passed |

`789b208` (bad) was taken from the first run (`../2026-09-27-windows/layer3-mb-789b208.log`), where
all six failed on both browsers. No commit failed to build, and none was skipped.

**The good commit was checked first**, because the browsers had changed since 2026-09-23 (Chrome
153.0.8010.48 then, 153.0.8010.54 now; Edge 153.0.4234.48 then, 154.0.4258.37 now). `13311ff`
passes on today's Chrome, so the scrollbar failure on Chrome is not the browser update. The bisect
ran on Chrome only, as `bisect-on-windows.md` says. Edge was not bisected.

## Not done

- **The runs at 100% and 125% at the first bad commit** (`bisect-on-windows.md` step 3): **blocked**.
  Changing the display scale on this machine needs a Windows sign-out, which stops WSL and this
  session (the first run's record and `tests/ExGrid.Browser/README.md`). The session was not
  authorised to change the display scale. Every run here is at 150%
