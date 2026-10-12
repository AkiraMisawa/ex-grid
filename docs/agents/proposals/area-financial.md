# Area: financial — proposals after the area pass of 2026-10-12

Branch `claude/area-financial`. The cases: PMT-009, 011 to 016 and 018, FV-006, 007 and 009, PV-005 and
007, NPV-001, 002 and 005 to 008. The evidence: `verification/2026-10-12-windows-area/` and the second
search in `spikes/exsheet-financial-evaluation/` (`Search.cs`, `search-cs.json`, `discriminate.txt`,
`discriminate-xnpv.txt`; the README says what each candidate family reproduces, and `Search.cs --refusals`
checks the engine against where the evaluations that survive part).

What the branch did, in brief:

- **Followed**: `type` is a truth value (PMT-009). PV and FV answer an `nper` that is not a whole number,
  or is negative, where every evaluation Excel's doubles leave gives the same double (FV-006, FV-007,
  PV-005; section 2). PV and FV group the payment as Excel does (FV-009, PV-007). XNPV discounts by
  e^(x ln(1+rate)), which gives XNPV-004 (found by the search; it had agreed only at 6 decimals).
- **Refused by decision**: PMT with an `nper` that is not a whole number from 0 (PMT-011, PMT-012).
  `NPV` is undeclared, as ADR-0202's note of 2026-10-12 decides when no evaluation fits (NPV-001 to 008).
- **Refused where the evaluations Excel's doubles leave part, until Excel is asked** (ADR-0047, ADR-0202):
  PV and FV at half a period (FV-016), at a negative fraction (FV-022), and at a rate of -1 with a
  negative `nper` (FV-020, FV-021, PV-012); FV with both a present value and a payment (FV-014, FV-015),
  but at the end of each period with a whole `nper` from 0, which keeps the answer it gave before the
  pass (FV-019 asks); XNPV at whole years and at the days where /365 and *(1/365) part (XNPV-005, 006,
  011). Kept from before the pass though the evaluations agree there: PV and FV with an `nper` that is
  not a whole number at a rate below -1 (FV-017, PV-011), and XNPV at a rate of -1 or below (XNPV-007 to
  010). Kept from before though they part: no periods at a rate of -1 (FV-018 asks).
- **Stopped, for the user**: PMT's last digits (PMT-013 to 016 and 018), section 1, and XNPV's evaluation,
  section 1b: 72 evaluations give its two observed doubles, and they part at about half the inputs drawn.

Each edit below is exact: the old text, then the new. Line breaks are as the files have them.

---

## 1. The question for the user: PMT's last digits

**What was found.** Excel's PMT-013 to 016 and 018 differ from the engine's by one to three units in
the last place:

| Case | Formula | Excel | Engine |
|---|---|---|---|
| PMT-013 | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | -7363.703123976373 |
| PMT-014 | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | -21530.430018725492 |
| PMT-015 | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | -852.5226402170998 |
| PMT-016 | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | -20936.48778866115 |
| PMT-018 | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | -27.90167156122446 |

Excel's PMT-001 to 003, 008 to 010 and 017 (a rate of 10^-7) are the engine's: the exact payment over
Excel's doubles, rounded once. No evaluation searched gives all 14 observed payments:

- `search.py`: 11,242 candidates, 0 give all 14, the best give 10. The 2504 that gave the first four
  all fail: none survives PMT-009 to 018 (`--spread` on PMT-013 and 018: "every survivor: none").
- `Search.cs`: 7,344 candidates, 0 give all 14, the best give 10. No candidate of either search gives
  PMT-013 except some of `search.py`'s excess forms, which miss others.
- The nearest: e = expm1(n log1p(rate)) correctly rounded and the rest in doubles (forms M5, M16), 10 of
  14, two units off PMT-012, 013 and 016 and one off PMT-017.

ADR-0202's rule is "corrected before the tag, or the arguments where it disagrees are refused with an
Error Value". The disagreements are at ordinary arguments (monthly rates with a future value or with
payment at the start), so no domain of arguments can be refused without refusing PMT where users use
it. The options:

