# Area: financial — proposals after the fourth round of 2026-10-12

Branch `claude/area2-financial`, on `claude/area-corrections` (which holds the first round, `claude/area-financial`).
The cases: FV-011 to 024, PV-008 to 016, PMT-011 to 053, NPV-001 and 009 to 022, XNPV-005 to 030. The
evidence: `verification/2026-10-12-windows-area-2/` (`results-2026-10-12-area-2-com-en-US.json`, Excel
16.0.20430.20146, each double Excel's `number`, G17), the first pass `verification/2026-10-12-windows-area/`, the
seventeenth run's results, and `spikes/exsheet-financial-evaluation/` (`Search.cs`, `search-cs.json`,
`pmt_exact.py`; the README says what each family reproduces, which observed doubles rule each out, and what the
third and fourth rounds' checks found).

## Needs the user's decision (2026-10-12, the third and fourth rounds)

The branch does not decide these. Meanwhile PMT answers and refuses as below: the rule at the start (3) and the
refusal where the readings part by cancellation (2) are the branch's choices, not the user's.

**1. PMT's exception says "digits no format displays", and at three observed payments a format displays them.**
ExSheet's General shows 15 significant digits (`NumberText.General`, G15), so a wide enough General cell, or a
format with that many decimals, shows these:

| Case | Where | Excel's double | at 15 digits | The exact payment rounded once | at 15 digits | ExSheet's | at 15 digits |
|---|---|---|---|---|---|---|---|
| PMT-013 | at the end | -7363.7031239763755 | -7363.70312397638 | -7363.703123976373 | -7363.70312397637 | the exact payment | -7363.70312397637 |
| PMT-026 | at the end | 1.8452907580883169 | 1.84529075808832 | 1.8452907580883144 | 1.84529075808831 | the exact payment | 1.84529075808831 |
| PMT-025 | at the start | 50.90552130119933 | 50.9055213011993 | 50.90552130119934 | 50.9055213011993 | 50.90552130119935 | 50.9055213011994 |

At PMT-013 and PMT-026 Excel's double and the exact payment rounded once differ in the 15th digit. At PMT-025
they agree to 15 digits, and it is ExSheet's double, the payment at the end's double over 1+rate rounded again
(question 3), that shows another 15th digit. The other 16 of the 19 observed payments that differ from ExSheet's
agree with it at 15 digits. Options: (a) keep the exception, its scope restated as 1 to 11 units in the last
place, which at some payments reaches the 15th significant digit; (b) narrow it, so that PMT refuses where its
readings could differ from the exact payment at 15 digits (the engine knows the exact payment, but not on which
side of a 15-digit step Excel's double falls, and the steps lie some 5 to 90 units in the last place apart, so
that within 11 units of one is a large share of all payments); (c) refuse PMT for 0.1.0, `#NAME?` as `NPV`, a
Known limitation. The branch's texts state the facts without "no format displays", and quote it only as the
decision's words.

**2. Where PMT's readings part far beyond the last digits by cancellation, PMT now refuses; whether it should.**
The third round answered there with the exact payment. A reading's roundings are magnified where the terms it
adds nearly cancel, and there the readings give 0, numbers of the other sign, or numbers of a few significant
digits, against the exact payment (`Search.cs --pmt-show`):

| Formula | The exact payment rounded once | What the 375 readings at the end give |
|---|---|---|
| `=PMT(0.1,2,1000,-1210)` | -5.8154539385127246E-15 | 0 (139), 1.691768418476429E-14 (75), the exact payment (60), -1.9761969838327786E-14, -1.4210854715202004E-14, -1.1368683772161604E-14, -9.5E-15 and others |
| `=PMT(0.1,1E-17,1000,-1000)` | -100 | 0 (216), -104.83672617280074 (63), -100 (60), -102.37961540312571 (15), -104, -96, -88 and others |
| `=PMT(-0.5,36.25,-150,0,0)` | 9.17749001306952E-10 | the exact payment (200), and 9.177492099722465E-10, 9.177512083624606E-10 and others, agreeing to about 5 significant digits |

pv·g and fv cancel at the first two (1000 × 1.1^2 against 1210, and 1000 × 1.1^1E-17 against 1000), and the third
takes a growth of 1.2E-11 as 1 + (g − 1), which keeps some 5 of its digits. The branch now refuses (`#VALUE!`,
PMT-047 to PMT-053) wherever a reading magnifies its own roundings more than 2^6 times
(`ExactPayment.Conditioned`): (|pv|·(1+g) + |fv|) / |pv·g + fv|; |x|·|pv+fv|·g / (|g−1|·|pv·g + fv|), x =
nper·ln(1+rate), the exponent's rounding (=PMT(1,1000,0,1000), where readings lie some 600 units apart); and at
the start |1+rate| + |rate| (the payment at the end less its rate's share), LibreOffice's divisor
(|nper+1|·|ln(1+rate)|·(1+rate)·g + |rate|) / ((1+rate)·|g−1|), which is 0 at a tiny `nper` whose nper+1 rounds to
1 (=PMT(0.1,1E-17,1000,0,1), where LibreOffice's reading gives `#NUM!` and the others -9.5382351702337E+19), and
its power of pv. No observed payment has a factor above 2^5 (PMT-020's, 32.4). Where PMT answers, every reading
run lies within 93 units in the last place of its payment (`Search.cs --refusals` over its 20,096 grid
inputs, `--pmt-readings` over `pmt_exact.py`'s 20,000). Options: (a) keep the refusal with this bound; (b)
another bound; (c) answer there with the exact payment, the exception covering it, as the third round did; (d)
as 1(c).

**3. PMT's payment at the start of each period is not the exact payment rounded once that the exception names.**
The engine gives the payment at the end's double divided by 1+rate exactly and rounded again: the branch's rule,
carried from before the exception. It gives Excel's double at PMT-008 (and 009, 010 and 019, the same payment),
-147.95035898410148, where the exact payment at the start rounded once is -147.95035898410146; but it lies a unit
in the last place from the exact payment rounded once at 809 of the 4,132 payments at the start
that `pmt_exact.py` draws, and at PMT-014, 015, 025 and 029, where Excel's double lies on the other side of the
exact payment, nearer it than the engine's:

| Case | Excel's double | The exact payment at the start rounded once | ExSheet's (rounded again) |
|---|---|---|---|
| PMT-008 | -147.95035898410148 | -147.95035898410146 | -147.95035898410148 |
| PMT-014 | -21530.430018725485 | -21530.43001872549 | -21530.430018725492 |
| PMT-015 | -852.5226402171 | -852.5226402170999 | -852.5226402170998 |
| PMT-025 | 50.90552130119933 | 50.90552130119934 | 50.90552130119935 |
| PMT-029 | 63.197272167756296 | 63.19727216775628 | 63.197272167756275 |

Options: (a) the exact payment at the start rounded once, which the exception's words name: PMT-008, 009, 010 and
019 then differ from Excel's by a unit (23 of the 30 observed payments differ instead of 19), PMT-014, 015, 025
and 029 come a unit nearer Excel's, and PMT-025 agrees at 15 digits; (b) keep the double rounding, which gives
PMT-008's double but lies outside the exception as worded, the exception then naming it.

## What the branch did, in brief

- **Decided with the user on 2026-10-12, and applied: `PMT` is kept, under an exception to ADR-0047 for
  `PMT` alone.** Its result may differ from Excel's in digits no format displays (observed up to 11 units in the
  last place, PMT-026, a relative error of about 1e-15; but see question 1), the decision resting on the engine
  giving the exact payment rounded once (but see question 3); it is listed under 0.1.0's Known limitations. The 19
  observed payments that differ from the engine's (PMT-011 to 016, 018, 020, 021, 023 to 032) are recorded as
  differences by that decision, `source: observed`, `expect` the engine's double and `excelExpect` Excel's; the
  11 that agree are observed. `NPV` is not part of the exception and stays undeclared.
- **The third round (the branch's choices, not the user's).** PMT's exact payment at the end is held between
  bounds that decide its double (`Enclosure`, `ExactPayment`), with (1+rate)^nper − 1 raised from the rate itself:
  the second round's double-double gave visibly wrong payments at rates below about 1e-290 (=PMT(1E-320,0.5,1000)
  gave -2007.94 for -2000) and was 1 or 2 units off at 874 of 3,167 payments at rates below 1e-15. PMT's readings,
  the evaluations within 64 units in the last place of Excel's at every observed payment, are run with a double's
  range (`Search.cs --refusals`), and where they part in kind PMT refuses: at a rate of −1 with periods and below
  −1 with a whole `nper` or an odd root (PMT-034 to 037; PMT-036 and 037 were the engine's answers, -6 and
  `#DIV/0!`), where a quantity they compute passes a double's range or falls below its normal numbers (PMT-038 to
  044), at a growth below 2^-52 (PMT-045), and with no periods at the start (PMT-046).
- **The fourth round (the branch's choices, not the user's).** PMT also refuses where a reading magnifies its own
  roundings more than 2^6 times (question 2; PMT-047 to PMT-053): where pv·g and fv cancel, where a growth far
  below 1 is taken as 1 + (g − 1), where the exponent's rounding moves the payment, and at the start where
  LibreOffice's divisor comes out as 0 or far from (1+rate)·((1+rate)^nper − 1), as at a tiny `nper`
  (=PMT(0.1,1E-17,1000,0,1)), or where the payment at the end less its rate's share cancels, at a rate far above
  1. The texts no longer say that the payment at the start is the exact payment rounded once (question 3), nor
  that PMT answers "at any rate above −1 and any `nper`". `pmt_exact.py` checked 20,000 inputs against Python's
  decimal at 1200 digits, tiny `nper` and the new refusals among them: the engine gives decimal's answer by its
  rules at every one. **XNPV** reads its rate and every cell of both ranges before it answers, and refuses where
  more than one answer results (XNPV-021 to 030): a rate that is an Error Value no longer returns before the
  ranges are read, a date before the first counts wherever it lies, and two different Error Values in one range
  are refused.
- **Settled and followed: `PV` and `FV`** (unchanged): one of the 480 evaluations searched for each, the
  financial power, gives all 17 FV and 9 PV doubles and all five Error Values. PV-014, PV-015, FV-023 and FV-024
  ask where the power is the engine's reading; PV at a rate of −1 with an `nper` above 0 is refused (PV-013 asks).
- **`XNPV` follows Excel at every observed double** (unchanged): 4 of 2,592 candidates give all nine, two
  evaluations; the engine is `#NUM!` below 0, Excel's answer at the four rates asked and the engine's at any other
  (XNPV-020 asks −0.01), and refuses a rate of 0 (XNPV-019) and a last addition that nearly cancels (XNPV-018).
- **Not settled: `NPV`**, which stays undeclared (`#NAME?`) for 0.1.0 as ADR-0202's note decided.
- **New cases:** PMT-039 to PMT-046 and XNPV-021 to XNPV-026 (the third round), PMT-047 to PMT-053 and XNPV-027
  to XNPV-030 (the fourth), all `uncertain`, each the engine's `#VALUE!`; PMT-034 to 038's descriptions say what
  the readings give; the descriptions of PMT-008, 011 to 016, 018, 020, 021 and 023 to 032 say how far Excel's
  double lies from the engine's and from the exact payment rounded once, and whether they agree at 15 digits.

The branch edits itself what is not an ADR, the glossary, the Definition of Done, the Docs Site or the shared
engine README: the engine, the corpus, the spike, the financial Change Notes, the catalogue's PMT, PV, FV, NPV
and XNPV rows and its notes 15 and 16 (`docs/specs/exsheet-functions/spec.md`), and ticket 13's comments.

## PMT: the decision and the cases it covers (applied in the branch)

**The decision** (with the user, 2026-10-12): PMT is kept, with an exception to ADR-0047 for PMT alone: its
result may differ from Excel's in digits no format displays (observed up to 11 units in the last place, a
relative error of about 1e-15), because the engine gives the exact payment rounded once and the difference is
Excel's own rounding. It is listed under 0.1.0's Known limitations. NPV stays undeclared, and is not part of
the exception. (Question 1 above: at three of the payments below, a format displays the difference. Question 3:
at the start of each period the engine's payment is not the exact payment rounded once.)

**What it covers.** The 30 payments Excel was asked (PMT-001 to 004, 008 to 033; PMT-004 at a rate of 0), of
which these 19 differ from the engine's; "Exact" is the exact payment rounded once, which is the engine's at the
end of each period (`type` 0):

| Case | Formula | Excel | Engine | Exact, where not the engine's | Units, Excel from the engine |
|---|---|---|---|---|---:|
| PMT-011 | `=PMT(8%/12,10.5,10000)` | -989.2729401451879 | -989.2729401451878 | | 1 |
| PMT-012 | `=PMT(8%/12,-10,10000)` | 970.3654226924854 | 970.3654226924855 | | 1 |
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 | | 3 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 | -21530.43001872549 | 2 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | -852.5226402170998 | -852.5226402170999 | 2 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 | | 1 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 | | 1 |
| PMT-020 | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | -10.501648240558218 | | 1 |
| PMT-021 | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | -32615.986669943606 | | 1 |
| PMT-023 | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | -11260.941796175352 | | 1 |
| PMT-024 | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | -10750.782380145603 | | 2 |
| PMT-025 | `=PMT(0.065/12,60,1000,-5000,1)` | 50.90552130119933 | 50.90552130119935 | 50.90552130119934 | 3 |
| PMT-026 | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | 1.8452907580883144 | | 11 |
| PMT-027 | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | -13.963833694802405 | | 1 |
| PMT-028 | `=PMT(0.025/4,240,1000,-5000)` | 0.9737277420722876 | 0.9737277420722884 | | 7 |
| PMT-029 | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | 63.197272167756275 | 63.19727216775628 | 3 |
| PMT-030 | `=PMT(0.045/12,12,10000,-5000)` | -445.6426079318603 | -445.64260793186025 | | 1 |
| PMT-031 | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | -22603.91407630351 | | 1 |
| PMT-032 | `=PMT(0.03/12,12,250000,20000)` | -22817.29866479201 | -22817.298664792015 | | 1 |

The largest relative difference is PMT-026's, 1.3e-15. PMT-001 to 004, 008 to 010, 017, 019, 022 and 033 are the
engine's doubles; at PMT-008, 009, 010 and 019 the exact payment at the start rounded once is a unit from them.

**The search, for the record.** `Search.cs`, the third search, tried 39,602 candidates (11,242 in `search.py`
and 7,344 in the second search before it). None gives all 29 payments at a rate other than 0; the best give 16.
`--pmt-spread` gives how far each family lies from Excel at its farthest observed payment: the accurate-e
families 8 units at best, the x87's FYL2XP1 and F2XM1 10, the exact growth 10, LibreOffice's `ScGetPMT` 21,
every family that rounds 1+rate to a double some 3,000,000.

**How the branch applies it (its choices, not the user's).** The 19 cases above are `observed` with
`engineDiffersByDecision` citing the exception (and, at the start, the branch's rule there) and `excelExpect`
Excel's double. The engine computes the payment at the end in `Enclosure`s, interval arithmetic over exact dyadic
numbers, each operation rounding its bounds outward: (1+rate)^nper − 1 by (1+a)(1+b) − 1 = a + b + ab from the
rate at a whole `nper`, and as e^x − 1 of x = nper·ln(1+rate) by series that keep their relative precision
otherwise; the double is given only where both bounds round to it, with twice the bits while they do not, up to
1536. At the start that double is divided by 1+rate exactly and rounded again (question 3). The readings of PMT
are taken as the 22,118 candidates within 64 units in the last place of Excel's at every observed payment, 2,245
by their form, their way to the start, their growth's class and the precision of the rest, and LibreOffice's;
`Search.cs --refusals` runs each with a double's range, and the engine refuses wherever they part in kind, or
beyond the last digits by a magnification above 2^6. Those refusals, each asked by a case:

| Case | Formula | Where | What the readings give (`--pmt-show`) |
|---|---|---|---|
| PMT-034 | `=PMT(-1,10,1000,500)` | a rate of −1, periods above 0 | −500, that is −fv, where a form multiplies by a growth of 0 (219), and `#NUM!` (12) |
| PMT-035 | `=PMT(-2,2,1000)` | below −1, a growth of exactly 1 | `#NUM!` from every reading run (231); which Error Value Excel gives has not been observed |
| PMT-036 | `=PMT(-9,1/3,-1)` | below −1, an odd root | `#NUM!` from every reading run; −6 from an evaluation that raises 1+rate as PV and FV do (FV-017) |
| PMT-037 | `=PMT(-1,-10,1000)` | a rate of −1, periods below 0 | `#NUM!` (138) and 1000 (21) |
| PMT-038 | `=PMT(1,1100,1000)` | the growth past the range | `#NUM!` (167) and −1000 (64) |
| PMT-039 | `=PMT(1,1020,1000)` | pv·g past the range | −1000 (244) and `#NUM!` (131) |
| PMT-040 | `=PMT(0.5,1740,1000)` | as PMT-039 | −500 (244) and `#NUM!` (131) |
| PMT-041 | `=PMT(1E10,30,1)` | rate·pv·g past the range | −1E10 (317), −9999999999.999998 (2) and `#NUM!` (56) |
| PMT-042 | `=PMT(-0.99,160,1000,500)` | the growth below the normal numbers | −495 (313), −494.99999999999994 (50) and `#NUM!` (12) |
| PMT-043 | `=PMT(1E-320,0.5,1000)` | a subnormal rate | −2000 (146), `#NUM!` (11) and 0 (2) |
| PMT-044 | `=PMT(-1.64435447903749E-307,283.5,1E-05,0)` | pv·rate below the normal numbers | the exact payment (128), and one that differs from it in the 12th significant digit (31) |
| PMT-045 | `=PMT(-0.5,360,1000,500)` | a growth below 2^−52 | −250 (371) and `#NUM!` (4), a reading that takes g as 1 + (g − 1) holding it as 0 |
| PMT-046 | `=PMT(0.42,0,1000,0,1)` | no periods at the start | `#NUM!` (2,244), and from LibreOffice's reading 7.566047373982433E+18, over the remainder of a rounding |
| PMT-047 | `=PMT(0.1,1E-17,1000,0,1)` | a tiny `nper` at the start | -9.5382351702337E+19 (1,651) and two doubles beside it (593); `#NUM!` from LibreOffice's reading, whose divisor is 0 where nper+1 rounds to 1 |
| PMT-048 | `=PMT(-0.999999,1E-12,1000,0,1)` | a small `nper` at the start near −1 | -7.238234126569758E+19 (1,283) and doubles a few units from it; `#NUM!` from LibreOffice's reading |
| PMT-049 | `=PMT(0.1,2,1000,-1210)` | pv·g and fv cancel | 0 (139), 1.691768418476429E-14 (75), the exact payment −5.8154539385127246E-15 (60), and others down to −1.98E-14 |
| PMT-050 | `=PMT(0.1,1E-17,1000,-1000)` | the same at a tiny `nper` | 0 (216), −104.83672617280074 (63), −100, the exact payment (60), −102.4, −96, −88 |
| PMT-051 | `=PMT(-0.5,36.25,-150,0,0)` | a growth of 1.2E-11 taken as 1 + (g − 1) | the exact payment 9.17749001306952E-10 (200), and others agreeing to about 5 significant digits |
| PMT-052 | `=PMT(1,1000,0,1000)` | the exponent's rounding, x = 693 | the exact payment −9.332636185032189E-299 (216), and −9.332636185032829E-299 (142), 603 units away |
| PMT-053 | `=PMT(1000,2,1000,0,1)` | at the start, p − p·r/(1+r) cancels | the exact payment −999.0019960079841 (1,825), and −999.0019960080972 (366), 995 units away |

The quantities the range rule checks (`ExactPayment.Quantities`) are every product of powers of the rate, pv,
fv, pv+fv, g, g−1, 1+rate and 1+rate·type that a form of the identity computes, each between 2^−1021 and
2^1022; the rule refuses 173 of the grid's inputs where the readings agree in kind (it does not model which
reading reaches which quantity). Below −1, any other fractional `nper` is `#NUM!`, which every reading gives.

## XNPV's evaluation (the first round's question 1b), answered as far as the doubles go

The first round found 72 evaluations that gave XNPV-001 and 004, and the engine refused where the six it
computed parted. The second pass read XNPV-005, 006 and 011 to 017:

- XNPV-005 and 006, whole years: squaring (2023.0065393622904, 2277.5848519247406), where e^(x ln b) gives
  2023.0065393622895 and 2277.5848519247415, and `Math.Pow` the second of those;
- XNPV-011, 101 days: the days over 365 (−7286.877809009438), where times 1/365 gives −7286.877809009439;
- XNPV-013: the discount e^(x ln(1+rate)) with the exponential correctly rounded (700.2269770028577). At 797
  days glibc's `exp` gives 1.2313555711418422, a unit below the correctly rounded 1.2313555711418425, and the
  sum 700.2269770028581, 4 units from Excel's;
- XNPV-014 and 015: values in no order of their dates, summed in the order given;
- XNPV-007 to 010, 012 and 017: `#NUM!` at −1, −1.5, −2 and −0.5.

Of the 2,592 candidates the third search tries, 4 give all nine doubles, and they are two evaluations: summed
from the first, and the same with Excel's last-addition adjustment. **None of the 4 gives the six `#NUM!`**:
each gives numbers at −0.5, at every date the first (−7250) or at whole years below −1 (400). So the engine
computes the first evaluation, refuses where the two part (XNPV-018 asks), and checks the rate before it
evaluates: `#NUM!` below 0, which is Excel's answer at −0.5, −1, −1.5 and −2 and the engine's at any other
negative rate (XNPV-020 asks −0.01), and `#VALUE!` at exactly 0, where "below 0" and "0 or below" part, but
where the counts or a date before the first give `#NUM!` under both readings (XNPV-019 asks).
`Search.cs --refusals` finds the engine giving the two evaluations' answer at all 4,004 inputs it draws, but
the 506 at a negative rate, where it gives `#NUM!`.

**The order of XNPV's checks (the third round).** Whether Excel checks the rate before or after an Error Value
or a cell that is not a number among its ranges has not been observed, nor its counts (XNPV-003) or a date
before the first (XNPV-002) against them, nor the values against the dates. Where more than one of those fails
and they give different answers, the answer would be the order's, and XNPV now refuses with `#VALUE!`, where it
gave the first Error Value: `=XNPV(-0.5,A1:A2,B1:B2)` with `#N/A` in A1 gave `#N/A`, and checking the rate first
gives `#NUM!`. XNPV-021 to XNPV-026 ask: an Error Value at a rate below 0, text there, an Error Value at a rate of
0, beside counts that differ, beside a date before the first, and in each range. Where every check that fails
gives the same Error Value, that is the answer. The doc comment no longer says Excel gave `#NUM!` at a negative
rate "whatever the values and dates": its six cases had numbers in order. *(The fourth round: the third still
answered by an order in three places. A rate that is an Error Value returned before the ranges were read
(`=XNPV(1/0,A1:A2,B1:B2)` with `#N/A` in A1 gave `#DIV/0!`, where reading the values first gives `#N/A`); a date
before the first was seen only among the dates read before a failing one (with `#N/A` in B2 and a date before the
first in B3, it gave `#N/A`); and of two different Error Values in one range it gave the first. XNPV now reads the
rate and every cell of both ranges, takes the answer of every check that fails, and refuses where they are more
than one; XNPV-027 to XNPV-030 ask those three.)*

## PV and FV: settled

The financial power (above; `Search.cs` g22) is the only one of FV's 480 candidates that gives all 17 doubles
and all three Error Values with the platform-independent functions: g20 gives the doubles but `#NUM!` for
FV-020's `#DIV/0!`, and g23, the same power with the platform's `Math.Exp` and `Math.Log`, gives everything FV
was observed to give and is ruled out by XNPV-013, which raises 1+rate by the same power and tells the
exponentials apart. No observed double tells the logarithms apart: the engine takes the correctly rounded one
with the exponential, and FV-023 and PV-016 ask where it parts from glibc's. PV's own doubles and Error Values
leave g18, g20, g22 and g23; FV-022 rules out g18 and FV-020 g20, PV raising 1+rate as FV does (every PV double
and Error Value is the financial power's). PV-014 and PV-015 ask PV where its own observations leave those
readings, and FV-024 asks half a period before. `Search.cs --refusals` finds the engine giving g22's answer at
all 7,296 inputs of each grid, but PV's 384 at a rate of −1 with `nper` above 0, which it refuses (PV-013).

So the first round's refusals where readings parted (half a period, a negative fraction, a rate of −1 with a
negative `nper`, FV's fused sums, a fraction at a rate below −1) are all answered as Excel answered them, and
its two kept exceptions (FV-018 answered 500, FV-019's unfused sum) are settled: FV-018 is `#NUM!`, FV-019 the
unfused sum.

## Edits for the orchestrator to apply

Each old text below is whole lines of its target, with the target's indentation; each occurs once, and the
list applies in order (checked by script, 2026-10-12). They replace every edit this file proposed before; none
of those has been applied. Where an edit is unchanged from the last round, its title says so.

### 1. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The admission rule: a pointer to the exception (unchanged from the last round).

Old:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
```

New:

```
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
*(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
digits no format displays. The note of that date, "An exception for `PMT` alone", says why and how
far it goes.)*
```

### 2. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The `type` bullet of the note of 2026-10-12 (unchanged).

Old:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
```

New:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area passes of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5, −1 and 0.1 (PMT-009,
  PMT-010, PMT-019), so a fraction is not truncated or rounded, and any `type` other than 0 is payment at
  the start, in PMT, PV and FV.)*
```

### 3. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The `nper` bullet: PMT at rates of −1 and below, and where it answers.

Old:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered.
```

New:

```
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered. *(Answered by the area passes of 2026-10-12 for PV and FV. Excel's doubles are
  those of raising 1+rate to a negative `nper` as 1 over the power to its absolute value (FV-007,
  FV-022), to 0.5 by the square root (FV-016), and to any other fraction as e^(nper ln(1+rate)) with
  the exponential correctly rounded (FV-006, PV-005; XNPV-013 tells it from glibc's) and the logarithm
  correctly rounded too, which no observed double tells from glibc's (FV-023 and PV-016 ask). Excel
  gave a negative 1+rate to a fraction whose reciprocal is odd its odd root (FV-017) and to another
  fraction `#NUM!` (PV-011), and 1+rate of 0 to the power 0 `#NUM!` (FV-018) and to a negative power
  `#DIV/0!` (FV-020, FV-021, PV-012). PV and FV now answer every `nper` so, but PV at a rate of −1 with
  an `nper` above 0, which divides by a growth of 0 and is refused until Excel is asked (PV-013). PMT
  answers any `nper` at a rate above −1 where it does not refuse, under the exception for PMT below,
  whose note says what it gives and where it refuses (PMT-011, PMT-012); below −1 a fraction whose
  reciprocal is not odd is `#NUM!`, and the rest at −1 and below is refused until Excel is asked. Past
  2^53, where every double is a whole number, all three still refuse.)*
```

### 4. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

Two dated notes after the paragraph that ends the note: what the area passes answered, and the exception for PMT with how the engine applies it.

Old:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.
```

New:

```
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.

*(2026-10-12, the two area passes, `verification/2026-10-12-windows-area/` and `-area-2/`.)* **What
those cases answered.** The order of the payment's factors: Excel's doubles are the payment times
the product of 1+rate·type and ((g−1)/rate) (FV-009, PV-007, where the engine's order was one unit in
the last place below), and with a present value as well the sum as written, not a fused multiply-add
(FV-014, FV-015). PV and FV raise 1+rate by one power, the note's squaring at a whole number and the
bullet above otherwise: `spikes/exsheet-financial-evaluation/` searched 480 evaluations of FV and 480
of PV, and this power is the one that gives all 17 and 9 of their observed doubles and their Error
Values with the exponential XNPV-013 tells apart. XNPV divides each value by that power of its days
over 365 and sums from the first value: of 2,592 candidates, 4 give its nine observed doubles, two
evaluations, this one and the same with Excel's last-addition adjustment, which parts from it only
where the last addition nearly cancels and is refused there until asked. None of the four gives
XNPV's `#NUM!` at a negative rate: the engine checks the rate before it evaluates and is `#NUM!`
below 0, as Excel was at −0.5, −1, −1.5 and −2, its own answer at other negative rates until
XNPV-020 is read, and refuses a rate of 0 until asked. The engine now computes e^x and ln x correctly
rounded on every platform; the platform's `Math.Exp` is a unit in the last place from Excel's
discount at XNPV-013, and no observed double tells the logarithms apart. **PMT's evaluation is not
settled**: of 39,602 evaluations searched, none gives all 29 observed payments at a rate other than
0 and the best give 16; 19 of the 30 observed payments are 1 to 11 units in the last place from the
engine's, which PMT keeps by the exception that follows.

*(2026-10-12, decided with the user)* **An exception for `PMT` alone: its result may differ from
Excel's in digits no format displays.** Two area passes asked Excel 29 payments at a rate other than
0, and some 58,000 evaluations were searched; none gives them all. The decision was taken as the
engine giving the exact payment over Excel's doubles, rounded once, and Excel's payment differing
from the engine's at 19 of the 30 observed, by 1 to 11 units in the last place (PMT-026, a relative
error of about 1e-15), which is taken to be Excel's own rounding. `PMT` is kept with that difference.
Its corpus cases record each as a difference by this decision, with Excel's double beside the
engine's. `NPV` is not part of it, and stays undeclared (ADR-0202's note of this date). `0.1.0` lists
it under Known limitations (ADR-0202). *(2026-10-12, later, found by the financial area and put to
the user: at the start of each period the engine's payment is the payment at the end's double divided
by 1+rate and rounded again, not the exact payment rounded once; it is Excel's double at PMT-008, 009,
010 and 019, where the exact payment at the start rounded once is a unit away, and a unit from the
exact payment rounded once at PMT-014, 015, 025 and 029. At PMT-013 and PMT-026, at the end, Excel's
double and the exact payment rounded once differ in the 15th significant digit, which General shows,
and at PMT-025 the engine's double differs there from both, so that "no format displays" does not
hold there.)*

*(2026-10-12, how the engine applies the exception: the financial area's choices, not the user's;
three of them are put to the user.)* Where `PMT` answers, the payment at the end of each period is
held between two bounds, interval arithmetic over exact dyadic numbers with (1+rate)^nper − 1 raised
from the rate itself, and its double, the exact payment rounded once however small the rate, is given
only where both bounds round to it (PMT-011, PMT-012, each one unit in the last place from Excel's);
the payment at the start is that double divided by 1+rate exactly and rounded again, which is not
everywhere the exact payment at the start rounded once.
`spikes/exsheet-financial-evaluation/pmt_exact.py` checked 20,000 inputs against Python's decimal at
1200 digits. PMT's readings are taken as the evaluations within 64 units in the last place of Excel's
at every observed payment, and where they part in kind, a number against an Error Value or one Error
Value against another, or beyond the last digits, `PMT` is refused with `#VALUE!` until Excel is
asked (PMT-034 to PMT-053; `Search.cs --refusals` and `--pmt-readings` run them with a double's
range):

- at a rate of −1 with an `nper` other than 0, and below −1 with a whole `nper` or a fraction whose
  reciprocal is odd; any other fraction there is `#NUM!`, which every reading gives;
- where a quantity a reading computes passes a double's range or falls below its normal numbers, or
  the growth lies below 2^−52, which a reading that takes it as 1 + (g − 1) holds as 0;
- with no periods at the start, where LibreOffice's reading divides by a rounding's remainder;
- where a reading magnifies its own roundings more than 2^6 times, so that the readings give 0,
  numbers of the other sign or of a few significant digits, or an Error Value: where pv·g and fv
  nearly cancel, where a growth far below 1 is taken as 1 + (g − 1), where a rounding of the exponent
  moves the payment, and at the start where LibreOffice's divisor comes out as 0 or far from
  (1+rate)·((1+rate)^nper − 1), as at a tiny `nper`, or where the payment at the end less its rate's
  share cancels.

Where it answers, every reading run lies within 93 units in the last place of its payment.
```

### 5. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

A dated note on the note of 2026-10-12 (unchanged).

Old:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
```

New:

```
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
  *(2026-10-12, what the answers decided. Two passes were made, `verification/2026-10-12-windows-area/`
  and `-area-2/`. **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known
  limitations: Excel answers the same values alike typed or in a range, which leaves 18 distinct observed
  doubles, and none of the 5,796 evaluations searched gives them all; the best give 7 of 21. **`PMT` at
  small rates agrees** (PMT-017, at 10^-7). **`PMT` at ordinary rates does not**: 19 of its 30 observed
  payments are 1 to 11 units in the last place from the engine's, and none of the 39,602 evaluations
  searched gives every payment Excel gave. Decided with the user the same day: **`PMT` is kept, under
  ADR-0047's exception for `PMT` alone** (its note of this date), and its last digits are listed under
  Known limitations. **`XNPV` agrees at every observed double**: 4 of the 2,592 candidates searched give
  all nine, two evaluations that part only where a last addition nearly cancels, where the engine
  refuses; none of them gives Excel's `#NUM!` at a negative rate, which the engine gives by a check of
  the rate. `spikes/exsheet-financial-evaluation/` holds the searches.)*
```

### 6. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

Known limitations (the Consequences): PMT's last digits without "no format displays".

Old:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```

New:

```
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12: also `NPV`, refused by the note of that date above, and `PMT`'s last digits, which may
  differ from Excel's, under ADR-0047's exception of that date.)*
```

### 7. docs/definition-of-done.md

§27's note of 2026-10-11, the first bullet (unchanged; the decision's words).

