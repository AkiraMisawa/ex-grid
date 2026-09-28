# The scroll height is compressed above the browser's layout ceiling

*(Decided with the user, 2026-09-27, after the second Windows run failed VZ-14, BIG-1, BIG-5, SH-2
and DC-2/3/7 at 150% display scale, and the bisect put the start at the commit that made `/wide` a
million rows — `verification/2026-09-27-windows-2/results.md`,
`verification/2026-09-27-windows-bisect/results.md`. It amends
[ADR-0013](./0013-fixed-row-height.md) and adds a sixth entry to
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md).)*

## What was found

ADR-0013 sets the scrollable height to rows × `RowHeight` and refuses a result whose height would
exceed 2²⁵ px (VZ-8). The refusal assumed that one CSS pixel is one layout pixel. It is not.
**Chromium clamps any layout length at just under 2²⁵ px counted in zoomed units**, so in CSS
pixels the ceiling is 2²⁵ ÷ (display scale × page zoom). Measured on Linux Chrome 154, with the
display scale forced on the command line:

| Scale | Spacer asked | Laid out | 2²⁵ ÷ scale | Last reachable row at 28 px |
|---|---|---|---|---|
| 1 | 28,000,028 | as asked | 33,554,432 | all |
| 1.25 | 28,000,028 | 26,843,542 | 26,843,545.6 | 958,697 |
| 1.5 | 28,000,028 | 22,369,618 | 22,369,621.3 | 798,914 |
| 2 | 28,000,028 | 16,777,214 | 16,777,216 | 599,185 |

The clamp is silent. `scrollHeight` comes back short, the browser pins `scrollTop` at the clamped
maximum, and the grid, which computes against the true height, paints rows that are not the ones
it thinks it is showing. At 150% the last 200,000 rows of `/wide` cannot be reached, and ExSheet's
1,048,576 rows already cannot at 125%. This is the quiet wrongness VZ-8 exists to refuse, let
through by an assumption. Browser page zoom (Ctrl+Plus) clamps the same way.

Two further facts shaped the decision:

- **`devicePixelRatio` does not say where the ceiling is.** Playwright's emulated scale factor
  raises `devicePixelRatio` without moving the clamp, and a real OS scale under an emulated
  viewport reports 1.0 while clamping. That is why the Linux suite, which emulates, was green.
- **Even at scale 1 the ceiling is 33,554,428, not 33,554,432.** VZ-8's guard was 4 px generous.

## Decision

**When the true height exceeds the ceiling, the scroll height is compressed. Below it, nothing
changes.**

- **The spacer is at most the ceiling, less a margin.** With H the true height, S the spacer's
  height and V the readable height, the content offset for a scroll offset s is
  `c(s) = s × k`, with `k = (H − V) / (S − V)`. Both ends are exact: s = 0 shows the first row, and
  the largest s shows the last. Where H fits under the ceiling, S = H and k = 1, and every formula
  in ADR-0013 is today's, bit for bit.
- **Every consumer of vertical geometry goes through the one mapping**: the slice
  (`first row = floor(c(s) / RowHeight)`), the placement of the painted rows, the selection
  overlay, the Cell Editor and popovers, the pointer's row (hit-testing, the hover band, drag
  autoscroll, the fill handle), and the reveal, which asks for `s = c* / k` and rounds towards
  "the whole Focus is visible". Rows are painted from the offset the browser reports, never from
  the one the grid asked for, because `scrollTop` is quantised to device pixels.
- **An overlay rectangle is clipped to the painted rows.** A whole-column selection was painted
  22,369,617 px tall at 150%, clamped like the spacer. Only its visible part was ever needed.
- **The grid is told the ceiling.** A `ResizeObserver` watches a hidden element declared 2²⁵ px
  tall inside a zero-size box. The size it reports **is** the current ceiling in CSS pixels, at
  every scale and zoom measured, including the emulated cases where `devicePixelRatio` is wrong.
  It reports once at attach and again only when the scale or the zoom changes. This is ADR-0021's
  sixth entry, argued there. The grid is told something the browser already knows; it performs no
  synchronous read on the path to a paint.
- **VZ-8's refusal stays, at the scale-1 ceiling.** A result whose true height exceeds 33,554,428
  CSS px is still refused by name, whatever the scale, so what is refused does not depend on the
  machine. Compression therefore covers exactly the results that fit at 100%, and k never exceeds
  the combined scale and zoom (1.5 at 150%, 5 at 500% zoom).

## What this costs, accepted

