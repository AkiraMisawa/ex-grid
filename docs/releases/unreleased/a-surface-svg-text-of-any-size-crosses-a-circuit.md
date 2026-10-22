Product: ExChart
Kind: Fixed
ADR: 0282

`ExSurface.SvgTextAsync` hands over a drawing of any size on Blazor Server. The text crossed the circuit as one message, and a drawing over the circuit's 32 KB message limit closed the circuit instead of answering. It now crosses as a stream.
