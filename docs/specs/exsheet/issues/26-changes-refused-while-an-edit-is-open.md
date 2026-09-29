# 26: The application's changes are refused while an edit is open

Status: ready-for-agent

**What to build:** ADR-0048, "While an edit is open, the application's changes are refused"
(2026-09-29), and ADR-0050, section 6. Found on `/sheet`: `99` typed over C4 (Plums), then *Insert a
row above row 2* pressed while the edit was open; Enter wrote 99 into Pears' price, and nothing was
said.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [ ] The grid raises a notification when an edit opens and when it ends: committed, cancelled or
      discarded; an edit a Reject holds open is still open. It carries no text, and it is raised in
      C# (ADR-0050 section 6, SH-29)
- [ ] Off by default: a Consumer that does not listen sees nothing change (a plain ExGrid)

ExSheet:

- [ ] `DoAsync`, `UndoAsync`, `RedoAsync`, `SetNumberFormatAsync` and `SetAlignmentAsync` are refused
      by name while an edit is open (a new `SheetRefusalReason` naming the open edit), and change
      nothing (SH-29)
- [ ] `DeclareLinkedTableAsync` and `PushLinkedTableAsync` are not refused (ADR-0049)
- [ ] ExSheet says whether an edit is open, and raises a notification when that changes, so the
      application can grey out its buttons (SH-29)
- [ ] The `/sheet` demo page greys out its toolbar buttons while an edit is open, as a Consumer would
- [ ] Layer 2, one test per command and per way an edit ends; layer 3 on `/sheet`: `99` over C4,
      the insert button, Enter: 99 is in Plums' row (SH-29)

Waiting for a decision (do not build):

- Replacing the whole Sheet Document (`Document` set by the application) while an edit is open. A
  parameter cannot be refused. Asked of the user on 2026-09-29.
