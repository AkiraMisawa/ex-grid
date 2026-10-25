Product: ExGrid
Kind: Fixed
ADR: 0023

`GridSource.From` refuses a Sort or a Filter on a column whose `ColumnType` was cast in from outside the enum before it reads any row, with an `ArgumentOutOfRangeException` that names the column. A Sort on such a column was refused only once two of its values were compared, so over zero or one row it was quietly accepted, and a Filter's refusal did not say which column it was.
