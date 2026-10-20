# ExSheet opens and saves `.xlsx` with a reader and writer of its own

*(Numbered ADR-0320 until 2026-10-20. That block had been reserved on `main` by ExChart's Chart Spec
first, so this branch moved into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for it. Commit messages before then use the old number.)*

*(Decided with the user, 2026-10-20, in a grilling (Q1 to Q5, then X1 to X4) that stopped half-way
for [ADR-0331](./0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md): two answers had only
worked around ExSheet's one Sheet, so the user decided that ExSheet holds a Workbook first. This ADR
was first written as a proposal for one Sheet; it now reads and writes a whole Workbook. The user took
the recommendation of every question except Q5 and X1.)*

[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md) and
[ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md) left
`.xlsx` to "a separate package when it comes", and
[ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) left theme colours
to it. This ADR says which packages read and write it, what crosses between a Workbook and a file,
what is refused, and how opening a file takes its place in the Input Order
([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)).

## Decision

### 1. Our own reader and writer, with no dependency

- **The format is a zip of XML parts, and the BCL reads both.** `System.IO.Compression` and
  `System.Xml`'s `XmlReader`/`XmlWriter` are enough, so nothing is added, as `ExGrid.Data`'s CSV reader
  adds nothing ([ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)).
- **A reader of our own reads only what it understands and refuses the rest by name.** A general
  library models the whole format and leaves the question of what was dropped to whoever maps it; the
  first principle needs it answered part by part, in one place.
- *Rejected:* DocumentFormat.OpenXml (a large assembly in every WebAssembly application that
  references the package) and ClosedXML (the whole workbook held as objects, which does not scale to
  large files). EPPlus is under a commercial licence.

### 2. Two packages: the workbook itself, and ExSheet's mapping over it (Q1)

```
ExGrid.Data.Xlsx   → ExGrid.Data        the file: parts, shared strings, styles, cells read and written as a stream;
                                         later, a worksheet read into a Snapshot under a Schema (an ADR of its own)
ExSheet.Xlsx       → ExGrid.Data.Xlsx, ExSheet.Engine
                                         a Workbook Document read from a file, a Workbook written to one
```

- **The file layer is shared**, so that reading a worksheet into a Snapshot for ExGrid and ExPivot
  later does not read the zip and the XML a second time. It sits beside `ExGrid.Data.Arrow`, in the same
  shape: an application that never touches `.xlsx` carries none of it, and an ExSheet application
  already carries `ExGrid.Data` through ExGrid.
- **Neither package references a component**, so a server converts files with no Blazor in the process.
- **Choosing a file and delivering the written one are the application's**, as persisting a document
  already is (ADR-0048): `InputFile` gives a stream, and the application sends the written one where
  it wants. ADR-0021's list does not grow.

*Rejected:* the file layer inside `ExGrid.Data`, beside the CSV reader (every grid application would
carry it); the two packages written independently (the file read twice).

### 3. A whole Workbook is opened and saved

Every worksheet becomes a Sheet, in the file's order, and the worksheet shown becomes the Sheet shown.
References across Sheets and 3D References (ADR-0331 §7) are carried as written. *(Q2 first decided
"one worksheet per opening"; ADR-0331 replaced it.)*

### 4. What a workbook holds falls into three kinds

**Carried**: the Sheets and their names; constants (numbers, text, booleans, Error Values); Formulas,
with Excel's stored `_xlfn.` and `_xlws.` prefixes taken off on reading and put back on writing, and
shared formulas expanded into each cell's own; spilled arrays
([ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md)) as their anchor's
Formula; Number Formats the engine reads; horizontal alignment; the Font's colour, bold, italic,
underline and strikethrough; solid fills; the border styles ADR-0071 has; column widths, in Excel's
own unit. A workbook on the 1904 date system has its date serials moved to 1900's, which changes no
date.

**Refused by name, and nothing opens** — anything that would change a Value, or the number a cell
shows:

- a Formula the engine cannot read;
- a reference to a defined name, to another workbook, or to an Excel Table (§6);
- a legacy multi-cell array formula;
- **a Number Format the engine does not read** (Q3), as ADR-0048 refuses one in a Sheet Document. A
  cell shown in General instead would show a date as `45000`, or drop a currency symbol, and look
  plausible;
- a file that is not a plain `.xlsx`: `.xlsm`, `.xlsb`, `.xls`, an encrypted file.

The refusal names the Sheet, the cell and what it held, as the CSV reader names a row and a column.

**Not carried, and said**: what changes how a Sheet looks, or what else the file held, but no Value:
font names and sizes, row heights, vertical alignment, wrapping, indent and rotation, merged cells,
pattern and gradient fills, conditional formatting, data validation, notes and comments, hyperlinks,
pictures and charts, frozen panes, hidden rows and columns, protection, print settings, rich-text runs
inside a cell (the text is carried), tab colours, a Sheet's being hidden (it opens shown, ADR-0331 §4),
defined names no Formula reads, the structure of an Excel Table no Formula reads (its cells are
carried), and the link from a colour to the theme (§8).

A Formula naming a function ExSheet does not support is **carried**: it shows `#NAME?`, which
ADR-0047 already makes the honest answer, and the Open Report lists it, so the application can say so
before the user meets it.

### 5. The Open Report

Reading returns, beside the Workbook Document, the **Open Report**: what was not carried, and the
cells whose functions ExSheet does not support, each with its Sheet and place. The package shows
nothing; the application decides how to tell the user, and the Docs Site's Example shows one way.

