# 95: The Formula Bar shows an unformatted date in its ISO form

Status: ready-for-agent

**What to build:** ADR-0006's note of 2026-10-01, found by ticket 94. On a display grid, the Formula Bar's full
value (`GetFocusedValue`, the role behind `####`, ADR-0016) is shown in the current culture's spelling. On the
Server host that culture is the server's, which brings back the day/month confusion that ticket 94 removed from
the cell.

**Blocked by:** None (can start immediately)

- [ ] **When a column has no `Format`, the Formula Bar shows `DateOnly`, `DateTime`, `DateTimeOffset` and
      `TimeOnly` values in ticket 94's forms** (`DisplayText.Of`). A declared `Format` and every other type are
      unchanged. ExSheet's own text is unchanged.
- [ ] **Read ADR-0051 on what the Formula Bar shows for a display grid.** Say in the comment which sentence this
      refines.
- [ ] **Layer 2 under en-US, en-GB and ja-JP. Layer 3 in a spec that CI runs.**
