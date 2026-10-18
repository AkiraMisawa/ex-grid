# Input paths into ExGrid, ExSheet and ExPivot (inventory, 2026-10-18)

Step 1 of `docs/specs/input-order/issues/01-one-ordered-input-path-per-grid.md`, taken from `main` at
d47a190. Line numbers are approximate pointers, not contracts.

## The one path that exists: the key gate

`src/ExGrid/Assets/ex-grid.js` (one script for every product; ExSheet and ExPivot draw through
`ExGrid<T>` and ship no script of their own).

- **Numbered** (`nextInput()`, echoed on the Settled Stamp as `data-ex-answered`, reordered in
  `ExGrid.Settled.cs` `InputAnswered`): keys (`OnKeyAsync`), presses and releases on the rows
  (`PressTakenAt` claimed by Blazor's `@onmousedown`/`@onmouseup` through `AsTaken`), the double
  click, bar / Name Box presses, Keyboard Field composition and text, paste, async copy, copy
  landed/rejected, held Tab refused.
- **Held**: `held[]` + `answering`; `drain` replays one entry at a time, re-gating each key against
  the mode now shown (`data-ex-mode`, `data-ex-mode-text`, ADR-0230).
- **Not serialized in C#**: each `[JSInvokable]` runs on the renderer's context and interleaves at
  every `await`. Order comes from the JS hold (only `mode`/`popover` verdicts wait), the commit
  barrier (`AfterCommitInFlightAsync`), `_pressAnswer` chaining, and the reorder buffer. `core`-verdict
  keys are fired without awaiting, so their handlers can overlap.

## Paths outside the order

| Path | Products | Today |
|---|---|---|
| Text typed into the Cell Editor, Formula Bar, Name Box (Blazor `@bind` on `oninput`) | all | not numbered; patched by `InputOvertaken` (`ExGrid.Pointing.cs:204`) and `markBehind` |
| Caret report (`OnEditorCaretAsync`) | all | not numbered |
| Heading mousedown/click, ▾ opener, context menu, grip double click (fit) | ExGrid | commit barrier only; counted as "the next input's" via `layerInputAt` |
| Completion list mousedown (built-in and Mud) | ExGrid | not ordered against held keys |
| Column menu, filter popover, find panel, value list, pager, status buttons | ExGrid + Wrapper | not ordered; several async before mutating |
| Action / row-mark / header-mark clicks | ExGrid | paint stamp (taken against), unnumbered |
| ExSheet toolbar band (KeyTips) | ExSheet | its own hold list and replay (`SheetToolbar.razor`) |
| Toolbar commands, Format Cells OK | ExSheet + Wrapper | `RefuseWhileEditing` + selection adopted; not numbered |
| Format Cells MudDialog opened from `OnAfterRender` | ExSheet.MudBlazor | its own `_keysReady` gate and key buffer |
| Field list, drag and drop, popups, toggles, settings dialogs | ExPivot (+ Wrapper) | its own hold lists (`_popupKeys`); `_pending` layout coalescing; identity-based toggles |
| Keyboard in/out of a Scope | ExSheet | `BrowserTurn.AnsweredAsync` + `Task.Yield` + counter |
| Press handed on to a pointing grid | ExSheet | queued, answered by the other grid; unnumbered |
| Pointer move/rest, scroll, viewport, DPR, clipboardchange | all | reports, not gestures; unnumbered |

## Timing-dependent fallbacks (the spine's sixth rule)

- `editorSettled` polls per frame for the editor's or popover's focus and **gives up after 2 s**,
  replaying against whatever is there; `handOffMissed` drops the queue.
- Focus moved from `OnAfterRender` without the guard: `MudToolbarItem` / `MudFormatCellsFrame`
  `_leavingFrom.FocusAsync`, `PivotFieldSettingsView` / `PivotValueFieldSettingsView` `_first.FocusAsync`,
  `PivotToggleButton`, `MudExGridFindPanel`'s fallback, PivotChrome's default `TakeKeyboard`.

## Local fixes the one path should absorb

ADR-0230 §1 (`data-ex-escape-leaves`), §2 (`data-ex-mode*` + `InputOvertaken`, which is what PR #148
was), §3 (Name Box press held); ADR-0204's addendum (ticket 240, `pressedPopovers`); tickets 260/261
(Wrapper dialog state); ADR-0210 (held text's synthetic `change`); ADR-0080 (Keyboard Field).

## Flaky record

No open ticket for a CI red or a hunt finding; `hunt.yml` (daily `--repeat-each 10`, and the widened
run at RTT 150 ms, CPU ×4) exists. Ticket 240's and 260's 20-run boxes are still unticked.
