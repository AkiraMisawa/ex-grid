Product: ExGrid
Kind: Fixed
ADR: 0080, 0021

A composition begun on a cell just pressed is typed into it. A press on the rows gave the keyboard to the grid's Keyboard Field only once the core had answered it, a round trip later on Blazor Server, and an IME composition begun and ended in between, on a page where the grid had not yet held the keyboard, was lost without a word. The keyboard now goes to the field at the press. ExSheet's and ExPivot's grids are fixed with it.
