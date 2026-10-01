# 86: ADR-0057's coloured layer lies over the field's text under the Wrapper's font

Status: done

**What to build:** the fix ticket 48 found and decided (its comment, "DC-48 under ExSheet.MudBlazor, at End").
- Under `ExSheet.MudBlazor`'s Chrome, with Roboto, the coloured layer of
  [ADR-0057](../../../adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) drifts from
  the Cell Editor's own text at the far end of a long Formula: 112 pixels differ at End.
- MudBlazor's grey text hid it. Ticket 48 draws the editor in the Ink, and 2 pixels now cross DC-48's
  threshold, so `reference-text.spec.mjs` › "DC-48 … (mud Chrome)" fails at End.
- The built-in Chrome shows 88 differing pixels, none past the threshold.

**Blocked by:** None (can start immediately)

- [x] **Find where the layer and the field part.** The test's comment names per-span snapping. Check
      the font's metrics, `letter-spacing`, `font-kerning`, `text-rendering` and `font-feature-settings`
      on the layer against the field, under the Wrapper.
- [x] **Make the layer draw where the field draws**, under both Chromes. Add no script beyond ADR-0057's
      allowance (ADR-0021). *The allowance as ADR-0057's and ADR-0021's notes of 2026-10-01 extend it.*
- [x] **DC-48 passes at its threshold under both Chromes**, at both ends, on both hosts. The threshold
      is not loosened.
- [x] **Say in the comment** how many pixels still differ under each Chrome.

## Comments

2026-10-01, agent cf-86.

The first box is done. The rest needs a decision. The drift is not the Wrapper's font: it is the
layer's spans, under both Chromes. The fix that removes it is the one ADR-0057 names, the CSS Custom
Highlight API, and that is script beyond ADR-0057's allowance. I stopped there, as the brief says,
and changed nothing in `src/`. The proposal is at the end.

### Where the layer and the field part

Measured on `/sheet` with DC-48's Formula, headless Chrome on this Mac, WebAssembly host.

- **The styles are the same.** Every computed `font-*`, `text-*`, `letter-spacing`, `word-spacing`,
  `line-height` and `-webkit-font-*` property of the layer, its line and a span equals the field's,
  under both Chromes. That includes `font-kerning`, `text-rendering`, `font-feature-settings` and
  `font-optical-sizing`. Only `white-space`, `unicode-bidi` and the colours differ, and none of them
  moves a glyph. Both boxes start at the same fractional x (613.09375 under the Wrapper), and both
  scroll offsets are 1398 at End.
- **The spans are what part them.** Chrome rounds each run of text in a line up to its layout unit,
  1/64 px, and positions the next run after the rounded width. The field's text is one run. The
  layer's is fifteen: seven References and the text between them.
  - Against one run of the same text in the same box, the layer's runs start 0, 1/64, 2/64, 3/64
    and, from `B7` on, 4/64 px late.
  - So at End the visible text, `B8` and what follows, stands 1/16 px right of the field's. That is
    the same under both Chromes: the layer's line is 1481 px long against 1480.953 under the
    Wrapper, and 1595.172 against 1595.109 under the built-in Chrome.
- **Shifted back, the two are one picture.** With the layer's line moved left by 1/32 to 1/16 px
  (a `text-indent` set in the page for the experiment), the Wrapper's Cell Editor at End has 1 pixel
  differing, none apart, and the built-in Chrome's has none at all.
- **One run removes it.** With the layer's line replaced, in the page, by one text node of the same
  text, the field and the layer are one picture at both ends, in both surfaces, under both Chromes
  (the table below).
- **Why only the Wrapper fails.** Chrome draws a glyph at one of four quarter-pixel phases. A 1/16 px
  shift can move a glyph into the next phase, and the edge pixels of a glyph moved a
  quarter pixel change by up to about 100 levels. The test's threshold is 96. Roboto's edges in
  `B8`, `"yyyy-mm-dd")` crossed it at 1 or 2 pixels, and the built-in font's did not.
  - Ticket 48's Ink only raised the contrast. MudBlazor's `#424242` scaled the same differences
    under 96.
  - The Formula Bar has the same drift over more of the Formula. It shows the first References, so
    it differs at Home as well. None of its differences went over 96 in these runs.

### The pixels that differ, per Chrome

The test's own comparison (`pixelsApart`): differing pixels / pixels over 96, on this tree.

