# Run 17 Part H, widening: what the orchestrator applies

Branch `claude/run17gh-widening`. Ctrl+U now widens as Ctrl+5 does (h7), with layer 2 and layer 3
tests, the ticket 100 comment and a Change Note. Those are on the branch. Each item below is text
for a file a background agent does not change (an ADR, the Docs Site), or for lines of
`ExSheet.razor` outside the one remark this area was given. Each is written as exact old -> new
whole lines, with where it goes.

The run's record is `verification/2026-10-11-windows-17/report.md` on
`claude/exsheet-windows-verify-17`, Part H, rows h7 and h8.

## 1. ADR-0071: a section "What the seventeenth Windows run settled"

Insert before `## Consequences` (line 852 on `origin/main` at a533de01), after the section "What was
decided after Part C":

```markdown
## What the seventeenth Windows run settled *(2026-10-12)*

The run was made at 8436e45e. Its record is `verification/2026-10-11-windows-17/report.md`, Part H,
rows h7 and h8. Every answer is Excel's (ADR-0047's rule). It asked what the fourteenth run left "as
Ctrl+B is, until a run asks".

**Widening**, over 123456789.12 in `0.00` showing `####`:
- **Ctrl+U widens as Ctrl+5 does** (h7). At the standard width Excel's Ctrl+U widened column A from
  8.09 to 11.73. ExSheet widens for it by ticket 100's path: a column at the standard width, or one
  an entry or a key widened, never one the user sized, in the key's one undo step. Ctrl+4 is the same
  key, so it widens too. Taking underline off widens alike; that is a reading, as it is for Ctrl+5.
- **Ctrl+I does not widen** (h7), as Ctrl+B did not. The number shows `####` in italic.
- **Format Cells' Font tab, with Bold and OK, widened column A to 11.73** (h7). Ctrl+B did not widen
  in the fourteenth run's case 7. What follows from that is the user's question. Until it is
  answered, Format Cells' OK, `SetCellFormatAsync` and the Sheet Toolbar's toggles widen only for a
  Number Format, as before.
- **At a width set through COM (5), none of the three widened** (h7), as a column the user sized
  never does.
- **Ctrl+5 widened again** at the standard width, and not at a set width (h8), as the fourteenth
  run's case 7 read.
```

## 2. ADR-0071: dated notes on the three places that left Ctrl+I, Ctrl+U and the Font tab to a run

**a.** "What the fourteenth Windows run settled", Widening, lines 643 and 644:

old:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks.
```
new:
```markdown
  - Ctrl+I, Ctrl+U and Format Cells' Font tab were not asked. They stay as Ctrl+B is, until a run
    asks. *(2026-10-12: the seventeenth run asked. Ctrl+U widens and Ctrl+I does not. Format Cells'
    Font tab with Bold widened, which waits for the user's question. See "What the seventeenth
    Windows run settled".)*
```

**b.** "Seen in the run, and left to the next PR", lines 709 and 710:

old:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab.
```
new:
```markdown
- **Ctrl+5's widening raises a question about the other Font keys** (above). The next run that asks
  Excel about Cell Format can ask about Ctrl+I, Ctrl+U and the Font tab. *(2026-10-12: the
  seventeenth run asked them, h7. See "What the seventeenth Windows run settled".)*
```

**c.** "What was decided after Part C", "Seen beside the readings", lines 849 and 850:

old:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format.
```
new:
```markdown
- **The other Font keys** (Ctrl+I, Ctrl+U, and the Font tab beside Ctrl+5's widening) wait for the
  next run that asks Excel about Cell Format. *(2026-10-12: the seventeenth run asked them, h7. See
  "What the seventeenth Windows run settled".)*
```

## 3. The Docs Site: `samples/ExGrid.Docs/Pages/Sheet/SheetFormatting.razor`, "Cell Formats"

Line 30 (the paragraph "A number that does not fit shows ####"):

old:
```html
            sized widens it, as Excel's does, and so does setting a Number Format, as Excel's formatting keys do. A
```
new:
```html
            sized widens it, as Excel's does, and so do setting a Number Format and pressing Ctrl+5 or Ctrl+U, as
            Excel's formatting keys do; Ctrl+B and Ctrl+I widen nothing. A
```

