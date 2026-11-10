Product: ExGrid
Kind: Behaviour
Docs: exgrid/appearance#visual-tokens
ADR: 0030

Under the Mantine, MUI and MudBlazor Wrappers the Focus outline, the Heading Mark's line, the grid's focus ring and the
editor's underline read 3:1 against the grid's ground and the Heading Mark's shades whatever the theme's primary, in
either scheme (#556). They drew in the primary as it was in the light scheme, so an amber primary's Focus read 2.7:1 on
white and its line 2.0:1 on the shade, and a saturated primary's line fell short on the dark shade. The primary is now
darkened in a light scheme and lightened, its chroma softened, in a dark one, hue kept, only as far as 3:1 needs: Mantine's
default blue draws a shade darker in the light scheme. Under MudBlazor's dark scheme the Heading Mark's shades are now
darker than the surface rather than lighter, so its line clears them as well as an ExSheet's white Paper.
ExSheet's and ExPivot's grids under those Wrappers show the same; ExSheet's own toolbar, swatch and tab marks under
MUI and MudBlazor still take the primary as it is.
