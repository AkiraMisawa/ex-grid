# 94: A date without a Format shows in an ISO form

Status: done

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

- [x] **The one place that falls back to `ToString()`** (`ExGrid.razor`, the display text with no `Format`) uses
      these forms, written with the invariant culture.
  - Every use of that text follows: the cell, copy's `text/plain`, the value list, the Auto width and the
    editor's opening text (ADR-0006, ADR-0005, ADR-0016).
  - Copy's raw `text/html` is unchanged.
- [x] **A declared `Format` still wins.** Number, Text and Boolean are unchanged.
- [x] **Find what depended on the old text:** demo pages without a `Format` on a date column, tests that pinned
      a culture's date text, and the filter panel's value list. Update them to the new form, and say which in the
      comment.
- [x] **Tests.**
  - Layer 1/2: each type's form under en-US, en-GB and ja-JP, and the same text under all three.
  - Layer 3 in a spec that CI runs.

## Comments

2026-10-01, agent cf-83 (ticket 94).

### What changed

- **The text of a value with no `Format`** is now `DisplayText.Of`, an internal helper of the core:
  - `DateOnly` → `yyyy-MM-dd`;
  - `DateTime` → `yyyy-MM-dd HH:mm:ss`;
  - `DateTimeOffset` → `yyyy-MM-dd HH:mm:ss zzz`;
  - `TimeOnly` → `HH:mm:ss`;
  - anything else → its own `ToString()`, as before.

  It is written in the invariant culture.
- **One place reads it:** `ColumnInfo.TextFor`, the rule `TextOf` and the row both paint by. Through
  it, the new text reaches:
  - the cell;
  - copy's `text/plain`;
  - the Auto width and Size to fit;
  - the editor's opening text;
  - a Source's Find, which reads `TextOf`.
- **Copy's `text/html` is unchanged.** `RawText` still writes `s`, ISO with a `T`, and
  `yyyy-MM-dd'T'HH:mm:sszzz` for an offset.
- **A declared `Format` still wins.** Numbers, text and booleans still show their own `ToString()`.

### What depended on the old text, and what was done

- **The filter panel's value list** had its own copy of the fallback (`PanelText`). It now reads the
  column's `TextFor`, so the list and its search match what the cell shows.
- **The filter panel's condition form had a quietly wrong round trip.**
  - A date operand reopened as `DateTime.ToString()` in the current culture: under en-GB, 5 January
    reopened as `05/01/2026 09:05:07`.
  - `FilterPanelChoices.ParseOperand` reads a date in the invariant culture first, so OK read it
    back as 1 May.
  - The operand box now writes a date in the same ISO form, which reads back as the same date.
- **Demo pages.** These columns declare no `Format` and now show the ISO form. The pages needed no
  change:
  - `DemoData.Columns`' "Trade date" on `/virtual`, `/lifecycle`, `/stretch`, `/shared` and
    `/identity`;
  - `DemoData.ServerColumns`' "As of" on `/fetch`.

  "Trade date" holds midnights, so it reads `2026-01-05 00:00:00`. A page that wants the date alone
  declares `yyyy-MM-dd`, as the ADR says. These pages were left on the default, which they now
  show. `ExGrid.MudBlazor`'s README example has a `TradeDate` column with no `Format`; it now shows
  the ISO form, and its text needed no change.
- **Tests that pinned a culture's date text:** none.
  - The date tests in layers 1 and 2 (`ColumnFormatTests` among them) declare a `Format`.
  - No layer-3 spec reads a date column without one. `declarations.spec.mjs`'s `26/09/2026` is
    Excel's clipboard pasted into a Sheet, and the Sheet's engine formats it.
- **Doc comments that described the fallback as `ToString()`:** `GridColumn.Format`, `CellType`'s
  remarks, `PaintedTextOf` and `ExGridRow.CellText`.

### What did not change, and two things for the orchestrator

- **The Formula Bar** still shows a date without a `Format` as `IFormattable.ToString(null,
  CurrentCulture)`. ADR-0051 decides that the bar shows the full value "in the current culture's own
  spelling".
  - On the Server host, that is the server's culture: the same day-and-month reading this note
    removes from the cell.
  - Whether the bar should follow the ISO form for a date is ADR-0051's to decide, so I left it.
  - ExSheet is not affected: it supplies its own opening text, the Entry.
- **A number operand has the same round-trip fault, and it is unchanged here.**
  - Under a culture with a decimal comma (de-DE), `1234.5` reopens in the condition form as
    `1234,5`.
  - `ParseOperand` reads a number in the invariant culture first, with thousands allowed, so OK
    reads it back as 12345.
  - Numbers are out of this ticket's scope.
- **ExSheet** formats its own text in its engine (ADR-0047, ADR-0071), so nothing in it changed.

### Tests

- **Layer 1:** `DateWithoutFormatTests` in ExGrid.Tests, 10 cases:
  - each type's form under en-US, en-GB and ja-JP, including a midnight and two offsets;
  - the same text under all three, where `ToString()` read three ways;
  - a declared `Format` wins;
  - numbers, text and booleans are their own `ToString()`.
- **Layer 2:** `DateWithoutFormatTests` in ExGrid.Components, 18 cases, under each of the three
  cultures:
  - the cells;
  - copy's `text/plain`, and its `text/html` unchanged;
  - the value list;
  - the editor's opening text;
  - the Auto width;
  - the date operand reopening in ISO form and reading back as the same date.
- **Layer 3:** `date-text.spec.mjs`. `/virtual`'s "Trade date" reads `yyyy-MM-dd HH:mm:ss` in browser
  contexts whose locale is en-US, en-GB and ja-JP, and the same text under all three.
  - Run locally on port 5481, headless, project chrome, WebAssembly host: 4 passed. The host was
    stopped afterwards.
  - The Server host is CI's.
- **Layers 1 and 2 pass:** ExGrid.Tests 1011, ExGrid.Components 1318 (one skipped),
  ExGrid.MudBlazor.Tests 168, ExSheet.Engine.Tests 2334, ExSheet.Components.Tests 592,
  ExSheet.MudBlazor.Tests 43.
