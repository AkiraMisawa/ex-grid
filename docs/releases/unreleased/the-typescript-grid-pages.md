Product: ExGrid
Kind: Added
ADR: 0015

`@ex-grid/core`'s grid takes `pageSize`, as the Blazor grid's `PageSize` (ADR-0015): a pager `‹ n / N ›` stands after the rows, the scrollbar spans the page and a drag stops at its edge, the keyboard crosses pages, and Ctrl+A and the corner select the page while the status line counts the cells and offers "Select all N rows". Under a Mark Column the header's checkbox lines up the page, and "Mark all N rows" marks the whole result. A `pageSize` below one is refused by name.
