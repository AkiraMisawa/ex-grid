# 93: Keys typed while a Consumer's own frame opens do not reach the grid

Status: ready-for-agent

**What to build:** the gap ADR-0039's note of 2026-10-01 leaves open, found by the fix for PR #42's CI.
- On the Server host, choosing the Context Menu's "Format Cells…" returns the keyboard to the grid's root after
  the command.
- `ExSheet.MudBlazor`'s dialog takes it a round trip later.
- A key typed in between reaches the grid. A digit would start an edit in the Focus cell behind the dialog. That
  is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [ ] **Show it first,** on the Server host with an injected round trip, as the CI fix did at 80 ms.
- [ ] **Keys typed in the gap reach the frame, in order, or are refused by name.** They are never the grid's.
      - For example, a command can tell the core that the Consumer is opening a frame of its own (ADR-0050
        item 16), so the core holds the keys for it instead of handing the keyboard to its root.
      - If that needs a new declaration, stop and report it as a proposal.
- [ ] **The built-in Chrome's popover** gets the same guarantee, if it has the same gap.
- [ ] **Layer 2** stages the circuit's order. **Layer 3** on the Server host. CI runs it.
