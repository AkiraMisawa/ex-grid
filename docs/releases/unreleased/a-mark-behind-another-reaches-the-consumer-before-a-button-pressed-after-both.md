Product: ExGrid
Kind: Fixed
ADR: 0240, 0043

On Blazor, an event the grid raised behind another, two row Marks clicked in quick succession among them, waited in .NET for the browser's reply to the first one's state before it reached the Consumer. On a circuit's round trip a button of the Consumer's outside the grid, pressed after both, could then read only the first mark. Each event now reaches the Consumer as soon as the one before it is handled; what it wrote still crosses to the browser before the browser hears it was handled.
