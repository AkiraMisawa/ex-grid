# Agent skills

Vendored from [mattpocock/skills](https://github.com/mattpocock/skills) at commit
`c55ee46073ed923f86ce59a5eb3b6d895095d1b7`, unmodified. MIT licensed — see
`LICENSE-mattpocock-skills`.

| Skill | Role | Calls |
|---|---|---|
| `grill-with-docs` | Interview a plan, writing `CONTEXT.md` and ADRs as decisions land | `grilling`, `domain-modeling` |
| `grilling` | Relentless interview, one frontier of the design tree per round | |
| `domain-modeling` | Challenge terms, update `CONTEXT.md`, offer ADRs sparingly | |
| `to-spec` | Turn the conversation into a spec on the issue tracker | |
| `to-tickets` | Break a spec into tracer-bullet tickets with blocking edges | |
| `implement` | Build from a spec or tickets | `tdd`, `code-review` |
| `tdd` | Red → green at pre-agreed seams | `codebase-design` |
| `codebase-design` | Deep-module vocabulary (module, interface, seam, depth, …) | |
| `code-review` | Two-axis review: Standards and Spec | |
| `setup-matt-pocock-skills` | One-time setup writing `docs/agents/*.md` (issue tracker, labels, domain layout) | |

`to-spec`, `to-tickets` and `code-review` read `docs/agents/issue-tracker.md`; run
`/setup-matt-pocock-skills` once to create it.

In this repository the rules in `CLAUDE.md` take precedence over the skills — in particular,
a background agent never changes an ADR, `CONTEXT.md` or `docs/definition-of-done.md`, and
nothing is committed unless asked.

To update, re-copy the directories from a newer upstream commit and change the hash above.
