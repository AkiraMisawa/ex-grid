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

2026-10-01, agent cf-88, after CI run 36929390859. On Linux, `mud.spec.mjs` › "the Wrapper's row rule
runs on across the Pinned Column" failed in the dark scheme, on both hosts. The pinned cell's line was
`[79,79,86]` and the scrollable cell's `[78,78,86]`.
- **The cause is the stage where the blend is done.** MudBlazor's dark rule is
  `rgba(255,255,255,30/255)` on `#373740`. The exact blend is 78.53 in red and green, which rounds to
  79 or 78, and 86.47 in blue, which gives 86 either way.
  - The pinned cell paints that blend onto its own opaque ground as it paints.
  - A scrollable cell lies on a transparent row. Its rule reached the grid's ground only when the
    Viewport's layer (`will-change: transform`) was composited. That second blend rounded down on
    Linux and up on this Mac, where every path I tried gave 79: GPU, `--disable-gpu`,
    `--disable-gpu-compositing` and SwiftShader. So Linux could not be reproduced here, and the cause
    is reasoned from the numbers.
- **So the two are made to paint identically.** The row now paints the grid's own ground,
  `:where(.ex-row) { background-color: var(--ex-background, Canvas) }`. The rule, or a stripe, is
  then blended onto an opaque ground as the row paints, as it is on the pinned cell.
  - The rule has zero specificity, so ExSheet's Paper on its rows wins whatever the order.
  - Nothing in the Viewport lies beneath the rows: the selection and its bands are above them
    (z-index 1).
- **The test still compares exactly**, and its comment says why.
- **Tests.**
  - A layer-2 test covers the row's ground and its specificity.
  - Locally the mud, stripes, appearance, selection-look, sheet-paper and presentation specs pass 94 of
    95. The one exception is stripes' vertical-scrollbar test, which fails on this Mac's
    headless overlay scrollbar, before and after.
  - CI is the judge of the Linux pixels.
- **If CI still parts them**, the paths could not be made one. The next step is then a tolerance of
  one level, for this half-level blend, and nothing wider.

2026-10-01, agent cf-88, after CI run 36934151352, at 523c0b9, which includes the opaque row ground.
- **The split is unchanged.** The pinned cell is still 79 and the scrollable cell 78, on chrome and
  msedge and on both hosts. So the reading above was wrong: the split is not the compositing of a
  transparent row. Both lines are now a translucent rule painted onto an opaque ground as the element
  paints, and Linux's Chrome still rounds the half-level blend two ways. I could not find which part
  of the two paths does it, because no Mac path reproduces it.
- **The opaque row ground is reverted.** It did not change what it was added for. It would also have
  repainted the rows of a Consumer that sets the grid's ground other than through `--ex-background`,
  for no gain. Its layer-2 test goes with it.
- **The test takes the fallback named above:** the two lines compared within one level on each
  channel, and nothing wider. The blend is 78.53, half a level from both 78 and 79. A rule missing
  from the pinned cell would leave its ground, about 23 levels away, so the check still catches the
  defect this ticket fixed. The grounds above the line are still compared exactly. The test's comment
  says all of this.
