Product: ExGrid
Kind: Fixed
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0039, 0010, 0080

Typing with an IME on Blazor Server: a composition finished on a selected cell in the round trip while a popover opened — Find after Ctrl+F, ExSheet's Format Cells after Ctrl+1 or from the Context Menu, a column's menu after Alt+↓, or your own `OpenPopoverAsync` — opened a Cell Editor holding its text behind the popover, and that editor then took the keyboard from it. Now the text goes where the keys typed with it go: into the popover's text field at the caret, as in Find, or, where the popover's control is not a text field, as on Format Cells' tabs, nowhere, with the keys typed after it, the way a held Tab already is.
