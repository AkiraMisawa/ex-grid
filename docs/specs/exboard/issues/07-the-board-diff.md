# 07: The Board Diff

Status: done
Roadmap: internal

ADR-0340 (Q7): running a Board never rewrites it. What the user changes is raised as one Board Diff —
the grid's View State JSON, the pivot's layout JSON, the chart's raised changes (ADR-0272) — which the
host may lay over the Board next time; a part that no longer fits is dropped by name.
