# ExChart ships on the family's train, is measured without a gate, and is checked by machine

*Decided with the user, 2026-10-19 (M4 grilling Q6, Q7 and Q8). This fills the plan's M4 (performance)
and M5 (shipping); ADR-0252 and DoD §33 already settled that render counts gate and timings do not.*

## The same tag ships thirteen packages

ExChart's three packages join the family's train (ADR-0042, "The family ships together"): one `v*`
tag, one version, thirteen packages. `ExChart` depends on exactly the `ExGrid` it was built with, as
ExSheet and ExPivot do.

- **PackageSmoke adds the three**, and checks from the `.nuspec` that `ExChart.Engine` depends on
  nothing, ExGrid included (ADR-0250: a chart without a grid).
- **ExChart stays out of `src/` until it is ready.** Whatever is in `src/` at a tag ships, so a
  half-built ExChart is kept out of the tree a tag packs, not shipped and apologised for.
- A release candidate that carries ExChart is held, like any other, by ADR-0208's checks; §33 holds
  ExChart's own criteria.

## Performance is measured and recorded, never gated

`spikes/render-bench` gains an ExChart mode: 1,566 dates × several series, timing a pointer move,
hiding a series, a dragged range and a data replacement, on Server, on WebAssembly and on WebAssembly
with AOT. The result JSON is recorded beside the proof of concept's 644 ms (Server) and 1.8 s
(WebAssembly) for its redraw. Whether to recommend AOT to a Consumer is decided from those numbers
and said on the Overview page (ADR-0280).

## What a person used to check is checked by machine

*The user, 2026-10-19: automated, not by hand.* Three things that layer 3's ordinary specs do not
reach are each a layer-3 test, and **none is a row checked by hand** under ADR-0208:

- **The reading.** The accessible summary and "View as table" (ADR-0275) are fixed with an ARIA
  snapshot, so a change to the summary's words fails a test.
- **125% and 150%.** At `deviceScaleFactor` 1.25 and 1.5 the 2D plot's lines lie on Device Pixels,
  judged with `paints`, and the PNG (ADR-0282) and the 3D surface are drawn at that scale, not
  scaled up from 100%. *(Written more exactly than the grilling's answer, which said the 3D surface's
  lines lie on Device Pixels: a turned surface has no line along the pixel grid to lie on.)*
- **High contrast.** Under `forcedColors: 'active'` the series stay told apart by line style and
  marker (ADR-0256's other two keys), where their colours are forced away.

What no browser test can say is said instead (spine rule 1): **NVDA's actual speech and a real
Windows display are not verified.** §33 states it.

## Considered

- **A tag and version of ExChart's own** (`exchart-v*`): it could ship sooner, at the cost of
  deciding how `ExChart` binds the ExGrid it draws with, which ADR-0042's one version already answers.
- **`-preview` kept on ExChart after the family goes stable**: decided when the stable version is,
  in the change that rewrites ADR-0042.
- **No measurement at all**, since nothing gates on it: the AOT question would be answered by
  guessing.
- **Timings recorded in CI's layer 3**: a shared runner's swing makes a trend of noise.
- **Rows checked by hand on a Windows run**: the user chose automated checks, and names what they
  leave unverified rather than a person's reading of it.
