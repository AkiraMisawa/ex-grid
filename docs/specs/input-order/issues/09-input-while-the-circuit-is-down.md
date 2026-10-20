# 09: Input made while the Server circuit is down is refused aloud, and never stalls the order

Status: ready-for-agent
Roadmap: ExGrid, ExSheet, ExPivot — Keys and presses made while a Blazor Server connection is lost are refused visibly once it is back, and the grid keeps answering.

**Why:** ADR-0240 numbers every input in the script and has the core take them one at a time, in
number order, with no clock. It does not say what happens to a number sent while the circuit is
disconnected and reconnecting. If that message never reaches the core, the core waits for it before
every later number, and since ADR-0240 §4 removed the clocks, nothing ends that wait (inferred, not
yet reproduced). A key the user typed during the outage is also lost without a word, which is the
spine's first principle broken.

**What it is:** Once the circuit is back, any input that did not reach the
core is refused aloud (ADR-0240 §4, ADR-0203), and its number is made void, so the order goes on. A
circuit that does not come back is Blazor's to show.

**Decided** (ADR-0240 section 11, 2026-10-20): refused, never replayed. The script learns of the
failure from its own call's rejected promise, keeps the number owed-void and the root unsettled, and
sends it with the next message that reaches the core; no ADR-0021 entry.

- [x] The decision recorded
- [ ] Layer 3 on the Server host: drop the connection through the latency proxy, type and press, bring
      it back; the inputs are refused aloud and the next key is answered
- [ ] Layer 2: a number made void after reconnection releases the inputs behind it
- [ ] Docs Site: the Server hosting page says what the user sees; a Change Note
