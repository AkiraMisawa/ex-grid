Product: ExGrid
Kind: Fixed
Docs: exgrid/columns#mark--template-and-action-columns

Under the Mantine, MUI and MudBlazor Wrappers the Action Column's buttons take the theme's colours in either scheme
(#553): Mantine's default Button, MUI's paper and text, MudBlazor's outlined default. They kept the browser's own grey
button, which in a dark scheme was the brightest thing in the row. Their box is unchanged, so an Auto column keeps its
width.
