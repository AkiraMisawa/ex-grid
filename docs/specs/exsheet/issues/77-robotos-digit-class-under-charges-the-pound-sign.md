# 77: `ExGrid.MudBlazor` charges `£` below its width

Status: ready-for-agent

**What to build:** a fix found by ticket 47. `ExGrid.MudBlazor` declares Roboto's regular digit class
at 8.0px. `£`, which is in that class, measured 8.281px at weight 600. A number with a `£` can then be
judged to fit when its last pixels do not, and it is cut instead of shown as `####`. That is quietly
wrong (principle 1; ADR-0016, ADR-0030).

**Blocked by:** None (can start immediately)

- [ ] Re-measure Roboto's character classes at the weights `ExGrid.MudBlazor` paints (400 and 600 for
      regular, 700 for bold), as §21.7a measured the core's: Chrome, 14px, tabular digits, each
      class's glyphs.
- [ ] Each class declares its widest glyph, or the glyph moves to the class that covers it.
- [ ] A layer-1 test with `£1,234,567.50` at the width where the old value fits and the true one does
      not, named after ADR-0016 and ADR-0030.
- [ ] Say in the comment whether `€`, `¥` and `%` were checked too.