1. **Ask more, then search again before the tag** (recommended if a pass can be made). PMT-019 to 033 are
   in `pmt.json`, `uncertain` with the engine's double. PMT-020 to 033 are the inputs that split the 92
   nearest candidates into 45 groups (`discriminate.txt` lists which gives what). One
   `oracle.ps1 -Area pmt` call asks them through COM in a minute. The search reruns on every observed
   double.
2. **Refuse PMT for 0.1.0**, `#NAME?` as `NPV` and `IRR` are, and list it under Known limitations. This
   follows ADR-0202 as NPV followed it, but PMT is the most used of the four.
3. **Keep PMT and say so under Known limitations**: "PMT may differ from Excel's in the last of its
   seventeen digits". It relaxes ADR-0047's admission rule for one function, which is an ADR change
   (ADR-0202 considered and rejected the like for IRR: "a financial function that answers close to Excel
   is a plausible wrong number").

If 1 cannot be made before the tag, 2 is what ADR-0202 says. The branch keeps PMT as it is: the user
decides.

## 1b. The question for the user: XNPV's evaluation

**What was found.** Excel's XNPV-001 and XNPV-004 are the two observed XNPV doubles. `search.py` tried 12
evaluations and found two that give both; `Search.cs` now tries 1,496 and finds **72**: the days over 365
or times 1/365; the discount by e^(x ln(1+rate)), at whole years by squaring or by `Math.Pow`, by a
correctly rounded power, by the x87's, or as 1 over e^(-x ln(1+rate)); each value divided by it or
multiplied by its reciprocal; summed forward, forward with a fused multiply-add or Excel's last-addition
adjustment, or pairwise by halving; terms at 53 or 64 bits. The engine computes six of them (the days
either way, e^(x ln(1+rate)) or at whole years squaring or `Math.Pow`, divided and summed forward) and
refuses where those six part: at whole years (XNPV-005, 006) and at the days where /365 and *(1/365)
part (XNPV-011). The 72 part at 1,923 of the 4,004 inputs `Search.cs --refusals` draws; the engine answers
at 1,668 of those. A correctly rounded e^(x ln b) also parts from the platform's `Math.Exp` and `Math.Log`
at one of XNPV-012 to 017, so the platform's library is among the questions.

ADR-0047 admits an answer only where it follows from observed answers by a rule they settle, and where
the evaluations they leave part, the engine refuses. Applied to all 72, XNPV refuses about half of
ordinary inputs, which is ADR-0202's question as NPV's was. The options:

1. **Ask, then search again before the tag** (recommended if the pass can be made). XNPV-012 to 017 are in
   `xnpv.json`, `uncertain` with the engine's answer: `--discriminate-xnpv`'s inputs, which split the 72
   into 38 groups (`discriminate-xnpv.txt` lists which gives what), each of evaluations that agree on
   every input drawn. They go with PMT-019 to 033 and NPV-009 to 022 in the next `oracle.ps1 -Area` pass.
2. **Refuse XNPV for 0.1.0**, `#NAME?` as `NPV` and `IRR` are, and list it under Known limitations, as
   ADR-0202's note did for NPV.
3. **Refuse with `#VALUE!` wherever the 72 part.** Computable, but the engine would carry 64-bit,
   correctly rounded and x87 evaluations to know where, and it refuses about half of ordinary XNPVs: 2 in
   effect, with a less legible failure.
4. **Keep XNPV as it is and say so under Known limitations.** This relaxes ADR-0047's admission rule for
   one function, which ADR-0202 rejected for IRR.

The branch keeps XNPV's six and refuses where they part; the user decides.

## 2. The fractional power: every reading the observations leave, refused where they part

PV and FV raise 1+rate to a whole `nper` from 0 by squaring and to a negative whole one as 1 over that:
one reading each, which every observed double settles (squaring 1/(1+rate) is 19 units in the last place
from FV-007). For any other `nper`, the observed doubles, FV-006's and PV-005's 10.5, leave more than one
reading, each of which gives every observed PV and FV double (`Search.cs` g0 and g18 to g21; `search.py`'s
`pv-fv` is g18):

- e^(nper ln(1+rate)) by the platform's `exp` and `log` (g0), or by a correctly rounded pair (g21, which
  parts from g0 at no input `--refusals` draws);
- the `^` operator's own evaluation, `Arithmetic.Power` (g18): that, but the correctly rounded square root
  at exactly 0.5 (ARITH-133);
