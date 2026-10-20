Product: ExGrid
Kind: Fixed
ADR: 0005

On Blazor Server, a paste whose clipboard data stopped arriving (seen with a ~10 MB paste on a slow connection) escaped a minute later as an unhandled exception in the circuit. It is now refused aloud with the new reason `PasteRefusalReason.Unreadable`, nothing is pasted from part of it, and the grid goes on answering. A Consumer that switches over `PasteRefusalReason` without a default case gains one value to handle.
