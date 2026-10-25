Product: ExPivot
Kind: Fixed
ADR: 0362

Under the MudBlazor Wrapper, Enter on a Layout ▾ choice no longer leaves the pivot unsettled. The click the browser makes of that Enter reached a choice already closed, and its input was never answered, so automation waiting on the Settled Mark waited until the next input.
