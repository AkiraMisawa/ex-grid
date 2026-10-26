Product: Data
Kind: Fixed
ADR: 0064

When two separators split every record of a file alike and their fields read equally well, `CsvSchema.SuggestAsync`'s Separator mark said the one suggested was chosen because its fields read best as numbers and dates. It now says what chose it: the culture's list separator, or, when that is neither, nothing but the order they are tried in.
