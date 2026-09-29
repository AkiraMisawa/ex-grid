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

- [ ] Replacing the whole Sheet Document (`Document` set by the application) while an edit is open
      discards the edit and announces it with the reason that the document was replaced (decided
      with the user 2026-09-29; ADR-0048, ADR-0050 section 6, SH-29)
- [ ] The core lets a Consumer discard an open edit with a reason of its own, announced through
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
