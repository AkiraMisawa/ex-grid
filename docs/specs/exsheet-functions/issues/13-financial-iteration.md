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
may differ from Excel's in its last digits (observed up to 11 units in the last place, PMT-026, a relative
error of about 1e-15), the engine giving the exact payment rounded once; 0.1.0 lists it under Known limitations
(ADR-0202). *(The decision was taken on digits no format displays, and on the difference being Excel's own
rounding. Neither is observed: at PMT-013 and PMT-026 the difference shows in the 15th significant digit, which
General shows (question 1 below), and no run has read how Excel rounds its payment; that it is Excel's own
rounding is a reading of the searches, which no evaluation reproduces.)* The payments that differ are recorded
as differences by that decision. `PMT` leaves this ticket's questions; an evaluation found later that gives
every payment Excel gave would end the exception. `NPV` is not part of the exception and stays here,
undeclared.

2026-10-12, the third and fourth rounds of the financial area, applying that decision (the branch's choices,
not the user's). Where `PMT` answers, the engine holds the payment at the end of each period between bounds that
decide its double, with (1+rate)^nper - 1 raised from the rate itself, so that it is the exact payment rounded
once however small the rate: the second round's double-double lost the rate below about 1e-15 and at subnormal
parts, and was 1 or 2 units in the last place off at 874 of 3,167 such payments. The payment at the start is that
double divided by 1+rate and rounded again (question 3 below, decided on 2026-10-13: the exact payment at the start
rounded once). `spikes/exsheet-financial-evaluation/pmt_exact.py`
checked the engine against Python's decimal at 1200 digits over 20,000 inputs, and it gave decimal's answer by
those rules at every one. PMT-011 and 012 (an `nper` of 10.5 and of -10) are answered so, each one unit in the
last place from Excel's. PMT's readings are taken as the evaluations within 64 units in the last place of Excel's
at every observed payment (`Search.cs --pmt-spread`): (1+rate)^nper - 1 through log1p and expm1, an x87's FYL2XP1
and F2XM1, or exact, in the forms that come so near, and LibreOffice's `ScGetPMT`. Where they part in kind, a
number against an Error Value or one Error Value against another, or beyond the last digits, `PMT` refuses until
Excel is asked (PMT-034 to PMT-053): at a rate of -1 with periods, below -1 with a whole `nper` or a fraction
whose reciprocal is odd, where a quantity a reading computes passes a double's range or falls below its normal
numbers, at a growth below 2^-52, with no periods at the start, and where a reading magnifies its own roundings
more than 2^6 times (pv g and fv nearly cancelling, a growth far below 1 taken as 1 + (g-1), the exponent's
rounding, and at the start a tiny `nper`, where LibreOffice's divisor comes out as 0, or a rate far above 1).
`Search.cs --refusals` runs every reading with a double's range over 20,096 inputs, and `--pmt-readings` over
`pmt_exact.py`'s 20,000: the engine refuses wherever they part in kind, and where it answers every reading lies
within 93 units in the last place of its payment. Three questions for the user come of it:

1. **The 15th digit.** At PMT-013 and PMT-026, payments at the end, Excel's double and the exact payment rounded
   once, the engine's, differ in the 15th significant digit, which ExSheet's General shows (-7363.70312397638
   against -7363.70312397637, 1.84529075808832 against 1.84529075808831): the decision's "digits no format
   displays" does not hold there. At PMT-025, at the start, Excel's double and the exact payment rounded once
   agree to 15 digits (50.9055213011993), and the engine's double, rounded again (question 3), shows
   50.9055213011994.
2. **Cancellation.** Where a reading magnifies its own roundings, the readings part far beyond the last digits,
   and `PMT` now refuses there, by a bound of 2^6 that is the branch's: at `=PMT(0.1,2,1000,-1210)`, where pv g
   and fv cancel, 139 readings give 0, 75 give 1.691768418476429E-14, 60 the exact payment rounded once,
   -5.8154539385127246E-15, and others -1.98E-14, -1.42E-14, -1.14E-14 or -9.5E-15; at
   `=PMT(0.1,1E-17,1000,-1000)` 216 give 0, 60 the exact payment, -100, and others -104.8, -102.4, -96 or -88;
   at `=PMT(-0.5,36.25,-150,0,0)` they agree to about 5 significant digits. Keeping the refusal, with this or
   another bound, or answering there with the exact payment under the exception, as the third round did, is the
   user's to decide.
3. **The payment at the start.** The engine divides the payment at the end's double by 1+rate and rounds again.
   That gives Excel's double at PMT-008, 009, 010 and 019, where the exact payment at the start rounded once is a
   unit from Excel's, but it is not the exact payment rounded once that the exception names: it lies a unit from
   it at 809 of the 4,132 payments at the start `pmt_exact.py` draws (20%), and at PMT-014, 015, 025 and 029,
   where Excel's double lies on the other side of it. The exact payment at the start rounded once would differ
   from Excel's at 23 of the 30 observed payments instead of 19, lie a unit nearer Excel's at PMT-014, 015, 025
   and 029, and agree with Excel's at 15 digits at PMT-025.

2026-10-13, decided with the user (question 3, option (a)): the payment at the start of each period is the
exact payment at the start rounded once, as the exception names, not the payment at the end's double divided by
1+rate and rounded again. Excel's payment differs from the engine's at 23 of the 30 observed payments, by 1 to 11
units in the last place: a unit at PMT-008, 009, 010 and 019, where the double rounding gave Excel's double, and a
unit nearer Excel's at PMT-014, 015, 025 and 029. Two of them differ in the 15th significant digit, PMT-013 and
PMT-026, both at the end; PMT-025 agrees with Excel's at 15 digits.

2026-10-13, decided with the user (questions 1 and 2, option (a) each): the exception is restated as observed.
Excel's double and the exact payment rounded once differ by 1 to 11 units in the last place, and at PMT-013 and
PMT-026 that shows in General's 15th digit; `PMT` is kept there too (ADR-0047's note of that date). The bound of
2^6 is kept: where a reading magnifies its own roundings more than 2^6 times, `PMT` refuses until Excel is asked
(PMT-047 to PMT-053). No question of this ticket's `PMT` part is open.
