# ED-30's composition race: the ADR, Definition of Done and CLAUDE.md edits

Branch `claude/ed30-composition-race`, 2026-10-13. The user decided on 2026-10-12: the editor's waiting request
for the keyboard is granted once the IME's key has been told whole, at a keyup heard on the root, or at once when a
composition ends with no such key, and not if that key started the next composition. The timer of the task after a
composition's end is gone. ADR-0021's seventh entry, the Keyboard Field's composition and focus on the root, gains
`keyup` on the root.

A background agent does not edit `docs/adr/`, `CONTEXT.md` or `docs/definition-of-done.md`, so this file holds each
such edit as an exact old text and its new text: whole lines, with the target's indentation, checked by script
against the files at this branch's tip. Every old text occurs once, and the list applies in order without one edit
spoiling another's match. Every edit is a new dated note or an addition; no dated note is rewritten in place.
Nothing here edits `CONTEXT.md`: no term is new. `CLAUDE.md` is not an ADR, and its edit is proposed under its own
heading for the orchestrator to take or leave.

What the branch itself changes is in its two commits: `ex-grid.js` and its minified build, the layer-2 and layer-3
tests, `tests/ExGrid.Browser/README.md` and the Change Note `docs/releases/unreleased/ime-text-held-for-the-edit.md`.
The Docs Site describes no part of this (the IME on a selected cell has no page of its own), so it has no edit.

## The `focusout` clause is the rule's implementation, not a new decision

The keyup rule alone left a key unended when its keyup never reaches the page. The sixteenth Windows run's k15,
Alt+Tab mid-composition, recorded that in 7 of 12 configurations: the IME's keydown (`Process`, AltLeft, 229) on the
field, `compositionend` at the window switch, and no keyup. The request then waited for whatever key came next, and
the field stayed drawn. The root's `focusout`, already the seventh entry's, now ends the key as the keyboard leaves
the field or the root, and decides a waiting request against the element the keyboard is going to (DOM focus is on
nothing while the browser tells a move), so a control outside the grid that the user moved to keeps the keyboard.
Edits 1, 3 and 4 carry it inside the dated notes of the decision, and edit 1 says that it is the implementation of
that decision, not one of its own.

One limit is stated in edit 1 rather than hidden: a browser driven through DevTools emulates focus (Playwright does
so by default) and tells a page nothing of a window switch, neither `focusout` nor `blur`. A Windows run driven that
way, as the sixteenth was, will see k15's request wait until the next keyup heard on the root, such as Alt released
on the way back. Layer 3 models k15 by leaving the field from script.

## `docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md`

### 1. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md

After the refinement of 2026-10-02 (ticket 120) in "DOM focus never moves while a composition lasts": the keyup rule, the `focusout` clause, the held text, and the corrected evidence.

Old:

````text
    have returned. On WebAssembly the edit opened, rendered and asked for the keyboard inside that run.)*
````

New:

````text
    have returned. On WebAssembly the edit opened, rendered and asked for the keyboard inside that run.)*
    *(Refined again 2026-10-12, decided with the user: granted once the IME's key has been told whole,
    at a keyup heard on the root, or at once when a composition ends with no such key being told (a
    candidate chosen with the pointer, a script's commit), and not if that key started the next
    composition there; then it waits for that one's end. The task after the end was a time, not an
    order, and the timer is gone. A script driving the IME tells an end and the next start as two
    DevTools commands. In 60 instrumented runs of ED-30's third composition under the MudBlazor Chrome
    on WebAssembly, the start reached the page 0.1 to 2.9 ms after the end in the 54 that passed, where
    the timer fired 0.9 to 10.8 ms after it, and 5.8 to 85 ms after it in the 6 that failed, where the
    timer had fired at 0.3 to 0.9 ms, granted the request between the two, and the start composed in
    the Cell Editor. Every event the IME owes a key reaches the page before that key's keyup, so the
    keyup is the moment the next start, if there is one, has been told. The keyboard leaving the field
    or the root ends the key as well, at the root's `focusout`: whatever the key still owes is told
    wherever the keyboard went. Alt+Tab mid-composition is such a key. In the sixteenth run's k15 the
    IME took Alt's keydown, the window switch ended the composition, and in 7 of 12 configurations no
    keyup reached the page, since it went to the other window; at the keyup alone, the request would
    have waited for whatever key came next. A request waiting on that key is decided where the keyboard
    is going, by ADR-0021's rule for the editor's own focus: a control outside the grid that the user
    moved to keeps the keyboard. This `focusout` clause is how the rule just decided is implemented,
    written with it, and not a decision of its own. A browser driven through DevTools, as Playwright
    drives one, emulates focus and tells a page nothing of a window switch, so a Windows run driven
    that way sees the request wait for the next keyup heard on the root; layer 3 leaves the field from
    script for k15. And the text the hold has for the edit goes into it as the editor takes the
    keyboard, in the same task. Typed at the next animation frame, it came after a composition the IME
    started in the editor at once: D10 read `かなｄ感じです` in 4 of the 6 failing runs above. The keyup is
    heard by `keyup` on the root, which ADR-0021's seventh entry gains.)*
````

### 2. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md

Consequences, the line on ADR-0021: a dated note.

Old:

````text
  up by a press during a composition), and the third is refined (the editor's request waits while the
  field composes).
````

New:

````text
  up by a press during a composition), and the third is refined (the editor's request waits while the
  field composes). *(2026-10-12, decided with the user: the seventh entry gains `keyup` on the root,
  and the editor's request also waits while a key the IME took is still being told, until a keyup, or
  until the keyboard leaves the root.)*
