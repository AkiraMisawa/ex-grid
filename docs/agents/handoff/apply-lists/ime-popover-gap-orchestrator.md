# ime-popover-gap: apply list for the orchestrator

Neither edit is needed for the code to be right: the fix applies ADR-0039's note of 2026-10-01 and
ADR-0010 as written. Item 1 records the defect where ADR-0080 describes the composition's text among
the held keys, so a reader does not take "the core opens the Cell Editor" to hold while a popover
opens. Item 2 adds the case to ED-30's criterion and its method and pass columns: a requirement
added, so it is the user's call.

### 1. docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md

Old:
```
  - A second composition the IME finishes in the field before the first one's editor has the keyboard
    (a circuit, a fast typist) is typed into the edit at its caret, in order.
```

New:
```
  - A second composition the IME finishes in the field before the first one's editor has the keyboard
    (a circuit, a fast typist) is typed into the edit at its caret, in order.
  - A composition the IME finishes in the field while a popover or a frame of the Consumer's own takes
    the keyboard is that one's, as the keys typed with it are (ADR-0039, note of 2026-10-01): typed into
    its text field at the caret, as a held character is, or, on any other control, dropped with every
    key held behind it, as a key script cannot reproduce (ADR-0010). *(2026-10-13, no new decision: found
    while building #108. The hold asked the core to open the Cell Editor holding the text whatever held
    the keyboard by then, and the editor opened behind Find or Format Cells and took the keyboard from
    it.)*
```

### 2. docs/definition-of-done.md

Old:
```
| **ED-30** | MUST | On a selected cell of a grid that edits, an IME composes from the first key (ADR-0080, 2026-10-02): the composition is drawn over the Focus cell, and nothing commits, moves or points while it lasts; its end opens the Cell Editor in Overwrite holding its text, with the keyboard; a cancelled composition opens an empty edit; a press during a composition puts its text in the cell it was composed on, then commits there and selects what it pressed; keys typed after a composition's end follow it in order; over a cell that does not edit, nothing composes and nothing opens | Layer 2 for the core; Layer 3 composing through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) on `/sheet`, under both Chromes, on both hosts and behind 150 ms, with the key the IME takes told through `Input.dispatchKeyEvent` (its keydown and its keyup, `Process` with keyCode 229, as the sixteenth run recorded them) where one key ends a composition and starts the next (that run's k6, and a third clause while the editor's request waits), and that run's k15, Alt+Tab mid-composition: a keydown whose keyup never comes, the end, then the keyboard leaving the field, after which the Cell Editor has the keyboard holding the text; a real IME on Windows by hand (the sixteenth run) | D10 shows the composition while no edit is open; after its end the Cell Editor holds the text and has DOM focus; Enter commits it and moves to D11; after a press on D12 mid-composition, D10 holds the text and D12 is selected |
```

New:
```
| **ED-30** | MUST | On a selected cell of a grid that edits, an IME composes from the first key (ADR-0080, 2026-10-02): the composition is drawn over the Focus cell, and nothing commits, moves or points while it lasts; its end opens the Cell Editor in Overwrite holding its text, with the keyboard; a cancelled composition opens an empty edit; a press during a composition puts its text in the cell it was composed on, then commits there and selects what it pressed; keys typed after a composition's end follow it in order; a composition finished while a popover opens is the popover's, as the keys typed with it are (ADR-0039, ADR-0010): typed into its text field, or dropped with the keys behind it on any other control, and never an edit behind it; over a cell that does not edit, nothing composes and nothing opens | Layer 2 for the core; Layer 3 composing through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) on `/sheet`, under both Chromes, on both hosts and behind 150 ms, with the key the IME takes told through `Input.dispatchKeyEvent` (its keydown and its keyup, `Process` with keyCode 229, as the sixteenth run recorded them) where one key ends a composition and starts the next (that run's k6, and a third clause while the editor's request waits), and that run's k15, Alt+Tab mid-composition: a keydown whose keyup never comes, the end, then the keyboard leaving the field, after which the Cell Editor has the keyboard holding the text; and on the Server host behind 150 ms, under both Chromes, a composition finished after Ctrl+F, after Ctrl+1, and once Format Cells… run from the Context Menu has closed it, each read to have ended in the Keyboard Field (`key-field.spec.mjs`); a real IME on Windows by hand (the sixteenth run) | D10 shows the composition while no edit is open; after its end the Cell Editor holds the text and has DOM focus; Enter commits it and moves to D11; after a press on D12 mid-composition, D10 holds the text and D12 is selected; the composition finished after Ctrl+F is in the find field with the digit typed after it, the one finished as Format Cells opens is dropped with the End typed after it, its Number tab still chosen, and no Cell Editor opens behind either |
```
