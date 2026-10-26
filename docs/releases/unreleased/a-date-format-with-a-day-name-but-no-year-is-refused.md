Product: Data
Kind: Fixed
ADR: 0064

A CSV Date column whose format names the day of the week but no year (`ddd dd MMM`) is now refused when the Schema is read, as a format with a month or a day but no year already was: .NET reads such a value in the current year, so the same file read next year would read otherwise.