- a negative fraction as 1 over either of those to its absolute value, as FV-007's -10 is reached (g19,
  g20).

So `=(1+r)^n` and the same power inside FV can differ: at half a period, where FV's reading may not be
the operator's, and at a rate of -1, where the `^` operator gives `=0^0` as `#NUM!` (ARITH-041) and 0 to a
negative power as `#DIV/0!` (ARITH-042), while squaring gives 0^0 as 1 and 1 over 0^n no finite number
(`#NUM!`). PV and FV now compute every reading, and FV every form that survives (section 3c), and answer
only where all give the same double; where they part, PV and FV are `#VALUE!` until Excel is asked:

- half a period (FV-016: 45.627983043166275 by the square root, 45.62798304316633 by e^(0.5 ln(1+rate)));
  PV-010 is answered, the two giving the same double there;
- a negative fraction where the power and 1 over it part (FV-022: -247.828794185625 and
  -247.8287941856272); FV-013 is answered, the two agreeing at -10.5;
- a negative `nper` at a rate of -1 (FV-020, FV-021, PV-012).

Two exceptions keep what the engine did before the pass, and are asked. No periods at a rate of -1 is
answered by squaring, 0^0 as 1, where the `^` reading gives `#NUM!` (FV-018). At a rate below -1, a
fractional `nper` stays `#VALUE!`: where 1/nper is an odd whole number the `^` operator gives a negative
base an odd root (Excel's `=(-8)^(1/3)` is -1.9999999999999998, ARITH-043) and e^(nper ln(1+rate)) none,
so they part; elsewhere neither has an answer, and the refusal stands only because no run has observed
what FV and PV give there (FV-017, PV-011 ask). A whole `nper` there is raised by squaring, which does not
depend on the sign.

`Search.cs --refusals` runs every survivor over 7,296 inputs a function and the engine through its public
`Sheet`: the engine gives their answer wherever they agree and `#VALUE!` wherever they part, at every
input but those exceptions and FV's at the end (section 3c), which it counts.

The engine's `exp` and `log` are the platform's, as its `^`, `EXP`, `LN`, `LOG` and hyperbolic functions
are. A correctly rounded pair gives the same PV and FV doubles at every input drawn, but parts at one of
XNPV-012 to 017 (section 1b). If a Windows run shows .NET on Windows differing from Excel, the remedy is
the trigonometry's: compute both portably.

## 3. ADR-0047, the note of 2026-10-12 "PV and FV give Excel's double, and PMT every double Excel was seen to give"

### 3a. The `type` bullet

Old:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
```

New:

```
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area pass of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5 and for -1 (PMT-009,
  PMT-010), so 0.5 is not truncated to 0, and any `type` other than 0 is payment at the start, in PMT,
  PV and FV.)*
```

### 3b. The `nper` bullet

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
  `nper` is answered. *(Answered by the area pass of 2026-10-12 for PV and FV: Excel raises 1+rate to a
  negative whole number as 1 over the power by squaring (FV-007; squaring 1/(1+rate) is 19 units in the
  last place away), and to 10.5 as e^(nper ln(1+rate)) by the platform's `exp` and `log` (FV-006,
  PV-005; a correctly rounded power is two units from PV-005). That leaves more than one reading of
  other numbers: the `^` operator's square root at exactly 0.5 (ARITH-133), a negative fraction as 1
  over the power to its absolute value, as -10 is reached, and at a rate of -1 the `^` operator's
  `#DIV/0!` for 0 to a negative power (ARITH-042). PV and FV now compute each reading and answer where
  all give the same double, and are `#VALUE!` where they part (FV-016, FV-020 to 022 and PV-012 ask);
  they stay `#VALUE!`, as before, at a rate below -1 with an `nper` that is not a whole number (FV-017,
  PV-011). PMT still refuses an `nper` that is not a whole number from 0: Excel answers PMT-011 and
  PMT-012, but no evaluation searched gives those payments with every other payment Excel gave. Past
  2^53, where every double is a whole number, all three still refuse.)*
