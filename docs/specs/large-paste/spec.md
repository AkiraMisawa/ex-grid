# A large paste completes, says it is under way, and fails aloud

Status: needs-triage
Roadmap: ExGrid — A large paste on a slow connection shows that it is under way, completes, and is refused aloud if its data cannot be read.

**Why:** PST-5 pastes about 10 MB of TSV. Under the widened run (150 ms round trip, CPU ×4) it never
completes on the Server host, and its stream's timeout a minute later reaches the host log as an
unhandled circuit exception (`docs/specs/widened-run-failures`, tickets 05 and 06). Meanwhile the
user sees nothing: the paste is a numbered input, so every key behind it waits, with no sign why.

- Ticket 05 (widened run): find why the stream stops, and fix it.
- Ticket 06 (widened run): a read that fails refuses the paste aloud.
- [01](issues/01-a-large-paste-shows-it-is-under-way.md): the grid says a paste is under way, and Escape
  cancels it (ADR-0240 section 12).
