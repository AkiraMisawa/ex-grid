# A rendered value is not a new field input

Status: ready-for-agent
Roadmap: internal

Normal CI on `2025c846`, run
[39513904379, job 118942969353](https://github.com/AkiraMisawa/ex-grid/actions/runs/39513904379/job/118942969353),
failed `declarations.spec.mjs` ED-22/ED-29 under both Chromes on Server. Typing `=A1+` in the
Cell Editor, immediately pressing the Formula Bar and typing `B1` should leave `=A1+B1`.
The bar instead ended at `=` under Built-in and `=B1` under MudBlazor. Both original cases
reproduced locally on Server Chrome at a 150 ms round-trip floor and fourfold CPU throttling.

## Cause

The previous field-revision fix assigned a new revision when reading a field whose value had
changed, including the first read of another surface. That conflated a later reading with
later typing. The newly focused Formula Bar could still show `=` from an older render;
its acknowledgement adopted that value and overwrote the cell's `=A1+`.

Revisions now originate only in the existing native input listener. A read carries a matching
input event's revision, or explicitly says it is a rendered value. A rendered value cannot
replace the core's newer text. A caret in matching text still applies, and the persistent
Formula Bar's input belongs to the edit where it was typed. New typing back to the same value
still gets a fresh revision. No listener, interop message, timer or command queue is added.

## Regression and verification

`PointModeTests.ADR0240_a_bar_press_cannot_adopt_its_older_render_as_new_typing` reads the
Consumer's committed value at the real interop boundary. Before the fix, the rendered-value
case committed `=` instead of `=A1+`; its fresh-typing companion committed the complete text.
After the fix, all 39 Point Mode component cases pass. The browser regression is the original
unchanged two-case test; no input wait or expected result was relaxed.

The selected repeat scope omitted `declarations.spec.mjs`, even though it covers Formula Bar
transfers and F4. The next related-input gate adds that entire spec to the existing twelve.
This corrects a coverage omission; the previous green subsets did not establish correctness
of all field-transfer paths. The original failures and adjacent field, Point, row, heading and
menu cases now pass 45 executions on Chrome Server, three repetitions at a 150 ms round-trip
floor and fourfold CPU throttling. The solution builds with no warnings or errors. All sixteen
logic/component suites pass: 14,502 passed, 11 skipped. The Docs Site's Formula example was
exercised for Point and cell-to-bar transfer under Built-in and MudBlazor, with no browser
warning or exception. Standards and Spec reviews found no actionable findings. The replacement
CI gate remains pending.

The original normal CI run has completed. Chrome and Edge on Server both failed these same
two transfer cases; the other normal jobs passed (the Server aggregate reports their failure).
The twelve-spec related run `39513932686` passed, but omitted this spec and therefore did not
establish that field transfer worked. The next gate retains those twelve and adds `declarations`.

## Upstream comparison, 2026-10-21

Fetched main `16847110` and inspected the five main commits since this branch's last
integration, plus the other open PRs #218 and #219. Main's #212 adds Input Order tracking for
Sheet switches; #219 keeps an edit open while another Sheet is shown and changes the Formula
Bar's display for that case. Neither changes the field-revision bridge or the adoption of
carried field text responsible for this reproduction. #218 adds Board Inputs and shaped grids.
No equivalent fix was found in those diffs. This is source comparison, not a claim that the
combined branches have been tested; main integration still requires verification.
