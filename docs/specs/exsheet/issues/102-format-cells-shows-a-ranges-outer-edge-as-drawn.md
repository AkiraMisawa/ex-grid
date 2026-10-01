# 102: Format Cells shows a range's outer edge as drawn

Status: done

**What to build:** the fourteenth Windows run's case 13. With A1's bottom thick and A2 recording
nothing, Format Cells over A2 shows a thick top, its button pressed. ADR-0071, "What the fourteenth
Windows run settled", ticket 57's model:

- **An outer edge of the selected range shows as it is drawn.** Where the cell records no line on that
  edge, the neighbour's line there shows.
- **An inside edge shows mixed where the two cells' own records differ** (the eleventh run's case 24),
  as today.
- **OK sets only what the user touched** (SH-45). A shown edge left alone writes nothing. Taking it away
  clears that edge on both sides, as clearing an edge does. *(A reading: no run pressed it.)*

**Blocked by:** None (can start immediately)

- [x] **A2 under A1's thick bottom opens with a thick top**, under the built-in Chrome and under
      `ExSheet.MudBlazor`. Over several ranges, each range's outer edges show this way.
- [x] **OK without a change sets nothing:** A2 still records no top, and no undo step is added.
- [x] **Taking the shown top away and pressing OK** clears A1's bottom too: no line is drawn there.
- [x] **Layer 2 under both Chromes.** Layer 3 if the change shows in a browser; CI runs it.

## Comments

*(2026-10-02, agent cf-102.)* Format Cells now shows a range's outer edges as they are drawn.

- **The engine answers the lines along an edge.** `Sheet.GetEdgeLines(range)` is new, and returns a
  new public record, `RangeEdgeLines`. For each of the range's four outer edges it gives the lines
  drawn there, each once.
  - Each line is the one `GetBorders` answers on that side of a cell: the neighbour's line where the
    cell records none, and the upper (left) cell's where both record one. A side on the Sheet's edge
    is the cell's own.
  - It is read from what the Sheet records, as `GetCellFormats` is. Cells beside the edge that record
    Borders of their own are read one by one. Every other place is read from the level across the
    edge there. So the left edge of whole columns costs what is recorded, not a million cells.
- **The draft uses it** (`FormatCellsSpread` in `src/ExSheet/FormatCellsDraft.cs`). Top, bottom, left
  and right take each range's drawn lines. Horizontal and Vertical still compare the cells' own
  sides, so the eleventh run's case 24 stays mixed. Both Chromes read the draft, so both show it.
- **OK is unchanged.** It sets only what was touched, so a shown edge left alone writes nothing.
  Taking it away sets that edge to none, and the engine already clears the neighbour's record of an
  edge it sets. So A1's bottom goes too.
- **Where Excel was not observed.** Where both cells record a line on an outer edge, the dialog shows
  the drawn one, the upper or left cell's. The run saw only a cell that records nothing. This follows
  "as it is drawn".
- **Tests.**
  - **Layer 1**, `CellFormatSpreadTests`, 6 new tests:
    - case 13 itself;
    - the upper or left cell's line where both record one;
    - differing lines along an edge;
    - whole columns read from their levels;
    - cells that cover a level hide it;
    - a cell-by-cell comparison with `GetBorders`. It covers 11 ranges over cells, rows, columns and
      the Sheet's edges, whole columns and rows among them.
  - **Layer 2, draft:** `FormatCellsDraftTests`, 4 new tests. They cover A2's thick top, several
    ranges, a shown edge left alone, and taking it away on both sides. The old reading ("A2 alone
    records no top") is replaced by the run's answer.
  - **Layer 2, built-in Chrome:** `FormatCellsTests`, 5 new tests:
    - the Top button pressed and the preview's thick line;
    - two ranges by Ctrl+click: the same line shows pressed, a differing one mixed;
    - OK with nothing touched: no undo step, and A2 still records nothing (read back from the Sheet
      Document);
    - taking the top away clears A1's bottom.
  - **Layer 2, `ExSheet.MudBlazor`:** `MudFormatCellsTests`, 3 new tests: the same, under the
    MudDialog.
  - **Layer 3:** one new test in `format-cells.spec.mjs` (built-in Chrome; the Mud spec covers the
    frame, not the content). It sets A1's thick bottom through the dialog, then opens A2 and checks
    that Top is pressed. **It was written and not run here**, as the task asked; CI runs it.
  - `nix develop -c dotnet test ExGrid.slnx`: ExGrid.Tests 1089, ExGrid.MudBlazor.Tests 198,
    ExSheet.Engine.Tests 2340, ExGrid.Components 1356 (+1 skipped), ExSheet.MudBlazor.Tests 47,
    ExSheet.Components.Tests 605. All pass.

