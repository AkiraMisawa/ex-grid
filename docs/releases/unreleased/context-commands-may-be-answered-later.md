Product: ExGrid
Kind: Added
ADR: 0421

The Script Core's `contextCommands` may answer a promise: the Context Menu opens once it settles, and a key typed meanwhile is held and reaches the menu then. A command's `invoke` may return a promise, and the input after it waits until it settles. Blazor's `ExGrid.Script.ExGrid` takes `ContextCommands` and `ContextCommandLabel`, asked of .NET as the menu opens, and `RowSource`, the source it reads its rows from.
