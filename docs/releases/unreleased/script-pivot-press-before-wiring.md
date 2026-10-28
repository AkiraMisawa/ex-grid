Product: ExPivot
Kind: Fixed
ADR: 0410

In Blazor's ExPivot and ExBoard over the Script Core, a Razor Chrome's button pressed in the round trip after it was drawn and before .NET said so (a field just placed on MudPivotChrome's Field List) was lost, or refused for want of its anchor; it now waits for its wiring and is taken in its turn in the report's Input Order, or refused where the Field List gave its entry's place to another field meanwhile.
