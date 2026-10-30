Product: ExPivot
Kind: Fixed
ADR: 0005

On Blazor Server, Blazor's ExPivot over the Script Core (`ExPivot.Components.ExPivot`) no longer closes the circuit when the browser hands .NET more than the 32 KB a hub takes in one message, a large Selection or a query over many items among them. Such a call is held in the browser and taken by .NET as a gzip stream, in its turn, so every call after it waits for it. Nothing changes on WebAssembly, or for an application that raised its hub's limit.
