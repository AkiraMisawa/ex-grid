Product: ExGrid
Kind: Fixed
ADR: 0380

On Blazor's `ExGrid.Components.ExGrid`, Escape typed while a commit waits for its verdict no longer throws in .NET: `OnRefused` hears a commit refused with the new `CommitRefusalReason.Cancelled`, as the TypeScript core's `onRefused` hears `'cancelled'`.
