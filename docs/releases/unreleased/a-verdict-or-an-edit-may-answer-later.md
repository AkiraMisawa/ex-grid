Product: ExGrid
Kind: Added
ADR: 0380

In `@ex-grid/core` and `@ex-grid/react`, a column's `validate` may answer a promise of an Edit Verdict, and `onEdit` and `onPaste` may return a promise, before which `refuse` still holds the editor or the paste; each is handed an `AbortSignal`, on the intent as `signal`. While the answer is out the commit is pending: the editor stands read-only and `aria-busy` with the text, the root carries `data-ex-pending-commit`, every input made meanwhile waits and is taken after the answer, and Escape cancels it aloud, raising `onRefused` with kind `commit` (or `paste`) and reason `cancelled`. An answer given at once behaves as before. It is published with 0.3.0.
