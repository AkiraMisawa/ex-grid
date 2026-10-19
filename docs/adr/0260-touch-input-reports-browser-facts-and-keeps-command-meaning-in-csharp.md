# Touch input reports browser facts and keeps command meaning in C#

Decided with the user, 2026-10-19, in the responsive/touch session, Q24; formalized when the user
asked to complete implementation prerequisites. [ADR-0045](./0045-exgrid-is-a-desktop-grid.md)
records the product behavior and its reasons. The number comes from this branch's reserved
0260–0269 block in [the numbering record](../agents/numbering.md).

This is an accepted addition to [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md),
not a claim of implemented or verified mobile support. The earlier unnumbered draft is replaced
by ADR-0260 through ADR-0263; the agreed behavior has not changed.

## Decision and reason

The input bridge belongs to one product's Input Root and its registered external surfaces.
It reports pointer identity and original input type, starting target and Paint, movement,
browser event timing, and the distinction between end and cancellation. It captures and releases
a pointer for a dedicated handle. It recognizes tap and long-press candidates from the browser
event sequence; C# determines the resulting command.

The bridge prevents one recognized touch gesture from also executing through compatibility
mouse, click or context-menu events. Keyboard and assistive activation remain operable.
Ordinary content permits native panning, and pinch remains available, including when contact
starts over a handle. Direct-manipulation permissions must be set before contact; changing them
after a second finger arrives does not recover a gesture the browser already excluded.

A one-time fine/hover capability report supplies the initial handle presentation. Unknown
capability uses Selection Handle. Actual input then follows ADR-0045's revised Q9: touch chooses
Selection Handle, a mouse click restores the mouse presentation, and mouse motion alone does not.
The bridge does not enumerate hardware or continuously monitor device availability. Pen or
unknown input is not authorization for mouse Fill.

**Why browser-side:** capture and arbitration with browser defaults occur at contact, before a
Server round trip could answer. A server-started timer cannot establish the original gesture's
timing. There are no per-cell listeners or JavaScript grid-cell lookups. Optimizing work that
Blazor can do still needs a recorded measurement; this decision supplies no general per-move
performance exception.

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

- **Treat touch as a replayed mouse gesture.** That loses the origin and can turn a finger's
  first contact with a fill handle into a data write. Keep the original type and captured meaning.
- **Wait for the Server before capturing or arbitrating a gesture.** The browser has already
  chosen its default action. Browser event timing establishes candidates; C# decides commands.
- **Disable all native touch behavior.** Ordinary panning and pinch are part of the accepted
  behavior. Test pinch over handles too; do not recover it by changing permissions too late.
- **Move grid hit testing or per-move product logic to JS without evidence.** Existing C#
  geometry remains authoritative. A performance exception still needs a recorded measurement.

## Verification

[Definition of Done, section 33](../definition-of-done.md#33-responsive-layout-and-touch-rt)
provides the acceptance criteria, particularly RT-2 through RT-5, RT-12, RT-16 and RT-17, with RT-18's real-device matrix.
Use both hosting models and both Chromes, including immediate inputs under Server latency,
interleaved instances, stale targets, cancellation and disposal. Real Android, iPhone and iPad
verification is still required for native pan, keyboard, IME, clipboard, pinch and rotation.
Tests are obligations here, not passing results. Existing virtualisation, row render skipping
and no-per-cell-interop invariants remain. RT-19 records cost rather than inventing a speed claim.

## Browser contract and related decisions

[Pointer Events](https://www.w3.org/TR/pointerevents3/) defines capture, compatibility events and direct manipulation.
