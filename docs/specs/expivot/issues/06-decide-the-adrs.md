# 06: Decide ADR-0059 to ADR-0063 with the user

Status: done

**What to do:** ADR-0059 to ADR-0063 were written as proposals while ExPivot was built, and each
says so. Put them in front of the user together, as AGENTS.md asks of decisions made alongside
implementation, and record the outcome in each ADR: accepted, or changed — in which case the code
and §29 follow the ADR.

The choices most worth their attention:

- ExPivot outside the ExGrid release, in a feed of its own (ADR-0059), as ExSheet is.
- Aggregation in process over a snapshot, with a Consumer-answered pivot query reserved (ADR-0059).
- What the first version holds and what it leaves for later (ADR-0059's table).
- The one core change, `OnCellDoubleClick` (ADR-0063), which gates ExGrid through DC-59.
- The Wrapper reusing `MudExGridPaper` rather than a paper of its own (ADR-0062).

**Blocked by:** None

## Comments

2026-09-30: Decided with the user in a grilling of the requirements (Q1 to Q63). ADR-0059 to
ADR-0063 are marked decided, each change kept in the text with its reason; the grilling added
ADR-0064 to ADR-0069. The build follows them through tickets 09 to 20.