### 6. Writing: Values with every Formula, and each Linked Table on a Sheet of its own

- **Every Formula is written with its Value, and the workbook is marked to be calculated in full when
  Excel opens it** (Q4). A workbook is read by programs that do not calculate (previews, data
  libraries), and the Value is what the user saw: ADR-0048's own rule for what goes outward. **Reading
  never takes a stored Value**: every Value is computed again, as for a Sheet Document.
- **Writing is taken from the Workbook as it stands**, not from a Workbook Document, because the
  document holds neither Values nor a Linked Table's rows. The Values written are the ones on screen,
  `RAND()`'s included ([ADR-0180](./0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md)).
- **Each Linked Table is written as an Excel Table on a worksheet of its own** (Q5), named after it,
  holding its rows as they stand when the file is written. Its Formulas are written as they are, so
  `Positions[PV]` keeps working in Excel. The write is refused by name while a Linked Table has not
  answered (`#GETTING_DATA`), when its rows exceed Excel's 1,048,576, or when its name is already a
  Sheet's. *(Recommended against, and chosen: refusing the write of a Formula that reads a Linked
  Table.)*
- **That worksheet is written for Excel, and is not read back as a Linked Table** (X1). Nothing in an
  `.xlsx` can say that a table is the Consumer's data, and a mark hidden in the file would hold only
  until a rename in Excel or a tool that drops it. Opened again in ExSheet, it is a worksheet whose
  Excel Table Formulas read, which §4 refuses until ExSheet reads Excel Tables. **So a workbook ExSheet
  wrote with a Linked Table in it opens in Excel, and opens in ExSheet only once Excel Tables are
  supported**, by an ADR of their own. *(Recommended against, and chosen: a hidden declaration read
  back as the Linked Table.)*

### 7. The culture is the application's (X2)

A workbook stores numbers and dates in one form in every language, so no Value read from it depends on
a culture. What does is how the user's typing is read afterwards, and how Values are shown (ADR-0331
§2). **Reading takes the Workbook's culture from the application**, which declares it, as a CSV's
encoding is declared rather than guessed (ADR-0064). Writing records none; Excel shows a workbook in
its reader's culture. *Rejected:* guessing from the language a workbook records, which turns a typed
`1,234` into 1.234 when the guess is wrong.

### 8. Theme colours are read as the RGB the file's theme gives them (X3)

A colour that names a theme slot and a tint is resolved, through the workbook's own theme part, to the
RGB value Excel paints, and recorded as that. A document still holds Automatic or RGB only (ADR-0048,
ADR-0071). The link to the theme is listed as not carried; writing records RGB values. *Rejected:*
theme colours in ExSheet's Cell Format (a mechanism of its own in format and paint, for an ADR if it
is ever wanted), and refusing them (nearly every workbook has them).

### 9. Large files, and what a file may not do

- **A worksheet's XML is read as a stream**, cell by cell, never as a document. Shared strings are the
  one part held whole, because cells refer to them by index.
- **Reading works in batches**, reporting progress and yielding between them, as the CSV reader does,
  so a browser application stays responsive while a large file is read.
- **A workbook is untrusted input.** DTDs are prohibited, and the uncompressed size and the cell count
  have limits the application may raise; beyond them the file is refused by name, not read in part.
  The default limits are set from a measurement in the first ticket, not from reasoning.

### 10. Opening a file takes one place in the Input Order (X4)

A file is opened by the application, outside the instance, and read for as long as it takes. If the
application set the `Workbook` parameter when the read finished, a key typed meanwhile would land on
the old Workbook or the new one depending on how long the read took, which the spine's sixth rule
refuses.

- **ExSheet gains a method that replaces its Workbook with one still being read.** The application
  calls it from its Open button's handler, and the replacement takes its place in the Input Order
  there. Inputs behind it wait until the document arrives and are then taken against the new Workbook,
  or refused visibly when what they were taken against no longer holds (ADR-0240 §4).
- **While it is read, the instance says so**, as a paste under way does, and **Escape cancels it**,
  under the exception ADR-0240 §12 accepts for a paste: both outcomes are said aloud. A refused or
  cancelled read leaves the current Workbook as it was.
- When it lands it replaces the whole document, with ADR-0048's consequences: the undo stack is
  cleared, and an open edit is discarded and announced.
- Setting the `Workbook` or `Document` parameter keeps working, for a document the application
  already holds.
- **Writing is taken at its own place in the order**, against the Workbook as the inputs before it left
  it. While an edit is open, it is refused by name, as ADR-0048 refuses the commands that change a
  Sheet: the file would quietly lack what the user is typing. *(Settled by ADR-0048's rule, not put to
  the user.)*

## Consequences

- Two new shipped packages, packed and smoke-tested as the others are (ADR-0042).
- `CONTEXT.md` gains **Open Report**.
- Round-tripping a workbook through ExSheet loses what §4 lists as not carried. It is said each time, by
  the Open Report, and never silently.
- A workbook ExSheet writes with a Linked Table in it cannot be opened in ExSheet until Excel Tables
  are; that ADR is the next one this one points at.
- Reading a worksheet into a Snapshot for ExGrid and ExPivot is left to an ADR of its own, over the same
  file layer.
- The Docs Site gains a page for `.xlsx` on ExSheet, with an Example that opens a file, shows its Open
  Report and saves it, and a Change Note is written when it is built.
