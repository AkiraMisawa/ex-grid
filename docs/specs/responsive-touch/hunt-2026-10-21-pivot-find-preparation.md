# Pivot Find starts after its initial Window is drawn

PR #187 revision `c241c0a3` failed
[CI 39646250405, job 119382494441](https://github.com/AkiraMisawa/ex-grid/actions/runs/39646250405/job/119382494441)
in `FindTests.ADR0332_rows_beyond_the_window_are_searched`. `PressAsync` could not find
an `ExGrid<PivotDisplayRow>` component when sending Ctrl+F immediately after `RenderPivot`.
The preceding negative assertion, that R0450 was not painted, also passed when the
initial render contained no rows at all. Report computation can yield before that
first Window is drawn. The same test arrived from main's ExPivot Find implementation;
this is separate from the unresolved Heading renderer stop.

The preparation now positively checks the first row, `R0000 | 0`, before checking
that R0450 is outside the Window and before the first input. It uses the same
render-driven `WaitForAssertion` boundary as the adjacent 12,000-row Find case.
There is no fixed delay, larger timeout or wait between search inputs. The forward
match at `(450, 0)` and backward match at `(599, 0)` remain unchanged. A report that
never renders now fails the preparation instead of passing an empty negative check.

The ExPivot component suite passed all 367 cases after the correction. Static review
found no weakened assertion or new timing dependency. This changes test preparation
only; no Consumer behavior, Docs Site example or Change Note changes are needed.

The corrected PR #187 tree also passed all nineteen logic/component suites:
15,279 passed, 11 skipped, zero failures. CI remains the verification of its pushed revision.
