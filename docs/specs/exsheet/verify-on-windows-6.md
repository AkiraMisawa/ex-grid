# What to verify on Windows, sixth run

Status: ready-for-human — **A, B in order**.

For the Claude Code session on the Windows desktop of the earlier runs. The fifth run's method,
tools and advance authorisation still apply: read [`verify-on-windows-5.md`](verify-on-windows-5.md)
first. Reuse the scripts in `verification/2026-09-29-windows-5/`. **Decide nothing. Record
everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-6`** from its tip,
  and record that tip as the verified commit. If the tip moves during the run, do not merge it in.
- Build, then run layers 1–2 in WSL, and record the counts. Run `npm ci` afresh in the Windows copy
  of `tests/ExGrid.Browser`.
- **The user has authorised this run in advance:** real keys and mouse to the browsers. Excel is not
  needed. **Do not stop to ask.** Say "starting" before the first input and "finished" after the
  last.

## Part A — by hand, what changed since the fifth run

On both hosts and both browsers, at the display's 150%:

1. **A far jump paints its rows at once** (ADR-0012, "a reveal paints where it is going"). Open
   `/wide`, click a cell, and press Ctrl+End, Ctrl+Home, Ctrl+↓ and Ctrl+↑. Record the screen during
   each jump as a screen recording, or with screenshots as fast as the tools allow.
   - Record whether any frame shows the Viewport with no rows.
   - Record whether the status line ever says "outside the visible range".
   - The fifth run's traces showed both on Server, for about a round trip.
2. **The live region after a collapse** (ADR-0033, added after the fifth run). In `/sheet`, select
   B2:C3, then click D5. Record the text of `.ex-announce` (read it through the page, not by
   screen reader). It should be empty. Then paste `=A1` from Notepad over B2:C3 again, and record it.
   Close only the Notepad tab you opened.

## Part B — layer 3, both browsers, both hosts, at 150%

The whole suite on both hosts, as in the fifth run, with `chrome-150`.
- **Do not park the cursor for the first run on each host.** Leave it wherever it is: UX-16's fix
  is meant to hold with the real cursor over the window.
- Record every failure with its criterion ID, and rerun each failure three times.

Then run each of these with `--repeat-each=10`. Record the count, and for each failure the trace's
last frame and the received value:

- **DC-13**, the edge auto-scroll test, on Server `chrome` and `msedge`. It failed 3 of 3 in the
  fifth run, and was fixed as a test that read the Name Box before Ctrl+↓ had answered.
- **UX-16**, "a Cell State ground and the overlays paint over the stripe", on Server `chrome` and
  `msedge`, with the cursor **not** parked.
- **WR-6** on Server `chrome` and `msedge`. It is now compared by row index.
- **"a far reveal paints rows in every frame"** (`circuit.spec.mjs`) on Server `chrome`, `msedge`
  and `chrome-150`.

## Finishing

Commit everything to `claude/exsheet-windows-verify-6` and push. The last message lists every
disagreement and failure, each with the ADR or criterion it belongs to. It proposes nothing on the
user's behalf.
