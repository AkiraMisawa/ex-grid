# Windows, exsheet-workbook ticket 12: Excel's answers to the Workbook's questions

**Asked at:** `5799246a` (origin/main), on branch `claude/project-thread-gnj7zt`, on 2026-10-24 from 22:34 to 22:47 BST,
by a Claude Code session on the Windows desktop of the earlier runs, at the user's request, to unblock ticket 11 (3D
References). ADR-0331 §5 and §7 build nothing from reasoning about Excel; ADR-0208 asks a feature's questions when it is
built. These are questions, not checks: nothing here is a by-hand row of the Definition of Done, so the run tags nothing.

**Nothing here decides.** Where Excel differs from what ExSheet does, it is listed under "Where Excel differs".

## Files

| | |
|---|---|
| `report.md` | this report |
| `excel/ask-com.ps1` | the questions COM can ask: an Excel of its own, a fresh workbook per case, Sheet operations through the object model, every Formula's `Formula2`, `Text`, `Value2` and `NumberFormat` read back. Groups `functions`, `forms`, `more`, `span`, `rows`, `copy`, `deleted`, `names` |
| `excel/functions.txt` | every function ExSheet declares at `5799246a`, with its argument counts: the `functions` group asks each |
| `excel/xl-server.ps1`, `excel/xs.py` | real keys and the real mouse: the 2026-10-14 readings' server, unchanged, and its client rewritten to run on Windows itself |
| `records/com-*.jsonl` | one line per COM case, as `ask-com.ps1` wrote it |
| `records/keys-xs.jsonl` | every exchange with the server, in order, with Excel's answer |
| `records/com-offset.jsonl` | `OFFSET`'s three cases of the `more` group asked again from Out!A9 at 23:06, since in Out!A1 they named their own cell: the same answers |
| `records/com-later.jsonl` | the arguments past the third, asked at 23:20 after the code review found them unasked (`later` group, from Out!A9). `SWITCH`'s and `IFS`'s results, `CHOOSE`'s and `IF`'s later values and the criteria functions' later ranges answer as their first ones; `MATCH`'s match_type and `XLOOKUP`'s if_not_found are `#VALUE!`; a criterion is `#REF!` and matches nothing (`SUMIF(…,Sheet2:Sheet4!B2)` 0); and **`IFERROR` and `IFNA` hand their fallback on**: `SUM(IFERROR(1/0,Sheet2:Sheet4!B2))` is 1110 |
| `excel/ask-spill-xml.ps1`, `records/spill-xml.txt` | how Excel stores `=SUM(Sheet2:Sheet4!B2#)` in an `.xlsx`: `SUM(_xlfn.ANCHORARRAY(Sheet2:Sheet4!B2))` |
| `to-fixture.py` | writes these records as the engine's layer 1 fixture, `tests/ExSheet.Engine.Tests/ThreeDReadings/excel-readings.json`, which `ThreeDExcelReadingsTests` reads |

The pictures stay on the machine (`%LOCALAPPDATA%\exgrid-layer3\workbook-12\xs\shots`): they show the title bar's account
corner.

## Environment

- Windows 11 Pro, build 26200, one display 3840 × 2160 at 150%, display language en-GB, regional format **en-GB**
  (unchanged). Excel for Microsoft 365, **16.0 build 20430**, the same build as runs 17 and 18a. Excel's window was given
  English (UK) by the server, as before.
- Every COM case's workbook is `Out, Sheet1, Sheet2, Sheet3, Sheet4, Sheet5`, `B2` on SheetN holding 10^(N−1) (1, 10,
  100, 1000, 10000) and `C3` twice that, so a sum names the Sheets it read. Unless a case says otherwise the Formula is
  `Out!A1 =SUM(Sheet2:Sheet4!B2)`, 1110.

## The answers

### 1. Which functions take a 3D Reference, and what the others answer

`records/com-functions.jsonl` (every declared function, the span as each of its first three arguments in turn, the
others `1`), `records/com-forms.jsonl` and `records/com-more.jsonl` (the shapes around it, and arguments of the right
kind where COM refused a `1` in place of a range). Excel refused none of the Formulas with a span in it; each one it
refused (`AVERAGEIFS(1,…)`, `COUNTIF(1,…)`, `OFFSET(1,…)` and the like) it refused for the `1` written where a range is
asked, and the same function asked with a range answered.

