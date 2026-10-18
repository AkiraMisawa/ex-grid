# 03: ADR-0021 names the page-follow scroll ADR-0204 allows

Status: ready-for-agent
Roadmap: internal

**Why:** the 0.1.0 sign-off's PF-2 inspection found every listener and interop in `ex-grid.js` mapped to
an ADR-0021 entry, with one caveat: `element.scrollIntoView` (page-follow) is authorised by ADR-0204, but
ADR-0021 still says `scrollIntoView()` "is refused" and carries no dated note for it. And the comment on
the script's first line still says "The six permitted uses", where AGENTS.md and ADR-0021 name ten.

**What:** a dated note in ADR-0021 recording ADR-0204's page-follow use, in the form of its other notes,
and the script's first comment brought to ten (edited in `src/ExGrid/Assets/`, then minified, AGENTS.md
"Toolchain").
