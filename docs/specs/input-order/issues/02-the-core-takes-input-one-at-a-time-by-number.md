# 02: ExGrid takes every input one at a time, by its number, and nothing waits on a clock

Status: ready-for-agent
Roadmap: ExGrid — Keys, presses and clicks on headings, menus and buttons sent at machine speed are taken in the order they were made, on Blazor Server and WebAssembly alike.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 1. The core gets one queue per instance (§3); every input the script hears is numbered as it happens and claimed by its Blazor event (§2), headings, ▾ openers, menus, the completion list, the pager and the status buttons among them; `editorSettled`'s 2 s give-up and every wait like it are replaced by a wait on a number or a mark, and an input that no longer holds is refused aloud (§4).

- [x] One queue per instance; the commit barrier and the `core` keys sent without waiting folded into it (`_pressAnswer`'s chain still stands inside the press answers' slots)
- [x] Every click the script hears inside the Input Root numbered and claimed; an unclaimed number answered void (at the next telling, not at a later number: ADR-0240, "As built")
- [x] No wait ends on a clock; a refused input is said in the live region
- [x] `spikes/render-bench` mode measured against `main` (ADR-0240, "What it may cost")
- [ ] Layer 2 and the targeted layer 3 green; the hunt and the widened run no worse on the branch than on `main` (decided with the user, 2026-10-19: `main` itself fails about 33 tests in the widened run, none of them about the Input Order; those get tickets of their own, and a failure only the branch shows is fixed here)

**Blocked by:** nothing.

## Comments

2026-10-18: Built on `claude/project-thread-8j2tu3` (ADR-0240, "As built: ticket 02"). Layers 1 and 2
green; the targeted layer 3 green on both hosts under the bundled Chromium (no Chrome or Edge in the
container); CI's full run, the hunt and the widened run, and the render-bench measurement still owed.
