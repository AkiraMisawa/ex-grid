# 26: The application's changes are refused while an edit is open

Status: ready-for-agent

**What to build:** ADR-0048, "While an edit is open, the application's changes are refused"
(2026-09-29), and ADR-0050, section 6. Found on `/sheet`: `99` typed over C4 (Plums), then *Insert a
row above row 2* pressed while the edit was open; Enter wrote 99 into Pears' price, and nothing was
said.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [x] The grid raises a notification when an edit opens and when it ends: committed, cancelled or
      discarded; an edit a Reject holds open is still open. It carries no text, and it is raised in
      C# (ADR-0050 section 6, SH-29)
- [x] Off by default: a Consumer that does not listen sees nothing change (a plain ExGrid)

ExSheet:

- [x] `DoAsync`, `UndoAsync`, `RedoAsync`, `SetNumberFormatAsync` and `SetAlignmentAsync` are refused
      by name while an edit is open (a new `SheetRefusalReason` naming the open edit), and change
      nothing (SH-29)
- [x] `DeclareLinkedTableAsync` and `PushLinkedTableAsync` are not refused (ADR-0049)
- [x] ExSheet says whether an edit is open, and raises a notification when that changes, so the
      application can grey out its buttons (SH-29)
- [x] The `/sheet` demo page greys out its toolbar buttons while an edit is open, as a Consumer would
- [ ] Layer 2, one test per command and per way an edit ends; layer 3 on `/sheet`: `99` over C4,
      the insert button, Enter: 99 is in Plums' row (SH-29)

- [x] Replacing the whole Sheet Document (`Document` set by the application) while an edit is open
      discards the edit and announces it with the reason that the document was replaced (decided
      with the user 2026-09-29; ADR-0048, ADR-0050 section 6, SH-29)
- [x] The core lets a Consumer discard an open edit with a reason of its own, announced through
      `OnEditDiscarded` (ADR-0050 section 6)

## Comments

2026-09-29, implementation: ExGrid's `OnEditingChanged` (`EventCallback<bool>`) is raised with
true when an edit opens (a key typed onto a cell, F2, a double click, a press into the Formula
Bar) and false when it ends (a commit by key, by a press past the editor or by Ctrl+Enter, a
cancel, a discard at the commit or by a parameter change). It is raised in C# at the change,
before the key gate is told; the one late case is a discard by a parameter change, raised after
that render as `OnEditDiscarded` is, so the Consumer hears the end late and never the opening.
ExSheet mirrors it as `IsEditing` and `EditingChanged`, and the five commands throw
`SheetRefusedException` with `SheetRefusalReason.EditIsOpen`. `/sheet` greys out Undo, Redo, the
format button and the insert button, and catches a refusal from a press that lands before the
grey-out does. Layer 2: `EditingNotificationTests` (13) and `RefusedWhileEditingTests` (14). The
engine only gained the reason, which it never raises, so layer 1 has nothing new to pin. Layer 3
is written in `sheet.spec.mjs` (two `SH-29` tests) and checked ad hoc in headless Chrome on the
Server host under both Chromes; the suite itself was not run here, so the last box stays open
until it is.

2026-09-29, second round (the replaced-document decision, and review): the core gains
`ExGrid.DiscardEditAsync(reason)`, which discards the open edit, announces the Consumer's sentence
through the root's live region and raises `OnEditDiscarded` as the new
`EditDiscardReason.DiscardedByConsumer`, as a refused copy's `RefusedByConsumer` does. ExSheet
calls it when a different Sheet Document is handed over while an edit is open, and now says every
discard in its notice, the grid's own reasons included. One call, `PushEditingStateAsync`, tells
the Consumer and then the key gate at every edit-state site, and `OnEditingChanged` is raised and
not waited for, so a handler that awaits holds up neither the gate nor the gesture. The grid's own
undo and redo reach ExSheet through a route that says a refusal rather than throwing it into the
key handler. Layer 2 now has `EditingNotificationTests` (20), four more in `CellEditorTests` for
the Consumer's discard, and `RefusedWhileEditingTests` (18). Layer 3: the `SH-29` repro now
reaches the refusal on the Server host by pressing the insert button inside a 150 ms round trip;
checked ad hoc against a private host behind `latency-proxy.mjs`, where it passed three times and
failed with the refusal removed. The layer 2/layer 3 box stays open until the suite has run.
