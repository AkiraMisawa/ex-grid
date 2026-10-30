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

### Under the Input Order (ticket 07, 2026-10-25)

The supplied `change` is **kept**. ADR-0240 §5 has the core adopt the text a field holds with the next
input, but the field's own handlers commit on the browser's `change`, and a value written by script —
held typing typed into the field at its turn — still gets none. The order makes the core hear the
text; this note is still what makes the design system's field commit it.

### Over the Script Core (2026-10-30)

The opening key hold this decision mended was `ex-grid.js`'s, and went with it when pull request #501 removed the
Razor components. The defect did not go. The Script Core holds input while a commit's answer is out
([ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md) section 3) and
types the held text at its turn into whatever field then holds the keyboard, from script, with an `input` event and
no `change`. In a field whose owner commits on `change`, such as one a Wrapper draws in a slot with Blazor's
`@bind`, the text would show uncommitted. Checked in Chromium on 2026-10-30: a field given text by `setRangeText` and
an `input` event, then left, hears `blur` and no `change`.

So the decision is carried over (`HeldTextCommit`, `src/npm/core/src/dom/held-text-commit.ts`): one record per grid,
made when a held input changes a field that is not the grid's own (the Keyboard Field and the built-in Cell Editor,
whose text the core hears as it is typed), the field the keyboard holds or, for text put in with no key, the field it
was put in; `blur`, `change` and a capture-phase `keydown` heard on that field alone while it stands; a held Enter
commits after the field's handlers have heard the key and before its form is submitted; an Enter typed once the hold
is over commits first; the record is retired by a `change` of the field's own, by held typing into another field, and
when the grid goes. Two points are made explicit that `ex-grid.js` left implicit. **Enter commits a single-line field
only**: a textarea takes Enter as a line, and the browser commits nothing there. **The supplement carries only what
the grid wrote**: where the field no longer holds it, typing since is the browser's to commit, and a value the field's
owner wrote since is the owner's, so no `change` is sent for either. ADR-0021, which listed this as its tenth entry, is
superseded, and the rules this note keeps are ADR-0350 section 6's. Layer 2 holds it (`grid-pending-commit.test.ts`,
"held text in a field that commits on change").

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