| | Cell Editor, End | Cell Editor, Home | Formula Bar, End | Formula Bar, Home |
|---|---|---|---|---|
| Built-in, now | 88 / 0 | 0 / 0 | 1570 / 0 | 900 / 0 |
| Built-in, one run | 0 / 0 | 0 / 0 | 607 / 0 | 607 / 0 |
| Wrapper, now | 94 / **1** | 2 / 0 | 732 / 0 | 294 / 0 |
| Wrapper, one run | 1 / 0 | 2 / 0 | 5 / 0 | 7 / 0 |

- The built-in bar's 607 are its bottom row of pixels, one per column of the 607 px picture, at
  both ends. They are the bar's edge, not text, and they are there with or without the spans.
- Ticket 48 measured 112 / 2 for the Wrapper's Cell Editor at End. Here it is 94 / 1 on
  WebAssembly. The Server host fails the same test at End with 1 pixel apart.
- The "one run" rows are a plain text node in place of the spans. The prototype below, with its
  colours set to the Ink as the test draws the layer, gave the same numbers wherever both were
  measured. That is every cell but the built-in Cell Editor, whose run stopped on a screenshot error
  of the experiment's own after the plain text node's 0 / 0.

### No fix without script

Every way to colour part of a text without splitting it into runs is a highlight pseudo-element
(`::highlight()`, `::selection`). Only script can say which characters a highlight covers.
Without script, these were tried or ruled out:

- **SVG text with a `tspan` per Reference.** SVG places characters in floats, but the SVG root
  snaps to whole pixels, and its text does not draw as the field's: 165 pixels differing and 73
  apart at Home, worse than now. Measured in the page.
- **One copy of the text per Reference,** with everything before the Reference in one transparent
  run. Each Reference is then placed from one run, so its error is one rounding (under 1/64 px),
  not none. The layer's text grows with References × length: about 160,000 characters for eighty
  References in a thousand-character Formula. All of it is re-rendered on each key, and sent on each
  key over a circuit.
- **Colour bands blended over the field's own text** (`mix-blend-mode: screen`). This is exact only
  for black Ink on a white ground. The bar follows the scheme, and so does the built-in Chrome
  elsewhere.
