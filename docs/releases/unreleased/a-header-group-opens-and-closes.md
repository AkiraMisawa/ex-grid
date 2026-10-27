Product: ExGrid
Kind: Added
ADR: 0400

A Header Group with an `id` and an `expanded` draws a toggle that asks the Consumer, through `onHeaderGroupToggled` (`OnHeaderGroupToggled` on Blazor's `ExGrid.Script.ExGrid`), to show the group's other columns or hide them; Alt+Shift+↓ and Alt+Shift+↑ open and close the group over the Focus's column, and the Focus stays on its cell across the push. A closed group can carry a Hidden Mark, a corner in a Cell State's colour that says what its hidden columns hold. The grid holds no columns of its own, and Blazor's C# core grid, which draws no toggle, refuses a group that asks for one.
