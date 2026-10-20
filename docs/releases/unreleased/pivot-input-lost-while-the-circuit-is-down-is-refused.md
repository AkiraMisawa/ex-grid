Product: ExPivot
Kind: Fixed
ADR: 0240

On Blazor Server, input made while the circuit was disconnected no longer leaves the report unanswering after the reconnection: the next input refuses the lost input aloud, never replays it, and is handled.
