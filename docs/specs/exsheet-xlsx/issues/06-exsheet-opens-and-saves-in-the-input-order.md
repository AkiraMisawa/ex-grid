# 06: ExSheet opens and saves a file at its place in the Input Order

Status: done
Roadmap: ExSheet — Opening a file is shown while under way, can be cancelled with Escape, and a key typed meanwhile lands on the opened Workbook.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §9 and §10.

- [x] A method replacing the Workbook with one still being read, taken at its place in the Input Order; inputs behind it taken against the new Workbook
- [x] Said aloud while under way; Escape cancels; a refused or cancelled read leaves the Workbook as it was
- [x] On landing: the undo stack cleared, an open edit discarded and announced
- [x] Saving at its own place, refused by name while an edit is open
- [x] Saving takes a snapshot of the Workbook first (ticket 04 reads it whole, synchronously, before the first yield) and
      builds the file from it while yielding, with progress and cancellation, as ADR-0330 §9 does for a read
- [x] Bound the rows a row format writes: a row format covering every row writes about a million `<row>` elements, which
      `XlsxSaveOptions.MaxCells` does not count (only `MaxUncompressedBytes` bounds them today)
- [x] Docs Site page and Example (open, Open Report, save) with the Built-in / MudBlazor switch; layer 2 and layer 3 at machine speed on both hosts; Change Note
- [x] The page says ticket 05's Linked Tables: each is saved as an Excel Table on a worksheet of its name, and a file that
      holds one opens in Excel, not in ExSheet, until ExSheet reads Excel Tables (ADR-0330 §6, X1)

**Blocked by:** 02, 04, and input-order ticket 04.

**Settled while building (2026-10-21):** the method is `ExSheet.OpenAsync(Func<CancellationToken, Task<WorkbookDocument>> read)`,
answering `true` once the document opened and `false` when Escape cancelled the read, and saving is
`ExSheet.SaveAsync(Func<Workbook, Task> write)`. ExSheet references no file format (ADR-0330 §2): the application
hands over `XlsxWorkbookReader.ReadAsync` and `XlsxWorkbookWriter.WriteAsync`, and keeps the Open Report the read
answers. *(Both shapes were chosen here, as the ADR's "a method" and "writing … at its own place" asked for nothing
more; flagged for the user to confirm.)*

- **The grid's seam gains work of the Consumer's own** (ADR-0240 §6, as ExSheet is ExGrid's Consumer and reaches
  only its public surface): `ExGrid.InOrderAsync(handling)` takes a place after every input received so far and
  holds the inputs behind it, claiming no number told for a control's event; `ExGrid.CancellableInOrderAsync(handling,
  cancelledTooLate)` does the same and, while it runs, marks the root `aria-busy` and `data-ex-work` (its number),
  paints it with the paste's tokens (`--ex-pasting-*`, beside `.ex-pasting` in `ex-grid.css`) and keeps the Settled
  Mark off (`data-ex-owing`). `InputOrder.AfterReceivedAsync` gained `holds`: work that found the order idle used to
  run beside it, outside every flow, so a key typed while it awaited was not held.
- **Escape** is ADR-0240 §12's, decided by what the Escape was taken against, read at its keydown (reviews of
  2afdfde1 and 820dcdf0..e5cf5121): from the moment work takes its place until it ends — waiting for its turn or
  running — the Settled Stamp names the latest such work (`data-ex-work-placed`) and drops `data-ex-escape-leaves`.
  The key gate's capture `keydown` (ADR-0021's first entry, no new listener) reads it there, held or not: a plain
  Escape (no modifier, no repeat) typed against a stamp that names work is numbered there as a cancel naming it and
  heard at once (`OnWorkCancelAsync`); one typed against a stamp that names none is an ordinary key (`OnKeyAsync`),
  whatever has been placed by the time it arrives, and never cancels. The core decides from the number alone: the
  first work placed up to it that is not cancelled is cancelled, and the Escape does nothing else; when it is
  cancelled already, the Escape is consumed and answered as the work ends; when it has ended, the Escape is
  answered at once. In the Escape's turn `cancelledTooLate(true)` says the work had landed, `cancelledTooLate(false)`
  that it found nothing to cancel (failed, refused, cancelled, or already over), so a second Escape gets the same
  answer whether it arrives before the work ends or after. The root's `aria-busy` and `data-ex-work` say the work's
  turn has started; the stamp says it is placed. One cancellable work inside another is refused; disposing of the
  grid cancels every work placed (iterating a copy: a cancel runs the token's callbacks inline); both seams marshal
  onto the renderer. An opening an Escape cancelled before its turn ends cancelled, even if an edit was opened
  meanwhile.
- **An opening while an edit is open is refused** (`SheetRefusedException`, `EditIsOpen`, said), as a save is
  (review of 2afdfde1, chosen by the user); the edit stays open.
- **The point of landing.** ExSheet looks at the token once more after the read answers, then opens the document in
  the engine and lands it; a document the engine refuses leaves the Workbook as it was, said. Landing is tracked by
  ExSheet itself, so an exception a `WorkbookChanged` handler throws afterwards — even an
  `OperationCanceledException` — is `OpenAsync`'s and never reads as a cancel. The Workbook, undo stack and views are
  replaced under a new Row Sequence Version; the Linked Tables declared go with the Workbook replaced. A disposed
  Sheet lands nothing and raises nothing.
- **The save does not hold the order for its I/O.** The writer is called in the save's turn and reads the Workbook
  before its first yield; the inputs behind the save are taken once the call returns its task. A save while an
  edit is open is a `SheetRefusedException` (`EditIsOpen`) with a sentence of its own, nothing handed to the writer.
- **The writer's snapshot, then slices.** `XlsxWorkbookWriter.WriteAsync` now takes, synchronously, every Sheet's
  document, the Value of every cell holding one, its spills and each Linked Table's rows, and builds the file from
  that alone, a slice at a time (`SavePacer`, as `ExGrid.Data.Xlsx`'s `Pacer` paces a read). `XlsxSaveOptions`
  gained `Progress` (`XlsxSaveProgress`: worksheet, worksheets, elements written of those the file writes; Sheet -1
  while the snapshot is planned), `SliceBudget` and `Yield`; the token is looked at between batches, and a write
  cancelled while it is built leaves the stream empty. Layer 1 in `tests/ExSheet.Xlsx.Tests/SavePacingTests.cs`.
- **Rows bounded.** `XlsxSaveOptions.MaxCells` counts each row a row format writes as an element, holding a cell or
  not (`SheetFormatPlan.FormattedRows`), so a row format over every row costs 1,048,576 of the budget and ten such
  Sheets are refused by name before anything is written. One limit rather than a second one, since the ticket named
  `MaxCells` as what missed them; the reader still counts cells only, so a file within it always opens again.
- **No JavaScript for the file itself.** Choosing a file is Blazor's `InputFile`; the Docs Example and `/xlsx`
  hand the saved bytes to a `data:` link the user clicks, since the Docs Site's JavaScript list (ADR-0110) has no
  download. A one-click download would need an entry there.
- `/xlsx` on the DemoHost (`?hold=1` holds the read until a button lets it go, so the opening is under way with no
  clock) and `tests/ExGrid.Browser/xlsx.spec.mjs` (XL-1 to XL-3, both Chromes); layer 2 in
  `tests/ExGrid.Components/WorkUnderWayTests.cs` and `tests/ExSheet.Components/OpenAndSaveInOrderTests.cs`.
