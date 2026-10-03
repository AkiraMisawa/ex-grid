# Excel evidence for implementing the Observe functions, LET and random functions

**All 24 Observe functions and LET, RAND and RANDBETWEEN were exercised in real Windows Excel.**
There are **1,180 distinct COM cases**, **97 independent repeat cases**, and **14 LET cases entered
with real keys**. Every input-type/value audit and repeat comparison passed; no committed run has
a probe failure or a blocked keyboard case. Invalid entries and returned Excel Error Values are
observations, not failed probes. The result is evidence for implementation, not an admission of
these functions into ExSheet's supported set.

Baseline: `origin/main`, `c3ddb7df5476e550763659760d49736d879c9296`. Excel is **16.0.20430.20092,
64-bit**, on Windows, with **en-GB** regional format and English Excel UI. Regional format stayed
en-GB. Excel's 1900 date system was used. Exact environment, input hashes, script hashes and run
times are retained in the result files. [README.md](README.md) explains reproduction and limits;
[case-index.md](case-index.md) indexes every observation, including the typed cases.

## Evidence and verification

| Run | Cases | Returned result | Refused entry | Probe failures |
|---|---:|---:|---:|---:|
| Main, `results.json` | 990 | 983 | 7 | 0 |
| Follow-up, `followup-results.json` | 124 | 121 | 3 | 0 |
| Additional boundaries, `additional-results.json` | 66 | 61 | 5 | 0 |
| Independent repeats, `repeat-results.json` | 97 | 91 | 6 | 0 |
| Real keys, `keyboard-results.json` | 14 | 7 | 7 | 0 |

The repeats include every Observe function, deterministic LET cases, decimal rounding boundaries,
and both successful and failing guesses for multiple-root financial inputs. All repeated numeric
results matched **bit for bit**, and all other repeated results and refusals matched. A separate
40-case SUMIF pilot compared individually populated fresh workbooks with unchanged fixture
copies; both the fixture readback and all output states matched. Both pilot records are retained.

`python3 summarize.py` validates scope against the baseline catalogue, all explicit fixture Values
and their kinds, round-trip numeric strings against IEEE 754 bits, random bounds/integer results,
automatic/manual recalculation and dependent values, independent repeats, and the typed-input
Oracle/wrapper/source hashes. It generates the per-case index. A runtime exception aborts a probe;
there are no hidden runtime exceptions treated as observations.

The main case COUNTIFS-035 has an inaccurate original question label: its actual Formula has only
one criteria range and is a baseline, not a shape mismatch. The generated index corrects that label.
The actual length mismatch is COUNTIFS-ADDITIONAL-001; the same-count/different-shape mismatch is
COUNTIFS-FOLLOWUP-011. Neither is inferred from the baseline.

## Criteria: SUMIF, SUMIFS, COUNTIF, COUNTIFS, AVERAGEIF, AVERAGEIFS, MAXIFS, MINIFS

One criteria parser is appropriate, as the catalogue already says, but it cannot be a generic
comparison followed by generic Error Value propagation. The isolated and mixed fixtures establish:

- **Blank, formula-empty text, zero, and a blank criterion are different.** In the main mixed
  fixture, COUNTIF with `"="` counts only the absent cell (1), while `""` counts that cell and
  the Formula `=""` (2). Numeric criterion 0 counts the numeric zero only (1). With the follow-up
  fixture's blank D1 as criterion, numeric zero and text `"0"` match (2); a formula-empty D2
  matches the blank and formula-empty cells (2). SUMIF's weighted results show which rows matched,
  rather than merely a count that could hide a different membership (SUMIF-001/003/004,
  COUNTIF-001/003/004, and FOLLOWUP-001/002).
- **Equality and inequality are not complements over numeric text.** An A1 stored as text `"1"`
  matches both `"=1"` and `"<>1"`. An A1 stored as the number 1 matches only `"=1"`.
  COUNTIF-ADDITIONAL-001/002/005/006 and the matching SUMIF cases pin this explicitly.
