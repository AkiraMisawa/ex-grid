# A refusal is worded by the built-in Chrome unless someone else words it

*(Decided with the user on 2026-10-11, while settling the Definition of Done's ERR-2 for `0.1.0`. It
amends [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)'s "the grid cannot
announce this one".)*

ERR-2 asks that "a refusal message says what to do next where one exists", verified by a message
asserted in layer 1. No message exists to assert:

- **The core refuses with an enum.** `OnPasteRefused` carries a `PasteRefusalReason` and
  `OnCopyRefused` a `CopyRefusalReason`. The advice ("reselect a target of the same shape",
  ADR-0014) lives only in doc comments.
- **ADR-0035 decided that the words are Chrome's, and that whatever renders a refusal must be a live
  region.** It also concluded that the built-in Chrome, holding no strings, "cannot announce this one".
- **That premise has weakened since.** ADR-0036 gave the built-in Chrome English defaults keyed by id
  (`BuiltInCommandLabels`, replaceable through `CommandLabel`), and `FilterPanelChoices.RefusalText`
  already says what to do next when a filter value reads two ways.
- **So a Consumer that binds neither event hears nothing.** A paste the grid refuses is neither shown
  nor announced. The Docs Site's Clipboard page does not say so. The examples, the DemoHost and ExSheet
  each word refusals themselves.

Silence is not wrong, but it is not "say it cannot be done" either, and `0.1.0` is the first version
a Consumer may put in front of users without reading every page first.

## The decision

- **The built-in Chrome words every refusal reason**, paste and copy, in English, keyed by the reason,
  as it words commands. Where a next step exists the words say it: a shape mismatch says to select a
  target of the copied shape, or a single cell; a target that is not editable says which column refused.
- **The words are announced in the root's live region**, politely, when the refusal happens
  ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)), as a Consumer's
  `GridCopyAnswer.Refuse` sentence already is.
- **A Consumer replaces them through the Chrome's label function**, the seam `CommandLabel` already
  is. Answering no words for a reason silences the built-in announcement for it, and the Consumer then
  owns saying it — as ExSheet does, with its own words, through the same seam so that a refusal is
  announced once.
- **The events are unchanged.** A Consumer that binds `OnPasteRefused` or `OnCopyRefused` still
  receives the reason; binding one does not silence the words.
- **The MudBlazor Wrapper** uses MudBlazor's localised text where MudBlazor has a key, and the Chrome's
  label function otherwise, as WR-3 has it for the filter panel.
- **The core still holds no sentence.** The words are the built-in Chrome's, behind the same seam as
  its command labels, so a substituted Chrome brings its own (principle 4).

## Considered options

- **Rewrite ERR-2 to match ADR-0035, and say in the Docs that an unbound refusal is silent.** No code,
  and true to ADR-0035's letter, but a default that says nothing is the quiet failure this family
  refuses.
- **The core returns an advice value beside the reason** (`ReselectSameShape`, `None`). It nearly
  duplicates the reasons, and nobody would hear it unless a Chrome turned it into words anyway.

## Consequences

- ADR-0035's bullet carries a dated note pointing here.
- The Definition of Done's ERR-2 is restated: every reason has built-in words, those for a reason with a
  next step say it, and they reach the live region; verified in layer 1 (the words per reason) and
  layer 2 (the announcement, once, with and without a Consumer's words).
- ExSheet's refusal words move behind the grid's label seam, so that a refusal on a Sheet is announced
  once.
- The Docs Site's Clipboard page says what a refusal sounds like by default, and how to replace it.
