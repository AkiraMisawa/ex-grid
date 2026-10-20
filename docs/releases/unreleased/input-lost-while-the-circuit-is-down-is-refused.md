Product: ExGrid
Kind: Fixed
ADR: 0240

On Blazor Server, a key, press or paste made while the circuit was disconnected never reached the grid, and every input after the reconnection then waited for it, so the grid stopped answering. Once the connection is back, the next input now finds what went missing: it is refused aloud with the new `KeyRefusalLabelIds.LostOnTheWay`, never replayed, and the new input is handled.
