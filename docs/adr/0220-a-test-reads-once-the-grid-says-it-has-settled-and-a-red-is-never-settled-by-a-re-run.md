# A test reads once the grid says it has settled, and a red is never settled by a re-run

Decided with the user, 2026-10-16. **Not yet built**: this records the contract and the rules. The
signal's exact conditions, its attribute, and the CI jobs are settled when they are built, by the
rules below.

## Why

On 2026-10-15 and 2026-10-16 three layer-3 tests failed now and then on `main`. Each had been
written under ADR-0056's rules, and each passed the three runs a changed spec gets before it merges.

- **SH-2** (`sheet.spec.mjs`) counted the Sheet's elements straight after a press, before the
  press's own render was in the DOM. The product was right; the test read too early (#121).
- **SH-52** (`sheet-toolbar.spec.mjs`) typed F10, H, Escape, H, A at once. About one run in forty
  on the Server host, a key held for the toolbar was overtaken by one typed straight on it, and
  an edit opened in the cell. The test was right; the product had a race (#122).
- **DC-55/SH-35** (`pointing-scope.spec.mjs`) read `scrollTop` between the two frames of the
  repaint the grid makes after a reveal. The product was right; the test read too early (#123).

They were found one at a time, because a failure that comes one run in forty passes three runs
about 93% of the time, and a red that passed on a re-run was merged. The user's question was why
such failures keep coming back.

## The ideal

**A test passes every run when the library handles its input correctly, and fails when the
library does not.** A re-run then never changes an outcome, and a red always names a defect,
either in the product or in where the test reads. There are two ways to fall short, and each has
its own remedy:

| | Falls short when | Remedy |
|---|---|---|
| A correct library fails | The test reads before the grid has finished with its input | The grid says when it has settled (1), and the test reads only then (2) |
| A racy library passes | The window the race needs is narrow | Inputs at machine speed (2), and the windows widened on purpose (5) |

## Decision

### 1. The grid says when it has settled

The grid's root carries a mark of its own that says **every input it has taken has been handled
and its effect is in the DOM**:

- no key is held by the gate, and no hand-off of the keyboard is under way;
- no render is owed for an input already taken;
- a repaint the grid makes after a reveal has finished.

The mark is the grid's, not a test's. A Consumer's automation (Playwright, or any driver) can wait
on it too, as the spine's sixth principle expects of tools that drive the grid at machine speed.
It is **not `aria-busy`**, which a screen reader interprets.

**What it may cost.** The mark changes at most once per burst of input, on the root alone. No row
learns it, so rows still skip their render (ADR-0003). Nothing per cell reaches JavaScript, and it
adds no round trip of its own on the Server host (ADR-0027's P1–P9). It is derived from what the key
gate and the core already know, never measured from the page.

**Measured before its default is chosen** (`spikes/render-bench`, a mode of its own: typing
continuously, scrolling, and over the Server host's round trip):

- if the measurement shows no difference, the mark is **always on**;
- if it shows one, the mark is **opt-in**, and layer 3 turns it on.

Whether the JavaScript side needs an entry of its own in ADR-0021's allowlist is settled when it is
built. If it only writes an attribute from the key gate's existing capture-phase `keydown` handling,
it lies within that use; otherwise ADR-0021 gains an entry.

### 2. How a test drives and reads

- **Inputs go in at machine speed. A test never waits between inputs to make itself pass.** A wait
  between two keys hides the race a user typing quickly, or a script, meets. A test that is about
  slow input says so in its name.
- **A result is read only once the grid has settled**: the mark of (1) once it is built, and until
  then ADR-0056's rules (the expected state, then `circuitQuiet()` on the Server host). A fixed wait
  never stands in for either.
- **A failure is one of two kinds, and each is fixed where it lies.** A product race is fixed in the
  product, never by a wait in the test. A reading taken too early is fixed where the test reads,
  never by a wait between inputs.

### 3. A red is never settled by a re-run (A)

A layer-3 failure that passes when the job is run again is a defect, not noise.

- **While one is unresolved, the only pull requests that merge are those that fix such failures.**
  Other work waits. *(The rule as decided with the user on 2026-10-16. A fix for one may merge while
  another is still open, so a fix is never held hostage to an unrelated failure.)*
- "Passed on a re-run" is never counted as green, for any pull request.
- Each such failure is recorded, with the run that showed it, until the change that fixes it merges.

### 4. A daily hunt (B)

Once a day, CI runs the whole of layer 3 with each test repeated (`--repeat-each`, ten to start
with), on both hosts and in both browsers. It gates nothing. Every failure it finds enters (3)'s
list, so failures one run in forty are found before they meet a pull request.

### 5. Windows widened on purpose (D)

The daily run, or a job of its own, also runs layer 3 with the latency proxy's round trip raised and
the CPU throttled. A race that needs a narrow window then fails nearly every run, instead of one in
forty. The round trips and the throttle are chosen when the job is built, from the failures of
(4) that they make reliable.

### 6. A lint for the specs (E)

A check, run with layer 2 in CI, refuses a spec that:

- waits for a fixed time (`waitForTimeout`, or a sleep of its own);
- reads the DOM, a scroll offset or a picture straight after an input, with no awaited
  expectation, `circuitQuiet()` or, once built, the settled mark between them.

A line the lint cannot judge is allowed by a comment that names why. It does not judge what a test
asserts.

## Rejected

- **More repeats before a merge.** Running a changed spec twenty times instead of three lets a
  one-in-forty failure through about 60% of the time instead of 93%. That costs CI minutes on every
  push and still lets most through. (4) and (5) find the same failures once, ahead of time.
- **A test-only hook in place of the mark.** It would cost nothing in production, but the grid
  under test would then differ from the one Consumers run, and their own automation would have
  nothing to wait on.
- **`aria-busy`.** A screen reader reads it, so a testing meaning would reach users.

## Consequences

- ADR-0056's rules stand. (2) adds the rule for inputs, and the mark of (1) replaces
  `circuitQuiet()` where it applies once built.
- Under (3), feature work (the eighteenth run's corrections among it) waits while #121, #122 and
  #123's failures are open; those three are the fixes.
- CI's minutes grow by the daily run. Nothing that gates a pull request grows.
- The Definition of Done gains a criterion for the mark when it is built (its conditions, and that
  rows still skip their render), as every built feature does.
