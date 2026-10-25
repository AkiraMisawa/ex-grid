Product: ExGrid
Kind: Fixed
ADR: 0023

`GridSource.From` refuses a Sort or a Filter on a column whose `ColumnType`, or a Date column whose `DateType`, was cast in from outside the enum before it reads any row, with an `ArgumentOutOfRangeException` that names the column. Before, such a Query was refused only once a value of the column was compared or read, so over rows that never reached that point it was quietly accepted, and a Filter's refusal did not say which column it was.
