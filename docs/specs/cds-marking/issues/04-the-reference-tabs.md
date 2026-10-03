# 04: The Reference tabs

Status: ready-for-agent

**What to build:** `/cds` with the Universe, Single Names, Indices and Official Proxy tabs, read-only
(spec, "Reference Curves").

- **The page:** `MudTabs` with `KeepPanelsAlive`, an ExGrid per tab, and a name picker at start.
- **Auto-quote, on the server:** linear in tenor years between observed Points, flat beyond them,
  flat at a single observed Point, and an error with none.
- **Faint:** an Auto-quoted Point carries `cds-estimated` through the Cell Class. Until ticket 02
  lands, the tab ships without it and the ticket stays open.
- **Change Highlight:** `CellChangedAt` answered from the server's change times, for moves of at
  least the page's threshold. The sample's stylesheet sets the colour token.
- **Indices:** the on-the-run rows first, then every series.
- **The Marked set:** add from Universe; remove from Single Names. A removal asks first when a
  Bespoke Proxy reads the curve; that check is wired once ticket 07 exists.

**Blocked by:** 03 (and 02 for the faint Points)

- [ ] Layer 1: Auto-quote at both ends, with one observed Point and with none
- [ ] Layer 3 on both hosts, stepping the feed: a faint Point is faint, a highlighted Point survives
  a scroll on its cell, adding to the Marked set shows the curve on Single Names
- [ ] No console message or exception on either host
