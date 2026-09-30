# 06: Decide ADR-0058 to ADR-0062 with the user

Status: ready-for-human

**What to do:** ADR-0058 to ADR-0062 were written as proposals while ExPivot was built, and each
says so. Put them in front of the user together, as AGENTS.md asks of decisions made alongside
implementation, and record the outcome in each ADR: accepted, or changed — in which case the code
and §29 follow the ADR.

The choices most worth their attention:

- ExPivot outside the ExGrid release, in a feed of its own (ADR-0058), as ExSheet is.
- Aggregation in process over a snapshot, with a Consumer-answered pivot query reserved (ADR-0058).
- What the first version holds and what it leaves for later (ADR-0058's table).
- The one core change, `OnCellDoubleClick` (ADR-0062), which gates ExGrid through DC-52.
- The Wrapper reusing `MudExGridPaper` rather than a paper of its own (ADR-0061).

**Blocked by:** None

## Comments
