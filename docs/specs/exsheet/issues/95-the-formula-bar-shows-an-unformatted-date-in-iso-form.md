# 95: The Formula Bar shows an unformatted date in its ISO form

Status: done

**What to build:** ADR-0006's note of 2026-10-01, found by ticket 94. On a display grid, the Formula Bar's full
value (`GetFocusedValue`, the role behind `####`, ADR-0016) is shown in the current culture's spelling. On the
Server host that culture is the server's, which brings back the day/month confusion that ticket 94 removed from
the cell.

**Blocked by:** None (can start immediately)

- [x] **When a column has no `Format`, the Formula Bar shows `DateOnly`, `DateTime`, `DateTimeOffset` and
      `TimeOnly` values in ticket 94's forms** (`DisplayText.Of`). A declared `Format` and every other type are
      unchanged. ExSheet's own text is unchanged.
- [x] **Read ADR-0051 on what the Formula Bar shows for a display grid.** Say in the comment which sentence this
      refines.
- [x] **Layer 2 under en-US, en-GB and ja-JP. Layer 3 in a spec that CI runs.**

## Comments

2026-10-01, agent cf-83 (ticket 95).

### What changed

- **`FormulaBarText`** is the bar's text when no edit is open and the Consumer supplies no opening
  text. Where the column declares no `Format`, a `DateOnly`, `DateTime`, `DateTimeOffset` or
  `TimeOnly` value now shows in ticket 94's form (`DisplayText.Of`), the text its cell shows.
- **Unchanged:**
  - every other type, still `IFormattable.ToString(null, CurrentCulture)`, so a number keeps the
    current culture's separators and its full precision;
  - a date in a column that declares a `Format`, still the full value in the current culture;
  - `GetFocusedValue`, which still hands the raw value to a Chrome.
- **ExSheet is unaffected.** It supplies the Entry as its opening text.
- **The MudBlazor Chrome's bar** paints the same text: the core hands it over in
  `FormulaBarTextContext`.

### Which sentence of ADR-0051 this refines

- ADR-0051, "The Formula Bar and the Name Box", says the bar holds "the Focus cell's text (the Entry
  on a Sheet, **the full value on a display grid**, which is ADR-0016's display)".
- The code's comment added "in the current culture's own spelling".
- For an unformatted date or time, the full value is now spelled in its ISO form by type, as ADR-0006's
  note of 2026-10-01 records. The value is still the full one: a `DateTime` keeps its seconds and
  its time. Only its spelling no longer follows the culture.

### Demo page

- `/virtual?bar=1` switches the Formula Bar on (`ShowFormulaBar`), so layer 3 can read the bar on a
  display grid.
- `/virtual` without the parameter is unchanged.

### Tests

- **Layer 2:** `DateWithoutFormatTests` (ExGrid.Components), under en-US, en-GB and ja-JP:
  - the bar shows each type's ISO form, the same text as the cell;
  - a formatted date, and a number, keep the current culture's full value.

  That is six new cases.
- **Layer 3:** `date-text.spec.mjs`. Under browser locales en-US, en-GB and ja-JP, it presses
  `/virtual?bar=1`'s first Trade date. The bar shows `yyyy-MM-dd HH:mm:ss`, the cell's own text, and
  the same under all three.
  - Run locally on port 5481, headless, project chrome, WebAssembly host: the file's 8 tests passed.
  - The Server host is CI's.
- **Layers 1 and 2 pass:** ExGrid.Tests 1011, ExGrid.Components 1329 (one skipped),
  ExGrid.MudBlazor.Tests 168, ExSheet.Engine.Tests 2334, ExSheet.Components.Tests 594,
  ExSheet.MudBlazor.Tests 44.
