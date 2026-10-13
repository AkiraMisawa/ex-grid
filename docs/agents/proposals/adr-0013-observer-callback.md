# ADR-0013: what the gutter's observer may do inside its callback — a correction

Branch `claude/dod-gaps-browser-tests`, 2026-10-13. ADR-0013 says nothing writes to the DOM inside the
Scrollbar Gutter observer's callback, so the browser's "ResizeObserver loop" warning cannot arise. On
WebAssembly that was false: `invokeMethodAsync` runs the .NET method at once on the page's thread, and a
render it asks for is applied before the call returns, so the report's render wrote to the DOM inside the
observation. On `/density` at attach the gutter's strip appeared in that render, the scroller's content box
moved from 1216 to 1204 px in the same observation, and Chrome raised the loop on `window` (3 of 3 fresh
loads; never on the Server host, whose call crosses the circuit). The fix tells C# from a task of its own
after the observation, for the gutter and for the Layout Ceiling. The decision stands; the claim is
corrected. One edit, for the orchestrator to apply:

## 1. docs/adr/0013-fixed-row-height.md

Old:

```
  there is no loop for the observation to chase, and nothing writes to the DOM inside the
  callback, so the browser's own "ResizeObserver loop" warning has no way to arise either.
```

New:

```
  there is no loop for the observation to chase, and nothing writes to the DOM inside the
  callback, so the browser's own "ResizeObserver loop" warning has no way to arise either.
  *(Corrected 2026-10-13: on WebAssembly C# did write inside it. `invokeMethodAsync` runs the
  .NET method at once on the page's thread and applies the render it asks for before returning,
  so the report's render — the gutter's strip appearing — moved the observed box within the
  observation, and Chrome raised the loop on `window` (CON-4, `/density` at attach). The report
  is now told from a task of its own after the observation, for the gutter and for the Layout
  Ceiling, carrying the latest values, which the frame accepted below already covers.)*
```
