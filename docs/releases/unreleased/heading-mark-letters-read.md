Product: ExGrid
Kind: Fixed
Docs: exgrid/appearance#visual-tokens
ADR: 0029

The marked Heading's letters read at 4.5:1 under the Mantine, MUI and MudBlazor Wrappers, whatever the theme's primary:
they drew in the Focus outline's primary, a colour chosen for a line, and an amber primary read 2.05:1. The letters now
take a Visual Token of their own, `--ex-heading-mark-color`, which is `--ex-heading-mark`'s colour unless set; the
Wrappers map it to a shade of the primary that reads as text, and leave the line as it was. ExSheet and ExPivot under
those Wrappers show the same. Under a Wrapper, a Consumer who set `--ex-heading-mark` to colour the letters sets
`--ex-heading-mark-color` as well.
