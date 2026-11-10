Product: ExGrid
Kind: Added
Docs: exgrid/columns#a-compact-header

`HeaderDisplay` (`headerDisplay` in ExGrid for React) chooses how every header of a grid lays out what stands beside its
label (ADR-0500). `Standard`, the default, is the header as it was. `Compact` gives the column menu's ▾ and the marks of the
column's sort and filter one box inside the cell's padding, so a header needs 28 px less on a column that can be sorted and
has the menu; the box shows an arrow, a funnel or both while the column holds a sort or a filter, and a press on it opens
the column menu. An Auto width is measured under the display chosen; a width that was declared, dragged, fitted or
restored is never changed by it.
