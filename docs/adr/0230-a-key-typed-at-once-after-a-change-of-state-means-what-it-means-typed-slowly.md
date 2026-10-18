# A key typed at once after a change of state means what it means typed slowly

Decided with the user, 2026-10-17, for tickets 220, 221 and 222. Proposed the same day with the
options each section still lists; the user took the recommendation of every section, and the
question of section 1 as recommended. Built the same day, the three tickets on one branch ("As built").

## Why

The spec lint (#137, ADR-0220 §6) left eighteen waits between two inputs, each allowed as a suspected
race. Tickets 223 and 224 ask whether a wait hides anything; these three know it does, and the
outcome depends on how fast the second input follows the first. The spine's sixth principle and
ADR-0220 §2 refuse that. Each needs a decision before it is fixed, because each fix changes what the
key gate knows, or when.

All three come from the same cause. **The gate judges a key from what it was last told, and some
of what it is told arrives a message after the render that shows it.** On a circuit those are two
messages. On WebAssembly they are two steps of one task, and a key from the next task falls between
them. A key typed in that gap is judged against the state before it. #137 has already closed this
gap for one state: the opening of a completion list. The core marks the list on its box
(`data-ex-list`) in the render itself, and the gate reads the mark (`listShown()`). That is the
model for what follows.

## 1. Escape, then Tab at once (ticket 220)

**Today.** With nothing to dismiss, Escape on the root releases Tab (ADR-0012, rewritten
2026-10-01). Whether there is nothing to dismiss is the core's answer. An Interactive cell, a Copied
Range and a popover each take an Escape before the way out. So the gate treats Escape as a mode change
(`'mode'`) and holds the keys after it until the answer arrives. If the answer is the release, a held
Tab is dropped (`verdict === 'out'`). Script cannot make the browser's move of DOM focus (ADR-0021).
Typed slowly, the Tab leaves the grid. Typed at once, nothing happens. Seen on WebAssembly in
`key-field.spec.mjs` A11Y-4 under both Chromes.

**Options.**

- **(a) The render says whether an Escape now leaves, and the gate takes the release at the
  keydown.** The Settled Stamp (ADR-0220, "As built") already renders with every answered input. It
  gains one attribute that says an Escape on the root would leave: no Interactive cell, no Copied
  Range, no popover or menu. The gate reads that attribute when Escape is pressed. It can trust it
  when every input sent before the Escape is answered (`data-ex-answered`), or when the inputs still
  unanswered cannot add a layer an Escape would peel. Arrows, Tab and Enter cycling, and Home or End
  cannot. A copy, Space, a press, a popover opener or a declared key can. In either case the Escape
  is forwarded without being held (`'core'`), the release stands at once, and a Tab after it is the
  browser's. Otherwise the gate holds the Escape as today, and a Tab held behind a release is
  refused as in (b).
- **(b) A held Tab that meets a release is refused visibly.** The live region says that Tab was
  pressed before the grid let it go, and that the next Tab leaves. The outcome still depends on
  speed, but it is no longer silent (the spine's first principle).
- **(c) Script moves DOM focus to the next or previous focusable element.** Rejected. ADR-0021
  keeps focus out of JavaScript. The browser's sequential navigation order (tabindex, shadow roots,
  frames, `inert`) is not exposed, and a script that guesses it would be quietly wrong on some
  page.
- **(d) Escape on the root always releases Tab, whatever it dismisses.** This changes ADR-0012's
  layering, where each press is one dismissal. An Escape that clears a Copied Range would then also
  let the next Tab leave the grid. Rejected unless the user wants that layering changed.

**Decision: (a), with (b) for what (a) leaves.** (a) makes an Escape deterministic when it is
typed after keys that cannot add a layer. Of the twelve waits:

- eight follow a Tab or Shift+Tab, and (a) covers them;
- one follows an Escape that cancelled an edit; at its turn that Escape is answered, so (a) covers
  it once the stamp carries the answer;
- three follow a press: `key-field.spec.mjs` ~578 and ~841, and `pivot.spec.mjs` ~549. A press can
  engage an Interactive cell, so (a) holds the Escape until the press is answered.

What (a) leaves is an Escape typed at once after an input that could have added a layer but did
not, followed by a Tab. For that case (b) says so instead of dropping the key silently.

**The three tests that follow a press read the press's outcome before they type Escape** (asked
of the user with the proposal, and decided so). Each already expects the Focus after the press, so
the reading is the Settled Mark (`settled(grid)`), not a new kind of step. It reads the press's
outcome, which ADR-0220 §2 allows, though it sits between two inputs; the Escape is then judged
after an answered input, and (a) covers it. (b)'s refusal is asserted by a test of its own, which
types Escape and Tab at once behind an input that could have added a layer.

The attribute rides on a render that happens anyway. No row learns it (RR-14), and nothing is
measured. No JavaScript use is added: the gate reads it as it reads `data-ex-paint`, within
ADR-0021's first entry.

## 2. A change of the editor's mode told after its render (ticket 221)

**Today.** The gate's `editing` mode (`'overwrite'`, `'caret'`, `'point'`, `'completion'`, …) is
set by `setEditing`. The core calls it from `OnAfterRenderAsync`, a message after the render that
shows the new mode. A list's opening is read from `data-ex-list` in the render itself (#137). Its
closing, Point ending under typing, and the mode typing changes are not. A Home, End, ↓ or F2 typed
in that gap is judged in the old mode. `declarations.spec.mjs` has five waits for it (SH-36, ticket
75, ED-29, ED-29 7k).

**Options.**

