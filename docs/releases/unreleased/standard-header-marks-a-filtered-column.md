Product: ExGrid
Kind: Behaviour
Docs: exgrid/sorting-filtering
ADR: 0501

A filtered column now says so in its header: while the `Filter` holds a spec for a column, the column menu's ▾ in that
column's header is drawn as a funnel, in the default header as in the compact one, and the button is named
`Column menu, filtered` for a screen reader. No column's width changes, and a click, a press on the ▾ and Alt+↓ mean what they meant.
A grid that shows the column menu and is given a filter will look different on its filtered columns; a test that finds
the ▾ of a filtered column by the exact name `Column menu` should find it by `Column menu, filtered` or by its class.
With `HideColumnMenu` the default header has no ▾ and shows no mark.
