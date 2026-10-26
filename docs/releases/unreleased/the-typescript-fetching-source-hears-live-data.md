Product: ExGrid
Kind: Added
ADR: 0141

`@ex-grid/core`'s `GridSource.fetch` now takes live server data, as the Blazor `FetchingGridSource` does. Given a Row Key, `notifyChanged(addedKeys?)` reads the Window again, gathered every `gatherInterval` (250 ms unless set), without raising `loading`. The rows are paired by key with the rows painted, and `cellChangedAt` marks the cells whose displayed text changed. A key the Consumer names as added is marked whole wherever it lands. An answer whose order token differs moves the Row Sequence Version, and so does every change from a server that sends no token. Without a Row Key, `notifyChanged`, `gatherInterval` and `changeTimesKeptFor` are refused by name.
