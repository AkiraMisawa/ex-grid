# 02: ExGrid takes every input one at a time, by its number, and nothing waits on a clock

Status: ready-for-agent
Roadmap: ExGrid — Keys, presses and clicks on headings, menus and buttons sent at machine speed are taken in the order they were made, on Blazor Server and WebAssembly alike.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 1. The core gets one queue per instance (§3); every input the script hears is numbered as it happens and claimed by its Blazor event (§2), headings, ▾ openers, menus, the completion list, the pager and the status buttons among them; `editorSettled`'s 2 s give-up and every wait like it are replaced by a wait on a number or a mark, and an input that no longer holds is refused aloud (§4).

- [ ] One queue per instance; the commit barrier, `_pressAnswer`'s chain and the `core` keys sent without waiting folded into it
- [ ] Every click the script hears inside the Input Root numbered and claimed; an unclaimed number answered void
- [ ] No wait ends on a clock; a refused input is said in the live region
- [ ] `spikes/render-bench` mode measured against `main` (ADR-0240, "What it may cost")
- [ ] Layer 2 and the targeted layer 3 green; the hunt and the widened run green on the branch

**Blocked by:** nothing.