- **A tighter test, or a shorter Formula.** Both are ruled out: the threshold stays (the
  orchestrator's decision on ticket 48).

### Proposal: the layer as one run, coloured by the Custom Highlight API

This is the fix ADR-0057's own note names ("draw the layer as one run and colour it with the CSS
Custom Highlight API, which needs script of its own and a decision"). It needs that decision, an
amended note on ADR-0021, and DC-51 widened.

- **The core renders the layer's text as one text node,** and the References as data on the layer
  beside `data-ex-text`: each one's start, length and place in the palette, and the pointed span.
  It renders no spans.
- **The editor listener colours it.** The listener already decides when a layer shows (the texts
  equal, the surface focused, no composition). At that moment it also builds one `Range` per
  Reference over the layer's text node, from the offsets the render wrote. It adds each Range to
  the Highlight for its place, `ex-reference-1` to `ex-reference-7`, and the pointed span to
  `ex-reference-pointed`. It takes them away when the layer hides or the edit ends.
  - It reads no layout. Offsets come from an attribute, and a Range over a text node is not a
    measurement.
  - It runs nowhere new: on the input, the attribute change and the composition end it already
    hears.
- **The stylesheet paints the colours** with `::highlight(ex-reference-N) { color: var(--ex-reference-N) }`,
  the pointed shade the same way, and the grey ground as the pointed Highlight's
  `background-color`, above the colour by the Highlight's priority. The Visual Tokens stay as they
  are, and a Consumer still picks no colour.
- **Every grid on a page shares the browser's registry.** `CSS.highlights` is one per document, and
  a stylesheet names a highlight statically, so names cannot be per instance. Each instance would
  add and remove only its own Ranges in the shared named Highlights. That is state shared between
  instances in the browser's registry, which ADR-0018 and DC-54's wording would have to allow for.
  This is the one part I would ask the user about specifically.
- **Browsers.** The API is in Chromium from version 105. ADR-0017 targets Chromium only.
- **Prototype.** I built it in the page, against the Wrapper's Cell Editor and Formula Bar, with
  Ranges over one text node and `::highlight()` rules: the "one run" rows above. With the colours
  shown, `=IF(AND(B2…` draws `B2` in the first Reference colour, over the field's characters.
- **What else changes.**
  - DC-48's test sets the layer's colours to the Ink through `::highlight()`, not through
    `span { color: inherit }`. DC-47's frame sampler is unaffected: the layer's `textContent` is
    still the text.
  - ADR-0057's paragraph that accepts the drift goes, since the drift does.
  - The `CellEditorContext.ReferenceText` and `FormulaBarTextContext.ReferenceText` fragments keep
    their shape, so neither Chrome changes.

Nothing is committed for this ticket except this comment.

*(2026-10-01, orchestrator.)* **Decided with the user: the CSS Custom Highlight API.** This replaces a short-lived
decision, taken the same day, to keep accepting the drift.
- ADR-0057's note, ADR-0021's note and DC-51 of this date say what the script may do.
- The layer becomes one text node, and its References are coloured by highlights named per instance, which carry
  the grid's id. The grid's generated stylesheet paints them (ADR-0018).
- [x] DC-48 passes at its unchanged threshold under both Chromes, at both ends, on both hosts.
- [x] Two grids on a page each keep their colours while the other edits (ADR-0018).
- [x] An IME composition still recolours on `compositionend`. Dark-scheme, pointed and forced-colours shades are
      unchanged.

2026-10-01, agent cf-86, building the decision.

Built as decided. The layer's text is one run, and its References are coloured by the CSS Custom
Highlight API, under highlight names of the grid's own. DC-48 passes at its unchanged threshold, under
both Chromes, at both ends and on both hosts.

### What was built

- **One run** (`ExGrid.ReferenceText.cs`, `ReferenceHighlights.cs`). The layer's line holds the
  edit's text and nothing else, under both Chromes and in both surfaces. The core writes on the layer,
  beside `data-ex-text`, which highlight covers which characters: `data-ex-colours`, with
  `start,length,name` per stretch.
  - Each Reference is one stretch, in its colour's highlight.
  - The span Point wrote is a stretch on the pointed ground, written first. The References inside
    it wear their pointed shades: a Reference standing exactly there, or the References of a lookup
    a press on another grid wrote.
  - A span that cuts through a Reference wears no look, as before.
  - The string is written again only when the colouring or the pointed span changes.
- **Names of the grid's own.** Each name carries the grid's id prefix:
  `ex12-reference-1` to `-7`, `ex12-reference-1-pointed` to `-7-pointed`, and
  `ex12-reference-pointed` for the ground.
  - The grid renders its own `<style>` with one rule per name, fifteen in all, wherever a References
    function is declared. There is no `>` in it, so a prerender writes it as it is.
  - Each rule reads a property the shipped stylesheet declares on the layer:
    `--ex-reference-text-N`, `--ex-reference-text-N-pointed` and `--ex-reference-text-pointed`. Those
    take the Visual Tokens with the defaults the spans had, so a Theme and the dark scheme reach the
    highlights as they reached the spans. The generated stylesheet holds no colour.
- **The editor listener** (`ex-grid.js`). When it shows a layer, the listener builds one `Range` per
  stretch over the layer's one text node, from `data-ex-colours`. It adds each `Range` to the
  highlight of that name.
  - The listener registers a highlight the first time this grid uses its name. The grid's dispose
    deletes from `CSS.highlights` only the highlights the grid registered.
  - Hiding a layer takes its ranges out. So does the edit closing.
  - It builds again only when the layer's text, its colours or its text node change, or when a range
    has been moved by a change to the text. It reads no layout, and it hears no new event.
- **Forced colours.** Under forced colours, Chrome paints a custom highlight in `Highlight` and
  `HighlightText`, whatever its rule says. That set each Reference in a dark block, where the spans
  came out as plain text.
  - In the forced-colours block, the layer's line is `forced-color-adjust: none`, in `CanvasText`.
    Its colour properties are all `CanvasText`, and the ground is transparent.
  - Measured against the span build under forced colours, in the cell and in the bar: the same
    pixels. In the dark and light schemes the same colours appear, with the dark scheme's lifted
    colours in the bar and the pointed shade on the grey in the cell.

### One thing for the orchestrator: DC-51 says "one attribute"

DC-51 and ADR-0021's note say the `MutationObserver` watches one attribute of the layer. It watches
two now: `data-ex-text` and `data-ex-colours`. The colours can change while the text does not, and
watching only the text would leave a stale look until the next key:
- Point ends without the text changing.
- A Linked Table's declaration arrives during an edit.

It is the same observer, on the layer's own attributes, and reads no layout. DC-51's wording wants
"two attributes" or "its text and its colours". I have not edited it.

### Tests

- **Layer 2.**
  - `ReferenceTextTests`, new: the line holds one run on both surfaces; each stretch names the grid's
    own highlights; two grids have different prefixes; each grid's stylesheet paints exactly its
    fifteen names from the layer's properties, with no colour in it. Also, without a References
    function no `::highlight` is written (DC-1).
  - `ShippedStylesheetTests`:
    - the script's shape: the gate, the two attributes, `new Highlight()`, the ranges, the deletes
      and dispose;
    - the pointed shades as layer properties (DC-56);
    - a new test for the forced-colours block.
  - The layer-2 tests that read a layer's markup now read it through `tests/ReferenceText/ColouredText.cs`,
    linked into the three test projects. It writes the layer's colouring back as the markup the
    tests already expected. It also checks the colouring is consistent: one run, a pointed shade
    exactly on the grey, and every name the grid's own. Every expected string is unchanged.
- **Layer 3.** The new helper `stretchesOf` in `sheet-helpers.mjs` reads the ranges the listener
  registered in `CSS.highlights`, and the colours the grid's stylesheet paints them in. It replaces
  the span readers in reference-text, pointing-scope, declarations and sheet-paper.
  - DC-47's frame sampler: in every frame, a shown layer's highlights cover exactly the stretches the
    core named, and a hidden layer has none.
  - DC-47's IME test: no highlight while composing, and `A1` coloured again after `compositionend`.
  - DC-47 on WebAssembly: one run, three References in three colours.
  - DC-48's drawing of the layer in the Ink sets the layer's colour properties to `currentColor`, in
    place of the spans' colour. The threshold is unchanged.
  - `sheets.spec.mjs`, new (ADR-0018): the left Sheet colours `=B1+C1+`, and the right Sheet colours
    `=C1+` with a different prefix. Each stylesheet paints only its own names. A press back on the
    left's rows brings back the left's colours, in the colours they wore; a press back on the right
    brings back the right's.
    - With one shared name forced into the build for a check, this test fails where the left's
      colours should come back.

### Pixels that differ now

DC-48's comparison: differing pixels / pixels over 96.

| | Cell Editor, End | Cell Editor, Home | Formula Bar, End | Formula Bar, Home |
|---|---|---|---|---|
| Built-in | 0 / 0 | 0 / 0 | 606 / 0 | 606 / 0 |
| Wrapper | 1 / 0 | 2 / 0 | 5 / 0 | 7 / 0 |

- The built-in bar's 606 are its bottom row of pixels. That row is the bar's edge, not text, and it
  differed the same way with the spans.
- With the spans the table was 88 / 0, 0 / 0, 1570 / 0 and 900 / 0 for the built-in Chrome, and 94 / 1,
  2 / 0, 732 / 0 and 294 / 0 for the Wrapper.
- WebAssembly and Server gave the same numbers.

### Runs

- **Layer 3**, headless on this Mac, `--project=chrome`.
  - **WebAssembly:** `reference-text.spec.mjs` whole, 25 passed. The tests touched in sheets,
    pointing-scope, sheet-paper and declarations also ran: 67 passed and 2 failed.
  - The 2 failures are ticket 90's pinned-cell gridline test, under both Chromes. It also fails on
    this tree's base without these changes.
  - **Server:** DC-48, DC-47, DC-56, ADR-0051/0057 and the new two-Sheets test: 23 passed, and 2
    skipped by design (WebAssembly only).
- **Layers 1 and 2:**

  | Suite | Passed |
  |---|---|
  | ExGrid.Tests | 1001 |
  | ExSheet.Engine.Tests | 2333 |
  | ExGrid.MudBlazor.Tests | 168 |
  | ExGrid.Components | 1295 (one skipped) |
  | ExSheet.MudBlazor.Tests | 43 |
  | ExSheet.Components.Tests | 587 |

### Seen on the way

The probes of past Windows runs read the layer's spans:
- `verification/2026-09-30-windows-8/part-b-probe.mjs`;
- the `pointing-scope-probe.mjs` of runs 9 and 13.

They are records, and are left as they are. A run that uses them again needs `stretchesOf`'s reading.