- **Read across the span, as one range, Sheet by Sheet in tab order, each row-major:** `SUM`, `AVERAGE`, `COUNT`,
  `COUNTA`, `MAX`, `MIN`, `PRODUCT`, `STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P`, `MEDIAN`, `AND`, `OR`, `XOR`, `CONCAT`,
  `LARGE` and `SMALL` (their array), `RANK.EQ` (its ref: `RANK.EQ(10,Sheet2:Sheet4!B2)` is 3), and `TEXTJOIN`, both its
  texts and its delimiter (`TEXTJOIN(Sheet2:Sheet4!B2,TRUE,"a","b","c")` is `a10b100c`). The values are a range's:
  text and logicals in the cells are passed over by `SUM` and the like and counted by `COUNTA`; the first error in that
  order is the answer (`#N/A` on Sheet2 before `#DIV/0!` on Sheet4 gives `#N/A`).
- **`#VALUE!`** where a function asks for a range of one Sheet: `SUMIF`, `SUMIFS`, `COUNTIF`, `COUNTIFS`,
  `AVERAGEIF`, `AVERAGEIFS`, `MAXIFS`, `MINIFS` (any of their ranges), `COUNTBLANK`, `ROWS`, `COLUMNS`, `ROW`, `COLUMN`,
  `INDEX` (its array), `MATCH` and `XMATCH` (their array), `VLOOKUP`, `HLOOKUP`, `XLOOKUP` (their tables and results),
  `FILTER`, `SUBTOTAL` and `AGGREGATE` (their refs); and in `EDATE`, `EOMONTH`, `MROUND`, `NETWORKDAYS`, `WORKDAY`,
  `YEARFRAC`, `XNPV` and `RANDBETWEEN`, wherever it stands.
