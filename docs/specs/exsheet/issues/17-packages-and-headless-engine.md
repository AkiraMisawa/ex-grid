# 17: Packaging, and the engine on a server

Status: ready-for-agent

**What to build:** `ExSheet` and `ExSheet.Engine` pack and publish through the release workflow as prereleases
(ADR-0042). The package smoke test restores them as a Consumer would. A test, or the DemoHost's
Server host, computes a saved Sheet Document's Values with `ExSheet.Engine` alone and gets the same
numbers the screen shows.

**Blocked by:** 05

- [ ] Both packages pack, with XML docs on every public member, and pass the package smoke check
- [ ] `ExSheet.Engine` loads with no Blazor dependency
- [ ] A saved document's Values computed headless equal the Values on screen (ADR-0047/0048)

## Comments
