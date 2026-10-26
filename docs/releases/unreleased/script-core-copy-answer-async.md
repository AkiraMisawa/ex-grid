Product: ExGrid
Kind: Added
ADR: 0152, 0370

`@ex-grid/core` takes `copyAnswerAsync`, as the Blazor component's `CopyAnswerAsync`: a Consumer that must ask elsewhere for what it copies answers each approved copy later, with both flavours or a refusal in its own sentence. The copy then always takes the clipboard's asynchronous route; a copy a newer one supersedes is aborted through the signal it is handed and writes nothing, and an answer that fails is said as `rowsUnavailable`. `copyAnswer` is still the one asked where both are given.
