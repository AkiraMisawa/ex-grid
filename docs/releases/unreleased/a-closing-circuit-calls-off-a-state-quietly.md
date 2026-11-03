Product: ExGrid
Kind: Fixed
ADR: 0370

On Blazor Server, a grid whose source changed just before its page was left no longer logs "Unhandled exception in
circuit: A task was canceled." The state that change sent was called off by the closing circuit, and nothing was wrong;
ExPivot's notice of a changed source is let go the same way.