- **(a) Every mode is marked in the render that shows it, and the gate reads the mark.** The edit's
  surfaces (the Cell Editor and the Formula Bar, which render with every change of text anyway)
  carry `data-ex-mode`, with the same values `setEditing` sends. A missing list mark already means no
  list. The gate's `claimedWhile[editing]` reads the surface's mark, and falls back to what it was
  told only where no surface stands (the edit's first render). `setEditing` still carries what is
  not a mode: whether the caret is reported, and whether F4 cycles.
- **(b) Hold every key typed after a key that may change the mode until the tell arrives.** Every
  character typed into a Formula can end Point or close a list, so on a circuit typing would wait
  one round trip per character. Rejected as the cost ADR-0220 (1) refuses.
- **(c) The gate derives the mode itself from the text and caret.** This duplicates the core's
  rules in JavaScript, so a disagreement makes a key do something else. Rejected for the same
  reason ADR-0010 keeps the meaning of a key in C#.

**Decision: (a).** It extends what #137 did for the list's opening to every change of mode,
and adds nothing per key. The five waits go.

## 3. A press into the Name Box straight after a press on the rows (ticket 222)

**Today.** A press on the rows is held among the keys until it is answered (ADR-0021's note of
2026-10-01). A press into the Name Box is not. It takes the keyboard at once, and its release selects
the box's text (ticket 78). If the row press's render lands after the user has started typing, it
writes the new address over what was typed. `nameBoxSelected` re-selects the text only when the
render comes before the first key. `declarations.spec.mjs` waits 600 ms before typing.

**Options.**

- **(a) The Name Box's press takes its place among the held keys, as the Formula Bar's did in
  ticket 202.** Behind a row press still unanswered, the press into the Name Box keeps its default
  (DOM focus), and the keys typed after it are held behind the row press. At its turn, the
  row press's render is already in the box, so the gate selects the box's whole text (ticket 78's
  rule) and then hands the held keys to it. The outcome is the one a slow press gives: the address
  replaced by what was typed.
- **(b) The core never renames the Name Box while it holds the keyboard.** On a circuit the core
  does not know yet that the box holds the keyboard when it renders, so this is the race moved, not
  removed.
- **(c) Script puts back the text a render wrote over.** Script and Blazor would then fight over
  the field's value. That is the defect CLAUDE.md's `@bind` trap describes. Rejected.

**Decision: (a).** It is ticket 202's decision applied to the second field beside the rows,
and it uses the hold that already exists. No listener is added.

## As built (2026-10-17)

- **(1)** The stamp's attribute is `data-ex-escape-leaves`: no popover or menu, no Interactive cell, no
  Copied Range and no edit. The gate trusts it once the stamp's `data-ex-answered` reaches the last input
  that could add a layer. The keys that cannot are the ones on the root that move the Focus or peel a
  layer, unless a Consumer declared one: the arrows and their Shift and Control forms, Tab and
  Shift+Tab, Home and End and their Control forms, the page keys, and Escape. Enter stays among the
  keys that can, because an Interactive cell may answer it. A press the core hears without a number of
  its own (a ▾, a heading) counts as the next input's. A release the gate ends (spent by its Tab, or
  ended by another key) counts as an end heard, so the `releaseTab` still on its way for an Escape
  released at its keydown grants nothing after it. (b)'s words have the id
  `KeyRefusalLabelIds.TabBeforeRelease`, resolved as a refusal's are (ADR-0203); its test runs on the
  Server host only, where a 150 ms round trip keeps the input before the Escape unanswered.
- **(2)** The mark is `data-ex-mode` with `data-ex-mode-version`, a count of the mode's changes that the
  tell carries too, so a render the core suppressed can never leave an older mark to outrank a newer
  tell. Removing the waits found a second gap, outside the gate: on a circuit a key the script sends can
  reach the core before Blazor's input events of the typing the key carries. `=SUM(` and Home typed at
  once pointed, and the input event of the `(`, heard after, ended pointing as typing and put `=SUM(`
  back. A key that carries text the core had not heard now marks the input events up to that text as
  overtaken, and the core drops them: every change of an edit surface's text fires one, the script's
  own typing included, and they arrive in order, so the one that brings the key's text arrives.
  *(Note of 2026-10-17, from the red on #147's run.)* A third gap: the mark is the mode of the text
  its surface showed in that render, and a field typed into faster than the core answers holds text
  the core has not heard. On a circuit `=SUM(` typed at once painted the list of names the core opened
  for `=S` while the field already held `=SUM(`; Home, read as that list's, was left to the field and
  moved the caret, where typed slowly it points at A10. Each surface now carries the text it showed
  (`data-ex-mode-text`), and a key whose meaning differs between the modes (the arrows, Home, End and
  their Shift and Primary Modifier forms) typed in a field whose surface marks another text is held,
  with every key after it, until the core's render shows the field's text, and is gated then; so is
  such a key already held behind another. A character never waits on it, so typing still goes at the
  browser's speed and option (b) stays rejected. Its test holds the list for `=S` on screen behind a
  150 ms round trip while `UM(` and Home are typed: before the change it failed every run.
- **(3)** At its turn, the Name Box's press waits for the stamp to answer every input sent before it,
  then selects the box's whole text.

## Consequences

- ADR-0012 gains a note on (1), ADR-0021 a note on (1), (2) and (3). None adds an allowlist entry.
- The Settled Stamp gains one attribute (1). RR-14 still holds.
- Tickets 220 to 222 are built with the waits removed, and each test types at machine speed and
  passes on both hosts, 20 runs each (ADR-0220 §2).
