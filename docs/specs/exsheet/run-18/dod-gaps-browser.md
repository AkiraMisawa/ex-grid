# The eighteenth Windows run: the gutter's report, moved out of the observation

Gathered from `claude/dod-gaps-browser-tests` (the Definition of Done audit's layer-3 tests, 2026-10-13), to be
folded into the eighteenth run's spec. The branch's proposal (an ADR-0013 correction, carried by the orchestrator)
asked no question of Excel or of a real IME. One change of its own touches a row checked on Windows:

1. **VZ-14, the Scrollbar Gutter at fractional display scaling**, as the row is written (`scrollbar.spec.mjs`'s
   "on a Windows desktop at fractional display scaling (VZ-14)" block, Chrome and Edge, WebAssembly and the Server
   host). The gutter's observer, and the Layout Ceiling's, now tell C# from a task of their own after the browser's
   observation rather than inside its callback (`dotnet/src/ExGrid/Assets/ex-grid.js`, `toldAfterTheObservation`, commit
   `2d8695d9`; CON-4, ADR-0013). The row's record (`verified/2026-10-01-windows-15`) predates the change, so this run
   is the first to read the deferred report at an OS scale that is not a whole number of device pixels per CSS
   pixel. No step is added to the row.
