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

## Column Heading observation in the focused diagnostic

The pinned Server diagnostic [39588334803](https://github.com/AkiraMisawa/ex-grid/actions/runs/39588334803)
used product revision `9a4ed03a`, Chrome 155.0.8059.84 and Edge 155.0.4283.62. Edge passed all
1,400 executions (14 tests, 100 repetitions). Chrome passed 1,399, but the Column Heading
right-edge drag failed on repetition 5 (the sixth run), job 119182726414. The test's two first
scroll readings returned zero; its next locator read never returned. The 10-second assertion
expired, and teardown's Input Journal read then stopped for five minutes. The final screenshot
shows an eight-column whole-column Selection. This is another stopped-renderer observation,
not a final scroll-position reading after a completed gesture.

The temporary probe covered only the Row Heading test, so this Column Heading failure has no
native stack. The Row Heading repetitions and the Edge pass do not establish a fix. The new
trace is `headings-DC-43-a-Column-He-c78b9-lls-sideways-and-never-down-chrome-repeat5/trace.zip`
in `heading-native-chrome` from that run.

A further isolated diagnostic, [39593331016](https://github.com/AkiraMisawa/ex-grid/actions/runs/39593331016),
wraps both edge drags. It leaves the debugger disabled during the original gesture, requests
stacks after three seconds, and awaits and retains a started native capture even if the
assertion times out first. One job retains the full heading sequence; another selects only
the two edge drags to seek a smaller reproduction. Each stops at its first failure to collect
evidence promptly. The unchanged assertions remain the verdict; this is not a completion gate.
The wrapped gestures passed six local Chrome Server executions (three each), and the capture
and cleanup delta was reviewed without actionable findings. Its code is isolated on diagnostic
branch `c7944285`; it does not change the product PR's test behavior.

An auxiliary local run used a headed Linux ARM64 Chromium 151.0.7922.34 container against
this worktree's macOS Server host (`fcd295ba` integrated). Both original edge gestures passed
100 repetitions each (200 executions); the watchdog did not fire. This is additional
non-reproduction evidence, not the CI browser/version or a verification substitute. The
container and the dedicated local host/proxy were stopped after the run.

The full-sequence job in 39593331016 passed all 1,400 executions, but its setup log revealed
Chrome **155.0.8059.39**, whereas the reproduced Column Heading failure used **155.0.8059.84**.
The edge-only job used .39 too; it was stopped to collect its partial record and align the
browser version. This difference is evidence about the environment, not proof of a browser
regression. The full-sequence artifact contains no fired watchdog record.

Diagnostic [39597224314](https://github.com/AkiraMisawa/ex-grid/actions/runs/39597224314), isolated
branch `74f99f90`, installs and checks the exact official Chrome .84 package before either run.
It keeps the full-sequence and edge-only scopes and stops at the first failure. Its 60-second
diagnostic test bound shortens evidence collection; the product PR's assertions and timeouts
are unchanged, and this run is not a completion gate. The native capture is independent of CDP.
A disposable JavaScript busy-loop check showed that enabling the debugger after a stop cannot
reliably retrieve JavaScript frames; native stacks are therefore the next evidence to read.
Official .84 debug symbols are available for decoding those native addresses.

A subsequent probe removes all setup before the gesture and all watchdog inspection during
the original assertion. An `afterEach` hook gathers the browser's current renderer PIDs and
native stacks only after an edge-drag test has already failed. This avoids changing the
input timing and avoids a native debugger creating the failure it is meant to explain. The
original gesture bodies are byte-for-byte unchanged. Six local Chrome Server executions
passed; the diagnostic delta was reviewed, and the stale import found in review was removed.
This remains isolated diagnostic code, not a product fix.

The fixed-.84 full-sequence job (119212430493 in 39597224314) completed 1,400
executions with no failure in 23 minutes. That non-reproduction does not close the defect.
The after-failure-only probe is isolated at `1e5bca80`, run
[39599811038](https://github.com/AkiraMisawa/ex-grid/actions/runs/39599811038); the product
PR retains its existing assertions while this investigation continues.
