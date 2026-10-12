# 13: Financial iteration that reproduces Excel

Status: needs-info
Roadmap: ExSheet — NPV, IRR, XIRR and RATE, once an evaluation is found that gives every answer Excel
was seen to give; until then they are `#NAME?` (ADR-0047, ADR-0202).

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

2026-10-12, the area pass (`verification/2026-10-12-windows-area/`, ADR-0202's note of 2026-10-12): `NPV`
joins this ticket, undeclared (`#NAME?`) for 0.1.0. The pass read NPV-005 to 008, which ask what the
seventeenth run's NPV-001 and 002 left open. Excel answers the same values alike whether they are typed
or read from a range, and with the rate typed or in a cell, so a split by the kind of argument is ruled
out. That leaves three distinct observed doubles (NPV-001, NPV-002/005/006 and NPV-003), and no
evaluation searched gives all three: 757 in `search.py` and 3,876 in `Search.cs` (every way of
discounting a value, every order of summing, Horner's rule from either end, 53 and 64 bits, fused
multiply-adds, an x87 register stored to a double, Excel's last-addition adjustment). The running
product summed forward gives NPV-001 and NPV-003 and is one unit in the last place from NPV-005;
Horner's rule from the first value with a fused multiply-add gives NPV-001 and NPV-005 and is one unit
from NPV-003. NPV-009 to 022 are recorded cases chosen to tell the nearest evaluations apart
(`spikes/exsheet-financial-evaluation/discriminate.txt`). IRR's root is where NPV is 0, so the
evaluation found for NPV is the one IRR's iteration would rest on.

`PMT` stays Supported, but its last digits are open here too: PMT-013 to 016 and 018 differ from
Excel's by one to three units in the last place, and none of the 11,242 evaluations in `search.py` nor
the 7,344 in `Search.cs` gives every payment Excel was seen to give. PMT-011 and 012 (an `nper` of 10.5
and of -10) are refused by decision until one does. Whether PMT is corrected or refused before the tag is
the user's question (ADR-0202); PMT-019 to 033 ask Excel more.

`XNPV` stays Supported (ticket 05), but its evaluation is open as PMT's is (2026-10-12, later). Excel's two
observed doubles, XNPV-001 and 004, are given by 72 of the 1,496 evaluations `Search.cs` tries: the days
over 365 or times 1/365; the discount e^(years ln(1+rate)), at whole years by squaring or a power, a
correctly rounded power, or an x87's; each value divided by it or multiplied by its reciprocal; summed
forward, with a fused multiply-add or Excel's last-addition adjustment, or pairwise. The engine computes
six and refuses where they part (XNPV-005, 006 and 011); the 72 part at 1,668 of the 4,004 inputs
`Search.cs --refusals` draws where the engine answers. Whether XNPV is refused for 0.1.0 as NPV is, refused
wherever the 72 part, or asked before the tag is the user's question (ADR-0202); XNPV-012 to 017 are the
inputs that tell the 72 apart (`discriminate-xnpv.txt`).

2026-10-12, the second area pass (`verification/2026-10-12-windows-area-2/`): `XNPV` is settled and leaves
this ticket's questions. Its nine observed doubles and eight Error Values are given by one evaluation of the
2,592 the third search tries (`spikes/exsheet-financial-evaluation/`): the days over 365, whole years by
squaring and e^(years ln(1+rate)) with the exponential and the logarithm correctly rounded otherwise, each
value divided, summed from the first, and `#NUM!` at a rate below 0. XNPV-013 is the correctly rounded
exponential's double where glibc's is a unit low, so the engine now computes both functions correctly
rounded on every platform; PV and FV take the same power, and every one of their observed doubles and Error
Values is its. `NPV` stays here, undeclared: none of the 5,796 evaluations gives all 21 observed doubles, and
the best give 7 (NPV-009 to 022 are recorded with Excel's doubles). `PMT` stays Supported with its last
digits open: none of the 39,602 evaluations gives all 29 observed payments, and the best give 16; PMT-013 to
016, 018, 020, 021 and 023 to 032 differ from the engine's by 1 to 11 units in the last place. Whether PMT is
refused for 0.1.0 is the user's question (ADR-0202; `docs/agents/proposals/area-financial.md`, section 1).
