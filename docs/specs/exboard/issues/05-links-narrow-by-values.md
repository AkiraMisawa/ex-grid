# 05: Links narrow by values

Status: ready-for-agent
Roadmap: internal

ADR-0340 (Q5): a link names columns; a selection in its view (a grid's rows, a pivot's cell) becomes an
equality filter on them, executed by the narrowed view's source (ADR-0002). A dropped selection drops
the filter (ADR-0011). Not capped; a source that cannot execute a long value list refuses with the
reason.
