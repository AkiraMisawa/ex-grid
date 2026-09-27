# 14: The clipboard, and the paste that spills

Status: ready-for-agent

**What to build:** Inside ExSheet, a copy carries Entries and relative References shift on paste. Outward it
carries Values as unformatted text. Inward, each field is parsed as if typed. The third ADR-0050
declaration arrives: a paste may spill from a single cell. The pasted block then becomes the
Selection, a spill past the extent is refused by name, and `Editable` is checked on the block
first. A paste is one undo step.

**Blocked by:** 03, 05, 12

- [ ] Copying `=A1` from B1 to B2 pastes `=A2` (ADR-0048)
- [x] Copying to another program gives the Values (ADR-0005)
- [x] Pasting `=A1+1` and `1,234` from outside makes a Formula and a number under the culture
- [x] A 3×3 block pasted onto one cell writes 3×3 and selects it (ADR-0050)
- [x] Without the declaration, ExGrid still refuses range → one cell (ADR-0014)
- [x] A spill past the edge is refused by name

## Comments

The core half, 2026-09-27: the third ADR-0050 declaration (DC-8, DC-9, DC-10).

- **`ExGrid.PasteMaySpill`** (`bool`, off by default). Declared, a block of several cells
  pasted onto one cell raises one `OnPaste` intent of the existing shape, whose plan is the
  block with that cell at its top-left. The block then becomes the Selection, with the Anchor
  and the Focus at its top-left. If handling the intent moved the Row Sequence Version, the
  block is not placed (ADR-0011).
- **`ClipboardRules.PlanPaste(..., GridExtent? spillWithin = null)`** is the pure half.
  `Editable` is judged on the block's columns first, then a block crossing the last row or
  column is refused as the new **`PasteRefusalReason.SpillPastExtent`**. It is never clipped.
  "May not" comes before "cannot", in ADR-0035's order. Every other ADR-0014 rule stands, and
  without the declaration range → one cell is still `SingleCellTarget`.
- No JavaScript was added.

Layer 1: `SpillPasteTests`. Layer 2: four tests at the end of `ClipboardWiringTests`.

What remains, for ExSheet:

- Declaring `PasteMaySpill`, and resolving the intent: Entries inside the Sheet, with relative
  References shifted (ADR-0048), Values outward, and fields parsed as if typed inward. The
  first three checkboxes are ExSheet's.
- Making a paste one undo step (ticket 12).
- ExSheet's wording for `SpillPastExtent`.
- A real clipboard paste that spills, in layer 3.

2026-09-27, engine half: `Sheet.Copy(range)` returns a `SheetCopy` holding the Entries as a
`SheetBlock` (Entry, number format and alignment per cell, and the place they came from) and
the Values in the two flavours ExGrid puts on the clipboard: `Text`, each Value as the cell
shows it, TSV with Excel's quoting and CR LF lines, and `Html`, a table of the unformatted,
invariant Values; a Value its format cannot show goes out as itself, never as `####`. A range
holding a `#GETTING_DATA` Value is refused whole with `SheetRefusalReason.WaitingForData`
(ADR-0049; tested with ticket 16). `SheetEdit.Paste(block, origin)` writes the block with every
relative Reference shifted by the distance pasted, absolute parts unmoved, a range written from
its top-left, and a Reference shifted off the Sheet written `#REF!`; formats and alignment go
with it, and a blank cell of the block clears what it lands on. `SheetEdit.Paste(block,
target)` repeats a block over a whole multiple of itself. `SheetEdit.PasteText(fields, origin)`
takes each field as if typed under the Sheet's culture (`=A1+1` a Formula, `1,234` a number
under `en-US`, a date with its implied format). A block that would run past the Sheet's edge is
refused by name (`BlockWouldLeaveSheet`), and each paste is one `SheetStep`
(`ClipboardTests`). Covers the engine side of the first three criteria and the sixth. **What
remains is the component's:** the clipboard itself and telling its own copy from another
program's, the spill declaration and the pasted block becoming the Selection, `Editable` on the
block, ExGrid's unchanged refusal without the declaration, and layer 3 with the real
clipboard. A pasted field beginning with `=` that cannot be read refuses the whole paste
(`FormulaSyntaxException`); what Excel does with such a field is not pinned, reported for a
decision.