- **A wheel notch or a scrollbar arrow moves k times as far** while compressed: 1.25 for `/wide`
  and 1.31 for a Sheet at 150%. Keyboard movement is exact, because the reveal computes through
  the mapping.
- **A reveal can be off by under one CSS pixel**, from rounding `s` to device pixels. The rounding
  goes towards showing the whole Focus.
- **On a Server circuit, a compressed grid re-renders on every scroll event**, because the rows'
  placement depends on `s` even when the slice has not changed. Between the native scroll and the
  answering render the rows drift by (k − 1) × Δs, which shows as a slight jitter at 50–150 ms
  round trips. Each row still carries its own content, so nothing is ever shown against the wrong
  row. Uncompressed, the Server cost is today's.
- **The thumb stays proportional to the whole result.** That is the reason this option was chosen.

## Considered options

- **Refuse by scale.** Keep the geometry and refuse any result over the told ceiling. A sheet that
  works would become a refusal when the user presses Ctrl+Plus, and ExSheet's extent at 28 px would
  be refused from about 115%. Rejected as hostile.
- **A fixed conservative ceiling, compressed always** (2²⁵ ÷ 8, with nothing told). Deterministic
  and needs no JS, but k is about 7 at every scale, and a wheel notch jumps about 24 rows. Rejected.
- **A rebased scroll window**: the spacer covers a bounded window of rows and re-centres near its
  edges. Exact wheel steps, but the thumb no longer maps to the whole result, and each rebase is a
  programmatic scroll that interrupts momentum and costs a round trip on Server. Rejected for
  ExGrid. For ExSheet it resembles Excel, whose thumb spans the used range, and may return as a
  Sheet's own extent later, independent of this decision.
- **Lower the requirement**: a scale-independent cap near 300,000 rows with paging beyond. It gives
  up BIG-1/5 and ExSheet's full extent. Rejected.

## Consequences

- ADR-0013 gains a note that its scroll height is the true height only below the ceiling.
- ADR-0021's list has six entries.
- `CONTEXT.md` gains **Layout Ceiling**.
- The Definition of Done: VZ-8 and BIG-4 name 33,554,428; a new criterion holds the compressed
  geometry; and VZ-14's note records that emulation misses the clamp. **The regression test launches
  Chrome with `--force-device-scale-factor=1.5` and the viewport left to the window**, which runs
  headed under xvfb on Linux, so CI can gate it. A test using Playwright's `deviceScaleFactor`
  would pass without testing anything.

## What the implementation settled *(2026-09-28)*

- **The margin is 256 px, and k keeps 2 px of end slack**: `k = (H − V) / (S − V − 2)`. At 150%
  Chrome held `scrollTop` one device pixel short of the maximum it was asked for, and at k = 1.25
  that pixel cut 1.67 px off the last row. The slack absorbs it.
- **An overlay rectangle entirely outside the painted rows is not emitted.** A Focus scrolled far
  away has no outline element until its rows are painted. On Server the outline arrives with them.
- **A change of the ceiling keeps the first visible row**, as a change of the row height does
  (ADR-0028), and writes the new scroll offset to the browser. A told ceiling that compresses
  nothing, before or after, renders nothing.
- **The horizontal axis is not compressed.** Its guard follows the 33,554,428 constant, but no
  column layout comes near the ceiling at any scale a person uses, so a width above a smaller told
  ceiling is not handled. That is recorded, not solved.
- **A layer-3 test sets a scroll offset through the mapping, never as rows × `RowHeight`**
  *(2026-09-28, third Windows run)*. Five tests that did so passed on Linux and failed at the
  display's real 150%, where `n × 28` shows row 1.31 n. They were test defects, and the scale-forcing
  project now runs them.
- **A layer-3 test reads a length from the style attribute, not through the CSSOM**, which rounds
  it to six significant figures (`2.23694e+07px`).

## Measured on Windows *(2026-09-28, verification/2026-09-28-windows-3)*

At the display's real 150%, one wheel notch on `/wide` moved 100 px, **4.47 rows** at the top and
4.33 near the end, on both browsers and both hosts: 1.25 times the uncompressed 3.57, as predicted.
Excel moves 3 rows a notch at 100% zoom, Windows' 3 lines. On the Server host the rows first moved
by the uncompressed amount and the render caught up within one to three frames, **at most 0.62 rows
at a 100 ms round trip**; no frame moved them against the scroll by more than 0.05 rows. Both are
the costs accepted above, now measured, and nothing is changed for them.