```

### 3c. A dated note after the paragraph that ends the note

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

*(2026-10-12, the area pass, `verification/2026-10-12-windows-area/`.)* **What those cases answered.**
The order of the payment's factors: Excel multiplies the payment by the product of 1+rate·type and
((g−1)/rate), and FV-009 and PV-007 are that evaluation's doubles to the bit, where the engine's order
was one unit in the last place below; the engine now takes it. Three evaluations with that grouping give
every observed FV double, the sum as it is and two that fuse one of its products into a multiply-add,
and they part where FV has both a present value and a payment: FV is `#VALUE!` there (FV-014 and 015
ask), but at the end of each period with a whole `nper` from 0, where it keeps the sum it gave before
(FV-019 asks). PMT's evaluation is not settled: Excel's PMT-017, at 10^-7, is
the exact model's double, but its PMT-013 to 016 and 018 are one to three units in the last place from
it, and none of the 18,586 evaluations the two searches tried (`spikes/exsheet-financial-evaluation/`)
gives all 14 of Excel's payments. The engine keeps the exact model until the user decides, under
ADR-0202, whether PMT is corrected or refused; PMT-019 to 033 ask Excel more. And XNPV, which the
search covered: XNPV-004, asked to tell a correctly rounded power from e^(x ln(1+rate)), is the second
summed forward, to the bit, where `Math.Pow` so summed was 8 units away. The corpus had compared XNPV at
6 decimals, so the pass counted it as agreeing; XNPV now takes e^(x ln(1+rate)) and its cases compare
every bit. But 72 evaluations give both observed XNPV doubles, and they part at about half the inputs
drawn: XNPV is `#VALUE!` where the six it computes part (whole years, XNPV-005 and 006; the days over 365
or times 1/365, XNPV-011), and whether it answers where the others part is decided with the user under
ADR-0202 (XNPV-012 to 017 ask). At a rate of -1 or below, where 1+rate has no logarithm and `Math.Pow`
gave a number for some arguments, XNPV is refused with `#VALUE!` until Excel is asked (XNPV-007 to 010).
```

*(If the user takes option 2 of section 1, the sentence "The engine keeps the exact model ... ask Excel
more." becomes: "`PMT` is undeclared for 0.1.0, `#NAME?` as `NPV` and `IRR` are (ADR-0202).")*

## 4. ADR-0202

### 4a. A dated note on the note of 2026-10-12 (on `main`)

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
  *(2026-10-12, what the answers decided. The pass was made, `verification/2026-10-12-windows-area/`.
  **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known limitations: Excel's
  NPV-005 to 008 answer the same values alike typed or in a range, which leaves three distinct observed
  doubles, and none of the 4,633 evaluations searched gives all three. **`PMT` at small rates agrees**:
  PMT-017, at 10^-7, is the engine's double. **`PMT` at ordinary rates does not**: PMT-013 to 016 and
  018 are one to three units in the last place from the engine's, and none of the 18,586 evaluations
  searched gives every payment Excel gave. Correcting it is not possible on what is known, and its
  disagreements lie where it is used, so no domain of arguments can be refused; whether `PMT` is
  refused, or asked more before the tag, is decided with the user. **`XNPV` agrees at both observed
  doubles, but they do not settle it**: 72 of the 1,496 evaluations searched give both, and they part at
  about half the inputs drawn; `XNPV` refuses where the six it computes part, and whether it is refused
  for `0.1.0`, or asked more before the tag, is decided with the user too.
  `spikes/exsheet-financial-evaluation/` holds both searches.)*
```

*(The sentences on `PMT`'s and `XNPV`'s questions are replaced by the user's decisions when they are made.)*

### 4b. Known limitations (the Consequences)

Old:

```
- `0.1.0`'s Known limitations will hold at least: `IRR`, `XIRR` and `RATE`; dragging a Reference Outline;
  ExPivot's speed near its cap; and whatever of ExPivot's layout and wording the run finds different.
```

New:

```
- `0.1.0`'s Known limitations will hold at least: `IRR`, `XIRR` and `RATE`; dragging a Reference Outline;
  ExPivot's speed near its cap; and whatever of ExPivot's layout and wording the run finds different.
  *(2026-10-12: also `NPV`, refused by the note of that date above.)*
```

### 4c. The Known limitations line for `0.1.0`'s Release Notes

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
```

## 5. The Docs Site (`samples/ExGrid.Docs`, not edited by this branch)

