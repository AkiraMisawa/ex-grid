# 13: Inserting and deleting rows and columns

Status: ready-for-agent

**What to build:** Commands on the Context Menu insert and delete rows and columns. References are rewritten to keep
naming the same cells, and a deleted target is `#REF!`. Because rows and columns are places, the
Row Sequence Version does not move and the Selection stays where it was. Each command is one undo
step.

**Blocked by:** 03, 12

- [ ] Inserting above a referenced cell rewrites every Reference to it (ADR-0046/0047)
- [ ] Deleting a referenced cell makes the Formula `#REF!`
- [ ] The Selection stays in place after an insertion, as in Excel (ADR-0011/0046)
- [ ] One Ctrl+Z restores the structure and every Reference

## Comments
