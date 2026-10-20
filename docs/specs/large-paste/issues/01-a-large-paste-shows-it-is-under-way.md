# 01: A paste still being read says it is under way, and Escape cancels it

Status: done
Roadmap: ExGrid — A large paste shows that it is under way while its data is read, and Escape cancels it.

**Why:** while a paste's streams are read and parsed, the inputs behind it wait (ADR-0240). On a slow
circuit that is seconds, and the grid shows nothing. A grid that looks idle and does not answer is
the "looks plausible but is not" the spine forbids.

**What it is:** from the paste's start until it lands or is refused, the root says so (`aria-busy`,
and a Visual Token for a visible sign), and the live region announces it once.

**Decided** (ADR-0240 section 12, 2026-10-20): an Escape typed while the paste is under way cancels
it, told to the core outside the order. A paste that has not raised its Edit Intent is refused aloud
(`PasteRefusalReason.Cancelled`); one that has stands, and the Escape's turn says so aloud. The
Escape does nothing else.

- [x] The decision recorded
- [x] Layer 2: a cancel before the intent refuses the paste; one after it leaves it standing and says so
  (`PasteUnderWayTests`, with a cancel ahead of the paste's turn and one naming a finished paste)
- [x] Layer 2: busy from the paste's start to its landing or refusal
- [x] Layer 3: the sign is painted during a large paste on the Server host (`circuit.spec.mjs`, about
  200 KB rather than 10 MB: the sign is read from the render that applies it, and the cancel holds the
  host's answers instead of counting on a long read, ADR-0220)
- [x] Docs Site's paste page and a Change Note
