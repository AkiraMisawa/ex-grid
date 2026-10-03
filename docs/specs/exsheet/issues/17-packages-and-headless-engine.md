# 17: Packaging, and the engine on a server

Status: done

**What to build:** `ExSheet` and `ExSheet.Engine` pack and publish through the release workflow as prereleases
(ADR-0042). The package smoke test restores them as a Consumer would. A test, or the DemoHost's
Server host, computes a saved Sheet Document's Values with `ExSheet.Engine` alone and gets the same
numbers the screen shows.

**Blocked by:** 05

- [x] Both packages pack, with XML docs on every public member, and pass the package smoke check
- [x] `ExSheet.Engine` loads with no Blazor dependency
- [x] A saved document's Values computed headless equal the Values on screen (ADR-0047/0048)

## Comments

2026-09-27, engine half: `HeadlessTests` opens a saved Sheet Document (JSON, `de-DE`, with
formats, dates, a cycle, an error, a Linked Table reference) with nothing but `ExSheet.Engine` and
gets Excel's Values and displayed text; the same document gives the same Values whatever culture
the process runs under, and a Sheet edited (insertion, fill, paste, pasted text), saved and
opened again computes the same Values. The engine's whole reference closure holds no
`Microsoft.AspNetCore`, `Microsoft.JSInterop` or `ExGrid` assembly (the second criterion).
`tests/ExGrid.PackageSmoke/check.sh` now packs `ExSheet.Engine` too, reads its `.nuspec` back
(MIT, readme, repository and commit, symbols, XML docs, and no dependency at all), and the smoke
application restores it from the packed file and compiles the README's examples against it.
It is packed into a feed of its own, `.sheet-feed`, and not `.feed`: `.feed` is exactly what the
release workflow publishes, and its publish job accepts exactly ExGrid's four files, so
publishing `ExSheet.Engine` through the release is left for a decision (reported).
**What remains:** the `ExSheet` component package (it does not exist yet) and its smoke check,
the release workflow publishing both, and the third criterion's layer 2 half — the Values on
screen compared with the headless ones.

2026-09-27, engine: the Sheet Document is version 3 — row and column formats as runs, a cell's
own format and alignment — and the engine still opens versions 1 and 2, so a document saved by
an earlier prerelease computes the same Values headless. A headless reader that wants what a
column shows calls `Sheet.GetDisplay(address, width)` with the column's width in characters
(General fitted as Excel's, SH-20); `GetDisplay(address)` stays the unfitted text.
`SheetDocumentCell.Format` and `.Alignment` became nullable (null: the cell takes its row's or
column's), a source-level change for a Consumer that reads them.

2026-09-27, the component package: `check.sh` packs `ExSheet` too, into `.sheet-feed` beside the
engine and never into `.feed`, and now fails if `.feed` holds anything of ExSheet. It reads the
`.nuspec` back (MIT, readme, repository and commit, symbols, XML docs) and requires its
dependencies to be exactly `ExGrid` and `ExSheet.Engine` at the packed version and nothing else
(ADR-0046, SH-1). The smoke application restores `ExSheet` from the packed file and compiles the
README's `<ExSheet @bind-Document>` page (`Pages/Sheet.razor`); the published application carries
`ExSheet` and `ExSheet.Engine`. `check.sh` passes end to end. The third criterion's layer 2 half:
`SheetDocumentWiringTests.A_saved_document_shows_what_the_engine_alone_computes` hands the
headless suite's saved `de-DE` document (formats, a date, a cycle, `#DIV/0!`, an undeclared Linked
Table) to the component and finds every painted cell of A1:B13 equal to what `Sheet.Open` alone
gives for it. Publishing is not part of this ticket's done: ADR-0046 keeps `ExSheet.Engine` (and
so `ExSheet`) out of the release workflow while ExSheet is outside the release (Definition of Done
§2), so `release.yml` and `ci.yml` are unchanged — the `package` job uploads `.feed` alone, and the
release's publish job still accepts exactly ExGrid's four files. Publishing both is the release
decision's, when ExSheet joins it.

2026-10-03, publishing: the release decision came (ADR-0042's "The family ships together", decided
with the user). `check.sh` packs every package of the family into `.feed`, and `release.yml`
publishes `ExSheet.Engine`, `ExSheet` and `ExSheet.MudBlazor` from the next tag, with ExGrid's
and ExPivot's packages, at one version.
