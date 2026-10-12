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

2026-10-12, the second area pass (`verification/2026-10-12-windows-area-2/`): `XNPV` is settled as far as its
doubles go, and leaves this ticket's questions. Of the 2,592 candidates the third search tries
(`spikes/exsheet-financial-evaluation/`), 4 give its nine observed doubles, and they are two evaluations: the
days over 365, whole years by squaring and e^(years ln(1+rate)) otherwise, each value divided, summed from the
first, with or without Excel's last-addition adjustment, which part only where the last addition nearly
cancels (the engine refuses there; XNPV-018 asks). None of the 4 gives Excel's six `#NUM!` at negative rates
(XNPV-007 to 010, 012, 017), where each gives numbers at some: the engine checks the rate before it evaluates and is `#NUM!`
below 0, which is Excel's answer at -0.5, -1, -1.5 and -2 and the engine's at other negative rates (XNPV-020
asks -0.01), and refuses a rate of exactly 0 (XNPV-019). XNPV-013 is the correctly rounded exponential's
double where glibc's is a unit low, so the engine now computes the exponential correctly rounded on every
platform, and the logarithm too, which no observed double tells from glibc's (FV-023 and PV-016 ask); PV and
FV take the same power, and every one of their observed doubles and Error Values is its. `NPV` stays here,
undeclared: none of the 5,796 evaluations gives all 21 observed doubles, and the best give 7 (NPV-009 to 022
are recorded with Excel's doubles). None of the 39,602 evaluations gives all 29 of `PMT`'s observed payments
at a rate other than 0, and the best give 16; PMT-013 to 016, 018, 020, 021 and 023 to 032 differ from the
engine's by 1 to 11 units in the last place.

2026-10-12, decided with the user: `PMT` is kept, with an exception to ADR-0047 for `PMT` alone. Its result
may differ from Excel's in digits no format displays (observed up to 11 units in the last place, PMT-026, a
relative error of about 1e-15), because the engine gives the exact payment rounded once and the difference is
Excel's own rounding; 0.1.0 lists it under Known limitations (ADR-0202). The payments that differ are recorded
as differences by that decision. `PMT` leaves this ticket's questions; an evaluation found later that gives
every payment Excel gave would end the exception. `NPV` is not part of the exception and stays here,
undeclared.

2026-10-12, the third round of the financial area, applying that decision (the branch's choices, not the
user's). The engine holds PMT's exact payment between bounds that decide its double, with (1+rate)^nper - 1
raised from the rate itself, so that it is the exact payment rounded once at any rate above -1 and any `nper`:
the previous round's double-double lost the rate below about 1e-15 and at subnormal parts, and was 1 or 2 units
in the last place off at 874 of 3,167 such payments. `spikes/exsheet-financial-evaluation/pmt_exact.py`
checked the engine against Python's decimal at 1200 digits over 20,000 inputs, and it gave decimal's answer at
every one. PMT-011 and 012 (an `nper` of 10.5 and of -10) are answered so, each one unit in the last place from
Excel's. PMT's readings are taken as the evaluations within 64 units in the last place of Excel's at every
observed payment (`Search.cs --pmt-spread`): (1+rate)^nper - 1 through log1p and expm1, an x87's FYL2XP1 and
F2XM1, or exact, in the forms that come so near, and LibreOffice's `ScGetPMT`. Where they part in kind, a number
against an Error Value or one Error Value against another, or beyond the last digits through a double's range,
`PMT` refuses until Excel is asked (PMT-034 to PMT-046; `Search.cs --refusals` runs every reading with a
double's range over 16,896 inputs, and the engine refuses wherever they part in kind): at a rate of -1 with
periods, below -1 with a whole `nper` or a fraction whose reciprocal is odd, where a quantity a reading computes
passes a double's range or falls below its normal numbers, at a growth below 2^-52, and with no periods at the
start. Two questions for the user come of it:

- At PMT-013, PMT-025 and PMT-026, Excel's double and the exact payment rounded once differ in the 15th
  significant digit, which ExSheet's General shows (-7363.70312397638 against -7363.70312397637,
  50.9055213011993 against 50.9055213011994, 1.84529075808832 against 1.84529075808831): the decision's "digits
  no format displays" does not hold there.
- Where the readings part beyond the last digits by cancellation, PMT answers: among the 16,896 inputs, a
  reading lies up to some 2 * 10^10 units in the last place from the exact payment where the engine gives it
  (=PMT(-0.5,36.25,-150,0,0), where pv*r and (pv+fv)*r/(g-1) nearly cancel).