2026-09-27, ExSheet wiring, the paste half. ExSheet declares `PasteMaySpill`. `OnPaste` takes
every target cell's field, through the plan's tiling, as if typed under the Sheet's culture. That
is one `SheetEdit.Enter` of all the cells, so one step on the undo stack. An intent under any Row
Sequence Version but 0 is dropped (ADR-0011). A pasted field beginning with `=` that cannot be
read refuses the whole paste and says where. `OnPasteRefused` and `OnCopyRefused` put ExSheet's
sentence for each reason in the notice, including `SpillPastExtent` ("the block would run past
the Sheet's edge"). `OnCopyRowsNeeded` answers a copy that runs beyond the Window from the Sheet,
so such a copy is no longer refused as `RowsUnavailable`. A defect was fixed along the way. The
grid builds a copy's `text/html` flavour from each value's invariant form, and ExSheet's cell
value had none, so the clipboard carried `SheetCellText { Text = …, IsNumber = … }`. The value is
now `IFormattable` and gives the unformatted Value as the engine writes it (`Value.ToString()`,
which is what `SheetCopy.Html` holds). Layer 2, in `ClipboardWiringTests`: `=A1+1` and `"1,234"`
typed, a 3×3 spill selected with the Focus at its top-left, one undo, one value over a range, the
spill past the edge, an unreadable Formula, both flavours of a copy, and a copy of 1,000 rows.

**Blocked on the core: the copy half (the first criterion, and the second in full).** ExGrid puts
a copy together itself. `BuildCopyPayload` and `BuildCopyPayloadAsync` (ExGrid.razor) call
`ClipboardData.Assemble` over the columns' `Format` (text) and value (HTML). No parameter lets a
Consumer:

1. learn that a copy was made, and of which range;
2. supply the two flavours (`SheetCopy.Text` and `SheetCopy.Html`);
3. refuse a copy with its own reason. `SheetRefusalReason.WaitingForData` has no path to the
   user, and `CopyRefusalReason` has no Consumer member.

So ExSheet cannot keep the `SheetCopy` it would compare a later paste against, and a copy
reaching a `#GETTING_DATA` cell goes out as that text (ADR-0049 says it is refused). A cell whose
number cannot be shown goes out as the run of `#` it paints, where the engine writes the Value
(ADR-0016). Proposal: an opt-in declaration in ADR-0050's pattern, such as
`Func<CopyPlan, CopyAnswer>? CopyPayload`. It would be asked synchronously on the same side, on
both routes. Its answer would be both flavours, or a refusal carrying the Consumer's sentence.
With it, ExSheet answers `Sheet.Copy(range)` and keeps the `SheetCopy`. On paste it recognises its
own copy by comparing `intent.Values` with `ClipboardParse.Parse(copy.Html, copy.Text)`, which is
the same parse the grid applied. A browser clipboard carries no owner, and equality of the parsed
block is the strongest evidence a page can have. A false match would need another program to have
put exactly the same block there, and the paste then writes the Entries that showed exactly those
Values. On a match, ExSheet writes `SheetEdit.Paste(copy.Block, …)`.

**Reported for a decision: inward text under a culture whose decimal separator is not `.`.** The
grid prefers the HTML flavour, and there Excel's `x:num`, and ExSheet's own HTML, are invariant
(`1234.5`). Text content is shown text, and `GridPasteIntent.Values` does not say which of the two
a field is. Taken "as if typed" under `de-DE`, an invariant `1234.5` does not read as 1234.5.
Either the intent says which flavour each field came from, or ExSheet refuses a field that reads
differently as invariant and as typed. Under `en-US` and `ja-JP` the two readings agree.

**Also reported:** a single value pasted over whole columns writes a million Entries in one step.
Formatting has an interim cap (`FormatCellCap`); a paste has none.

2026-09-27, ExSheet wiring, the copy half (DC-32, ADR-0050 item 9). ExSheet declares ExGrid's
`CopyAnswer`, so both copy routes ask it synchronously and no longer gather rows
(`OnCopyRowsNeeded` is gone: the engine reads every row). One range is the engine's
`Sheet.Copy`: `text/plain` is `SheetCopy.Text` as the engine writes it, and `text/html` is
`SheetCopy.Html` with its table marked `data-ex-grid="invariant"` (ExGrid's
`ClipboardData.InvariantMarker`), so ExGrid's paste reads its fields as invariant. The copy keeps
the `SheetBlock` and the fields `ClipboardParse.ParseBlock(html, text)` reads back from what was
written, for the paste to recognise. A copy reaching `#GETTING_DATA` is refused with the engine's
sentence (`GridCopyAnswer.Refuse`); the grid announces it, ExSheet's notice says it, the
clipboard keeps what it held, and the Entries kept for the last copy stand. Several ranges
combined into one block, and a copy with the headers (the column letters), carry the Values cell
by cell, as the engine writes them, and keep no Entries: a block of several ranges was never one
place its References were relative to. Layer 2, in `ClipboardWiringTests`: both routes, the
Entries kept, the refusal in the engine's words and the last copy kept past it, a Value no format
can show going out as itself, and several ranges. The second criterion holds in layer 2; layer 3
with the real clipboard is still open.
