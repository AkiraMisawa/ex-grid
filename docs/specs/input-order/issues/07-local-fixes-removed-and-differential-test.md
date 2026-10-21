# 07: The local fixes the order covers go, and a fast-versus-slow differential test accepts it

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 6. Each local fix is kept, folded or removed, and its ADR gets a note saying which: ADR-0230 §1–§3, `InputOvertaken`, ADR-0204's addendum (`pressedPopovers`), ADR-0210's synthetic `change`. Layer 3 gains the differential test of §8: seeded sequences of keys, presses, typed text and commands, at machine speed on the Server host (150 ms) and WebAssembly, against the same sequence sent one at a time; every product.

- [ ] Each local fix decided and its ADR noted
- [ ] The differential test, seed printed, on both hosts, every product
- [ ] The Definition of Done gains its criterion
- [ ] Normal CI and the related input tests' repeated and widened runs green; the full hunt runs asynchronously (ADR-0240 §7, revised 2026-10-20)

**Blocked by:** 02 to 06.
