Product: Data
Kind: Added
ADR: 0065

`@ex-grid/data`, a new npm package with no dependencies, holds a Snapshot in TypeScript and reads and writes it as Arrow exactly as `ExGrid.Data.Arrow` does: `readArrowSnapshot` (an IPC stream or an Arrow file, in slices with progress and an `AbortSignal`), `readArrowShape`, `writeArrowSnapshot` and `createSnapshot`. Both packages run the same cases under `tests/cases/arrow/`, each reads what the other writes, and a refusal says the same row, column and words; a stream whose buffers are compressed is refused, since the package carries no codec. It is published with 0.3.0.
