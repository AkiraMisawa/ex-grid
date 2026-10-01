# 51: Excel's formatting keys

Status: ready-for-agent

**What to build:** [ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md) item 14, and
[ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Keys".

**Blocked by:** None (can start immediately). Ticket 50 is done, and the eleventh Windows run has
answered. Its record is `verification/2026-10-01-windows-excel-11/cell-format.md` with `../2026-10-01-windows-browser-11/keys.md`; ADR-0063 "What the
eleventh Windows run settled" sums it up.

- [ ] **Core: declared keys** (DC-57).
  - A declared key is claimed and raised together with whether an edit is open.
  - An undeclared key stays the browser's.
  - A declaration naming a key the core answers itself is refused by name.
- [ ] **ExSheet declares the keys the run found in Excel and in the page** (SH-42).
  - Each applies what Excel applies under the Sheet's culture, as one undo step (cases 18, 20):
    `~` General, `!` `#,##0.00`, `@` `h:mm` (`h:mm AM/PM` under en-US), `#` `d-mmm-yy`, `$` the
    built-in currency format localised by the culture (recorded as a culture-localised built-in, as
    the built-in short date is), `%` `0%`, `^` `0.00E+00`; `&` outlines each range, `_` clears every
    edge of the Selection and the same edges read from outside it (case 13).
  - B and 2 bold, I and 3 italic, U and 4 single underline, 5 strikethrough; each toggles, and the
    direction follows the Focus cell (cases 16, 17).
  - Keys are matched by the character they type (`KeyboardEvent.key`) with Ctrl, and whatever Shift
    the layout needs: on a UK layout `#` comes without Shift (case 19; Part B case 29).
- [ ] **Ctrl+1** opens Format Cells once ticket 52 is done. Until then it is not claimed.
- [ ] **While an edit is open**, a formatting key changes nothing (SH-43).
  - It is announced through the live region with its reason, and raised to the Consumer.
  - Ctrl+U is claimed, so the page's source does not open.
  - Ctrl+1 is one of them: Excel opens a Font-only Format Cells there, for rich text ExSheet does
    not have (case 21).
- [ ] Layer 2; Layer 3 for Ctrl+U and for the toggles.
