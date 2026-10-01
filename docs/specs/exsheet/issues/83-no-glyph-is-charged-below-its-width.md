# 83: No glyph a Number Format can emit is charged below its width

Status: ready-for-agent

**What to build:** ADR-0016's note of 2026-10-01, "No glyph a Number Format can emit is charged below
its width". This comes from ticket 82's measurement (see its comment).
- After ticket 82, 63 of 3,015 strings from ExSheet's built-in formats, across 24 cultures, are still
  estimated as fitting when they do not.
- Each of them holds a glyph wider than its class:
  - a currency sign: `₼ ₽ ¤ ₱ ₩ ₦ ₪`;
  - a letter such as `M` or `W`, in `AM`/`PM` or a month's name.
- Such a number or date is cut instead of shown as `####`.

**Blocked by:** 82

- [ ] **Decide the mechanism in the ticket's comment, and give the reason.** Either:
  - add a class for wide letters and symbols, as ADR-0016 added full-width as a fourth class; or
  - charge every glyph outside the measured classes at a width that covers it.
  Pick whichever errs towards `####` and costs the fewest early `####`s on ordinary numbers. Record
  the measured widths.
- [ ] **The core's defaults** (system-ui on macOS, DejaVu on Linux) and **`ExGrid.MudBlazor`'s Roboto**
      both cover these glyphs, at every weight painted.
- [ ] **Layer 1:** re-run ticket 82's 3,015 strings, or its tool, against the new estimate. None may
      paint past its estimate.
  - Name the test after ADR-0016 and principle 1.
  - Keep the corpus or the tool in the repo, so a later font change can re-run it.
- [ ] **Say what stays out.** For example, glyphs that only a Custom format can emit, and how a
      Consumer's font is covered (ADR-0027's metrics-bearing obligation).
