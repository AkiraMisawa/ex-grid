# 02: The Cell Class

Status: ready-for-agent

**What to build:** a Column's Cell Class in `ExGrid`
([ADR-0121](../../../adr/0121-a-consumers-cell-class-paints-and-only-paints.md)). This is a product
change, and it gates ExGrid's release (§26).

- **The declaration:** a function of the row on the Column, returning one class name or null, asked
  when the row renders, for value cells only.
- **Refusals:** a name with whitespace, or one that begins with `ex-`, is refused by name.
- **Interning:** one class string is interned per distinct name, with the grid's marker class, as
  `CellClasses` interns the closed enums.
- **`ex-grid.css`:** on the marker it holds, `!important`, every property ADR-0121 lists. It clamps
  `--ex-cell-font-weight` to 600 or below, and sets no transition and no animation. Check that
  `clamp()` and `round()` in `font-weight` behave in Chrome and Edge, and record the result in the
  ADR.
- **The Wrapper:** `ExGrid.MudBlazor` keeps refusing `CellClassFunc`.
- **A demo page** whose Cell Class sets every held property, a weight of 800, a transition and an
  animation.

**Blocked by:** None

- [ ] A `spikes/render-bench` mode measures the Cell Class beside a Tone rule, before the
  declaration is merged
- [ ] DC-67, layer 2: the class and the marker, the refusals, render counts, nothing without the
  declaration
- [ ] DC-68, layer 3 on both hosts: on the demo page, overflow read from the layout agrees with the
  core's decision on every cell, and the weight paints at 600
- [ ] DC-69, layer 3: UX-6 holds on the demo page, and the overlay, Focus outline and Change
  Highlight paint over a classed cell
- [ ] ADR-0029's tables and `docs/implementation-status.md` list the marker and the token
