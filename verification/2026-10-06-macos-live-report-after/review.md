# Live-data continuation review

Reviewed the implementation at `1f4432bc898806f26c9190d9d86ddd959dd0b5c4` against
`41c8d8c8ee33a06be19a804628db7d49a637e171` using `git diff <base>...HEAD`.
Two independent agents reviewed Standards and Spec, followed by a read-only check of the fixes.
The specification is `docs/specs/live-data/spec.md`, tickets 01–03 and ADR-0150–0153.

## Standards

1. **P1 — Report JSON depended on reflection metadata.** A trimmed Consumer could lose the
   required properties or constructors. The Docs Site retained the whole engine assembly, so
   its successful execution alone did not establish the trim safety required by ADR-0066.
   **Resolved:** `PivotReportJsonContext` generates metadata for wire messages; the serializer
   uses explicit type information with no reflection fallback. The package smoke test publishes
   and runs a trimmed Consumer with reflection disabled and without assembly roots. It exercises
   Windows, deltas, Copy, Summary, Items, raw Items, populated Details, refusals, explicit culture,
   word overrides, policy identifiers, highlight duration and per-glyph label metrics.
2. **P2 — Display settings validation omitted `ChangeHighlightDuration`.** A response could use
   another setting while appearing to satisfy ADR-0152's explicit-settings contract.
   **Resolved:** settings equality includes the duration. A deterministic refusal test failed
   before the fix and passes after it.

The follow-up review found no further defect in the generated metadata or converters. No
additional code-smell finding was reported.

## Spec

1. **P1 — A response could become obsolete during validation and still publish.** The original
   generation check preceded validation; a newer request could finish before the older response
   assigned `Current` or `Refusal`, violating ADR-0152 and LV-24.
   **Resolved:** starting a request and publishing either outcome use the same lock, with the
   generation checked again at publication. Two deterministic regression cases interleave a
   newer completed request during valid and malformed older-response validation. Both failed
   before the fix and pass after it; they require neither timing delays nor retries.
2. **P3 — PV-2 still described a whole local report Window.** This conflicted with the common
   local/server asynchronous requested-Window boundary in ADR-0153.
   **Resolved:** PV-2 names requested Windows and full report extent for both providers.

The follow-up review confirmed both fixes and reported no additional major issue. No missing
requirement or scope-creep finding was otherwise reported.

Standards: two findings, highest P1, both resolved. Spec: two findings, highest P1, both resolved.
The [verification record](README.md) identifies the final test runs; full layer 3 remains CI's.
