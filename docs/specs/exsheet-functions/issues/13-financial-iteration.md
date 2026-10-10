# 13: Financial iteration that reproduces Excel

Status: needs-info

**What to build:** IRR, XIRR and RATE under ADR-0047's existing admission rule, including the
selected root, stopping result and failure cases at 15 significant digits.

**Blocked by:** a defensible matching iteration algorithm; observation has completed.

- [x] Audit the 143 observations and retain exact expected numbers.
- [x] Run generic Newton, secant and tighter-tolerance Newton acceptance experiments.
- [ ] Establish initialization, recurrence, root selection, stopping and failure behavior.
- [ ] Reproduce all admitted numeric and Error Value cases without loosening the comparator.
- [ ] Add production implementations and declare the functions only after that acceptance.

## Comments

2026-10-03: `spikes/exsheet-financial-solvers/compare.py` reproduces the failed experiments.
Newton disagrees in 24/60 IRR, 32/67 XIRR and 8/16 RATE cases; neither other candidate closes
the gap. IRR-018 selects a different root; XIRR-001 stops at a different number; XIRR-013
solves a case Excel refuses. RATE-009's near-zero answer also differs. Full inputs, results
and methodology remain reviewable. This does not prove that a matching algorithm is impossible.
All three functions remain undeclared (`#NAME?`).

2026-10-10, decided with the user: not pursued for now. The three functions stay undeclared
(`#NAME?`). The catalogue marks them Blocked, a status added for a function that has been observed
but that nothing yet reproduces. What would unblock them:
- an implementation of Excel's own iteration: its start, update, guard, stopping test, the iterate
  it returns, and when it fails;
- that reproduces all 143 observed cases to 15 significant digits;
- then a Windows run designed to tell candidate algorithms apart.

Microsoft's documented tolerances cannot prove any domain of arguments to match, so none is admitted
with the rest refused (ADR-0047's note of 2026-10-10). The Status stays `needs-info`: the missing
information is the algorithm.
