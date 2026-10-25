Product: ExGrid
Kind: Fixed
ADR: 0023

`GridSource.From` refuses a Filter whose column spec has its `Clauses` written as null, as a Query deserialised from JSON can write them, with the engine's own `InvalidOperationException` naming the column, while another Filter is in force. Before, comparing it with the Filter in force threw a `NullReferenceException`.
