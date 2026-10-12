Product: ExSheet
Kind: Behaviour
Docs: getting-started#requirements
ADR: 0205

`ExSheet.MudBlazor` takes MudBlazor `[9.0.0,10.0.0)` rather than 9.0.0 or anything above it, so an application that moves to MudBlazor 10 is told so at restore (NU1608) instead of running the Wrapper on a major version it was never tested with; any 9.x from 9.0.0 is taken as before.
