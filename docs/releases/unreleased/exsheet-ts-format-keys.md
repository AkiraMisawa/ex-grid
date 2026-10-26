Product: ExSheet
Kind: Added
ADR: 0071, 0050

ExSheet in TypeScript takes Excel's formatting keys on the Selection, as the Blazor Sheet does: Ctrl+B, Ctrl+I, Ctrl+U and Ctrl+5 by the Focus cell's direction, the Number Format keys (Ctrl+Shift+~, !, @, #, $, %, ^), the outline and no-border keys, each one step to undo and widening the columns a number no longer fits where Excel's key does. Ctrl+Plus and Ctrl+Minus insert or delete whole rows or columns, and say why over any other Selection. While a cell is edited none changes anything, and the notice says why. Ctrl+1 waits on Format Cells.
