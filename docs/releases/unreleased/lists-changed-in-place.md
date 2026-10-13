Product: ExGrid
Kind: Fixed
ADR: 0003

A `Columns`, `HeaderGroups` or `Sorts` list edited in place and handed again now repaints, where it used to crash (a column added) or show the old cells under the new header; what the grid holds for a column, such as an open column menu, follows the column by its Name.