### 5a. The Formulas page, `Pages/Sheet/SheetFormulas.razor`

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
                <b><code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel was seen to
                give for them. Which result that is depends on how Excel raises 1+rate to the number of periods: by
                squaring to a whole number, and 1 over that to a negative one. Excel's results leave more than one
                way of raising it to any other number, and <code>PV</code> and <code>FV</code> answer where every
                way gives the same result. At half a period, at some negative numbers that are not whole, and at a
                negative number at a rate of -1 they differ, and the result is <code>#VALUE!</code> until Excel has
                been asked, as it is for a number that is not whole at a rate below -1. <code>FV</code> with both a
                present value and a payment is <code>#VALUE!</code> where two evaluations Excel's results leave
                differ in the last digit, but at the end of each period with a whole number of periods.
                <code>type</code> is read as a truth value: any number but 0, 0.5 and 2 as well as 1, is payment at
                the start of each period, as Excel reads it.
                <b><code>XNPV</code></b> discounts each value by e<sup>years·ln(1+rate)</sup>, which gives the two
                results Excel was asked for. Where the dates are whole years apart, and at some numbers of days,
                other ways of discounting give a different last digit, and <code>XNPV</code> is <code>#VALUE!</code>
                there, as it is at a rate of -1 or below, until Excel has been asked. Other evaluations give Excel's
                two results too, so at other dates its last digit may still differ from Excel's.
                <b><code>PMT</code></b> reads <code>type</code> the same way, and gives Excel's payment at the rates
                asked, from 10<sup>-7</sup> to 10% a period, but for a few monthly and daily payments whose last
                digit differs from Excel's by one to three units. At a rate other than 0, a number of periods that
                is not a whole number, or is negative, is <code>#VALUE!</code> in <code>PMT</code>, though Excel
                answers it. <b><code>NPV</code></b> is <code>#NAME?</code>, as <code>IRR</code> is: its last digits
                differ from Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
```

*(If the user takes option 2 of section 1, the `PMT` sentences become: "<b><code>PMT</code></b> is
<code>#NAME?</code> for now, as <code>NPV</code> is: some of its payments differ from Excel's in their last
digit, and no evaluation found gives every payment Excel was seen to give." If the user takes option 2 of
section 1b, the `XNPV` sentences become: "<b><code>XNPV</code></b> is <code>#NAME?</code> for now, as
<code>NPV</code> is: the two results Excel was asked for do not tell which of several evaluations it takes,
and they give different last digits at about half of other dates.")*

### 5b. The Roadmap, `Pages/RoadmapByHand.cs`

Old:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

New:

```
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
```

The Functions page needs no edit: it reads the catalogue, where `NPV` is now Blocked under ticket 13, so
it lists `NPV` among the functions that are `#NAME?` with ticket 13's Roadmap entry (whose `Roadmap:`
line the branch updated).

## 6. The engine's README (`src/ExSheet.Engine/README.md`, shared)

### 6a. The table of functions

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

### 6b. "Asked of Excel before they are answered"

Old:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
```

New:

```
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PV` and `FV` with an `nper`
  that is not a whole number at a rate below -1, or where the readings of a fractional or negative
  `nper` that Excel's doubles leave part (half a period, a negative fraction, a negative `nper` at a
  rate of -1), `FV` with a present value and a payment where a fused multiply-add parts from the sum
  (but at the end of each period with a whole `nper`), `PMT`, `PV` and `FV` with an `nper` past 2^53 at
  a rate other than 0, and `XNPV` at a rate of -1 or below, at whole years and at the days where /365
  and *(1/365) part (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a
  blank or an
```

