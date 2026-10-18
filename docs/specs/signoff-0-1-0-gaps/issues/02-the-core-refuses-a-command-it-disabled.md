# 02: The core refuses a command it marked disabled

Status: needs-triage
Roadmap: ExGrid — a substituted menu can no longer run a command the grid shows as unavailable

**Why:** the 0.1.0 sign-off's inspection of FN-18
(`verification/2026-10-18-linux-signoff/inspect-rows.md`) found that `RunCommandAsync`
(`src/ExGrid/Components/ExGrid.razor`, near line 5564 at 0.1.0) runs `command.Invoke()` without reading
`Enabled`. A substituted Chrome that ignores `Enabled` could send `OnPinnedCountChanged` a pin the core
disabled because the boundary cuts a Header Group (ADR-0032), or a sort for a column that is not
queryable. It is the same shape as the gap fixed for FN-18: the core's own flag, not enforced at the call.
FN-18 as written does not reach it, so 0.1.0 passed.

**What:** the core refuses a command whose `Enabled` is false at the moment it is invoked, as
`ApplyFilterSpecAsync` refuses an operator outside `Allowed`, and a layer-2 test with a stub Chrome
that invokes a disabled command pins it. Whether the refusal is announced, and whether FN-18's row is
widened to say it, is the triage question (spine rule 1; ADR-0009, ADR-0010).
