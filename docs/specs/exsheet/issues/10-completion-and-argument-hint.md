# 10: Completion and the argument hint

Status: ready-for-agent

**What to build:** The core reports the editor's text and caret as the user types (a Blazor input event). The
Consumer answers with candidates and a hint. Chrome paints the list as the editor's Inner Popup,
inside the grid's box, and the hint beneath it. ↑/↓ choose, Tab accepts, and Escape closes the list
before it cancels. ExSheet supplies the function list and, once ticket 16 lands, the Linked Tables'
names. Candidates that come back for text that has since changed are dropped.

**Blocked by:** 04, 09

- [ ] `=SU` offers `SUM` and `SUMIF`-style candidates from the declared list; Tab accepts (ADR-0051)
- [ ] Escape closes the list and leaves the edit open
- [ ] The list stays inside the grid's box (ADR-0040) under the built-in Chrome and `ExGrid.MudBlazor`'s
- [ ] A stale candidate list is never shown (layer 2 with a delayed answer)
- [ ] Works from the Formula Bar as from the cell

## Comments
