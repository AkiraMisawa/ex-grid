# 25: An edit stands when the keyboard leaves the grid, and a press brings it back

Status: ready-for-agent

**What to build:** ADR-0018, section 6 (2026-09-29), with ADR-0021's note of the same day, for ExGrid
as a whole. Found on `/sheet`: `=` typed into a cell, then a click on the positions grid. The Sheet's
edit stayed open and nothing reached it again. Escape went to the positions grid, and a click back on
the Sheet's rows pointed while the keyboard stayed with the positions grid.

**Blocked by:** None (can start immediately)

- [x] Losing DOM focus neither commits nor discards an open edit: to another grid, to a control on
      the page, or to nothing (ED-26)
- [x] Escape and other keys pressed in another grid are that grid's; this grid's edit stays open
      (ED-26, KB-1)
- [x] A press on the rows or the headings while an edit stands here and DOM focus is outside the root
      puts the keyboard into the editor surface that last held it, in the capture-phase `mousedown`,
      before the press is handled (ADR-0021 note)
- [x] After that press, a press that points leaves the keyboard in the edit's surface and the next
      Escape cancels this edit; a press that commits leaves it on the root (ED-26)
- [x] The Formula Bar case: an edit last typed in the bar gets the keyboard back in the bar
- [ ] Held keys and held presses (ED-22) still keep their order when the keyboard comes back
- [x] The comment on `reclaimFocus` no longer calls itself the one decision about focus made in
      script
- [x] No JavaScript use is added; the listener stays the allowlisted `mousedown` (ADR-0021)
- [ ] Layer 3 on `/sheet` and `/sheets`, both hosts (ED-26)
- [x] A press on the rows while an edit is open holds the keys typed after it until the core has
      answered it, and hands them on against the mode the answer leaves: on a plain editable grid and
      on a Sheet, a press that commits or points. Plain navigation is not held, and a double click on
      another cell still commits and opens that cell's text (ADR-0010 widened 2026-09-29, ADR-0021,
      ED-22)
- [x] While DOM focus is outside the root, the Cell Editor's outline is 1px wide in
      `--ex-editor-outline`'s style and colour, and at full width once the keyboard returns, under
      both Chromes, by the stylesheet alone (ADR-0018 section 6, ED-27)
- [ ] Layer 3 for both, on both hosts (ED-22 widened, ED-27)

## Comments

**2026-09-29, implementation (branch `claude/exsheet-edit-stands`).** Nothing in C# or in
`ex-grid.js` ended an edit on a focus loss; `EditStandsTests` pins that nothing the edit is drawn
with hears a blur or a focusout. The capture-phase `mousedown` now focuses the surface that last
held the keyboard — the one in which the listener last saw a key, an input or a press, taken
afresh as an edit opens and forgotten as it ends; the first `.ex-editor` in the markup when none
is known — for a press on this grid's own rows or headings while an edit is open and DOM focus is
outside the root. A column heading's press does not suppress its default, so it already reached
the root; a Row Heading's is a press on the rows and did not.

Found on the way, and fixed with it: an edit typed in the Formula Bar and committed by a press on
the rows of a grid that declares pointing left DOM focus in the bar, with no edit open — the core
suppresses that press's default (ADR-0051), and the hand-back declined a field with focus of its
own. Typing then showed in the bar and went nowhere (`127` in the bar, B2 untouched). It happened
without leaving the grid too; ED-26 asks for the root after a committing press back, which is the
same path. The bar's focus is now marked as left standing by the press, as `staleField` already
did for a held press, and the hand-back takes it.

The last box stays open until the runner has passed `edit-stands.spec.mjs` on both hosts: it was
written against, and executed through a stand-in harness on, private DemoHosts (Server behind the
latency proxy, and WebAssembly), headless Chrome on macOS — 16/16 on each, 0/16 against the module
before this ticket.

Seen and left alone, since each happens the same without leaving the grid, or before an edit
stands, and changing it needs a decision:

- Keys typed straight after a press on the rows that commits, in a grid that declares pointing,
  before the hand-back has landed, are lost: they go into the Cell Editor the commit is removing,
  then to `body` (`99` in C4, B2 pressed, `7` and Enter at once: B2 unchanged and the Focus still
  on B2 — on the Server host at 150 ms whether or not the keyboard had left, and at 0 ms when it
  had not). ED-22 holds keys behind a key or a held press, not behind a press that passed.
- A press back within one round trip of the key that opens the edit (or of any key still being
  answered, with no edit open yet) is held, and a held press suppresses its default: DOM focus
  stays in the other grid, and the keys typed next go there.

