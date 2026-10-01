# 94: A date without a Format shows in an ISO form

Status: ready-for-agent

**What to build:** ADR-0006's note of 2026-10-01, decided with the user. ExGrid's text for a value with no
`Format` is its own `ToString()`, which depends on the culture the code runs under. On the Server host that culture
is the server's. Dates and times get one form instead, by type:

| Type | Form |
|---|---|
| `DateOnly` | `yyyy-MM-dd` |
| `DateTime` | `yyyy-MM-dd HH:mm:ss` |
| `DateTimeOffset` | `yyyy-MM-dd HH:mm:ss zzz` |
| `TimeOnly` | `HH:mm:ss` |

**Blocked by:** None (can start immediately)

- [ ] **The one place that falls back to `ToString()`** (`ExGrid.razor`, the display text with no `Format`) uses
      these forms, written with the invariant culture.
  - Every use of that text follows: the cell, copy's `text/plain`, the value list, the Auto width and the
    editor's opening text (ADR-0006, ADR-0005, ADR-0016).
  - Copy's raw `text/html` is unchanged.
- [ ] **A declared `Format` still wins.** Number, Text and Boolean are unchanged.
- [ ] **Find what depended on the old text:** demo pages without a `Format` on a date column, tests that pinned
      a culture's date text, and the filter panel's value list. Update them to the new form, and say which in the
      comment.
- [ ] **Tests.**
  - Layer 1/2: each type's form under en-US, en-GB and ja-JP, and the same text under all three.
  - Layer 3 in a spec that CI runs.
