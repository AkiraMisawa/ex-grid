# 01: Exit criteria for ExSheet and for the core declarations

Status: ready-for-human

**What to build:** Write the pass/fail criteria before the code they judge. `docs/definition-of-done.md` gains a
section for ExSheet and criteria in ExGrid's own sections for each declaration of ADR-0050 and
ADR-0051. Each criterion names its ADR and the test layer that discharges it. The same change puts
the release question to the user: `docs/definition-of-done.md` §2 says "finished" covers
ADR-0001 to ADR-0030 and places ExSheet out of scope. Do ADR-0050/0051's ExGrid declarations join
ExGrid's release, and what is ExSheet's own sign-off?

This changes the Definition of Done, so it is written by the orchestrator with the user, never by a
background agent (`AGENTS.md`, "Working in parallel").

**Blocked by:** None (can start immediately)

- [ ] Each ADR-0050 declaration and each ADR-0051 aid has criteria in ExGrid's sections, including "off by default leaves the grid unchanged"
- [ ] An ExSheet section covers ADR-0046 to ADR-0049, each criterion with its layer
- [ ] The release-scope question is answered by the user and recorded in §2
- [ ] §21 still lists no open question

## Comments
