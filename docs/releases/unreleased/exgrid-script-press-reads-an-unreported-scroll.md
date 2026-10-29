Product: ExGrid
Kind: Fixed
ADR: 0012

Over the Script Core, a script could write a scroll offset and press at once, before the browser reported the scroll. That press could land on a column to the right of the one under the pointer. A press now reads the offsets first, so it lands on the cell under the pointer however soon it follows the scroll. ExSheet and ExPivot draw their grids this way, so the fix reaches them too.
