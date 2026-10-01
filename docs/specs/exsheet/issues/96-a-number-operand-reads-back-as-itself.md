# 96: A number filter operand reads back as itself in every culture

Status: done

**What to build:** a quiet fault ticket 94 found (ADR-0006's note of 2026-10-01; filter panel, ADR-0009 and
ADR-0023). The condition form reopens a number operand in the current culture's text. Under de-DE, `1234.5` reopens
as `1234,5`. `ParseOperand` reads invariant first, with thousands separators allowed, so OK turns it into 12345. That
is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [x] **An operand reopens in a text that reads back to the same value, in every culture.**
- [x] **A number the user types reads as the user meant it.** Read it in the culture the form shows numbers in, and
      never silently in another culture's separators. A text that reads two ways (`1.234` under de-DE) is refused by
      name, not guessed.
- [x] **Both Chromes' panels use the same reading.** `FilterPanelChoices`, and the MudBlazor panel's `CanApply`.
- [x] **Layers 1 and 2** under en-US, de-DE, fr-FR and ja-JP, named after ADR-0023 and principle 1.

## Comments

2026-10-01, agent cf-83 (ticket 96).

### The reading, in one place

`FilterPanelChoices.ReadOperand(type, text, culture)` returns an `OperandReading`: the value, or an
`OperandRefusal` saying why the text was refused. Both panels read through it.

**A number** is read in the culture the form shows numbers in, the current culture:
- **Grouping is checked.** .NET reads a group separator anywhere: `12,34` under en-US read as 1234,
  and `1234,5` as 12345. A text that carries the culture's group separator must group its integer
  digits as the culture writes them (`N0`), or it is refused as `NotReadable`.
- **Another culture's separators are not read.** `1234.5` under de-DE and `1.234` under fr-FR are
  `NotReadable`, where the old reading took the first as 12345 and the second as 1.234.
- **A space-like group separator** (fr-FR's narrow no-break space) is also typed as a space or a
  no-break space, so those stand for it: `1 234,5` reads as 1234.5.
- **A text that reads as two numbers is refused, never guessed.** Under a culture that groups with a
  dot, such as de-DE, a text with a dot and no decimal comma reads one way there and another where
  the dot is the decimal point. That other reading is the invariant culture's, and the one the
  panels took before. So `1.234`, `1.000` and `12.345` are `ReadsTwoWays`, and the refusal names
  both readings.
  - `1,234` under de-DE is the culture's own decimal comma, 1.234, and not refused.
  - `1,234` under en-US is 1234, as en-US writes it, and not refused.
  - A culture whose group separator is not the dot has no second reading to refuse.

**A date** is read in the invariant culture first, so the ISO form it reopens in (ticket 94) reads
back as itself; otherwise in the culture. **A boolean** and **text** are as before.

**`OperandText(operand, culture)`** is what an operand reopens in:
- a number in the culture's own digits and decimal separator, ungrouped, which `ReadOperand` reads
  back as the same value in the same culture;
- a date or a time in its ISO form.

`ParseOperand(type, text)` is kept, and now reads through `ReadOperand` in the current culture.

### Both Chromes

- **The built-in panel** reopens its operands with `OperandText` and reads them with `ReadOperand`.
  A refused text shows `<p class="ex-popover-refusal" role="alert">` under its field, in the
  built-in Chrome's English (`FilterPanelChoices.RefusalText`), and OK applies nothing:
  - `“1.234” reads two ways: as 1234, and as 1.234. Type 1234 without separators, or 1,234 for the other.`
  - `“1234.5” is not a value of this column as German (Germany) writes it.`
- **The MudBlazor panel:**
  - **The field.** The number field was a `MudNumericField` on the invariant culture. It is now a
    `MudTextField` read by `ReadOperand` in the current culture. Under de-DE it had shown `1234.5`
    and read typed text in invariant separators.
  - **Apply.** `CanApply` is unchanged in shape: Apply waits for an operand. A refused text has no
    operand, so Apply is unavailable while the field shows the refusal as its error text.
  - **The words.** The refusal is the Chrome's own word where `MudGridChrome.Label` gives one, for
    the new ids `operand-not-readable` and `operand-reads-two-ways`, and the core's English with both
    readings otherwise.
- **Ticket 94 missed one fallback, fixed here.** The MudBlazor panel's value list wrote an unformatted
  date with `Convert.ToString(value, CurrentCulture)`. It now reads
  `FilterPanelChoices.ValueText(value, format)`: the Format, or a date's ISO form. That is the text
  the cells and the built-in list show.

### Public shape, for the record

- `FilterPanelChoices.ReadOperand(ColumnType, string, CultureInfo)`, returning `OperandReading`.
- `FilterPanelChoices.OperandText(object?, CultureInfo)`.
- `FilterPanelChoices.RefusalText(OperandReading, string, CultureInfo)`.
- `FilterPanelChoices.ValueText(object, Func<object, string>?)`.
- `OperandReading(object? Value, OperandRefusal Refusal, (object, object)? OtherValue)`, with
  `IsRefused`.
- `enum OperandRefusal { None, NotReadable, ReadsTwoWays }`.
- `MudExGridWords.OperandNotReadable` and `OperandReadsTwoWays`.

### Found, not fixed: a typed date has the same fault

- `ReadOperand` keeps the date reading it inherited: invariant first, then the culture. A reopened
  date reads back as itself, because it is ISO.
- A date the user **types** in the culture's own short form does not: under en-GB, `05/01/2026`
  reads invariant first, as 1 May.
- The same rule as for numbers would read the ISO forms exactly, and anything else in the culture
  alone. This ticket's rule covers numbers, so it is left for a decision.

### Tests

All under en-US, de-DE, fr-FR and ja-JP, and named after ADR-0023 and principle 1.

- **Layer 1:** `OperandReadingTests` (ExGrid.Tests, 43 cases):
  - a number reopens and reads back as itself (ten values per culture), and a date as well;
  - the de-DE case by name;
  - what a typed number reads as in each culture, the space-like separators included;
  - the texts that read two ways;
  - other cultures' separators and wrong grouping;
  - nothing typed;
  - the refusal's words;
  - `ParseOperand` in the current culture, and `ValueText`.
- **Layer 2, the built-in panel:** `NumberOperandTests` (ExGrid.Components, 13). A condition in
  force reopens in the culture's text and OK keeps 1234.5. A typed number reads as the culture
  writes it. A refused text is said by name, with `role="alert"`, and OK applies nothing.
- **Layer 2, the MudBlazor panel:** `MudNumberOperandTests` (ExGrid.MudBlazor.Tests, 14) covers the
  same cases, with Apply unavailable while the field is refused, and the Chrome's own word standing
  in for the core's.
- **Layer 2, the MudBlazor value list:** `MudValueListTextTests` (4) checks that an unformatted date
  is listed in its ISO form.
- **Changed:** `MudFilterPanelTests.The_operand_is_the_types_own_control`. A number's control is now
  a `MudTextField`.
- **Layers 1 and 2 pass:** ExGrid.Tests 1054, ExGrid.Components 1342 (one skipped),
  ExGrid.MudBlazor.Tests 186, ExSheet.Engine.Tests 2334, ExSheet.Components.Tests 594,
  ExSheet.MudBlazor.Tests 44.
- **Layer 3**, targeted on port 5481, headless, project chrome, WebAssembly host:
  `popovers.spec.mjs` (both Chromes' panels, typing `3000000` into the condition and pressing
  Enter) and `date-text.spec.mjs`, 65 passed. The Server host, and `circuit.spec.mjs`'s Server-only
  panel cases, are CI's.
