# Pivot touch drag resolves only registered live targets

Decided with the user, 2026-10-19, in the responsive/touch session, Q23; formalized when the user
asked to complete implementation prerequisites. [ADR-0045](./0045-exgrid-is-a-desktop-grid.md)
records the product behavior and its reasons. The number comes from this branch's reserved
0260–0269 block in [the numbering record](../agents/numbering.md).

This is an accepted addition to [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md),
not a claim of implemented or verified mobile support. The earlier unnumbered draft is replaced
by ADR-0260 through ADR-0263; the agreed behavior has not changed.

## Decision and reason

During that drag only, `elementFromPoint()` resolves registered Area, entry and explicit removal
targets belonging to its ExPivot instance. Report target changes and the final target with the
rendering identity they refer to. C# validates placement and applies a valid release. A release
outside the dedicated surface cancels; the report hidden behind it is not a touch removal target.

**Why browser-side:** the Field List's variable targets are laid out by the browser. Native HTML
drag events provide targets, but a captured pointer stream does not provide equivalent target
transitions. Recreating that layout in C# would introduce another layout model.

This exception permits neither grid-cell geometry reads nor cell scanning. It runs neither
during ordinary rendering nor when held input is replayed. ADR-0021's rejection of rediscovering
the target under an old coordinate at replay remains: the target there now may not be the target
the user acted on.

## Ownership and lifetime

The owning product decides meaning in C#. Its external surfaces retain that owner and use its
[Input Order](./0240-input-to-one-product-instance-is-taken-in-one-order.md); a shared rendering
host does not introduce shared product state or a separate input queue. ADR-0240's implementation
is a dependency to establish, not a facility assumed complete because its decision is written.
Listeners, captured pointers, pending candidates and registrations end with their owning lifecycle;
a late answer cannot act on a replacement target or a disposed instance.

ADR-0045's interruption rules apply: Selection retains its last valid state; unconfirmed Fill,
resize, reorder and Pivot drag previews are discarded. Ordinary Fill release retains its preview
for Apply. Source/order invalidation keeps its existing precedence, and ordinary editing text
survives rotation. Opening, closing and applying remain C# decisions in the owner's order.

## Rejected alternatives

- **Recreate the Field List's layout in C#.** Variable browser-laid-out entries would require
  a second layout model. Restrict the lookup to the instance's registered drag targets instead.
- **Find the target again when held input is replayed.** The layout may have changed. Keep the
  target and rendering identity captured during the gesture and let C# refuse stale input.
- **Extend the lookup to grid cells or ordinary rendering.** Grid cells already have C#
  geometry. This exception is only for active ExPivot touch drag targets.
- **Treat the hidden report as a removal target.** A release outside the dedicated touch
  surface cancels. Remove or an explicit target inside that surface performs removal.

## Verification

[Definition of Done, section 33](../definition-of-done.md#33-responsive-layout-and-touch-rt)
provides the acceptance criteria, particularly RT-10, RT-12, RT-16 and RT-17, with RT-18's real-device matrix.
Use both hosting models and both Chromes, including immediate inputs under Server latency,
interleaved instances, stale targets, cancellation and disposal. Real Android, iPhone and iPad
verification is still required for native pan, keyboard, IME, clipboard, pinch and rotation.
Tests are obligations here, not passing results. Existing virtualisation, row render skipping
and no-per-cell-interop invariants remain. RT-19 records cost rather than inventing a speed claim.

## Browser contract and related decisions

Native pointer capture and HTML drag targets are different input paths; this decision preserves
ExPivot's existing placement semantics across them. See [ADR-0061](./0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md).
