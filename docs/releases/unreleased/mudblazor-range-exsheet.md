Product: ExSheet
Kind: Behaviour
Docs: getting-started#requirements
ADR: 0205

`ExSheet.MudBlazor` takes MudBlazor `[9.0.0,10.0.0)` rather than 9.0.0 or anything above it, so an application that moves to MudBlazor 10 is warned at restore (NU1608, a warning unless the application treats it as an error), rather than left unwarned on a major version the Wrapper was never tested with; any 9.x from 9.0.0 is taken as before.
