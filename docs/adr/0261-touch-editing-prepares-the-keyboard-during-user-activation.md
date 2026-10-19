# Touch editing prepares the keyboard during user activation

Decided with the user, 2026-10-19, in the responsive/touch session, Q25; formalized when the user
asked to complete implementation prerequisites. [ADR-0045](./0045-exgrid-is-a-desktop-grid.md)
records the product behavior and its reasons. The number comes from this branch's reserved
0260–0269 block in [the numbering record](../agents/numbering.md).

This is an accepted addition to [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md),
not a claim of implemented or verified mobile support. The earlier unnumbered draft is replaced
by ADR-0260 through ADR-0263; the agreed behavior has not changed.

## Decision and reason

C# registers an actual input surface in advance. During the browser's activating edit gesture,
JavaScript focuses it and preserves immediately typed text for the candidate target. The final
edit decision enters the owner's Input Order. Acceptance gives that text to that edit; refusal
writes nothing, closes the candidate and says why. The prepared input and dialog use the same
draft. A Wrapper does not create another edit or queue.

Pending text must survive later renders, and a delayed answer must not steal focus back from
another instance. An ordinary selection tap does not prepare keyboard input. A missing required
host reports a configuration failure and leaves no invisible input holding the keyboard.

**Why browser-side:** waiting for Server acceptance before focusing can lose the browser's
user-activation opportunity to open the software keyboard. DOM focus alone does not prove that
the keyboard opened; the supported real devices must demonstrate this path with immediate typing
and Japanese IME.

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

- **Focus only after Server acceptance.** That can miss the software keyboard's activation
  opportunity. Preparation happens during the gesture, while acceptance stays in C#.
- **Accept an edit or write data in JS to avoid the round trip.** That bypasses permissions,
  source/order checks and the owner's Input Order. Preparation never grants permission.
- **Use a second pending edit owned by the Wrapper.** This splits draft and keyboard ownership;
  the prepared input and dialog belong to the same edit.
- **Assume focused DOM means usable mobile editing.** Software keyboard and IME behavior must
  be demonstrated on the supported real devices.

## Verification

[Definition of Done, section 34](../definition-of-done.md#34-responsive-layout-and-touch-rt)
provides the acceptance criteria, particularly RT-7 through RT-9, RT-13, RT-14 and RT-17, with RT-18's real-device matrix.
Use both hosting models and both Chromes, including immediate inputs under Server latency,
interleaved instances, stale targets, cancellation and disposal. Real Android, iPhone and iPad
verification is still required for native pan, keyboard, IME, clipboard, pinch and rotation.
Tests are obligations here, not passing results. Existing virtualisation, row render skipping
and no-per-cell-interop invariants remain. RT-19 records cost rather than inventing a speed claim.

## Browser contract and related decisions

[HTML user activation](https://html.spec.whatwg.org/multipage/interaction.html#tracking-user-activation)
and the [WebKit focus policy discussion](https://bugs.webkit.org/show_bug.cgi?id=195884)
explain the activation constraint. They do not replace real-device verification.
