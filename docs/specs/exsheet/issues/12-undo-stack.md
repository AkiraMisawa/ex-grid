# 12: The one undo stack

Status: ready-for-agent

**What to build:** ExSheet holds one undo stack. Each user operation is one step: an edit now, and paste, fill,
insertion and deletion as they arrive. Ctrl+Z undoes and Ctrl+Y redoes. A change the Consumer makes
through ExSheet's commands lands on the same stack. Replacing the Sheet Document clears it.

**Blocked by:** 02

- [ ] Ctrl+Z and Ctrl+Y step through edits in order (ADR-0048)
- [ ] A Consumer command is undone in its place in the order
- [ ] Replacing the document clears the stack
- [ ] Two ExSheets on one page keep separate stacks (ADR-0018)

## Comments