**2026-09-29, after review.** The box for held keys and held presses is open again. No test covers
keys or a press held as the keyboard comes back, and the second case above is exactly where it does
not come back: a press back within one round trip of the key that opens the edit is held, its
default is suppressed, and the keys typed next go to the grid the user left. That case was put to
the user as a round-trip gap, and ADR-0018 section 6 now records it as accepted (381d4a1). The
first case above, keys lost after a press that commits, is decided as ED-22 widened (ADR-0010,
the same commit) and is not built under this ticket.

A column heading's press does not suppress its default: DOM focus goes on to the scroller and from
there to the root, as it did before this ticket. The listener's `focus()` into the edit's surface
is therefore a no-op there; it matters for a Row Heading's press, which is a press on the rows.

Fixed from the review: the surface the keyboard comes back to is one of this grid's own, never an
editor of a grid nested in one of its cells, and one helper finds it for the return and for the
field a held key is typed into; the Formula Bar's mark left by a press that passes on lasts only
until the core has answered that press, so a press that points does not leave it for a later
hand-back; and the /sheet setup and the positions grid's locator live in `sheet-helpers.mjs`.

**2026-09-30, the hold behind a press and the standing edit's look.** The box for held keys and held
presses as the keyboard comes back stays open, and is Q24's: the one case where the keyboard does
not come back — a press back within one round trip of the key that opens the edit, held behind that
key with its default suppressed — was accepted by the user as a one-round-trip gap (ADR-0018 section
6, last bullet), and the rest has no test of its own. The first case recorded on 2026-09-29, keys
lost after a press that commits, is built now as ED-22 widened.

- **The hold.** The capture-phase `mousedown` starts it for a press on the rows while an edit is
  open; the press passes on untouched, the keys after it are held, and the drain waits for the
  core's answer to the press before anything else. The question goes after the press has reached
  the core and ahead of its release — from a later task, or from this grid's own capture `mouseup` —
  because the core answers for the press or release it heard last; its answer also takes off the
  mark the press left on a Formula Bar. A press while only a press is being answered, with no key
  held behind it, passes on too, so the second press of a double click keeps its place before the
  double click.
- **The editor's removal.** With the hold alone, 3 in 20 runs on the Server host at 0 ms still lost
  the `7`: the edit's end asked for the hand-back only after the gate's answer, while the render that
  removes the editor went out first, and DOM focus sat on `body` in between, where no listener of
  the root hears a key. `EndEditingAsync` now makes both calls before it yields, as a popover's close
  does. 0 in 30 at 0 ms on each host, and 0 in 15 at 150 ms on the Server host.
- **The look.** One rule, `.ex-grid:not(:focus-within) .ex-viewport .ex-editor { outline-width:
  1px; }`. `/sheets` takes `?chrome=mud`, each Sheet on the Wrapper's paper, where the token is 2px of
  the palette's primary; the colour is unchanged at 1px under both Chromes.

Layer 2: `EditStandsTests` (the core's half of the hold, and the hand-back asked while the editor
still stands) and `ShippedStylesheetTests` (the hold's and the rule's shape). Layer 3:
`edit-stands.spec.mjs`, executed through a stand-in harness on private DemoHosts, headless: 26/26 on
the Server host and 22/22 on WebAssembly (the four 150 ms cases skip themselves there). Against the
module before the hold, the committing cases fail on the Sheet at 0 and 150 ms and on `/features` at
150 ms. The runner has not run it.

**2026-09-30, second review.** The hold behind a press is scoped to this grid's own rows: a press on
the rows of a grid nested in one of its cells neither starts this grid's hold nor is held by it, and
never asks this core about a press it did not hear (the held-press path had the same bare check, and
is scoped with it). Layer 3 covers it with a stand-in for a nested grid inside the Sheet: at 150 ms
on the Server host the key typed after a press on its rows reaches it as the browser's own keydown,
where before this fix it was held and handed on from script (red 3 of 3 against the previous
module). Two claims of the boxes above now have tests of their own on `/features`: outside an edit a
press on the rows and an arrow hold nothing (a page listener hears the key typed after them), and a
double click on another cell commits the edit and opens that cell's text on a plain grid, whose
press keeps its default, as on the Sheet, whose press does not.

Known limit of the standing edit's look, not changed: while the keyboard is in a grid nested inside
this one, this grid's root still matches `:focus-within`, so its standing edit keeps the full-width
outline.

