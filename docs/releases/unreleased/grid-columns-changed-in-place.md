Product: ExGrid
Kind: Fixed
Docs: exgrid/data#three-ways-to-give-the-grid-rows
ADR: 0003

A column list changed in place shows only once it is handed to the grid again; in React a scroll, and in Blazor a
row fetched, could paint the changed list's cells under the headers of the list the grid had taken.
