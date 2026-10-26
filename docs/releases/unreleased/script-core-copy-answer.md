Product: ExGrid
Kind: Added
ADR: 0050, 0370

`@ex-grid/core` takes `copyAnswer`, as the Blazor component's `CopyAnswer`: for the copy plan the grid approved, the Consumer writes both flavours itself or refuses with a sentence of its own, which the grid says and reports as `refusedByConsumer`. The core also exports the pieces a Consumer's answer is built from: `assembleCopy`, `parseClipboard`, `invariantMarker` and `markAttribute`.
