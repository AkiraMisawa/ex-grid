Product: Data
Kind: Fixed
ADR: 0065

`ExGrid.Data.Arrow` refuses an Arrow dictionary that says it holds nulls and carries no validity bitmap to say which entries, naming the column, as it already refused such a column. Its null entries were read as text before.