*(Other areas may rewrite the same bullet for `EOMONTH`, `EDATE`, `SMALL` and `LARGE`; only the `PMT`,
`PV`, `FV` and `XNPV` clause is this area's.)*

### 6c. The financial bullet

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
- **`PV`, `FV` and `XNPV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run and the area pass of 2026-10-12), and where the evaluations those doubles leave part,
  `#VALUE!`. `PV` and `FV` evaluate Microsoft's identity in doubles, the payment times
  (1+rate·type)·((g−1)/rate), raising 1+rate by squaring to a whole `nper` and 1 over that to a negative
  one. Any other `nper` has several readings: e^(nper ln(1+rate)) by the platform's `exp` and `log`, the
  `^` operator's square root at exactly 0.5, and a negative fraction as 1 over the power to its absolute
  value. `FV` with a present value and a payment has three forms, the sum and two fused multiply-adds.
  Both compute every reading and form and answer where all agree (but `FV` at the end of each period
  with a whole `nper`, which keeps the sum until Excel is asked), and an `nper` that is not a whole
  number at a rate below -1 is `#VALUE!` until Excel is asked. `XNPV` discounts each value by
  e^(years ln(1+rate)), with squaring and `Math.Pow` as readings at whole years and the days over 365 or
  times 1/365, and is `#VALUE!` where those part, and at a rate of -1 or below, until Excel is asked; 72
  evaluations give its two observed doubles, and whether it answers where the others part is the
  user's question (ADR-0202). `type` is a truth value: any number other than 0, 0.5 included, is
  payment at the start. **`PMT` is not settled**: it computes the payment at the
  end exactly and rounds it once, and the payment at the start divides that double by 1+rate, rounded
  once. That gives Excel's PMT-001 to 003, 008 to 010 and 017 (a rate of 10^-7), but PMT-013 to 016
  and 018 differ from Excel's by one to three units in the last place, and no evaluation searched gives
  all (ADR-0202: the user decides). At a rate other than 0, an `nper` that is not a whole number from 0
  is `#VALUE!` in `PMT`, though Excel answers it.
```

### 6d. The functions that stay undeclared

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

## 7. Change Notes this branch's change makes false (written by the run 17 branch, not this area's)

`docs/releases/unreleased/run17-financial-periods-refused.md`, old:

```
At a rate other than 0, `PMT`, `PV` and `FV` with a number of periods that is not a whole number, or is negative, are now `#VALUE!` where they gave a number, until Excel has been asked how it raises 1+rate to one; at a rate of 0 every number of periods is still answered. A Formula that relied on a fractional `nper` shows `#VALUE!`.
```

New:

```
At a rate other than 0, `PMT` with a number of periods that is not a whole number, or is negative, is now `#VALUE!` where it gave a number, though Excel answers it. `PV` and `FV` are now `#VALUE!` until Excel has been asked at half a period and at some negative numbers of periods that are not whole, where they gave a number, and at a negative number of periods at a rate of -1, or one that is not whole at a rate below -1, where they were `#NUM!`; at a rate of 0 every number of periods is still answered. A Formula that relied on such an `nper` shows `#VALUE!`.
```

`docs/releases/unreleased/run17-financial-annuity-doubles.md`, old:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that rests on payments at rates of 0.5% to 10% a period; at much smaller rates Excel has not been asked, and its payment may differ from the 13th significant digit. A `type` that is a whole number other than 0, such as 2, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

New:

```
`PMT`, `PV` and `FV` now give, to the last digit, every result the seventeenth Windows run read from Excel for them, where they differed in the 13th to 15th significant digit: `PV` and `FV` raise 1+rate by squaring, and `PMT` computes the payment exactly before rounding it once. For `PMT` that holds at the rates Excel was asked, from 10^-7 to 10% a period, but for a few monthly and daily payments whose last digit differs from Excel's (see the notes of the area pass). A `type` other than 0, such as 2 or 0.5, is payment at the start of each period, as Excel reads it, where it was `#VALUE!`.
```

## 8. Tickets 04 and 05 (applied by the orchestrator with the other areas' comments)

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/04-p2-ready-functions.md`:

```
2026-10-12, the area pass: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13 with `PMT`'s last digits. `PV` and `FV` read any `type` as Excel does, and answer an `nper`
that is not a whole number, or is negative, where every reading Excel's doubles leave gives the same
double; where they part (half a period, a negative fraction, a negative `nper` at a rate of -1, and
`FV` with a present value and a payment but at the end with a whole `nper`), and at a rate below -1
with a fractional `nper`, they are refused until Excel is asked (FV-014 to 022, PV-010 to 012)
(`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`).
```

A pointer under `## Comments` of `docs/specs/exsheet-functions/issues/05-p3-ready-functions.md`:

```
2026-10-12, the area pass: `XNPV` discounts by e^(years ln(1+rate)), which gives Excel's XNPV-004 to
the bit where `Math.Pow` was 8 units in the last place away, and its cases compare every bit. It is
`#VALUE!` where the six evaluations it computes part (whole years, XNPV-005 and 006; the days over 365
or times 1/365, XNPV-011), and at a rate of -1 or below (XNPV-007 to 010), where 1+rate has no logarithm
and `Math.Pow` gave a number for some arguments. 72 evaluations give both observed doubles and part at
about half the inputs drawn; whether XNPV answers there is the user's question, with ticket 13
(XNPV-012 to 017 ask; `spikes/exsheet-financial-evaluation/`).
```

## 9. Found on the way, for the orchestrator

- **XNPV disagreed, hidden by `"digits": 6`.** The branch follows Excel in its own commit (`634ad2f8`),
  which touches `xnpv.json`, outside this area's files: two expectations and two new cases. Drop the
  commit if XNPV is to stay as it was.
- **`oracle.ps1` writes Excel's double with `ToString('R')`.** Section 10 proposes `G17`.
- **Excel gives a financial Formula's General cell its currency format on entry.** The area pass read
  `$#,##0.00_);[Red]($#,##0.00)` on every PMT, PV, FV and NPV cell; ExSheet gives none. The catalogue's
  PMT note said "until that is observed" and now says it was. Following it is a cell-format question
  for another ticket.
