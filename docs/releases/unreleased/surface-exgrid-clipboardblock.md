Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.ClipboardBlock` is no longer public, and neither is `ClipboardParse.ParseBlock`, which returned it. `ClipboardParse.Parse` reads the same values, and a paste's `GridPasteIntent` carries where each field came from (`Origins`, `OriginFor`).
