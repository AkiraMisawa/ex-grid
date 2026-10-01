# 92: A pinned cell shows the theme's row rule

Status: done

**What to build:** a defect ticket 88's agent read from the stylesheets but did not check in a browser. When a
theme turns the row rule on, as `ExGrid.MudBlazor` does, the core's Pinned Column cells hide it. Their ground is
opaque, and nothing paints the rule on them. A row's rule then stops at the pinned block.

Ticket 90 gave a lined cell the `--ex-row-rule` hook, which ExSheet sets on its own pinned cells. The core sets it
nowhere.

**Blocked by:** None (can start immediately)

- [x] **Read it in a browser first.** Use `/features?chrome=mud` or any grid with a Pinned Column under
      `ExGrid.MudBlazor`, and say whether the rule really stops there.
- [x] **If it does, a pinned cell paints the row rule as the scrollable cells show it.** The core does this by
      itself (for example by setting `--ex-row-rule` on `.ex-pinned`), so a bare ExGrid with the rule off paints
      as before.
- [x] **ExSheet's own gridline on pinned cells (ticket 90) is unchanged.**
- [x] **Layer 2 for the stylesheet. Layer-3 pixels under both Chromes.** CI runs them.

## Comments

2026-10-01, agent cf-88.

### Read in the browser first

On `/mud` the rule stops at the pinned block. This was read on the first grid (`.demo-paper-a`: the
pinned Book, MudBlazor's table lines on). On rows 1 and 2, the bottom device pixel was `#ffffff` in
the pinned cell and `#e0e0e0` in a scrollable cell. The screenshot shows the lines ending at Book's
right edge. Headless Chrome on this Mac, WebAssembly. So the defect is real.

### What was built

All of it is in the core's stylesheet (`src/ExGrid/wwwroot/ex-grid.css`), next to the row's own rule.
- **A Pinned Column's cell paints its row's rule itself.** It uses the same image the row paints, from
  the same token (`--ex-row-rule-color`, transparent by default), named in `--ex-row-rule`:
  `.ex-pinned:where(.ex-cell)`. It has one class's specificity and leaves header cells out.
  - With the rule off, the image is transparent, so a bare grid paints as before. The layer-3 test
    reads that too.
- **Each ground that paints over a pinned cell keeps the rule where the scrollable cells show it:**
  - **a Row Stripe:** `var(--ex-row-rule, none), var(--ex-tint)`. The rule lies above the stripe, as
    the row paints its rule above its stripe;
  - **a Missing state:** `var(--ex-tint), var(--ex-row-rule, none)`. The tint lies over the rule, as a
    scrollable cell's tint lies over its row's;
  - **a cell's lines:** `.ex-lined`, from ticket 90, unchanged.
- **Where the scrollable cells show no rule, the pinned cell paints none:**
  - **a Fill covers it.** The generated Fill rule now also names `--ex-row-rule:none`. It is more
    specific than the pinned cell's rule, so it wins whatever the order;
  - **a group or total row names none**, since its row paints none (ADR-0024).
- **ExSheet's rule from ticket 90 is unchanged.** On the Sheet it now says what the core says, for
  pinned cells without a Fill. It could be removed later; I left it, as the ticket asks.

### Tests

- **Layer 2.**
  - `PinnedRowRuleTests`, 6 tests, read from the shipped stylesheet's cascade over the rendered
    cells (`ShippedStylesheetTests.Winning`, moved there from `RowKindTests`). They cover: a pinned
    cell's rule against the row's own, and transparent by default; a scrollable cell paints none; a
    pinned header cell keeps the header's rule; the stripe, group, total and Missing orders; and the
    Fill's `none`, which outranks the pinned rule.
  - `CellAppearanceTests` follows: the Fill rule's text, the tint rules' layers, and which rules name
    `--ex-row-rule`.
  - Five of the new tests, and the two updated ones, fail on the tree before the fix. The header test
    passes either way, as it should.
- **Layer 3.**
  - `stripes.spec.mjs`, under the built-in Chrome: with the rule off, the pinned and scrollable cells'
    last line is their ground. With `--ex-row-rule-color` set on the grid, the line is the rule in
    the pinned Name and in Amount and Desk, on a striped and an unstriped row. The grounds above it
    match, and a group row shows no rule on its pinned cell.
  - `mud.spec.mjs`, under ExGrid.MudBlazor: on `/mud`, light and dark, the pinned Book's last line is
    the scrollable cell's rule on three rows.
  - Headless on this Mac, `--project=chrome`, WebAssembly: both failed with `src/` stashed, and pass
    with the fix. `stripes.spec.mjs`, `mud.spec.mjs`, `sheet-borders.spec.mjs`, `sheet-paper.spec.mjs`
    and `format-cells-mud.spec.mjs` otherwise pass. The one exception is stripes' "the vertical
    scrollbar paints a thumb in its gutter", which fails the same way on the tree before the fix:
    macOS's headless overlay scrollbar. That is CI's to judge.
- **Layers 1 and 2 in full**: ExGrid.Tests 1001, ExSheet.Engine.Tests 2333, ExGrid.MudBlazor.Tests
  168, ExGrid.Components 1291 (one skipped), ExSheet.MudBlazor.Tests 39, ExSheet.Components.Tests 587.

### Seen on the way, not built

A lined pinned cell on a striped row paints the stripe over the rule, in ticket 90's order: covers
must lie over the rule, and the tint over the covers. A pinned cell without lines, and the scrollable
cells, paint the rule over the stripe. The two differ only when the rule colour is translucent, and
then by the stripe's own alpha (2 to 5%).