The Change Note `docs/releases/unreleased/run17gh-widening-ctrl-u.md` links
`exsheet/formatting#cell-formats`, the section this paragraph is in. Run the site once after
applying it (`nix develop -c dotnet run --project samples/ExGrid.Docs`).

## 4. `src/ExSheet/Components/ExSheet.razor`: three remarks outside the one this area changed

This area changed only `OnFormatKeyAsync`'s remark, as briefed. Three other remarks still name
Ctrl+5 alone, and one of them is a public member's doc comment that h7 makes false. Apply at
integration, once the other workflows' edits to the file are in.

**a.** `SetCellFormatAsync`'s remarks, lines 652 to 656 (public, so a Consumer reads it):

old:
```csharp
    /// never widens (case 18). Format Cells' OK does the same. A Font, a Fill or Borders widens
    /// nothing, strikethrough among them: Excel's Ctrl+5 widens as a Number Format key does (the
    /// fourteenth Windows run, case 7), but Format Cells' Font tab is read as Ctrl+B is, which no
    /// run has seen widen, and this method sets a Font as that tab does. <see cref="DoAsync"/> does
    /// an edit exactly as it is written, and widens nothing.
```
new:
```csharp
    /// never widens (case 18). Format Cells' OK does the same. A Font, a Fill or Borders widens
    /// nothing, strikethrough and underline among them: Excel's Ctrl+5 and Ctrl+U widen as a Number
    /// Format key does (the fourteenth Windows run, case 7; the seventeenth, h7), and so did Excel's
    /// Format Cells' Font tab with Bold (h7), where Ctrl+B did not. Until the user decides what
    /// follows from that, the Font tab widens nothing, and this method sets a Font as that tab does.
    /// <see cref="DoAsync"/> does an edit exactly as it is written, and widens nothing.
```

**b.** `FormatSelectionAsync`'s summary, lines 1578 and 1579:

old:
```csharp
    /// step (ADR-0071, case 17), and so does a change <paramref name="widen"/> names although it
    /// sets none: Ctrl+5's (the fourteenth run's case 7).
```
new:
```csharp
    /// step (ADR-0071, case 17), and so does a change <paramref name="widen"/> names although it
    /// sets none: Ctrl+5's and Ctrl+U's (the fourteenth run's case 7; the seventeenth's h7).
```

**c.** `Widen`'s summary, lines 1638 to 1640:

old:
```csharp
    /// Formula whose result is a number, as Excel was observed to widen it (WD-007), and the
    /// numbers a Number Format, or Ctrl+5, no longer fits (ADR-0071, case 17; the fourteenth run's
    /// case 7), in a column the user has not
```
new:
```csharp
    /// Formula whose result is a number, as Excel was observed to widen it (WD-007), and the
    /// numbers a Number Format, Ctrl+5 or Ctrl+U no longer fits (ADR-0071, case 17; the fourteenth
    /// run's case 7; the seventeenth's h7), in a column the user has not
```

## 5. Optional: `src/ExSheet/Toolbar/ToolbarItems.cs`, the Underline toggle's summary

The brief kept the toggles' behaviour as it is, and this area did not touch the file. The Underline
item's summary does not say that it widens nothing where Ctrl+U now widens. If the orchestrator
wants it said, as the Strikethrough item says it:

old:
```csharp
/// <summary>A single underline (ADR-0100): Ctrl+U's toggle.</summary>
```
new:
```csharp
/// <summary>
/// A single underline (ADR-0100): Ctrl+U's toggle. It sets the Font as Format Cells' Font tab does,
/// which widens no column, where Ctrl+U widens as a Number Format key does (ADR-0071, the
/// seventeenth Windows run's h7).
/// </summary>
```

## For the user

- **Format Cells' Font tab widened in Excel** (h7: Bold, then OK, widened column A to 11.73). Ctrl+B
  did not widen in the fourteenth run. Does Format Cells' OK widen for a Font change? If so, does it
  widen for every Font change, or only when something is set? And do `SetCellFormatAsync` and the
  Sheet Toolbar's toggles, which set a Font as that tab does, widen with it? Nothing here changes
  them.
