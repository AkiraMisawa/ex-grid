# Run 17, records and widths: what the orchestrator applies

Branch `claude/run17-records-and-widths`. The corpus, the engine's width rule and their tests are
changed on the branch; nothing here is. Each item below is text for a file a background agent does
not change (an ADR, the Definition of Done, `dotnet/src/ExSheet/README.md`, the Docs Site), written in the
repository's voice, with where it goes.

There were no research-then-follow cases in this area: every case was a record, a carried difference
by decision, a promotion, or a width the run observed directly.

## 1. ADR-0046: a note on the bullet "A recorded width says whether the user set it"

Append after the note of 2026-09-28:

> *(2026-10-12, the seventeenth Windows run, b10: **a column inserted to the right of a width widened
> by entry takes that width as set by the user.** Excel wrote the inserted column `customWidth`
> without `bestFit`, while the column to its left kept `bestFit` (CW-023). `bestFit` is the flag
> ECMA-376 gives a column that a typed number widens again, so Excel's inserted column holds the
> width as one the user set, not one an entry widened. Deletion moves a width with its kind (CW-025:
> `bestFit` kept), and undo puts a width back of its kind (CW-026: `bestFit` back). A Sheet Document
> saved before keeps the kinds it recorded. Whether a longer number then leaves the inserted column
> as it is, and still widens the column to its left, goes to the next keys run (CW-030 to 032).)*

## 2. ADR-0047: a note on the third run's bullet "A column a typed entry widened is widened again by a longer entry"

Append to that bullet (or under a short heading for the seventeenth run, if the orchestrator gathers
the run's notes there):

> *(2026-10-12, the seventeenth Windows run, b10: typed, `12345678901` widened a standard column to
> 11.18 at 150% display scale, written `width="11.81640625"`. The width an entry gives is Excel's
> pixels turned back into characters, and depends on the display's scale and the font. The engine
> harness widens to whole characters (11), and the component to its own pixels. So the corpus never
> names a width an entry gave: a typed case bounds it (`widthAtLeast`, `widthAtMost`), wide enough
> for Excel's at any scale and for the engine's, and narrow enough to tell it from the default width
> and from a width the user set (CW-021, 023, 025, 026). A width set by a command is still compared
> exactly (CW-022: 11).)*

Optional, on the second run's sentence "The deliberate differences recorded earlier stand":

> *(2026-10-12: the corpus now writes Excel's answer beside each of them, `excelExpect` (TABLE-011
> and 013: refused on entry; COPY-025: the text `=SUM(1.5,2)`), as ADR-0208's release check asks of
> every case that differs by decision. The seventeenth run gave the same answers.)*

## 2b. ADR-0047: a note on "A circular reference is `#CIRC!`" *(added after review, 2026-10-12)*

ERR-078, 080 and 081 record Excel's 1, 2 and `2x` beside the engine's `#CIRC!`, and their reasons said "where Excel
shows 0", which only ERR-079's B1 does. **On the branch** the three reasons now say: Excel shows 0 only in B1,
whose entry closed the cycle, and here the Value the cell had before, which B1's 0 also gives. ADR-0047's paragraph
under "Error Values, and the one ExSheet adds" says only "Excel shows 0 and a warning in the status bar, and the 0 is a
number that flows into every dependent Formula". A dated note after that paragraph:

> *(2026-10-12, the seventeenth Windows run: **Excel showed 0 in one cell of a cycle, not in each.** With A1
> `=B1+1`, C1 `=A1*2` and D1 `=C1&"x"` entered first and B1 `=A1` last, B1, whose entry closed the cycle, showed 0,
> and A1, C1 and D1 kept 1, 2 and `2x` (ERR-078 to 081): the Values they had before, which B1's 0 also gives them. A
> Formula entered while a cycle stands showed 0 whatever it computes from a cell of the cycle: `=A1+1`, `=ISNUMBER(A1)`
> and `=ERROR.TYPE(A1)` over A1 `=B1`, B1 `=A1` (ERR-084, ISNUMBER-008, ERRORTYPE-020). Which Values Excel shows
> depends on the order the cells were entered, which a Sheet does not keep. The decision stands, for the same
> reason.)*

**This note and logic-error-type's ADR-0047 item 2 touch the same paragraph.** That item reads ERRORTYPE-020 and
ISNUMBER-008 as "the 0 does not flow into the dependent Formula as a number; the dependent shows 0 of its own". ERR-080
and 081 show dependents with Values other than 0. The note above reads both areas' cases together, and
`docs/agents/proposals/run17-apply.md` carries it in place of the two.

## 3. Definition of Done, SH-26 (and SH-22's evidence)

SH-26's requirement, after "a user's width replaces an entry's, never the reverse":

> ; a column inserted to the right of a recorded width takes it as set by the user, as Excel's file
> holds it (CW-023)

SH-26's verification column: "Layer 1 against the case corpus (CW-018, 021 to 029); Layer 2". Once the
next keys run has asked CW-030 to 032, add them.

## 4. `dotnet/src/ExSheet/README.md`, the column-widths paragraph

Replace its last sentence ("A width an entry or a Number Format widened … nothing widens that
column.") with:

> A width an entry or a Number Format widened the column to stays automatic, and a longer number
> widens the column again; a width the user set — a drag or a size to fit — is custom, and nothing
> widens that column. A column inserted to the right of either takes its width as one the user set,
> as Excel's does.

## 5. Docs Site, `/exsheet/formatting`, section "Cell Formats"

The page does not say today that an inserted column keeps a width's kind, so nothing on it is false.
The Change Note links here (`exsheet/formatting#cell-formats`), so the section should say it. In the
paragraph that begins "**A number that does not fit shows `####`.**", replace the sentence "Setting a
Number Format widens a column the user has not sized, as Excel's formatting keys do." with:

> A number typed into a column you have not sized widens it, as Excel's does, and so does setting a
> Number Format, as Excel's formatting keys do. A column inserted beside a widened one takes its width
> as one you set, so a longer number typed into it is fitted to the column rather than widening it.

## 6. The questions this branch adds (ADR-0208)

CW-030, CW-031 and CW-032 are `uncertain`, typed cases that ask whether each kind widens as `bestFit`
says after an insertion or a deletion. ADR-0208 needs them asked before a version without a suffix:

```powershell
pwsh dotnet/tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1 -Keys -Area column-widths -Update
```

under en-US, with nothing else taking the foreground. The same call asks CW-021 to 025 again by keys,
which agreed by hand in b10. CW-026 is blocked by its undo, and stays observed from b10.

## 7. Notes for the merge

- **The Change Note's kind is `Behaviour`**, not "Changed": ADR-0190 allows Added, Fixed, Behaviour
  or Breaking, and the Docs build refuses any other.
- **CW-026's `oracleSkip` does not say "ask by keys"**, unlike the triage's wording: its undo cannot go
  through COM, so a keys pass would type it and then block. It names b10 as where it was asked.
- **Corrected after review (2026-10-12):** `ExcelCorpus.cs`' comments on `widthAtMost` and `widthAtLeast` said
  "the check column's width after every typed entry"; the harness and `oracle.ps1` read it once, when the case is
  read, as `column-widths.json`'s `about` says, and the comments now say so.
- **Two files outside the area's list**, doc comments only, because the rule change made them false:
  `dotnet/src/ExSheet.Engine/Sheet.Structure.cs` (`InsertColumns`' summary said an inserted column takes the
  width "automatic or custom as it is") and `dotnet/src/ExSheet.Engine/SheetColumnWidth.cs`
  (`SheetColumnWidthKind.SetByUser` now names the inserted column).
