# 08: MEM-7 measured over the Script Core

Status: ready-for-agent
Roadmap: internal

**Why:** MEM-7's harness measured the C# core's render, which #494 to #512 removed, so the 0.2.0 sign-off recorded it
as not run (the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), Step 6).

**What:** a harness that measures what MEM-7 names over the Script Core, under React and Blazor WebAssembly, run in
`tests/browser`'s measure specs or `spikes/render-bench`, recorded and never gated.
