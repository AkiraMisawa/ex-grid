# 07: SH-45's Border tab does not check Medium on Edge

Status: needs-triage
Roadmap: ExSheet — The Border tab of Format Cells takes a line style pressed as soon as it opens.

**Seen:** `format-cells.spec.mjs:330`, "SH-45, run 18a format-cells 2: over B2 with a thick top and a
medium right the Border tab opens on the top's Thick", Server host, Edge, shard 3/4, in both runs:
`locator.check: Clicking the checkbox did not change its state` on `getByLabel('Medium')` in
`.ex-popover-consumer` (line 338).

**Related:** `docs/specs/exsheet/issues/240-a-popover-s-opening-focus-does-not-take-the-keyboard-back-from-a-press-inside-it.md`
fixed a press inside a popover that had just opened. Its last two boxes, 20 runs on each host, are not
ticked. This may be the same race on a pointer instead of a key.

- [ ] Find whether the press lands before the tab's controls take it, or is undone by a render.
- [ ] Fix it in the product.
- [ ] The test passes 20 runs on each browser under the widened conditions.
