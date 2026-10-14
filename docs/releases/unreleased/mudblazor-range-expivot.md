Product: ExPivot
Kind: Breaking
Docs: getting-started#requirements
ADR: 0205

`ExPivot.MudBlazor` takes MudBlazor `[9.6.0,10.0.0)` where it took 9.0.0 or anything above it, the floor of `ExGrid.MudBlazor`, which it brings in: an application that references MudBlazor 9.0 to 9.5 moves it to 9.6.0 or a later 9.x, or restore stops it (NU1605, an error by default); below 9.4.0 the selects of Field Settings… and Value Field Settings… have no role and no name where the keyboard is, and below 9.2.0 Field Settings… can open with the keyboard nowhere. An application on MudBlazor 10 is warned at restore (NU1608, a warning unless the application treats it as an error), rather than left unwarned on a major version the Wrapper was never tested with.
