Product: ExGrid
Kind: Breaking
ADR: 0201

The methods of `ExGrid` that the grid's own script calls (`[JSInvokable]`) are not the contract: they are hidden from a Consumer's editor and may change in any version. Since `0.1.0-beta.2`, `OnKeyAsync`, `OnKeyFieldTextAsync` and `OnPasteStreamsAsync` each take the paint a key or a paste was made against, and others have come; nothing a Consumer writes calls them.
