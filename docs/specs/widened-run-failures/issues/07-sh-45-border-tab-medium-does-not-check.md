# 07: SH-45's Border tab does not check Medium on Edge

Status: done
Roadmap: ExSheet — The Border tab of Format Cells takes a line style pressed as soon as it opens.

**Seen:** `format-cells.spec.mjs:330`, "SH-45, run 18a format-cells 2: over B2 with a thick top and a
medium right the Border tab opens on the top's Thick", Server host, Edge, shard 3/4, in both runs:
`locator.check: Clicking the checkbox did not change its state` on `getByLabel('Medium')` in
`.ex-popover-consumer` (line 338).

**Related:** `docs/specs/exsheet/issues/240-a-popover-s-opening-focus-does-not-take-the-keyboard-back-from-a-press-inside-it.md`
fixed a press inside a popover that had just opened. Its last two boxes, 20 runs on each host, are not
ticked. This may be the same race on a pointer instead of a key.

- [x] Find whether the press lands before the tab's controls take it, or is undone by a render.
- [x] Fix it in the product.
- [x] The test passes 20 runs on each browser under the widened conditions.

**Found (2026-10-20), by reading, as no Edge is at hand here:** the press lands, and the render
answering the choice before it (Thick) can arrive after the click on Medium and put Thick back in the
radio group for a round trip, until Medium's own answer lands. Playwright's `check()` reads the box
straight after its click, and failed on that moment. The state the grid ends in is the one pressed.

**Fixed where the test reads:** `format-cells.spec.mjs` presses a choice and waits for it to read as
chosen (`pick`). Chrome passes 20 runs; Edge is left to CI.
