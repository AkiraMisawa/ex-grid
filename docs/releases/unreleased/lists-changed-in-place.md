Product: ExGrid
Kind: Fixed
ADR: 0003

A `Columns`, `HeaderGroups` or `Sorts` list edited in place now shows its change when it is handed to the grid again, where it used to crash (a column added) or show the old cells under the new header; until then the grid draws from the copy it took when it was handed the list, so a scroll or a key in between never shows a half-applied change, and what the grid holds for a column, such as an open column menu, follows the column by its Name.