- **Python 3.12 and later sum floats with compensation** (`sum()`), so a spike that sums doubles with
  `sum()` models no evaluation Excel performs. Neither search uses it for a modelled sum.
- **`Search.cs` now references `src/ExSheet.Engine`** (`#:project`), for `--refusals`, which checks the
  engine through its public `Sheet`; running the spike builds the engine. `search.py --vectors` takes one
  reading of PV and FV and no longer models the engine where it refuses; `--refusals` replaces it there.
- **A correctly rounded `exp` and `log` part from glibc's** at one of XNPV-012 to 017, though at no PV or
  FV input drawn: the platform's library is a question XNPV's next pass also asks (section 2's last
  paragraph).

## 10. `oracle.ps1`: write Excel's doubles with `"G17"`, not `"R"` (for the orchestrator)

**The finding.** `oracle.ps1` records each number Excel answers as `$v.ToString('R', $Invariant)`.
Windows PowerShell 5.1 runs on .NET Framework, where Microsoft documents that `"R"` does not always
round-trip a `Double` (on 64-bit .NET Framework, some values formatted with `"R"` parse back as a
neighbouring double) and recommends `"G17"` instead; `"R"` was only made to round-trip in .NET Core
3.0. So a string the oracle wrote with 15 or fewer significant digits may name a neighbour of Excel's
double, and the corpus, the area searches and this proposal read those strings as Excel's. Of this
area's, PMT-015 (`-852.5226402171`) and NPV-003 (`5.56724267468069`) are so written; neither string
lies near enough to a midpoint for the failure to apply, so neither changes a conclusion here. The
corpus's other short ones are unchecked.

What is not affected: the oracle's agree or disagree compares the double in memory with the corpus's
expectation parsed on the same .NET Framework (`$expected -eq [double]$Answer.value2`), so the written
string plays no part in a case's status. (Whether .NET Framework parses every 17-digit expectation to
its nearest double is a separate question, unchecked here.) What is at risk is what is written:
`number`, and `value2`, which `ConvertTo-Json` formats on Windows PowerShell 5.1 by its own means, so the
string to trust is `number` once it is `"G17"`.

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, old:

```
    elseif ($v -is [double]) { $answer.value2 = $v; $answer.kind = 'number'; $answer.number = $v.ToString('R', $Invariant) }
```

New:

```
    elseif ($v -is [double]) { $answer.value2 = $v; $answer.kind = 'number'; $answer.number = $v.ToString('G17', $Invariant) }
```

`"G17"` always writes 17 significant digits, so `-852.5226402171` would read `-852.52264021710005`: the
same double, written so that any parser gets it back. The next `oracle.ps1 -Area` pass (PMT-019 to 033,
NPV-009 to 022, FV-011 to 022, PV-008 to 012, XNPV-005 to 017) should run with it, and an importer that
takes Excel's double from a result file should read `number`.