Old:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
```

New:

```
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
  *(2026-10-12, decided with the user: one exception, `PMT`, whose result may differ from Excel's in
  digits no format displays, by ADR-0047's note of that date. Its cases that differ are recorded as
  differences by that decision, with Excel's double beside the engine's, and it is a known limitation.)*
```

### 8. docs/definition-of-done.md

The note's third bullet (unchanged).

Old:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations.
```

New:

```
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations. *(2026-10-12: so is `NPV`, refused
  by ADR-0202's note of that date, and so are `PMT`'s last digits, kept by ADR-0047's.)*
```

### 9. docs/definition-of-done.md

SH-7's row (unchanged; the decision's words).

Old:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047) | Layer 1 | every row of every table |
```

New:

```
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047). *(2026-10-12, decided with the user: `PMT` but in digits no format displays, ADR-0047's exception for `PMT` alone; its rows that differ are recorded as differences by that decision.)* | Layer 1 | every row of every table |
```

### 10. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The Formulas page: PMT's refusals, its payment at the start and its last digits, XNPV's order.

Old:

```
            <li>
                <b><code>PMT</code>, <code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel
                was seen to give for them. Which result that is depends on how Excel raises 1+rate to the number of
                periods. For <code>PMT</code> it rests on payments at rates of 0.5% to 10% a period. At much smaller
                rates Excel has not been asked, and its payment may differ from ExSheet's from the 13th significant
                digit. At a rate other than 0, a number of periods that is not a whole number, or is negative, is
                <code>#VALUE!</code> until Excel has been asked how it raises to one; so is a <code>type</code> that is
                not a whole number. Any other <code>type</code> but 0, 2 as well as 1, is payment at the start of each
                period, as Excel reads it.
            </li>
```

New:

```
            <li>
                <b><code>PV</code>, <code>FV</code> and <code>XNPV</code></b> give, to the last digit, every result Excel
                was seen to give for them, Error Values included. 1+rate is raised to the number of periods by
                squaring to a whole number, as 1 over the power to a negative one, by the square root at half a
                period, and otherwise as e<sup>n·ln(1+rate)</sup> with the exponential and the logarithm correctly
                rounded, which ExSheet computes the same on every platform. A negative 1+rate has an odd root, as
                <code>(-8)^(1/3)</code> does, and no other fractional power (<code>#NUM!</code>); at a rate of -1 no
                periods is <code>#NUM!</code> and a negative number of periods <code>#DIV/0!</code>, as Excel gave
                them. <code>PV</code> at a rate of -1 with periods is <code>#VALUE!</code> until Excel has
                been asked. <code>type</code> is read as a truth value: any number but 0, 0.5 and 2 as well as 1, is
                payment at the start of each period, as Excel reads it. <code>XNPV</code> counts the years as the days
                over 365 and sums from the first value. A rate below 0 is <code>#NUM!</code>, which is Excel's answer
                at every negative rate it was asked, -0.5 to -2, and ExSheet's own between -0.5 and 0 until Excel has
                been asked; a rate of exactly 0, or values whose last addition nearly cancels, are
                <code>#VALUE!</code> until Excel has been asked. An Error Value in the rate, the values or the dates is
                that Error Value, but <code>#VALUE!</code> where another check would give another answer (a rate below
                0 or of 0, counts that differ, a date before the first, a cell that is not a number, or a different
                Error Value anywhere among them), since the order Excel checks them in has not been observed.
                <b><code>PMT</code></b>, where it answers, gives at the end of each period the exact payment, rounded
                once, and at the start of each period that payment divided by 1+rate and rounded again; it reads
                <code>type</code> as <code>PV</code> and <code>FV</code> do. Excel's own payment can differ from
                ExSheet's in its last digits: of the 30 payments Excel was asked, 19 differ by one to eleven units in
                the last of a double's digits, a relative difference of at most about 1.3·10<sup>-15</sup>, which at
                three of them shows in the 15th significant digit. No evaluation found gives every payment Excel gave,
                and this is a known limitation of 0.1.0. Where the evaluations Excel's payments leave give different
                kinds of answer, or answers far apart, <code>PMT</code> is <code>#VALUE!</code> until Excel has been
                asked: at a rate of -1 or below (there a number of periods that is a fraction whose reciprocal is not
                odd is <code>#NUM!</code>), where a number it works with would pass a double's range or fall below its
                normal numbers, as in <code>=PMT(1,1020,1000)</code>, where 1+rate raised lies below
                2<sup>-52</sup>, with no periods at the start of each period, and where the evaluations would lose more
                than a few of their last digits, as where the present value grown and the future value nearly cancel
                (<code>=PMT(0.1,2,1000,-1210)</code>) or a number of periods is tiny at the start of each period.
                <b><code>NPV</code></b> is <code>#NAME?</code>, as <code>IRR</code> is: its last digits differ from
                Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
```

### 11. samples/ExGrid.Docs/Pages/RoadmapByHand.cs

The Roadmap: PMT's last digits.

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, PMT's last digits, which may differ from Excel's, and a Reference Outline, which cannot be dragged.",
```

### 12. src/ExSheet.Engine/README.md

The table of functions (unchanged).

Old:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `NPV` | `rate, value1, [value2], ...` |
| `XNPV` | `rate, values, dates` |
```

New:

```
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `XNPV` | `rate, values, dates` |
```

### 13. src/ExSheet.Engine/README.md

"Asked of Excel before they are answered": PMT's and XNPV's refusals; other areas rewrite the clauses beside it in the same lines.

Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` at a rate of -1 with an
  `nper` above 0; `PMT` at a rate of -1 with periods, below -1 with a whole `nper` or a fraction whose
  reciprocal is odd, where a quantity the evaluations Excel's payments leave compute passes a double's
  range or falls below its normal numbers, at a growth below 2^-52, with no periods at the start, and
  where one of them magnifies its own roundings more than 2^6 times; `PMT`, `PV` and `FV` with an
  `nper` past 2^53 at a rate other than 0; and `XNPV` at a rate of 0, where its last addition nearly
  cancels, and where an Error Value in its rate or ranges fails beside another check that gives
  another answer (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank
  or an
```

### 14. src/ExSheet.Engine/README.md

The financial bullet.

Old:

```
- **`PMT`, `PV` and `FV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run). `PV` and `FV` evaluate Microsoft's identity in doubles, raising 1+rate by squaring.
  `PMT` computes the payment at the end exactly and rounds it once, and the payment at the start
  divides that double by 1+rate, rounded once. For `PMT` that is one of several evaluations that fit
  the four payments observed, at rates of 0.5% to 10% a period. At smaller rates they part, from the
  13th significant digit at a rate of 10^-7, and PMT-016 to 018 ask Excel. A `type` that is a whole
  number other than 0 is payment at the start, as 1 is.
```

New:

```
- **`PV`, `FV` and `XNPV` give every double and Error Value Excel was observed to give, to the bit**
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV`
  evaluate Microsoft's identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the
  financial power: squaring to a whole `nper`, 1 over the power to a negative one, the square root at
  0.5, and e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform; Excel's exponential is the correctly rounded one at
  XNPV-013, and no observed double tells logarithms apart); a negative 1+rate has an odd root where
  1/nper is odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a
  negative one. `XNPV` divides each value by that power of its days over 365 and sums from the first;
  it checks the rate before it evaluates and is `#NUM!` below 0, Excel's answer at the rates asked
  (-0.5 to -2) and the engine's at others, and refuses an Error Value in its rate or ranges beside
  another check that would give another answer. `type` is a truth value: any number other than 0, 0.5
  included, is payment at the start. **`PMT` is kept under ADR-0047's exception for `PMT` alone**: where
  it answers, the payment at the end is the exact payment rounded once, held between two bounds that
  decide its double (`Enclosure`, interval arithmetic over exact dyadic numbers, (1+rate)^nper - 1
  raised from the rate itself), and the payment at the start that double divided by 1+rate and rounded
  again. Excel's payment differs from ExSheet's at 19 of the 30 observed, by 1 to 11 units in the last
  place, and no evaluation searched gives every one (a Known limitation of 0.1.0). Where the
  evaluations Excel's payments leave part in kind, or beyond the last digits, `PMT` refuses
  (`ExactPayment`, `Functions.Financial.cs`).
```

### 15. src/ExSheet.Engine/README.md

The functions that stay undeclared (unchanged).

Old:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. The October 3 observation run did not resolve existing Supported functions' other
`uncertain` corpus cases.
```

New:

```
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. `NPV` is undeclared too (ADR-0202, note of 2026-10-12): no evaluation in
`spikes/exsheet-financial-evaluation/` gives every double Excel gave for it. The October 3
observation run did not resolve existing Supported functions' other `uncertain` corpus cases.
```

## The Known limitations lines for `0.1.0`'s Release Notes (not an edit)

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
- `PMT`'s payment can differ from Excel's in its last digits: 19 of the 30 payments Excel was asked differ
  from ExSheet's by 1 to 11 units in the last place, a relative difference of at most about 1.3e-15, which
  at three of them shows in the 15th significant digit; and where the evaluations Excel's payments leave
  part, `PMT` is `#VALUE!` ([Formulas](exsheet/formulas#supported-functions)).
```

## Change Notes (applied in the branch; nothing for the orchestrator to apply)

This round: `run17-financial-annuity-doubles.md` says that, where `PMT` answers, its payment at the end is the
exact payment rounded once and at the start that double over 1+rate rounded again, which gives every payment
the seventeenth run read, and that Excel's differs from ExSheet's by at most about 1.3e-15, reaching the 15th
significant digit at three payments; `area-financial-type-and-periods.md` says the same of a fractional or
negative `nper`, where `PMT` does not refuse; `area-financial-refused-where-readings-part.md` lists PMT's
refusals of the third and fourth rounds and XNPV's wherever more than one of its checks fails with different
answers. The rest are as the earlier rounds left them: `area-financial-last-digits.md`, `area-financial-xnpv-rate.md`,
`area-financial-xnpv-last-digits.md`, `area-financial-npv.md`, and `run17-financial-periods-refused.md` deleted.

## Comments for tickets 04 and 05 (for the orchestrator, with the other areas' comments; not edits)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13. `PV` and `FV` give every double and Error Value Excel was observed to give: they read any
`type` as Excel does and raise 1+rate to any `nper` by the financial power, with the exponential and
the logarithm correctly rounded (`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`); PV at
a rate of -1 with an `nper` above 0 is refused until asked (PV-013). `PMT` is kept under ADR-0047's
exception for `PMT` alone (decided with the user): its result may differ from Excel's in its last
digits. Where it answers, the branch gives the exact payment rounded once at the end of each period,
and that double over 1+rate rounded again at the start (the branch's rule, which ticket 13 puts to
the user); it refuses where the evaluations its observed payments leave part in kind or beyond the
last digits, until asked (PMT-034 to 053).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area passes: `XNPV` gives every double Excel was observed to give: the days over 365,
whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value divided,
summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), one of the two evaluations that give
them. It checks the rate before it evaluates: `#NUM!` below 0, Excel's answer at -0.5 to -2 (XNPV-007
to 010, 012, 017), which no evaluation gives, and the engine's at other negative rates (XNPV-020
asks). It refuses a rate of 0, a last addition that nearly cancels, and an Error Value in its rate or
ranges beside another check that would give another answer, until Excel is asked (XNPV-018, XNPV-019,
XNPV-021 to 030; `spikes/exsheet-financial-evaluation/`).
```

Ticket 13 carries these rounds' comments itself: the decision's comment holds what the user decided, and a
comment of the third and fourth rounds what the branch applied and the three questions at the top of this file.

## Found on the way, for the orchestrator

- **Excel's exponential is not glibc's.** At XNPV-013's 797 days glibc's `exp` (which .NET's `Math.Exp` calls
  on Linux) rounds the other way from the correctly rounded value, and Excel's double is the correctly
  rounded one's. That is the one argument observed where they part; no observed double tells logarithms
  apart. The engine's `^` operator, `EXP`, `LN`, `LOG`, `POWER` and the hyperbolic functions still take the
  platform's `Math.Exp` and `Math.Log`, which differ between platforms and from the correctly rounded ones at
  about 1 argument in 2,000 (18 of the 40,010 checked). `CorrectlyRounded` is in the engine; whether the
  arithmetic area takes it is that area's question (ARITH-098's and ARITH-134's doubles are the correctly
  rounded functions' too). PV's and FV's power and the `^` operator now differ at such arguments.
- **The order of a domain check and an Error Value in a range is unobserved.** `XNPV` now reads its rate and
  every cell of both ranges and refuses where the order would decide the answer (XNPV-021 to 030). The same
  question stands for every function with a check of an argument beside a range; outside XNPV it is left as it
  was.
- **PMT's tests and texts used a double-double that was not exact.** The previous round's `ExpWide` and
  `LogWide` lost the rate below about 1e-15 and at subnormal parts; they are removed, and PMT computes in
  `Enclosure`s. `CorrectlyRounded.Exp` and `.Log` (PV, FV and XNPV) round their double-double with a bound and
  fall back to 256-bit fixed point, and are unchanged.
- **`Search.cs` hung in Debug** after the second pass: FV-017's rate of −9 made Goldberg's log1p NaN, and
  reading a NaN's bits as a dyadic number fed the fixed-point `exp` an argument of 2^1000. `X.Of` now refuses a
  non-finite double. The spike's runs are `-c Release` (half a minute; Debug took over 15 minutes).
- **Excel gives a financial Formula's General cell its currency format on entry** (the second pass read
  `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV and FV cell). Following it is a cell-format question for
  another ticket.
- The first round's `oracle.ps1` proposal (G17 for Excel's doubles) is applied (`afd70b15`).
