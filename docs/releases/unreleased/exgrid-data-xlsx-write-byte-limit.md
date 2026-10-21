Product: Data
Kind: Added
ADR: 0330

`XlsxWriteOptions.MaxUncompressedBytes` bounds what an `XlsxWriter` writes: the package's parts together may hold at most this many uncompressed bytes, by default `XlsxReadOptions.DefaultMaxUncompressedBytes` (1 GiB), the most a file is read with, so what is written opens again. A write that would go past it is refused as an `XlsxException` with `XlsxRefusal.TooLarge`, naming the part and the sheet it met, rather than failing later with an unnamed `IOException`.