- **`#REF!`** everywhere else it is read as one value: on its own (`=Sheet2:Sheet4!B2`), under an operator (`+1`,
  unary `-`, `SUM(Sheet2:Sheet4!B2+0)`), and as any other argument (`ABS`, `ROUND`, `IF`'s condition, `CONCATENATE`,
  `SUMPRODUCT`, `SORT`, `INDIRECT`, `OFFSET`, `LARGE`'s k, …). So `ISERROR` is TRUE, `ISNUMBER` FALSE, `IFERROR(…,1)` 1,
  `IFNA(…,1)` `#REF!`, `ERROR.TYPE` 4.
- **Handed on by `IF`, `CHOOSE` and `LET`**, as a reference: `SUM(IF(TRUE,Sheet2:Sheet4!B2))`,
  `SUM(CHOOSE(1,Sheet2:Sheet4!B2))` and `SUM(LET(x,Sheet2:Sheet4!B2,x))` are 1110, and so are `LARGE`, `CONCAT`, `AND`
  and `COUNT` over one. `IFS` and `SWITCH` do not hand it on (`#VALUE!`), nor `INDEX` (`#VALUE!`), nor `IFERROR` or
  `IFNA` as their value (`#REF!`, as a value); as their fallback they hand it on (`com-later.jsonl`). One handed on is `#VALUE!` where it is read as one value
  (`IF(TRUE,Sheet2:Sheet4!B2)+1`), and so is a Formula whose result it is (`=IF(1,Sheet2:Sheet4!B2)`,
  `=LET(x,Sheet2:Sheet4!B2,x)`).
- **`#NAME?`** in parentheses: `=(Sheet2:Sheet4!B2)` and `=SUM((Sheet2:Sheet4!B2))`. And when the span names a Sheet
  the workbook lacks, typed (`=SUM(Sheet2:Nope!B2)`, no dialog) and through COM alike.
- **`#VALUE!`** for an intersection with one (`SUM(Sheet2:Sheet4!B2:C3 Sheet2:Sheet4!B2)`), a range between two
  (`Sheet2:Sheet4!B2:Sheet2:Sheet4!C3`) or one ended by `INDEX`; **`#REF!`** for a spill, `Sheet2:Sheet4!B2#`.
- `ISREF` (not declared by ExSheet) is FALSE of one, `AREAS` (neither) `#VALUE!`.

### 2 and 3. The span when Sheets are moved, deleted, inserted or copied

`records/com-sheets.jsonl` (`span` group). Each case also held `Out!A3 =SUM(Sheet2:Sheet3!B2)`, a span of two.

- **A span is the Sheets between its two end Sheets in tab order, whatever moves between them.** A Sheet inserted,
  copied or moved between the ends is read (`New` inserted after Sheet2: 1117); one inserted, copied or moved just
  outside an end is not; a Sheet of the span moved outside it is no longer read (Sheet3 moved to the end: 1010), nor a
  Sheet deleted from between the ends. The Formula's text does not change in any of these.
- **An end Sheet moved keeps its end** while it stays on its side of the other: Sheet2 moved before Sheet1 makes the
  span `Sheet2:Sheet4` over Sheet2, Sheet1, Sheet3, Sheet4 (1111); Sheet2 moved after Sheet3, inside the span, leaves
  `Sheet2:Sheet4` over Sheet2 and Sheet4 only (1010).
- **An end moved past the other end hands its end to the Sheet next to it, inward.** Sheet2 (first) moved after Sheet4
  or to the end: `Sheet3:Sheet4`. Sheet4 (last) moved before Sheet2 or to the front: `Sheet2:Sheet3`. For the span of
  two, Sheet2 moved past Sheet3 leaves `Sheet3!B2`, and Sheet3 moved before Sheet1 leaves `Sheet2!B2`.
- **An end deleted hands its end inward the same way:** Sheet2 deleted, `Sheet3:Sheet4`; Sheet4 deleted,
  `Sheet2:Sheet3`; Sheet2 then Sheet3 deleted, `SUM(Sheet4!B2)`.
- **A span left with one Sheet is written as a Reference to that Sheet**, and deleting that Sheet too gives `#REF!B2`,
  as for any Reference to a deleted Sheet (answer 11): Sheet2, Sheet3 and Sheet4 deleted, `=SUM(#REF!B2)`.
- A rename rewrites the end it renames (`'Q 2:Sheet4'`, then `'Q 2:Jan'`); the Formula's own Sheet moved into the span
  is read like any other.

### 4. Rows or columns inserted or deleted on one Sheet of a span

`records/com-rest.jsonl` (`rows` group). **A 3D Reference is never rewritten**: its text stays `Sheet2:Sheet4!B2`
(and `B2:C3`, `$B$2`) whatever is inserted or deleted on any Sheet of it, and it reads what is then at those cells. A
row inserted above B2 on Sheet3 gives 1010; row 2 deleted on Sheet3 gives 1010, with no `#REF!`; a column inserted
before B on Sheet3, 1010; the same insertion on all three Sheets in turn, 0. A Reference of one Sheet to the same cell,
`=Sheet3!B2`, moves as before (`Sheet3!B3`, `Sheet3!#REF!`). The Formula's own cell moves with its row when it sits on
a Sheet of the span, its text unchanged. *(A row inserted while the three Sheets were grouped, through COM, reached the
active Sheet only, so that case says nothing about grouping.)*

### 5. A copy of a Sheet whose own Formula holds a span it begins or ends

`records/com-rest.jsonl` (`copy` group). **In the copy, a 3D Reference with the copied Sheet at either end becomes
`#REF!B2`** (`=SUM(#REF!B2)`), wherever the copy is put, before or after the source, inside the span or outside it, or
in a new workbook. One that does not name the copied Sheet at an end is copied as it is (Sheet3's
`=SUM(Sheet2:Sheet4!B2)`, Sheet1's). The source's Formula is unchanged, and reads the copy where the copy falls inside
its span (Sheet2 copied after Sheet2: 1120).

### 6. How Excel writes a span, and whether it gives a format

Typed with real keys (`records/keys-xs.jsonl`) and through COM alike:

- `=sum(sheet3:sheet2!b2)` is written `=SUM(Sheet2:Sheet3!B2)`: **the ends in tab order and in the Sheets' own case**.
- `=SUM(Sheet2:Sheet2!B2)` is written `=SUM(Sheet2!B2)`.
- `=SUM('Sheet2:Sheet3'!B2)` is written bare, `=SUM(Sheet2:Sheet3!B2)`; with a space in a name, quoted whole,
  `'Q 2:Sheet3'`; `'Q 2':'Q 4'!B2` is `#NAME?`.
- **A Formula entered into a General cell takes no format from a 3D Reference**: over three percentages,
  `=SUM(Sheet2:Sheet3!B2)` stays General. `=SUM(Sheet2:Sheet2!B2)`, typed as a span, stays General too, though it is
  written `=SUM(Sheet2!B2)`; `=SUM(Sheet2!B2)` typed so takes `0%`.

### 7. A tab press while a constant is being typed

`12` typed in Sheet1!A1, then a click on Sheet2's tab: **the constant is entered** (Sheet1!A1 is 12) and Sheet2 is
shown, Ready. Ctrl+PageDown does the same.

### 8. Tab presses while a Formula is being typed

- `=` typed in Sheet1!C1, then a click on Sheet2's tab: **the bar reads `=Sheet2!` at once**, in Point mode, before any
  cell is pointed. A click on B2 there makes it `=Sheet2!B2`.
- `+` and a click back on Sheet1's tab writes `Sheet1!` at once, and a click on B2 there **`Sheet1!B2`**
  (`=Sheet2!B2+Sheet1!B2`).
- **Escape on Sheet2 shows Sheet1 again**, C1 selected, nothing entered. Enter on Sheet2 enters `=Sheet2!B2` in
  Sheet1!C1 and shows Sheet1 with C2 selected.
- *(Not asked, recorded:)* a click on Sheet2's tab then a Shift+click on Sheet3's writes `=SUM('Sheet2:Sheet3'!`,
  quoted, before the cell.

### 9. Ctrl+Z after a Sheet operation

From another Sheet each time, with a cell selected there after the operation:

| Operation | Ctrl+Z |
|---|---|
| Insert (Shift+F11) | undone: the new Sheet is gone, and the Sheet and selection of before the insertion are shown (Sheet1, B3). The next Ctrl+Z undoes the entry before it |
| Rename (Alt H O R) | undone: the renamed Sheet is shown with the selection it had when renamed (Sheet3, C4) |
| Move (drag) | undone: the order is put back, and **the Sheet shown and its selection stay as they are** (Sheet1, G9). The next Ctrl+Z undoes the entry before it, showing its Sheet |
| Copy (Move or Copy, Create a copy) | **not undone**: the copy stays; Ctrl+Z undoes the entry before the copy, showing that entry's Sheet |
| Delete (Alt H D S) | **not undone, and the undo stack is emptied**: nothing earlier can be undone either |

### 10. A Formula over a percentage on another Sheet

`=Sheet2!B2`, typed into a General cell over `0%`, **takes `0%`**, as ExSheet does since 01. So does `=SUM(Sheet2!B2)`.

### 11. A Formula reading a deleted Sheet

`records/com-rest.jsonl` (`deleted`). **`#REF!` followed by the cells**: `=#REF!B2`, `=#REF!B2:C3`,
`=SUM(#REF!B2:C3)`, `=#REF!$B$2`, `=#REF!B2+Sheet1!B2`, `=SUM(#REF!B:B)`, `=#REF!2:2`, `=ROW(#REF!B5)`, for a bare and
a quoted name alike.

### 12. A copied Sheet's References qualified with its source's name

**They name the copy**: on a copy of Sheet2, `=Sheet2!B2` is `='Sheet2 (2)'!B2` and `=SUM(Sheet2!B2:C3)` is
`=SUM('Sheet2 (2)'!B2:C3)`, as ExSheet assumes since 03. A Reference to another Sheet and an unqualified one are copied
as they are.

### 13. The name of a new Sheet, and of a long copy

- **A new Sheet takes `Sheet` and a number the workbook counts, never given twice, skipping a name taken**: Sheet1,
  Sheet2, Sheet3 with Sheet2 deleted gives Sheet4 (COM and Shift+F11 alike); Sheet1 renamed Sheet5 gives Sheet2; Sheet1
  renamed Sheet3 gives Sheet2, then Sheet4; Sheet1 renamed Jan gives Sheet2. A Sheet inserted and undone still used its
  number (Shift+F11 afterwards gave Sheet6, then Sheet7).
- **A copy is `name (2)`, its name cut so that it fits 31 characters**: a copy of 31, 30 or 27 `A`s is 27 `A`s and
  ` (2)`, the next ` (3)`. A copy of `Jan (2)` is `Jan (3)`, and of `Sheet1 (2)` `Sheet1 (3)`.

## Where Excel differs from ExSheet at `5799246a`

These are for the user; none is changed by this record.

1. **Answer 11:** Excel writes a Reference to a deleted Sheet `#REF!B2`; ExSheet writes `#REF!` (ticket 03).
2. **Answer 13:** Excel numbers a new Sheet from a count of the Sheets the workbook has made, skipping names taken;
   ExSheet takes one more than the highest `SheetN` (`NextSheetName`). With Sheet1 renamed Sheet5, Excel gives Sheet2
   and ExSheet Sheet6.
3. **Answer 9:** Excel cannot undo a deletion or a copy of a Sheet, and a deletion empties its undo stack; ExSheet
   undoes both, as ADR-0331 §3 decided. After a move is undone Excel keeps the Sheet shown; after a rename it shows the
   renamed Sheet.
4. **Answer 7:** Excel enters a constant and shows the other Sheet; ExSheet refuses the tab press while a constant is
   typed (ticket 09).
5. **Answer 8:** Excel writes `Sheet2!` at the tab press itself; ExSheet writes the qualifier only when a cell is
   pointed (ticket 09).
6. **Answers 1 to 6** are what ticket 11 builds from; the prototype it starts from refused each of them.
