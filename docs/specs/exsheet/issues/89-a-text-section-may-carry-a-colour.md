# 89: A text section may carry a colour: `0;[Red]@`

Status: ready-for-agent

**What to build:** an Excel fidelity gap ticket 48 found. The engine refuses `0;[Red]@`, but Excel took it in
the eleventh run's case 3b (`verification/2026-10-01-windows-excel-11/cell-format.md`, case 3b). The record
reads that `@` belongs in the fourth section, and that a `[Red]` there colours text.

**Blocked by:** None (can start immediately)

- [ ] **Read the case's record exactly:** what Excel stored for `0;[Red]@`, and what each value showed in
      which colour.
- [ ] **The engine reads the code as Excel does.** It shows what Excel showed, in the colour Excel
      showed, for a number, a negative number, zero and text. Where the record does not say, build
      the reading and name it in the comment.
- [ ] **Case 3b on the DemoHost** (`/sheet?case=3b`) includes the cell it now leaves out.
- [ ] **Layer 1** tests named after ADR-0047, ADR-0071 and case 3b.
