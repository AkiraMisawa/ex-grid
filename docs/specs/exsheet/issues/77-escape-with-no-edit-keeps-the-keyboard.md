# 77: Escape with no edit open keeps the keyboard on the grid

Status: ready-for-agent

**What to build:** a defect the fifteenth Windows run saw (`verification/2026-10-01-windows-15/report.md`,
"Seen, and not asked"): on `/sheet`, with an edit cancelled by Escape, a second Escape with no edit
open sent DOM focus from the grid to `body`, in all twelve configurations (both Chromes, both
browsers, WebAssembly, Server, Server behind 150 ms). D10 stayed selected, and the next keys reached
nothing. Excel's Escape with nothing to cancel does nothing.

**Blocked by:** None.

- [ ] Find what moves DOM focus on an Escape with no edit open (the key gate, a reclaim of focus, a
      popover's Escape bookkeeping, ADR-0039, or the browser's own default), and make Escape there
      leave the keyboard where it is: on the root, the Selection and the Focus unchanged
- [ ] An Escape that a popover, an Inner Popup, Find or a list takes keeps its meaning (ADR-0039,
      ADR-0051); only the Escape nothing takes is this ticket's
- [ ] Layer 2 where the path is C#'s; Layer 3 on `/sheet` under both Chromes, on both hosts: an edit
      opened by a character, Escape, Escape, then a character: the keyboard is still the grid's, and
      the character opens an edit in the selected cell

## Comments
