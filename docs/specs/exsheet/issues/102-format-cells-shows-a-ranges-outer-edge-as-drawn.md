# 102: Format Cells shows a range's outer edge as drawn

Status: ready-for-agent

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

- [ ] **A2 under A1's thick bottom opens with a thick top**, under the built-in Chrome and under
      `ExSheet.MudBlazor`. Over several ranges, each range's outer edges show this way.
- [ ] **OK without a change sets nothing:** A2 still records no top, and no undo step is added.
- [ ] **Taking the shown top away and pressing OK** clears A1's bottom too: no line is drawn there.
- [ ] **Layer 2 under both Chromes.** Layer 3 if the change shows in a browser; CI runs it.

## Comments
