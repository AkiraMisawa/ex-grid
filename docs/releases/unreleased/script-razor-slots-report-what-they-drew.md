Product: ExGrid
Kind: Fixed
ADR: 0410

On Blazor Server, a Chrome slot filled with Razor could report the newest data its slot had been sent rather than the data its contents were drawn from. This happened when an update arrived between the render and its report. Under MudBlazor, the Script Sheet's completion list then stayed on an earlier list, or a key in it threw "the script no longer holds" the data. A slot now reports the number it was drawn from, and the render its update asked for is reported in turn. This reaches ExSheet, ExPivot, ExChart and ExBoard over the Script Core, whose slots cross the same way.
