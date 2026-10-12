Product: ExSheet
Kind: Breaking
ADR: 0201

`ExSheet.Engine.ErrorValues`, with its `ToText` and `TryParseTyped`, is no longer public: it is the engine's own table of Error Value texts. A Consumer reads an Error Value's text as `Value.ToString()`, or a cell's as `Sheet.GetDisplay(address).Text`, and reads typed error text such as `#N/A` with `Entry.Parse`.
