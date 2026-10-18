# 03: Text typed into a field rides with the next numbered input, and the Settled Mark covers typing

Status: ready-for-agent
Roadmap: ExGrid, ExSheet — Text typed into the Cell Editor, Formula Bar or Name Box at machine speed is never overtaken by the key, press or click that follows it.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 2. Every numbered input carries the text of the field that holds the keyboard; the core adopts it first and drops the older input events (§5), generalising `InputOvertaken`. The Settled Mark waits until a field's text is the text its surface last rendered; ASY-7's exclusion of typed text goes.

- [ ] Every numbered input carries the field's text, and the core adopts it
- [ ] The Settled Mark covers typing; ASY-7 rewritten
- [ ] Layer 3: typing followed at once by a press, a heading click and a menu command, on both hosts

**Blocked by:** 02.
