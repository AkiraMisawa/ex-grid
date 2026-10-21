# 06: ExSheet opens and saves a file at its place in the Input Order

Status: ready-for-agent
Roadmap: ExSheet — Opening a file is shown while under way, can be cancelled with Escape, and a key typed meanwhile lands on the opened Workbook.

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §9 and §10.

- [ ] A method replacing the Workbook with one still being read, taken at its place in the Input Order; inputs behind it taken against the new Workbook
- [ ] Said aloud while under way; Escape cancels; a refused or cancelled read leaves the Workbook as it was
- [ ] On landing: the undo stack cleared, an open edit discarded and announced
- [ ] Saving at its own place, refused by name while an edit is open
- [ ] Saving takes a snapshot of the Workbook first (ticket 04 reads it whole, synchronously, before the first yield) and
      builds the file from it while yielding, with progress and cancellation, as ADR-0330 §9 does for a read
- [ ] Bound the rows a row format writes: a row format covering every row writes about a million `<row>` elements, which
      `XlsxSaveOptions.MaxCells` does not count (only `MaxUncompressedBytes` bounds them today)
- [ ] Docs Site page and Example (open, Open Report, save) with the Built-in / MudBlazor switch; layer 2 and layer 3 at machine speed on both hosts; Change Note

**Blocked by:** 02, 04, and input-order ticket 04.