````

## `docs/adr/0021-javascript-is-allowlisted-not-minimised.md`

### 3. docs/adr/0021-javascript-is-allowlisted-not-minimised.md

The seventh entry's row: `keyup` added.

Old:

````text
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. |
````

New:

````text
| **The Keyboard Field's composition and focus, on the grid root** | Added 2026-10-02 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)). An IME starts only in an element that is editable, and no Blazor API gives the root, a `div`, an input context. So on a grid that edits a text field of the grid's own holds the keyboard. Its composition's end has to take its place among the held keys and presses, which exist only in the browser (the first entry's hold), so `compositionstart` and `compositionend` on the root are heard there, always on. The root's own `focus` is passed on to the field at once: Blazor's `@onfocus` and `FocusAsync` land a round trip later on a circuit, and a key typed in between was read as a descendant's (ADR-0033's scroller, 2026-09-26). The root's `focusout` empties the field as it is left, and ends the release of Tab when DOM focus leaves the grid (ADR-0012). No layout is read. *(`keyup` added 2026-10-12 ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)), decided with the user: the root's `keyup` says that a key the IME took in the field has been told whole. The editor's request for the keyboard waits for it, so that it is granted in order with the IME's events, where the task after a composition's end missed a start the browser told a moment later. A key the IME took is told from its keydown, which the first entry already leaves to the IME, to the next keyup heard on the root, or until the keyboard leaves the root, which the root's `focusout` tells: Alt+Tab mid-composition sends its keyup to the other window. Nothing is measured, and no timer is set.)* |
````

### 4. docs/adr/0021-javascript-is-allowlisted-not-minimised.md

"What deliberately stays out of JavaScript", the decisions about focus: a dated note on the third, the editor's request, after the note of 2026-10-02.

Old:

````text
  (ticket 120). The hand-back puts the keyboard in the field.)* *(Six since 2026-10-02 too: the
````

New:

````text
  (ticket 120). The hand-back puts the keyboard in the field.)* *(Refined again 2026-10-12,
  [ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md),
  decided with the user: the editor's request waits while the field composes, and while a key the IME
  took is still being told, until a keyup, or until the keyboard leaves the root. The task after a
  composition's end is gone. A request granted as the keyboard leaves is judged where the keyboard is
  going, as the third decision judges any request where DOM focus is.)* *(Six since 2026-10-02 too: the
````

## `docs/definition-of-done.md`

### 5. docs/definition-of-done.md

ED-30's row, whole: the method gains `Input.dispatchKeyEvent` for the key that ends one composition and starts the next, and the k15 case.

Old:

````text
| **ED-30** | MUST | On a selected cell of a grid that edits, an IME composes from the first key (ADR-0080, 2026-10-02): the composition is drawn over the Focus cell, and nothing commits, moves or points while it lasts; its end opens the Cell Editor in Overwrite holding its text, with the keyboard; a cancelled composition opens an empty edit; a press during a composition puts its text in the cell it was composed on, then commits there and selects what it pressed; keys typed after a composition's end follow it in order; over a cell that does not edit, nothing composes and nothing opens | Layer 2 for the core; Layer 3 composing through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) on `/sheet`, under both Chromes, on both hosts and behind 150 ms; a real IME on Windows by hand (the sixteenth run) | D10 shows the composition while no edit is open; after its end the Cell Editor holds the text and has DOM focus; Enter commits it and moves to D11; after a press on D12 mid-composition, D10 holds the text and D12 is selected |
````

New:

````text
| **ED-30** | MUST | On a selected cell of a grid that edits, an IME composes from the first key (ADR-0080, 2026-10-02): the composition is drawn over the Focus cell, and nothing commits, moves or points while it lasts; its end opens the Cell Editor in Overwrite holding its text, with the keyboard; a cancelled composition opens an empty edit; a press during a composition puts its text in the cell it was composed on, then commits there and selects what it pressed; keys typed after a composition's end follow it in order; over a cell that does not edit, nothing composes and nothing opens | Layer 2 for the core; Layer 3 composing through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) on `/sheet`, under both Chromes, on both hosts and behind 150 ms, with the key the IME takes told through `Input.dispatchKeyEvent` (its keydown and its keyup, `Process` with keyCode 229, as the sixteenth run recorded them) where one key ends a composition and starts the next (that run's k6, and a third clause while the editor's request waits), and that run's k15, Alt+Tab mid-composition: a keydown whose keyup never comes, the end, then the keyboard leaving the field, after which the Cell Editor has the keyboard holding the text; a real IME on Windows by hand (the sixteenth run) | D10 shows the composition while no edit is open; after its end the Cell Editor holds the text and has DOM focus; Enter commits it and moves to D11; after a press on D12 mid-composition, D10 holds the text and D12 is selected |
````

## `CLAUDE.md` (proposed for the orchestrator; not an ADR)

### 6. CLAUDE.md

The allowlist paragraph's seventh entry.

Old:

````text
element the browser lays out at the current scale and zoom (ADR-0053). The seventh is the Keyboard
Field's composition and focus on the root, so an IME can start on a selected cell (ADR-0080). The
````

New:

````text
element the browser lays out at the current scale and zoom (ADR-0053). The seventh is the Keyboard
Field's composition, focus and `keyup` on the root, so an IME can start on a selected cell and the
editor takes the keyboard only once the IME's key has been told whole (ADR-0080). The
````
