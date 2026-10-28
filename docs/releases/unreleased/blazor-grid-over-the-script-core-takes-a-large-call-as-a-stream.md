Product: ExGrid
Kind: Fixed
ADR: 0005

On Blazor Server, Blazor's ExGrid over the Script Core (`ExGrid.Script.ExGrid`) no longer closes the circuit when the browser hands .NET more than the 32 KB a hub takes in one message: a paste, a long edit and its verdict, a filter of many values, a filter panel's list or a large Selection. Such a call is held in the browser and taken by .NET as a gzip stream, in its turn, so every call after it waits for it and the order the grid raised them in holds. Nothing changes on WebAssembly, or for an application that raised its hub's limit.
