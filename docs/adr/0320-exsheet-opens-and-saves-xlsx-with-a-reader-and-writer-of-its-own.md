# ExSheet opens and saves `.xlsx` with a reader and writer of its own

*(Proposed 2026-10-20, not yet decided. The user chose two things on 2026-10-20: `.xlsx` starts with
ExSheet's opening and saving, ahead of reading a sheet into a Snapshot for ExGrid and ExPivot, and the
reader and writer are the project's own, with no third-party dependency. Everything else below is a
recommendation put to the user as a question; the "Questions" section lists them. This ADR is
rewritten as decided once they are answered.)*

[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md) and
[ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md) left
`.xlsx` to "a separate package when it comes", and
[ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) left theme colours
to it. This ADR says what that package is, what crosses between a Sheet Document and a workbook, what
is refused, and how opening a file takes its place in the Input Order
([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)).

## Why our own reader and writer *(decided with the user, 2026-10-20)*

- **The format is a zip of XML parts, and the BCL reads both.** `System.IO.Compression` and
  `System.Xml`'s `XmlReader`/`XmlWriter` are enough, so the package adds no dependency, as
  `ExGrid.Data`'s CSV reader adds none
  ([ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)).
- **A reader of our own reads only what it understands, and refuses the rest by name.** A general
  library models the whole format and leaves the decision of what was dropped to whoever maps it; the
  project's first principle needs that decision made part by part, in one place.
- **Size in a browser.** DocumentFormat.OpenXml brings a large assembly into every WebAssembly
  application that references the package. ClosedXML holds the whole workbook as objects, which does
  not scale to large files. EPPlus is under a commercial licence.

*Considered and rejected by the user:* DocumentFormat.OpenXml (MIT), ClosedXML.

## Proposed decision

### 1. A package of its own, over the engine: `ExSheet.Xlsx`

- **`ExSheet.Xlsx` references `ExSheet.Engine` only**, never the component, so a server can convert
  files with no Blazor in the process. ExSheet's component and its Wrapper do not reference it.
- **Its surface is a conversion between a stream and a Sheet Document**, and nothing else: reading
  returns a Sheet Document and the Open Report (§4); writing takes a Sheet Document and writes a
  workbook to a stream. Both are asynchronous, report progress and take a cancellation token (§6).
- **The engine gains a public way to build a Sheet Document.** Today only `Sheet.ToDocument()` and
  `SheetDocument.FromJson` make one; the records it holds are already public. The constructor becomes
  public rather than the engine granting `InternalsVisibleTo` to another shipped package, which would
  tie the two packages' versions together outside the declared surface (ADR-0201).
- **Choosing the file and saving it are the application's**, as persisting a Sheet Document already is
  (ADR-0048): `InputFile` gives the stream, and the application delivers the written one. ADR-0021's
  list does not grow.

### 2. One worksheet of a workbook is opened

ExSheet holds one Sheet (ADR-0046). **Reading opens one worksheet, which the application names; with no
name, the first.** The others are listed in the Open Report as not carried. A formula on the opened
worksheet that reaches outside it is refused (§3), so nothing it computes depends on what was left
behind.

### 3. What a workbook holds falls into three kinds

**Carried**: the worksheet's name; constants (numbers, text, booleans, Error Values); Formulas, with
Excel's stored `_xlfn.` and `_xlws.` prefixes taken off on reading and put back on writing, and shared
formulas expanded into each cell's own; spilled arrays (ADR-0125) as their anchor's Formula; Number
Formats the engine reads; horizontal alignment; the Font's colour, bold, italic, underline and
strikethrough; solid fills; the border styles ADR-0071 has; column widths, in Excel's own unit. A
workbook on the 1904 date system has its date serials moved to 1900's, which changes no date.

**Refused by name, and nothing opens**: anything that would change a Value, or the number a cell
shows. A Formula the engine cannot read; a reference to another worksheet, a defined name, another
workbook or an Excel Table; a legacy multi-cell array formula; a Number Format the engine does not read
(as ADR-0048 refuses one in a Sheet Document); a file that is not a plain `.xlsx` (`.xlsm`, `.xlsb`,
`.xls`, an encrypted file). The refusal names the cell and what it held, as the CSV reader names a row
and a column.

**Not carried, and said**: what changes how the sheet looks or what else the file held, but not a
Value: font names and sizes, row heights, vertical alignment, wrapping, indent and rotation, merged
cells, pattern and gradient fills, conditional formatting, data validation, notes and comments,
hyperlinks, pictures and charts, frozen panes, hidden rows and columns, protection, print settings,
rich-text runs inside a cell (the text is carried), and the other worksheets. Each is listed in the
Open Report with where it was.

