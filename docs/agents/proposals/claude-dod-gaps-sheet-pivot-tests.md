# Proposals from `claude/dod-gaps-sheet-pivot-tests`

Branch `claude/dod-gaps-sheet-pivot-tests`, 2026-10-13. Edits to files a background agent does not edit,
for the orchestrator to apply. Each block is exact whole lines, old -> new.

## 1. ADR-0152: a dated note on a change of `OrderKeyPolicies`

Decided with the user on 2026-10-13. The fix is on this branch: `ExPivot.razor`'s `OnParametersSetAsync`, with the
tests in `tests/ExPivot.Components/OrderKeyPolicyChangeTests.cs`, and the Change Note
`docs/releases/unreleased/order-key-policies-ask-again.md`.

File `docs/adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md`, line 47.

Old:
```
that throws retains ADR-0060's named refusal; a null key retains its existing meaning.
```
New:
```
that throws retains ADR-0060's named refusal; a null key retains its existing meaning.
*(2026-10-13, decided with the user: a change of OrderKeyPolicies on a shown report asks for the report again, as a layout change does. Otherwise the screen would keep one order while the parameter names another. The same policies handed again, even in a new dictionary, ask nothing.)*
```

## 2. The Docs Site: the "An Order Key on a server" section

File `samples/ExGrid.Docs/Pages/Pivot/PivotLayoutPage.razor`, line 121.

Old:
```
            never ordered by label instead.
```
New:
```
            never ordered by label instead. Changing <code>OrderKeyPolicies</code> on a report already shown asks
            for the report again, as a new layout does, so the Items follow the policy the parameter names.
```

## 3. The Definition of Done: LV-26 (optional, for the orchestrator to weigh)

The row does not mention the decision. If the decision belongs in the criterion, this is the row with it added
(file `docs/definition-of-done.md`, line 1564).

Old:
```
| **LV-26** | MUST | A remote custom Order Key selects a server-registered function; local computation uses its local function. Ordering semantics remain ADR-0060's. Unknown or unsupported policies are refused by name, never silently replaced with label order; a registered function that throws is refused by name across the wire (`PivotReportRefusalKind.OrderKeyFailed`). Culture, formats and display words are explicit and consistent across the report and its operations; Culture does not select the words (ADR-0152) | Layer 1: local/server ordering and settings parity, null keys, ties, Blank/error and policy refusals, a throwing function over JSON; Layer 2: the refusal is surfaced; serialization tests contain settings/policy identifiers, not delegates | equal semantics; no implicit fallback or language switch |
```
New:
```
| **LV-26** | MUST | A remote custom Order Key selects a server-registered function; local computation uses its local function. Ordering semantics remain ADR-0060's. Unknown or unsupported policies are refused by name, never silently replaced with label order; a registered function that throws is refused by name across the wire (`PivotReportRefusalKind.OrderKeyFailed`). Culture, formats and display words are explicit and consistent across the report and its operations; Culture does not select the words; a change of `OrderKeyPolicies` on a shown report asks for the report again, and the same policies ask nothing (ADR-0152) | Layer 1: local/server ordering and settings parity, null keys, ties, Blank/error and policy refusals, a throwing function over JSON; Layer 2: the refusal is surfaced, and a changed policy reorders a shown report; serialization tests contain settings/policy identifiers, not delegates | equal semantics; no implicit fallback or language switch |
```
