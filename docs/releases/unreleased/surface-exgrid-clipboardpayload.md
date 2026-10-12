Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Clipboard.ClipboardPayload`, what the grid hands its own script to put on the clipboard, is not the contract: it is hidden from a Consumer's editor and may change in any version. Since `0.1.0-beta.2` its constructor and deconstruction take the copy's `Landing`. A Consumer answering a copy through `CopyAnswer` receives a `GridCopyRequest` and answers with a `GridCopyAnswer`, never a payload.
