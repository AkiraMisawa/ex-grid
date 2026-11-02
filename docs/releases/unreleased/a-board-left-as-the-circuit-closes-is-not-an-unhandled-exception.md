Product: ExBoard
Kind: Fixed
ADR: 0370

On Blazor Server, an `ExBoard` disposed while its circuit was closing, as on leaving its page, could write `TaskCanceledException` to the host's log as an unhandled exception in the circuit. It now lets the browser's Board go quietly, as `ExGrid`, `ExSheet`, `ExPivot` and `ExChart` do.
