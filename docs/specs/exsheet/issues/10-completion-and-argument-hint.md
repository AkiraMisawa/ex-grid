# 10: Completion and the argument hint

Status: ready-for-agent

**What to build:** The core reports the editor's text and caret as the user types (a Blazor input event). The
Consumer answers with candidates and a hint. Chrome paints the list as the editor's Inner Popup,
inside the grid's box, and the hint beneath it. ↑/↓ choose, Tab accepts, and Escape closes the list
before it cancels. ExSheet supplies the function list and, once ticket 16 lands, the Linked Tables'
names. Candidates that come back for text that has since changed are dropped.

**Blocked by:** 04, 09

- [ ] `=SU` offers every declared function starting with `SU` (today `SUM`), and `=X` offers `XLOOKUP`; Tab accepts (ADR-0051)
- [ ] Escape closes the list and leaves the edit open
- [ ] The list stays inside the grid's box (ADR-0040) under the built-in Chrome and `ExGrid.MudBlazor`'s
- [ ] A stale candidate list is never shown (layer 2 with a delayed answer)
- [ ] Works from the Formula Bar as from the cell

## Comments

2026-09-27, engine half: `FormulaEntry.Complete(text, caret, linkedTables)` (and
`Sheet.Complete(text, caret)`, which passes the Sheet's own Linked Tables) returns the
declared functions and Linked Table names beginning with the name being typed at the caret,
without regard to case, in one alphabetical list, with the span of text accepting a candidate
replaces and what it writes (`SUM(` for a function, the name for a table). It offers nothing
inside text in quotes, a Reference with `$`, a number, a column in brackets, a name that does
not stand where an operand can start, or when nothing matches. `FormulaEntry.HintAt(text,
caret)` returns the innermost declared function whose argument list holds the caret, the
argument index counted by the commas at its own depth, and that argument's name as
`DeclaredFunction.Arguments` lists it. Both work on unfinished text and never require the
Formula to parse (`FormulaEntryTests`). The first criterion's `=SU` offers `SUM` only: the
declared set has no `SUMIF` (ADR-0047). **What remains is the component's:** reporting the
text and caret as the user types, the Inner Popup and the hint painted by Chrome, ↑/↓/Tab/Escape,
dropping a stale list, and all of it from the Formula Bar too.

2026-09-27, ExGrid core half: `ExGrid.CompleteEditorText`, a
`Func<string, int, ValueTask<EditorCompletion?>>`, is handed the editor's text and caret on every
Blazor input event, from the Cell Editor and the Formula Bar alike (and when Overwrite opens on a
typed character). `EditorCompletion` carries `CompletionCandidate(Label, Start, Length, Insert)`s
and an optional `EditorHint(Text, EmphasisStart, EmphasisLength)`. A `ValueTask` so ExSheet's
synchronous `FormulaEntry.Complete` shows its list in the render the keystroke causes, while an
asynchronous Consumer can still answer. The answer is painted in a core-owned `ex-completion` box
beneath the editor surface, inside the grid's box, on the side with more room and bounded by it
(ADR-0040). Chrome paints the contents through `IGridChrome.EditorCompletion(EditorCompletionContext)`,
a default member. The built-in list and `MudGridChrome`'s both implement it. ↑/↓ choose, Tab
accepts (the candidate's span is replaced by its `Insert`), and Escape closes the list and leaves
the edit open. While a list is open in Caret the gate is told `overwrite`, so ↑/↓ reach the core.
A list goes the moment the text changes, and an answer for text that has since changed, or for an
edit that closed, is dropped. Covered in layer 1 (`EditorTextRulesTests`) and layer 2
(`CompletionTests`, `MudGridChromeTests`): DC-17 under both Chromes, DC-18 with a delayed answer,
and DC-1 (nothing declared, no box, no `aria-autocomplete`).

**What remains.** ExSheet wiring: pass `Sheet.Complete`/`HintAt` as `CompleteEditorText`, mapping
the engine's span and replacement onto `CompletionCandidate` and the argument onto `EditorHint`.
Layer 3: the box inside the grid's box under both Chromes (DC-17's "Layer 3 for the box"), and the
keys through the real capture listener. Two limits are for the orchestrator to decide. First,
the caret reported on an input event is inferred from the edit (`EditorTextRules.InferCaret`),
because a Blazor input event does not carry `selectionStart`. It is exact for one edit at one place
and ambiguous only between repeated letters. Second, the core cannot place the DOM caret: after an
accept the browser leaves it at the end of the text, which is right for the usual case (`=SU|` →
`=SUM(|`) and visibly wrong when text follows the caret. In Caret with a list open, ←/→/Home/End
close the list and do not move the caret, because the gate has no set that claims ↑/↓ alone.

2026-09-27, ExGrid core, second round (ADR-0051's second round, DC-31's C# side): both limits
above are gone. **The caret is reported, never inferred:** `EditorTextRules.InferCaret` is
removed. Where completion or pointing is declared, the grid's listener reports each input in an
editor surface (the Cell Editor, the Formula Bar, a Chrome's control inside `.ex-editor`) with
the field's `value` and `selectionStart`, through a new `[JSInvokable] OnEditorCaretAsync(text,
caret)`; `setEditing` now carries whether to report, so a grid declaring neither sends nothing
more (DC-1). The report and Blazor's input event are two messages and either may come first: a
report for text not heard yet is kept for its input event, and until the caret is known the
Consumer is not asked (a key that carries it asks at once). **The caret is set after the core
rewrites the text:** after an accepted candidate, and after a written Reference, the grid calls
`setCaret(text, caret)` on the handle after the render that carries the text, and the listener
places it only while the surface still holds that text. **While a list is open the gate is told
`completion`**, which claims only ↑/↓ beside the editing keys (Tab, Escape, Enter, F2), in
Overwrite and Caret alike, so ← and → move the caret. A key that carries a caret other than the
one the list answered re-asks the Consumer first and is decided against that answer, so Tab
after ← replaces the span at the new caret. A ←/→/Home/End that a gate not yet told still
claimed only closes the list. Layer 2: `CaretTests` (including `=SS` completed at the reported
caret, a report ahead of its input, the caret placed mid-text), `CompletionTests`,
`MudGridChromeTests`; the script shape in `ShippedStylesheetTests`. **What remains:** layer 3
with real keys (DC-31): `=SS` then completion replaces the span at the caret, the caret sits
after the inserted text, and ←/→ with the list open move the caret; plus the earlier
layer 3 items and the ExSheet wiring.
