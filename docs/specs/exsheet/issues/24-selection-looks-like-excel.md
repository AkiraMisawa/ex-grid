# 24: The Focus and a single range look like Excel's, and the header's rule is whole

Status: ready-for-agent

**What to build:** ADR-0008, "Excel's look for the Focus and a single range" (2026-09-29), and
ADR-0030's change of the same day, for ExGrid as a whole. Found on `/sheet`: the active cell's left
edge was half as thick as its other three, a selected range had no outline, and the header's rule
stopped short of column A.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [x] The Focus outline lies wholly inside its cell, so no layer above the selection layer covers
      part of it: beside a Pinned Column, beside the Headings, under the header (UX-18)
- [x] The fill handle stays centred on the outline's corner; the forced-colors outline of a range is
      drawn inside as well
- [x] The Focus cell is never tinted: a range holding the Focus is painted with a hole there, as
      geometry resolved in C# and emitted inline, and the range stays one element (UX-19)
- [x] A selection of one range shows one outline around the whole range and none around the Focus
      inside it; a one-cell selection shows the Focus outline alone; several ranges are each tinted
      with no outline, and the Focus cell among them untinted and outlined (UX-19)
- [x] The header's rule runs under a Pinned Column's header and under the Headings' corner (UX-17)
- [x] Every one of these reads an existing token: `--ex-focus-outline`, `--ex-header-rule-color`,
      and for the single range's outline `--ex-selection-outline`, the token ADR-0029 reserved for
      it, taken up with the Focus outline as its default; no new token is added (ADR-0029)
- [x] The hole adds nothing measurable to a drag: measured before it is claimed (ADR-0008;
      `verification/2026-09-29-selection-hole/`)

ExGrid.MudBlazor:

- [x] `--ex-focus-outline` is the palette's primary in the light scheme, and in the dark scheme the
      primary with its lightness raised through relative colour syntax (ADR-0030, 2026-09-29)
- [x] UX-9 measures both schemes; if the dark one does not clear 3:1, it keeps the ink colour and
      ADR-0030's paragraph says so

## To observe on the next Windows run

Carry these into the next `verify-on-windows` procedure. They are read, not observed, and ADR-0008
waits on them:

1. Excel, a single range dragged from A1 to C5: is there any outline around A1 itself, apart from
   the range's outline and the untinted cell?
2. Excel, a selection made of two ranges with Ctrl+click (A1:B2, then D4:E6): which of them carries
   an outline, if any; does the active cell carry a border; where is the fill handle, if any?
3. Excel, a whole column and a whole row selected from the Headings: the same questions as 1.

Screenshots of each, beside ExSheet showing the same selection.

## Comments

2026-09-29, implemented on `claude/exsheet-selection-look`; layer 3 written and not yet run by the
suite's own runner (the orchestrator runs it).

- **Inside outlines (UX-18).** `.ex-focus` and the forced-colors `.ex-range` are offset inward by
  their full width. Read in device pixels at a scale of 2 on `/sheet`, the Focus's left edge was 2
  beside the pinned A (B4) and beside the Headings (A4), and its top edge 2 under the header (C1),
  against 4 elsewhere; on `/wide` the first scrollable column's left edge was 2 (r0c2, r3c2), and so
  was column A's against the grid's own edge (r3c0). All are 4 on every side now. The fill handle's
  geometry is unchanged: centred on the range's corner, which is the outline's outer corner.
- **Header rule (UX-17).** A pinned header cell and the Headings' corner paint the rule themselves.
  The band's last device pixel under the corner and column A's header was the ground before and is
  the rule's colour now, as under B.
- **The hole and the single range (UX-19).** `SelectionStyles.Range` resolves each range's inline
  style: its box, the hole where the Focus is as an evenodd polygon (`--ex-range-hole`) that clips
  the range's `::before` tint only, and, for a range across the pinned boundary, the whole range in
  both layers each clipped to its side (`clip-path: inset(...)`), so nothing drawn inside the edge
  draws a seam there and no outline width is known in C#. A range that is the Focus's cell alone is
  not painted. A Selection of one range adds `ex-range-single`, outlined with the Focus outline's
  rule; the Focus inside it keeps its element, unoutlined, and forced colors outline it again.
- **The hole's cost.** CDP `Performance.getMetrics` main-thread time over a ten-step selecting drag
  on `/sheet`, Release builds, interleaved, medians of 20 to 30 drags. Server host: 13.2 → 15.0 ms
  in a first A/B and 14.1 → 14.7 ms in a second (interquartile ranges 12.0-19.8 and 12.4-17.7);
  WebAssembly: 28.2 → 29.0 and, across the pinned boundary, 28.0 → 26.8 ms. On the changed build,
  the hole switched off by an injected rule: 16.2 against 15.6 ms (Server) and 33.3 against 32.6 ms
  (WebAssembly), inside the run-to-run spread. Style recalculation rose by about 0.1 to 0.2 ms per
  drag (the `::before`). Nothing attributable to the hole was measured.
- **MudBlazor (ADR-0030).** MudBlazor 9 names its scheme only in `--mud-native-html-color-scheme`,
  which `MudThemeProvider` emits beside the palette; a style query reads it. Light: the primary
  #594ae2, 6.00:1 against the surface. Dark: `oklch(from primary max(l, 0.62) c h)`, rgb(124, 113,
  237), 3.05:1 against #373740 where the primary alone is 2.83:1.