A Formula naming a function ExSheet does not support is **carried**: it shows `#NAME?`, which
ADR-0047 already makes the honest answer, and the Open Report lists those cells so the application can
say so before the user meets them.

### 4. The Open Report

Reading returns, beside the Sheet Document, the list of what was not carried and of the cells whose
functions ExSheet does not support, each with its place in the workbook. The package shows nothing; the
application decides how to tell the user, and the Docs Site's Example shows it. A new term,
**Open Report**, goes into `CONTEXT.md` when this is decided.

### 5. What writing puts in the file

- **Every Formula is written with the Value the engine computes for it**, and the workbook is marked to
  be calculated in full when Excel opens it. ADR-0048 keeps Values out of a Sheet Document because a
  stored Value is a second truth for whoever reads it back; a workbook is read by programs that do not
  calculate (previews, data libraries), and the Value is what the user saw, which is ADR-0048's own
  rule for what goes outward ("Values outward"). **Reading never takes a stored Value**: it is
  computed again, as for a Sheet Document.
- **A Formula that reads a Linked Table refuses the write by name.** A Linked Table is the Consumer's
  data (ADR-0049) and has no place in a workbook; written as it is, Excel cannot read the reference,
  and written as a Value, it is a Formula quietly turned into a constant. A Linked Table declared and
  not read is listed as not carried.
- **The Sheet's culture is not written**, because a workbook has no place for one: Excel shows a
  workbook in its reader's culture. **Reading takes the culture from the application**, which declares
  it, as a CSV's encoding is declared rather than guessed (ADR-0064).
- Theme colours: see §7.

### 6. Large files, and what a file may not do

- **The worksheet's XML is read as a stream**, cell by cell, never as a document; shared strings are
  the one part held whole, because cells refer to them by index.
- **Reading works in batches**, reporting progress and yielding between them, as the CSV reader does,
  so a browser application stays responsive while a large file is read.
- **A workbook is untrusted input.** DTDs are prohibited, and the uncompressed size and the cell count
  have limits the application may raise; beyond them the file is refused by name, not read in part.
  The default limits are set from a measurement in the first ticket, not from reasoning (CLAUDE.md,
  "Measure before claiming anything about performance").

### 7. Theme colours are read as the RGB the file's theme gives them

A colour that names a theme slot and a tint is resolved, through the workbook's own theme part, to the
RGB value Excel paints, and recorded as that. A Sheet Document still holds Automatic or RGB only
(ADR-0048, ADR-0071); the link to the theme is listed as not carried. Writing records RGB values.

### 8. Opening a file takes one place in the Input Order

A file is opened by the application, outside the instance, and read for as long as it takes. If the
application set `Document` when the read finished, a key typed meanwhile would land on the old Sheet or
the new one depending on how long the read took, which the spine's sixth rule refuses.

- **ExSheet gains a method that replaces the Sheet Document with one still being read**, called on the
  user's gesture (the application's Open button), which takes the input's place in the Input Order at
  that moment. Inputs behind it wait until the document arrives and are then taken against the new
  Sheet, or refused visibly if what they were taken against no longer holds (ADR-0240 §4).
- **While it is read, the instance says so**, as a paste under way does, and **Escape cancels it**
  under the same exception ADR-0240 §12 accepts for a paste: both outcomes are said aloud. A refused or
  cancelled read leaves the current Sheet as it was.
- When it lands it is a replacement of the whole document, with ADR-0048's consequences: the undo stack
  is cleared, and an open edit is discarded and announced.
- Setting the `Document` parameter keeps working as it does for a document the application already
  holds.

## Questions put to the user

1. §1: a package `ExSheet.Xlsx` over the engine only, with a public constructor for `SheetDocument`.
2. §2: one worksheet per opening, named by the application, the first by default.
3. §3: a Number Format the engine cannot read refuses the whole file, rather than opening the cell in
   General and reporting it.
4. §5: a written Formula carries its computed Value (and a full calculation on open), rather than no
   Value.
5. §5: a Formula reading a Linked Table refuses the write.
6. §5: the culture is declared by the application on reading and not written.
7. §7: theme colours resolved to RGB on reading.
8. §8: opening a file is one input in the Input Order, with progress and Escape as for a paste.

## Consequences

- `ExSheet.Engine`'s public surface grows by `SheetDocument`'s constructor (a Change Note when built).
- A new shipped package, packed and smoke-tested as the others are (ADR-0042).
- Round-tripping a workbook through ExSheet loses what §3 lists as not carried. That is said each time,
  by the Open Report, and never silently.
- `.xlsx` for ExGrid and ExPivot (reading a sheet into a Snapshot) stays for later, under an ADR of its
  own.
