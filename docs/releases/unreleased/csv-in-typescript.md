Product: Data
Kind: Added
ADR: 0064, 0370

`@ex-grid/data` reads a CSV under a declared Schema: `readCsvSnapshot` takes the Schema as an object (its columns, separator, header row, encoding, blank texts and Record Key) and a `Uint8Array`, `ArrayBuffer` or `Blob`, and reads it as `CsvSchema.ReadAsync` does, in paced slices that report progress and can be cancelled. A refusal is a `SnapshotError` naming the row, column and line in the same words as `SnapshotException`; both packages run the same cases under `tests/cases/data/`. A culture is a language tag read through `Intl`; a date in a calendar other than the Gregorian or the Thai Buddhist, a standard date format in a culture, the standard format `U`, and the era (`g`) are refused when the Schema is read. Shift-JIS is decoded as the browser's decoder reads it. It is published with 0.3.0.
