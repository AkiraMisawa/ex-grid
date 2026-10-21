# 10: ExBoard ships as packages

Status: done
Roadmap: internal

ADR-0340 names `ExBoard` and `ExBoard.MudBlazor`. Before a release carries them: the release workflow's
package list, the package smoke check (dependencies exactly ExGrid, ExPivot and ExChart of the same
version), the coverage summary's products, the Release Notes' products (so a Change Note can say
`Product: ExBoard`), the Roadmap's products, and the Docs Site's API reference. Decide whether a
Wrapper is needed, since the component already takes each product's Chrome.
Ticket 09 left two things here, since both need ExBoard as a product: the Change Note for the Board's JSON
as a public surface (`Product: ExBoard`), and the Docs Site's Examples under both Chromes, which follow the
Wrapper decision.
