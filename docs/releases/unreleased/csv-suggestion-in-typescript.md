Product: Data
Kind: Added
ADR: 0064, 0370

`@ex-grid/data` suggests a Schema for a CSV nobody has described: `suggestCsvSchema` reads a `Uint8Array`, an `ArrayBuffer` or a `Blob` only as far as the rows sampled, and proposes each column's kind and reading with the marks `CsvSchema.SuggestAsync` makes, in the same words; both packages run the same cases under `tests/cases/data/`. The `culture` is a language tag. .NET's list separator, which breaks a tie between two separators that split every record alike, is not in the browser's `Intl`, so the package carries .NET 10's by language; a tag .NET holds no culture for is taken by its language, where .NET gives a comma. It is published with 0.3.0.