- **Text matching is case-insensitive in the observed ASCII cases.** `"alpha"` matches `alpha`
  and `ALPHA`. Wildcards apply to text cells, including numeric text; `*` counts the formula-empty
  text but not the absent cell. The main `?` cases include a one-character numeric string and a space.
- **Tilde behaviour needs its own tests.** `a~*b` matches literal `a*b`, and `a~?b` matches
  literal `a?b`. In the follow-up, `a~b` matches literal `a~b`, but `a~~b` and `a~~~b` match none;
  trailing `a~` matches literal `a~`. Do not import another function's or a regex library's escape
  rules without these cases (each family's FOLLOWUP-005 through FOLLOWUP-010).
- **An Error Value can be a criterion.** With A1 holding `=NA()`, A2 holding `=1/0`, and A3
  holding literal text `#N/A`, both criterion `NA()` and criterion `"#N/A"` match **A1 only**.
  COUNTIF/COUNTIFS return 1; the six weighted aggregates return A1's result, 10. The text A3 is
  not matched. An Error Value in a matched result range is returned; an unmatched result error is
  ignored. These are in each family's ADDITIONAL cases and SUMIF/AVERAGEIF-FOLLOWUP-012.
- **Result-range coercion is separate from matching.** For matched inputs, blank, formula-empty
  text, numeric text `"2"`, and TRUE in the result range are ignored; a matched `#N/A` is returned.
  Main SUMIF-036 through 040 and the corresponding aggregates pin those inputs.
- **The one-condition and multiple-condition functions differ on range shape.** SUMIF and
  AVERAGEIF with A1:A4 and an explicit B1:B2 result range read the result footprint starting at B1
  with the criteria range's size: the matching fourth row returns B4's 40. The IFS functions return
  `#VALUE!` for mismatched lengths and for 4×1 versus 2×2 even though the cell count is equal.
  The latter is in the follow-up shape cases; COUNTIFS's length case is ADDITIONAL-001.
- **No-match outcomes differ by aggregate.** Counts, sums, MAXIFS and MINIFS return 0; the average
  functions return `#DIV/0!` (the FOLLOWUP-004 cases). Multiple criteria are ANDed.

Under this en-GB Excel, a criteria string such as `">1,5"` is not interchangeable with `">1.5"`:
main SUMIF-021 returns 550, whereas SUMIF-020 returns 0. That is an observation in this culture,
not proof of any other culture's reading. The catalogue already permits refusing culture-sensitive
criteria with `#VALUE!`; an implementation must not guess a decimal parsing rule here.

**Implementation use:** transcribe the weighted fixtures as well as the isolated cells, make criteria
matching separate from aggregation, and explicitly dispatch blank/text/number/boolean/Error Value
inputs. Preserve the catalogue's culture-sensitive refusals where Excel's reading is not pinned.

## COUNTBLANK

COUNTBLANK counts a cleared cell and a Formula returning `""`, one each. Zero, space text,
numeric text, booleans and errors do not count; A1:A18 of the main fixture returns 2
(COUNTBLANK-001 through COUNTBLANK-019). Do not reuse the criteria parser's `"="` test as its blank test.

## VLOOKUP, HLOOKUP and MATCH

The key matrix covers ascending duplicates, descending duplicates, unsorted keys, blanks, numbers
and numeric text, errors, ASCII case, wildcards, mode coercion and invalid/fractional indices.

With keys **1,2,2,3** and return values **10,20,30,blank**:

| Operation | Observed answer | Evidence |
|---|---|---|
| VLOOKUP or HLOOKUP, key 2, exact | 20: first duplicate | VLOOKUP/HLOOKUP-001 |
| VLOOKUP or HLOOKUP, key 2, approximate | 30: last duplicate | VLOOKUP/HLOOKUP-002 |
| MATCH, key 2, mode 0 | 2: first duplicate | MATCH-001 |
| MATCH, key 2, mode 1 | 3: last duplicate | MATCH-002 |
| MATCH on descending 3,2,2,1, mode −1 | 2: first equal key in that order | MATCH-018 |

Omitting the mode gives the approximate/default behaviour in the follow-up cases. Returning a
blank target gives numeric 0. Exact numeric-text key `"2"` is not the numeric key 2. Exact `"*"`
is a wildcard; the observed ASCII exact matches ignore case. Fractional and invalid row/column
indices and modes have their own main cases; use their recorded results rather than a shared
coercion rule assumed from XLOOKUP.

Excel gives results on some unsorted data that look plausible. **ExSheet's catalogue already
requires refusing unsorted approximate searches with `#VALUE!`.** These observations do not
revoke that refusal. Implement the sorted-data check before returning an approximate result; the
unsorted outputs are evidence of the danger, not the desired ExSheet contract.

## MROUND, CEILING.MATH and FLOOR.MATH

Observed halfway outputs include MROUND(6.05,0.1) = **6**, MROUND(7.05,0.1) =
**7.1000000000000005**, MROUND(0.15,0.1) = **0.2**, and MROUND(0.35,0.1) = **0.4**
(MROUND-001 through 004). A decimal implementation or one uniform binary `round(n/m)*m` rule
must first reproduce these outputs. The recorded bits distinguish display-equivalent results.

MROUND(-2.5,1) and MROUND(2.5,-1) give `#NUM!`; MROUND(-2.5,-1) gives −3. With a zero
significance these three functions return 0 in the observed cases, including nonzero numbers.
CEILING.MATH(-2.5,1) gives −2 and mode 1 gives −3; FLOOR.MATH gives −3 and mode 1 gives −2.
Significance's sign is ignored by the latter two in these cases. MROUND(TRUE,1) is `#VALUE!`,
whereas CEILING.MATH(TRUE,1) and FLOOR.MATH(TRUE,1) are 1. Text numbers are accepted in the
observed scalar tests. All of these have individual rows, including near-integer doubles.

**Implementation use:** the 97-case repeat includes all four decimal boundaries for all three
functions. Treat sign/mode rules, type coercion and binary rounding as separate behaviours.
Microsoft also documents unresolved midpoint behaviour for decimal multiples in
[MROUND](https://support.microsoft.com/en-us/excel/functions/mround-function); the measured table
is necessary rather than a claim that a convenient rounding primitive implements Excel.

## SEARCH, UPPER, LOWER and PROPER

The Unicode corpus makes these materially different from an invariant runtime casing call:

- UPPER of `straße` is `STRAßE`, not `STRASSE`; `ß` stays `ß`, and `ẞ` stays `ẞ`.
  LOWER of `ẞ` also leaves `ẞ` unchanged (UPPER-002/004/005, LOWER-005).
- LOWER of standalone `Σ` is `ς`; of `ΣΣ` it is `σς`; of `ΣΑ` it is `σα`.
  The extra Greek-context inputs are LOWER-ADDITIONAL-001 through 007.
- LOWER of `İ` is `i`; UPPER of `i I ı İ` is `I I I İ`. Supplementary-plane Deseret
  characters are unchanged in the tested casing calls. PROPER's punctuation/digit example is
  `Hello-World 42Foo O'Neill`, and its other Unicode cases remain explicit observations.
- SEARCH does not equate `SS` with `ß`, or the tested `i` with dotted/dotless variants;
  it does equate the tested sigma forms and accented upper/lower letters. SEARCH of supplementary
  Deseret lower in upper gives `#VALUE!` (SEARCH-001 through 007).
- SEARCH(`a?b`,`a😀b`) is `#VALUE!`, while `a*b` matches at 1. `?` alone matches at 1 in
  `😀`, but starting at 2 returns `#VALUE!` in this build (SEARCH-008/009/015/016).
  Do not infer one Unicode character-count rule for every search operation from a regex engine.
- An empty needle is found at the valid start position; invalid starts give `#VALUE!` and start
  1.9 is truncated to 1. Numeric, boolean, blank and Error Value coercions are separately recorded.

**Implementation use:** these fixtures are enough to disprove broad runtime-casing substitutions,
not enough to derive all of Excel's Unicode tables. A candidate implementation must be compared
against these records and then broadened over the Unicode domain it intends to answer. Refuse
unmatched domains under ADR-0047 instead of silently using a different mapping.

## YEARFRAC and DATEDIF

YEARFRAC has a matrix of all five bases across leap days, month ends, year boundaries, reverse
order, fractional serials and the fictitious 1900 leap day. For 2020-02-29 to 2021-02-28,
bases 0–4 return respectively **1**, **0.99726775956284153**, **1.0138888888888888**, **1**,
and **0.99722222222222223** (YEARFRAC-011 through 015). The 59-to-61 and 61-to-59 matrices
are symmetric in YEARFRAC but differ between bases (YEARFRAC-036 through 045). Fractional dates
are truncated in the observed 45000.9-to-45001.1 cases. Basis coercion, invalid dates and date
text under en-GB have individual rows.

For the same 2020-02-29 to 2021-02-28 interval, DATEDIF gives Y=0, M=11, D=365, YM=11,
YD=365, MD=30. A reversed interval returns `#NUM!`, rather than YEARFRAC's positive answer.
Unit spelling, invalid dates, booleans, fractions and the 1900 serials are recorded separately.
**MD was measured for comparison but remains refused by the catalogue's existing decision.**

**Implementation use:** day-count bases need independent algorithms and leap/month-end tables;
a generic elapsed-days divisor or calendar-month subtraction is insufficient. There is evidence
for the admission tests, not a claim that the entire calendar domain has been exhausted.

## IRR, XIRR and RATE

The financial matrix covers omitted and explicit guesses, negative and out-of-domain guesses,
multiple roots, no sign change, zero flows, text/boolean/blank/error inputs, differing date shapes,
duplicate/reversed/fractional/invalid dates and type/period coercions.

For **−100,230,−132**, IRR's output changes with the guess:

| Guess | Exact recorded result | Evidence |
|---|---|---|
| omitted or 0.1 | 0.10000000000000009 | IRR-013/014 |
| −0.9 | 0.10000000000000098 | IRR-015 |
| 0.5 | 0.19999999999999951 | IRR-016 |
| −1 | #VALUE! | IRR-017 |
| 2 | 0.20000000000000306 | IRR-018 |

All six were independently repeated and matched, including bits and the Error Value. RATE with
12 periods, −100 payments and 1000 present value gives **0.02922854076913774** by default,
**0.02922854076913305** with guess 2, and `#NUM!` with guess −0.9 (RATE-001/006/005).
Type 2 behaves as type 1 in RATE-004, despite the documented signature naming only 0 and 1.
RATE(12,0,1000,−1000) gives **8.4459342326353958E-14**, not exact mathematical zero.
IRR ignores the tested numeric-text/blank cash flow and fails when that removes the only positive
number; XIRR accepts the tested numeric text and returns a result (IRR/XIRR-037 onwards).

The same nominal multiple-root cash flows with Jan 1 dates in 2020, 2021 and 2022 produced
`#NUM!` in all six XIRR guesses, while the single-root inputs had successful results. This is
retained as an acceptance case, not replaced by a mathematically expected answer.

Microsoft describes stopping tolerances and iteration caps for
[IRR](https://support.microsoft.com/en-us/excel/functions/irr-function),
[XIRR](https://support.microsoft.com/en-us/excel/functions/xirr-function) and
[RATE](https://support.microsoft.com/en-us/excel/functions/rate-function): respectively 20, 100
and 20 attempts. These pages do not specify the complete recurrence and initialization needed
to reproduce these last digits and failure cases.

**Implementation use:** these functions still need a solver that reproduces Excel's chosen root,
stopping result and failures to the catalogue's 15-significant-digit criterion. A correct root
from a general Newton/bisection solver does not establish that. Start from the measured matrix,
compare the candidate solver, and explicitly refuse argument domains it cannot reproduce.
This run does not infer an Excel solver algorithm from the finite observations.

## LET: scope and evaluation, with a separate grammar check by keys

Measured results cover sequential bindings, case-insensitive lookup, nested shadowing and outer
scope, unresolved forward/self names, errors not used by the result, blanks, booleans, text,
range and computed-array bindings, dependencies after cell edits, repeated random binding uses,
and the name/pair-count boundaries.

- `LET(x,2,LET(x,3,x)+x)` and `LET(x,2,LET(y,x+1,y)+x)` both return 5; a binding does
  not escape its own LET (`LET(x,1,1)+x` gives `#NAME?`). A forward binding and `LET(x,x+1,x)`
  give `#NAME?`, not a circular cell-reference result (LET-004/005/007/008, FOLLOWUP-005).
- `LET(x,NA(),1)` returns 1; `LET(x,1/0,IF(FALSE,x,7))` returns 7. This establishes that
  unused error bindings do not poison the result; it does **not** establish a full lazy-evaluation
  algorithm for all binding expressions. The former was also entered with real keys.
- A range binding is still usable by SUM and spills in arithmetic; a SEQUENCE binding spills.
  Changing A1 updates `LET(x,A1,x*2)` from 6 to 8, and changing A2 updates a bound range's SUM
  from 6 to 9 (FOLLOWUP-001/012). The existing array/dependency work needs to flow through bindings.
- `LET(x,RAND(),x=x)` is TRUE and `LET(x,RAND(),x-x)` is 0 across all 16 sampled recalculations
  of each case. Reuse a binding's Value within one calculation; do not re-run its expression on
  each name access. This agrees with [Microsoft's LET description](https://support.microsoft.com/en-us/excel/functions/let-function).
- 126 name/value pairs are accepted and 127 are refused (LET-029/030), as documented.
  The tested all-`x` names of length 248 and 249 are accepted through COM, but lengths 250–256
  are refused; 250 and 255 are also refused by typed entry. This is a measured bound for these
  inputs, not a deduction about every kind of name.

**The keyboard check is essential:**

| Input | Formula2 | Real typed input | Evidence |
|---|---|---|---|
| `LET(A1,2,A1)` | Returns 2 | Refuses entry with a dialog | LET-023, LET-KEYS-004 |
| `LET(c,2,c)` and `LET(r,2,r)` | Return 2 | Return 2 | LET-021/022, LET-KEYS-002/003 |
| `LET(a.b,2,a.b)` | Refuses | Refuses | LET-024, LET-KEYS-005 |
| `LET(_x,2,_x)` | Returns 2 | Returns 2 | LET-025, LET-KEYS-006 |
| Duplicate `x` binding | Refuses | Refuses; duplicate-name dialog | LET-006, LET-KEYS-007 |
| Japanese `名前` binding | Returns 3 | Returns 3 | FOLLOWUP-006, LET-KEYS-010 |
| Missing final result / even argument count | Refuses | Refuses | LET-026/028, LET-KEYS-011/012 |

Microsoft's page says a name such as `c` conflicts with reference syntax; this build accepts both
`c` and `r` by real keys. Record the discrepancy rather than imposing the documentation's example
on the actual keyboard answer. Conversely, A1 is rejected by the keyboard even though COM
accepts it. ExSheet's entry grammar must use the typed answer for this case.

**Decision still needed:** an ADR admitting names scoped to a Formula, with sequential bindings,
case-insensitive lookup and nested shadowing, valid-name/arity entry rules, memoized Value reuse,
and integration with References, dependency extraction and arrays. Workbook-level names are a
separate feature and are not demonstrated by this LET research. The baseline evaluator currently
answers NameNode with `#NAME?`; adding a declaration alone cannot implement lexical binding.
No ADR or glossary change is made by this report.

## RAND and RANDBETWEEN

RAND's 128 observed recalculations all returned numbers in [0,1). Separate RAND calls in a
Formula did not compare equal in the 16 samples, while the LET-bound call was reused exactly.
This is consistent with [Microsoft's RAND contract](https://support.microsoft.com/en-us/excel/functions/rand-function);
it does not identify Excel's random generator or prove distribution/independence from a sample.

For RAND, RANDBETWEEN and a LET holding RAND, an unrelated edit in automatic mode changed the
random Value and its dependent `=Z1*2` coherently. In manual mode, an unrelated edit left both
unchanged; an explicit Calculate changed them. Every dependent Value was exactly twice the Value
in its recorded calculation. Random functions therefore need the volatile recalculation mechanism
of ADR-0124 and one completed recalculation's Values, not a component's render-time random call.

RANDBETWEEN's small integer samples include both endpoints. It rejects TRUE and invalid text
with `#VALUE!`, accepts the tested numeric string, treats a blank Reference as 0, propagates the
tested Error Values, and returns `#NUM!` when bottom is greater than top. It accepts the sampled
bounds at ±1E16; any implementation must account for what doubles can represent there.

**Fractional bounds are particularly unsafe to guess:**

| Arguments | All recorded outputs for these cases |
|---|---|
| −1.9, 1.9 | −1, 0, 1 appeared |
| 1.1, 1.9 | 2 |
| −1.9, −1.1 | −1 |
| 0.1, 0.9 | 1 |
| −0.9, −0.1 | 0 |
| −1.9, −1.9 | −1 |
| 1.9, 1.9 | 2 |

The result can exceed the original fractional top. These are RANDBETWEEN-004/005/006 and the
FOLLOWUP cases, not a transcription of another platform's RandBetween implementation.
Microsoft's [RANDBETWEEN page](https://support.microsoft.com/en-gb/excel/functions/randbetween-function)
describes integer bounds and recalculation; this measured table pins the additional edge cases.

**Decision still needed, together:** how random results coexist with the engine's deterministic
outcome requirement and the Sheet Document's Entries-only persistence. The user needs to decide
whether a Consumer supplies a reproducible random source/state, whether that state and a
recalculation identity are persisted, and what restoring/reopening/recalculating promises. Distinct
calls must get their own draws while repeated reads of a LET binding share one draw. A source
whose result depends on unspecified traversal order would reintroduce the timing/order problem.
Excel's behaviour does not decide that application contract. ADR-0124 explicitly leaves RAND and
RANDBETWEEN for their own decision; this report preserves that boundary.

## Suggested implementation sequence

1. Transcribe the P1 criteria and lookup fixtures into the engine's normal corpus, using typed
   expectations and the keyboard answer for entry refusal; implement the shared criteria matcher
   and the sorted-data lookup cases. Preserve existing refusals in the catalogue.
2. Implement COUNTBLANK, the other criteria aggregates, and the three rounding functions against
   the recorded tables. Test binary boundaries and coercions separately.
3. Implement date bases/units and the chosen text domain against the Unicode/date matrices,
   refusing domains whose Excel behaviour is not yet reproduced.
4. Validate financial solver candidates against the exact recorded outputs and failed guesses
   before admitting them. More algorithm/domain work remains even though their observation run
   is complete.
5. Decide LET and both random functions in ADRs using the questions above, then implement them.
   These decisions can now use the measured keyboard, binding, spill and volatility behaviour.

The existing supported functions' `uncertain` cases are a separate Oracle backlog. This run's
scope is the requested Observe functions plus the three named Decide functions; no existing
catalogue status or case-source label was silently changed.
