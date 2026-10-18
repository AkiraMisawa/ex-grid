# Held text commits when its field is left

Decided with the user, 2026-10-14.

## The defect

The opening key hold (ADR-0010, ADR-0039) writes held text into the field that receives the
keyboard. The browser does not treat that script-written text as its own edit. Sending `input`
updates an input-bound Consumer, but a change-bound field can show the new text while its
Consumer still holds the old value. More Colours exposed this: HEX showed red, yet leaving the
field without Enter left the chosen colour unchanged.

## Decision

The core may temporarily hear `blur` on an editable text field **only after held typing has
actually changed its value**. On leaving that field it supplies the missing bubbling `change`
notification, if the value differs from the value before the held edit. A held Enter commits
that same edit before the existing form submission. An unchanged value or caret movement
alone emits no change.

A temporary `change` listener on that same field recognises a normal commit first and removes
the supplement, so subsequent blur cannot commit it twice. Both listeners are removed before
a supplied change, on a normal change, or when the grid is disposed. There is at most one
pending field per grid instance: starting a held edit in another field retires the old record
without committing a detached or abandoned field. Removing an element need not emit blur;
such a record is retired by the next held edit or disposal and cannot accumulate detached fields.
Nothing is installed on the document, no layout is read, and no timer, retry, or observer is added.

This is the tenth entry of ADR-0021's JavaScript allowlist. Blazor cannot mark a script-written
value as a native edit, nor does it own the design system's private text field. The core already
knows which field the hold wrote and supplies only the notification that write otherwise lacks.
It does not choose or parse the colour, validate the text, or replace the field's existing handlers.

### Widened 2026-10-17, decided with the user: an Enter typed after the hold

The decision above committed a held Enter, and missed an Enter typed once the hold was over.
That Enter reaches the field natively, and the browser commits only text it has typed itself
since script last wrote the field: none. HEX showed `#FF0000` and the fill stayed unchanged
(PR #147's CI, WebAssembly; reproduced each time on the Server host with a round trip, where
the text is held while HEX opens and the Enter comes after it is shown). The same record now
also hears `keydown` on that same field, and an Enter there that the core is not itself
handing on supplies the missing `change` before the field's handlers hear the key. It is
removed with the other two, and is the same entry of ADR-0021's allowlist, not a new one.
Where typing after the held text has changed the value, the browser commits it itself, so the
supplement stays out and no change is sent twice.

## Alternatives

Keeping MudBlazor's existing inputs was chosen over replacing its colour entry and modes in
C#. The latter would duplicate the design system's controls to solve a missing browser event.
Requiring Enter would leave the ordinary act of leaving a field silently wrong. A permanent
blur listener on every input, a document listener, and a timed commit are outside this decision.

## Verification

Layer 3 exercises More Colours with immediate opening text, both with Enter and by leaving HEX,
on Server and WebAssembly. Controlled tests exercise the shipped module's notification and
listener lifetime: changed and unchanged values, normal change followed by blur, held Enter
followed by blur, disposal, and replacement of an abandoned field. ADR-0021's listener-count
test names the additional blur and change listeners explicitly.
