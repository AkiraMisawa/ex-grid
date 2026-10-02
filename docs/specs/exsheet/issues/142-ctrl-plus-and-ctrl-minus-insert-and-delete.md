# 142: Ctrl+Plus and Ctrl+Minus insert and delete rows and columns

Status: ready-for-agent

**What to build:** ADR-0050 item 14's note of 2026-10-02. ExSheet declares Ctrl with `+` (with Shift and
without) and Ctrl with `-`. Over a Selection of whole rows they insert or delete those rows, over whole
columns those columns, as the Context Menu's commands do, each one undo step, on the Selection the key
carries. Any other Selection changes nothing and says why; so does a key while an edit is open.

**Blocked by:** None (can start immediately)

- [ ] **Whole rows, whole columns, every cell, a part of a row, several ranges**: each answered as
      SH-48 says.
- [ ] **The page does not zoom** from a key pressed inside the Sheet.
- [ ] **Layer 2** for each Selection shape; **layer 3** on both hosts, with the key straight after
      Shift+Space.
