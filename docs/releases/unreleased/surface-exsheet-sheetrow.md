Product: ExSheet
Kind: Breaking
ADR: 0201

`ExSheet.SheetRow` is no longer public: it is the row ExSheet hands the grid it draws, and no public member took or returned one. Nothing replaces it; a Consumer reads a cell through the `Sheet` (`GetValue`, `GetDisplay`, `GetEntry`) or the Sheet Document.
