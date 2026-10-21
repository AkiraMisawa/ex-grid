# 06: Outputs raised on a confirm

Status: done
Roadmap: internal

ADR-0340 (Q6): an output is declared and raised only by a declared confirm action, as a value
condition on the input with the source version it was taken against, as a .NET event and as JSON. A
version that has moved on is refused (spine rule 6).

Done: the reader's `outputs` (`BoardOutputDeclaration`), `BoundBoard.CanConfirm` / `Confirm`,
`BoardOutput` (`Take`, `ToJson`), the component's confirm under its grid and `OnOutput`, the Docs Site's
section, and `board.spec.mjs`. The defaults taken are in ADR-0340's implementation notes. The confirm is a
plain button under every Chrome; a MudBlazor one is ticket 10's question of a Wrapper.
