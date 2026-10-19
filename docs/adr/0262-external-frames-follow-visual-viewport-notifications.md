# External frames follow visual viewport notifications

Decided with the user, 2026-10-19, in the responsive/touch session, Q26; formalized when the user
asked to complete implementation prerequisites. [ADR-0045](./0045-exgrid-is-a-desktop-grid.md)
records the product behavior and its reasons. The number comes from this branch's reserved
0260–0269 block in [the numbering record](../agents/numbering.md).

This is an accepted addition to [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md),
not a claim of implemented or verified mobile support. The earlier unnumbered draft is replaced
by ADR-0260 through ADR-0263; the agreed behavior has not changed.

## Decision and reason

Report the visual viewport's offsets, dimensions and scale when an external surface opens and
on its resize/scroll notifications. Unsubscribe when the last surface closes. The shared host
may share this subscription, but each surface keeps its product's state and ownership.

Place the frame against that visible rectangle so input, commit and cancel remain reachable.
The host must be outside clipping ancestors and ancestors that give viewport-positioned children
a different containing block. Ordinary adaptive Chrome still follows its container's size.
The notification is not a guessed keyboard height, cell or anchor measurement, or render-time poll.

**Why browser-side:** Blazor does not expose these browser notifications. On Server, correct
placement follows handling the notification; this does not promise zero transient overlap during
each keyboard-animation frame.

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

- **Position from the layout viewport alone or a guessed keyboard height.** Keyboard, zoom
  and viewport motion can change the visible rectangle independently of those assumptions.
- **Poll on render or measure cells and anchors.** These frames need the browser's visible
  rectangle, not another grid geometry model.
- **Keep the subscription permanently active.** Its lifetime is the open external surfaces;
  close or disposal of the last one removes it.
- **Promise no transient overlap during every keyboard animation frame on Server.** The
  notification crosses the circuit. Correct placement is required after it has been handled.

## Verification

[Definition of Done, section 33](../definition-of-done.md#33-responsive-layout-and-touch-rt)
provides the acceptance criteria, particularly RT-7, RT-13 through RT-15 and RT-17, with RT-18's real-device matrix.
Use both hosting models and both Chromes, including immediate inputs under Server latency,
interleaved instances, stale targets, cancellation and disposal. Real Android, iPhone and iPad
verification is still required for native pan, keyboard, IME, clipboard, pinch and rotation.
Tests are obligations here, not passing results. Existing virtualisation, row render skipping
and no-per-cell-interop invariants remain. RT-19 records cost rather than inventing a speed claim.

## Browser contract and related decisions

[CSSOM View: VisualViewport](https://drafts.csswg.org/cssom-view/#visualviewport) defines the values and events.
