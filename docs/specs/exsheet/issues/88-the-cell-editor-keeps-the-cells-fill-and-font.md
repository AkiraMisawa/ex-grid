# 88: The Cell Editor keeps the cell's Fill and Font

Status: done

**What to build:** an Excel fidelity gap ticket 48 found. In Excel, a yellow, bold, red cell stays yellow,
bold and red while it is edited. ExSheet's Cell Editor shows the Paper and the Ink. The core's editor box
takes no appearance.

**Blocked by:** None (can start immediately)

- [x] **The core's Cell Editor takes the edited cell's appearance** (ADR-0050 item 15): its Fill as the
      ground and its Font as the text. Italic, underline and strikethrough follow if Excel shows them
      while editing. Check the record of the eleventh run, and if it says nothing, note that as a reading.
- [x] **A Number Format's colour is not the editor's.** The editor shows the Entry, not the formatted
      Value.
- [x] **ADR-0057's coloured References still read** over a Fill.
- [x] **No row height changes** and no geometry moves. The editor's box stays where it is.
- [x] **Tests:** layer 2 for the markup; layer-3 pixels in a spec. CI runs them.

## Comments

2026-10-01, agent cf-88.

### What the record says

The eleventh run's record says nothing about how a formatted cell looks while it is edited. Its one
edit, case 21, opened on an unformatted `abc`. No other run looked either. So every part of the
editor's look below is a **reading**, built and not observed:
- the Fill as the editor's ground, and the Font's colour and bold as its text, which is the ticket's
  own premise, from ticket 48;
- italic, underline and strikethrough, which follow as the rest of the Font does;
- a Number Format's colour, which the editor does not take, because it shows the Entry;
- ADR-0057's References over a Fill, which keep their palette colours, with the cell's Font weight
  and slant;
- an underline or a strikethrough under a coloured Reference, drawn in the Font's colour, because
  the field's line lies over the coloured text's.

### What was built

**The core** (ADR-0050 item 15):
- **A new grid parameter, `EditorAppearance`** (`CellAppearanceOf<TRow>?`). It answers the look the
  Cell Editor takes over the cell it edits: the Fill as the editor's ground, and the Font as its
  text. Its Borders are not read. When it is null, the editor takes the `CellAppearance` answer, so
  a Consumer declares it only where the editor should look unlike the cell. Without either
  declaration nothing is asked and the editor is as before (DC-1).
- **The editor wears the cell's own interned classes** (`ex-font-…`, `ex-fill-…`). They go on the
  core's field, on the box a Chrome's control stands in, and on the coloured text in the cell
  (`.ex-reference-text-cell`). The Formula Bar keeps its own look.
- **Each class gains one editor rule in the generated stylesheet**, the first time an editor names
  it (`AppearanceStyles.EditorClassFor`). The rule sets the editor's own tokens rather than its
  background and colour:
  - the Fill is `--ex-editor-background`;
  - the Font's colour is `--ex-editor-color`;
  - bold, italic and the decoration are set directly.
  So the field still turns see-through while the coloured text shows (ADR-0057's
  `background: transparent` wins), and the text is read on the Fill.
- **A Chrome's control inherits colour and weight from the box.** A decoration reaches neither a
  control nor an absolutely placed layer, so the rule also names the box's `input`, `textarea` and
  `.ex-reference-text`. These are the fields the listener already recognises.
- **The rule has no child combinator.** The `<style>` text would carry it escaped as `&gt;` from
  bUnit's renderer, and from a prerender, which would drop the whole rule.
- **The class string is interned.** A render that changes nothing writes no class, so the
  listener's `ex-reference-text-shown` on the field stays.
- **No geometry moves.** The box, its inline geometry, its padding and its outline are untouched,
  and no row renders (ED-1).

**ExSheet**:
- `EditorAppearance="SheetAppearance.Editor"`, which reads the cell's Font and Fill afresh, with the
  Font's own colour (`SheetRow.EditorAppearanceAt`). So -5 in `0;[Red]-0` under a blue Font shows
  red in the cell and blue in the editor. An Automatic Font is the Ink, and a row's or column's Fill
  is the ground of a cell that holds nothing.
- `ex-sheet.css`'s comment and `src/ExSheet/README.md` say so.

### Tests

- **Layer 2, core.** `CellEditorAppearanceTests`, 9 tests:
  - the field's classes and the two editor rules;
  - the box's inline geometry and the rows' are the same as without the look;
  - no declaration, and a cell with no look, give `ex-editor` alone;
  - `EditorAppearance` outranks `CellAppearance`, and alone paints no cell;
  - the pinned editor;
  - the coloured text, with the Formula Bar left alone;
  - a Chrome's box;
  - no row renders, and no more JavaScript calls than without the look.
- **Layer 2, ExSheet.** `SheetAppearanceTests` gains 2 tests:
  - B1 (-5, `0;[Red]-0`, blue, bold, yellow) is `ex-font-ff0000b` in the cell, and the editor and
    its coloured text are `ex-font-0000ffb ex-fill-ffff00`;
  - an empty cell under a column's Fill edits on that Fill.
- **Layer 3.** `sheet-paper.spec.mjs`, 6 tests (DC-58/SH-39):
  - Under both Chromes and both schemes, on `/sheet?case=paper`, F2 is pressed on A1 (a red Font,
    pinned), B1 (the case above), C1 (bold), D1 (italic), A2 (underline) and B2 (strikethrough).
    The field and the coloured text each resolve to the Font's colour, weight, slant and
    decoration. The ground is the Paper. The editor's box is the cell's to 0.5 px, and the row
    keeps its height.
  - Under both Chromes, C3 (yellow Fill): the editor's ground is yellow and its text the Ink. Then
    `=A1+B1` shows the coloured text, with the field see-through. Both References' colours resolve
    and are painted in the editor over the yellow.
  - Headless on this Mac, `--project=chrome`: all 6 failed with the tests in place and `src/`
    stashed. With the fix, `sheet-paper.spec.mjs` passed 17 of 17 on WebAssembly. Tickets 88's and
    90's 8 tests passed 8 of 8 on the Server host.
- **Layers 1 and 2 in full**: ExGrid.Tests 874, ExSheet.Engine.Tests 2333, ExGrid.MudBlazor.Tests
  133, ExGrid.Components 1282 (one skipped), ExSheet.MudBlazor.Tests 39, ExSheet.Components.Tests
  562.

### Seen on the way, not built

- **The editor's box covers the cell's own share of its Borders while the edit is open**: its
  bottom and right lines, which the cell draws inside its box. This was so before this ticket. On
  the Sheet it shows once ticket 49 declares Borders. Whether Excel keeps a cell's borders visible
  around its in-cell editor is not in any record, and the box was not moved (this ticket's last
  box).
