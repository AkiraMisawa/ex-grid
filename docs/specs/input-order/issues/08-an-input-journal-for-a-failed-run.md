# 08: Each instance keeps a journal of its numbered inputs, and a failed layer-3 test carries it

Status: ready-for-agent
Roadmap: internal

**Why:** the Hunt traces of 2026-10-19 (FD-6, KB-31, ED-22 nested) could not say which inputs the
script numbered, which it held, and which the core answered. A Playwright trace keeps the DOM and the
HTTP log, not the WebSocket frames, and not the script's `answering`, `pressAnswer` or `inputsSent`.
Each cause was inferred from the Settled Stamp and then confirmed by a fix. Under ADR-0220 every red
that passes on a re-run is a defect to explain, so the evidence has to be in the failed run's
artifact.

**What it is:** each instance records, in order, every input the script numbers (its number and kind,
held or sent, the mode it was gated against), every hold and its release, and every answer the core
gives (`data-ex-answered`), in a bounded buffer. The core's side (`RunAsync` entered, finished, made
void) is recorded too. A layer-3 test that fails attaches both, per instance on the page.

**Decided** (ADR-0240 section 10, 2026-10-20): internal only. The script's side is an always-on ring
buffer kept by the existing listeners; the core's side goes to `ILogger` at Debug. No public API, and
no ADR-0021 entry.

- [x] The decision recorded
- [ ] The script's side and the core's side recorded per instance
- [ ] `tests/ExGrid.Browser` attaches the journal to a failed test, on both hosts
- [ ] Nothing per cell, and nothing per pointer move, is recorded (ADR-0027 P1-P9)
