# Row Heading drag stopped responding in a repeated Server run

Status: ready-for-agent
Roadmap: internal

Observed on `6c4b5ede` in related-input run
[39509384816, job 118927649614](https://github.com/AkiraMisawa/ex-grid/actions/runs/39509384816/job/118927649614):
`headings.spec.mjs` DC-43, Chrome Server, repetition 7 of 10. The test requires a Row Heading
drag at the bottom edge to scroll down past 200 px and never sideways. That assertion stays
in the normal suite and the related-input gate; it has not been skipped or relaxed.

The first reads returned scrollTop 0, then a locator read stopped returning. Trace screenshots
also stopped, and the diagnostic `page.evaluate` never returned before teardown exceeded five
minutes. The core journal contains the initial press as received, started, finished and
answered. The last screenshot shows a growing whole-row Selection and rows displaced below
blank space. This establishes a browser response failure during the drag, not that the grid
completed the drag with a final scrollTop of zero.

The retained CI artifact is `hunt-server-chrome-7`, artifact ID 12304559996. Its trace is
`headings-DC-43-a-Row-Headi-1a259-lls-down-and-never-sideways-chrome-repeat7/trace.zip`.
It provides the evidence when the Input Journal could no longer be read.

No cause or fix is claimed here. The existing failing test is the regression signal.
Three isolated local repetitions passed on Server Chrome with a 150 ms round-trip floor and
fourfold CPU throttling on 2026-10-21; they did not reproduce the stopped renderer. The next
related gate remains pending. The separate field
text and secondary-press fixes do not claim to resolve this observation.

## Second observation, 2026-10-21

The same test stopped responding on `430cc823`, Edge Server, repetition 1 (the second run),
in [39521512192, job 118967489081](https://github.com/AkiraMisawa/ex-grid/actions/runs/39521512192/job/118967489081).
The shard ran all 140 expected tests: 139 passed and this one failed. Its artifact is
`hunt-server-msedge-8`, ID 12312736240, with trace
`headings-DC-43-a-Row-Headi-1a259-lls-down-and-never-sideways-msedge-repeat1/trace.zip`.

Here the vertical-scroll assertion already passed: the trace read scrollTop above 200 px.
After the edge-band observation interval, the next locator read stopped returning. Trace
screenshots stopped too, and teardown's diagnostic evaluation also hung. The last screenshot
shows whole rows selected, a `41R x 16384C` Size Tip and blank space above the painted rows.
This distinguishes the response failure from an assertion that scrolling never started.

Three isolated repetitions on the integration of main `bdea73f4` (`f9a283df`), Chrome Server,
150 ms RTT floor and fourfold CPU throttling, passed. They do not establish a fix. Investigation
now includes the preceding heading gestures in the shared document, as CI ran them.

The whole heading file then passed ten local repetitions with those widened settings (140
executions), and ten without extra latency or CPU throttling (140 executions). Neither run
reproduced the stop. PR #228's release fix concerns presses on the surrounding Sheet controls;
this trace stops before mouse-up inside the grid, so that change is not an established fix.

Temporary diagnostics now wrap only this test. Its original inputs, assertions and timeout
remain. If it has not completed after ten seconds, a CDP debugger pause records JavaScript
frames; on Linux, process state and native stacks are requested only for renderers returned
by this browser's process list. The probe is disarmed when the test finishes. The wrapped
gesture passed three local repetitions on Chrome Server. These diagnostics are evidence
collection for the next CI run, not a product fix or a replacement completion condition.
