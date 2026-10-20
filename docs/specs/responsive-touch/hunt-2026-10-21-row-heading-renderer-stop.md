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
