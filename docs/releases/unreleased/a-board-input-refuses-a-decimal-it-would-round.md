Product: ExBoard
Kind: Fixed
ADR: 0341

A Board Input's JSON rows no longer round a `decimal` with more digits than a decimal holds: `0.12345678901234567890123456789` or `"79228162514264337593543950335.5"` is refused by input, row and column, as a value that does not convert, rather than handed on as money nobody wrote. A refusal of a row that is not an object now numbers the row in the invariant culture, as every other refusal of the rows does.