Open, for the orchestrator:

- `ex-range-single` (a class) and `--ex-range-hole` (a custom property written inline on a range,
  never on the root) are new names in the presentation surface; ADR-0029's lists do not have them.
  Proposed as internal.
- With `HighlightFocusRow` on, the Focus band still tints the Focus's cell, and the hover band does
  when the pointer is on its row. UX-19's "never tinted" was read as the selection's tint.
- On `/mud`, where the Focus band is on, the dark outline stands on the band's tint over the
  surface, and the painted ratio there is 2.92:1. A floor of 0.63 would clear that too (3.04:1).
  UX-9 measures against the cell ground, which is the surface, so 0.62 stands as decided.
- The dark floor is fixed for MudBlazor's default dark surface; a Consumer's own dark palette is not
  recomputed. CSS cannot compare two colours' luminance.

2026-09-29, second round, after the decisions of the same day (ADR-0008/0029/0030 as corrected):

- **The range outline's token.** `ex-range-single` reads `--ex-selection-outline`, defaulting to
  `--ex-focus-outline`; forced colors still outline every range in Highlight. Checking it by pixel
  showed the range's `::before` tint painted *over* the range's own outline, so the outline was
  tinted (rgb(11, 22, 39) where it should have been black). The range now isolates its painting and
  the tint lies beneath the outline; a drag measured 13.9 ms with that against 15.1 ms without,
  which is noise. Layer 3 now checks the outline's pixels are its own colour.
- **A correction to the first round.** Over MudBlazor's dark surface, the Focus band's tint paints
  rgb(60, 59, 77), which is the band's 8% of the primary over rgb(55, 55, 64). The first round
  sampled rgb(58, 57, 75) and reported 2.92:1 at a floor of 0.62. The figure at 0.62 is 2.83:1.
- **Re-check at the floor of 0.63**, painted, headless, the same on `/mud` and `/mud-app`: the dark
  outline is rgb(127, 116, 241), 3.18:1 against the surface and **2.95:1 against the Focus band's
  tint**, so it does not clear 3:1 there. UX-9's layer-3 test asserts the band case and is left
  failing on it. Measured by injecting other floors: 0.635 gives 3.02:1 over the band (3.26:1 over
  the surface), 0.64 gives 3.06:1 (3.31:1). Proposed: 0.64. The light scheme is the primary
  #594ae2: 6.00:1 against the surface, 5.36:1 painted over the band.

2026-09-29, third round: the dark floor is 0.64 (ADR-0030 as corrected). Re-checked headless, as
painted, the same on `/mud` and `/mud-app`: the dark outline is rgb(130, 119, 244), 3.31:1 against
the surface rgb(55, 55, 64) and 3.06:1 over the Focus band's tint rgb(60, 59, 77). The light outline
is the primary rgb(89, 74, 226), 6.00:1 against the surface and 5.36:1 over the band's tint
rgb(242, 241, 253). UX-9's band assertion passes at device scales 1 and 2, and so does the rest of
`mud.spec.mjs`.

The two `stripes.spec.mjs` failures seen in the second round's ad-hoc runs, headless on macOS:

- *a pinned and a scrollable cell of one striped row paint the same ground (UX-15)*: an artefact of
  the ad-hoc harness, which lacked `scrollRowToTop`. With it, the test passes on both the changed
  build and the pre-change build.
- *the vertical scrollbar paints a thumb in its gutter (ADR-0029, UX-10)*: fails at "the bar
  occupies layout" with a gutter of 0, the same way on the pre-change build. Headless Chrome on
  macOS keeps overlay scrollbars, which the suite's README predicts; the real layer-3 run is the one
  that answers it.

2026-09-30, after review: the hole's measurement is recorded in
`verification/2026-09-29-selection-hole/`, with the script, its driver and a README giving the first
round's medians and interquartile ranges, their builds, host and date. That round's per-drag rows
were not kept; a re-run with the committed scripts, which write every drag to `results.json`, is
pending on a machine no other session is loading. A re-run tried at 23:25 on 2026-09-29 under a load
average of 60 to 85 from other sessions' layer-3 runs was stopped and is not used. The box on tokens
above now names `--ex-selection-outline`, the reserved token the single range's outline took up.

2026-09-30, layer 3 by the suite's own runner, headless (the user's instruction on this machine),
Chrome only (Edge is not installed here), both hosts, at 18a6e2e's code: the eight specs this ticket
touched (selection-look, mud, sheet, declarations, sheet-vs-excel, features, stripes, presentation)
157 passed, 4 failed, 7 skipped on each host; the other specs that read the overlay (gestures, find,
mud-app, scrollbar, virtualisation, sheets) 52 passed, 5 failed, 1 skipped on each. Every failure also
fails on the unchanged base (dd63bb6), in a full headless run on the same machine: the macOS overlay
scrollbar (UX-10, the scrollbar spec, the stripes thumb), the system clipboard (the two sheet-vs-excel
copies), DC-13, and WR-7's Drawer and tab. Nothing fails here that passes there. Headless macOS cannot
answer the scrollbar cases; CI's Linux run is where they count.
